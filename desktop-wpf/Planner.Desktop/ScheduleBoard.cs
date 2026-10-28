using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Planner.Core;
using Planner.Data;

namespace Planner.Desktop;
public sealed class ScheduleBoard:Grid
{
    const string BackgroundColor="#F5F3F0",Accent="#C65B40";
    readonly PlannerViewModel vm;readonly BoardPreferenceStore preferenceStore;BoardPreferences preferences;
    readonly Grid header=new(),body=new();readonly Button caption;readonly Dictionary<string,Button> modeButtons=[];
    ScrollViewer? scroll;Grid? monthGrid;readonly List<(DateOnly Date,FrameworkElement Heading)> listHeadings=[];
    readonly Dictionary<string,(string Key,UIElement Content,ScrollViewer? Scroll,List<(DateOnly Date,FrameworkElement Heading)> Headings)> cache=[];
    readonly Dictionary<string,(double X,double Y)> scrollPositions=[];string displayedView="";
    DateOnly month,loadedFrom,loadedUntil,lastSelection;DateOnly? listVisibleDate;string view;bool resetScroll=true,rendering;
    public event Action<DateTime>? AddRequested;public event Action<Occurrence>? EditRequested;public event Action<Occurrence>? Selected;public event Action<Occurrence>? DeleteRequested;
    public string ViewMode=>view;
    public DateOnly DisplayMonth=>month;
    public ScheduleBoard(PlannerViewModel model)
    {
        vm=model;Background=Ui.Brush(BackgroundColor);preferenceStore=new(vm.DataDirectory);preferences=preferenceStore.Load();view=preferences.View;
        month=new(vm.SelectedDate.Year,vm.SelectedDate.Month,1);loadedFrom=month;loadedUntil=month.AddMonths(1);lastSelection=vm.SelectedDate;
        Resources.MergedDictionaries.Add(new(){Source=new Uri("/NanjingPlanner;component/BoardStyles.xaml",UriKind.Relative)});
        RowDefinitions.Add(new(){Height=GridLength.Auto});RowDefinitions.Add(new(){Height=GridLength.Auto});RowDefinitions.Add(new());header.Margin=new(5,4,2,9);Children.Add(header);Grid.SetRow(body,2);Children.Add(body);
        header.ColumnDefinitions.Add(new());header.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
        var left=new WrapPanel{VerticalAlignment=VerticalAlignment.Center};header.Children.Add(left);var label=Ui.Text("行程看板",19,true);label.Foreground=Ui.Brush("#292725");label.Margin=new(0,5,12,4);left.Children.Add(label);
        caption=Button("",PickDate);caption.FontWeight=FontWeights.SemiBold;caption.ToolTip="点击选择日期";left.Children.Add(caption);
        var prev=Button("‹",()=>Shift(-1));prev.FontSize=22;prev.Padding=new(8,0,8,3);prev.ToolTip="上月";left.Children.Add(prev);
        var next=Button("›",()=>Shift(1));next.FontSize=22;next.Padding=new(8,0,8,3);next.ToolTip="下月";left.Children.Add(next);
        var today=Button("今天",()=>NavigateTo(Clock.Today));today.Background=Brushes.White;left.Children.Add(today);
        var modes=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Top};Grid.SetColumn(modes,1);header.Children.Add(modes);
        foreach(var (key,text) in new[]{("MONTH","月历"),("LIST","列表")}){var b=Button(text,()=>SetView(key));modeButtons[key]=b;modes.Children.Add(b);}
        var filters=new WrapPanel{Margin=new(8,0,0,5)};Grid.SetRow(filters,1);Children.Add(filters);
        var completed=new CheckBox{Content="显示已完成",IsChecked=preferences.ShowCompleted,Margin=new(0,0,16,0)};var incomplete=new CheckBox{Content="显示未完成",IsChecked=preferences.ShowIncomplete};
        completed.Click+=(_,_)=>Ui.Safe(()=>SaveFilter(completed.IsChecked==true,incomplete.IsChecked==true));incomplete.Click+=(_,_)=>Ui.Safe(()=>SaveFilter(completed.IsChecked==true,incomplete.IsChecked==true));filters.Children.Add(completed);filters.Children.Add(incomplete);
        vm.Changed+=OnChanged;Loaded+=(_,_)=>Render();Render();
    }
    Button Button(string text,Action action)
    {
        var b=new Button{Content=text,Style=(Style)FindResource("BoardButton")};b.Click+=(_,_)=>Ui.Safe(action);return b;
    }
    public void SetView(string mode)
    {
        if(mode is not("MONTH" or "LIST"))throw new ArgumentException("未知看板视图。");
        if(mode==view)return;
        var next=JsonKit.Clone(preferences);next.View=mode;preferenceStore.Save(next);preferences=next;view=mode;listVisibleDate=null;resetScroll=false;Render();
    }
    public void NavigateTo(DateOnly date)
    {
        month=new(date.Year,date.Month,1);loadedFrom=month;loadedUntil=month.AddMonths(1);listVisibleDate=null;resetScroll=true;vm.FocusDate(date);
    }
    void Shift(int direction){var basis=view=="LIST"&&listVisibleDate is {} date?new DateOnly(date.Year,date.Month,1):month;NavigateTo(basis.AddMonths(direction));}
    void SaveFilter(bool completed,bool incomplete){var next=JsonKit.Clone(preferences);next.ShowCompleted=completed;next.ShowIncomplete=incomplete;preferenceStore.Save(next);preferences=next;cache.Clear();Render();}

    void OnChanged()
    {
        if(lastSelection!=vm.SelectedDate)
        {
            var dates=BoardProjection.MonthDays(month);bool inRange=view=="LIST"?vm.SelectedDate>=loadedFrom&&vm.SelectedDate<loadedUntil:dates.Contains(vm.SelectedDate);
            if(!inRange){month=new(vm.SelectedDate.Year,vm.SelectedDate.Month,1);loadedFrom=month;loadedUntil=month.AddMonths(1);listVisibleDate=null;resetScroll=true;}
            lastSelection=vm.SelectedDate;
        }
        Render();
    }
    public void Render()
    {
        if(rendering)return;rendering=true;
        try
        {
            if(scroll is not null&&displayedView.Length>0)scrollPositions[displayedView]=(scroll.HorizontalOffset,scroll.VerticalOffset);
            var position=resetScroll?(0d,0d):scrollPositions.GetValueOrDefault(view);double offset=position.Item2;displayedView=view;
            string key=$"{vm.State.Revision}|{month}|{loadedFrom}|{loadedUntil}|{string.Join(',',VisibleTables())}|{preferences.ShowCompleted}|{preferences.ShowIncomplete}|{string.Join(',',preferences.Completed.Order())}";
            body.Children.Clear();
            if(cache.TryGetValue(view,out var cached)&&cached.Key==key){body.Children.Add(cached.Content);scroll=cached.Scroll;monthGrid=scroll?.Content as Grid;listHeadings.Clear();listHeadings.AddRange(cached.Headings);UpdateSelectedDay();caption.Content=view=="MONTH"?$"{month.Year} 年 {month.Month} 月":"全部行程 · 按日期";UpdateModes();resetScroll=false;var reused=scroll;Dispatcher.BeginInvoke(()=>{if(ReferenceEquals(scroll,reused)){FitRows();reused?.ScrollToHorizontalOffset(position.Item1);reused?.ScrollToVerticalOffset(position.Item2);UpdateListCaption();}},System.Windows.Threading.DispatcherPriority.Loaded);return;}
            monthGrid=null;scroll=null;listHeadings.Clear();
            UpdateModes();caption.Content=$"{month.Year} 年 {month.Month} 月";
            if(view=="MONTH")BuildMonth();else BuildList();
            cache[view]=(key,body.Children[0],scroll,listHeadings.ToList());
            resetScroll=false;if(scroll is {} current)Dispatcher.BeginInvoke(()=>{if(ReferenceEquals(scroll,current)){current.ScrollToVerticalOffset(offset);FitRows();UpdateListCaption();}});
        }
        finally{rendering=false;}
    }
    void UpdateSelectedDay(){if(monthGrid is null)return;foreach(var pick in monthGrid.Children.OfType<Border>().SelectMany(c=>((StackPanel)c.Child).Children.OfType<DockPanel>()).SelectMany(p=>p.Children.OfType<Button>()))if(pick.Tag is DateOnly day)pick.Background=day==vm.SelectedDate?Ui.Brush("#FAECE6"):Brushes.Transparent;}
    void UpdateModes(){foreach(var (mode,b) in modeButtons){b.Background=mode==view?Ui.Brush(Accent):Brushes.White;b.Foreground=mode==view?Brushes.White:Ui.Brush("#68605A");}}
    List<BoardDay> DisplayDays(DateOnly from,DateOnly until)=>BoardProjection.Range(vm.State,from,until,VisibleTables()).Select(d=>new BoardDay(d.Date,d.Entries.Where(e=>preferences.IsVisible(e.Occurrence.Key)).ToList())).ToList();
    IEnumerable<string> VisibleTables()=>vm.State.Tables.Where(t=>!vm.HiddenTables.Contains(t.Id)).Select(t=>t.Id);
    Dictionary<string,List<Conflict>> Conflicts(DateOnly from,DateOnly until)
    {
        var result=new Dictionary<string,List<Conflict>>();foreach(var c in CalendarEngine.AllConflicts(vm.State,from.ToDateTime(TimeOnly.MinValue),until.ToDateTime(TimeOnly.MinValue)))foreach(var key in new[]{c.FirstKey,c.SecondKey}.Where(k=>k is not null).Cast<string>()){if(!result.TryGetValue(key,out var list))result[key]=list=[];list.Add(c);}return result;
    }
    void BuildMonth()
    {
        var dates=BoardProjection.MonthDays(month);var days=DisplayDays(dates[0],dates[^1].AddDays(1));var conflicts=Conflicts(dates[0],dates[^1].AddDays(1));
        var layout=new Grid();layout.RowDefinitions.Add(new(){Height=new(31)});layout.RowDefinitions.Add(new());body.Children.Add(layout);
        var weekdayViewport=new Border{ClipToBounds=true,Margin=new(0,0,SystemParameters.VerticalScrollBarWidth,0)};layout.Children.Add(weekdayViewport);
        var weekdays=new Grid{HorizontalAlignment=HorizontalAlignment.Left};weekdayViewport.Child=weekdays;
        monthGrid=new Grid{Background=Ui.Brush("#EEEAE5"),MinWidth=840};
        var renderedMonth=monthGrid;monthGrid.SizeChanged+=(_,_)=>weekdays.Width=renderedMonth.ActualWidth;
        for(int i=0;i<7;i++){weekdays.ColumnDefinitions.Add(new());monthGrid.ColumnDefinitions.Add(new());var text=Ui.Text(new[]{"周一","周二","周三","周四","周五","周六","周日"}[i],11,true);text.Foreground=Ui.Brush("#77716A");text.HorizontalAlignment=HorizontalAlignment.Center;text.Margin=new(0,6,0,0);Grid.SetColumn(text,i);weekdays.Children.Add(text);}
        for(int i=0;i<dates.Count/7;i++)monthGrid.RowDefinitions.Add(new(){Height=GridLength.Auto,MinHeight=88});
        scroll=new(){Content=monthGrid,VerticalScrollBarVisibility=ScrollBarVisibility.Visible,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto};
        scroll.ScrollChanged+=(_,e)=>{weekdays.RenderTransform=new TranslateTransform(-e.HorizontalOffset,0);if(e.ViewportHeightChange!=0||e.ViewportWidthChange!=0)FitRows();};Grid.SetRow(scroll,1);layout.Children.Add(scroll);
        scroll.SizeChanged+=(_,_)=>FitRows();
        scroll.Loaded+=(_,_)=>FitRows();
        for(int index=0;index<days.Count;index++)
        {
            var day=days[index];bool current=day.Date.Month==month.Month;var panel=new StackPanel{Margin=new(5,3,5,5)};
            var cell=new Border{Background=Ui.Brush(current?"#F2EFEB":"#F8F6F3"),BorderBrush=Ui.Brush("#E6E0DA"),BorderThickness=new(.5),Child=panel,AllowDrop=true,Tag=day.Date};Grid.SetRow(cell,index/7);Grid.SetColumn(cell,index%7);monthGrid.Children.Add(cell);
            var daybar=new DockPanel{Margin=new(0,0,0,4)};
            var pick=Button(day.Date.Day+(day.Date==Clock.Today?" · 今天":""),()=>SelectDay(day.Date));pick.Tag=day.Date;pick.Padding=new(3,3,0,3);pick.HorizontalContentAlignment=HorizontalAlignment.Left;pick.FontSize=11;pick.FontWeight=FontWeights.SemiBold;pick.Foreground=Ui.Brush(day.Date==Clock.Today?Accent:current?"#66605A":"#C1B8AF");if(day.Date==vm.SelectedDate)pick.Background=Ui.Brush("#FAECE6");daybar.Children.Add(pick);panel.Children.Add(daybar);
            foreach(var entry in day.Entries)panel.Children.Add(ActivityCard(entry,false,conflicts));
            cell.MouseLeftButtonUp+=(_,e)=>{DependencyObject? target=e.OriginalSource as DependencyObject;while(target is not null&&target!=cell){if(target is ButtonBase or TextBox or CheckBox||target is Border{Tag:BoardEntry})return;target=VisualTreeHelper.GetParent(target);}Add(day.Date);e.Handled=true;};
            cell.DragOver+=(_,e)=>{e.Effects=e.Data.GetDataPresent(typeof(Occurrence))?DragDropEffects.Move:DragDropEffects.None;e.Handled=true;};
            cell.Drop+=(_,e)=>{if(e.Data.GetData(typeof(Occurrence)) is Occurrence occurrence)Ui.Safe(()=>Move(occurrence,day.Date));e.Handled=true;};
            var menu=new ContextMenu();var item=new MenuItem{Header="添加当天行程"};item.Click+=(_,_)=>Add(day.Date);menu.Items.Add(item);cell.ContextMenu=menu;
        }
    }
    void FitRows()
    {
        if(monthGrid is null||scroll is null||monthGrid.RowDefinitions.Count==0)return;
        double width=Math.Max(840,scroll.ActualWidth-SystemParameters.VerticalScrollBarWidth);monthGrid.Width=width;foreach(var col in monthGrid.ColumnDefinitions)col.Width=new GridLength(width/7);
        double height=BoardLayout.RowMinimum(scroll.ViewportHeight>0?scroll.ViewportHeight:scroll.ActualHeight,monthGrid.RowDefinitions.Count);foreach(var row in monthGrid.RowDefinitions)if(Math.Abs(row.MinHeight-height)>.1)row.MinHeight=height;
    }
    void BuildList()
    {
        var panel=new StackPanel{Margin=new(8,4,8,12)};var days=DisplayDays(loadedFrom,loadedUntil);var conflicts=Conflicts(loadedFrom,loadedUntil);
        scroll=new(){Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Visible,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};scroll.ScrollChanged+=(_,_)=>UpdateListCaption();body.Children.Add(scroll);
        var range=Ui.Text($"{loadedFrom:yyyy年MM月} — {loadedUntil.AddDays(-1):yyyy年MM月} · 按日期查看",11);range.Foreground=Ui.Brush("#9B948E");panel.Children.Add(range);
        if(days.All(d=>d.Entries.Count==0)){panel.Children.Add(Ui.Text("这段时间还没有符合显示设置的行程",18,true));panel.Children.Add(Button("＋ 添加行程",()=>Add(vm.SelectedDate)));}
        foreach(var day in days.Where(d=>d.Entries.Count>0))
        {
            var heading=new DockPanel{Margin=new(0,16,0,10)};var count=Ui.Text(day.Entries.Count+" 项",12);count.Foreground=Ui.Brush("#A39A90");DockPanel.SetDock(count,Dock.Right);heading.Children.Add(count);heading.Children.Add(Ui.Text($"{day.Date.Day}  周{new[]{"一","二","三","四","五","六","日"}[((int)day.Date.DayOfWeek+6)%7]}    {day.Date:yyyy年M月}",17,true));panel.Children.Add(heading);listHeadings.Add((day.Date,heading));
            BoardEntry? previous=null;
            foreach(var entry in day.Entries)
            {
                if(previous is not null){int gap=(int)(entry.Start-previous.End).TotalMinutes;if(gap is >0 and <60){var gapText=Ui.Text($"◷  间隔 {gap} 分钟（含交通）",11,true);gapText.Foreground=Ui.Brush(Accent);panel.Children.Add(new Border{Background=Ui.Brush("#FAECE6"),Padding=new(9,3,9,0),Margin=new(104,0,0,7),HorizontalAlignment=HorizontalAlignment.Left,Child=gapText});}}
                var row=new Grid{Margin=new(0,0,0,9)};row.ColumnDefinitions.Add(new(){Width=new(94)});row.ColumnDefinitions.Add(new());var times=new StackPanel{Margin=new(6,10,6,8)};times.Children.Add(Ui.Text(entry.Start.ToString("HH:mm"),15,true));times.Children.Add(Ui.Text(((int)(entry.Occurrence.End-entry.Occurrence.Start).TotalMinutes)+" 分钟",11));times.Children.Add(Ui.Text(entry.TimeText.Split('—')[1],15,true));row.Children.Add(new Border{Background=Ui.Brush("#EBEEF0"),Child=times,Margin=new(0,0,10,0)});var card=ActivityCard(entry,true,conflicts);card.Margin=new(0);Grid.SetColumn(card,1);row.Children.Add(card);panel.Children.Add(row);previous=entry;
            }
        }
        var footer=new WrapPanel{Margin=new(0,16,0,4)};footer.Children.Add(Button("加载上月",()=>LoadMore(-1)));footer.Children.Add(Button("加载下月",()=>LoadMore(1)));panel.Children.Add(footer);panel.Children.Add(Ui.Text("每次展示最多连续6个月；可使用顶部月份切换继续查看。点击日期空白可添加活动。",11));
    }
    void LoadMore(int direction)
    {
        int span=(loadedUntil.Year-loadedFrom.Year)*12+loadedUntil.Month-loadedFrom.Month;
        if(span>=6){MessageBox.Show("已展示连续6个月，请用顶部月份切换查看其他时段。","行程列表",MessageBoxButton.OK,MessageBoxImage.Information);return;}
        if(direction<0){loadedFrom=loadedFrom.AddMonths(-1);resetScroll=true;}else loadedUntil=loadedUntil.AddMonths(1);Render();
    }
    void UpdateListCaption()
    {
        if(view!="LIST"||scroll?.Content is not StackPanel panel||listHeadings.Count==0)return;
        DateOnly date=listHeadings[0].Date;foreach(var item in listHeadings){if(item.Heading.TranslatePoint(new(0,0),panel).Y<=scroll.VerticalOffset+8)date=item.Date;else break;}
        listVisibleDate=date;caption.Content=$"{date:yyyy年M月d日}";
    }
    Border ActivityCard(BoardEntry entry,bool list,Dictionary<string,List<Conflict>> conflicts)
    {
        var o=entry.Occurrence;var table=vm.State.Tables.Single(t=>t.Id==o.Activity.TableId);bool done=preferences.Completed.Contains(o.Key),active=table.Active(DateOnly.FromDateTime(o.Start));conflicts.TryGetValue(o.Key,out var warnings);
        var panel=new StackPanel();var top=new DockPanel();var check=new CheckBox{IsChecked=done,Tag=o.Key,ToolTip="标记完成（仅改变看板显示）",Margin=new(0,2,6,0),VerticalAlignment=VerticalAlignment.Top};System.Windows.Automation.AutomationProperties.SetName(check,o.Title+"完成标记");check.Click+=(_,e)=>{Ui.Safe(()=>ToggleCompleted(o.Key));e.Handled=true;};DockPanel.SetDock(check,Dock.Right);top.Children.Add(check);
        var time=Ui.Text(entry.TimeText+(entry.ContinuesFromYesterday?" · 续":""),list?12:11,true);time.Foreground=Ui.Brush(Accent);time.Margin=new(0,1,0,5);top.Children.Add(time);panel.Children.Add(top);
        var title=Ui.Text(o.Title,list?15:12,true);title.Margin=new(0,0,0,5);title.Foreground=Ui.Brush(done?"#AAA39C":"#302C29");if(done)title.TextDecorations=TextDecorations.Strikethrough;panel.Children.Add(title);
        panel.Children.Add(Muted("地点："+vm.State.PlaceName(o.PlaceId),list?12:11));panel.Children.Add(Muted("耗时："+((int)(o.End-o.Start).TotalMinutes)+" 分钟",11));
        if(!string.IsNullOrWhiteSpace(o.Activity.Note))panel.Children.Add(Muted("备注："+o.Activity.Note,list?12:11));
        panel.Children.Add(Muted(table.Name+(o.OriginDate is null?"":" · 每周")+(!active?" · 未生效":""),10));
        if(warnings is {Count:>0}){var warning=Ui.Text(warnings.Any(w=>w.Kind=="TIME_OVERLAP")?"! 时间冲突":"◷ 交通待核验",10,true);warning.Foreground=Ui.Brush(Accent);warning.Margin=new(0,3,0,0);panel.Children.Add(warning);}
        if(list){var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};var edit=Button("编辑",()=>{SelectOccurrence(o);EditRequested?.Invoke(o);});edit.Foreground=Ui.Brush(Accent);var delete=Button("删除",()=>DeleteRequested?.Invoke(o));delete.Foreground=Ui.Brush("#A99A90");actions.Children.Add(edit);actions.Children.Add(delete);panel.Children.Add(actions);}
        var card=new Border{Tag=entry,Background=Brushes.White,BorderBrush=Ui.Brush(warnings is {Count:>0}?"#D89B86":"#E8DFD9"),BorderThickness=new(1),Padding=new(list?13:7,list?11:7,list?13:7,list?11:7),Margin=new(0,0,0,6),Child=panel,Cursor=Cursors.Hand,ToolTip=$"{o.Title}\n{o.Start:yyyy-MM-dd HH:mm} — {o.End:MM-dd HH:mm}\n{table.Name}\n点击编辑；右键更多；月历可拖到其他日期"};
        Point origin=new();bool dragged=false,pressedOnCard=false;
        card.MouseLeftButtonDown+=(_,e)=>{origin=e.GetPosition(card);dragged=false;pressedOnCard=true;};
        card.MouseMove+=(_,e)=>{if(view=="MONTH"&&pressedOnCard&&!dragged&&e.LeftButton==MouseButtonState.Pressed){var p=e.GetPosition(card);if(Math.Abs(p.X-origin.X)>SystemParameters.MinimumHorizontalDragDistance||Math.Abs(p.Y-origin.Y)>SystemParameters.MinimumVerticalDragDistance){dragged=true;DragDrop.DoDragDrop(card,new DataObject(typeof(Occurrence),o),DragDropEffects.Move);pressedOnCard=false;}}};
        card.MouseLeftButtonUp+=(_,e)=>{if(!pressedOnCard)return;pressedOnCard=false;if(!dragged){SelectOccurrence(o);EditRequested?.Invoke(o);}e.Handled=true;};
        var menu=new ContextMenu();foreach(var (label,action) in new (string,Action)[]{("查看详情",()=>SelectOccurrence(o)),("编辑活动",()=>{SelectOccurrence(o);EditRequested?.Invoke(o);}), (done?"撤销完成标记":"标记完成",()=>ToggleCompleted(o.Key)),("删除这一次",()=>DeleteRequested?.Invoke(o))}){var item=new MenuItem{Header=label};item.Click+=(_,_)=>Ui.Safe(action);menu.Items.Add(item);}card.ContextMenu=menu;return card;
    }
    static TextBlock Muted(string text,double size){var label=Ui.Text(text,size);label.Foreground=Ui.Brush("#77716A");label.Margin=new(0,0,0,4);return label;}
    void ToggleCompleted(string key)
    {
        var next=JsonKit.Clone(preferences);if(!next.Completed.Add(key))next.Completed.Remove(key);
        try{preferenceStore.Save(next);preferences=next;}finally{cache.Clear();Render();}
    }
    void SelectDay(DateOnly date)=>vm.FocusDate(date);
    void SelectOccurrence(Occurrence o){vm.FocusDate(DateOnly.FromDateTime(o.Start));Selected?.Invoke(o);}
    void Add(DateOnly date){SelectDay(date);AddRequested?.Invoke(date.ToDateTime(new(9,0)));}
    void Move(Occurrence occurrence,DateOnly day)
    {
        if(day==DateOnly.FromDateTime(occurrence.Start))return;var command=BoardProjection.MoveToDay(vm.State,occurrence,day);var next=ChangeService.Apply(vm.State,[command],vm.State.Revision);
        var warnings=CalendarEngine.AllConflicts(next,command.Activity.StartAt!.Value.Date.AddDays(-1),command.Activity.EndAt!.Value.Date.AddDays(2));
        if(warnings.Any(w=>w.Kind=="TIME_OVERLAP"))throw new ArgumentException("目标时间与已有行程冲突，请打开编辑器调整时间。");
        if(!Ui.Confirm($"将“{occurrence.Title}”改到 {day:yyyy-MM-dd}，保留原时间和时长。\n"+(occurrence.OriginDate is null?"":"只修改这一次，不改变其他每周安排。\n")+string.Join("\n",warnings.Take(4).Select(w=>w.Message))))return;
        vm.Persist(next);vm.FocusDate(day);
    }
    void PickDate()
    {
        var picker=new System.Windows.Controls.Calendar{SelectedDate=vm.SelectedDate.ToDateTime(TimeOnly.MinValue),DisplayDate=month.ToDateTime(TimeOnly.MinValue),DisplayMode=CalendarMode.Year};var popup=new Popup{Child=new Border{Background=Brushes.White,BorderBrush=Ui.Brush("#E6E0DA"),BorderThickness=new(1),Child=picker},PlacementTarget=caption,Placement=PlacementMode.Bottom,StaysOpen=false};
        picker.SelectedDatesChanged+=(_,_)=>{if(picker.SelectedDate is {} date){popup.IsOpen=false;NavigateTo(DateOnly.FromDateTime(date));}};popup.IsOpen=true;
    }
}
