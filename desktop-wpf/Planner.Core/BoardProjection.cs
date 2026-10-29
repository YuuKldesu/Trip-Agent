namespace Planner.Core;
public sealed record BoardEntry(Occurrence Occurrence,DateOnly Day,DateTime Start,DateTime End)
{
    public PlanLeg? Transfer {get;init;}
    public bool ContinuesFromYesterday=>Start>Occurrence.Start;
    public bool ContinuesTomorrow=>End<Occurrence.End;
    public int DayMinutes=>(int)(End-Start).TotalMinutes;
    public int TotalMinutes=>(int)(Occurrence.End-Occurrence.Start).TotalMinutes;
    public string DurationText=>DayMinutes==TotalMinutes?$"耗时：{TotalMinutes} 分钟":$"本日 {DayMinutes} 分钟 · 全程 {TotalMinutes} 分钟";
    public string ContinuityText=>ContinuesFromYesterday&&ContinuesTomorrow?"跨日接续 · 始于 "+Occurrence.Start.ToString("MM-dd HH:mm")+"，至 "+Occurrence.End.ToString("MM-dd HH:mm"):ContinuesFromYesterday?"接续 · 始于 "+Occurrence.Start.ToString("MM-dd HH:mm"):ContinuesTomorrow?"跨日 · 接续至 "+Occurrence.End.ToString("MM-dd HH:mm"):"";
    public string TimeText=>Start.ToString("HH:mm")+"—"+(End.Date>Start.Date&&End.TimeOfDay==TimeSpan.Zero?"24:00":End.ToString("HH:mm"));
}
public sealed record BoardDay(DateOnly Date,IReadOnlyList<BoardEntry> Entries);
public static class BoardProjection
{
    public static List<DateOnly> MonthDays(DateOnly month)
    {
        var first=new DateOnly(month.Year,month.Month,1);var start=Clock.Monday(first);var last=first.AddMonths(1).AddDays(-1);var end=Clock.Monday(last).AddDays(7);
        return Enumerable.Range(0,end.DayNumber-start.DayNumber).Select(start.AddDays).ToList();
    }
    public static List<BoardDay> Range(PlannerState state,DateOnly from,DateOnly until,IEnumerable<string>? visibleTables=null)
    {
        var entries=CalendarEngine.ExpandForDisplay(state,from.ToDateTime(TimeOnly.MinValue),until.ToDateTime(TimeOnly.MinValue),visibleTables);var result=new List<BoardDay>();var visible=visibleTables?.ToHashSet();var travel=new List<(Occurrence Occurrence,PlanLeg Leg)>();foreach(var leg in state.Legs.Where(l=>l.ArriveAt>l.DepartAt)){var target=state.Activities.FirstOrDefault(a=>a.Id==leg.ToActivityId);if(target is null||visible is not null&&!visible.Contains(target.TableId))continue;var origin=state.Activities.FirstOrDefault(a=>a.Id==leg.FromActivityId);string fromName=state.PlaceName(origin?.PlaceId??state.Plans.FirstOrDefault(p=>p.Id==leg.PlanId)?.StartPlaceId);string to=state.PlaceName(target.PlaceId);var description=string.Join("；",state.Segments.Where(s=>s.LegId==leg.Id).OrderBy(s=>s.Seq).Select(s=>s.Instruction??s.Mode));var activity=new Activity{Id="route:"+leg.Id,TableId=target.TableId,Title="交通｜"+fromName+" → "+to,PlaceId=target.PlaceId,StartAt=leg.DepartAt,EndAt=leg.ArriveAt,DurationMin=(int)Math.Ceiling((leg.ArriveAt-leg.DepartAt).TotalMinutes),Note=description+"\n"+(leg.ValidationStatus=="VALID"?"地图路线查询快照":leg.ValidationStatus=="STALE"?"起终点或安排已变动，交通需重新核验":"交通粗估，未联网核验")};travel.Add((new(activity,null,activity.Title,activity.PlaceId,leg.DepartAt,leg.ArriveAt),leg));}
        for(var day=from;day<until;day=day.AddDays(1))
        {
            var start=day.ToDateTime(TimeOnly.MinValue);var end=start.AddDays(1);
            result.Add(new(day,entries.Where(o=>o.Start<end&&o.End>start).Select(o=>new BoardEntry(o,day,o.Start<start?start:o.Start,o.End>end?end:o.End)).Concat(travel.Where(t=>t.Occurrence.Start<end&&t.Occurrence.End>start).Select(t=>new BoardEntry(t.Occurrence,day,t.Occurrence.Start<start?start:t.Occurrence.Start,t.Occurrence.End>end?end:t.Occurrence.End){Transfer=t.Leg})).OrderBy(e=>e.Start).ThenBy(e=>e.Occurrence.Key).ToList()));
        }
        return result;
    }
    public static EditCommand MoveToDay(PlannerState state,Occurrence occurrence,DateOnly day)
    {
        int delta=day.DayNumber-DateOnly.FromDateTime(occurrence.Start).DayNumber;var a=JsonKit.Clone(occurrence.Activity);a.Title=occurrence.Title;a.PlaceId=occurrence.PlaceId;a.StartAt=occurrence.Start.AddDays(delta);a.EndAt=occurrence.End.AddDays(delta);a.DurationMin=(int)(occurrence.End-occurrence.Start).TotalMinutes;
        if(a.Kind=="ONCE"&&a.ConstraintType=="FLEXIBLE"){a.WindowStart=a.WindowStart?.AddDays(delta);a.WindowEnd=a.WindowEnd?.AddDays(delta);}
        var reminder=state.Reminders.FirstOrDefault(r=>r.ActivityId==a.Id);
        return new(){Activity=a,OriginDate=occurrence.OriginDate,ReminderEnabled=reminder?.Enabled??false,ReminderLeadMin=reminder?.LeadMin??15};
    }
}
