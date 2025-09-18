namespace api;

public class CampaignStorageService
{
	public CampaignStorageService()
	{
	}

	public string[] ListCampaigns()
	{
		return Directory.GetDirectories(Constants.CampaignsDirectoryName)
				.Select(filePath => Path.GetFileName(filePath))
				.ToArray();
	}

	public bool CreateCampaign(string campaignName)
	{
		var campaignPath = GetCampaignRootPath(campaignName);
		if (Directory.Exists(campaignPath))
		{
			return false;
		}
		Directory.CreateDirectory(campaignPath);
		return true;
	}

	public bool DeleteCampaign(string campaignName)
	{
		var campaignPath = GetCampaignRootPath(campaignName);
		if (!Directory.Exists(campaignPath))
		{
			return false;
		}
		Directory.Delete(campaignPath, true);
		return true;
	}

	public async Task<string> GetCampaignTranscriptionForCharactersAsync(string campaignName, CancellationToken cancellationToken)
	{
		// get the first episode transcript file by oldest creation date first
		var transcriptFile = new DirectoryInfo(GetTranscriptionsRootPath(campaignName))
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

	public async Task SaveCharacterProfilePictureAsync(string campaignName, string characterName, Stream imageStream, CancellationToken cancellationToken)
	{
		var imagePath = GetCharacterProfilePicturePath(campaignName, characterName);
		using var imageFile = File.Create(imagePath);
		await imageStream.CopyToAsync(imageFile, cancellationToken).ConfigureAwait(false);
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

	public async Task<string> SaveRecordingAsync(string campaignName, string recordingName, Stream recordingStream, CancellationToken cancellationToken)
	{
		var recordingsRootPath = GetRecordingsRootPath(campaignName);
		if (!Directory.Exists(recordingsRootPath))
		{
			Directory.CreateDirectory(recordingsRootPath);
		}
		var recordingPath = GetRecordingPath(campaignName, recordingName);
		using var fileStream = File.Create(recordingPath);
		await recordingStream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
		return Path.GetFileName(recordingPath);
	}
	public async Task SaveTranscriptionAsync(string campaignName, string recordingName, string transcriptionContent, CancellationToken cancellationToken)
	{
		var transcriptionsRootPath = GetTranscriptionsRootPath(campaignName);
		if (!Directory.Exists(transcriptionsRootPath))
		{
			Directory.CreateDirectory(transcriptionsRootPath);
		}
		var transcriptionPath = GetTranscriptionPath(campaignName, recordingName);
		await File.WriteAllTextAsync(transcriptionPath, transcriptionContent, cancellationToken).ConfigureAwait(false);
	}
	public async Task<string> GetTranscriptionAsync(string campaignName, string recordingName, CancellationToken cancellationToken)
	{
		var transcriptionPath = GetTranscriptionPath(campaignName, recordingName);
		if (!File.Exists(transcriptionPath))
		{
			return string.Empty;
		}
		var transcription = await File.ReadAllTextAsync(transcriptionPath, cancellationToken).ConfigureAwait(false);
		return transcription;
	}
	public async Task SaveEpicMomentTaleAsync(string campaignName, string recordingName, string taleContent, CancellationToken cancellationToken)
	{
		var taleFile = GetEpicMomentTextPath(campaignName, recordingName);
		var taleDirectory = GetEpicMomentsRootPath(campaignName);
		if (!Directory.Exists(taleDirectory) && taleDirectory is not null)
		{
			Directory.CreateDirectory(taleDirectory);
		}
		await File.WriteAllTextAsync(taleFile, taleContent, cancellationToken).ConfigureAwait(false);
	}
	public async Task SaveEpicMomentVideoAsync(string campaignName, string recordingName, Stream videoStream, CancellationToken cancellationToken)
	{
		var epicMomentVideoPath = GetEpicMomentVideoPath(campaignName, recordingName);
		using var videoFile = File.Create(epicMomentVideoPath);
		await videoStream.CopyToAsync(videoFile, cancellationToken).ConfigureAwait(false);
	}
	public string[] GetRecordings(string campaignName)
	{
		var recordingsRootPath = GetRecordingsRootPath(campaignName);
		if (!Directory.Exists(recordingsRootPath))
		{
			return [];
		}
		return Directory.GetFiles(recordingsRootPath)
				.Select(filePath => Path.GetFileNameWithoutExtension(filePath))
				.ToArray();
	}
	public Stream? GetEpicMomentVideo(string campaignName, string recordingName)
	{
		var epicMomentVideoPath = GetEpicMomentVideoPath(campaignName, recordingName);
		if (!File.Exists(epicMomentVideoPath))
		{
			return null;
		}
		var videoStream = File.OpenRead(epicMomentVideoPath);
		return videoStream;
	}
	static string GetCharactersRootPath(string campaignName) =>
		GetRecordingAssetsRootPath(campaignName, Constants.CharactersDirectoryName);
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
	static string GetCampaignRootPath(string campaignName)
	{
		if (Path.IsPathRooted(campaignName) || campaignName.Contains("..", StringComparison.Ordinal))
		{
			throw new InvalidDataException("Name contains invalid characters.");
		}
		return Path.Combine(Constants.CampaignsDirectoryName, campaignName);
	}
	static string GetRecordingsRootPath(string campaignName)
	{
		return GetRecordingAssetsRootPath(campaignName, Constants.RecordingsDirectoryName);
	}
	static string GetTranscriptionsRootPath(string campaignName)
	{
		return GetRecordingAssetsRootPath(campaignName, Constants.TranscriptionDirectoryName);
	}
	static string GetEpicMomentsRootPath(string campaignName)
	{
		return GetRecordingAssetsRootPath(campaignName, Constants.EpicMomentsDirectoryName);
	}
	static string GetEpicMomentVideoPath(string campaignName, string recordingName) => Path.ChangeExtension(GetEpicMomentTextPath(campaignName, recordingName), ".mp4");
	static string GetEpicMomentTextPath(string campaignName, string recordingName)
	{
		return Path.ChangeExtension(GetRecordingAssetPath(campaignName, recordingName, Constants.EpicMomentsDirectoryName), ".txt");
	}
	static string GetRecordingAssetsRootPath(string campaignName, string assetType)
	{
		ArgumentException.ThrowIfNullOrEmpty(assetType);
		return Path.Combine(GetCampaignRootPath(campaignName), assetType);
	}
	static string GetRecordingAssetPath(string campaignName, string recordingName, string assetType)
	{
		if (Path.IsPathRooted(recordingName) || recordingName.Contains("..", StringComparison.Ordinal))
		{
			throw new InvalidDataException("Name contains invalid characters.");
		}
		return Path.Combine(GetRecordingAssetsRootPath(campaignName, assetType), recordingName);
	}
	static string GetRecordingPath(string campaignName, string recordingName)
	{
		return Path.ChangeExtension(GetRecordingAssetPath(campaignName, recordingName, Constants.RecordingsDirectoryName), ".mp3");
	}
	static string GetTranscriptionPath(string campaignName, string recordingName)
	{
		return Path.ChangeExtension(GetRecordingAssetPath(campaignName, recordingName, Constants.TranscriptionDirectoryName), ".txt");
	}
}