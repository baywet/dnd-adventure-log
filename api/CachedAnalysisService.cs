namespace api;

public class CachedAnalysisService : IAnalysisService
{
	private readonly CampaignAnalysisService _concreteService;
	private readonly CampaignStorageService _storageService;

	public CachedAnalysisService(CampaignAnalysisService concreteService, CampaignStorageService storageService)
	{
		ArgumentNullException.ThrowIfNull(concreteService);
		ArgumentNullException.ThrowIfNull(storageService);
		_concreteService = concreteService;
		_storageService = storageService;
	}

	public async Task<string> ExtractCharactersAsync(string campaignName, CancellationToken cancellationToken)
	{
		using var existingCharacters = _storageService.GetCharacterSummary(campaignName);
		if (existingCharacters is not null)
		{
			using var reader = new StreamReader(existingCharacters);
			var existingJson = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
			return existingJson;
		}
		var resultJson = await _concreteService.ExtractCharactersAsync(campaignName, cancellationToken).ConfigureAwait(false);
		return resultJson;
	}

	public Task GenerateCharacterProfilePictureAsync(string campaignName, string characterName, CancellationToken cancellationToken)
	{
		using var existingImage = _storageService.GetCharacterProfilePicture(campaignName, characterName);
		if (existingImage is not null)
		{
			return Task.CompletedTask;
		}
		return _concreteService.GenerateCharacterProfilePictureAsync(campaignName, characterName, cancellationToken);
	}

	public Task GenerateEpicMomentVideoAsync(string campaignName, string recordingName, CancellationToken cancellationToken)
	{
		using var existingVideo = _storageService.GetEpicMomentVideo(campaignName, recordingName);
		if (existingVideo is not null)
		{
			return Task.CompletedTask;
		}
		return _concreteService.GenerateEpicMomentVideoAsync(campaignName, recordingName, cancellationToken);
	}

	public Task<string[]> SaveRecordingsAndGenerateTranscriptionsAsync(string campaignName, IFormFileCollection form, CancellationToken cancellationToken)
	{
		var recordings = _storageService.GetRecordings(campaignName)?.ToHashSet(StringComparer.Ordinal);
		var formFiles = form.Select(f => Path.GetFileNameWithoutExtension(f.FileName)).ToHashSet(StringComparer.Ordinal);
		if (recordings is not null && formFiles.All(recordings.Contains) && recordings.Count == formFiles.Count)
		{
			return Task.FromResult(recordings.ToArray());
		}
		return _concreteService.SaveRecordingsAndGenerateTranscriptionsAsync(campaignName, form, cancellationToken);
	}
}