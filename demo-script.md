# Demo script for the session

## Pre-reqs

1. Dotnet SDK 9 `sudo winget install Microsoft.Dotnet.SDK.9`.
1. Azure CLI `sudo winget install Microsoft.AzureCLI`.
1. A valid azure subscription.
1. Visual Studio code `sudo winget install Microsoft.VisualStudioCode`.

## Getting ready

1. Browser open to [ai.azure.com](https://ai.azure.com)
1. VSCode open to this repository, with the model-instructions open.
1. Terminal open, and signed in to azure `az login --tenant "vincentbirethotmail.onmicrosoft.com"`.
1. API running `dotnet watch run --project .\api\api.csproj`.

## Deploying and testing the models

1. Navigate to models & endpoints.
1. Show how we can deploy new models, the different kind of inferences they can perform etc.
1. Navigate to the playground.
1. Show how we can iterate and capture great instructions for our models.

## Creating a custom content filter

1. Show in the playground that image generation is not working because of the default filters.
1. Navigate to "Guardrails & controls" under "protect and govern".
1. Select "create a new content filter".
1. Set (both input and output):

   - Violence: low
   - Hate: medium
   - Sexual: high
   - Self-harm: high
   - Prompts: annotate and block

