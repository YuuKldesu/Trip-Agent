namespace Planner.Core;
public sealed record ScopeItem(string ActivityId,DateOnly? OriginDate,string TableId,string Title,string Place,DateTime StartAt,DateTime EndAt,string ConstraintType);
public sealed record ScopeTable(string Id,string Name,bool Enabled,DateOnly? ValidFrom,DateOnly? ValidUntil);
public sealed class ScopeContext
{
    public List<string> TableIds {get;set;}=[];
    public List<ScopeTable> Tables {get;set;}=[];
    public DateTime From {get;set;}
    public DateTime Until {get;set;}
    public bool AllowFutureRules {get;set;}
    public List<ScopeItem> Items {get;set;}=[];
    public static ScopeContext Build(PlannerState state,IEnumerable<string> tables,DateTime from,DateTime until,bool future=false)
    {
        var ids=tables.Distinct().ToList();if(ids.Count==0||ids.Any(id=>!state.Tables.Any(t=>t.Id==id)))throw new ArgumentException("请选择允许智能助手读取的行程表。");
        return new(){TableIds=ids,Tables=state.Tables.Where(t=>ids.Contains(t.Id)).Select(t=>new ScopeTable(t.Id,t.Name,t.Enabled,t.ValidFrom,t.ValidUntil)).ToList(),From=from,Until=until,AllowFutureRules=future,Items=CalendarEngine.ExpandForDisplay(state,from,until,ids).Select(o=>new ScopeItem(o.Activity.Id,o.OriginDate,o.Activity.TableId,o.Title,state.PlaceName(o.PlaceId),o.Start,o.End,o.Activity.ConstraintType)).ToList()};
    }
}
public sealed class AiOperation
{
    public string Action {get;set;}="ADD";
    public string? TargetActivityId {get;set;}
    public DateOnly? OriginDate {get;set;}
    public string? TableId {get;set;}
    public string? Title {get;set;}
    public string? Place {get;set;}
    public DateTime? StartAt {get;set;}
    public DateTime? EndAt {get;set;}
    public string ConstraintType {get;set;}="FIXED";
    public DateTime? WindowStart {get;set;}
    public DateTime? WindowEnd {get;set;}
    public int WeekMask {get;set;}
    public DateOnly? UntilDate {get;set;}
    public string Scope {get;set;}="ONCE";
}
public sealed class AiReply { public string Message {get;set;}="";public List<AiOperation> Operations {get;set;}=[]; }
public sealed record ChangeDraft(long Revision,ScopeContext Context,AiReply Reply);
public static class DraftService
{
    public static string Describe(PlannerState state,ChangeDraft draft,int index)
    {
        var command=Commands(state,draft,[index]).Single();var a=command.Activity;
        var prior=draft.Context.Items.FirstOrDefault(x=>x.ActivityId==a.Id&&(command.OriginDate is null||x.OriginDate==command.OriginDate));
        var text=new System.Text.StringBuilder();text.AppendLine($"{(command.Action=="DELETE"?"删除":prior is null?"新增":"修改")} · {a.Title}");
        text.AppendLine("行程表："+state.Tables.Single(t=>t.Id==a.TableId).Name);
        if(prior is not null)text.AppendLine($"原：{prior.StartAt:yyyy-MM-dd HH:mm}—{prior.EndAt:MM-dd HH:mm} · {prior.Place}");
        if(command.Action=="DELETE"){text.AppendLine(command.OriginDate is null?"实际影响：仅此项活动":"实际影响：仅取消原发生日期 "+command.OriginDate.Value.ToString("yyyy-MM-dd")+" 这一次，保留后续周期。");return text.ToString();}
        text.AppendLine($"拟：{a.StartAt:yyyy-MM-dd HH:mm}—{a.EndAt:MM-dd HH:mm} · {command.Place?.Name??state.PlaceName(a.PlaceId)}");
        if(command.Rule is {} rule&&(prior is null||command.Scope=="FUTURE"||prior.OriginDate is null))
        {
            var names=new[]{"周一","周二","周三","周四","周五","周六","周日"};var start=command.Scope=="FUTURE"?command.SplitDate??rule.ValidFrom:rule.ValidFrom;
            text.AppendLine($"实际影响：{(prior is null?"新增未来每周规则":"调整未来每周规则")}，从 {start:yyyy-MM-dd} 起");
            text.AppendLine(string.Join("、",names.Where((_,i)=>(rule.WeekMask&(1<<i))!=0))+$" · {rule.StartTime}开始 · {a.DurationMin}分钟");
            text.AppendLine(rule.ValidUntil is {} end?$"至 {end:yyyy-MM-dd}（含）":"不设结束日，持续每周重复");
            if(prior is not null&&command.Scope=="FUTURE")
            {
                var exceptions=state.Exceptions.Where(e=>e.ActivityId==a.Id&&e.OriginDate>=start).ToList();text.AppendLine($"未来单日例外 {exceptions.Count}项：");
                foreach(var ex in exceptions.Take(15)){bool matches=(rule.WeekMask&(1<<(((int)ex.OriginDate.DayOfWeek+6)%7)))!=0&&(rule.ValidUntil is null||ex.OriginDate<=rule.ValidUntil);text.AppendLine($"{ex.OriginDate:yyyy-MM-dd} · "+(matches?"迁移并保留":ex.Kind=="OVERRIDE"?"转为独立单次活动，保留提醒":"取消日期不再产生实例"));}
                if(exceptions.Count>15)text.AppendLine("其余例外按相同规则处理。");
            }
        }
        else text.AppendLine(command.OriginDate is {} date?$"实际影响：仅原发生日期 {date:yyyy-MM-dd} 这一次；其余周期不变。":"实际影响：仅此项具体日期活动。");
        return text.ToString().TrimEnd();
    }
    public static List<EditCommand> Commands(PlannerState state,ChangeDraft draft,IEnumerable<int> selected)
    {
        if(state.Revision!=draft.Revision)throw new InvalidOperationException("草稿生成后行程已改变，请重新生成并预览。");
        var commands=new List<EditCommand>();var touched=new HashSet<string>();var context=draft.Context;
        if(draft.Reply.Operations.Count>50)throw new ArgumentException("单次操作过多，请缩小范围。");
        foreach(var index in selected.Distinct())
        {
            if(index<0||index>=draft.Reply.Operations.Count)throw new ArgumentException("草稿选项已失效。");
            var op=draft.Reply.Operations[index];if(op.Action is not("ADD" or "UPDATE" or "DELETE"))throw new ArgumentException("智能助手返回了不支持的操作。");
            Activity a;ScopeItem? target=null;
            if(op.Action=="ADD")a=new Activity{TableId=op.TableId??context.TableIds.First()};
            else
            {
                var matches=context.Items.Where(x=>x.ActivityId==op.TargetActivityId&&(op.OriginDate is null||x.OriginDate==op.OriginDate)).ToList();
                if(matches.Count!=1)throw new ArgumentException("修改或删除目标不明确，或不在授权范围；请明确日期后重试。");
                target=matches.Single();if(!touched.Add(target.ActivityId+":"+target.OriginDate))throw new ArgumentException("同一实例包含重复操作，请重新生成草稿。");
                a=JsonKit.Clone(state.Activities.Single(x=>x.Id==target.ActivityId));a.StartAt=target.StartAt;a.EndAt=target.EndAt;a.Title=target.Title;
                if(target.OriginDate is {} origin&&state.Exceptions.FirstOrDefault(e=>e.ActivityId==a.Id&&e.OriginDate==origin) is {PlaceOverride:true} ex)a.PlaceId=ex.PlaceId;
            }
            if(!context.TableIds.Contains(a.TableId)||(op.TableId is not null&&!context.TableIds.Contains(op.TableId)))throw new ArgumentException("目标行程表未授权。");
            if(op.Action=="DELETE")
            {
                if(target!.StartAt<context.From||target.EndAt>context.Until)throw new ArgumentException("删除跨出了选定日期，请扩大范围后再操作。");
                commands.Add(new(){Action="DELETE",Activity=a,OriginDate=target.OriginDate});continue;
            }
            a.Title=op.Title??a.Title;a.TableId=op.TableId??a.TableId;a.StartAt=op.StartAt??a.StartAt;a.EndAt=op.EndAt??a.EndAt;
            if(a.StartAt is null||a.EndAt is null||a.StartAt<context.From||a.EndAt>context.Until)throw new ArgumentException("草稿起止时间不完整或超出授权日期，请补充或扩大范围。");
            a.DurationMin=(int)(a.EndAt-a.StartAt).Value.TotalMinutes;a.ConstraintType=op.ConstraintType;
            a.WindowStart=op.WindowStart;a.WindowEnd=op.WindowEnd;
            if(a.ConstraintType=="FLEXIBLE"&&(a.StartAt<a.WindowStart||a.EndAt>a.WindowEnd))throw new ArgumentException("活动时间应在允许的灵活范围内。");
            Place? place=null;
            if(op.Place is not null){place=state.Places.FirstOrDefault(p=>p.Name==op.Place.Trim())??new(){Name=op.Place.Trim(),InputName=op.Place.Trim()};a.PlaceId=place.Id;}
            RecurrenceRule? rule=null;
            if(op.WeekMask>0)
            {
                if(!context.AllowFutureRules && (op.UntilDate is null||op.UntilDate>=DateOnly.FromDateTime(context.Until)))throw new ArgumentException("周期超出所选日期；请明确允许未来周期规则，或提供范围内的结束日。");
                a.Kind="WEEKLY";rule=new(){ActivityId=a.Id,ValidFrom=DateOnly.FromDateTime(a.StartAt.Value),ValidUntil=op.UntilDate,WeekMask=op.WeekMask,StartTime=a.StartAt.Value.ToString("HH:mm"),WindowFrom=op.WindowStart?.ToString("HH:mm"),WindowSpanMin=op.WindowStart is not null&&op.WindowEnd is not null?(int)(op.WindowEnd-op.WindowStart).Value.TotalMinutes:null};
            }
            var future=target is not null&&op.Scope=="FUTURE";
            if(future&&!context.AllowFutureRules)throw new ArgumentException("修改未来周期尚未明确授权。");
            var setting=state.Reminders.FirstOrDefault(r=>r.ActivityId==a.Id);
            commands.Add(new(){Activity=a,Rule=rule,OriginDate=target?.OriginDate,Scope=future?"FUTURE":"ONCE",SplitDate=target?.OriginDate,Place=place,ReminderEnabled=setting?.Enabled??false,ReminderLeadMin=setting?.LeadMin??15});
        }
        // Reuse the same business checks before showing a confirmable preview.
        _=ChangeService.Apply(state,commands,state.Revision);
        return commands;
    }
}
