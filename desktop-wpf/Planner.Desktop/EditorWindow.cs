using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Planner.Core;
using Activity=Planner.Core.Activity;
namespace Planner.Desktop;
public sealed partial class EditorWindow:Window
{
    public DateOnly? SavedDate {get;private set;}
    readonly PlannerViewModel vm;readonly Occurrence? occurrence;readonly long openedRevision;readonly CancellationTokenSource cancel=new();
    readonly TextBox title,place,windowStart,windowEnd,note,lead;readonly TimeEntry start,end;
    readonly DatePicker date,endDate,until;readonly ComboBox table,kind,constraint,scope;
    readonly CheckBox reminder=new(){Content="启用提醒"},overnight=new(){Content="结束于次日"};readonly CheckBox[] weekdays;
    readonly StackPanel form=new();
    readonly TextBlock trafficHint=Ui.Text("保存时自动检查前后行程是否来得及；缺少信息时再询问。",11);
    readonly TextBlock durationLabel=Ui.Text("",12),status=Ui.Text("",11);bool closing,syncingTimes;DateTime previousStart;
    public EditorWindow(PlannerViewModel model,Occurrence? item=null,DateTime? initial=null)
    {
        vm=model;occurrence=item;openedRevision=vm.State.Revision;Title=item is null?"自定义添加":"编辑行程";Width=820;Height=760;MinWidth=440;MinHeight=360;WindowStartupLocation=WindowStartupLocation.CenterOwner;Background=Ui.Brush("#FBF7F0");FontFamily=new("Microsoft YaHei UI");
        var area=SystemParameters.WorkArea;Width=Math.Min(Width,area.Width);Height=Math.Min(Height,area.Height);MinWidth=Math.Min(MinWidth,area.Width);MinHeight=Math.Min(MinHeight,area.Height);
        var outer=new DockPanel{Margin=new(20)};Content=outer;
        var footer=new Grid{Margin=new(0,12,0,0)};footer.ColumnDefinitions.Add(new());footer.ColumnDefinitions.Add(new(){Width=GridLength.Auto});DockPanel.SetDock(footer,Dock.Bottom);outer.Children.Add(footer);var actions=Ui.Row(Ui.Button("取消",Close));Grid.SetColumn(actions,1);footer.Children.Add(actions);status.MinWidth=0;footer.Children.Add(status);
        var save=Ui.Button("保存",()=>{},true);actions.Children.Add(save);save.Click+=async(_,_)=>{if(closing)return;save.IsEnabled=false;form.IsEnabled=false;status.Text="正在检查前后行程…";try{await SaveAsync();}catch(OperationCanceledException){if(!closing)status.Text="已返回编辑，尚未保存。";}catch(Exception ex){status.Text=ex.Message;}finally{if(!closing){save.IsEnabled=true;form.IsEnabled=true;}}};
        var panel=form;outer.Children.Add(new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,HorizontalContentAlignment=HorizontalAlignment.Stretch});
        var actualStart=item?.Start??initial??vm.SelectedDate.ToDateTime(new(9,0));var actualEnd=item?.End??actualStart.AddHours(1);
        title=Ui.Input(item?.Title??"");title.Tag="ActivityTitle";Typography.Set(title,14);title.FontWeight=FontWeights.SemiBold;title.AcceptsReturn=false;title.TextWrapping=TextWrapping.NoWrap;title.MinLines=title.MaxLines=1;title.MinHeight=32;title.Padding=new(10,5,10,5);Ui.Field(panel,item is null?"添加一项安排":"调整这项安排",title);
        panel.Children.Add(Ui.Text(item is null?"填写活动、日期与时间，其他信息可以稍后完善。":new Planner.Data.BoardPreferenceStore(vm.DataDirectory).Load().Completed.Contains(item.Key)?"已完成":"待进行",12));
        place=Ui.Input(item is null?"":vm.State.PlaceName(item.PlaceId));if(item?.PlaceId is null)place.Text="";
        date=new StyledDateEntry{SelectedDate=actualStart.Date,Tag="StartDate"};endDate=new StyledDateEntry{SelectedDate=actualEnd.Date,Tag="EndDate"};until=new StyledDateEntry();start=new(actualStart.ToString("HH:mm")){Tag="StartTime",DateContext=actualStart.Date};end=new(actualEnd.ToString("HH:mm")){Tag="EndTime",DateContext=actualEnd.Date};previousStart=actualStart;
        AddPair(panel,"开始日期",date,"结束日期",endDate);AddPair(panel,"开始时间",start,"结束时间",end);Ui.Field(panel,"地点",place);
        start.Changed+=StartChanged;end.Changed+=EndChanged;start.DateTimeChanged+=_=>StartChanged();end.DateTimeChanged+=_=>EndChanged();
        date.SelectedDateChanged+=(_,_)=>{if(!syncingTimes&&date.SelectedDate is {} d){start.DateContext=d.Date;StartChanged();}};endDate.SelectedDateChanged+=(_,_)=>{if(!syncingTimes&&endDate.SelectedDate is {} d){end.DateContext=d.Date;EndChanged();}};
        var quick=new WrapPanel{Tag="QuickDurations",Margin=new(0,3,0,0)};foreach(int minutes in new[]{30,60,90,120}){int duration=minutes;var choice=Ui.Button(minutes+" 分钟",()=>{if(TimeOnly.TryParseExact(start.Text,"HH:mm",out var st)&&date.SelectedDate is {} d)SetEnd(d.Date.Add(st.ToTimeSpan()).AddMinutes(duration));});choice.Tag="QuickDuration:"+minutes;choice.Padding=new(10,4,10,4);choice.Margin=new(0,0,6,6);choice.MinHeight=30;Typography.Set(choice,12);quick.Children.Add(choice);}panel.Children.Add(quick);durationLabel.Tag="DurationSummary";durationLabel.Margin=new(0,0,0,8);panel.Children.Add(durationLabel);
        panel.Children.Add(trafficHint);
        note=Ui.Input(item?.Activity.Note??"");note.AcceptsReturn=true;note.TextWrapping=TextWrapping.Wrap;note.MinHeight=90;Ui.Field(panel,"备注",note);
        table=new();foreach(var t in vm.State.Tables)table.Items.Add(new ComboBoxItem{Content=t.Name,Tag=t.Id});table.SelectedIndex=Math.Max(0,vm.State.Tables.FindIndex(t=>t.Id==(item?.Activity.TableId??vm.SelectedTableId)));
        kind=Ui.Choices(["具体某一天","每周重复"],item?.Activity.Kind=="WEEKLY"?1:0);constraint=Ui.Choices(["固定时间","允许规划时调整"],item?.Activity.ConstraintType=="FLEXIBLE"?1:0);scope=Ui.Choices(item?.Activity.Kind=="WEEKLY"?["仅这一次","从所选日期起调整整套规则"]:["此项活动"]);
        var rule=vm.State.Rules.FirstOrDefault(r=>r.ActivityId==item?.Activity.Id);weekdays=Enumerable.Range(0,7).Select(i=>new CheckBox{Content=new[]{"一","二","三","四","五","六","日"}[i],IsChecked=rule is not null&&(rule.WeekMask&(1<<i))!=0}).ToArray();until.SelectedDate=rule?.ValidUntil?.ToDateTime(TimeOnly.MinValue);
        windowStart=Ui.Input(item?.Activity.WindowStart?.ToString("HH:mm")??rule?.WindowFrom??"08:00");windowEnd=Ui.Input(item?.Activity.WindowEnd?.ToString("HH:mm")??"22:00");
        var setting=vm.State.Reminders.FirstOrDefault(r=>r.ActivityId==item?.Activity.Id);reminder.IsChecked=setting?.Enabled??false;lead=Ui.Input((setting?.LeadMin??15).ToString());lead.Width=Math.Max(64,44*Typography.Scale);
        var more=new StackPanel();AddPair(more,"所属行程表",table,"重复方式",kind);more.Children.Add(Ui.Row(weekdays));Ui.Field(more,"重复结束日（可留空）",until);if(item?.Activity.Kind=="WEEKLY")Ui.Field(more,"修改影响范围",scope);Ui.Field(more,"排程约束",constraint);AddPair(more,"灵活范围开始",windowStart,"灵活范围结束",windowEnd);var reminderRow=new WrapPanel{Margin=new(0,8,0,8),Tag="ReminderRow"};
        reminder.VerticalAlignment=VerticalAlignment.Center;reminder.Margin=new(0,0,18,0);reminderRow.Children.Add(reminder);
        var leadGroup=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};var leadLabel=Ui.Text("提前",12);leadLabel.Margin=new(0,0,8,0);leadLabel.VerticalAlignment=VerticalAlignment.Center;leadGroup.Children.Add(leadLabel);
        lead.Tag="ReminderLead";lead.Width=Math.Max(64,44*Typography.Scale);lead.Margin=new(0);lead.Padding=new(6,4,6,4);lead.MinHeight=32;lead.TextAlignment=TextAlignment.Center;Typography.Set(lead,12);lead.VerticalAlignment=VerticalAlignment.Center;leadGroup.Children.Add(lead);var minutesLabel=Ui.Text("分钟",12);minutesLabel.Margin=new(8,0,0,0);minutesLabel.VerticalAlignment=VerticalAlignment.Center;leadGroup.Children.Add(minutesLabel);reminderRow.Children.Add(leadGroup);more.Children.Add(reminderRow);
        panel.Children.Add(new Expander{Header=Ui.Text("更多设置 · 行程表、周期与提醒",13),Content=more,IsExpanded=item?.Activity.Kind=="WEEKLY",Margin=new(0,14,0,10)});
        if(item?.Activity.Kind=="WEEKLY"){void UpdateScope(){bool enabled=scope.SelectedIndex==1;foreach(var c in new Control[]{table,kind,constraint,note,until,windowStart,windowEnd}.Concat(weekdays))c.IsEnabled=enabled;}scope.SelectionChanged+=(_,_)=>UpdateScope();UpdateScope();}
        void UpdateMetrics()=>lead.Width=Math.Max(64,44*Typography.Scale);vm.Changed+=UpdateMetrics;
        UpdateDuration();PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape){Close();e.Handled=true;}};Closed+=(_,_)=>{vm.Changed-=UpdateMetrics;closing=true;cancel.Cancel();};
    }
    public string DurationText=>durationLabel.Text;
    void StartChanged()
    {
        if(syncingTimes||!TimeOnly.TryParseExact(start.Text,"HH:mm",out var s))return;var next=start.DateContext.Date.Add(s.ToTimeSpan());
        syncingTimes=true;try{if(endDate.SelectedDate is {} d&&TimeOnly.TryParseExact(end.Text,"HH:mm",out var e)&&d.Date.Add(e.ToTimeSpan())>previousStart){var moved=DateTimeSelection.MoveStart(previousStart,d.Date.Add(e.ToTimeSpan()),next);endDate.SelectedDate=moved.End.Date;end.DateContext=moved.End.Date;end.Text=moved.End.ToString("HH:mm");}date.SelectedDate=next.Date;previousStart=next;}finally{syncingTimes=false;}UpdateDuration();
    }
    void EndChanged(){if(syncingTimes)return;if(TimeOnly.TryParseExact(end.Text,"HH:mm",out _)&&endDate.SelectedDate!=end.DateContext.Date){syncingTimes=true;try{endDate.SelectedDate=end.DateContext.Date;}finally{syncingTimes=false;}}UpdateDuration();}
    void SetEnd(DateTime value){syncingTimes=true;try{endDate.SelectedDate=value.Date;end.DateContext=value.Date;end.Text=value.ToString("HH:mm");}finally{syncingTimes=false;}UpdateDuration();}
    void UpdateDuration(){if(date.SelectedDate is {} from&&endDate.SelectedDate is {} untilDay&&TimeOnly.TryParseExact(start.Text,"HH:mm",out var s)&&TimeOnly.TryParseExact(end.Text,"HH:mm",out var e)){int minutes=(int)(untilDay.Date.Add(e.ToTimeSpan())-from.Date.Add(s.ToTimeSpan())).TotalMinutes;overnight.IsChecked=untilDay.Date>from.Date;durationLabel.Text=minutes>0?$"耗时 {minutes} 分钟"+(untilDay.Date>from.Date?$" · 跨 {(untilDay.Date-from.Date).Days} 天":""):"结束日期时间应晚于开始";}else durationLabel.Text="请填写日期和HH:mm格式时间";}
    static void AddPair(Panel parent,string left,Control first,string right,Control second)
    {
        var row=new Grid{Margin=new(0,0,0,4),HorizontalAlignment=HorizontalAlignment.Stretch};row.ColumnDefinitions.Add(new());row.ColumnDefinitions.Add(new());row.RowDefinitions.Add(new(){Height=GridLength.Auto});row.RowDefinitions.Add(new(){Height=GridLength.Auto});
        first.MinWidth=second.MinWidth=0;var a=new StackPanel();var b=new StackPanel();Ui.Field(a,left,first);Ui.Field(b,right,second);row.Children.Add(a);row.Children.Add(b);parent.Children.Add(row);bool? narrow=null;
        void Reflow(){bool next=row.ActualWidth<540*Typography.Scale;if(narrow==next)return;narrow=next;Grid.SetColumn(b,next?0:1);Grid.SetRow(b,next?1:0);Grid.SetColumnSpan(a,next?2:1);Grid.SetColumnSpan(b,next?2:1);a.Margin=next?new(0):new(0,0,8,0);b.Margin=next?new(0):new(8,0,0,0);}
        row.SizeChanged+=(_,_)=>Reflow();row.Loaded+=(_,_)=>Reflow();
    }
    async Task SaveAsync()
    {
        if(vm.State.Revision!=openedRevision)throw new InvalidOperationException("相关行程已发生变化，请关闭后重新打开编辑。 ");
        var day=DateOnly.FromDateTime(date.SelectedDate??throw new ArgumentException("请选择日期。"));
        if(!TimeOnly.TryParseExact(start.Text,"HH:mm",out var st)||!TimeOnly.TryParseExact(end.Text,"HH:mm",out var et))throw new ArgumentException("时间请使用HH:mm格式，例如09:30。");
        var begin=day.ToDateTime(st);var finish=(endDate.SelectedDate??throw new ArgumentException("请选择结束日期。")).Date.Add(et.ToTimeSpan());
        if(finish<=begin)throw new ArgumentException("结束日期时间应晚于开始；跨日请设置正确的结束日期。");
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
        var problems=EditConflictGuard.ForEdit(vm.State,proposed,day);
        if(problems.Count>0)throw new ArgumentException("所选时间或未来周期与已有行程冲突，请调整时间。\n"+string.Join("\n",problems.Take(4).Select(p=>p.Message)));
        var checks=new List<TrafficSnapshot>();
        if(!AdjacentTraffic.SameTimingAndPlace(occurrence,begin,finish,targetPlace,vm.State))
        {
            var candidates=CalendarEngine.ExpandForDisplay(proposed,begin.Date,finish.Date.AddDays(1));
            var target=candidates.FirstOrDefault(o=>o.Start==begin&&o.End==finish&&o.Activity.Id==a.Id)
                ??candidates.FirstOrDefault(o=>o.Start==begin&&o.End==finish&&o.Title==a.Title&&o.Activity.TableId==a.TableId);
            if(target is not null&&proposed.Tables.Any(t=>t.Id==target.Activity.TableId&&t.Active(day)))
            {
                (proposed,checks)=await CheckAdjacentAsync(proposed,target,command);
                problems=EditConflictGuard.ForEdit(vm.State,proposed,day);
                if(problems.Count>0)throw new ArgumentException("地点或时间调整后与已有安排冲突，请返回修改。");
            }
        }
        cancel.Token.ThrowIfCancellationRequested();if(vm.State.Revision!=openedRevision)throw new InvalidOperationException("行程已有变化，请重新打开编辑。");
        if(command.Scope=="FUTURE")
        {
            var exceptions=vm.State.Exceptions.Where(e=>e.ActivityId==a.Id&&e.OriginDate>=command.SplitDate).ToList();
            var impact=string.Join("\n",exceptions.Take(15).Select(e=>$"{e.OriginDate:yyyy-MM-dd} · "+((rule!.WeekMask&(1<<(((int)e.OriginDate.DayOfWeek+6)%7)))!=0&&(rule.ValidUntil is null||e.OriginDate<=rule.ValidUntil)?"迁移并保留":"OVERRIDE"==e.Kind?"转为单次活动":"取消日期不再产生实例")));
            if(!Ui.Confirm($"从 {command.SplitDate:yyyy-MM-dd} 起调整周期。\n此前记录不变；未来例外 {exceptions.Count} 项：\n{impact}\n"+(exceptions.Count>15?"其余同类例外按相同规则处理。\n":"")+"确认保存？"))return;
        }
        cancel.Token.ThrowIfCancellationRequested();if(vm.State.Revision!=openedRevision)throw new InvalidOperationException("行程已有变化，请重新打开编辑。");
        new Planner.Data.TrafficSnapshotStore(vm.DataDirectory).Save(checks);vm.Persist(proposed);SavedDate=day;Close();
    }
}
