using Azure.AI.OpenAI;
using Azure.Identity;
using api;
using System.ClientModel;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder => builder.AllowAnyOrigin()
                           .AllowAnyMethod()
                           .AllowAnyHeader());
});
builder.Services.AddSingleton<AzureNamedServicesHolder>(_ =>
{
    AzureOpenAIClient createClient(string region) => new(
        new Uri(builder.Configuration[$"AzureOpenAI:{region}"] ??
                throw new InvalidOperationException($"Please set the AzureOpenAI:{region} configuration value.")),
        new ApiKeyCredential(builder.Configuration["AzureOpenAIKey"] 
             ?? throw new InvalidOperationException("Please set the AzureOpenAI:ApiKey secret.")));
    return new(new(StringComparer.OrdinalIgnoreCase)
    {
        { Constants.EastUS2Region, createClient(Constants.EastUS2Region) },
        { Constants.EastUSRegion, createClient(Constants.EastUSRegion) },
    });
});
builder.Services.AddSingleton(sp => sp.GetRequiredService<AzureNamedServicesHolder>()
                                        .GetService(Constants.EastUS2Region)
                                        .GetAudioClient("gpt-4o-transcribe"));

builder.Services.AddSingleton(sp => sp.GetRequiredService<AzureNamedServicesHolder>()
                                        .GetService(Constants.EastUS2Region)
                                        .GetChatClient("gpt-4o"));
builder.Services.AddSingleton(sp => sp.GetRequiredService<AzureNamedServicesHolder>()
                                        .GetService(Constants.EastUS2Region)
                                        .GetImageClient("gpt-image-1"));
builder.Services.AddHttpClient();

builder.Services.AddSingleton(sp => new CustomVideoClient(
    builder.Configuration[$"AzureOpenAI:{Constants.EastUS2Region}"] ??
    throw new InvalidOperationException($"Please set the AzureOpenAI:{Constants.EastUS2Region} configuration value."), sp.GetRequiredService<IHttpClientFactory>()));

builder.Services.AddSingleton<CampaignStorageService>();

builder.Services.AddSingleton<CampaignAnalysisService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.AddOpenAPI();
app.UseHttpsRedirection();
app.AddCampaignOperations();
app.AddRecordingOperations();
app.AddCharacterOperations();
app.UseCors("AllowAll");

Directory.CreateDirectory(Constants.CampaignsDirectoryName);

await app.RunAsync();
