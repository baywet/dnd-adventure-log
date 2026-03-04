using System.ClientModel;
using System.Text;
using System.Text.Json;
using OpenAI.Videos;

namespace api;

public class CustomVideoClient
{
	private readonly string _modelName;
	private readonly VideoClient _videoClient;
	
	public CustomVideoClient(VideoClient videoClient, string modelName)
	{
		ArgumentNullException.ThrowIfNull(videoClient);
		ArgumentException.ThrowIfNullOrEmpty(modelName);
		_modelName = modelName;
		_videoClient = videoClient;
	}
	const string textPlainMimeType = "text/plain";
	public async Task<Stream?> GetEpicMomentVideoAsync(string recounting, CancellationToken cancellationToken)
	{
		var boundary = Guid.NewGuid().ToString();
		var contentType = $"multipart/form-data; boundary=\"{boundary}\"";

		using var multipart = new MultipartFormDataContent(boundary);

        multipart.Add(new StringContent(_modelName, Encoding.UTF8, textPlainMimeType), "model");
        multipart.Add(new StringContent(recounting, Encoding.UTF8, textPlainMimeType), "prompt");
        multipart.Add(new StringContent("1280x720", Encoding.UTF8, textPlainMimeType), "size");
        multipart.Add(new StringContent("8", Encoding.UTF8, textPlainMimeType), "seconds");

		using var bodyStream = await multipart.ReadAsStreamAsync(cancellationToken);

		var result = await _videoClient.CreateVideoAsync(BinaryContent.Create(bodyStream), contentType).ConfigureAwait(false);

		var createResult = await _videoClient.CreateVideoAsync(BinaryContent.Create(bodyStream), contentType);
        var createRaw = createResult.GetRawResponse().Content;

        using var createdDoc = JsonDocument.Parse(createRaw);
        var taskId = createdDoc.RootElement.GetProperty("id").GetString();
		
		return await PollForVideoGenerationStatus(taskId ?? throw new InvalidOperationException("Task ID is missing."), cancellationToken).ConfigureAwait(false);
	}
	private async Task<Stream?> PollForVideoGenerationStatus(string videoId, CancellationToken cancellationToken)
	{
		var result = await _videoClient.GetVideoAsync(videoId).ConfigureAwait(false);
		var getRaw = result.GetRawResponse().Content;
		using var getDoc = JsonDocument.Parse(getRaw);
        var status = getDoc.RootElement.GetProperty("status").GetString();

		if (string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase))
		{
			var videoDownload = await _videoClient.DownloadVideoAsync(videoId).ConfigureAwait(false);
			using var dlStream = videoDownload.GetRawResponse().ContentStream ?? throw new InvalidOperationException("Video stream is missing.");
			var ms = new MemoryStream();
			await dlStream.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
			ms.Position = 0;
			return ms;
		}
		else if (string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidOperationException("Video generation failed.");
		}
		else
		{
			await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
			return await PollForVideoGenerationStatus(videoId, cancellationToken).ConfigureAwait(false);
		}
	}
}