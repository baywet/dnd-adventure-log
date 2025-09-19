namespace api;

public static class OpenAPIOperations
{
	public static void AddOpenAPI(this WebApplication app)
	{
		app.MapOpenApi();
		app.UseSwaggerUI((options) =>
		{
			options.SwaggerEndpoint("/openapi/v1.json", "v1");
			options.RoutePrefix = Constants.ApiDocsPath;
		});
		if (File.Exists("wwwroot/index.html"))
		{
			app.MapGet("/", () => Results.LocalRedirect("/index.html")).ExcludeFromDescription();
		}
		else
		{
			app.MapGet("/", () => Results.LocalRedirect($"/{Constants.ApiDocsPath}")).ExcludeFromDescription();
		}
	}
}