using System.ClientModel.Primitives;

namespace api;

internal partial class ApiVersionPipelinePolicy : PipelinePolicy
{
	private readonly string _version;
	public ApiVersionPipelinePolicy(string version = "2025-03-01-preview")
	{
		ArgumentException.ThrowIfNullOrEmpty(version);
		_version = version;
	}
    public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        message.Request.Uri = new(message?.Request?.Uri ?? throw new InvalidOperationException("The request URI is null"), $"?api-version={_version}");
        ProcessNext(message, pipeline, currentIndex);
    }

    public override async ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        message.Request.Uri = new(message?.Request?.Uri ?? throw new InvalidOperationException("The request URI is null"), $"?api-version={_version}");
        await ProcessNextAsync(message, pipeline, currentIndex);
    }
}