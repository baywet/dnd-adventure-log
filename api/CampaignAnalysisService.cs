using System.ClientModel;
using System.Text.Json;
using OpenAI.Audio;
using OpenAI.Chat;
using OpenAI.Images;
using OpenAI.Responses;

namespace api;

public class CampaignAnalysisService : IAnalysisService
{
	public CampaignAnalysisService(CampaignStorageService storageService, CustomVideoClient customVideoClient, ChatClient chatClient, AudioClient audioClient, IHttpClientFactory httpClientFactory, ImageClient imageClient, ResponsesClient responsesClient)
	{
		ArgumentNullException.ThrowIfNull(storageService);
		ArgumentNullException.ThrowIfNull(customVideoClient);
		ArgumentNullException.ThrowIfNull(chatClient);
		ArgumentNullException.ThrowIfNull(audioClient);
		ArgumentNullException.ThrowIfNull(httpClientFactory);
		ArgumentNullException.ThrowIfNull(imageClient);
		ArgumentNullException.ThrowIfNull(responsesClient);
		_storageService = storageService;
		_customVideoClient = customVideoClient;
		_chatClient = chatClient;
		_audioClient = audioClient;
		_imageClient = imageClient;
		_httpClientFactory = httpClientFactory;
		_responsesClient = responsesClient;
	}

	private readonly CampaignStorageService _storageService;
	private readonly CustomVideoClient _customVideoClient;
	private readonly ChatClient _chatClient;
	private readonly AudioClient _audioClient;
	private readonly ImageClient _imageClient;
	private readonly IHttpClientFactory _httpClientFactory;
	private readonly ResponsesClient _responsesClient;

	public async Task<string> ExtractCharactersAsync(string campaignName, CancellationToken cancellationToken)
	{
		var transcript = await _storageService.GetCampaignTranscriptionForCharactersAsync(campaignName, cancellationToken).ConfigureAwait(false);
		if (string.IsNullOrEmpty(transcript))
		{
			throw new FileNotFoundException("No transcript found for this campaign.");
		}
		var response = await _responsesClient.CreateResponseAsync(
			new CreateResponseOptions
			(
				[
					ResponseItem.CreateSystemMessageItem(
						"""
						You are a note taker assisting a group of dungeons and dragons players tasked with recording and putting together recaps of each play session so the dungeon master and players can get insights from previous sessions.
						The transcripts provided to you might contain dialogues that are not relevant to the game, you should ignore those.
						"""
					),
					ResponseItem.CreateUserMessageItem(
						"""
						Based on the following transcript, give me a summary of the characters in this adventure in a form of 3 sentences per character. I'm interested in their cast, race, level, physical appearance and background.
						"""
					),
					ResponseItem.CreateUserMessageItem(transcript),
				]
			)
			{
				TextOptions = new ResponseTextOptions
				{
					TextFormat = ResponseTextFormat.CreateJsonSchemaFormat("characters", BinaryData.FromString
					(
						"""
						{
						    "type": "object",
						    "properties": {
						        "characters": {
									"type": "array",
									"items": {
										"type": "object",
										"properties": {
											"name": { "type": "string" },
											"description": { "type": "string" },
											"level": { "type": "integer", "nullable": true },
											"race": { "type": "string", "nullable": true }
										},
										"required": [
											"name",
											"description",
											"level",
											"race"
										],
										"additionalProperties": false
									}
								}
							},
							"required": [
								"characters"
							],
							"additionalProperties": false
						}
					"""
					))
				}
			},
			cancellationToken: cancellationToken
		).ConfigureAwait(false);
		var jsonContent = response.Value.GetOutputText() ?? throw new InvalidOperationException("Failed to extract characters.");
		await _storageService.SaveCharacterSummaryAsync(campaignName, jsonContent, cancellationToken).ConfigureAwait(false);
		return jsonContent;
	}

	public async Task<Stream> GenerateCharacterProfilePictureAsync(string campaignName, string characterName, CancellationToken cancellationToken)
	{
		using var fs = _storageService.GetCharacterSummary(campaignName) ?? throw new FileNotFoundException("Characters summary not found.");
		var characters = await JsonSerializer.DeserializeAsync<CharacterList>(fs, cancellationToken: cancellationToken).ConfigureAwait(false)
			?? throw new InvalidOperationException("Invalid character summary format.");
		var character = characters.characters.FirstOrDefault(c => characterName.Equals(c.name, StringComparison.OrdinalIgnoreCase)) ?? throw new FileNotFoundException("Character not found.");
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

		var stream = await GetImageStreamFromResult(result, cancellationToken).ConfigureAwait(false);
		await _storageService.SaveCharacterProfilePictureAsync(campaignName, characterName, stream, cancellationToken).ConfigureAwait(false);
		stream.Position = 0;
		return stream;
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

	public async Task<Stream> GenerateEpicMomentVideoAsync(string campaignName, string recordingName, CancellationToken cancellationToken)
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
				You are a movie director writing the script of an epic fantasy movie based on the adventures of a group of dungeons and dragons heroes.
				Your task is to analyze the transcript of one of their play sessions and extract the most epic moment of that session, then write a script for that moment as if it was an epic scene in a movie.
				Make sure to include dialogues, descriptions of the environment, characters' feelings and actions, and anything else that can make this scene really come to life.
				The script should be around 10 sentences long.
				"""),
			new UserChatMessage(transcription)
		], cancellationToken: cancellationToken).ConfigureAwait(false);
		var taleContent = result.Value.Content[0].Text;
		await _storageService.SaveEpicMomentTaleAsync(campaignName, recordingName, taleContent, cancellationToken).ConfigureAwait(false);
		var epicMomentVideo = await _customVideoClient.GetEpicMomentVideoAsync(taleContent, cancellationToken).ConfigureAwait(false)
			?? throw new InvalidOperationException("Failed to generate epic moment video.");
		await _storageService.SaveEpicMomentVideoAsync(campaignName, recordingName, epicMomentVideo, cancellationToken).ConfigureAwait(false);
		epicMomentVideo.Position = 0;
		return epicMomentVideo;
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