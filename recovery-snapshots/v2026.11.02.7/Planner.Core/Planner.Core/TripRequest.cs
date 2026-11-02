using System;

namespace Planner.Core;

public sealed class TripRequest
{
	public string EventId { get; set; } = "";

	public DateOnly? Day { get; set; }

	public DateOnly? EndDay { get; set; }

	public string Title { get; set; } = "";

	public string Location { get; set; } = "";

	public string? PlaceRef { get; set; }

	public int? Duration { get; set; }

	public int? Start { get; set; }

	public int? End { get; set; }

	public int? Earliest { get; set; }

	public int? LatestEnd { get; set; }

	public string Notes { get; set; } = "";

	public string? ModeIn { get; set; }

	public bool IsReturn { get; set; }

	public bool ReminderEnabled { get; set; }

	public int ReminderLeadMin { get; set; } = 15;
}
