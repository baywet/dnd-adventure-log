using OpenAI.Audio;
using OpenAI.Chat;

namespace api;

public static class RecordingOperations
{
	public static void AddRecordingOperations(this WebApplication app)
	{
		app.MapPost($"{Constants.CampaignsApiSegment}/{{campaignName}}{Constants.RecordingsApiSegment}", async (HttpRequest request, string campaignName, CampaignStorageService storageService, AudioClient client, CancellationToken cancellationToken) =>
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

			var results = new List<string>();
			foreach (var file in form.Files)
			{
				using var ms = new MemoryStream();
				await file.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
				ms.Position = 0;
				var fileName = await storageService.SaveRecordingAsync(campaignName, file.FileName, ms, cancellationToken).ConfigureAwait(false);
				ms.Position = 0;

				var transcription = await AudioHelper.ChunkAndMergeTranscriptsIfRequired(ms, fileName, options, client, cancellationToken).ConfigureAwait(false);
				await storageService.SaveTranscriptionAsync(campaignName, fileName, transcription, cancellationToken).ConfigureAwait(false);
				results.Add(Path.GetFileNameWithoutExtension(fileName));
			}

			return Results.Ok(results);
		}).WithName("UploadRecording").WithOpenApi().DisableRequestTimeout();

		app.MapGet($"{Constants.CampaignsApiSegment}/{{campaignName}}{Constants.RecordingsApiSegment}", (string campaignName, CampaignStorageService storageService) =>
		{
			var files = storageService.GetRecordings(campaignName)
				.ToArray();

			return Results.Ok(files);
		}).WithName("ListRecordings").WithOpenApi();

		app.MapPost($"{Constants.CampaignsApiSegment}/{{campaignName}}{Constants.RecordingsApiSegment}/{{recordingName}}{Constants.EpicMomentsApiSegment}", async (string campaignName, string recordingName, CampaignStorageService storageService, CustomVideoClient customVideoClient, ChatClient client, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
		{
			var transcription = await storageService.GetTranscriptionAsync(campaignName, recordingName, cancellationToken);
			if (string.IsNullOrEmpty(transcription))
			{
				return Results.NotFound("Transcription not found.");
			}
			var result = await client.CompleteChatAsync(
			[
				new SystemChatMessage(
					"""
					You are a bard following a group of dungeons and dragons heroes and tasked with collecting tales of their most epic moments during their adventures.
					Analyze the transcript of this play session and extract a tale of an epic encounter. You might recount it as a 10 sentences story or ballad.
					Exaggerate the details and the facts to make it more interesting and entertaining.
					"""),
				new UserChatMessage(transcription)
			], cancellationToken: cancellationToken).ConfigureAwait(false);
			var taleContent = result.Value.Content[0].Text;
			await storageService.SaveEpicMomentTaleAsync(campaignName, recordingName, taleContent, cancellationToken).ConfigureAwait(false);
			using var epicMomentVideo = await customVideoClient.GetEpicMomentVideoAsync(taleContent, cancellationToken).ConfigureAwait(false);
			if (epicMomentVideo is null)
			{
				return Results.StatusCode(500);
			}
			await storageService.SaveEpicMomentVideoAsync(campaignName, recordingName, epicMomentVideo, cancellationToken).ConfigureAwait(false);
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