namespace Planner.Core;

// Activities and route snapshots share the caller's one revision/transaction.
public static class GeneratedPlanService
{
 public static PlannerState Apply(PlannerState state,RepositoryPlan plan,string tableId)
 {
  var next=ChangeService.Apply(state,RepositoryPlanner.Commands(plan,tableId),plan.Revision);
  foreach(var row in plan.Rows.Where(r=>r.PreviousActivityId is not null&&r.Before.Minutes>0&&r.TravelDepartAt is not null))
  {
   var day=row.Trip.Request.Day!.Value;var daily=next.Plans.FirstOrDefault(p=>p.PlanDate==day);
   if(daily is null){daily=new(){PlanDate=day,Status="CONFIRMED",EarliestDepart=row.TravelDepartAt};next.Plans.Add(daily);}
   foreach(var id in new[]{tableId,next.Activities.First(a=>a.Id==row.PreviousActivityId).TableId}.Distinct())if(!next.Scopes.Any(s=>s.PlanId==daily.Id&&s.TableId==id))next.Scopes.Add(new(){PlanId=daily.Id,TableId=id,TableVersion=next.Tables.Single(t=>t.Id==id).Version});
   var option=row.Before.Option;var leg=new PlanLeg{PlanId=daily.Id,Seq=next.Legs.Where(l=>l.PlanId==daily.Id).Select(l=>l.Seq).DefaultIfEmpty(0).Max()+1,FromActivityId=row.PreviousActivityId,FromOriginDate=row.PreviousOriginDate,ToActivityId=row.ActivityId,DepartAt=row.TravelDepartAt!.Value,ArriveAt=row.TravelDepartAt.Value.AddMinutes(row.Before.Minutes),RequestedDepartAt=row.TravelDepartAt,QueryAt=option?.QueryAt??Clock.Now,ValidationStatus=option is null?"UNVERIFIED":"VALID",KnownCostFen=option?.KnownCostFen,CostComplete=option?.CostComplete??false,CostNote=option?.CostNote??"粗估交通，费用未核验"};next.Legs.Add(leg);
   var segments=option?.Segments.Select(JsonKit.Clone).ToList()??[];
   if(segments.Count==0)segments.Add(new(){Mode=row.Trip.Request.ModeIn switch{"公交"=>"BUS","骑行"=>"BIKE","驾车"=>"CAR",_=>"WALK"},Instruction=row.Before.Source,DurationSec=row.Before.Minutes*60,Provider=row.Before.Source});
   int seq=0;foreach(var segment in segments){segment.Id=Guid.NewGuid().ToString("N");segment.LegId=leg.Id;segment.Seq=++seq;next.Segments.Add(segment);}
  }
  return next;
 }
}
