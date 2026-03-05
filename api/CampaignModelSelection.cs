namespace api;

public record CampaignModelSelection(
	string ChatModel,
	string AudioModel,
	string ImageModel,
	string ResponsesModel,
	string VideoModel
);