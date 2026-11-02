namespace Planner.Data;

public sealed class ServiceSettings
{
	public string AgentProvider { get; set; } = "AUTO";

	public string LlmEndpoint { get; set; } = "";

	public string Model { get; set; } = "";

	public string LlmKeyProtected { get; set; } = "";

	public string MapServiceKeyProtected { get; set; } = "";

	public string MapJsKeyProtected { get; set; } = "";

	public string MapSecurityProtected { get; set; } = "";

	public string AssistantPersona { get; set; } = "DEFAULT";

	public bool JsonMode { get; set; } = true;
}
