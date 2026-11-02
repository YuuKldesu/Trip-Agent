using System;

namespace Planner.Core;

public static class Clock
{
	public static DateTime Now => DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)).DateTime;

	public static DateOnly Today => DateOnly.FromDateTime(Now);

	public static DateOnly Monday(DateOnly date)
	{
		return date.AddDays(-((int)(date.DayOfWeek + 6) % 7));
	}
}
