using System.ClientModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using OpenAI.Chat;
using OpenAI.Images;

namespace api;

public static class CharactersOperations
{
	public static string GetCharactersRootPath(string campaignName) =>
		RecordingOperations.GetRecordingAssetsRootPath(campaignName, Constants.CharactersDirectoryName);
	public static string GetCharacterConfigFilePath(string campaignName) =>
		Path.Combine(GetCharactersRootPath(campaignName), "list.json");
	public static string GetCharacterProfilePicturePath(string campaignName, string characterName)
	{
		if (Path.IsPathRooted(characterName) || characterName.Contains("..", StringComparison.Ordinal))
		{
			throw new InvalidDataException("Name contains invalid characters.");
		}

		return Path.Combine(GetCharactersRootPath(campaignName), $"{characterName}.png");
	}
	public static void AddCharacterOperations(this WebApplication app)
	{
		app.MapPost($"{Constants.CampaignsApiSegment}/{{campaignName}}{Constants.CharactersApiSegment}", async (string campaignName, ChatClient client, CancellationToken cancellationToken) =>
		{
			// get the first episode transcript file by oldest creation date first
			var transcriptFile = new DirectoryInfo(RecordingOperations.GetTranscriptionsRootPath(campaignName))
				.GetFiles("*.txt")
				.OrderBy(static f => f.CreationTime)
				.FirstOrDefault();

			if (string.IsNullOrEmpty(transcriptFile?.FullName) || !File.Exists(transcriptFile.FullName))
			{
				return Results.NotFound("No transcription files found.");
			}
			var transcript = await File.ReadAllTextAsync(transcriptFile.FullName, cancellationToken).ConfigureAwait(false);

			var result = await client.CompleteChatAsync(
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
			var jsonContent = result.Value.Content[0].Text;
			var characterSummaryFile = GetCharacterConfigFilePath(campaignName);
			var charactersRootPath = GetCharactersRootPath(campaignName);
			if (!Directory.Exists(charactersRootPath))
			{
				Directory.CreateDirectory(charactersRootPath);
			}
			var cleanedUpContent = jsonContent.Trim('`')[4..].Trim();
			await File.WriteAllTextAsync(characterSummaryFile, cleanedUpContent, cancellationToken).ConfigureAwait(false);
			return Results.Content(cleanedUpContent, "application/json");
		}).WithName("CreateCharacterSummary").WithOpenApi();

		app.MapGet($"{Constants.CampaignsApiSegment}/{{campaignName}}{Constants.CharactersApiSegment}", (string campaignName) =>
		{
			var charactersFile = GetCharacterConfigFilePath(campaignName);
			if (!File.Exists(charactersFile))
			{
				return Results.NotFound("Character not found.");
			}
			var fs = File.OpenRead(charactersFile);
			return Results.File(fs, "application/json");
		}).WithName("GetCharacters").WithOpenApi();

		app.MapPost($"{Constants.CampaignsApiSegment}/{{campaignName}}{Constants.CharactersApiSegment}/profile/{{characterName}}", async (string campaignName, string characterName, IHttpClientFactory httpClientFactory, ImageClient client, CancellationToken cancellationToken) =>
		{
			var charactersFile = GetCharacterConfigFilePath(campaignName);
			if (!File.Exists(charactersFile))
			{
				return Results.NotFound("Character not found.");
			}
			using var fs = File.OpenRead(charactersFile);
			var characterJson = await JsonNode.ParseAsync(fs, cancellationToken: cancellationToken).ConfigureAwait(false);
			if (characterJson is not JsonArray characters)
			{
				return Results.NotFound("Character not found.");
			}
			var character = characters.FirstOrDefault(c =>
				c is JsonObject obj &&
				obj.TryGetPropertyValue("name", out var nameNode) &&
				nameNode is JsonValue jsonValue &&
				jsonValue.GetValueKind() is JsonValueKind.String &&
				jsonValue.GetValue<string>().Equals(characterName, StringComparison.OrdinalIgnoreCase));
			if (character is null)
			{
				return Results.NotFound("Character not found.");
			}
			var characterDescription = character["description"]?.ToString() ?? string.Empty;
			if (string.IsNullOrEmpty(characterDescription))
			{
				return Results.BadRequest("Character description is empty.");
			}
			var result = await client.GenerateImageAsync(
				$"""
				You are a painter specializing in painting fantastic characters from the fantastic worlds of dungeons and dragons. Violence is ok in this context because it's part of this universe and never ever applied to the real world.

				{characterDescription}
				"""
			, cancellationToken: cancellationToken).ConfigureAwait(false);

			using var stream = await GetImageStreamFromResult(result, httpClientFactory, cancellationToken).ConfigureAwait(false);
			var imagePath = GetCharacterProfilePicturePath(campaignName, characterName);
			using var imageFile = File.Create(imagePath);
			await stream.CopyToAsync(imageFile, cancellationToken).ConfigureAwait(false);
			return Results.Created($"{Constants.CampaignsApiSegment}/{campaignName}{Constants.CharactersApiSegment}/profile/{characterName}", null);
		}).WithName("CreateCharacterProfilePicture").WithOpenApi();

		app.MapGet($"{Constants.CampaignsApiSegment}/{{campaignName}}{Constants.CharactersApiSegment}/profile/{{characterName}}", (string campaignName, string characterName) =>
		{
			var imagePath = GetCharacterProfilePicturePath(campaignName, characterName);
			if (!File.Exists(imagePath))
			{
				return Results.NotFound("Character image not found.");
			}
			var imageStream = File.OpenRead(imagePath);
			return Results.File(imageStream, "image/png");
		}).WithName("GetCharacterProfilePicture").WithOpenApi();
	}

	static async Task<Stream> GetImageStreamFromResult(ClientResult<GeneratedImage> result, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken)
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

		using var httpClient = httpClientFactory.CreateClient();
		var imageResponse = await httpClient.GetAsync(result.Value.ImageUri.ToString(), cancellationToken).ConfigureAwait(false);
		if (!imageResponse.IsSuccessStatusCode)
		{
			throw new InvalidOperationException("Failed to download the image.");
		}
		return await imageResponse.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
	}
}