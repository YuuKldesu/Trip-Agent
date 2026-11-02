using System;

namespace Planner.Core;

public sealed class ActivityException : Entity
{
	public string ActivityId { get; set; } = "";

	public DateOnly OriginDate { get; set; }

	public string Kind { get; set; } = "OVERRIDE";

	public DateTime? StartAt { get; set; }

	public DateTime? EndAt { get; set; }

	public string? Title { get; set; }

	public string? PlaceId { get; set; }

	public bool PlaceOverride { get; set; }

	public int Version { get; set; } = 1;

	public DateTime UpdatedAt { get; set; } = Clock.Now;
}
