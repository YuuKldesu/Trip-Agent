using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Planner.Core;
using Planner.Data;
namespace Planner.Desktop;
public partial class MainWindow:Window
{
    readonly PlannerViewModel vm;readonly StackPanel tables=new();readonly DockPanel details=new();readonly TextBlock status=Ui.Text("",11);readonly ScheduleBoard calendar;
    readonly DockPanel navigation=new();readonly Border left,right;readonly FrameworkElement center,brand;readonly Button compactSettings;bool compact,changingLayout,closing;
    double fullWidth,fullHeight,compactWidth,compactHeight;SettingsWindow? settingsWindow;EditorWindow? activeEditor;
    public bool IsCompact=>compact;
    public MainWindow(PlannerViewModel model)
    {
        InitializeComponent();vm=model;DataContext=vm;Typography.Apply(vm.Appearance.FontSize);Typography.Set(this,13);fullWidth=vm.Appearance.FullWidth;fullHeight=vm.Appearance.FullHeight;compactWidth=vm.Appearance.CompactWidth;compactHeight=vm.Appearance.CompactHeight;
        Root.RowDefinitions.Add(new());Root.RowDefinitions.Add(new(){Height=GridLength.Auto});Root.ColumnDefinitions.Add(new(){Width=new(62)});Root.ColumnDefinitions.Add(new(){Width=new(0)});Root.ColumnDefinitions.Add(new());Root.ColumnDefinitions.Add(new(){Width=new(0)});
        navigation.Background=Ui.Brush("#F1EEEA");navigation.Margin=new(0);Grid.SetRowSpan(navigation,2);Root.Children.Add(navigation);
        left=new(){Visibility=Visibility.Collapsed};
        var middle=new DockPanel{Margin=new(14,10,14,0)};center=middle;Grid.SetColumn(middle,2);Root.Children.Add(middle);var heading=new DockPanel{Margin=new(2,0,2,10)};DockPanel.SetDock(heading,Dock.Top);middle.Children.Add(heading);var actions=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right};DockPanel.SetDock(actions,Dock.Right);heading.Children.Add(actions);compactSettings=ShellIcons.Button(ShellIcons.Settings,"设置",()=>OpenSettings(),"NavSettingsCompact");actions.Children.Add(compactSettings);actions.Children.Add(Ui.Button("＋ 添加行程",()=>OpenEditor(null),true));var title=new StackPanel();title.Children.Add(Ui.Text("南京市内智能行程表",21,true));title.Children.Add(Ui.Text("把每一天安排得从容一点",11));brand=title;heading.Children.Add(title);
        calendar=new(vm){Focusable=true};middle.Children.Add(calendar);calendar.AddRequested+=d=>OpenEditor(null,d);calendar.EditRequested+=o=>OpenEditor(o);calendar.Selected+=ShowDetails;calendar.DeleteRequested+=Delete;
        right=new(){Background=Ui.Brush("#F6F0E7"),BorderBrush=Ui.Brush("#E5DBCF"),BorderThickness=new(1,0,0,0),Padding=new(12),Child=details};Grid.SetColumn(right,3);Root.Children.Add(right);BuildAssistant(details);AddAssistantSplitter();
        Grid.SetRow(status,1);Grid.SetColumn(status,1);Grid.SetColumnSpan(status,3);status.Margin=new(14,5,14,5);Root.Children.Add(status);
        vm.Changed+=Refresh;SizeChanged+=(_,_)=>{if(!changingLayout){RememberSize();ApplyLayout();}};Loaded+=(_,_)=>{changingLayout=true;try{var area=SystemParameters.WorkArea;bool small=vm.Appearance.LayoutMode=="COMPACT";Width=Math.Min(small?compactWidth:fullWidth,area.Width);Height=Math.Min(small?compactHeight:fullHeight,area.Height);}finally{changingLayout=false;}ApplyLayout();};
        PreviewKeyDown+=(_,e)=>{if(e.Key==Key.B&&Keyboard.Modifiers==ModifierKeys.Control){ToggleSidebar();e.Handled=true;}};
        Closing+=(_,_)=>{closing=true;RememberSize();new BoardPreferenceStore(vm.DataDirectory).Update(p=>{p.FullWidth=fullWidth;p.FullHeight=fullHeight;p.CompactWidth=compactWidth;p.CompactHeight=compactHeight;});};Closed+=(_,_)=>{vm.Changed-=Refresh;settingsWindow?.Close();OnClosedServices();};Refresh();
    }
    void Refresh(){if(closing)return;Typography.Apply(vm.Appearance.FontSize);BuildNavigation();ApplyLayout();}
    void ShowBoard(){Activate();calendar.Focus();}
    public void ToggleTables()=>vm.UpdateAppearance(p=>{p.SidebarExpanded=true;p.TablesPanelOpen=!p.TablesPanelOpen;});
    void ToggleSidebar()=>vm.UpdateAppearance(p=>p.SidebarExpanded=!p.SidebarExpanded);
    public void ToggleAssistant()=>vm.UpdateAppearance(p=>p.AssistantPanelOpen=!p.AssistantPanelOpen);
    void RememberSize(){if(!IsLoaded||WindowState!=WindowState.Normal)return;if(compact&&ActualWidth is >=320 and <=800&&ActualHeight is >=460 and <=3000){compactWidth=ActualWidth;compactHeight=ActualHeight;}else if(!compact&&ActualWidth is >=1120 and <=4000&&ActualHeight is >=680 and <=3000){fullWidth=ActualWidth;fullHeight=ActualHeight;}}
    public void SetLayoutMode(string mode)
    {
        if(mode is not("AUTO" or "FULL" or "COMPACT"))throw new ArgumentException("未知界面模式。");RememberSize();vm.UpdateAppearance(p=>p.LayoutMode=mode);if(mode!="AUTO"){changingLayout=true;try{var area=SystemParameters.WorkArea;Width=Math.Min(mode=="COMPACT"?compactWidth:fullWidth,area.Width);Height=Math.Min(mode=="COMPACT"?compactHeight:fullHeight,area.Height);Left=Math.Clamp(Left,area.Left,Math.Max(area.Left,area.Right-Width));Top=Math.Clamp(Top,area.Top,Math.Max(area.Top,area.Bottom-Height));}finally{changingLayout=false;}}ApplyLayout();
    }
    void ApplyLayout()
    {
        if(changingLayout||closing)return;changingLayout=true;try{compact=PresentationPolicy.Compact(vm.Appearance.LayoutMode,ActualWidth>0?ActualWidth:Width,ActualHeight>0?ActualHeight:Height,compact,vm.Appearance.FontSize);MinWidth=Math.Min(vm.Appearance.LayoutMode=="FULL"?1120:320,SystemParameters.WorkArea.Width);MinHeight=Math.Min(compact?460:680,SystemParameters.WorkArea.Height);
        navigation.Visibility=brand.Visibility=compact?Visibility.Collapsed:Visibility.Visible;left.Visibility=Visibility.Collapsed;right.Visibility=!compact&&vm.Appearance.AssistantPanelOpen?Visibility.Visible:Visibility.Collapsed;Root.ColumnDefinitions[0].Width=new(compact?0:vm.Appearance.SidebarExpanded?260:56);Root.ColumnDefinitions[1].Width=new(0);Root.ColumnDefinitions[3].Width=new(right.Visibility==Visibility.Visible?Math.Min(vm.Appearance.AssistantWidth,Math.Max(280,ActualWidth-Root.ColumnDefinitions[0].Width.Value-420)):0);compactSettings.Visibility=compact?Visibility.Visible:Visibility.Collapsed;center.Margin=compact?new(8,6,8,0):new(14,10,14,0);Topmost=compact&&vm.Appearance.AlwaysOnTop;Title=compact?"今日行程":"南京市内智能行程表";calendar.SetCompact(compact);status.Text=vm.Status;}finally{changingLayout=false;}
    }
    public void OpenSettingsCategory(string category)
    {
        if(settingsWindow is not null){settingsWindow.SelectCategory(category);settingsWindow.Activate();return;}var w=new SettingsWindow(vm,category){Owner=this};settingsWindow=w;w.Closed+=(_,_)=>settingsWindow=null;w.Show();
    }
    void OpenTables(){if(compact)OpenSettingsCategory("appearance");else if(!vm.Appearance.TablesPanelOpen)ToggleTables();}
    void ShowDetails(Occurrence? occurrence){if(occurrence is not null)vm.Notify($"{occurrence.Title} · {occurrence.Start:MM-dd HH:mm}—{occurrence.End:MM-dd HH:mm}");else{details.Children.Clear();BuildAssistant(details);}}
    void OpenEditor(Occurrence? o,DateTime? initial=null){if(vm.State.Tables.Count==0)throw new ArgumentException("请先新建一份行程表。");if(activeEditor is not null){activeEditor.Activate();return;}var w=new EditorWindow(vm,o,initial){Owner=this};activeEditor=w;w.Closed+=(_,_)=>{activeEditor=null;if(w.SavedDate is {} date)calendar.NavigateTo(date);};w.Show();}
    void Delete(Occurrence o){if(!Ui.Confirm($"删除“{o.Title}”？\n"+(o.OriginDate is null?"此项活动及关联路线和提醒将移除。":"仅取消这次，不影响之后的每周安排。")))return;vm.Edit([new(){Activity=JsonKit.Clone(o.Activity),OriginDate=o.OriginDate,Action="DELETE"}]);}
    void TableEditor(ScheduleTable? original)
    {
        var w=new Window{Title=original is null?"新建行程表":"行程表设置",Width=460,Height=Math.Min(640,SystemParameters.WorkArea.Height),Owner=this,WindowStartupLocation=WindowStartupLocation.CenterOwner,MinWidth=340,MinHeight=360};var p=new StackPanel{Margin=new(22)};w.Content=new ScrollViewer{Content=p,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};var name=Ui.Input(original?.Name??"");var colors=new TableColorPicker(original?.Color);var from=new StyledDateEntry{SelectedDate=original?.ValidFrom?.ToDateTime(TimeOnly.MinValue)};var until=new StyledDateEntry{SelectedDate=original?.ValidUntil?.ToDateTime(TimeOnly.MinValue)};Ui.Field(p,"名称",name);p.Children.Add(Ui.Text("选择行程表主色",13,true));p.Children.Add(colors);Ui.Field(p,"生效起日（可留空）",from);Ui.Field(p,"结束日期（可留空）",until);
        var save=Ui.Button("保存",()=>{if(string.IsNullOrWhiteSpace(name.Text)||name.Text.Trim().Length>60)throw new ArgumentException("名称应为1至60字。");if(colors.SelectedColor is null)throw new ArgumentException("请选择行程表主色。");if(from.SelectedDate>until.SelectedDate)throw new ArgumentException("结束日早于起日。");vm.Mutate(s=>{var t=original is null?new ScheduleTable():s.Tables.Single(t=>t.Id==original.Id);t.Name=name.Text.Trim();t.Color=colors.SelectedColor;t.ValidFrom=from.SelectedDate is {} a?DateOnly.FromDateTime(a):null;t.ValidUntil=until.SelectedDate is {} b?DateOnly.FromDateTime(b):null;t.Version++;if(original is null)s.Tables.Add(t);foreach(var l in s.Legs)l.ValidationStatus="STALE";});w.Close();},true);save.IsEnabled=colors.SelectedColor is not null;colors.SelectionChanged+=()=>save.IsEnabled=true;p.Children.Add(save);
        if(original is not null)p.Children.Add(Ui.Button("删除行程表",()=>{int count=vm.State.Activities.Count(a=>a.TableId==original.Id);if(!Ui.Confirm($"删除“{original.Name}”及其 {count} 项活动、周期例外和关联路线？此操作不可撤销。"))return;vm.Mutate(s=>{foreach(var id in s.Activities.Where(a=>a.TableId==original.Id).Select(a=>a.Id).ToList())ChangeService.RemoveActivity(s,id);s.Scopes.RemoveAll(x=>x.TableId==original.Id);s.Tables.RemoveAll(t=>t.Id==original.Id);});if(vm.SelectedTableId==original.Id)vm.SelectedTableId=vm.State.Tables.FirstOrDefault()?.Id??"";w.Close();}));w.ShowDialog();
    }
    partial void BuildAssistant(Panel host);partial void OpenSettings();partial void OpenRoutes();partial void OnClosedServices();
}
