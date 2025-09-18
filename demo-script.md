# Demo script for the session

## Pre-reqs

1. Dotnet SDK 9 `sudo winget install Microsoft.Dotnet.SDK.9`.
1. Azure CLI `sudo winget install Microsoft.AzureCLI`.
1. A valid azure subscription.
1. Visual Studio code `sudo winget install Microsoft.VisualStudioCode`.
1. An Azure AI Foundry deployment with the following models `gpt-4o`, `gpt-4o-transcribe`, `gpt-image-1` and `sora`.

## Getting ready

1. Updated `appsettings.development.json` with the endpoint.
1. Browser open to [ai.azure.com](https://ai.azure.com)
1. VSCode open to this repository, with the model-instructions open.
1. Terminal open, and signed in to azure `az login --tenant "vincentbirethotmail.onmicrosoft.com"`.
1. API running `dotnet watch run --project .\api\api.csproj`.
1. Front end run `cd frontend && npm ci && npm run dev`.
1. Browser open to the built front end application.
1. Downloaded the first 10 episodes in the resources directory.

## Demos

### Showcase the application

1. Upload the episodes.
1. Explain it: generates a transcription, detects the characters, generates a profile picture for the characters, finds an epic moment, generates a video for the epic moment.

### Deploying and testing the models

1. Navigate to models & endpoints.
1. Show how we can deploy new models, the different kind of inferences they can perform etc.
1. Navigate to the playground.
1. Show how we can iterate and capture great instructions for our models.

### Creating a custom content filter

1. Show in the playground that image generation is not working because of the default filters.
1. Navigate to "Guardrails & controls" under "protect and govern".
1. Select "create a new content filter".
1. Set (both input and output):

   - Violence: low
   - Hate: medium
   - Sexual: high
   - Self-harm: high
   - Prompts: annotate and block

### Generating the transcript

1. Navigate to Program.cs.
1. Show the Auth setup, benefits of Azure Credentials.
1. Show the services layering, explain it's the same exact client as OpenAI SDK, + some auth magic.
1. Navigate to CampaignAnalysisService.cs, SaveRecordingsAndGenerateTranscriptionsAsync.
1. Explain we're using the audio client.
1. Explain the issues with audio size and length.

### Generating the characters

1. Navigate to CampaignAnalysisService.cs, ExtractCharactersAsync.
1. Explain we're using the chat client.
1. Explain the difference between system and user messages.
1. Explain the JSON schema thing.

### Generating the character profile images

1. Navigate to CampaignAnalysisService.cs, GenerateCharacterProfilePictureAsync.
1. Explain we're using the ImageClient.
1. Explain the issues with Image Uri vs Bytes.

### Generating the epic moment

1. Navigate to CampaignAnalysisService.cs, GenerateEpicMomentVideoAsync.
1. Explain we're using a custom made video client, show the complexity.
