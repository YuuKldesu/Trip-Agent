using System;
using System.Collections.Generic;
using System.Linq;

namespace Planner.Core;

public static class ReminderEngine
{
	public static List<DueReminder> MissedToday(PlannerState state, DateTime now)
	{
		return (from x in Due(state, now, now.Date)
			where x.DueAt < now
			select x).ToList();
	}

	public static List<DueReminder> Due(PlannerState state, DateTime now, DateTime since)
	{
		List<DueReminder> list = new List<DueReminder>();
		foreach (Occurrence o in CalendarEngine.Expand(state, since.Date.AddDays(-7.0), now.Date.AddDays(8.0)))
		{
			ReminderSetting reminderSetting = state.Reminders.FirstOrDefault((ReminderSetting r) => r.ActivityId == o.Activity.Id && r.Enabled);
			if (reminderSetting == null)
			{
				continue;
			}
			PlanLeg planLeg = state.Legs.FirstOrDefault((PlanLeg l) =>
			{
				if (l.ToActivityId == o.Activity.Id)
				{
					DateOnly? toOriginDate = l.ToOriginDate;
					DateOnly? originDate = o.OriginDate;
					if (toOriginDate.HasValue == originDate.HasValue && (!toOriginDate.HasValue || toOriginDate.GetValueOrDefault() == originDate.GetValueOrDefault()) && l.ValidationStatus == "VALID")
					{
						return state.Plans.Any((Plan p) => p.Id == l.PlanId && p.Status == "CONFIRMED");
					}
				}
				return false;
			});
			bool flag = reminderSetting.Basis == "DEPARTURE" && planLeg != null;
			DateTime due = (flag ? planLeg.DepartAt : o.Start).AddMinutes(-reminderSetting.LeadMin);
			if (!(due > now) && !(due < since) && !state.Deliveries.Any((ReminderDelivery d) => d.InstanceKey == o.Key && d.DueAt == due))
			{
				list.Add(new DueReminder(o.Activity.Id, o.Key, due, o.Title, flag ? "出发提醒" : "活动开始提醒"));
			}
		}
		return list.OrderBy((DueReminder x) => x.DueAt).ToList();
	}
}
