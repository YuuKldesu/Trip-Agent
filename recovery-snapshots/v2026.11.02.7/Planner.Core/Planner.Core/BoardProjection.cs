using System;
using System.Collections.Generic;
using System.Linq;

namespace Planner.Core;

public static class BoardProjection
{
	public static List<DateOnly> MonthDays(DateOnly month)
	{
		DateOnly date = new DateOnly(month.Year, month.Month, 1);
		DateOnly dateOnly = Clock.Monday(date);
		return Enumerable.Range(0, Clock.Monday(date.AddMonths(1).AddDays(-1)).AddDays(7).DayNumber - dateOnly.DayNumber).Select(((DateOnly)dateOnly).AddDays).ToList();
	}

	public static List<BoardDay> Range(PlannerState state, DateOnly from, DateOnly until, IEnumerable<string>? visibleTables = null)
	{
		List<Occurrence> source = CalendarEngine.ExpandForDisplay(state, from.ToDateTime(TimeOnly.MinValue), until.ToDateTime(TimeOnly.MinValue), visibleTables);
		List<BoardDay> list = new List<BoardDay>();
		HashSet<string> hashSet = visibleTables?.ToHashSet();
		List<(Occurrence, PlanLeg)> list2 = new List<(Occurrence, PlanLeg)>();
		foreach (PlanLeg leg in state.Legs.Where((PlanLeg l) => l.ArriveAt > l.DepartAt))
		{
			Activity activity = state.Activities.FirstOrDefault((Activity a) => a.Id == leg.ToActivityId);
			if (activity == null)
			{
				continue;
			}
			string ownerTable = TransferEndpoints.Snapshot(state, leg, "ownership_table_id") ?? activity.TableId;
			if (state.Tables.Any((ScheduleTable t) => t.Id == ownerTable) && (hashSet == null || hashSet.Contains(ownerTable)))
			{
				state.Activities.FirstOrDefault((Activity a) => a.Id == leg.FromActivityId);
				string text = TransferEndpoints.Snapshot(state, leg, "from_name") ?? state.PlaceName(TransferEndpoints.PlaceId(state, leg.FromActivityId, leg.FromOriginDate) ?? state.Plans.FirstOrDefault((Plan p) => p.Id == leg.PlanId)?.StartPlaceId);
				string text2 = TransferEndpoints.Snapshot(state, leg, "to_name") ?? state.PlaceName(TransferEndpoints.PlaceId(state, leg.ToActivityId, leg.ToOriginDate));
				string text3 = string.Join("；", from s in state.Segments
					where s.LegId == leg.Id
					orderby s.Seq
					select s.Instruction ?? s.Mode);
				Activity activity2 = new Activity
				{
					Id = "route:" + leg.Id,
					TableId = ownerTable,
					Title = "交通｜" + text + " → " + text2,
					PlaceId = activity.PlaceId,
					StartAt = leg.DepartAt,
					EndAt = leg.ArriveAt,
					DurationMin = (int)Math.Ceiling((leg.ArriveAt - leg.DepartAt).TotalMinutes),
					Note = text3 + "\n" + ((leg.ValidationStatus == "VALID") ? "地图路线查询快照" : ((leg.ValidationStatus == "STALE") ? "起终点或安排已变动，交通需重新核验" : "交通粗估，未联网核验"))
				};
				list2.Add((new Occurrence(activity2, null, activity2.Title, activity2.PlaceId, leg.DepartAt, leg.ArriveAt), leg));
			}
		}
		DateOnly day = from;
		while (day < until)
		{
			DateTime start = day.ToDateTime(TimeOnly.MinValue);
			DateTime end = start.AddDays(1.0);
			list.Add(new BoardDay(day, (from e in (from o in source
					where o.Start < end && o.End > start
					select new BoardEntry(o, day, (o.Start < start) ? start : o.Start, (o.End > end) ? end : o.End)).Concat(list2.Where(((Occurrence Occurrence, PlanLeg Leg) t) => t.Occurrence.Start < end && t.Occurrence.End > start).Select(((Occurrence Occurrence, PlanLeg Leg) t) => new BoardEntry(t.Occurrence, day, (t.Occurrence.Start < start) ? start : t.Occurrence.Start, (t.Occurrence.End > end) ? end : t.Occurrence.End)
				{
					Transfer = t.Leg
				}))
				orderby e.Start, e.Occurrence.Key
				select e).ToList()));
			day = day.AddDays(1);
		}
		return list;
	}

	public static EditCommand MoveToDay(PlannerState state, Occurrence occurrence, DateOnly day)
	{
		int num = day.DayNumber - DateOnly.FromDateTime(occurrence.Start).DayNumber;
		Activity a = JsonKit.Clone(occurrence.Activity);
		a.Title = occurrence.Title;
		a.PlaceId = occurrence.PlaceId;
		a.StartAt = occurrence.Start.AddDays(num);
		a.EndAt = occurrence.End.AddDays(num);
		a.DurationMin = (int)(occurrence.End - occurrence.Start).TotalMinutes;
		if (a.Kind == "ONCE" && a.ConstraintType == "FLEXIBLE")
		{
			a.WindowStart = a.WindowStart?.AddDays(num);
			a.WindowEnd = a.WindowEnd?.AddDays(num);
		}
		ReminderSetting reminderSetting = state.Reminders.FirstOrDefault((ReminderSetting r) => r.ActivityId == a.Id);
		return new EditCommand
		{
			Activity = a,
			OriginDate = occurrence.OriginDate,
			ReminderEnabled = (reminderSetting?.Enabled ?? false),
			ReminderLeadMin = (reminderSetting?.LeadMin ?? 15)
		};
	}
}
