using System;

namespace Planner.Core;

public sealed class RecurrenceRule
{
	public string ActivityId { get; set; } = "";

	public DateOnly ValidFrom { get; set; } = Clock.Today;

	public DateOnly? ValidUntil { get; set; }

	public int WeekMask { get; set; } = 31;

	public string StartTime { get; set; } = "09:00";

	public string? WindowFrom { get; set; }

	public int? WindowSpanMin { get; set; }

	public bool Matches(DateOnly date)
	{
		if (date >= ValidFrom)
		{
			if (ValidUntil.HasValue)
			{
				DateOnly value = date;
				DateOnly? validUntil = ValidUntil;
				if (!(value <= validUntil))
				{
					goto IL_005d;
				}
			}
			return (WeekMask & (1 << (int)(date.DayOfWeek + 6) % 7)) != 0;
		}
		goto IL_005d;
		IL_005d:
		return false;
	}
}
