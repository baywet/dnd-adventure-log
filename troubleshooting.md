# troubleshooting

## Dumping requests


```csharp

public class ConsoleDumpRequestPolicy : PipelinePolicy
{
    public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        using var ms = new MemoryStream();
        message.Request.Content!.WriteTo(ms);
        Console.WriteLine(Encoding.UTF8.GetString(ms.ToArray()));
        
        PipelinePolicy.ProcessNext(message, pipeline, currentIndex);
    }

    public override ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        using var ms = new MemoryStream();
        message.Request.Content!.WriteTo(ms);
        Console.WriteLine(Encoding.UTF8.GetString(ms.ToArray()));
        
        return PipelinePolicy.ProcessNextAsync(message, pipeline, currentIndex);
    }
}

```

```csharp
var loggerFactory = LoggerFactory.Create(builder => { builder .SetMinimumLevel(LogLevel.Trace) .AddConsole(); });

var loggingOptions = new ClientLoggingOptions();
loggingOptions.AllowedHeaderNames.Add("*");
loggingOptions.AllowedQueryParameters.Add("*");
loggingOptions.EnableLogging = true;
loggingOptions.EnableMessageLogging = true;
loggingOptions.EnableMessageContentLogging = true;
loggingOptions.LoggerFactory = loggerFactory;

var clientOptions = new OpenAIClientOptions()
{
    Endpoint = endpoint,
    MessageLoggingPolicy = new MessageLoggingPolicy(loggingOptions)
};
clientOptions.AddPolicy(new ConsoleDumpRequestPolicy(), PipelinePosition.PerCall);
```

## Setting query parameter

```csharp
internal partial class ApiVersionPipelinePolicy : PipelinePolicy
{
    public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        message.Request.Uri = new(message?.Request?.Uri, "?api-version=2025-04-01-preview");
        ProcessNext(message, pipeline, currentIndex);
    }

    public override async ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        message.Request.Uri = new(message?.Request?.Uri, "?api-version=2025-04-01-preview");
        await ProcessNextAsync(message, pipeline, currentIndex);
    }
}
```

```csharp
clientOptions.AddPolicy(new ApiVersionPipelinePolicy(), PipelinePosition.BeforeTransport);
```
