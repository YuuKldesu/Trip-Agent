using System;

namespace Planner.Core;

public sealed class AiOperation
{
	public string Action { get; set; } = "ADD";

	public string? TargetActivityId { get; set; }

	public DateOnly? OriginDate { get; set; }

	public string? TableId { get; set; }

	public string? Title { get; set; }

	public string? Place { get; set; }

	public DateTime? StartAt { get; set; }

	public DateTime? EndAt { get; set; }

	public string ConstraintType { get; set; } = "FIXED";

	public DateTime? WindowStart { get; set; }

	public DateTime? WindowEnd { get; set; }

	public int WeekMask { get; set; }

	public DateOnly? UntilDate { get; set; }

	public string Scope { get; set; } = "ONCE";
}
