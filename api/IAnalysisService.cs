namespace api;

public interface IAnalysisService
{
	Task<string> ExtractCharactersAsync(string campaignName, CancellationToken cancellationToken);
	Task<Stream> GenerateCharacterProfilePictureAsync(string campaignName, string characterName, CancellationToken cancellationToken);
	Task<Stream> GenerateEpicMomentVideoAsync(string campaignName, string recordingName, CancellationToken cancellationToken);
	Task<string[]> SaveRecordingsAndGenerateTranscriptionsAsync(string campaignName, IFormFileCollection form, CancellationToken cancellationToken);
}