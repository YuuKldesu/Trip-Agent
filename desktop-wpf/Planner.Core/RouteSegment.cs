namespace Planner.Core;

public sealed class RouteSegment : Entity
{
	public string LegId { get; set; } = "";

	public int Seq { get; set; }

	public string Mode { get; set; } = "WALK";

	public string? ResourceType { get; set; }

	public string? Instruction { get; set; }

	public int DurationSec { get; set; }

	public int? DistanceM { get; set; }

	public int? KnownCostFen { get; set; }

	public string? CostDetailJson { get; set; }

	public string? Congestion { get; set; }

	public string? Geometry { get; set; }

	public string Provider { get; set; } = "高德";
}
