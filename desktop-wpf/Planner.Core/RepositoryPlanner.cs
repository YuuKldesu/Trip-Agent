namespace Planner.Core;

public sealed class TripRequest
{
    public DateOnly? Day {get;set;}
    public DateOnly? EndDay {get;set;}
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
        if(r.Day is null||string.IsNullOrWhiteSpace(r.Title)||string.IsNullOrWhiteSpace(r.Location)||r.Title.Length>200||r.Duration is null||r.Duration<(r.IsReturn?0:1)||r.Duration>10080||r.Start is <0 or >=1440||r.Earliest is <0 or >1440||r.LatestEnd is <0 or >11520||r.End is <0 or >1440)
            throw new ArgumentException("请完善日期、活动、地点及1—10080分钟的耗时；返程可为0分钟。");
        if(r.EndDay is {} endDay){if(r.Start is null||r.End is null)throw new ArgumentException("跨日活动请明确起止日期和时间。");int duration=(endDay.DayNumber-r.Day.Value.DayNumber)*1440+r.End.Value-r.Start.Value;if(duration!=r.Duration||duration<(r.IsReturn?0:1))throw new ArgumentException("跨日起止时间与耗时不一致，请重新核对。");}
    }
    public static async Task<RepositoryPlan> PlanAsync(PlannerState state,IReadOnlyList<PreparedTrip> requests,Func<Place,Place,string?,DateTime,CancellationToken,Task<TravelEstimate>> route,CancellationToken token=default,string? excludeKey=null)
    {
        if(requests.Count is <1 or >15)throw new ArgumentException("每次支持1—15项活动。");
        var working=state.Copy();var provisional=new ScheduleTable{Name="本次排程约束"};working.Tables.Add(provisional);var previousNew=new Dictionary<DateOnly,int>();var rows=new List<PlannedTrip>();
        foreach(var item in requests)
        {
            token.ThrowIfCancellationRequested();var e=item.Request;Validate(e);var day=e.Day!.Value;int duration=Math.Max(1,e.Duration!.Value);
            var begin=day.ToDateTime(TimeOnly.MinValue);var horizon=begin.AddMinutes((e.Start??480)+duration).Date.AddDays(1);var existing=CalendarEngine.Expand(working,begin,horizon).Where(o=>o.Key!=excludeKey).Select(o=>new Busy((int)(o.Start-begin).TotalMinutes,(int)(o.End-begin).TotalMinutes,working.Places.FirstOrDefault(p=>p.Id==o.PlaceId),o.Title)).OrderBy(o=>o.Start).ToList();
            int baseStart=e.Start is not null?0:480,finishLimit=e.Start is {} fixedAt?fixedAt+duration:1320;
            int ordered=Math.Max(e.Earliest??0,previousNew.GetValueOrDefault(day));PlannedTrip? found=null;
            for(int i=0;i<=existing.Count;i++)
            {
                var prev=i>0?existing.Take(i).MaxBy(o=>o.End):null;var next=i<existing.Count?existing[i]:null;
                int low=Math.Max(baseStart,Math.Max(ordered,prev?.End??baseStart));int high=Math.Min(finishLimit-duration,Math.Min((e.LatestEnd??finishLimit)-duration,(next?.Start??finishLimit)-duration));
                if(low>high||(e.Start is {} fixedStart&&(fixedStart<low||fixedStart>high)))continue;
                async Task<TravelEstimate> Leg(Place? from,Place? to,string? mode,int at)
                {
                    if(from is null||to is null)throw new ArgumentException("相邻活动有未确认地点，请先完善地点后重试。");
                    var estimate=await route(from,to,mode,day.ToDateTime(TimeOnly.MinValue).AddMinutes(Math.Max(0,at)),token);
                    if(estimate.Minutes<0)throw new InvalidOperationException("交通耗时不合法。");return estimate;
                }
                var before=prev is null?new TravelEstimate(0,"无前序行程"):await Leg(prev.Place,item.Place,e.ModeIn,prev.End);
                low=Math.Max(baseStart,Math.Max(ordered,(prev?.End??baseStart)+(prev is null?0:before.Minutes)));
                var after=next is null?new TravelEstimate(0,"无后续行程"):await Leg(item.Place,next.Place,null,(e.Start??low)+duration);
                high=Math.Min(high,(next?.Start??finishLimit)-duration-after.Minutes);
                int candidate=e.Start??low;if(candidate<low||candidate>high)continue;
                found=new(item,candidate,before,after,prev?.Title,next?.Title);break;
            }
            if(found is null)throw new ArgumentException($"“{e.Title}”无法在指定时间与已有活动及交通之间安排；未指定开始时间时默认在08:00—22:00寻找空档。");
            rows.Add(found);if(!working.Places.Any(p=>p.Id==item.Place.Id))working.Places.Add(JsonKit.Clone(item.Place));working.Activities.Add(new(){TableId=provisional.Id,Title=e.Title,PlaceId=item.Place.Id,StartAt=begin.AddMinutes(found.Start),EndAt=begin.AddMinutes(found.Start+duration),DurationMin=duration});previousNew[day]=found.Start+duration;
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
