namespace Planner.Core;
public static class PlanService
{
    public static PlannerState Confirm(PlannerState current,PlannerState resolved,PlanningRequest request,ItineraryCandidate candidate,long revision)
    {
        if(current.Revision!=revision||resolved.Revision!=revision)throw new InvalidOperationException("行程已变化，请重新生成路线。");
        if(candidate.Stops.Count==0)throw new ArgumentException("方案为空。");
        ValidateResources(resolved,request,candidate);
        var day=request.Date.ToDateTime(TimeOnly.MinValue);var originals=CalendarEngine.Expand(current,day,day.AddDays(1));
        if(originals.Count!=candidate.Stops.Count||candidate.Stops.Select(x=>x.Original.Key).Distinct().Count()!=originals.Count)throw new ArgumentException("方案事项不完整。");
        var commands=new List<EditCommand>();
        foreach(var stop in candidate.Stops)
        {
            var prior=originals.SingleOrDefault(x=>x.Key==stop.Original.Key)??throw new ArgumentException("方案实例无效。");
            if(prior.VersionKey!=stop.Original.VersionKey)throw new InvalidOperationException("事项版本已变化。");
            if(stop.Arrive>stop.Start||stop.End<=stop.Start)throw new ArgumentException("路线到达时间不满足活动要求。");
            if(stop.Start!=prior.Start||stop.End!=prior.End)
            {
                if(!request.ReplanFlexible||!request.EditableTables.Contains(prior.Activity.TableId)||prior.Activity.ConstraintType!="FLEXIBLE")throw new ArgumentException("方案不能移动固定事项或其他行程表。");
                var a=JsonKit.Clone(prior.Activity);a.StartAt=stop.Start;a.EndAt=stop.End;var reminder=current.Reminders.FirstOrDefault(x=>x.ActivityId==a.Id);
                commands.Add(new(){Activity=a,OriginDate=prior.OriginDate,ReminderEnabled=reminder?.Enabled??false,ReminderLeadMin=reminder?.LeadMin??15});
            }
        }
        var next=resolved.Copy();
        foreach(var place in next.Places)
        {
            var old=current.Places.FirstOrDefault(x=>x.Id==place.Id);
            if(old is not null&&(old.Lng!=place.Lng||old.Lat!=place.Lat))foreach(var a in next.Activities.Where(x=>x.PlaceId==place.Id))ChangeService.Invalidate(next,a.Id);
        }
        next=ChangeService.Apply(next,commands,revision);
        var oldPlans=next.Plans.Where(x=>x.PlanDate==request.Date).ToList();var oldIds=oldPlans.Select(x=>x.Id).ToHashSet();var legs=next.Legs.Where(x=>oldIds.Contains(x.PlanId)).Select(x=>x.Id).ToHashSet();
        next.Segments.RemoveAll(x=>legs.Contains(x.LegId));next.Legs.RemoveAll(x=>oldIds.Contains(x.PlanId));next.Scopes.RemoveAll(x=>oldIds.Contains(x.PlanId));next.Plans.RemoveAll(x=>oldIds.Contains(x.Id));
        var plan=new Plan{PlanDate=request.Date,StartPlaceId=request.Origin.Id,EarliestDepart=request.Earliest,Version=(oldPlans.FirstOrDefault()?.Version??0)+1,ResourceJson=System.Text.Json.JsonSerializer.Serialize(new{request.Modes,request.BikeResource,request.ParkingMinutes},JsonKit.Options)};
        if(!next.Places.Any(x=>x.Id==request.Origin.Id))next.Places.Add(JsonKit.Clone(request.Origin));next.Plans.Add(plan);
        foreach(var table in next.Tables.Where(x=>x.Active(request.Date)))next.Scopes.Add(new(){PlanId=plan.Id,TableId=table.Id,TableVersion=table.Version,Role=request.EditableTables.Contains(table.Id)&&request.ReplanFlexible?"EDITABLE":"CONSTRAINT"});
        PlannedStop? previous=null;int seq=0;
        foreach(var stop in candidate.Stops)
        {
            if(previous is not null&&stop.Depart<previous.End)throw new ArgumentException("路线与上一事项重叠。");
            var leg=new PlanLeg{PlanId=plan.Id,Seq=seq++,FromActivityId=previous?.Original.Activity.Id,FromOriginDate=previous?.Original.OriginDate,ToActivityId=stop.Original.Activity.Id,ToOriginDate=stop.Original.OriginDate,DepartAt=stop.Depart,ArriveAt=stop.Arrive,BufferSec=stop.BufferSec,QueryAt=stop.Route.QueryAt,RequestedDepartAt=stop.Depart,ValidationStatus="VALID",KnownCostFen=stop.Route.KnownCostFen,CostComplete=stop.Route.CostComplete,CostNote=stop.Route.CostNote};next.Legs.Add(leg);
            int part=0;foreach(var segment in stop.Route.Segments){var copy=JsonKit.Clone(segment);copy.Id=Guid.NewGuid().ToString("N");copy.LegId=leg.Id;copy.Seq=part++;next.Segments.Add(copy);}previous=stop;
        }
        return next;
    }
    public static void ValidateResources(PlannerState state,PlanningRequest request,ItineraryCandidate candidate)
    {
        Place at=request.Origin,car=request.Origin,bike=request.Origin;var time=request.Earliest;
        bool Same(Place a,Place b)=>a.Id==b.Id||a.Lng is {} x&&b.Lng is {} y&&a.Lat is {} u&&b.Lat is {} v&&Math.Abs(x-y)<.00001&&Math.Abs(u-v)<.00001;
        foreach(var stop in candidate.Stops)
        {
            var destination=state.Places.SingleOrDefault(x=>x.Id==stop.Original.PlaceId)??throw new ArgumentException("目的地已失效。");
            if(!TransportPolicy.Allows(stop.Route,request.Modes))throw new ArgumentException("方案包含未选择的交通方式。");
            if(stop.Route.Uses("CAR")&&!Same(at,car))throw new ArgumentException("自驾车辆仍在此前地点，不能异地取车。");
            if(stop.Route.Uses("BIKE")&&request.BikeResource=="OWN_BIKE"&&!Same(at,bike))throw new ArgumentException("自有自行车仍在此前地点，不能异地取车。");
            if(stop.Depart<time||stop.Arrive!=stop.Depart.AddSeconds(stop.Route.DurationSec+stop.BufferSec)||stop.Arrive>stop.Start)throw new ArgumentException("该路段与活动时间不能衔接，请重新规划。");
            if(stop.Route.Uses("CAR"))car=destination;if(stop.Route.Uses("BIKE")&&request.BikeResource=="OWN_BIKE")bike=destination;at=destination;time=stop.End;
        }
    }
}
