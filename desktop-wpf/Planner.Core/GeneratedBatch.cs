using System.Text.Json;
namespace Planner.Core;

public sealed class GeneratedBatchReceipt:Entity
{
    public string BranchId {get;set;}="";
    public string Status {get;set;}="APPLIED";
    public DateTime CreatedAt {get;set;}=Clock.Now;
    public long ExpectedRevision {get;set;}
    public long SourceVersion {get;set;}
    public List<Activity> Activities {get;set;}=[];
    public List<ScheduleTable> Tables {get;set;}=[];
    public List<Place> Places {get;set;}=[];
    public List<ReminderSetting> Reminders {get;set;}=[];public List<Plan> Plans {get;set;}=[];public List<PlanLeg> Legs {get;set;}=[];public List<RouteSegment> Segments {get;set;}=[];public List<PlanScope> Scopes {get;set;}=[];
}
public static class GeneratedBatchService
{
    static bool Same<T>(T a,T b)=>JsonSerializer.Serialize(a,JsonKit.Options)==JsonSerializer.Serialize(b,JsonKit.Options);
    public static GeneratedBatchReceipt Capture(PlannerState before,PlannerState after,string branchId)
    {
        if(before.Activities.Any(a=>!after.Activities.Any(b=>b.Id==a.Id&&Same(a,b))))throw new ArgumentException("生成批次只能新增活动，不能覆盖已有行程。");
        var added=after.Activities.Where(a=>before.Activities.All(b=>b.Id!=a.Id)).ToList();if(added.Count is <1 or >15)throw new ArgumentException("生成批次需要包含1—15项新增活动。");
        return new(){BranchId=branchId,ExpectedRevision=before.Revision,Activities=JsonKit.Clone(added),Tables=JsonKit.Clone(after.Tables.Where(t=>before.Tables.All(b=>b.Id!=t.Id)).ToList()),Places=JsonKit.Clone(after.Places.Where(p=>before.Places.All(b=>b.Id!=p.Id)).ToList()),Plans=JsonKit.Clone(after.Plans.Where(x=>before.Plans.All(b=>b.Id!=x.Id)).ToList()),Legs=JsonKit.Clone(after.Legs.Where(x=>before.Legs.All(b=>b.Id!=x.Id)).ToList()),Segments=JsonKit.Clone(after.Segments.Where(x=>before.Segments.All(b=>b.Id!=x.Id)).ToList()),Scopes=JsonKit.Clone(after.Scopes.Where(x=>before.Scopes.All(b=>b.PlanId!=x.PlanId||b.TableId!=x.TableId)).ToList()),Reminders=JsonKit.Clone(after.Reminders.Where(r=>added.Any(a=>a.Id==r.ActivityId)).ToList())};
    }
    public static PlannerState Undo(PlannerState state,GeneratedBatchReceipt receipt)
    {
        if(receipt.Status is "UNDONE" or "NOT_APPLIED" or "CONFLICT"||receipt.Activities.Count==0)throw new InvalidOperationException("此批次当前不能撤回，请先重新核对记录。");var ids=receipt.Activities.Select(a=>a.Id).ToHashSet();
        foreach(var original in receipt.Activities){var current=state.Activities.FirstOrDefault(a=>a.Id==original.Id);if(current is not null&&!Same(current,original))throw new InvalidOperationException("生成的行程已被手动修改，本次未撤回，请先核对修改。");}
        var ownedLegs=receipt.Legs.Select(x=>x.Id).ToHashSet();var ownedPlans=receipt.Plans.Select(x=>x.Id).ToHashSet();foreach(var original in receipt.Legs){var current=state.Legs.FirstOrDefault(x=>x.Id==original.Id);if(current is null)continue;var normalized=JsonKit.Clone(current);if(normalized.ValidationStatus=="STALE")normalized.ValidationStatus=original.ValidationStatus;if(!Same(normalized,original))throw new InvalidOperationException("生成的交通安排已被修改，本次未撤回。");}foreach(var original in receipt.Plans){var current=state.Plans.FirstOrDefault(x=>x.Id==original.Id);if(current is null)continue;var normalized=JsonKit.Clone(current);if(normalized.Status=="STALE")normalized.Status=original.Status;if(!Same(normalized,original))throw new InvalidOperationException("生成的路线方案已被修改，本次未撤回。");}if(state.Segments.Any(s=>ownedLegs.Contains(s.LegId)&&!receipt.Segments.Any(old=>Same(old,s)))||receipt.Segments.Any(old=>state.Segments.Any(s=>s.Id==old.Id&&!Same(s,old)))||state.Scopes.Any(s=>ownedPlans.Contains(s.PlanId)&&!receipt.Scopes.Any(old=>Same(old,s))))throw new InvalidOperationException("生成的交通明细已被修改或关联，本次未撤回。");if(state.Legs.Any(l=>!ownedLegs.Contains(l.Id)&&(ownedPlans.Contains(l.PlanId)||ids.Contains(l.ToActivityId)||(l.FromActivityId is {} from&&ids.Contains(from))))||state.Rules.Any(r=>ids.Contains(r.ActivityId))||state.Exceptions.Any(e=>ids.Contains(e.ActivityId)))throw new InvalidOperationException("生成行程已被路线或周期安排使用，本次未撤回，请先处理关联安排。");
        foreach(var now in state.Reminders.Where(r=>ids.Contains(r.ActivityId)))if(!receipt.Reminders.Any(old=>Same(now,old)))throw new InvalidOperationException("生成行程的提醒已被修改，本次未撤回。");
        if(receipt.Reminders.Any(old=>state.Activities.Any(a=>a.Id==old.ActivityId)&&!state.Reminders.Any(now=>Same(now,old))))throw new InvalidOperationException("生成行程的提醒已被修改，本次未撤回。");
        var next=state.Copy();next.Segments.RemoveAll(s=>ownedLegs.Contains(s.LegId));next.Legs.RemoveAll(l=>ownedLegs.Contains(l.Id));next.Scopes.RemoveAll(s=>ownedPlans.Contains(s.PlanId)||receipt.Scopes.Any(old=>Same(old,s))&&!next.Legs.Any(l=>l.PlanId==s.PlanId&&next.Activities.Any(a=>a.TableId==s.TableId&&(a.Id==l.ToActivityId||a.Id==l.FromActivityId))));next.Plans.RemoveAll(p=>ownedPlans.Contains(p.Id));foreach(var id in ids)ChangeService.RemoveActivity(next,id);
        foreach(var t in receipt.Tables){var now=next.Tables.FirstOrDefault(x=>x.Id==t.Id);if(now is not null&&Same(now,t)&&!next.Activities.Any(a=>a.TableId==t.Id)&&!next.Scopes.Any(s=>s.TableId==t.Id))next.Tables.Remove(now);}
        foreach(var p in receipt.Places)if(!next.Activities.Any(a=>a.PlaceId==p.Id)&&!next.Exceptions.Any(e=>e.PlaceId==p.Id)&&!next.Plans.Any(plan=>plan.StartPlaceId==p.Id))next.Places.RemoveAll(x=>x.Id==p.Id&&Same(x,p));
        next.Revision++;return next;
    }
    public static void Reconcile(PlannerState state,GeneratedBatchReceipt receipt)
    {
        if(receipt.Status is not("PENDING" or "UNDO_PENDING"))return;bool undo=receipt.Status=="UNDO_PENDING";int present=receipt.Activities.Count(a=>state.Activities.Any(x=>x.Id==a.Id));receipt.Status=present==receipt.Activities.Count?"APPLIED":present==0?undo?"UNDONE":"NOT_APPLIED":"CONFLICT";
    }
}
