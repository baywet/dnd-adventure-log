using NAudio.Lame;
using NAudio.Wave;
using OpenAI.Audio;

namespace api;

public static class AudioHelper
{
	const int maxChunkSize = 26_214_400; // 25 MB
	/// <summary>
	/// Converts an MP3 MemoryStream to a lower bitrate MP3 MemoryStream.
	/// </summary>
	/// <param name="inputMp3Stream">Input MP3 stream</param>
	/// <param name="targetBitrateKbps">Target bitrate in kbps (e.g., 64)</param>
	/// <returns>MemoryStream containing lower bitrate MP3</returns>
	static async Task<MemoryStream> ConvertMp3ToLowerBitrate(MemoryStream inputMp3Stream, CancellationToken cancellationToken)
	{
		if (inputMp3Stream.Length <= maxChunkSize)
		{
			return inputMp3Stream;
		}
		inputMp3Stream.Position = 0;
		using var mp3Reader = new Mp3FileReader(inputMp3Stream);
		using var pcmStream = WaveFormatConversionStream.CreatePcmStream(mp3Reader);
		var outStream = new MemoryStream();
		using var lame = new LameMP3FileWriter(outStream, pcmStream.WaveFormat, LAMEPreset.ABR_64);
		await pcmStream.CopyToAsync(lame, cancellationToken).ConfigureAwait(false);
		await lame.FlushAsync(cancellationToken).ConfigureAwait(false);
		outStream.Position = 0;
		return outStream;
	}

	const int chunkDurationSeconds = 20 * 60; // 20 minutes
	public static async Task<string> ChunkAndMergeTranscriptsIfRequired(MemoryStream originalStream, string fileName, AudioTranscriptionOptions options, AudioClient client, CancellationToken cancellationToken)
	{

		originalStream.Position = 0;
		using var mp3Reader = new Mp3FileReader(originalStream);
		var totalDuration = mp3Reader.TotalTime.TotalSeconds;

		if (totalDuration <= chunkDurationSeconds)
		{
			originalStream.Position = 0;
			using var uploadStream = await ConvertMp3ToLowerBitrate(originalStream, cancellationToken).ConfigureAwait(false);
			var transcription = await client.TranscribeAudioAsync(uploadStream, fileName, options, cancellationToken).ConfigureAwait(false);
			return transcription.Value.Text;
		}

		// Split into 25-minute chunks
		int chunkIndex = 0;
		var chunks = new List<Tuple<string, MemoryStream>>();
		while (mp3Reader.CurrentTime.TotalSeconds < totalDuration)
		{
			var chunkStart = mp3Reader.CurrentTime;
			var chunkEnd = TimeSpan.FromSeconds(Math.Min(chunkStart.TotalSeconds + chunkDurationSeconds, totalDuration));
			var chunkFileName = $"{Path.GetFileNameWithoutExtension(fileName)}-chunk{chunkIndex}.mp3";

			var chunkStream = new MemoryStream();
			Mp3Frame frame;
			while ((frame = mp3Reader.ReadNextFrame()) != null)
			{
				var frameTime = mp3Reader.CurrentTime;
				if (frameTime > chunkEnd)
					break;
				await chunkStream.WriteAsync(frame.RawData.AsMemory(0, frame.RawData.Length), cancellationToken).ConfigureAwait(false);
			}
			chunkStream.Position = 0;
			var convertedChunkStream = await ConvertMp3ToLowerBitrate(chunkStream, cancellationToken).ConfigureAwait(false);
			if (convertedChunkStream != chunkStream)
			{
				await chunkStream.DisposeAsync();
			}

			chunks.Add(new(chunkFileName, convertedChunkStream));
			chunkIndex++;
		}
		var transcriptions = (await Task.WhenAll(chunks.Select((c) => client.TranscribeAudioAsync(c.Item2, c.Item1, options, cancellationToken))).ConfigureAwait(false))
							.Select(static t => t.Value.Text)
							.ToArray();

		foreach (var chunk in chunks.Select(static c => c.Item2))
		{
			await chunk.DisposeAsync();
		}

		return string.Join("\n", transcriptions);
	}

}