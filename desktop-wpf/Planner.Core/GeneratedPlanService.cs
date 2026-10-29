using System.Text.Json;
namespace Planner.Core;

public static class TransferEndpoints
{
 public static string? PlaceId(PlannerState state,string? activityId,DateOnly? origin)
 {
  var activity=state.Activities.FirstOrDefault(a=>a.Id==activityId);if(activity is null)return null;
  var exception=origin is {} date?state.Exceptions.FirstOrDefault(e=>e.ActivityId==activityId&&e.OriginDate==date&&e.Kind=="OVERRIDE"):null;
  return exception?.PlaceOverride==true?exception.PlaceId:activity.PlaceId;
 }
 public static string? Snapshot(PlannerState state,PlanLeg leg,string field)
 {
  var json=state.Segments.Where(s=>s.LegId==leg.Id).OrderBy(s=>s.Seq).FirstOrDefault()?.CostDetailJson;if(string.IsNullOrWhiteSpace(json))return null;
  try{using var doc=JsonDocument.Parse(json);return doc.RootElement.ValueKind==JsonValueKind.Object&&doc.RootElement.TryGetProperty(field,out var value)&&value.ValueKind==JsonValueKind.String?value.GetString():null;}catch(JsonException){return null;}
 }
}

// Adds actual final neighbouring route snapshots within the activity transaction.
public static class GeneratedPlanService
{
 sealed record Edge(string From,DateOnly? FromOrigin,string? FromPlace,string To,DateOnly? ToOrigin,string? ToPlace,DateTime Depart,TravelEstimate Estimate,string? Mode);
 public static PlannerState Apply(PlannerState state,RepositoryPlan plan,string tableId)
 {
  var next=ChangeService.Apply(state,RepositoryPlanner.Commands(plan,tableId),plan.Revision);var edges=new Dictionary<string,Edge>();
  void Add(Edge edge){if(edge.Estimate.Minutes<=0)return;string key=$"{edge.From}:{edge.FromOrigin:yyyy-MM-dd}>{edge.To}:{edge.ToOrigin:yyyy-MM-dd}";edges.TryAdd(key,edge);}
  foreach(var row in plan.Rows)if(row.PreviousActivityId is {} from&&row.TravelDepartAt is {} depart)Add(new(from,row.PreviousOriginDate,row.PreviousPlaceId,row.ActivityId,null,row.Trip.Place.Id,depart,row.Before,row.Trip.Request.ModeIn));
  foreach(var row in plan.Rows)if(row.NextActivityId is {} to){var depart=row.Trip.Request.Day!.Value.ToDateTime(TimeOnly.MinValue).AddMinutes(row.Start+row.Trip.Request.Duration!.Value);Add(new(row.ActivityId,null,row.Trip.Place.Id,to,row.NextOriginDate,row.NextPlaceId,depart,row.After,null));}
  foreach(var edge in edges.Values)
  {
   var day=DateOnly.FromDateTime(edge.Depart);var daily=next.Plans.FirstOrDefault(p=>p.PlanDate==day);
   if(daily is null){daily=new(){PlanDate=day,Status="CONFIRMED",EarliestDepart=edge.Depart};next.Plans.Add(daily);}
   var fromActivity=next.Activities.Single(a=>a.Id==edge.From);var toActivity=next.Activities.Single(a=>a.Id==edge.To);
   foreach(var id in new[]{tableId,fromActivity.TableId,toActivity.TableId}.Distinct())if(!next.Scopes.Any(s=>s.PlanId==daily.Id&&s.TableId==id))next.Scopes.Add(new(){PlanId=daily.Id,TableId=id,TableVersion=next.Tables.Single(t=>t.Id==id).Version});
   var option=edge.Estimate.Option;var leg=new PlanLeg{PlanId=daily.Id,Seq=next.Legs.Where(l=>l.PlanId==daily.Id).Select(l=>l.Seq).DefaultIfEmpty(0).Max()+1,FromActivityId=edge.From,FromOriginDate=edge.FromOrigin,ToActivityId=edge.To,ToOriginDate=edge.ToOrigin,DepartAt=edge.Depart,ArriveAt=edge.Depart.AddMinutes(edge.Estimate.Minutes),RequestedDepartAt=edge.Depart,QueryAt=option?.QueryAt??Clock.Now,ValidationStatus=option is null?"UNVERIFIED":"VALID",KnownCostFen=option?.KnownCostFen,CostComplete=option?.CostComplete??false,CostNote=option?.CostNote??"粗估交通，费用未核验"};next.Legs.Add(leg);
   var segments=option?.Segments.Select(JsonKit.Clone).ToList()??[];
   if(segments.Count==0){string mode=edge.Mode??edge.Estimate.Source.Split(" · ")[0];segments.Add(new(){Mode=mode switch{"公交"=>"BUS","骑行"=>"BIKE","驾车"=>"CAR",_=>"WALK"},Instruction=edge.Estimate.Source,DurationSec=edge.Estimate.Minutes*60,Provider=edge.Estimate.Source});}
   int seq=0;foreach(var segment in segments){segment.Id=Guid.NewGuid().ToString("N");segment.LegId=leg.Id;segment.Seq=++seq;if(seq==1)segment.CostDetailJson=JsonSerializer.Serialize(new{provider_cost_detail=segment.CostDetailJson,from_name=next.PlaceName(edge.FromPlace),to_name=next.PlaceName(edge.ToPlace),ownership_table_id=tableId});next.Segments.Add(segment);}
  }
  return next;
 }
}
