using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace Planner.Core;

public sealed record TrafficPair(Occurrence From,Occurrence To)
{
    public int GapMinutes=>(int)Math.Floor((To.Start-From.End).TotalMinutes);
    public string Fingerprint(PlannerState state)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new[]{Stamp(From,state),Stamp(To,state)},JsonKit.Options))));
    static object Stamp(Occurrence o,PlannerState state){var p=state.Places.FirstOrDefault(p=>p.Id==o.PlaceId);return new{o.Key,o.Start,o.End,o.PlaceId,p?.Name,p?.Lng,p?.Lat};}
}
public sealed class TrafficSnapshot
{
    public string Fingerprint {get;set;}="";
    public string FromKey {get;set;}="";public string ToKey {get;set;}="";
    public int? Minutes {get;set;}public string Mode {get;set;}="";public string Source {get;set;}="";
    public DateTime CheckedAt {get;set;}=Clock.Now;
    public bool Fresh(DateTime now)=>CheckedAt<=now.AddMinutes(1)&&now-CheckedAt<TimeSpan.FromHours(24);
}
public static class AdjacentTraffic
{
    public static List<TrafficPair> Around(PlannerState state,Occurrence target)
    {
        var items=CalendarEngine.Expand(state,target.Start.Date.AddDays(-7),target.End.Date.AddDays(8)).Where(o=>o.Key!=target.Key).ToList();var result=new List<TrafficPair>();
        var before=items.Where(o=>o.End<=target.Start).MaxBy(o=>o.End);var after=items.Where(o=>o.Start>=target.End).MinBy(o=>o.Start);
        if(before is not null&&(target.Start-before.End).TotalHours<=24)result.Add(new(before,target));
        if(after is not null&&(after.Start-target.End).TotalHours<=24)result.Add(new(target,after));return result;
    }
    public static string Label(TrafficPair pair,TrafficSnapshot? snapshot)
    {
        string gap=$"间隔 {pair.GapMinutes} 分钟";
        if(snapshot?.Minutes is not {} minutes)return gap+" · 交通未核验";
        int spare=pair.GapMinutes-minutes;return gap+$" · 交通约 {minutes} 分钟 · "+(spare<0?$"来不及，缺 {-spare} 分钟":$"余量 {spare} 分钟");
    }
    public static bool SameTimingAndPlace(Occurrence? old,DateTime start,DateTime end,Place? place,PlannerState state)=>old is not null&&old.Start==start&&old.End==end&&state.Places.FirstOrDefault(p=>p.Id==old.PlaceId)?.Name==place?.Name&&old.PlaceId==place?.Id;
}
