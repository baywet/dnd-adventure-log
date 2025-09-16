using Swashbuckle.AspNetCore.SwaggerUI;
using OpenAI.Audio;
using OpenAI;
using Azure.AI.OpenAI;
using Azure.Identity;
using api;

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
const string transcriptionDirectoryName = "Transcriptions";
Directory.CreateDirectory(UploadDirectoryName);
Directory.CreateDirectory(transcriptionDirectoryName);

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
        var transcriptionPath = Path.Combine(transcriptionDirectoryName, Path.ChangeExtension(fileName, ".txt"));
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

app.MapDelete("/clean-app", () =>
{
    var uploadDirectoryInfo = new DirectoryInfo(UploadDirectoryName);
    foreach (var file in uploadDirectoryInfo.GetFiles())
    {
        file.Delete();
    }
    var transcriptionDirectoryInfo = new DirectoryInfo(transcriptionDirectoryName);
    foreach (var file in transcriptionDirectoryInfo.GetFiles())
    {
        file.Delete();
    }
    return Results.Accepted();
}).WithName("CleanApp").WithOpenApi();

await app.RunAsync();
