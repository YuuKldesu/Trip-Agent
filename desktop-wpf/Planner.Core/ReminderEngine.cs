namespace Planner.Core;
public sealed record DueReminder(string ActivityId,string Key,DateTime DueAt,string Title,string Label);
public static class ReminderEngine
{
    public static List<DueReminder> MissedToday(PlannerState state,DateTime now)=>Due(state,now,now.Date).Where(x=>x.DueAt<now).ToList();
    public static List<DueReminder> Due(PlannerState state,DateTime now,DateTime since)
    {
        var result=new List<DueReminder>();
        var instances=CalendarEngine.Expand(state,since.Date.AddDays(-7),now.Date.AddDays(8));
        foreach(var o in instances)
        {
            var setting=state.Reminders.FirstOrDefault(r=>r.ActivityId==o.Activity.Id&&r.Enabled);
            if(setting is null)continue;
            var leg=state.Legs.FirstOrDefault(l=>l.ToActivityId==o.Activity.Id&&l.ToOriginDate==o.OriginDate&&l.ValidationStatus=="VALID"&&state.Plans.Any(p=>p.Id==l.PlanId&&p.Status=="CONFIRMED"));
            var departure=setting.Basis=="DEPARTURE"&&leg is not null;
            var due=(departure?leg!.DepartAt:o.Start).AddMinutes(-setting.LeadMin);
            if(due>now||due<since||state.Deliveries.Any(d=>d.InstanceKey==o.Key&&d.DueAt==due))continue;
            result.Add(new(o.Activity.Id,o.Key,due,o.Title,departure?"出发提醒":"活动开始提醒"));
        }
        return result.OrderBy(x=>x.DueAt).ToList();
    }
}
