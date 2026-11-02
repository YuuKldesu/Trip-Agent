using System;

namespace Planner.Core;

public sealed record BoardEntry(Occurrence Occurrence, DateOnly Day, DateTime Start, DateTime End)
{
	public PlanLeg? Transfer { get; init; }

	public bool ContinuesFromYesterday => Start > Occurrence.Start;

	public bool ContinuesTomorrow => End < Occurrence.End;

	public int DayMinutes => (int)(End - Start).TotalMinutes;

	public int TotalMinutes => (int)(Occurrence.End - Occurrence.Start).TotalMinutes;

	public string DurationText
	{
		get
		{
			if (DayMinutes == TotalMinutes)
			{
				return $"耗时：{TotalMinutes} 分钟";
			}
			return $"本日 {DayMinutes} 分钟 · 全程 {TotalMinutes} 分钟";
		}
	}

	public string ContinuityText
	{
		get
		{
			if (!ContinuesFromYesterday || !ContinuesTomorrow)
			{
				if (!ContinuesFromYesterday)
				{
					if (!ContinuesTomorrow)
					{
						return "";
					}
					return "跨日 · 接续至 " + Occurrence.End.ToString("MM-dd HH:mm");
				}
				return "接续 · 始于 " + Occurrence.Start.ToString("MM-dd HH:mm");
			}
			return "跨日接续 · 始于 " + Occurrence.Start.ToString("MM-dd HH:mm") + "，至 " + Occurrence.End.ToString("MM-dd HH:mm");
		}
	}

	public string TimeText => Start.ToString("HH:mm") + "—" + ((End.Date > Start.Date && End.TimeOfDay == TimeSpan.Zero) ? "24:00" : End.ToString("HH:mm"));
}
