using System;

namespace Planner.Core;

public sealed class ScheduleReadItem
{
	public string Title { get; set; } = "";

	public string? Place { get; set; }

	public string? PlaceRef { get; set; }

	public string? Table { get; set; }

	public DateTime Start { get; set; } = Clock.Now;

	public DateTime End { get; set; } = Clock.Now;
}
