using Azure.AI.OpenAI;

namespace api;

public class AzureNamedServicesHolder(Dictionary<string, AzureOpenAIClient> services) : NamedServicesHolder<AzureOpenAIClient>(services)
{
}
public class NamedServicesHolder<T>(Dictionary<string, T> services) where T : class
{
	private readonly Dictionary<string, T> _services = services;

	public T GetService(string name)
	{
		if (_services.TryGetValue(name, out var service))
		{
			return service;
		}
		throw new KeyNotFoundException($"Service with name '{name}' not found.");
	}
}