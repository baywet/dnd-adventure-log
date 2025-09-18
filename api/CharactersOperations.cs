namespace api;

public static class CharactersOperations
{
	public static void AddCharacterOperations(this WebApplication app)
	{
		app.MapPost($"{Constants.CampaignsApiSegment}/{{campaignName}}{Constants.CharactersApiSegment}", async (string campaignName, CampaignAnalysisService analysisService, CancellationToken cancellationToken) =>
		{
			try
			{
				var resultJson = await analysisService.ExtractCharactersAsync(campaignName, cancellationToken).ConfigureAwait(false);
				return Results.Content(resultJson, "application/json");
			}
			catch (FileNotFoundException ex)
			{
				return Results.BadRequest(ex.Message);
			}
			catch (InvalidOperationException ex)
			{
				return Results.InternalServerError(ex.Message);
			}
		}).WithName("CreateCharacterSummary").WithOpenApi();

		app.MapGet($"{Constants.CampaignsApiSegment}/{{campaignName}}{Constants.CharactersApiSegment}", (string campaignName, CampaignStorageService storageService) =>
		{
			var fs = storageService.GetCharacterSummary(campaignName);
			if (fs is null)
			{
				return Results.NotFound("Character summary not found.");
			}
			return Results.File(fs, "application/json");
		}).WithName("GetCharacters").WithOpenApi();

		app.MapPost($"{Constants.CampaignsApiSegment}/{{campaignName}}{Constants.CharactersApiSegment}/profile/{{characterName}}", async (string campaignName, string characterName, CampaignAnalysisService analysisService, CancellationToken cancellationToken) =>
		{
			try
			{
				await analysisService.GenerateCharacterProfilePictureAsync(campaignName, characterName, cancellationToken).ConfigureAwait(false);
				return Results.Created($"{Constants.CampaignsApiSegment}/{campaignName}{Constants.CharactersApiSegment}/profile/{characterName}", null);
			}
			catch (FileNotFoundException ex)
			{
				return Results.NotFound(ex.Message);
			}
			catch (InvalidOperationException ex)
			{
				return Results.InternalServerError(ex.Message);
			}
		}).WithName("CreateCharacterProfilePicture").WithOpenApi();

		app.MapGet($"{Constants.CampaignsApiSegment}/{{campaignName}}{Constants.CharactersApiSegment}/profile/{{characterName}}", (string campaignName, string characterName, CampaignStorageService storageService) =>
		{
			var imageStream = storageService.GetCharacterProfilePicture(campaignName, characterName);
			if (imageStream is null)
			{
				return Results.NotFound("Character image not found.");
			}
			return Results.File(imageStream, "image/png");
		}).WithName("GetCharacterProfilePicture").WithOpenApi();
	}
}