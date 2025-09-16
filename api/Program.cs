using Swashbuckle.AspNetCore.SwaggerUI;
using OpenAI.Audio;
using OpenAI;
using Azure.AI.OpenAI;
using Azure.Identity;
using api;
using OpenAI.Chat;
using System.Text.Json.Nodes;
using OpenAI.Images;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
const string eastUS2Region = "EastUS2";
const string eastUSRegion = "EastUS";
builder.Services.AddSingleton<AzureNamedServicesHolder>(_ =>
{
    AzureOpenAIClient createClient(string region) => new(
        new Uri(builder.Configuration[$"AzureOpenAI:{region}"] ??
                throw new InvalidOperationException($"Please set the AzureOpenAI:{region} configuration value.")),
        new DefaultAzureCredential());
    return new(new(StringComparer.OrdinalIgnoreCase)
    {
        { eastUS2Region, createClient(eastUS2Region) },
        { eastUSRegion, createClient(eastUSRegion) },
    });
});
builder.Services.AddSingleton(sp => sp.GetRequiredService<AzureNamedServicesHolder>()
                                        .GetService(eastUS2Region)
                                        .GetAudioClient("gpt-4o-transcribe"));

builder.Services.AddSingleton(sp => sp.GetRequiredService<AzureNamedServicesHolder>()
                                        .GetService(eastUS2Region)
                                        .GetChatClient("gpt-4o"));
builder.Services.AddSingleton(sp => sp.GetRequiredService<AzureNamedServicesHolder>()
                                        .GetService(eastUSRegion)
                                        .GetImageClient("dall-e-3"));
builder.Services.AddHttpClient();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    const string apiDocsPath = "api-docs";
    app.UseSwaggerUI((options) =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "v1");
        options.RoutePrefix = apiDocsPath;
    });
    app.MapGet("/", () => Results.LocalRedirect($"/{apiDocsPath}")).ExcludeFromDescription();
}

app.UseHttpsRedirection();

const string UploadDirectoryName = "Uploads";
const string TranscriptionDirectoryName = "Transcriptions";
Directory.CreateDirectory(UploadDirectoryName);
Directory.CreateDirectory(TranscriptionDirectoryName);

const string recordingsApiSegment = "/recordings";

app.MapPost(recordingsApiSegment, async (HttpRequest request, AudioClient client, CancellationToken cancellationToken) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest("The request doesn't contain a form.");
    }

    var form = request.Form;

    if (form.Files.Count == 0)
    {
        return Results.BadRequest("No files were uploaded.");
    }
    var options = new AudioTranscriptionOptions
    {
        ResponseFormat = AudioTranscriptionFormat.Text,
        TimestampGranularities = AudioTimestampGranularities.Word | AudioTimestampGranularities.Segment,
    };

    var results = new List<object>();
    foreach (var file in form.Files)
    {
        // Process each uploaded file here
        // For example, you can save the file to a specific location
        var filePath = Path.Combine(UploadDirectoryName, file.FileName);

        using var fileStream = File.Create(filePath);
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
        ms.Position = 0;
        await ms.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
        ms.Position = 0;

        var fileName = Path.GetFileName(filePath);
        var transcription = await client.TranscribeAudioAsync(ms, fileName, options, cancellationToken).ConfigureAwait(false);
        var transcriptionPath = Path.Combine(TranscriptionDirectoryName, Path.ChangeExtension(fileName, ".txt"));
        await File.WriteAllTextAsync(transcriptionPath, transcription.Value.Text, cancellationToken).ConfigureAwait(false);
        results.Add(new { file = filePath, transcriptionFile = transcriptionPath });
    }

    return Results.Ok(results);
}).WithName("UploadRecording").WithOpenApi();

app.MapGet(recordingsApiSegment, () =>
{
    var files = Directory.GetFiles(UploadDirectoryName)
        .Select(filePath => new
        {
            FileName = Path.GetFileName(filePath),
            Url = $"/{UploadDirectoryName}/{Path.GetFileName(filePath)}"
        })
        .ToList();

    return Results.Ok(files);
}).WithName("ListRecordings").WithOpenApi();

const string CharactersDirectoryName = "Characters";
Directory.CreateDirectory(CharactersDirectoryName);
const string charactersApiSegment = "/characters";
app.MapPost(charactersApiSegment, async (ChatClient client, CancellationToken cancellationToken) =>
{
    // get the first episode transcript file by oldest creation date first
    var transcriptFile = new DirectoryInfo(TranscriptionDirectoryName)
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
    var characterSummaryFile = Path.Combine(CharactersDirectoryName, Path.ChangeExtension(transcriptFile.Name, ".json"));
    await File.WriteAllTextAsync(characterSummaryFile, jsonContent.Trim('`')[4..].Trim(), cancellationToken).ConfigureAwait(false);
    return Results.Created();
}).WithName("CreateCharacterSummary").WithOpenApi();

static string GetImageName(string recordingName, string characterName) =>
    $"{recordingName}-{characterName}.png";

app.MapPost($"{recordingsApiSegment}/{{recordingName}}{charactersApiSegment}/profile/{{characterName}}", async (string recordingName, string characterName, IHttpClientFactory httpClientFactory, ImageClient client, CancellationToken cancellationToken) =>
{
    if (Path.IsPathRooted(recordingName) || recordingName.Contains("..", StringComparison.Ordinal))
    {
        return Results.BadRequest("Invalid path.");
    }
    var charactersFile = Path.Combine(CharactersDirectoryName, $"{recordingName}.json");
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
        nameNode?.ToString().Equals(characterName, StringComparison.OrdinalIgnoreCase) == true);
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

    using var httpClient = httpClientFactory.CreateClient();
    var imageResponse = await httpClient.GetAsync(result.Value.ImageUri.ToString(), cancellationToken).ConfigureAwait(false);
    if (!imageResponse.IsSuccessStatusCode)
    {
        return Results.InternalServerError("Failed to download the image.");
    }
    using var stream = await imageResponse.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
    var imageName = GetImageName(recordingName, characterName);
    var imagePath = Path.Combine(CharactersDirectoryName, imageName);
    using var imageFile = File.Create(imagePath);
    await stream.CopyToAsync(imageFile, cancellationToken).ConfigureAwait(false);
    return Results.Created($"{recordingsApiSegment}/{recordingName}{charactersApiSegment}/profile/{characterName}", null);
}).WithName("CreateCharacterProfilePicture").WithOpenApi();

app.MapGet($"{recordingsApiSegment}/{{recordingName}}{charactersApiSegment}/profile/{{characterName}}", (string recordingName, string characterName, CancellationToken cancellationToken) =>
{
    if (Path.IsPathRooted(recordingName) || recordingName.Contains("..", StringComparison.Ordinal))
    {
        return Results.BadRequest("Invalid path.");
    }
    var charactersFile = Path.Combine(CharactersDirectoryName, $"{recordingName}.json");
    if (!File.Exists(charactersFile))
    {
        return Results.NotFound("Character not found.");
    }
    var imageName = GetImageName(recordingName, characterName);
    var imagePath = Path.Combine(CharactersDirectoryName, imageName);
    if (!File.Exists(imagePath))
    {
        return Results.NotFound("Character image not found.");
    }
    var imageStream = File.OpenRead(imagePath);
    return Results.File(imageStream, "image/png");
}).WithName("GetCharacterProfilePicture").WithOpenApi();



static void CleanUpDirectory(string directoryName)
{
    if (!Directory.Exists(directoryName))
    {
        return;
    }
    var directoryInfo = new DirectoryInfo(directoryName);
    foreach (var file in directoryInfo.GetFiles())
    {
        file.Delete();
    }
}

app.MapDelete("/clean-app", () =>
{
    CleanUpDirectory(UploadDirectoryName);
    CleanUpDirectory(TranscriptionDirectoryName);
    CleanUpDirectory(CharactersDirectoryName);
    return Results.Accepted();
}).WithName("CleanApp").WithOpenApi();


await app.RunAsync();
