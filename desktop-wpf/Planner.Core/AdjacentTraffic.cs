using System;
using System.Collections.Generic;
using System.Linq;

namespace Planner.Core;

public static class AdjacentTraffic
{
	public static List<TrafficPair> Around(PlannerState state, Occurrence target)
	{
		List<Occurrence> source = (from o in CalendarEngine.Expand(state, target.Start.Date.AddDays(-7.0), target.End.Date.AddDays(8.0))
			where o.Key != target.Key
			select o).ToList();
		List<TrafficPair> list = new List<TrafficPair>();
		Occurrence occurrence = source.Where((Occurrence o) => o.End <= target.Start).MaxBy((Occurrence o) => o.End);
		Occurrence occurrence2 = source.Where((Occurrence o) => o.Start >= target.End).MinBy((Occurrence o) => o.Start);
		if ((object)occurrence != null && (target.Start - occurrence.End).TotalHours <= 24.0)
		{
			list.Add(new TrafficPair(occurrence, target));
		}
		if ((object)occurrence2 != null && (occurrence2.Start - target.End).TotalHours <= 24.0)
		{
			list.Add(new TrafficPair(target, occurrence2));
		}
		return list;
	}

	public static string Label(TrafficPair pair, TrafficSnapshot? snapshot)
	{
		string text = $"间隔 {pair.GapMinutes} 分钟";
		int? num = snapshot?.Minutes;
		if (num.HasValue)
		{
			int valueOrDefault = num.GetValueOrDefault();
			int num2 = pair.GapMinutes - valueOrDefault;
			return text + $" · 交通约 {valueOrDefault} 分钟 · " + ((num2 < 0) ? $"来不及，缺 {-num2} 分钟" : $"余量 {num2} 分钟");
		}
		return text + " · 交通未核验";
	}

	public static bool SameTimingAndPlace(Occurrence? old, DateTime start, DateTime end, Place? place, PlannerState state)
	{
		if ((object)old != null && old.Start == start && old.End == end && state.Places.FirstOrDefault((Place p) => p.Id == old.PlaceId)?.Name == place?.Name)
		{
			return old.PlaceId == place?.Id;
		}
		return false;
	}
}
