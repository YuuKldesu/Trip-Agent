namespace Planner.Core;

public sealed class TripRequest
{
    public DateOnly? Day {get;set;}
    public string Title {get;set;}="";
    public string Location {get;set;}="";
    public int? Duration {get;set;}
    public int? Start {get;set;}
    public int? End {get;set;}
    public int? Earliest {get;set;}
    public int? LatestEnd {get;set;}
    public string Notes {get;set;}="";
    public string? ModeIn {get;set;}
    public bool IsReturn {get;set;}
}
public sealed record PreparedTrip(TripRequest Request,Place Place);
public sealed record TravelEstimate(int Minutes,string Source);
public sealed record PlannedTrip(PreparedTrip Trip,int Start,TravelEstimate Before,TravelEstimate After,string? PreviousTitle,string? NextTitle);
public sealed record RepositoryPlan(long Revision,IReadOnlyList<PlannedTrip> Rows);

// Port of app.py plan/plan_prepared: fixed existing events, ordered first feasible gap.
public static class RepositoryPlanner
{
    sealed record Busy(int Start,int End,Place? Place,string Title);
    public static void Validate(TripRequest r)
    {
        if(r.Day is null||string.IsNullOrWhiteSpace(r.Title)||string.IsNullOrWhiteSpace(r.Location)||r.Title.Length>200||r.Duration is null||r.Duration<(r.IsReturn?0:1)||r.Duration>720||r.Start is <0 or >=1440||r.Earliest is <0 or >1440||r.LatestEnd is <0 or >1440||r.End is <0 or >1440)
            throw new ArgumentException("请完善日期、活动、地点及1—720分钟的耗时；返程可为0分钟。");
    }
    public static async Task<RepositoryPlan> PlanAsync(PlannerState state,IReadOnlyList<PreparedTrip> requests,Func<Place,Place,string?,DateTime,CancellationToken,Task<TravelEstimate>> route,CancellationToken token=default,string? excludeKey=null)
    {
        if(requests.Count is <1 or >15)throw new ArgumentException("每次支持1—15项活动。");
        var working=new Dictionary<DateOnly,List<Busy>>();var previousNew=new Dictionary<DateOnly,int>();var rows=new List<PlannedTrip>();
        foreach(var item in requests)
        {
            token.ThrowIfCancellationRequested();var e=item.Request;Validate(e);var day=e.Day!.Value;int duration=Math.Max(1,e.Duration!.Value);
            if(!working.TryGetValue(day,out var existing))
            {
                var begin=day.ToDateTime(TimeOnly.MinValue);var end=begin.AddDays(1);
                existing=CalendarEngine.Expand(state,begin,end).Where(o=>o.Key!=excludeKey).Select(o=>new Busy((int)(o.Start-begin).TotalMinutes,(int)(o.End-begin).TotalMinutes,state.Places.FirstOrDefault(p=>p.Id==o.PlaceId),o.Title)).OrderBy(o=>o.Start).ToList();working[day]=existing;
            }
            int ordered=Math.Max(e.Earliest??0,previousNew.GetValueOrDefault(day));PlannedTrip? found=null;
            for(int i=0;i<=existing.Count;i++)
            {
                var prev=i>0?existing.Take(i).MaxBy(o=>o.End):null;var next=i<existing.Count?existing[i]:null;
                int low=Math.Max(480,Math.Max(ordered,prev?.End??480));int high=Math.Min(1320-duration,Math.Min((e.LatestEnd??1320)-duration,(next?.Start??1320)-duration));
                if(low>high||(e.Start is {} fixedStart&&(fixedStart<low||fixedStart>high)))continue;
                async Task<TravelEstimate> Leg(Place? from,Place? to,string? mode,int at)
                {
                    if(from is null||to is null)throw new ArgumentException("相邻活动有未确认地点，请先完善地点后重试。");
                    var estimate=await route(from,to,mode,day.ToDateTime(TimeOnly.MinValue).AddMinutes(Math.Max(0,at)),token);
                    if(estimate.Minutes<0)throw new InvalidOperationException("交通耗时不合法。");return estimate;
                }
                var before=prev is null?new TravelEstimate(0,"无前序行程"):await Leg(prev.Place,item.Place,e.ModeIn,prev.End);
                low=Math.Max(480,Math.Max(ordered,(prev?.End??480)+(prev is null?0:before.Minutes)));
                var after=next is null?new TravelEstimate(0,"无后续行程"):await Leg(item.Place,next.Place,null,(e.Start??low)+duration);
                high=Math.Min(high,(next?.Start??1320)-duration-after.Minutes);
                int candidate=e.Start??low;if(candidate<low||candidate>high)continue;
                found=new(item,candidate,before,after,prev?.Title,next?.Title);break;
            }
            if(found is null)throw new ArgumentException($"“{e.Title}”无法在08:00—22:00内安排，或指定时间与已有活动及交通冲突。");
            rows.Add(found);existing.Add(new(found.Start,found.Start+duration,item.Place,e.Title));existing.Sort((a,b)=>a.Start.CompareTo(b.Start));previousNew[day]=found.Start+duration;
        }
        return new(state.Revision,rows);
    }
    public static List<EditCommand> Commands(RepositoryPlan plan,string tableId)=>plan.Rows.Select(row=>
    {
        var r=row.Trip.Request;var begin=r.Day!.Value.ToDateTime(TimeOnly.MinValue).AddMinutes(row.Start);
        // Arrival-only markers occupy one minute in the existing nonzero-duration storage model.
        var duration=Math.Max(1,r.Duration!.Value);var note=r.Notes+(r.IsReturn?"\n返程到达标记（存储占1分钟）":"");
        if(row.PreviousTitle is not null)note+=$"\n前往方式：{row.Before.Source}，约{row.Before.Minutes}分钟";
        return new EditCommand{Activity=new(){TableId=tableId,Title=r.Title,StartAt=begin,EndAt=begin.AddMinutes(duration),DurationMin=duration,Note=note.Trim()},Place=row.Trip.Place};
    }).ToList();
}
