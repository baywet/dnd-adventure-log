using Azure.AI.OpenAI;
using Azure.Identity;
using api;
using System.ClientModel;
using Azure.Core;
using OpenAI.Videos;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSingleton<TokenCredential, DefaultAzureCredential>();
//builder.Services.AddSingleton(new ApiKeyCredential(builder.Configuration["AzureOpenAIKey"] 
 //            ?? throw new InvalidOperationException("Please set the AzureOpenAI:ApiKey secret.")));
builder.Services.AddSingleton<AzureNamedServicesHolder>(sp =>
{
    AzureOpenAIClient createClientWithUri(Uri uri) =>
        sp.GetService<ApiKeyCredential>() is { } apiKeyCredential ?
            new(uri, apiKeyCredential) :
            new(uri, sp.GetRequiredService<TokenCredential>());

    AzureOpenAIClient createClient(string region) =>
        createClientWithUri(new Uri(builder.Configuration[$"AzureOpenAI:{region}"] ??
            throw new InvalidOperationException($"Please set the AzureOpenAI:{region} configuration value.")));

    return new(new(StringComparer.OrdinalIgnoreCase)
    {
        { Constants.EastUS2Region, createClient(Constants.EastUS2Region) },
    });
});

string GetModelName(string modelKey) =>
    builder.Configuration[$"ModelDeploymentNames:{modelKey}"] ??
    throw new InvalidOperationException($"Please set the ModelDeploymentNames:{modelKey} configuration value.");

builder.Services.AddSingleton(sp => sp.GetRequiredService<AzureNamedServicesHolder>()
                                        .GetService(Constants.EastUS2Region)
                                        .GetAudioClient(GetModelName("Audio")));

builder.Services.AddSingleton(sp => sp.GetRequiredService<AzureNamedServicesHolder>()
                                        .GetService(Constants.EastUS2Region)
                                        .GetChatClient(GetModelName("Chat")));

builder.Services.AddSingleton(sp => sp.GetRequiredService<AzureNamedServicesHolder>()
                                        .GetService(Constants.EastUS2Region)
                                        .GetResponsesClient(GetModelName("Responses")));

builder.Services.AddSingleton(sp => sp.GetRequiredService<AzureNamedServicesHolder>()
                                        .GetService(Constants.EastUS2Region)
                                        .GetImageClient(GetModelName("Image")));

builder.Services.AddSingleton(sp => sp.GetRequiredService<AzureNamedServicesHolder>()
                                        .GetService(Constants.EastUS2Region)
                                        .GetVideoClient());

builder.Services.AddHttpClient();

builder.Services.AddSingleton(sp =>
{
    var modelName = GetModelName("Video");
    var endpoint = builder.Configuration[$"AzureOpenAI:{Constants.EastUS2Region}"] ??
    throw new InvalidOperationException($"Please set the AzureOpenAI:{Constants.EastUS2Region} configuration value.");
    var videoClient = sp.GetRequiredService<VideoClient>();
    return new CustomVideoClient(
        videoClient,
        modelName
    );
});

builder.Services.AddSingleton<CampaignStorageService>();

builder.Services.AddSingleton<CampaignAnalysisService>();

builder.Services.AddSingleton<IAnalysisService, CachedAnalysisService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.AddOpenAPI();
app.AddCampaignOperations();
app.AddRecordingOperations();
app.AddCharacterOperations();
app.UseStaticFiles();

Directory.CreateDirectory(Constants.CampaignsDirectoryName);

await app.RunAsync();
