using System;

namespace Planner.Core;

public static class EditWindows
{
	public static void Apply(Activity a, DateOnly date, string startText, string endText, bool singleOverride)
	{
		if (singleOverride)
		{
			return;
		}
		if (a.ConstraintType != "FLEXIBLE")
		{
			a.WindowStart = null;
			a.WindowEnd = null;
			return;
		}
		if (!TimeOnly.TryParseExact(startText.Trim(), "HH:mm", out var result) || !TimeOnly.TryParseExact(endText.Trim(), "HH:mm", out var result2))
		{
			throw new ArgumentException("灵活时间范围请使用HH:mm格式。");
		}
		a.WindowStart = date.ToDateTime(result);
		a.WindowEnd = date.ToDateTime(result2);
		if (a.WindowEnd <= a.WindowStart)
		{
			a.WindowEnd = a.WindowEnd.Value.AddDays(1.0);
		}
		if (!(a.StartAt < a.WindowStart) && !(a.EndAt > a.WindowEnd))
		{
			return;
		}
		throw new ArgumentException("当前起止时间应落在灵活范围内。");
	}
}
