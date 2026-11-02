using System;

namespace Planner.Core;

public sealed class Plan : Entity
{
	public DateOnly PlanDate { get; set; }

	public int Version { get; set; } = 1;

	public string Status { get; set; } = "CONFIRMED";

	public string? StartPlaceId { get; set; }

	public DateTime? EarliestDepart { get; set; }

	public string? ResourceJson { get; set; }

	public DateTime ConfirmedAt { get; set; } = Clock.Now;
}
