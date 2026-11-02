using System;
using System.Collections.Generic;
using System.Linq;

namespace Planner.Core;

public static class CalendarEngine
{
	public static List<Occurrence> ExpandForDisplay(PlannerState state, DateTime from, DateTime until, IEnumerable<string>? tables = null)
	{
		return Expand(state, from, until, tables, includeInactive: true);
	}

	public static List<Occurrence> Expand(PlannerState state, DateTime from, DateTime until, IEnumerable<string>? tableIds = null, bool includeInactive = false)
	{
		if (until <= from || (until - from).TotalDays > 370.0)
		{
			throw new ArgumentException("请选择不超过370天的有效日期范围。");
		}
		HashSet<string> hashSet = tableIds?.ToHashSet();
		Dictionary<string, Occurrence> result = new Dictionary<string, Occurrence>();
		Dictionary<string, ScheduleTable> dictionary = state.Tables.ToDictionary((ScheduleTable t) => t.Id);
		foreach (Activity activity2 in state.Activities)
		{
			Activity activity = activity2;
			if (!dictionary.TryGetValue(activity.TableId, out var table) || (!includeInactive && !table.Enabled) || (hashSet != null && !hashSet.Contains(table.Id)))
			{
				continue;
			}
			if (activity.Kind == "ONCE")
			{
				DateTime? startAt = activity.StartAt;
				if (startAt.HasValue)
				{
					DateTime valueOrDefault = startAt.GetValueOrDefault();
					startAt = activity.EndAt;
					if (startAt.HasValue)
					{
						DateTime valueOrDefault2 = startAt.GetValueOrDefault();
						Add(null, valueOrDefault, valueOrDefault2);
					}
				}
				continue;
			}
			RecurrenceRule rule = state.Rules.SingleOrDefault((RecurrenceRule r) => r.ActivityId == activity.Id);
			if (rule == null)
			{
				continue;
			}
			Dictionary<DateOnly, ActivityException> dictionary2 = state.Exceptions.Where((ActivityException e) => e.ActivityId == activity.Id).ToDictionary((ActivityException e) => e.OriginDate);
			DateOnly dateOnly = DateOnly.FromDateTime(from.AddMinutes(-activity.DurationMin));
			if (dateOnly < rule.ValidFrom)
			{
				dateOnly = rule.ValidFrom;
			}
			DateOnly dateOnly2 = DateOnly.FromDateTime(until);
			DateOnly dateOnly3 = dateOnly;
			while (dateOnly3 <= dateOnly2)
			{
				if (rule.Matches(dateOnly3))
				{
					dictionary2.TryGetValue(dateOnly3, out var value);
					if (!(value?.Kind == "CANCEL"))
					{
						DateTime start = value?.StartAt ?? dateOnly3.ToDateTime(TimeOnly.Parse(rule.StartTime));
						Add(dateOnly3, start, value?.EndAt ?? start.AddMinutes(activity.DurationMin), value);
					}
				}
				dateOnly3 = dateOnly3.AddDays(1);
			}
			foreach (ActivityException item in dictionary2.Values.Where((ActivityException e) => e.Kind == "OVERRIDE" && rule.Matches(e.OriginDate)))
			{
				DateTime? startAt = item.StartAt;
				if (startAt.HasValue)
				{
					DateTime valueOrDefault3 = startAt.GetValueOrDefault();
					startAt = item.EndAt;
					if (startAt.HasValue)
					{
						DateTime valueOrDefault4 = startAt.GetValueOrDefault();
						Add(item.OriginDate, valueOrDefault3, valueOrDefault4, item);
					}
				}
			}
			void Add(DateOnly? origin, DateTime dateTime, DateTime end, ActivityException? ex = null)
			{
				if (!(dateTime >= until) && !(end <= from) && (includeInactive || table.Active(DateOnly.FromDateTime(dateTime))))
				{
					Occurrence occurrence = new Occurrence(activity, origin, ex?.Title ?? activity.Title, (ex != null && ex.PlaceOverride) ? ex.PlaceId : activity.PlaceId, dateTime, end, ex?.Version ?? 0);
					result[occurrence.Key] = occurrence;
				}
			}
		}
		return (from o in result.Values
			orderby o.Start, o.End
			select o).ToList();
	}

	public static List<Conflict> Conflicts(IEnumerable<Occurrence> items)
	{
		List<Conflict> list = new List<Conflict>();
		List<Occurrence> list2 = new List<Occurrence>();
		foreach (Occurrence item in from x in items
			orderby x.Start, x.End
			select x)
		{
			list2.RemoveAll((Occurrence x) => x.End <= item.Start);
			foreach (Occurrence item2 in list2.Where((Occurrence x) => x.Key != item.Key))
			{
				list.Add(new Conflict("TIME_OVERLAP", item2.Key, item.Key, $"{item2.Title} 与 {item.Title} 在 {item.Start:MM-dd HH:mm} 时间重叠。"));
			}
			list2.Add(item);
		}
		return list;
	}

	public static List<Conflict> AllConflicts(PlannerState state, DateTime from, DateTime until)
	{
		List<Occurrence> list = Expand(state, from, until);
		List<Conflict> list2 = Conflicts(list);
		for (int i = 1; i < list.Count; i++)
		{
			Occurrence a = list[i - 1];
			Occurrence b = list[i];
			if (a.End > b.Start || a.PlaceId == null || b.PlaceId == null || a.PlaceId == b.PlaceId)
			{
				continue;
			}
			PlanLeg planLeg = state.Legs.FirstOrDefault((PlanLeg l) =>
			{
				if (l.FromActivityId == a.Activity.Id)
				{
					DateOnly? fromOriginDate = l.FromOriginDate;
					DateOnly? originDate = a.OriginDate;
					if (fromOriginDate.HasValue == originDate.HasValue && (!fromOriginDate.HasValue || fromOriginDate.GetValueOrDefault() == originDate.GetValueOrDefault()) && l.ToActivityId == b.Activity.Id)
					{
						originDate = l.ToOriginDate;
						fromOriginDate = b.OriginDate;
						if (originDate.HasValue != fromOriginDate.HasValue)
						{
							return false;
						}
						if (!originDate.HasValue)
						{
							return true;
						}
						return originDate.GetValueOrDefault() == fromOriginDate.GetValueOrDefault();
					}
				}
				return false;
			});
			if (planLeg == null || planLeg.ValidationStatus != "VALID")
			{
				list2.Add(new Conflict("UNVERIFIED", a.Key, b.Key, a.Title + " → " + b.Title + "：交通衔接待核验。"));
			}
			else if (planLeg.DepartAt < a.End || planLeg.ArriveAt > b.Start)
			{
				list2.Add(new Conflict("TRAVEL_SHORT", a.Key, b.Key, a.Title + " → " + b.Title + "：交通衔接时间不足。"));
			}
		}
		return list2;
	}
}
