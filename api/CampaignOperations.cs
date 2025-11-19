namespace api;

public static class CampaignOperations
{
	public static void AddCampaignOperations(this WebApplication app)
	{
		app.MapGet(Constants.CampaignsApiSegment, (CampaignStorageService storageService) =>
		{
			var campaignNames = storageService.ListCampaigns();

			return Results.Ok(campaignNames);
		}).WithName("ListCampaigns").Produces<string[]>(StatusCodes.Status200OK, Constants.ApplicationJsonMimeType);

		app.MapPost($"{Constants.CampaignsApiSegment}/{{campaignName}}", (string campaignName, CampaignStorageService storageService) =>
		{
			if (string.IsNullOrWhiteSpace(campaignName))
			{
				return Results.BadRequest("Campaign name cannot be empty.");
			}
			var created = storageService.CreateCampaign(campaignName);
			if (!created)
			{
				return Results.Conflict("Campaign already exists.");
			}
			return Results.Created($"{Constants.CampaignsApiSegment}/{campaignName}", null);
		}).WithName("CreateCampaign").Produces(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status409Conflict);

		app.MapDelete($"{Constants.CampaignsApiSegment}/{{campaignName}}", (string campaignName, CampaignStorageService storageService) =>
		{
			var deleted = storageService.DeleteCampaign(campaignName);
			if (!deleted)
			{
				return Results.NotFound("Campaign does not exist.");
			}
			return Results.Accepted();
		}).WithName("DeleteCampaign").Produces(StatusCodes.Status202Accepted).ProducesProblem(StatusCodes.Status404NotFound);
	}
}