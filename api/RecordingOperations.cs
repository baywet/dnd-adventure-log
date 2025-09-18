namespace api;

public static class RecordingOperations
{
	public static void AddRecordingOperations(this WebApplication app)
	{
		app.MapPost($"{Constants.CampaignsApiSegment}/{{campaignName}}{Constants.RecordingsApiSegment}", async (HttpRequest request, string campaignName, CampaignAnalysisService analysisService, CancellationToken cancellationToken) =>
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

			var results = await analysisService.SaveRecordingsAndGenerateTranscriptionsAsync(campaignName, form.Files, cancellationToken).ConfigureAwait(false);

			return Results.Ok(results);
		}).WithName("UploadRecording").WithOpenApi().DisableRequestTimeout();

		app.MapGet($"{Constants.CampaignsApiSegment}/{{campaignName}}{Constants.RecordingsApiSegment}", (string campaignName, CampaignStorageService storageService) =>
		{
			var files = storageService.GetRecordings(campaignName)
				.ToArray();

			return Results.Ok(files);
		}).WithName("ListRecordings").WithOpenApi();

		app.MapPost($"{Constants.CampaignsApiSegment}/{{campaignName}}{Constants.RecordingsApiSegment}/{{recordingName}}{Constants.EpicMomentsApiSegment}", async (string campaignName, string recordingName, CampaignAnalysisService analysisService, CancellationToken cancellationToken) =>
		{
			try
			{
				await analysisService.GenerateEpicMomentVideoAsync(campaignName, recordingName, cancellationToken);
			}
			catch (FileNotFoundException ex)
			{
				return Results.NotFound(ex.Message);
			}
			catch (InvalidOperationException ex)
			{
				return Results.InternalServerError(ex.Message);
			}
			
			return Results.Created();
		}).WithName("CreateEpicMoment").WithOpenApi();

		app.MapGet($"{Constants.CampaignsApiSegment}/{{campaignName}}{Constants.RecordingsApiSegment}/{{recordingName}}{Constants.EpicMomentsApiSegment}", (string campaignName, string recordingName, CampaignStorageService storageService) =>
		{
			var epicMomentVideoStream = storageService.GetEpicMomentVideo(campaignName, recordingName);
			if (epicMomentVideoStream is null)
			{
				return Results.NotFound("Epic moment video not found.");
			}
			return Results.Stream(epicMomentVideoStream, "video/mp4");
		}).WithName("GetEpicMoment").WithOpenApi();
	}
}