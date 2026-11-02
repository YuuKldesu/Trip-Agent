using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Planner.Core;

public static class LocalSearch
{
	public static List<LocalSearchHit> Find(PlannerState state, string query, DateOnly today)
	{
		string[] words = query.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
		if (words.Length == 0)
		{
			return new List<LocalSearchHit>();
		}
		List<LocalSearchHit> list = new List<LocalSearchHit>();
		foreach (ScheduleTable table in state.Tables)
		{
			if (Match(table.Name))
			{
				list.Add(new LocalSearchHit("table", table.Id, table.Id, table.Name, "行程表", null));
			}
		}
		foreach (Activity a in state.Activities)
		{
			ScheduleTable scheduleTable = state.Tables.FirstOrDefault((ScheduleTable t) => t.Id == a.TableId);
			if (scheduleTable == null)
			{
				continue;
			}
			string text = state.PlaceName(a.PlaceId);
			IEnumerable<ActivityException> source = state.Exceptions.Where((ActivityException e) => e.ActivityId == a.Id);
			InlineArray5<string> buffer = default;
			buffer[0] = a.Title;
			buffer[1] = text;
			buffer[2] = a.Note;
			buffer[3] = scheduleTable.Name;
			buffer[4] = string.Join(" ", source.Select((ActivityException e) => e.Title + " " + state.PlaceName(e.PlaceId)));
			if (!Match(string.Join(" ", (ReadOnlySpan<string?>)buffer)))
			{
				continue;
			}
			DateTime? startAt = a.StartAt;
			DateOnly? dateOnly;
			if (startAt.HasValue)
			{
				DateTime valueOrDefault = startAt.GetValueOrDefault();
				dateOnly = DateOnly.FromDateTime(valueOrDefault);
			}
			else
			{
				dateOnly = null;
			}
			DateOnly? day = dateOnly;
			if (a.Kind == "WEEKLY")
			{
				RecurrenceRule recurrenceRule = state.Rules.FirstOrDefault((RecurrenceRule r) => r.ActivityId == a.Id);
				if (recurrenceRule != null)
				{
					DateOnly dateOnly2 = ((today > recurrenceRule.ValidFrom) ? today : recurrenceRule.ValidFrom);
					DateOnly? validUntil = recurrenceRule.ValidUntil;
					if (validUntil.HasValue)
					{
						DateOnly valueOrDefault2 = validUntil.GetValueOrDefault();
						if (valueOrDefault2 < dateOnly2)
						{
							dateOnly2 = valueOrDefault2.AddDays(-7);
						}
					}
					List<Occurrence> list2 = (from o in CalendarEngine.ExpandForDisplay(state, dateOnly2.ToDateTime(TimeOnly.MinValue), dateOnly2.AddDays(15).ToDateTime(TimeOnly.MinValue), new _003C_003Ez__ReadOnlySingleElementList<string>(a.TableId))
						where o.Activity.Id == a.Id
						select o).ToList();
					day = ((list2.Count > 0) ? DateOnly.FromDateTime(list2[0].Start) : recurrenceRule.ValidFrom);
				}
			}
			list.Add(new LocalSearchHit("activity", a.Id, a.TableId, a.Title, ((a.Kind == "WEEKLY") ? "每周重复 · " : (a.StartAt?.ToString("yyyy-MM-dd HH:mm") + " · ")) + text + " · " + scheduleTable.Name, day));
		}
		return (from r in list
			orderby (!(r.Kind == "table")) ? 1 : 0, r.Day, r.Title
			select r).ToList();
		bool Match(string text2)
		{
			return words.All((string w) => text2.Contains(w, StringComparison.OrdinalIgnoreCase));
		}
	}
}
