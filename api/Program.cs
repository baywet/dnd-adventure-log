using Azure.AI.OpenAI;
using Azure.Identity;
using api;
using System.ClientModel;
using Azure.Core;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
// builder.Services.AddSingleton<DefaultAzureCredential>();
builder.Services.AddSingleton(new ApiKeyCredential(builder.Configuration["AzureOpenAIKey"] 
             ?? throw new InvalidOperationException("Please set the AzureOpenAI:ApiKey secret.")));
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
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder => builder.AllowAnyOrigin()
                           .AllowAnyMethod()
                           .AllowAnyHeader());
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

builder.Services.AddSingleton(sp =>
{
    const string modelName = "sora";
    var endpoint = builder.Configuration[$"AzureOpenAI:{Constants.EastUS2Region}"] ??
    throw new InvalidOperationException($"Please set the AzureOpenAI:{Constants.EastUS2Region} configuration value.");
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    if (sp.GetService<ApiKeyCredential>() is { } apiKeyCredential)
    {
        return new CustomVideoClient(
            endpoint,
            httpClientFactory,
            modelName,
            apiKeyCredential
        );
    }
    if (sp.GetService<TokenCredential>() is { } tokenCredential)
    {
        return new CustomVideoClient(
            endpoint,
            httpClientFactory,
            modelName,
            tokenCredential
        );
    }
    throw new InvalidOperationException("No valid authentication method configured.");
});

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
