namespace Planner.Core;

public sealed class MemoryUpdate
{
	public string Key { get; set; } = "";

	public string Value { get; set; } = "";

	public string SourceMessageId { get; set; } = "";

	public string Evidence { get; set; } = "";

	public bool Confirmed { get; set; }
}
