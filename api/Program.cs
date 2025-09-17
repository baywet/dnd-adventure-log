using Swashbuckle.AspNetCore.SwaggerUI;
using OpenAI.Audio;
using OpenAI;
using Azure.AI.OpenAI;
using Azure.Identity;
using api;
using OpenAI.Chat;
using System.Text.Json.Nodes;
using OpenAI.Images;
using System.ClientModel;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSingleton<AzureNamedServicesHolder>(_ =>
{
    AzureOpenAIClient createClient(string region) => new(
        new Uri(builder.Configuration[$"AzureOpenAI:{region}"] ??
                throw new InvalidOperationException($"Please set the AzureOpenAI:{region} configuration value.")),
        new DefaultAzureCredential());
    return new(new(StringComparer.OrdinalIgnoreCase)
    {
        { Constants.EastUS2Region, createClient(Constants.EastUS2Region) },
        { Constants.EastUSRegion, createClient(Constants.EastUSRegion) },
    });
});
builder.Services.AddSingleton(sp => sp.GetRequiredService<AzureNamedServicesHolder>()
                                        .GetService(Constants.EastUS2Region)
                                        .GetAudioClient("gpt-4o-transcribe"));

builder.Services.AddSingleton(sp => sp.GetRequiredService<AzureNamedServicesHolder>()
                                        .GetService(Constants.EastUS2Region)
                                        .GetChatClient("gpt-4o"));
builder.Services.AddSingleton(sp => sp.GetRequiredService<AzureNamedServicesHolder>()
                                        .GetService(Constants.EastUS2Region)
                                        .GetImageClient("gpt-image-1"));
builder.Services.AddSingleton(sp => new CustomVideoClient(
    builder.Configuration[$"AzureOpenAI:{Constants.EastUS2Region}"] ??
    throw new InvalidOperationException($"Please set the AzureOpenAI:{Constants.EastUS2Region} configuration value.")));
builder.Services.AddHttpClient();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.AddOpenAPI();
app.UseHttpsRedirection();
app.AddCampaignOperations();
app.AddRecordingOperations();

Directory.CreateDirectory(Constants.CampaignsDirectoryName);

app.MapPost(Constants.CharactersApiSegment, async (ChatClient client, CancellationToken cancellationToken) =>
{
    // get the first episode transcript file by oldest creation date first
    var transcriptFile = new DirectoryInfo(Constants.TranscriptionDirectoryName)
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
    var characterSummaryFile = Path.Combine(Constants.CharactersDirectoryName, Path.ChangeExtension(transcriptFile.Name, ".json"));
    await File.WriteAllTextAsync(characterSummaryFile, jsonContent.Trim('`')[4..].Trim(), cancellationToken).ConfigureAwait(false);
    return Results.File(characterSummaryFile, "application/json");
}).WithName("CreateCharacterSummary").WithOpenApi();

static string GetImageName(string recordingName, string characterName) =>
    $"{recordingName}-{characterName}.png";

app.MapGet($"{Constants.RecordingsApiSegment}/{{recordingName}}{Constants.CharactersApiSegment}", (string recordingName) =>
{
    if (Path.IsPathRooted(recordingName) || recordingName.Contains("..", StringComparison.Ordinal))
    {
        return Results.BadRequest("Invalid path.");
    }
    var charactersFile = Path.Combine(Constants.CharactersDirectoryName, $"{recordingName}.json");
    if (!File.Exists(charactersFile))
    {
        return Results.NotFound("Character not found.");
    }
    var fs = File.OpenRead(charactersFile);
    return Results.File(fs, "application/json");
}).WithName("GetCharacters").WithOpenApi();

app.MapPost($"{Constants.RecordingsApiSegment}/{{recordingName}}{Constants.CharactersApiSegment}/profile/{{characterName}}", async (string recordingName, string characterName, IHttpClientFactory httpClientFactory, ImageClient client, CancellationToken cancellationToken) =>
{
    if (Path.IsPathRooted(recordingName) || recordingName.Contains("..", StringComparison.Ordinal))
    {
        return Results.BadRequest("Invalid path.");
    }
    var charactersFile = Path.Combine(Constants.CharactersDirectoryName, $"{recordingName}.json");
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
    var imageName = GetImageName(recordingName, characterName);
    var imagePath = Path.Combine(Constants.CharactersDirectoryName, imageName);
    using var imageFile = File.Create(imagePath);
    await stream.CopyToAsync(imageFile, cancellationToken).ConfigureAwait(false);
    return Results.Created($"{Constants.RecordingsApiSegment}/{recordingName}{Constants.CharactersApiSegment}/profile/{characterName}", null);
}).WithName("CreateCharacterProfilePicture").WithOpenApi();

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

app.MapGet($"{Constants.RecordingsApiSegment}/{{recordingName}}{Constants.CharactersApiSegment}/profile/{{characterName}}", (string recordingName, string characterName) =>
{
    if (Path.IsPathRooted(recordingName) || recordingName.Contains("..", StringComparison.Ordinal))
    {
        return Results.BadRequest("Invalid path.");
    }
    var charactersFile = Path.Combine(Constants.CharactersDirectoryName, $"{recordingName}.json");
    if (!File.Exists(charactersFile))
    {
        return Results.NotFound("Character not found.");
    }
    var imageName = GetImageName(recordingName, characterName);
    var imagePath = Path.Combine(Constants.CharactersDirectoryName, imageName);
    if (!File.Exists(imagePath))
    {
        return Results.NotFound("Character image not found.");
    }
    var imageStream = File.OpenRead(imagePath);
    return Results.File(imageStream, "image/png");
}).WithName("GetCharacterProfilePicture").WithOpenApi();

await app.RunAsync();
