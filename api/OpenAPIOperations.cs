namespace api;

public static class OpenAPIOperations
{
	public static void AddOpenAPI(this WebApplication app)
	{
		if (app.Environment.IsDevelopment())
		{
			app.MapOpenApi();
			app.UseSwaggerUI((options) =>
			{
				options.SwaggerEndpoint("/openapi/v1.json", "v1");
				options.RoutePrefix = Constants.ApiDocsPath;
			});
			app.MapGet("/", () => Results.LocalRedirect($"/{Constants.ApiDocsPath}")).ExcludeFromDescription();
		}
	}
}