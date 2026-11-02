using System.Text.Json;

namespace Planner.Data;

public sealed class AgentToolRequest
{
	public string Name { get; set; } = "";

	public JsonElement Arguments { get; set; }
}
