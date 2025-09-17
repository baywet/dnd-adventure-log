using System.Text.Json.Nodes;
using Azure.Identity;

namespace api;

public class CustomVideoClient
{
	private readonly string _endpoint;
	private readonly IHttpClientFactory _httpClientFactory;

	public CustomVideoClient(string endpoint, IHttpClientFactory httpClientFactory)
	{
		ArgumentException.ThrowIfNullOrEmpty(endpoint);
		ArgumentNullException.ThrowIfNull(httpClientFactory);
		_endpoint = endpoint;
		_httpClientFactory = httpClientFactory;
	}
	public async Task<Stream?> GetEpicMomentVideoAsync(string recounting, CancellationToken cancellationToken)
	{
		using var httpClient = _httpClientFactory.CreateClient();
		httpClient.BaseAddress = new Uri(_endpoint);
		var credentials = new DefaultAzureCredential();
		var token = await credentials.GetTokenAsync(
			new Azure.Core.TokenRequestContext(new[] { "https://cognitiveservices.azure.com/.default" }),
			cancellationToken).ConfigureAwait(false);
		httpClient.DefaultRequestHeaders.Authorization = new("Bearer", token.Token);
		using var response = await httpClient.PostAsJsonAsync("/openai/v1/video/generations/jobs?api-version=preview",
			new
			{
				model = "sora",
				prompt = recounting,
				width = 1920,
				height = 1080,
				n_seconds = 15,
				format = "mp4"
			}, cancellationToken).ConfigureAwait(false);
		if (!response.IsSuccessStatusCode)
		{
			var errorContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
			throw new InvalidOperationException($"Video generation request failed with status code {response.StatusCode}: {errorContent}");
		}
		var responseJson = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken: cancellationToken).ConfigureAwait(false);
		var taskId = responseJson?["id"]?.ToString();
		return await PollForVideoGenerationStatus(httpClient, taskId ?? throw new InvalidOperationException("Task ID is missing."), cancellationToken).ConfigureAwait(false);
	}
	private async static Task<Stream?> PollForVideoGenerationStatus(HttpClient client, string taskId, CancellationToken cancellationToken)
	{
		var response = await client.GetAsync($"/openai/v1/video/generations/jobs/{taskId}?api-version=preview", cancellationToken).ConfigureAwait(false);
		if (!response.IsSuccessStatusCode)
		{
			var errorContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
			throw new InvalidOperationException($"Video generation status request failed with status code {response.StatusCode}: {errorContent}");
		}
		var responseJson = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken: cancellationToken).ConfigureAwait(false);
		var status = responseJson?["status"]?.ToString();
		if (string.Equals(status, "succeeded", StringComparison.OrdinalIgnoreCase))
		{
			var videoId = responseJson?["generations"]?[0]?["id"]?.ToString();
			if (string.IsNullOrEmpty(videoId))
			{
				throw new InvalidOperationException("Video ID is missing.");
			}
			var videoResponse = await client.GetAsync($"/openai/v1/video/generations/{videoId}/content/video?api-version=preview", cancellationToken).ConfigureAwait(false);
			videoResponse.EnsureSuccessStatusCode();
			return await videoResponse.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
		}
		else if (string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidOperationException("Video generation failed.");
		}
		else
		{
			await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
			return await PollForVideoGenerationStatus(client, taskId, cancellationToken).ConfigureAwait(false);
		}
	}
}