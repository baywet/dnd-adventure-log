namespace api;

public static class CharactersOperations
{
	public static void AddCharacterOperations(this WebApplication app)
	{
		app.MapPost($"{Constants.CampaignsApiSegment}/{{campaignName}}{Constants.CharactersApiSegment}", async (string campaignName, IAnalysisService analysisService, CancellationToken cancellationToken) =>
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
		}).WithName("CreateCharacterSummary").WithOpenApi().Produces<Character[]>(StatusCodes.Status200OK, Constants.ApplicationJsonMimeType).ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status500InternalServerError);

		app.MapGet($"{Constants.CampaignsApiSegment}/{{campaignName}}{Constants.CharactersApiSegment}", (string campaignName, CampaignStorageService storageService) =>
		{
			var fs = storageService.GetCharacterSummary(campaignName);
			if (fs is null)
			{
				return Results.NotFound("Character summary not found.");
			}
			return Results.File(fs, "application/json");
		}).WithName("GetCharacters").WithOpenApi().Produces<Character[]>(StatusCodes.Status200OK, Constants.ApplicationJsonMimeType).Produces(StatusCodes.Status404NotFound);

		app.MapPost($"{Constants.CampaignsApiSegment}/{{campaignName}}{Constants.CharactersApiSegment}/profile/{{characterName}}", async (string campaignName, string characterName, IAnalysisService analysisService, CancellationToken cancellationToken) =>
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
		}).WithName("CreateCharacterProfilePicture").WithOpenApi().Produces(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status500InternalServerError);

		app.MapGet($"{Constants.CampaignsApiSegment}/{{campaignName}}{Constants.CharactersApiSegment}/profile/{{characterName}}", (string campaignName, string characterName, CampaignStorageService storageService) =>
		{
			var imageStream = storageService.GetCharacterProfilePicture(campaignName, characterName);
			if (imageStream is null)
			{
				return Results.NotFound("Character image not found.");
			}
			return Results.File(imageStream, "image/png");
		}).WithName("GetCharacterProfilePicture").WithOpenApi().Produces<Stream>(StatusCodes.Status200OK, "image/png").Produces(StatusCodes.Status404NotFound);
	}
}