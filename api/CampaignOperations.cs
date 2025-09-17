namespace api;

public static class CampaignOperations
{
	public static string GetCampaignRootPath(string campaignName)
	{
		if (Path.IsPathRooted(campaignName) || campaignName.Contains("..", StringComparison.Ordinal))
		{
			throw new InvalidDataException("Name contains invalid characters.");
		}
		return Path.Combine(Constants.CampaignsDirectoryName, campaignName);
	}
	public static void AddCampaignOperations(this WebApplication app)
	{
		app.MapGet(Constants.CampaignsApiSegment, () =>
		{
			var campaignNames = Directory.GetDirectories(Constants.CampaignsDirectoryName)
				.Select(filePath => Path.GetFileName(filePath))
				.ToArray();

			return Results.Ok(campaignNames);
		}).WithName("ListCampaigns").WithOpenApi();

		app.MapPost($"{Constants.CampaignsApiSegment}/{{campaignName}}", (string campaignName) =>
		{
			var campaignPath = GetCampaignRootPath(campaignName);
			if (Directory.Exists(campaignPath))
			{
				return Results.Conflict("Campaign already exists.");
			}
			Directory.CreateDirectory(campaignPath);
			return Results.Created($"{Constants.CampaignsApiSegment}/{campaignName}", null);
		}).WithName("CreateCampaign").WithOpenApi();

		app.MapDelete($"{Constants.CampaignsApiSegment}/{{campaignName}}", (string campaignName) =>
		{
			var campaignPath = GetCampaignRootPath(campaignName);
			if (!Directory.Exists(campaignPath))
			{
				return Results.NotFound("Campaign does not exist.");
			}
			Directory.Delete(campaignPath, true);
			return Results.Accepted();
		}).WithName("DeleteCampaign").WithOpenApi();
	}
}