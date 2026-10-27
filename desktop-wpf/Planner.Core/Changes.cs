namespace Planner.Core;
public sealed class EditCommand
{
    public string Action { get; set; } = "UPSERT";
    public Activity Activity { get; set; } = new();
    public RecurrenceRule? Rule { get; set; }
    public DateOnly? OriginDate { get; set; }
    public string Scope { get; set; } = "ONCE";
    public DateOnly? SplitDate { get; set; }
    public bool ReminderEnabled { get; set; }
    public int ReminderLeadMin { get; set; } = 15;
    public Place? Place { get; set; }
}
public static class ChangeService
{
    public static PlannerState Apply(PlannerState state, IEnumerable<EditCommand> commands, long expectedRevision)
    {
        if(state.Revision!=expectedRevision) throw new InvalidOperationException("行程已发生变化，请重新预览后确认。");
        var batch=commands.ToList();
        foreach(var group in batch.GroupBy(c=>c.Activity.Id))
        {
            var original=state.Activities.SingleOrDefault(x=>x.Id==group.Key);
            if(original is not null&&group.Any(c=>c.Activity.Version!=original.Version))throw new InvalidOperationException("该活动已被修改，请重新打开。");
            if(group.Count()>1&&(original?.Kind!="WEEKLY"||group.Any(c=>c.Scope=="FUTURE"||c.OriginDate is null)||group.Select(c=>c.OriginDate).Distinct().Count()!=group.Count()))throw new ArgumentException("同一活动的批次操作范围重叠，请分别确认。");
        }
        var next=state.Copy();
        foreach(var command in batch)
        {
            var cmd=JsonKit.Clone(command);var a=cmd.Activity;
            var existing=next.Activities.SingleOrDefault(x=>x.Id==a.Id);
            if(existing?.Kind=="WEEKLY"&&a.Kind!="WEEKLY"&&cmd.Action!="DELETE")throw new ArgumentException("周期编辑保留重复类型；需要单次活动时请新建，或仅修改当前实例。");
            if(existing?.Kind=="WEEKLY"&&cmd.OriginDate is not null&&cmd.Scope!="FUTURE"&&a.TableId!=existing.TableId&&cmd.Action!="DELETE")throw new ArgumentException("单次覆盖不能更换所属表；请调整整套规则，或新建目标表中的单次活动。");
            if(cmd.ReminderLeadMin is <0 or >10080)throw new ArgumentException("提醒提前分钟应为0至10080。");
            if(cmd.Action is not ("UPSERT" or "DELETE")) throw new ArgumentException("未知编辑操作。");
            if(cmd.Action=="DELETE")
            {
                if(existing is null) throw new ArgumentException("要删除的活动已不存在。");
                if(existing.Kind=="WEEKLY" && cmd.OriginDate is { } origin && cmd.Scope!="FUTURE")
                {
                    var rule=next.Rules.Single(r=>r.ActivityId==existing.Id);
                    if(!rule.Matches(origin)) throw new ArgumentException("此日期不是原周期实例。");
                    next.Exceptions.RemoveAll(e=>e.ActivityId==existing.Id&&e.OriginDate==origin);
                    next.Exceptions.Add(new(){ActivityId=existing.Id,OriginDate=origin,Kind="CANCEL"});
                    existing.Version++;
                }
                else if(existing.Kind=="WEEKLY" && cmd.Scope=="FUTURE" && cmd.SplitDate is { } cutoff)
                {
                    var rule=next.Rules.Single(r=>r.ActivityId==existing.Id);
                    if(cutoff<=rule.ValidFrom) RemoveActivity(next,existing.Id);
                    else {rule.ValidUntil=cutoff.AddDays(-1);next.Exceptions.RemoveAll(e=>e.ActivityId==existing.Id&&e.OriginDate>=cutoff);existing.Version++;}
                }
                else RemoveActivity(next,existing.Id);
                Invalidate(next,a.Id);continue;
            }
            if(cmd.Place is not null)
            {
                var place=cmd.Place;
                var same=next.Places.FirstOrDefault(x=>x.Id==place.Id);
                if(same is null) next.Places.Add(place);else next.Places[next.Places.IndexOf(same)]=place;
                a.PlaceId=place.Id;
            }
            ValidateActivity(next,a,cmd.Rule,existing);
            if(existing?.Kind=="WEEKLY" && cmd.OriginDate is { } date && cmd.Scope!="FUTURE")
            {
                var rule=next.Rules.Single(r=>r.ActivityId==a.Id);
                if(!rule.Matches(date)) throw new ArgumentException("此日期不属于原周期规则。");
                if(a.StartAt is null || a.EndAt is null || a.EndAt<=a.StartAt) throw new ArgumentException("请填写本次活动的起止时间。");
                var previous=next.Exceptions.FirstOrDefault(e=>e.ActivityId==a.Id&&e.OriginDate==date);
                next.Exceptions.RemoveAll(e=>e.ActivityId==a.Id&&e.OriginDate==date);
                next.Exceptions.Add(new(){Id=previous?.Id??Guid.NewGuid().ToString("N"),ActivityId=a.Id,OriginDate=date,StartAt=a.StartAt,EndAt=a.EndAt,Title=a.Title,PlaceId=a.PlaceId,PlaceOverride=true,Version=(previous?.Version??0)+1});
                existing.Version++;
            }
            else if(existing?.Kind=="WEEKLY" && cmd.Scope=="FUTURE" && cmd.SplitDate is { } split)
                SplitRule(next,existing,a,cmd.Rule??throw new ArgumentException("缺少新周期规则。"),split);
            else
            {
                a.UpdatedAt=Clock.Now;a.Version=(existing?.Version??0)+1;
                if(existing is not null) next.Activities.Remove(existing);
                next.Activities.Add(a);next.Rules.RemoveAll(r=>r.ActivityId==a.Id);
                if(a.Kind=="WEEKLY") {var rule=cmd.Rule!;rule.ActivityId=a.Id;next.Rules.Add(rule);a.StartAt=null;a.EndAt=null;}
            }
            var savedId=next.Activities.Any(x=>x.Id==a.Id)?a.Id:existing?.Id;
            if(savedId is not null && next.Activities.Any(x=>x.Id==savedId))
            {
                next.Reminders.RemoveAll(r=>r.ActivityId==savedId);
                next.Reminders.Add(new(){ActivityId=savedId,Enabled=cmd.ReminderEnabled,LeadMin=cmd.ReminderLeadMin});
            }
            Invalidate(next,a.Id);
        }
        InvalidateChangedPlans(state,next);next.Revision=expectedRevision+1;return next;
    }
    public static void ValidateActivity(PlannerState state,Activity a,RecurrenceRule? rule,Activity? existing=null)
    {
        if(!state.Tables.Any(t=>t.Id==a.TableId)) throw new ArgumentException("请选择所属行程表。");
        if(string.IsNullOrWhiteSpace(a.Title)||a.Title.Trim().Length>100) throw new ArgumentException("事项名称应为1至100字。");
        a.Title=a.Title.Trim();
        if(a.Kind is not ("ONCE" or "WEEKLY")||a.ConstraintType is not ("FIXED" or "FLEXIBLE")) throw new ArgumentException("活动类型不合法。");
        if(a.DurationMin<=0 || a.DurationMin>10080) throw new ArgumentException("活动时长应大于0且不超过7天。");
        if(a.StartAt is { } start && a.EndAt is { } end && end<=start) throw new ArgumentException("结束时间必须晚于开始时间。");
        if(a.Kind=="ONCE" && (a.StartAt is null||a.EndAt is null)) throw new ArgumentException("一次性活动必须有起止时间。");
        if(a.ConstraintType=="FLEXIBLE" && a.Kind=="ONCE" && (a.WindowStart is null||a.WindowEnd is null||a.WindowEnd-a.WindowStart<TimeSpan.FromMinutes(a.DurationMin))) throw new ArgumentException("灵活活动需填写能够容纳活动的时间范围。");
        if(a.ConstraintType=="FLEXIBLE"&&a.Kind=="ONCE"&&(a.StartAt<a.WindowStart||a.EndAt>a.WindowEnd))throw new ArgumentException("活动时间应在允许的灵活范围内。");
        if(a.Kind=="WEEKLY" && (existing is null || rule is not null))
        {
            if(rule is null||rule.WeekMask is <1 or >127||!TimeOnly.TryParseExact(rule.StartTime,"HH:mm",out _)) throw new ArgumentException("请填写合法重复星期及共同开始时间。");
            if(rule.ValidUntil<rule.ValidFrom) throw new ArgumentException("周期结束日期不能早于开始日期。");
            if(a.ConstraintType=="FLEXIBLE" && (!TimeOnly.TryParse(rule.WindowFrom,out _)||rule.WindowSpanMin<a.DurationMin||rule.WindowSpanMin is null)) throw new ArgumentException("灵活周期需设置足够的时间窗。");
        }
    }
    static void SplitRule(PlannerState state,Activity old,Activity replacement,RecurrenceRule rule,DateOnly split)
    {
        var prior=state.Rules.Single(r=>r.ActivityId==old.Id);
        if(split<prior.ValidFrom) throw new ArgumentException("拆分日期不能早于原规则开始日。");
        var future=state.Exceptions.Where(e=>e.ActivityId==old.Id&&e.OriginDate>=split).ToList();
        var newId=Guid.NewGuid().ToString("N");replacement.Id=newId;replacement.Version=1;replacement.StartAt=null;replacement.EndAt=null;
        rule.ActivityId=newId;rule.ValidFrom=split;
        if(rule.ValidUntil<split) throw new ArgumentException("新规则结束日早于生效日。");
        state.Activities.Add(replacement);state.Rules.Add(rule);
        var reminder=state.Reminders.FirstOrDefault(x=>x.ActivityId==old.Id);
        void CopyReminder(string id){if(reminder is not null){var copy=JsonKit.Clone(reminder);copy.ActivityId=id;state.Reminders.Add(copy);}}
        CopyReminder(newId);
        var mapping=new Dictionary<DateOnly,(string Id,DateOnly? Origin)>();
        foreach(var ex in future)
        {
            if(rule.Matches(ex.OriginDate)){ex.ActivityId=newId;mapping[ex.OriginDate]=(newId,ex.OriginDate);}
            else if(ex.Kind=="OVERRIDE")
            {
                var standalone=JsonKit.Clone(replacement);standalone.Id=Guid.NewGuid().ToString("N");standalone.Kind="ONCE";standalone.StartAt=ex.StartAt;standalone.EndAt=ex.EndAt;standalone.Title=ex.Title??old.Title;standalone.PlaceId=ex.PlaceOverride?ex.PlaceId:old.PlaceId;standalone.ConstraintType="FIXED";standalone.DurationMin=(int)(ex.EndAt!-ex.StartAt!).Value.TotalMinutes;state.Activities.Add(standalone);
                CopyReminder(standalone.Id);mapping[ex.OriginDate]=(standalone.Id,null);
                state.Exceptions.Remove(ex);
            }
            else state.Exceptions.Remove(ex);
        }
        (string Id,DateOnly? Origin)? Target(DateOnly? date)=>date is {} d&&d>=split?(mapping.TryGetValue(d,out var mapped)?mapped:rule.Matches(d)?(newId,d):null):null;
        Invalidate(state,old.Id);
        var obsolete=new HashSet<string>();
        foreach(var leg in state.Legs)
        {
            if(leg.FromActivityId==old.Id&&leg.FromOriginDate>=split){if(Target(leg.FromOriginDate) is {} target){leg.FromActivityId=target.Id;leg.FromOriginDate=target.Origin;}else obsolete.Add(leg.Id);}
            if(leg.ToActivityId==old.Id&&leg.ToOriginDate>=split){if(Target(leg.ToOriginDate) is {} target){leg.ToActivityId=target.Id;leg.ToOriginDate=target.Origin;}else obsolete.Add(leg.Id);}
        }
        state.Segments.RemoveAll(s=>obsolete.Contains(s.LegId));state.Legs.RemoveAll(l=>obsolete.Contains(l.Id));
        foreach(var delivery in state.Deliveries.Where(x=>x.ActivityId==old.Id).ToList())
        {
            var suffix=delivery.InstanceKey[(delivery.InstanceKey.LastIndexOf(':')+1)..];
            if(!DateOnly.TryParseExact(suffix,"yyyy-MM-dd",out var date)||date<split)continue;
            if(Target(date) is {} target){delivery.ActivityId=target.Id;delivery.InstanceKey=target.Id+":"+(target.Origin?.ToString("yyyy-MM-dd")??"once");}
            else state.Deliveries.Remove(delivery);
        }
        if(split==prior.ValidFrom) RemoveActivity(state,old.Id);
        else {prior.ValidUntil=split.AddDays(-1);old.Version++;}
    }
    public static void RemoveActivity(PlannerState state,string id)
    {
        Invalidate(state,id);
        var legs=state.Legs.Where(l=>l.FromActivityId==id||l.ToActivityId==id).Select(l=>l.Id).ToHashSet();
        state.Segments.RemoveAll(s=>legs.Contains(s.LegId));state.Legs.RemoveAll(l=>legs.Contains(l.Id));
        state.Activities.RemoveAll(a=>a.Id==id);state.Rules.RemoveAll(r=>r.ActivityId==id);state.Exceptions.RemoveAll(e=>e.ActivityId==id);state.Reminders.RemoveAll(r=>r.ActivityId==id);state.Deliveries.RemoveAll(r=>r.ActivityId==id);
    }
    public static void Invalidate(PlannerState state,string activityId)
    {
        var ids=state.Legs.Where(l=>l.FromActivityId==activityId||l.ToActivityId==activityId).Select(l=>l.PlanId).ToHashSet();
        foreach(var id in ids)InvalidatePlan(state,id);
    }
    public static void InvalidatePlan(PlannerState state,string id)
    {
        foreach(var plan in state.Plans.Where(p=>p.Id==id))plan.Status="STALE";
        foreach(var leg in state.Legs.Where(l=>l.PlanId==id))leg.ValidationStatus="STALE";
    }
    public static void InvalidateChangedPlans(PlannerState before,PlannerState after)
    {
        string Fingerprint(PlannerState s,DateOnly date)
        {
            var day=date.ToDateTime(TimeOnly.MinValue);
            return System.Text.Json.JsonSerializer.Serialize(CalendarEngine.Expand(s,day,day.AddDays(1)).Select(o=>new{o.Key,o.Title,o.Start,o.End,o.PlaceId,o.Activity.TableId,Place=s.Places.FirstOrDefault(p=>p.Id==o.PlaceId)}),JsonKit.Options);
        }
        foreach(var plan in after.Plans.Where(p=>before.Plans.Any(old=>old.Id==p.Id)))
            if(Fingerprint(before,plan.PlanDate)!=Fingerprint(after,plan.PlanDate))InvalidatePlan(after,plan.Id);
    }
}
