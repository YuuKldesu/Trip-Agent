using System;
using System.Collections.Generic;
using System.Linq;

namespace Planner.Core;

public static class EditConflictGuard
{
	public static List<Conflict> ForEdit(PlannerState before, PlannerState after, DateOnly from)
	{
		HashSet<DateOnly> hashSet = new HashSet<DateOnly> { from };
		PlannerState[] array = new PlannerState[2] { before, after };
		foreach (PlannerState plannerState in array)
		{
			foreach (Activity activity in plannerState.Activities)
			{
				DateTime? startAt = activity.StartAt;
				if (startAt.HasValue)
				{
					DateTime valueOrDefault = startAt.GetValueOrDefault();
					hashSet.Add(DateOnly.FromDateTime(valueOrDefault));
				}
				startAt = activity.EndAt;
				if (startAt.HasValue)
				{
					DateTime valueOrDefault2 = startAt.GetValueOrDefault();
					hashSet.Add(DateOnly.FromDateTime(valueOrDefault2));
				}
			}
			foreach (RecurrenceRule rule in plannerState.Rules)
			{
				hashSet.Add(rule.ValidFrom);
				DateOnly? validUntil = rule.ValidUntil;
				if (validUntil.HasValue)
				{
					DateOnly valueOrDefault3 = validUntil.GetValueOrDefault();
					hashSet.Add(valueOrDefault3);
				}
			}
			foreach (ScheduleTable table in plannerState.Tables)
			{
				DateOnly? validUntil = table.ValidFrom;
				if (validUntil.HasValue)
				{
					DateOnly valueOrDefault4 = validUntil.GetValueOrDefault();
					hashSet.Add(valueOrDefault4);
				}
				validUntil = table.ValidUntil;
				if (validUntil.HasValue)
				{
					DateOnly valueOrDefault5 = validUntil.GetValueOrDefault();
					hashSet.Add(valueOrDefault5);
				}
			}
			foreach (ActivityException exception in plannerState.Exceptions)
			{
				hashSet.Add(exception.OriginDate);
				DateTime? startAt = exception.StartAt;
				if (startAt.HasValue)
				{
					DateTime valueOrDefault6 = startAt.GetValueOrDefault();
					hashSet.Add(DateOnly.FromDateTime(valueOrDefault6));
				}
				startAt = exception.EndAt;
				if (startAt.HasValue)
				{
					DateTime valueOrDefault7 = startAt.GetValueOrDefault();
					hashSet.Add(DateOnly.FromDateTime(valueOrDefault7));
				}
			}
		}
		Dictionary<string, Conflict> dictionary = new Dictionary<string, Conflict>();
		foreach (DateOnly item in hashSet.Where((DateOnly d) => d >= from).Order())
		{
			DateOnly dateOnly = item.AddDays(-7);
			if (dateOnly < from)
			{
				dateOnly = from;
			}
			DateOnly dateOnly2 = item.AddDays(15);
			Dictionary<string, Occurrence> old = CalendarEngine.Expand(before, dateOnly.ToDateTime(TimeOnly.MinValue), dateOnly2.ToDateTime(TimeOnly.MinValue)).ToDictionary((Occurrence o) => o.Key);
			HashSet<string> changed = (from o in CalendarEngine.Expand(after, dateOnly.ToDateTime(TimeOnly.MinValue), dateOnly2.ToDateTime(TimeOnly.MinValue))
				where !old.TryGetValue(o.Key, out var value) || value.Start != o.Start || value.End != o.End || value.Title != o.Title || value.Activity.TableId != o.Activity.TableId
				select o.Key).ToHashSet();
			if (changed.Count == 0)
			{
				continue;
			}
			foreach (Conflict item2 in CalendarEngine.AllConflicts(after, dateOnly.ToDateTime(TimeOnly.MinValue), dateOnly2.ToDateTime(TimeOnly.MinValue)).Where((Conflict c) =>
			{
				if (c.Kind == "TIME_OVERLAP")
				{
					if (!changed.Contains(c.FirstKey))
					{
						string secondKey = c.SecondKey;
						if (secondKey != null)
						{
							return changed.Contains(secondKey);
						}
						return false;
					}
					return true;
				}
				return false;
			}))
			{
				dictionary[item2.FirstKey + "/" + item2.SecondKey] = item2;
			}
		}
		return dictionary.Values.ToList();
	}
}
