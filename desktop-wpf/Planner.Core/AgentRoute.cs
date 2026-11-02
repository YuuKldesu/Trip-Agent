using System;

namespace Planner.Core;

public sealed class AgentRoute
{
	public string FromRef { get; set; } = "";

	public string ToRef { get; set; } = "";

	public string Mode { get; set; } = "";

	public DateTime DepartAt { get; set; } = Clock.Now;

	public int Minutes { get; set; }

	public string Source { get; set; } = "";

	public DateTime QueryAt { get; set; } = Clock.Now;

	public RouteOption? Option { get; set; }

	public int? KnownCostFen { get; set; }

	public bool CostComplete { get; set; }
}
