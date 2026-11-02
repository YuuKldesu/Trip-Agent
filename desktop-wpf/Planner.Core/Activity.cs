using System;

namespace Planner.Core;

public sealed class Activity : Entity
{
	public string TableId { get; set; } = "";

	public string Title { get; set; } = "";

	public string? PlaceId { get; set; }

	public string Kind { get; set; } = "ONCE";

	public string ConstraintType { get; set; } = "FIXED";

	public DateTime? StartAt { get; set; }

	public DateTime? EndAt { get; set; }

	public int DurationMin { get; set; } = 60;

	public DateTime? WindowStart { get; set; }

	public DateTime? WindowEnd { get; set; }

	public string? Note { get; set; }

	public int Version { get; set; } = 1;

	public DateTime UpdatedAt { get; set; } = Clock.Now;
}
