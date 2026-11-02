using System.Collections.Generic;

namespace Planner.Core;

public sealed class GuidedReply
{
	public string Message { get; set; } = "";

	public string Summary { get; set; } = "";

	public string DraftIssue { get; set; } = "";

	public List<string> Missing { get; set; } = new List<string>();

	public List<TripRequest> Events { get; set; } = new List<TripRequest>();

	public bool Ready { get; set; }

	public AgentMemory? Memory { get; set; }

	public List<MemoryUpdate> MemoryUpdates { get; set; } = new List<MemoryUpdate>();

	public string DraftAction { get; set; } = "";

	public List<DraftPatch> DraftPatches { get; set; } = new List<DraftPatch>();

	public List<DraftChange> Changes { get; set; } = new List<DraftChange>();

	public List<string> ToolErrors { get; set; } = new List<string>();
}
