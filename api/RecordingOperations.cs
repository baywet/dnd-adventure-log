using OpenAI.Audio;

namespace api;

public static class RecordingOperations
{
	public static string GetRecordingsRootPath(string campaignName)
	{
		return Path.Combine(CampaignOperations.GetCampaignRootPath(campaignName), Constants.RecordingsDirectoryName);
	}
	public static string GetTranscriptionsRootPath(string campaignName)
	{
		return Path.Combine(CampaignOperations.GetCampaignRootPath(campaignName), Constants.TranscriptionDirectoryName);
	}
	public static string GetRecordingRootPath(string campaignName, string recordingName)
	{
		if (Path.IsPathRooted(recordingName) || recordingName.Contains("..", StringComparison.Ordinal))
		{
			throw new InvalidDataException("Name contains invalid characters.");
		}
		return Path.Combine(GetRecordingsRootPath(campaignName), recordingName);
	}
	public static void AddRecordingOperations(this WebApplication app)
	{
		app.MapPost($"{Constants.CampaignsApiSegment}/{{campaignName}}{Constants.RecordingsApiSegment}", async (HttpRequest request, string campaignName, AudioClient client, CancellationToken cancellationToken) =>
		{
			// Set max request body size to 1 GB for this endpoint
			var maxRequestBodySizeFeature = request.HttpContext.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>();
			if (maxRequestBodySizeFeature != null && !maxRequestBodySizeFeature.IsReadOnly)
			{
				maxRequestBodySizeFeature.MaxRequestBodySize = 1_000_000_000; // 1 GB
			}
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
				var recordingsRootPath = GetRecordingsRootPath(campaignName);
				if (!Directory.Exists(recordingsRootPath))
				{
					Directory.CreateDirectory(recordingsRootPath);
				}
				var filePath = Path.Combine(recordingsRootPath, file.FileName);

				using var fileStream = File.Create(filePath);
				using var ms = new MemoryStream();
				await file.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
				ms.Position = 0;
				await ms.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
				ms.Position = 0;

				var fileName = Path.GetFileName(filePath);
				var transcription = await AudioHelper.ChunkAndMergeTranscriptsIfRequired(ms, fileName, options, client, cancellationToken).ConfigureAwait(false);
				var transcriptionsRootPath = GetTranscriptionsRootPath(campaignName);
				if (!Directory.Exists(transcriptionsRootPath))
				{
					Directory.CreateDirectory(transcriptionsRootPath);
				}
				var transcriptionPath = Path.Combine(transcriptionsRootPath, Path.ChangeExtension(fileName, ".txt"));
				await File.WriteAllTextAsync(transcriptionPath, transcription, cancellationToken).ConfigureAwait(false);
				results.Add(new { file = filePath, transcriptionFile = transcriptionPath });
			}

			return Results.Ok(results);
		}).WithName("UploadRecording").WithOpenApi().DisableRequestTimeout();

		app.MapGet($"{Constants.CampaignsApiSegment}/{{campaignName}}{Constants.RecordingsApiSegment}", (string campaignName) =>
		{
			var files = Directory.GetFiles(GetRecordingsRootPath(campaignName))
				.Select(filePath => new
				{
					FileName = Path.GetFileName(filePath),
					Url = $"{Constants.RecordingsApiSegment}/{Path.GetFileName(filePath)}"
				})
				.ToList();

			return Results.Ok(files);
		}).WithName("ListRecordings").WithOpenApi();
	}
}