using System.ClientModel;
using System.Text.Json.Nodes;
using Azure.Core;

namespace api;

public class CustomVideoClient
{
	private readonly string _endpoint;
	private readonly IHttpClientFactory _httpClientFactory;
	private readonly string _modelName;
	private readonly TokenCredential? _tokenCredentials;
	private readonly ApiKeyCredential? _apiKeyCredential;
	
	public CustomVideoClient(string endpoint, IHttpClientFactory httpClientFactory, string modelName, ApiKeyCredential apiKeyCredential):this(endpoint, httpClientFactory, modelName)
	{
		ArgumentNullException.ThrowIfNull(apiKeyCredential);
		_apiKeyCredential = apiKeyCredential;
	}

	public CustomVideoClient(string endpoint, IHttpClientFactory httpClientFactory, string modelName, TokenCredential tokenCredentials) : this(endpoint, httpClientFactory, modelName)
	{
		ArgumentNullException.ThrowIfNull(tokenCredentials);
		_tokenCredentials = tokenCredentials;
	}

	public CustomVideoClient(string endpoint, IHttpClientFactory httpClientFactory, string modelName)
	{
		ArgumentException.ThrowIfNullOrEmpty(endpoint);
		ArgumentNullException.ThrowIfNull(httpClientFactory);
		ArgumentException.ThrowIfNullOrEmpty(modelName);
		_endpoint = endpoint;
		_httpClientFactory = httpClientFactory;
		_modelName = modelName;
	}
	private async Task SetBaseAuthenticationHeaderAsync(HttpClient client)
	{
		var apiKey = string.Empty;
		_apiKeyCredential?.Deconstruct(out apiKey);
		if (!string.IsNullOrEmpty(apiKey))
		{
			client.DefaultRequestHeaders.Add("api-key", apiKey);
			return;
		}
		if (_tokenCredentials != null)
		{
			var token = await _tokenCredentials.GetTokenAsync(
				new TokenRequestContext(new[] { "https://cognitiveservices.azure.com/.default" }),
				CancellationToken.None).ConfigureAwait(false);
			client.DefaultRequestHeaders.Authorization = new("Bearer", token.Token);
			return;
		}
		throw new InvalidOperationException("No valid authentication method configured.");
	}
	public async Task<Stream?> GetEpicMomentVideoAsync(string recounting, CancellationToken cancellationToken)
	{
		using var httpClient = _httpClientFactory.CreateClient();
		httpClient.BaseAddress = new Uri(_endpoint);
		await SetBaseAuthenticationHeaderAsync(httpClient).ConfigureAwait(false);
		using var response = await httpClient.PostAsJsonAsync("/openai/v1/video/generations/jobs?api-version=preview",
			new
			{
				model = _modelName,
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
			using var videoResponse = await client.GetAsync($"/openai/v1/video/generations/{videoId}/content/video?api-version=preview", cancellationToken).ConfigureAwait(false);
			videoResponse.EnsureSuccessStatusCode();
			var ms = new MemoryStream();
			await videoResponse.Content.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
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
			return await PollForVideoGenerationStatus(client, taskId, cancellationToken).ConfigureAwait(false);
		}
	}
}