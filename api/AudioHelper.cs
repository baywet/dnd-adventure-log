using NAudio.SoundFile;
using OpenAI.Audio;

namespace api;

public static class AudioHelper
{
	const int maxChunkSize = 26_214_400; // 25 MB
	/// <summary>
	/// Converts an MP3 MemoryStream to a smaller MP3 MemoryStream.
	/// </summary>
	/// <param name="inputMp3Stream">Input MP3 stream</param>
	/// <returns>MemoryStream containing compressed MP3</returns>
	static MemoryStream ConvertMp3ToLowerBitrate(MemoryStream inputMp3Stream, CancellationToken cancellationToken)
	{
		if (inputMp3Stream.Length <= maxChunkSize)
		{
			return inputMp3Stream;
		}
		inputMp3Stream.Position = 0;
		using var audioReader = new SoundFileReader(inputMp3Stream);
		var outStream = new MemoryStream();
		WriteMp3Stream(audioReader, outStream, null, cancellationToken);
		outStream.Position = 0;
		return outStream;
	}

	static void WriteMp3Stream(SoundFileReader audioReader, Stream outStream, long? samplesToWrite, CancellationToken cancellationToken)
	{
		using var writer = new SoundFileWriter(outStream, audioReader.WaveFormat, SoundFileMajorFormat.Mp3, new()
		{
			Subtype = SoundFileSubtype.Mp3,
			VbrQuality = 0.25,
		});
		var channels = audioReader.WaveFormat.Channels;
		var buffer = new float[(64 * 1024 / channels) * channels];
		long samplesRemaining = samplesToWrite ?? long.MaxValue;
		while (samplesRemaining > 0)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var samplesRequested = (int)Math.Min(buffer.Length, samplesRemaining);
			var samplesRead = audioReader.Read(buffer.AsSpan(0, samplesRequested));
			if (samplesRead == 0)
			{
				break;
			}
			writer.WriteSamples(buffer.AsSpan(0, samplesRead));
			samplesRemaining -= samplesRead;
		}
	}

	const int chunkDurationSeconds = 20 * 60; // 20 minutes
	public static async Task<string> ChunkAndMergeTranscriptsIfRequired(MemoryStream originalStream, string fileName, AudioTranscriptionOptions options, AudioClient client, CancellationToken cancellationToken)
	{

		originalStream.Position = 0;
		using var audioReader = new SoundFileReader(originalStream);
		var totalDuration = audioReader.TotalTime.TotalSeconds;

		if (totalDuration <= chunkDurationSeconds)
		{
			originalStream.Position = 0;
			using var uploadStream = ConvertMp3ToLowerBitrate(originalStream, cancellationToken);
			var transcription = await client.TranscribeAudioAsync(uploadStream, fileName, options, cancellationToken).ConfigureAwait(false);
			return transcription.Value.Text;
		}

		// Split into 20-minute chunks
		int chunkIndex = 0;
		var chunks = new List<Tuple<string, MemoryStream>>();
		var samplesPerChunk = (long)audioReader.WaveFormat.SampleRate * audioReader.WaveFormat.Channels * chunkDurationSeconds;
		var totalSamples = audioReader.Length / sizeof(float);
		while (audioReader.Position < audioReader.Length)
		{
			var chunkFileName = $"{Path.GetFileNameWithoutExtension(fileName)}-chunk{chunkIndex}.mp3";

			var chunkStream = new MemoryStream();
			var samplesRemaining = totalSamples - (audioReader.Position / sizeof(float));
			WriteMp3Stream(audioReader, chunkStream, Math.Min(samplesPerChunk, samplesRemaining), cancellationToken);
			chunkStream.Position = 0;
			chunks.Add(new(chunkFileName, chunkStream));
			chunkIndex++;
		}

		try
		{
			var transcriptions = (await Task.WhenAll(chunks.Select((c) => client.TranscribeAudioAsync(c.Item2, c.Item1, options, cancellationToken))).ConfigureAwait(false))
								.Select(static t => t.Value.Text)
								.ToArray();

			return string.Join("\n", transcriptions);
		}
		finally
		{
			foreach (var chunk in chunks.Select(static c => c.Item2))
			{
				await chunk.DisposeAsync();
			}
		}
	}

}
