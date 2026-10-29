namespace Planner.Core;
public sealed record LocalSearchHit(string Kind,string Id,string TableId,string Title,string Detail,DateOnly? Day);
public static class LocalSearch
{
    public static List<LocalSearchHit> Find(PlannerState state,string query,DateOnly today)
    {
        var words=query.Trim().Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries);if(words.Length==0)return [];
        bool Match(string text)=>words.All(w=>text.Contains(w,StringComparison.OrdinalIgnoreCase));
        var result=new List<LocalSearchHit>();
        foreach(var table in state.Tables)if(Match(table.Name))result.Add(new("table",table.Id,table.Id,table.Name,"行程表",null));
        foreach(var a in state.Activities)
        {
            var table=state.Tables.FirstOrDefault(t=>t.Id==a.TableId);if(table is null)continue;
            string place=state.PlaceName(a.PlaceId);var exceptions=state.Exceptions.Where(e=>e.ActivityId==a.Id);
            if(!Match(string.Join(" ",a.Title,place,a.Note,table.Name,string.Join(" ",exceptions.Select(e=>e.Title+" "+state.PlaceName(e.PlaceId))))))continue;
            DateOnly? day=a.StartAt is {} start?DateOnly.FromDateTime(start):null;
            if(a.Kind=="WEEKLY"&&state.Rules.FirstOrDefault(r=>r.ActivityId==a.Id) is {} rule){var from=today>rule.ValidFrom?today:rule.ValidFrom;if(rule.ValidUntil is {} last&&last<from)from=last.AddDays(-7);var occurrences=CalendarEngine.ExpandForDisplay(state,from.ToDateTime(TimeOnly.MinValue),from.AddDays(15).ToDateTime(TimeOnly.MinValue),[a.TableId]).Where(o=>o.Activity.Id==a.Id).ToList();day=occurrences.Count>0?DateOnly.FromDateTime(occurrences[0].Start):rule.ValidFrom;}
            result.Add(new("activity",a.Id,a.TableId,a.Title,(a.Kind=="WEEKLY"?"每周重复 · ":a.StartAt?.ToString("yyyy-MM-dd HH:mm")+" · ")+place+" · "+table.Name,day));
        }
        return result.OrderBy(r=>r.Kind=="table"?0:1).ThenBy(r=>r.Day).ThenBy(r=>r.Title).ToList();
    }
}
