using Azure.Identity;
using api;
using Azure.Core;
using OpenAI.Videos;
using OpenAI.Chat;
using OpenAI;
using System.ClientModel.Primitives;
using OpenAI.Audio;
using OpenAI.Responses;
using OpenAI.Images;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
// ---- Entra ID to authenticate to Microsoft Foundry ----
builder.Services.AddSingleton<TokenCredential, DefaultAzureCredential>();
builder.Services.AddSingleton<AuthenticationPolicy>(sp => 
    new BearerTokenPolicy(sp.GetRequiredService<TokenCredential>(), "https://ai.azure.com/.default"));
// ---- API Keys to authenticate to Microsoft Foundry ----
//builder.Services.AddSingleton(new ApiKeyCredential(builder.Configuration["AzureOpenAIKey"] 
//            ?? throw new InvalidOperationException("Please set the AzureOpenAI:ApiKey secret.")));
// builder.Services.AddSingleton<AuthenticationPolicy>( sp => 
//     ApiKeyAuthenticationPolicy.CreateBearerAuthorizationPolicy(sp.GetRequiredService<ApiKeyCredential>()));
// ---- End of auth
var endpoint = new Uri(builder.Configuration[$"AzureOpenAI:EastUS2"] ?? throw new InvalidOperationException("Please set the AzureOpenAI:EastUS2 configuration value."));

var clientOptions = new OpenAIClientOptions()
{
    Endpoint = endpoint,
};

#region audioClientMagic

var audioClientOptions = new OpenAIClientOptions()
{
    Endpoint = new Uri(endpoint.ToString().Replace("openai.azure.com/openai/v1/", $"cognitiveservices.azure.com/openai/deployments/{GetModelName("Audio")}/"))
};

audioClientOptions.AddPolicy(new ApiVersionPipelinePolicy(), PipelinePosition.BeforeTransport);

#endregion

string GetModelName(string modelKey) =>
    builder.Configuration[$"ModelDeploymentNames:{modelKey}"] ??
    throw new InvalidOperationException($"Please set the ModelDeploymentNames:{modelKey} configuration value.");

builder.Services.AddSingleton(sp => 
    new AudioClient(
        authenticationPolicy: sp.GetRequiredService<AuthenticationPolicy>(),
        model: GetModelName("Audio"),
        options: audioClientOptions
));

builder.Services.AddSingleton(sp => 
    new ChatClient(
        authenticationPolicy: sp.GetRequiredService<AuthenticationPolicy>(),
        model: GetModelName("Chat"),
        options: clientOptions
));

builder.Services.AddSingleton(sp => 
    new ResponsesClient(
        sp.GetRequiredService<AuthenticationPolicy>(),
        clientOptions
));

builder.Services.AddSingleton(sp => 
    new ImageClient(
        authenticationPolicy: sp.GetRequiredService<AuthenticationPolicy>(),
        model: GetModelName("Image"),
        options: clientOptions
));

builder.Services.AddSingleton(sp => 
    new VideoClient(
        authenticationPolicy: sp.GetRequiredService<AuthenticationPolicy>(),
        options: clientOptions
));

builder.Services.AddHttpClient();

builder.Services.AddSingleton(sp =>
{
    var modelName = GetModelName("Video");
    var videoClient = sp.GetRequiredService<VideoClient>();
    return new VideoClientOrchestrator(
        videoClient,
        modelName
    );
});

builder.Services.AddSingleton(sp =>
{
    var modelSelection = new CampaignModelSelection(
        ChatModel: GetModelName("Chat"),
        AudioModel: GetModelName("Audio"),
        ImageModel: GetModelName("Image"),
        ResponsesModel: GetModelName("Responses"),
        VideoModel: GetModelName("Video")
    );
    return modelSelection;
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
