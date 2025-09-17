namespace api;

public static class OpenAPIHelper
{
	public static void AddOpenAPI(this WebApplication app)
	{
		if (app.Environment.IsDevelopment())
		{
			app.MapOpenApi();
			const string apiDocsPath = "api-docs";
			app.UseSwaggerUI((options) =>
			{
				options.SwaggerEndpoint("/openapi/v1.json", "v1");
				options.RoutePrefix = apiDocsPath;
			});
			app.MapGet("/", () => Results.LocalRedirect($"/{apiDocsPath}")).ExcludeFromDescription();
		}
	}
}