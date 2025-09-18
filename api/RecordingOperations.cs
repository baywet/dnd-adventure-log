using OpenAI.Audio;
using OpenAI.Chat;

namespace api;

public static class RecordingOperations
{
	public static string GetRecordingsRootPath(string campaignName)
	{
		return GetRecordingAssetsRootPath(campaignName, Constants.RecordingsDirectoryName);
	}
	public static string GetTranscriptionsRootPath(string campaignName)
	{
		return GetRecordingAssetsRootPath(campaignName, Constants.TranscriptionDirectoryName);
	}
	public static string GetEpicMomentsRootPath(string campaignName)
	{
		return GetRecordingAssetsRootPath(campaignName, Constants.EpicMomentsDirectoryName);
	}
	public static string GetEpicMomentVideoPath(string campaignName, string recordingName) => Path.ChangeExtension(GetEpicMomentTextPath(campaignName, recordingName), ".mp4");
	public static string GetEpicMomentTextPath(string campaignName, string recordingName)
	{
		return Path.ChangeExtension(GetRecordingAssetPath(campaignName, recordingName, Constants.EpicMomentsDirectoryName), ".txt");
	}
	public static string GetRecordingAssetsRootPath(string campaignName, string assetType)
	{
		ArgumentException.ThrowIfNullOrEmpty(assetType);
		return Path.Combine(CampaignStorageService.GetCampaignRootPath(campaignName), assetType);
	}
	public static string GetRecordingAssetPath(string campaignName, string recordingName, string assetType)
	{
		if (Path.IsPathRooted(recordingName) || recordingName.Contains("..", StringComparison.Ordinal))
		{
			throw new InvalidDataException("Name contains invalid characters.");
		}
		return Path.Combine(GetRecordingAssetsRootPath(campaignName, assetType), recordingName);
	}
	public static string GetRecordingPath(string campaignName, string recordingName)
	{
		return Path.ChangeExtension(GetRecordingAssetPath(campaignName, recordingName, Constants.RecordingsDirectoryName), ".mp3");
	}
	public static string GetTranscriptionPath(string campaignName, string recordingName)
	{
		return Path.ChangeExtension(GetRecordingAssetPath(campaignName, recordingName, Constants.TranscriptionDirectoryName), ".txt");
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

		app.MapPost($"{Constants.CampaignsApiSegment}/{{campaignName}}{Constants.RecordingsApiSegment}/{{recordingName}}{Constants.EpicMomentsApiSegment}", async (string campaignName, string recordingName, CustomVideoClient customVideoClient, ChatClient client, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
		{
			var transcriptionFile = GetTranscriptionPath(campaignName, recordingName);
			if (!File.Exists(transcriptionFile))
			{
				return Results.NotFound("Transcription not found.");
			}
			var transcript = await File.ReadAllTextAsync(transcriptionFile, cancellationToken).ConfigureAwait(false);
			var result = await client.CompleteChatAsync(
			[
				new SystemChatMessage(
					"""
					You are a bard following a group of dungeons and dragons heroes and tasked with collecting tales of their most epic moments during their adventures.
					Analyze the transcript of this play session and extract a tale of an epic encounter. You might recount it as a 10 sentences story or ballad.
					Exaggerate the details and the facts to make it more interesting and entertaining.
					"""),
				new UserChatMessage(transcript)
			], cancellationToken: cancellationToken).ConfigureAwait(false);
			var taleContent = result.Value.Content[0].Text;
			var taleFile = GetEpicMomentTextPath(campaignName, recordingName);
			var taleDirectory = GetEpicMomentsRootPath(campaignName);
			if (!Directory.Exists(taleDirectory) && taleDirectory is not null)
			{
				Directory.CreateDirectory(taleDirectory);
			}
			await File.WriteAllTextAsync(taleFile, taleContent, cancellationToken).ConfigureAwait(false);
			using var epicMomentVideo = await customVideoClient.GetEpicMomentVideoAsync(taleContent, cancellationToken).ConfigureAwait(false);
			if (epicMomentVideo is null)
			{
				return Results.StatusCode(500);
			}
			var epicMomentVideoPath = GetEpicMomentVideoPath(campaignName, recordingName);
			using var videoFile = File.Create(epicMomentVideoPath);
			await epicMomentVideo.CopyToAsync(videoFile, cancellationToken).ConfigureAwait(false);
			return Results.Created();
		}).WithName("CreateEpicMoment").WithOpenApi();

		app.MapGet($"{Constants.CampaignsApiSegment}/{{campaignName}}{Constants.RecordingsApiSegment}/{{recordingName}}{Constants.EpicMomentsApiSegment}", (string campaignName, string recordingName) =>
		{
			var epicMomentVideoPath = GetEpicMomentVideoPath(campaignName, recordingName);
			if (!File.Exists(epicMomentVideoPath))
			{
				return Results.NotFound("Epic moment video not found.");
			}
			var videoStream = File.OpenRead(epicMomentVideoPath);
			return Results.File(videoStream, "video/mp4", Path.GetFileName(epicMomentVideoPath));
		}).WithName("GetEpicMoment").WithOpenApi();
	}
}