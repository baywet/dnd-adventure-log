namespace api;

public class CampaignStorageService
{
	public CampaignStorageService()
	{
	}

	public async Task<string> GetCampaignTranscriptionForCharactersAsync(string campaignName, CancellationToken cancellationToken)
	{
		// get the first episode transcript file by oldest creation date first
		var transcriptFile = new DirectoryInfo(RecordingOperations.GetTranscriptionsRootPath(campaignName))
			.GetFiles("*.txt")
			.OrderBy(static f => f.CreationTime)
			.FirstOrDefault();
		if (string.IsNullOrEmpty(transcriptFile?.FullName) || !File.Exists(transcriptFile.FullName))
		{
			return string.Empty;
		}
		var transcript = await File.ReadAllTextAsync(transcriptFile.FullName, cancellationToken).ConfigureAwait(false);
		return transcript;
	}

	public async Task SaveCharacterSummaryAsync(string campaignName, string jsonContent, CancellationToken cancellationToken)
	{
		var characterSummaryFile = GetCharacterConfigFilePath(campaignName);
		var charactersRootPath = GetCharactersRootPath(campaignName);
		if (!Directory.Exists(charactersRootPath))
		{
			Directory.CreateDirectory(charactersRootPath);
		}
		await File.WriteAllTextAsync(characterSummaryFile, jsonContent, cancellationToken).ConfigureAwait(false);
	}

	public Stream? GetCharacterSummary(string campaignName)
	{
		var characterSummaryFile = GetCharacterConfigFilePath(campaignName);
		if (!File.Exists(characterSummaryFile))
		{
			return null;
		}
		var fs = File.OpenRead(characterSummaryFile);
		return fs;
	}

	public Task SaveCharacterProfilePictureAsync(string campaignName, string characterName, Stream imageStream, CancellationToken cancellationToken)
	{
		var imagePath = GetCharacterProfilePicturePath(campaignName, characterName);
		using var imageFile = File.Create(imagePath);
		return imageStream.CopyToAsync(imageFile, cancellationToken);
	}
	public Stream? GetCharacterProfilePicture(string campaignName, string characterName)
	{
		var imagePath = GetCharacterProfilePicturePath(campaignName, characterName);
		if (!File.Exists(imagePath))
		{
			return null;
		}
		var imageStream = File.OpenRead(imagePath);
		return imageStream;
	}
	static string GetCharactersRootPath(string campaignName) =>
		RecordingOperations.GetRecordingAssetsRootPath(campaignName, Constants.CharactersDirectoryName);
	static string GetCharacterConfigFilePath(string campaignName) =>
		Path.Combine(GetCharactersRootPath(campaignName), "list.json");
	static string GetCharacterProfilePicturePath(string campaignName, string characterName)
	{
		if (Path.IsPathRooted(characterName) || characterName.Contains("..", StringComparison.Ordinal))
		{
			throw new InvalidDataException("Name contains invalid characters.");
		}

		return Path.Combine(GetCharactersRootPath(campaignName), $"{characterName}.png");
	}
}