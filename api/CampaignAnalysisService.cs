using OpenAI.Audio;
using OpenAI.Chat;

namespace api;

public class CampaignAnalysisService
{
	public CampaignAnalysisService(CampaignStorageService storageService, CustomVideoClient customVideoClient, ChatClient chatClient, AudioClient audioClient)
	{
		ArgumentNullException.ThrowIfNull(storageService);
		ArgumentNullException.ThrowIfNull(customVideoClient);
		ArgumentNullException.ThrowIfNull(chatClient);
		ArgumentNullException.ThrowIfNull(audioClient);
		_storageService = storageService;
		_customVideoClient = customVideoClient;
		_chatClient = chatClient;
		_audioClient = audioClient;
	}

	private readonly CampaignStorageService _storageService;
	private readonly CustomVideoClient _customVideoClient;
	private readonly ChatClient _chatClient;
	private readonly AudioClient _audioClient;

	public async Task GenerateEpicMomentVideoAsync(string campaignName, string recordingName, CancellationToken cancellationToken)
	{
		var transcription = await _storageService.GetTranscriptionAsync(campaignName, recordingName, cancellationToken);
		if (string.IsNullOrEmpty(transcription))
		{
			throw new FileNotFoundException("Transcription not found.");
		}
		var result = await _chatClient.CompleteChatAsync(
		[
			new SystemChatMessage(
				"""
				You are a bard following a group of dungeons and dragons heroes and tasked with collecting tales of their most epic moments during their adventures.
				Analyze the transcript of this play session and extract a tale of an epic encounter. You might recount it as a 10 sentences story or ballad.
				Exaggerate the details and the facts to make it more interesting and entertaining.
				"""),
			new UserChatMessage(transcription)
		], cancellationToken: cancellationToken).ConfigureAwait(false);
		var taleContent = result.Value.Content[0].Text;
		await _storageService.SaveEpicMomentTaleAsync(campaignName, recordingName, taleContent, cancellationToken).ConfigureAwait(false);
		using var epicMomentVideo = await _customVideoClient.GetEpicMomentVideoAsync(taleContent, cancellationToken).ConfigureAwait(false)
									?? throw new InvalidOperationException("Failed to generate epic moment video.");
		await _storageService.SaveEpicMomentVideoAsync(campaignName, recordingName, epicMomentVideo, cancellationToken).ConfigureAwait(false);
	}

	public async Task<string[]> SaveRecordingsAndGenerateTranscriptionsAsync(string campaignName, IFormFileCollection form, CancellationToken cancellationToken)
	{
		var options = new AudioTranscriptionOptions
		{
			ResponseFormat = AudioTranscriptionFormat.Text,
			TimestampGranularities = AudioTimestampGranularities.Word | AudioTimestampGranularities.Segment,
		};

		var results = new List<string>();
		foreach (var file in form)
		{
			using var ms = new MemoryStream();
			await file.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
			ms.Position = 0;
			var fileName = await _storageService.SaveRecordingAsync(campaignName, file.FileName, ms, cancellationToken).ConfigureAwait(false);
			ms.Position = 0;

			var transcription = await AudioHelper.ChunkAndMergeTranscriptsIfRequired(ms, fileName, options, _audioClient, cancellationToken).ConfigureAwait(false);
			await _storageService.SaveTranscriptionAsync(campaignName, fileName, transcription, cancellationToken).ConfigureAwait(false);
			results.Add(Path.GetFileNameWithoutExtension(fileName));
		}
		return results.ToArray();
	}

}