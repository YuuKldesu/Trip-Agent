namespace Planner.Core;

// Windows are an internal expansion detail, never month pages in the UI.
public static class ContinuousList
{
    public static List<(DateOnly From,DateOnly Until)> Windows(PlannerState state,DateOnly horizon,IEnumerable<string> visibleTables)
    {
        var visible=visibleTables.ToHashSet();var activityIds=state.Activities.Where(a=>visible.Contains(a.TableId)).Select(a=>a.Id).ToHashSet();
        var ranges=new List<(DateOnly From,DateOnly Until)>();
        void Add(DateTime? start,DateTime? end){if(start is {} s&&end is {} e&&e>s)ranges.Add((DateOnly.FromDateTime(s),DateOnly.FromDateTime(e.AddTicks(-1)).AddDays(1)));}
        foreach(var a in state.Activities.Where(a=>activityIds.Contains(a.Id)))
        {
            if(a.Kind=="ONCE")Add(a.StartAt,a.EndAt);
            else if(state.Rules.FirstOrDefault(r=>r.ActivityId==a.Id) is {} rule)
            {
                var last=rule.ValidUntil??(horizon>rule.ValidFrom?horizon:rule.ValidFrom.AddDays(180));
                ranges.Add((rule.ValidFrom,last.AddDays(1+(int)Math.Ceiling(a.DurationMin/1440d))));
            }
        }
        foreach(var ex in state.Exceptions.Where(e=>activityIds.Contains(e.ActivityId)))Add(ex.StartAt,ex.EndAt);
        foreach(var leg in state.Legs.Where(l=>activityIds.Contains(l.ToActivityId)))Add(leg.DepartAt,leg.ArriveAt);
        var merged=new List<(DateOnly From,DateOnly Until)>();
        foreach(var range in ranges.OrderBy(r=>r.From))
        {
            if(merged.Count>0&&range.From<=merged[^1].Until){var prev=merged[^1];merged[^1]=(prev.From,range.Until>prev.Until?range.Until:prev.Until);}
            else merged.Add(range);
        }
        var windows=new List<(DateOnly From,DateOnly Until)>();
        foreach(var range in merged)for(var from=range.From;from<range.Until;){var until=DateOnly.FromDayNumber(Math.Min(range.Until.DayNumber,from.DayNumber+360));windows.Add((from,until));from=until;}
        return windows;
    }
}
