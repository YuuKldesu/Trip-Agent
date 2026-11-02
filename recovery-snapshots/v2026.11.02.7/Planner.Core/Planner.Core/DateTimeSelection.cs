using System;

namespace Planner.Core;

public static class DateTimeSelection
{
	public static DateTime Roll(DateTime value, bool hours, int steps)
	{
		try
		{
			return hours ? value.AddHours(steps) : value.AddMinutes(steps);
		}
		catch (ArgumentOutOfRangeException)
		{
			throw new ArgumentException("日期已超出可选择范围。");
		}
	}

	public static (DateTime Start, DateTime End) MoveStart(DateTime start, DateTime end, DateTime next)
	{
		TimeSpan timeSpan = end - start;
		if (timeSpan <= TimeSpan.Zero)
		{
			throw new ArgumentException("请先设置有效的起止时间。");
		}
		try
		{
			return (Start: next, End: next.Add(timeSpan));
		}
		catch (ArgumentOutOfRangeException)
		{
			throw new ArgumentException("结束日期超出可选择范围。");
		}
	}
}
