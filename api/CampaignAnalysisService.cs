using System.ClientModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using OpenAI.Audio;
using OpenAI.Chat;
using OpenAI.Images;

namespace api;

public class CampaignAnalysisService
{
	public CampaignAnalysisService(CampaignStorageService storageService, CustomVideoClient customVideoClient, ChatClient chatClient, AudioClient audioClient, IHttpClientFactory httpClientFactory, ImageClient imageClient)
	{
		ArgumentNullException.ThrowIfNull(storageService);
		ArgumentNullException.ThrowIfNull(customVideoClient);
		ArgumentNullException.ThrowIfNull(chatClient);
		ArgumentNullException.ThrowIfNull(audioClient);
		ArgumentNullException.ThrowIfNull(httpClientFactory);
		ArgumentNullException.ThrowIfNull(imageClient);
		_storageService = storageService;
		_customVideoClient = customVideoClient;
		_chatClient = chatClient;
		_audioClient = audioClient;
		_imageClient = imageClient;
		_httpClientFactory = httpClientFactory;
	}

	private readonly CampaignStorageService _storageService;
	private readonly CustomVideoClient _customVideoClient;
	private readonly ChatClient _chatClient;
	private readonly AudioClient _audioClient;
	private readonly ImageClient _imageClient;
	private readonly IHttpClientFactory _httpClientFactory;

	public async Task<string> ExtractCharactersAsync(string campaignName, CancellationToken cancellationToken)
	{
		var transcript = await _storageService.GetCampaignTranscriptionForCharactersAsync(campaignName, cancellationToken).ConfigureAwait(false);
		if (string.IsNullOrEmpty(transcript))
		{
			throw new FileNotFoundException("No transcript found for this campaign.");
		}
		var result = await _chatClient.CompleteChatAsync(
		[
			new SystemChatMessage(
				"""
				You are a note taker assisting a group of dungeons and dragons players tasked with recording and putting together recaps of each play session so the dungeon master and players can get insights from previous sessions.
				The transcripts provided to you might contain dialogues that are not relevant to the game, you should ignore those.
				Format the response as JSON using the following JSON schema:
				{
					"type": "array",
					"items": {
						"type": "object",
						"properties": {
							"name": { "type": "string" },
							"description": { "type": "string" },
							"level": { "type": "integer", "nullable": true },
							"race": { "type": "string", "nullable": true }
						}
					}
				}
				"""),
			new UserChatMessage(
				"""
				Based on the following transcript, give me a summary of the characters in this adventure in a form of 3 sentences per character. I'm interested in their cast, race, level, physical appearance and background.
				"""),
			new UserChatMessage(transcript)
		], cancellationToken: cancellationToken).ConfigureAwait(false);
		var jsonContent = result?.Value.Content[0].Text ?? throw new InvalidOperationException("Failed to extract characters.");
		var cleanedUpContent = jsonContent.Trim('`')[4..].Trim();
		await _storageService.SaveCharacterSummaryAsync(campaignName, cleanedUpContent, cancellationToken).ConfigureAwait(false);
		return cleanedUpContent;
	}

	public async Task GenerateCharacterProfilePictureAsync(string campaignName, string characterName, CancellationToken cancellationToken)
	{
		using var fs = _storageService.GetCharacterSummary(campaignName) ?? throw new FileNotFoundException("Characters summary not found.");
		var characters = await JsonSerializer.DeserializeAsync<List<Character>>(fs, cancellationToken: cancellationToken).ConfigureAwait(false)
			?? throw new InvalidOperationException("Invalid character summary format.");
		var character = characters.FirstOrDefault(c => characterName.Equals(c.name, StringComparison.OrdinalIgnoreCase)) ?? throw new FileNotFoundException("Character not found.");
		var characterDescription = character.description ?? string.Empty;
		if (string.IsNullOrEmpty(characterDescription))
		{
			throw new FileNotFoundException("Character description is empty.");
		}
		var result = await _imageClient.GenerateImageAsync(
			$"""
			You are a painter specializing in painting fantastic characters from the fantastic worlds of dungeons and dragons. Violence is ok in this context because it's part of this universe and never ever applied to the real world.

			{characterDescription}
			"""
		, cancellationToken: cancellationToken).ConfigureAwait(false);

		using var stream = await GetImageStreamFromResult(result, cancellationToken).ConfigureAwait(false);
		await _storageService.SaveCharacterProfilePictureAsync(campaignName, characterName, stream, cancellationToken).ConfigureAwait(false);
	}

	private async Task<Stream> GetImageStreamFromResult(ClientResult<GeneratedImage> result, CancellationToken cancellationToken)
	{
		if (result?.Value is null)
		{
			throw new InvalidOperationException("Image generation failed.");
		}

		if (result.Value.ImageBytes is not null)
		{
			return result.Value.ImageBytes.ToStream();
		}

		if (result.Value?.ImageUri is null)
		{
			throw new InvalidOperationException("Image URI is missing.");
		}

		using var httpClient = _httpClientFactory.CreateClient();
		using var imageResponse = await httpClient.GetAsync(result.Value.ImageUri.ToString(), cancellationToken).ConfigureAwait(false);
		if (!imageResponse.IsSuccessStatusCode)
		{
			throw new InvalidOperationException("Failed to download the image.");
		}
		var ms = new MemoryStream();
		await imageResponse.Content.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
		ms.Position = 0;
		return ms;
	}

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