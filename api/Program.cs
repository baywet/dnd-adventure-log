using Swashbuckle.AspNetCore.SwaggerUI;
using OpenAI.Audio;
using OpenAI;
using Azure.AI.OpenAI;
using Azure.Identity;
using api;
using OpenAI.Chat;

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

app.MapDelete($"{recordingsApiSegment}/{{fileName}}", (string fileName) =>
{
    if (Path.IsPathRooted(fileName) || fileName.Contains(".."))
    {
        return Results.BadRequest("Invalid file name.");
    }
    var filePath = Path.Combine(UploadDirectoryName, fileName);

    if (!File.Exists(filePath))
    {
        return Results.NotFound("File not found.");
    }

    File.Delete(filePath);
    return Results.Ok("File deleted successfully.");
}).WithName("DeleteRecording").WithOpenApi();

const string CharactersDirectoryName = "Characters";
Directory.CreateDirectory(CharactersDirectoryName);
app.MapPost("/characters", async (ChatClient client, CancellationToken cancellationToken) =>
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
