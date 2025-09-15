var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
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

await app.RunAsync();

