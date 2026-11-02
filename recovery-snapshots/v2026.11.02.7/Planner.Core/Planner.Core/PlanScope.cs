namespace Planner.Core;

public sealed class PlanScope
{
	public string PlanId { get; set; } = "";

	public string TableId { get; set; } = "";

	public string Role { get; set; } = "CONSTRAINT";

	public int TableVersion { get; set; }
}
