namespace Planner.Core;
public static class EditConflictGuard
{
    // Weekly patterns change only at rule/table boundaries or explicit dated exceptions.
    // Inspect their neighborhoods, plus all concrete events, without expanding an infinite rule.
    public static List<Conflict> ForEdit(PlannerState before,PlannerState after,DateOnly from)
    {
        var anchors=new HashSet<DateOnly>{from};
        foreach(var state in new[]{before,after})
        {
            foreach(var a in state.Activities){if(a.StartAt is {} s)anchors.Add(DateOnly.FromDateTime(s));if(a.EndAt is {} e)anchors.Add(DateOnly.FromDateTime(e));}
            foreach(var r in state.Rules){anchors.Add(r.ValidFrom);if(r.ValidUntil is {} end)anchors.Add(end);}
            foreach(var t in state.Tables){if(t.ValidFrom is {} s)anchors.Add(s);if(t.ValidUntil is {} e)anchors.Add(e);}
            foreach(var e in state.Exceptions){anchors.Add(e.OriginDate);if(e.StartAt is {} s)anchors.Add(DateOnly.FromDateTime(s));if(e.EndAt is {} end)anchors.Add(DateOnly.FromDateTime(end));}
        }
        var result=new Dictionary<string,Conflict>();
        foreach(var anchor in anchors.Where(d=>d>=from).Order())
        {
            var start=anchor.AddDays(-7);if(start<from)start=from;var until=anchor.AddDays(15);
            var old=CalendarEngine.Expand(before,start.ToDateTime(TimeOnly.MinValue),until.ToDateTime(TimeOnly.MinValue)).ToDictionary(o=>o.Key);
            var changed=CalendarEngine.Expand(after,start.ToDateTime(TimeOnly.MinValue),until.ToDateTime(TimeOnly.MinValue)).Where(o=>!old.TryGetValue(o.Key,out var prior)||prior.Start!=o.Start||prior.End!=o.End||prior.Title!=o.Title||prior.Activity.TableId!=o.Activity.TableId).Select(o=>o.Key).ToHashSet();
            if(changed.Count==0)continue;
            foreach(var c in CalendarEngine.AllConflicts(after,start.ToDateTime(TimeOnly.MinValue),until.ToDateTime(TimeOnly.MinValue)).Where(c=>c.Kind=="TIME_OVERLAP"&&(changed.Contains(c.FirstKey)||c.SecondKey is {} other&&changed.Contains(other))))result[c.FirstKey+"/"+c.SecondKey]=c;
        }
        return result.Values.ToList();
    }
}
