using System;
using System.Collections.Generic;
using System.Linq;

namespace Planner.Core;

public static class ContinuousList
{
	public static List<(DateOnly From, DateOnly Until)> Windows(PlannerState state, DateOnly horizon, IEnumerable<string> visibleTables)
	{
		HashSet<string> visible = visibleTables.ToHashSet();
		HashSet<string> activityIds = (from activity in state.Activities
			where visible.Contains(activity.TableId)
			select activity.Id).ToHashSet();
		List<(DateOnly From, DateOnly Until)> ranges = new List<(DateOnly, DateOnly)>();
		foreach (Activity a in state.Activities.Where((Activity activity) => activityIds.Contains(activity.Id)))
		{
			if (a.Kind == "ONCE")
			{
				Add(a.StartAt, a.EndAt);
				continue;
			}
			RecurrenceRule recurrenceRule = state.Rules.FirstOrDefault((RecurrenceRule r) => r.ActivityId == a.Id);
			if (recurrenceRule != null)
			{
				DateOnly dateOnly = recurrenceRule.ValidUntil ?? ((horizon > recurrenceRule.ValidFrom) ? horizon : recurrenceRule.ValidFrom.AddDays(180));
				ranges.Add((recurrenceRule.ValidFrom, dateOnly.AddDays(1 + (int)Math.Ceiling((double)a.DurationMin / 1440.0))));
			}
		}
		foreach (ActivityException item in state.Exceptions.Where((ActivityException e) => activityIds.Contains(e.ActivityId)))
		{
			Add(item.StartAt, item.EndAt);
		}
		foreach (PlanLeg item2 in state.Legs.Where((PlanLeg l) => activityIds.Contains(l.ToActivityId)))
		{
			Add(item2.DepartAt, item2.ArriveAt);
		}
		List<(DateOnly, DateOnly)> list = new List<(DateOnly, DateOnly)>();
		foreach (var item3 in ranges.OrderBy(((DateOnly From, DateOnly Until) r) => r.From))
		{
			if (list.Count > 0)
			{
				if (item3.From <= list[list.Count - 1].Item2)
				{
					(DateOnly, DateOnly) tuple = list[list.Count - 1];
					list[list.Count - 1] = (tuple.Item1, (item3.Until > tuple.Item2) ? item3.Until : tuple.Item2);
					continue;
				}
			}
			list.Add(item3);
		}
		List<(DateOnly, DateOnly)> list2 = new List<(DateOnly, DateOnly)>();
		foreach (var item4 in list)
		{
			var (dateOnly2, _) = item4;
			while (dateOnly2 < item4.Item2)
			{
				DateOnly dateOnly3 = DateOnly.FromDayNumber(Math.Min(item4.Item2.DayNumber, dateOnly2.DayNumber + 360));
				list2.Add((dateOnly2, dateOnly3));
				dateOnly2 = dateOnly3;
			}
		}
		return list2;
		void Add(DateTime? start, DateTime? end)
		{
			if (start.HasValue)
			{
				DateTime valueOrDefault = start.GetValueOrDefault();
				if (end.HasValue)
				{
					DateTime valueOrDefault2 = end.GetValueOrDefault();
					if (valueOrDefault2 > valueOrDefault)
					{
						ranges.Add((DateOnly.FromDateTime(valueOrDefault), DateOnly.FromDateTime(valueOrDefault2.AddTicks(-1L)).AddDays(1)));
					}
				}
			}
		}
	}
}
