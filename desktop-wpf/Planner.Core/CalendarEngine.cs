namespace Planner.Core;
public static class CalendarEngine
{
    public static List<Occurrence> ExpandForDisplay(PlannerState state,DateTime from,DateTime until,IEnumerable<string>? tables=null)=>Expand(state,from,until,tables,true);
    public static List<Occurrence> Expand(PlannerState state, DateTime from, DateTime until, IEnumerable<string>? tableIds = null,bool includeInactive=false)
    {
        if (until <= from || (until-from).TotalDays > 370) throw new ArgumentException("请选择不超过370天的有效日期范围。");
        var filter = tableIds?.ToHashSet();var result = new Dictionary<string, Occurrence>();
        var tables=state.Tables.ToDictionary(t=>t.Id);
        foreach(var activity in state.Activities)
        {
            if(!tables.TryGetValue(activity.TableId,out var table) || !includeInactive&&!table.Enabled || (filter is not null && !filter.Contains(table.Id))) continue;
            void Add(DateOnly? origin, DateTime start, DateTime end, ActivityException? ex=null)
            {
                if(start>=until || end<=from || !includeInactive&&!table.Active(DateOnly.FromDateTime(start))) return;
                var item=new Occurrence(activity,origin,ex?.Title??activity.Title,ex?.PlaceOverride==true?ex.PlaceId:activity.PlaceId,start,end,ex?.Version??0);
                result[item.Key]=item;
            }
            if(activity.Kind=="ONCE")
            {
                if(activity.StartAt is { } start && activity.EndAt is { } end) Add(null,start,end);
                continue;
            }
            var rule=state.Rules.SingleOrDefault(r=>r.ActivityId==activity.Id);
            if(rule is null) continue;
            var exceptions=state.Exceptions.Where(e=>e.ActivityId==activity.Id).ToDictionary(e=>e.OriginDate);
            var begin=DateOnly.FromDateTime(from.AddMinutes(-activity.DurationMin));
            if(begin<rule.ValidFrom) begin=rule.ValidFrom;
            var last=DateOnly.FromDateTime(until);
            for(var date=begin;date<=last;date=date.AddDays(1))
            {
                if(!rule.Matches(date)) continue;
                exceptions.TryGetValue(date,out var ex);
                if(ex?.Kind=="CANCEL") continue;
                var start=ex?.StartAt??date.ToDateTime(TimeOnly.Parse(rule.StartTime));
                Add(date,start,ex?.EndAt??start.AddMinutes(activity.DurationMin),ex);
            }
            foreach(var ex in exceptions.Values.Where(e=>e.Kind=="OVERRIDE" && rule.Matches(e.OriginDate)))
                if(ex.StartAt is { } start && ex.EndAt is { } end) Add(ex.OriginDate,start,end,ex);
        }
        return result.Values.OrderBy(o=>o.Start).ThenBy(o=>o.End).ToList();
    }
    public static List<Conflict> Conflicts(IEnumerable<Occurrence> items)
    {
        var result=new List<Conflict>();var active=new List<Occurrence>();
        foreach(var item in items.OrderBy(x=>x.Start).ThenBy(x=>x.End))
        {
            active.RemoveAll(x=>x.End<=item.Start);
            foreach(var other in active.Where(x=>x.Key!=item.Key))
                result.Add(new("TIME_OVERLAP",other.Key,item.Key,$"{other.Title} 与 {item.Title} 在 {item.Start:MM-dd HH:mm} 时间重叠。"));
            active.Add(item);
        }
        return result;
    }
    public static List<Conflict> AllConflicts(PlannerState state, DateTime from, DateTime until)
    {
        var items=Expand(state,from,until);var conflicts=Conflicts(items);
        for(var i=1;i<items.Count;i++)
        {
            var a=items[i-1];var b=items[i];
            if(a.End>b.Start) continue;
            if(a.PlaceId is null || b.PlaceId is null || a.PlaceId==b.PlaceId) continue;
            var leg=state.Legs.FirstOrDefault(l=>l.FromActivityId==a.Activity.Id && l.FromOriginDate==a.OriginDate && l.ToActivityId==b.Activity.Id && l.ToOriginDate==b.OriginDate);
            if(leg is null || leg.ValidationStatus!="VALID") conflicts.Add(new("UNVERIFIED",a.Key,b.Key,$"{a.Title} → {b.Title}：交通衔接待核验。"));
            else if(leg.DepartAt<a.End || leg.ArriveAt>b.Start) conflicts.Add(new("TRAVEL_SHORT",a.Key,b.Key,$"{a.Title} → {b.Title}：交通衔接时间不足。"));
        }
        return conflicts;
    }
}
