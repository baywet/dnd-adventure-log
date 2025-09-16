using Swashbuckle.AspNetCore.SwaggerUI;
using OpenAI.Audio;
using OpenAI;
using System.ClientModel;
using Azure.AI.OpenAI;
using Azure.Identity;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSingleton(sp =>
{
    var azureClient = new AzureOpenAIClient(
        new Uri("https://vince-mflc0t6x-eastus2.cognitiveservices.azure.com"),
        new DefaultAzureCredential());
    return azureClient;
});
builder.Services.AddSingleton(sp =>
{
    var azureClient = sp.GetRequiredService<AzureOpenAIClient>();
    var client = azureClient.GetAudioClient("gpt-4o-transcribe");
    return client;
});

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
Directory.CreateDirectory(UploadDirectoryName);

const string recordingsApiSegment = "/recordings";

app.MapPost(recordingsApiSegment, async (HttpRequest request, CancellationToken cancellationToken) =>
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

    foreach (var file in form.Files)
    {
        // Process each uploaded file here
        // For example, you can save the file to a specific location
        var filePath = Path.Combine(UploadDirectoryName, file.FileName);

        using var stream = File.Create(filePath);
        await file.CopyToAsync(stream, cancellationToken).ConfigureAwait(false);

    }

    return Results.Ok("Files uploaded successfully.");
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

app.MapDelete("/clean-app", () =>
{
    var directoryInfo = new DirectoryInfo(UploadDirectoryName);
    foreach (var file in directoryInfo.GetFiles())
    {
        file.Delete();
    }
    return Results.Accepted();
}).WithName("CleanApp").WithOpenApi();

const string transcriptionDirectoryName = "Transcriptions";
// Transcribe endpoint
app.MapPost("/transcribe", async (AudioClient client, CancellationToken cancellationToken) =>
{
    Directory.CreateDirectory(transcriptionDirectoryName);

    var mp3Files = Directory.GetFiles(UploadDirectoryName, "*.mp3");
    if (mp3Files.Length == 0)
    {
        return Results.BadRequest("No mp3 files found in Uploads.");
    }

    var options = new AudioTranscriptionOptions
    {
        ResponseFormat = AudioTranscriptionFormat.Text,
        TimestampGranularities = AudioTimestampGranularities.Word | AudioTimestampGranularities.Segment,
    };

    var results = new List<object>();
    foreach (var filePath in mp3Files)
    {
        var transcription = await client.TranscribeAudioAsync(filePath, options);
        var transcriptionPath = Path.Combine(transcriptionDirectoryName, Path.ChangeExtension(Path.GetFileName(filePath), ".txt"));
        await File.WriteAllTextAsync(transcriptionPath, transcription.Value.Text, cancellationToken);
        results.Add(new { file = filePath, transcriptionFile = transcriptionPath });
    }
    return Results.Ok(results);
}).WithName("TranscribeRecordings").WithOpenApi();

await app.RunAsync();
