using System.Windows;
using System.Windows.Controls;
using Planner.Core;
using Activity=Planner.Core.Activity;

namespace Planner.Desktop;
public sealed class EditorWindow : Window
{
    public DateOnly? SavedDate {get;private set;}
    readonly PlannerViewModel vm;readonly Occurrence? occurrence;
    readonly TextBox title,place,start,end,windowStart,windowEnd,note,lead;
    readonly DatePicker date,until;readonly ComboBox table,kind,constraint,scope;
    readonly CheckBox reminder=new(){Content="启用提醒"},overnight=new(){Content="结束于次日"};
    readonly CheckBox[] weekdays;
    public EditorWindow(PlannerViewModel model,Occurrence? item=null,DateTime? initial=null)
    {
        vm=model;occurrence=item;Title=item is null?"添加行程":"编辑行程";Width=650;Height=850;MinWidth=560;MinHeight=600;WindowStartupLocation=WindowStartupLocation.CenterOwner;Background=Ui.Brush("#FBF7F0");FontFamily=new("Microsoft YaHei UI");
        var outer=new DockPanel{Margin=new(22)};Content=outer;
        var bottom=Ui.Row(Ui.Button("取消",Close),Ui.Button("保存行程",Save,true));bottom.HorizontalAlignment=HorizontalAlignment.Right;DockPanel.SetDock(bottom,Dock.Bottom);outer.Children.Add(bottom);
        var panel=new StackPanel();outer.Children.Add(new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
        panel.Children.Add(Ui.Text(item is null?"为一天留好安排":"调整这项安排",22,true));
        var actualStart=item?.Start??initial??vm.SelectedDate.ToDateTime(new(9,0));var actualEnd=item?.End??actualStart.AddHours(1);
        title=Ui.Input(item?.Title??"");place=Ui.Input(vm.State.PlaceName(item?.PlaceId));if(item?.PlaceId is null)place.Text="";
        table=new();foreach(var t in vm.State.Tables)table.Items.Add(new ComboBoxItem{Content=t.Name,Tag=t.Id});table.SelectedIndex=Math.Max(0,vm.State.Tables.FindIndex(t=>t.Id==(item?.Activity.TableId??vm.SelectedTableId)));
        date=new(){SelectedDate=actualStart.Date};start=Ui.Input(actualStart.ToString("HH:mm"));end=Ui.Input(actualEnd.ToString("HH:mm"));overnight.IsChecked=actualEnd.Date>actualStart.Date;
        kind=Ui.Choices(["具体某一天","每周重复"],item?.Activity.Kind=="WEEKLY"?1:0);
        constraint=Ui.Choices(["固定时间","允许规划时调整"],item?.Activity.ConstraintType=="FLEXIBLE"?1:0);
        scope=Ui.Choices(item?.Activity.Kind=="WEEKLY"?["仅这一次","从所选日期起调整整套规则"]:["此项活动"]);
        var rule=vm.State.Rules.FirstOrDefault(r=>r.ActivityId==item?.Activity.Id);
        weekdays=Enumerable.Range(0,7).Select(i=>new CheckBox{Content=new[]{"一","二","三","四","五","六","日"}[i],IsChecked=rule is not null&&(rule.WeekMask&(1<<i))!=0}).ToArray();
        until=new(){SelectedDate=rule?.ValidUntil?.ToDateTime(TimeOnly.MinValue)};
        windowStart=Ui.Input(item?.Activity.WindowStart?.ToString("HH:mm")??rule?.WindowFrom??"08:00");windowEnd=Ui.Input(item?.Activity.WindowEnd?.ToString("HH:mm")??"22:00");
        note=Ui.Input(item?.Activity.Note??"");note.AcceptsReturn=true;note.Height=70;
        var setting=vm.State.Reminders.FirstOrDefault(r=>r.ActivityId==item?.Activity.Id);reminder.IsChecked=setting?.Enabled??false;lead=Ui.Input((setting?.LeadMin??15).ToString());lead.Width=80;
        Ui.Field(panel,"事项",title);Ui.Field(panel,"所属行程表",table);Ui.Field(panel,"地点（可先填写名称，路线规划时再确认）",place);
        Ui.Field(panel,"日期 / 周期生效起日",date);
        Ui.Field(panel,"开始时间",start);Ui.Field(panel,"结束时间",end);panel.Children.Add(overnight);
        Ui.Field(panel,"重复方式",kind);panel.Children.Add(Ui.Row(weekdays));Ui.Field(panel,"重复结束日（留空表示持续重复）",until);
        if(item?.Activity.Kind=="WEEKLY")Ui.Field(panel,"本次修改影响范围",scope);
        Ui.Field(panel,"排程约束",constraint);Ui.Field(panel,"灵活安排范围开始（仅灵活活动使用）",windowStart);Ui.Field(panel,"灵活安排范围结束",windowEnd);
        Ui.Field(panel,"备注（默认不发送给LLM）",note);panel.Children.Add(Ui.Row(reminder,Ui.Text("提前分钟："),lead));
        if(item?.Activity.Kind=="WEEKLY")panel.Children.Add(Ui.Text("默认仅修改当前实例；整套调整会保留此前的单日例外，并显示影响清单。",12));
        if(item?.Activity.Kind=="WEEKLY")
        {
            panel.Children.Add(Ui.Text("单次覆盖仅调整标题、地点和本次起止时间。所属表、重复规则、约束及备注请选整套调整；提醒设置对整项周期生效。",12));
            void UpdateScope(){bool enabled=scope.SelectedIndex==1;foreach(var control in new Control[]{table,kind,constraint,note,until,windowStart,windowEnd}.Concat(weekdays))control.IsEnabled=enabled;}
            scope.SelectionChanged+=(_,_)=>UpdateScope();UpdateScope();
        }
    }
    void Save()
    {
        var day=DateOnly.FromDateTime(date.SelectedDate??throw new ArgumentException("请选择日期。"));
        if(!TimeOnly.TryParseExact(start.Text,"HH:mm",out var st)||!TimeOnly.TryParseExact(end.Text,"HH:mm",out var et))throw new ArgumentException("时间请使用HH:mm格式，例如09:30。");
        var begin=day.ToDateTime(st);var finish=day.ToDateTime(et).AddDays(overnight.IsChecked==true?1:0);
        if(finish<=begin)throw new ArgumentException("结束时间应晚于开始；跨日请勾选次日。");
        var a=occurrence is null?new Activity():JsonKit.Clone(occurrence.Activity);
        a.Title=title.Text.Trim();a.TableId=(string)((ComboBoxItem)table.SelectedItem).Tag;a.StartAt=begin;a.EndAt=finish;a.DurationMin=(int)(finish-begin).TotalMinutes;a.Note=note.Text;
        a.Kind=kind.SelectedIndex==1?"WEEKLY":"ONCE";a.ConstraintType=constraint.SelectedIndex==1?"FLEXIBLE":"FIXED";
        EditWindows.Apply(a,day,windowStart.Text,windowEnd.Text,occurrence?.Activity.Kind=="WEEKLY"&&scope.SelectedIndex==0);
        Place? targetPlace=null;
        if(!string.IsNullOrWhiteSpace(place.Text))
        {
            targetPlace=vm.State.Places.FirstOrDefault(p=>p.Id==occurrence?.PlaceId&&p.Name==place.Text.Trim());
            targetPlace??=new(){Name=place.Text.Trim(),InputName=place.Text.Trim()};a.PlaceId=targetPlace.Id;
        }
        else a.PlaceId=null;
        RecurrenceRule? rule=null;
        if(a.Kind=="WEEKLY")rule=new(){ActivityId=a.Id,ValidFrom=day,ValidUntil=until.SelectedDate is {} u?DateOnly.FromDateTime(u):null,StartTime=start.Text,WeekMask=weekdays.Select((w,i)=>w.IsChecked==true?1<<i:0).Sum(),WindowFrom=a.ConstraintType=="FLEXIBLE"?windowStart.Text:null,WindowSpanMin=a.ConstraintType=="FLEXIBLE"?(int)(a.WindowEnd!-a.WindowStart!).Value.TotalMinutes:null};
        if(!int.TryParse(lead.Text,out var minutes)||minutes<0||minutes>10080)throw new ArgumentException("提醒提前分钟应为0至10080。");
        if(occurrence?.Activity.Kind=="WEEKLY" && scope.SelectedIndex==0 && a.Kind!="WEEKLY")throw new ArgumentException("单次修改保留原周期，请通过日期和时间调整本次安排。");
        var command=new EditCommand{Activity=a,Rule=occurrence?.Activity.Kind=="WEEKLY"&&scope.SelectedIndex==0?null:rule,Place=targetPlace,OriginDate=occurrence?.OriginDate,Scope=scope.SelectedIndex==1?"FUTURE":"ONCE",SplitDate=occurrence?.OriginDate??day,ReminderEnabled=reminder.IsChecked==true,ReminderLeadMin=minutes};
        var proposed=ChangeService.Apply(vm.State,[command],vm.State.Revision);
        var problems=CalendarEngine.AllConflicts(proposed,begin.Date.AddDays(-1),finish.Date.AddDays(2));
        if(problems.Count>0&&!Ui.Confirm("检查发现：\n"+string.Join("\n",problems.Take(5).Select(x=>x.Message))+"\n\n仍然保存？冲突和待核验标识会保留。"))return;
        if(command.Scope=="FUTURE")
        {
            var exceptions=vm.State.Exceptions.Where(e=>e.ActivityId==a.Id&&e.OriginDate>=command.SplitDate).ToList();
            var impact=string.Join("\n",exceptions.Take(15).Select(e=>$"{e.OriginDate:yyyy-MM-dd} · "+((rule!.WeekMask&(1<<(((int)e.OriginDate.DayOfWeek+6)%7)))!=0&&(rule.ValidUntil is null||e.OriginDate<=rule.ValidUntil)?"迁移并保留":"OVERRIDE"==e.Kind?"转为单次活动":"取消日期不再产生实例")));
            if(!Ui.Confirm($"从 {command.SplitDate:yyyy-MM-dd} 起调整周期。\n此前记录不变；未来例外 {exceptions.Count} 项：\n{impact}\n"+(exceptions.Count>15?"其余同类例外按相同规则处理。\n":"")+"确认保存？"))return;
        }
        vm.Persist(proposed);SavedDate=day;DialogResult=true;
    }
}
