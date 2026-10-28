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
    readonly PlannerViewModel vm;BoardPreferences preferences;
    readonly Grid header=new(),body=new();readonly Button caption;readonly Dictionary<string,Button> modeButtons=[];
    ScrollViewer? scroll;Grid? monthGrid;readonly List<(DateOnly Date,FrameworkElement Heading)> listHeadings=[];
    readonly Dictionary<string,(string Key,UIElement Content,ScrollViewer? Scroll,List<(DateOnly Date,FrameworkElement Heading)> Headings)> cache=[];
    readonly Dictionary<string,(double X,double Y)> scrollPositions=[];string displayedView="";
    readonly StackPanel modes;readonly WrapPanel filters;readonly CheckBox completedFilter,incompleteFilter;readonly TextBlock heading;
    bool compact;DateOnly agendaDate=Clock.Today;long renderedRevision=-1;string renderedVisibility="";
    DateOnly month,loadedFrom,loadedUntil,lastSelection;DateOnly? listVisibleDate;string view;bool resetScroll=true,rendering;
    public event Action<DateTime>? AddRequested;public event Action<Occurrence>? EditRequested;public event Action<Occurrence>? Selected;public event Action<Occurrence>? DeleteRequested;
    public string ViewMode=>compact?"AGENDA":view;
    public bool IsCompact=>compact;
    public DateOnly AgendaDate=>agendaDate;
    public DateOnly DisplayMonth=>month;
    public ScheduleBoard(PlannerViewModel model)
    {
        vm=model;Background=Brushes.Transparent;preferences=vm.Appearance;view=preferences.View;
        month=new(vm.SelectedDate.Year,vm.SelectedDate.Month,1);loadedFrom=month;loadedUntil=month.AddMonths(1);lastSelection=vm.SelectedDate;
        Resources.MergedDictionaries.Add(new(){Source=new Uri("/NanjingPlanner;component/BoardStyles.xaml",UriKind.Relative)});
        var surface=new Grid();surface.RowDefinitions.Add(new(){Height=GridLength.Auto});surface.RowDefinitions.Add(new(){Height=GridLength.Auto});surface.RowDefinitions.Add(new());Children.Add(new Border{CornerRadius=new(22),Background=Ui.Brush("#FBF7F0"),BorderBrush=Ui.Brush("#E5DDD3"),BorderThickness=new(1),Padding=new(9),Child=surface});header.Margin=new(4,2,2,7);surface.Children.Add(header);Grid.SetRow(body,2);surface.Children.Add(body);
        header.ColumnDefinitions.Add(new());header.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
        var left=new WrapPanel{VerticalAlignment=VerticalAlignment.Center};header.Children.Add(left);heading=Ui.Text("行程看板",19,true);heading.Foreground=Ui.Brush("#443B35");heading.Margin=new(0,5,12,4);left.Children.Add(heading);
        caption=Button("",PickDate);caption.FontWeight=FontWeights.SemiBold;caption.ToolTip="点击选择日期";left.Children.Add(caption);
        var prev=Button("‹",()=>Shift(-1));Typography.Set(prev,22);prev.Padding=new(8,0,8,3);prev.ToolTip="上月";left.Children.Add(prev);
        var next=Button("›",()=>Shift(1));Typography.Set(next,22);next.Padding=new(8,0,8,3);next.ToolTip="下月";left.Children.Add(next);
        var today=Button("今天",()=>NavigateTo(Clock.Today));today.Background=Brushes.White;left.Children.Add(today);
        modes=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Top};Grid.SetColumn(modes,1);header.Children.Add(modes);
        foreach(var (key,text) in new[]{("MONTH","月历"),("LIST","列表")}){var b=Button(text,()=>SetView(key));modeButtons[key]=b;modes.Children.Add(b);}
        filters=new WrapPanel{Margin=new(8,0,0,5)};Grid.SetRow(filters,1);surface.Children.Add(filters);
        completedFilter=new CheckBox{Content="显示已完成",IsChecked=preferences.ShowCompleted,Margin=new(0,0,16,0)};incompleteFilter=new CheckBox{Content="显示未完成",IsChecked=preferences.ShowIncomplete};
        completedFilter.Click+=(_,_)=>Ui.Safe(()=>SaveFilter(completedFilter.IsChecked==true,incompleteFilter.IsChecked==true));incompleteFilter.Click+=(_,_)=>Ui.Safe(()=>SaveFilter(completedFilter.IsChecked==true,incompleteFilter.IsChecked==true));filters.Children.Add(completedFilter);filters.Children.Add(incompleteFilter);
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
        listVisibleDate=null;resetScroll=false;vm.UpdateAppearance(p=>p.View=mode);
    }
    public void NavigateTo(DateOnly date)
    {
        if(compact)agendaDate=date;month=new(date.Year,date.Month,1);loadedFrom=month;loadedUntil=month.AddMonths(1);listVisibleDate=null;resetScroll=true;vm.FocusDate(date);
    }
    void Shift(int direction){if(compact){agendaDate=agendaDate.AddDays(direction);resetScroll=true;Render();return;}var basis=view=="LIST"&&listVisibleDate is {} date?new DateOnly(date.Year,date.Month,1):month;NavigateTo(basis.AddMonths(direction));}
    public void SetCompact(bool value){if(compact==value)return;compact=value;resetScroll=false;Render();}
    void SaveFilter(bool completed,bool incomplete)=>vm.UpdateAppearance(p=>{p.ShowCompleted=completed;p.ShowIncomplete=incomplete;});
    string VisibilityKey()=>string.Join(',',VisibleTables());
    string CacheKey()=>$"{vm.State.Revision}|{month}|{loadedFrom}|{loadedUntil}|{agendaDate}|{VisibilityKey()}|{preferences.FontSize}|{preferences.ShowCompleted}|{preferences.ShowIncomplete}|{string.Join(',',preferences.Completed.Order())}|{string.Join(',',preferences.Important.Order())}";
    void OnChanged()
    {
        var old=preferences;preferences=vm.Appearance;bool sameView=old.View==preferences.View;view=preferences.View;
        completedFilter.IsChecked=preferences.ShowCompleted;incompleteFilter.IsChecked=preferences.ShowIncomplete;
        if(sameView&&renderedRevision==vm.State.Revision&&renderedVisibility==VisibilityKey()&&lastSelection==vm.SelectedDate&&old.FontSize==preferences.FontSize&&old.ShowCompleted==preferences.ShowCompleted&&old.ShowIncomplete==preferences.ShowIncomplete&&preferences.ShowCompleted&&preferences.ShowIncomplete&&body.Children.Count>0)
        {
            foreach(var card in Descendants(body).OfType<SwipeActivityCard>()){var key=card.Entry.Occurrence.Key;card.SetState(preferences.Completed.Contains(key),preferences.Important.Contains(key),true);}
            cache.Clear();cache[ViewMode]=(CacheKey(),body.Children[0],scroll,listHeadings.ToList());return;
        }
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
            var position=resetScroll?(0d,0d):scrollPositions.GetValueOrDefault(ViewMode);double offset=position.Item2;displayedView=ViewMode;
            string key=CacheKey();renderedRevision=vm.State.Revision;renderedVisibility=VisibilityKey();heading.Text=compact?"今日行程":"行程看板";modes.Visibility=filters.Visibility=compact?Visibility.Collapsed:Visibility.Visible;
            body.Children.Clear();
            if(cache.TryGetValue(ViewMode,out var cached)&&cached.Key==key){body.Children.Add(cached.Content);scroll=cached.Scroll;monthGrid=scroll?.Content as Grid;listHeadings.Clear();listHeadings.AddRange(cached.Headings);UpdateSelectedDay();caption.Content=compact?$"{agendaDate:M月d日}":view=="MONTH"?$"{month.Year} 年 {month.Month} 月":"全部行程 · 按日期";UpdateModes();resetScroll=false;var reused=scroll;Dispatcher.BeginInvoke(()=>{if(ReferenceEquals(scroll,reused)){FitRows();reused?.ScrollToHorizontalOffset(position.Item1);reused?.ScrollToVerticalOffset(position.Item2);UpdateListCaption();}},System.Windows.Threading.DispatcherPriority.Loaded);return;}
            monthGrid=null;scroll=null;listHeadings.Clear();
            UpdateModes();caption.Content=compact?$"{agendaDate:M月d日}":$"{month.Year} 年 {month.Month} 月";
            if(compact)BuildAgenda();else if(view=="MONTH")BuildMonth();else BuildList();
            cache[ViewMode]=(key,body.Children[0],scroll,listHeadings.ToList());
            resetScroll=false;if(scroll is {} current)Dispatcher.BeginInvoke(()=>{if(ReferenceEquals(scroll,current)){current.ScrollToVerticalOffset(offset);FitRows();UpdateListCaption();}});
        }
        finally{rendering=false;}
    }
    static IEnumerable<DependencyObject> Descendants(DependencyObject root){yield return root;foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())foreach(var item in Descendants(child))yield return item;}
    void UpdateSelectedDay(){if(monthGrid is null)return;foreach(var pick in Descendants(monthGrid).OfType<Button>())if(pick.Tag is DateOnly day)pick.FontWeight=day==vm.SelectedDate?FontWeights.Bold:FontWeights.SemiBold;}
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
            var day=days[index];bool current=day.Date.Month==month.Month;var panel=new StackPanel();
            var cell=new Border{Background=Ui.Brush("#EEEDEB"),BorderBrush=Ui.Brush("#E4E0DB"),BorderThickness=new(1),CornerRadius=new(18),Margin=new(3),Child=panel,Tag=day.Date};Grid.SetRow(cell,index/7);Grid.SetColumn(cell,index%7);monthGrid.Children.Add(cell);
            var daybar=new DockPanel();
            var pick=Button(day.Date.Day+(day.Date==Clock.Today?" · 今天":""),()=>SelectDay(day.Date));pick.Tag=day.Date;pick.Padding=new(9,8,6,8);pick.HorizontalContentAlignment=HorizontalAlignment.Left;Typography.Set(pick,13);pick.FontWeight=FontWeights.SemiBold;pick.Foreground=Ui.Brush(day.Date==Clock.Today?"#FFFAF6":current?"#AC5A4C":"#B8978C");daybar.Children.Add(pick);panel.Children.Add(new Border{Background=Ui.Brush(day.Date==Clock.Today?"#AC5A4C":"#F4DFD9"),CornerRadius=new(17,17,0,0),Child=daybar});
            var activities=new StackPanel{Margin=new(6,8,6,6)};panel.Children.Add(activities);foreach(var entry in day.Entries)activities.Children.Add(ActivityCard(entry,false,conflicts));
            cell.MouseLeftButtonUp+=(_,e)=>{DependencyObject? target=e.OriginalSource as DependencyObject;while(target is not null&&target!=cell){if(target is ButtonBase or TextBox or CheckBox||target is Border{Tag:BoardEntry})return;target=VisualTreeHelper.GetParent(target);}Add(day.Date);e.Handled=true;};
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
        scroll=new(){Content=panel,PanningMode=PanningMode.VerticalOnly,VerticalScrollBarVisibility=ScrollBarVisibility.Visible,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};scroll.ScrollChanged+=(_,_)=>UpdateListCaption();body.Children.Add(scroll);
        var range=Ui.Text($"{loadedFrom:yyyy年MM月} — {loadedUntil.AddDays(-1):yyyy年MM月} · 按日期查看",11);range.Foreground=Ui.Brush("#9B948E");panel.Children.Add(range);
        if(days.All(d=>d.Entries.Count==0)){panel.Children.Add(Ui.Text("这段时间还没有符合显示设置的行程",18,true));panel.Children.Add(Button("＋ 添加行程",()=>Add(vm.SelectedDate)));}
        foreach(var day in days.Where(d=>d.Entries.Count>0))
        {
            var heading=new DockPanel{Margin=new(0,16,0,10)};var count=Ui.Text(day.Entries.Count+" 项",12);count.Foreground=Ui.Brush("#A39A90");DockPanel.SetDock(count,Dock.Right);heading.Children.Add(count);heading.Children.Add(Ui.Text($"{day.Date.Day}  周{new[]{"一","二","三","四","五","六","日"}[((int)day.Date.DayOfWeek+6)%7]}    {day.Date:yyyy年M月}",17,true));panel.Children.Add(heading);listHeadings.Add((day.Date,heading));
            BoardEntry? previous=null;
            foreach(var entry in day.Entries)
            {
                if(previous is not null){int gap=(int)(entry.Start-previous.End).TotalMinutes;if(gap is >0 and <60){var gapText=Ui.Text($"◷  间隔 {gap} 分钟（含交通）",11,true);gapText.Foreground=Ui.Brush(Accent);panel.Children.Add(new Border{Background=Ui.Brush("#FAECE6"),Padding=new(9,3,9,0),Margin=new(104,0,0,7),HorizontalAlignment=HorizontalAlignment.Left,Child=gapText});}}
                var row=new Grid{Margin=new(0,0,0,9)};row.ColumnDefinitions.Add(new(){Width=new(94)});row.ColumnDefinitions.Add(new());var times=new StackPanel{Margin=new(6,10,6,8)};times.Children.Add(Ui.Text(entry.Start.ToString("HH:mm"),15,true));times.Children.Add(Ui.Text(((int)(entry.Occurrence.End-entry.Occurrence.Start).TotalMinutes)+" 分钟",11));times.Children.Add(Ui.Text(entry.TimeText.Split('—')[1],15,true));row.Children.Add(new Border{CornerRadius=new(14),Background=Ui.Brush("#EEEDEB"),Child=times,Margin=new(0,0,10,0)});var card=ActivityCard(entry,true,conflicts);card.Margin=new(0);Grid.SetColumn(card,1);row.Children.Add(card);panel.Children.Add(row);previous=entry;
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
        if(compact||view!="LIST"||scroll?.Content is not StackPanel panel||listHeadings.Count==0)return;
        DateOnly date=listHeadings[0].Date;foreach(var item in listHeadings){if(item.Heading.TranslatePoint(new(0,0),panel).Y<=scroll.VerticalOffset+8)date=item.Date;else break;}
        listVisibleDate=date;caption.Content=$"{date:yyyy年M月d日}";
    }
    SwipeActivityCard ActivityCard(BoardEntry entry,bool list,Dictionary<string,List<Conflict>> conflicts)
    {
        var o=entry.Occurrence;var table=vm.State.Tables.Single(t=>t.Id==o.Activity.TableId);bool active=table.Active(DateOnly.FromDateTime(o.Start));conflicts.TryGetValue(o.Key,out var warnings);
        var card=new SwipeActivityCard(entry);var panel=new StackPanel();var top=new DockPanel();DockPanel.SetDock(card.Star,Dock.Right);top.Children.Add(card.Star);
        var time=Ui.Text(entry.TimeText+(entry.ContinuesFromYesterday?" · 续":""),12,true);time.Foreground=Ui.Brush("#9B5142");time.Margin=new(0,3,0,5);top.Children.Add(time);panel.Children.Add(top);
        var title=Ui.Text(o.Title,list?15:14,true);title.Margin=new(0,0,0,6);title.Foreground=Ui.Brush("#443B35");panel.Children.Add(title);
        panel.Children.Add(Muted("地点："+vm.State.PlaceName(o.PlaceId),12));panel.Children.Add(Muted(entry.DurationText,11));if(entry.ContinuityText.Length>0)panel.Children.Add(Muted(entry.ContinuityText,11));
        if(!string.IsNullOrWhiteSpace(o.Activity.Note))panel.Children.Add(Muted("备注："+o.Activity.Note,12));
        panel.Children.Add(Muted(table.Name+(o.OriginDate is null?"":" · 每周")+(!active?" · 未生效":""),11));
        if(warnings is {Count:>0}){var warning=Ui.Text(warnings.Any(w=>w.Kind=="TIME_OVERLAP")?"! 时间冲突":"◷ 交通待核验",11,true);warning.Foreground=Ui.Brush(warnings.Any(w=>w.Kind=="TIME_OVERLAP")?"#AF5146":"#986638");warning.Margin=new(0,3,0,0);panel.Children.Add(warning);}
        card.Child=panel;card.SetState(preferences.Completed.Contains(o.Key),preferences.Important.Contains(o.Key));
        card.ToggleRequested+=()=>Ui.Safe(()=>ToggleCompleted(o.Key));card.ImportantRequested+=()=>Ui.Safe(()=>vm.UpdateAppearance(p=>{if(!p.Important.Add(o.Key))p.Important.Remove(o.Key);}));card.EditRequested+=()=>{SelectOccurrence(o);EditRequested?.Invoke(o);};
        var menu=new ContextMenu();menu.Opened+=(_,_)=>
        {
            menu.Items.Clear();foreach(var (label,action) in new (string,Action)[]{("查看详情",()=>SelectOccurrence(o)),("编辑活动",()=>{SelectOccurrence(o);EditRequested?.Invoke(o);}), (vm.Appearance.Completed.Contains(o.Key)?"恢复未完成":"标记完成",()=>ToggleCompleted(o.Key)),(vm.Appearance.Important.Contains(o.Key)?"取消重要":"标记重要",()=>vm.UpdateAppearance(p=>{if(!p.Important.Add(o.Key))p.Important.Remove(o.Key);})),("删除这一次",()=>DeleteRequested?.Invoke(o))}){var item=new MenuItem{Header=label};item.Click+=(_,_)=>Ui.Safe(action);menu.Items.Add(item);}
        };card.ContextMenu=menu;return card;
    }
    static TextBlock Muted(string text,double size){var label=Ui.Text(text,size);label.Foreground=Ui.Brush("#77716A");label.Margin=new(0,0,0,4);return label;}
    void ToggleCompleted(string key)=>vm.UpdateAppearance(p=>{if(!p.Completed.Add(key))p.Completed.Remove(key);});
    void SelectDay(DateOnly date)=>vm.FocusDate(date);
    void SelectOccurrence(Occurrence o){vm.FocusDate(DateOnly.FromDateTime(o.Start));Selected?.Invoke(o);}
    void Add(DateOnly date){SelectDay(date);AddRequested?.Invoke(date.ToDateTime(new(9,0)));}
    void BuildAgenda()
    {
        var days=DisplayDays(agendaDate,agendaDate.AddDays(7));var conflicts=Conflicts(agendaDate,agendaDate.AddDays(7));var list=new StackPanel{Margin=new(2)};
        scroll=new(){Content=list,PanningMode=PanningMode.VerticalOnly,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};body.Children.Add(scroll);
        foreach(var day in days)
        {
            string label=(day.Date==Clock.Today?"今天":day.Date==Clock.Today.AddDays(1)?"明天":day.Date.ToString("M月d日"))+" · 周"+new[]{"一","二","三","四","五","六","日"}[((int)day.Date.DayOfWeek+6)%7];
            var group=new StackPanel();group.Children.Add(new Border{Background=Ui.Brush(day.Date==Clock.Today?"#AC5A4C":"#F4DFD9"),CornerRadius=new(15,15,0,0),Padding=new(12,7,12,5),Child=new TextBlock{Text=label,Foreground=Ui.Brush(day.Date==Clock.Today?"#FFFAF6":"#AC5A4C"),FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap}});
            var content=new StackPanel{Margin=new(8)};foreach(var entry in day.Entries)content.Children.Add(ActivityCard(entry,true,conflicts));if(day.Entries.Count==0)content.Children.Add(Button("暂无安排 · 点击添加",()=>Add(day.Date)));group.Children.Add(content);
            list.Children.Add(new Border{Background=Ui.Brush("#EEEDEB"),CornerRadius=new(16),Margin=new(0,0,0,12),Child=group,Tag=day.Date});
        }
    }
    void PickDate()
    {
        var picker=new System.Windows.Controls.Calendar{SelectedDate=vm.SelectedDate.ToDateTime(TimeOnly.MinValue),DisplayDate=(compact?agendaDate:month).ToDateTime(TimeOnly.MinValue),DisplayMode=CalendarMode.Year};var popup=new Popup{Child=new Border{Background=Brushes.White,BorderBrush=Ui.Brush("#E6E0DA"),BorderThickness=new(1),Child=picker},PlacementTarget=caption,Placement=PlacementMode.Bottom,StaysOpen=false};
        picker.SelectedDatesChanged+=(_,_)=>{if(picker.SelectedDate is {} date){popup.IsOpen=false;NavigateTo(DateOnly.FromDateTime(date));}};popup.IsOpen=true;
    }
}
