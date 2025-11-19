# DnD adventurer log - backend API

This backend API is written using dotnet 10 minimal APIs.

## Getting started

1. Ensure you update the service endpoint in `appsettings.Development.json`.
1. Ensure your service has the following models provisioned `gpt-4o-transcribe`, `gpt-4o`, `gpt-image-1` and `sora`.
1. Login with the Azure CLI `az login` so the application can authenticate to your Azure AI Foundry instance.
1. Run the command below to start the API locally, and reload on code changes.

   ```shell
   dotnet watch run --project api/api.csproj
   ```

   > Note: API structure changes, or configuration changes require using `ctrl + R` for a full reload.

## Requirements

- Dotnet SDK 10
- Azure CLI

## Architecture

The API is implemented through multiple files:

- Program.cs bootstraps the API, configures the services.
- *Operations.cs adds the diverse API operations.
- CampaignStorageService.cs handles all the storage operations and retrieval. This uses local storage for simplicity.
- CampaignAnalysisService.cs handles all the calls to Azure AI Foundry.
- CustomVideoClient.cs handcrafted client for video generation.
- AudioHelper.cs basic helpers Audio chunking.
- appsettings.Development.json: needs to be updated with your service endpoint.
