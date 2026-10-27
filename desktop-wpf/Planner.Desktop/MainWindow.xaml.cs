using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Planner.Core;

namespace Planner.Desktop;
public partial class MainWindow : Window
{
    readonly PlannerViewModel vm;readonly StackPanel tables=new(),details=new();readonly TextBlock status=Ui.Text("",11);readonly ScheduleBoard calendar;
    public MainWindow(PlannerViewModel model)
    {
        InitializeComponent();vm=model;DataContext=vm;Root.RowDefinitions.Add(new(){Height=new(82)});Root.RowDefinitions.Add(new());Root.RowDefinitions.Add(new(){Height=new(30)});
        Root.ColumnDefinitions.Add(new(){Width=new(210)});Root.ColumnDefinitions.Add(new());Root.ColumnDefinitions.Add(new(){Width=new(330)});
        var top=new DockPanel{Margin=new(20,14,20,10)};Grid.SetColumnSpan(top,3);Root.Children.Add(top);
        var brand=new StackPanel();brand.Children.Add(Ui.Text("南京市内智能行程表",23,true));brand.Children.Add(Ui.Text("把事情记清楚，把一天安排好",11));DockPanel.SetDock(brand,Dock.Left);top.Children.Add(brand);
        var buttons=Ui.Row(Ui.Button("服务设置",()=>OpenSettings()),Ui.Button("地图与路线",()=>OpenRoutes()),Ui.Button("＋ 添加行程",()=>OpenEditor(null),true));buttons.HorizontalAlignment=HorizontalAlignment.Right;top.Children.Add(buttons);
        var left=new DockPanel{Margin=new(14,0,8,0)};Grid.SetRow(left,1);Root.Children.Add(left);
        var bottom=new StackPanel();bottom.Children.Add(Ui.Button("＋ 新建行程表",()=>TableEditor(null)));bottom.Children.Add(Ui.Button("载入示例行程",()=>{if(Ui.Confirm("仅在空行程中添加清楚标注的示例数据，继续？"))vm.LoadSample();}));bottom.Children.Add(Ui.Button("重新加载",vm.Reload));bottom.Children.Add(Ui.Text("显示只控制卡片是否可见。\n生效才决定是否参与排程。",11));DockPanel.SetDock(bottom,Dock.Bottom);left.Children.Add(bottom);left.Children.Add(tables);
        var center=new Grid{Margin=new(4,0,6,0)};Grid.SetRow(center,1);Grid.SetColumn(center,1);Root.Children.Add(center);
        calendar=new(vm);center.Children.Add(calendar);calendar.AddRequested+=d=>OpenEditor(null,d);calendar.EditRequested+=o=>OpenEditor(o);calendar.Selected+=ShowDetails;calendar.DeleteRequested+=Delete;
        var right=new ScrollViewer{Content=details,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Margin=new(10,0,16,0)};Grid.SetColumn(right,2);Grid.SetRow(right,1);Root.Children.Add(right);
        Grid.SetRow(status,2);Grid.SetColumnSpan(status,3);status.Margin=new(20,3,0,0);Root.Children.Add(status);
        vm.Changed+=Refresh;Refresh();Closed+=(_,_)=>OnClosedServices();
    }
    void Refresh()
    {
        status.Text=vm.Status+"   ·   "+vm.DataDirectory;
        tables.Children.Clear();tables.Children.Add(Ui.Text("我的行程表",16,true));
        foreach(var t in vm.State.Tables)
        {
            var panel=new StackPanel();var pick=Ui.Button(t.Name,()=>{vm.SelectedTableId=t.Id;scopeTables.Clear();scopeInitialized=false;assistantDraft=null;vm.Notify();});pick.Background=Ui.Brush(t.Color);pick.FontWeight=t.Id==vm.SelectedTableId?FontWeights.Bold:FontWeights.Normal;panel.Children.Add(pick);
            var visible=new CheckBox{Content="显示",IsChecked=!vm.HiddenTables.Contains(t.Id)};visible.Click+=(_,_)=>{if(visible.IsChecked==true)vm.HiddenTables.Remove(t.Id);else vm.HiddenTables.Add(t.Id);vm.Notify();};
            var active=new CheckBox{Content="生效",IsChecked=t.Enabled};active.Click+=(_,_)=>Ui.Safe(()=>{if(Ui.Confirm($"{(active.IsChecked==true?"启用":"停用")}“{t.Name}”？这会改变排程约束。"))vm.Mutate(s=>{var x=s.Tables.Single(x=>x.Id==t.Id);x.Enabled=active.IsChecked==true;x.Version++;foreach(var l in s.Legs)l.ValidationStatus="STALE";foreach(var p in s.Plans)p.Status="STALE";});else Refresh();});
            panel.Children.Add(Ui.Row(visible,active,Ui.Button("设置",()=>TableEditor(t))));tables.Children.Add(Ui.Card(panel,"#F6EDDF"));
        }
        ShowDetails(null);
    }
    void ShowDetails(Occurrence? o)
    {
        details.Children.Clear();details.Children.Add(Ui.Text("今天，也从容一点",18,true));details.Children.Add(Ui.Text($"当前选择 {vm.SelectedDate:yyyy-MM-dd}",12));
        if(o is not null)
        {
            var p=new StackPanel();p.Children.Add(Ui.Text(o.Title,17,true));p.Children.Add(Ui.Text($"{o.Start:MM-dd HH:mm} — {o.End:MM-dd HH:mm}"));p.Children.Add(Ui.Text(vm.State.PlaceName(o.PlaceId)));p.Children.Add(Ui.Row(Ui.Button("编辑",()=>OpenEditor(o)),Ui.Button("删除本次",()=>Delete(o))));details.Children.Add(Ui.Card(p));
        }
        var warnings=vm.WeekConflicts();var list=new StackPanel();list.Children.Add(Ui.Text("本周检查",14,true));list.Children.Add(Ui.Text(warnings.Count==0?"暂无时间重叠；未填地点的事项尚未核验交通。":string.Join("\n\n",warnings.Take(6).Select(x=>x.Message)),12));details.Children.Add(Ui.Card(list));
        BuildAssistant(details);
    }
    void OpenEditor(Occurrence? o,DateTime? initial=null){if(vm.State.Tables.Count==0)throw new ArgumentException("请先新建一份行程表。");var w=new EditorWindow(vm,o,initial){Owner=this};if(w.ShowDialog()==true&&w.SavedDate is {} date)calendar.NavigateTo(date);}
    void Delete(Occurrence o)
    {
        if(!Ui.Confirm($"删除“{o.Title}”？\n"+(o.OriginDate is null?"此项活动及关联路线和提醒将移除。":"仅取消这次，不影响之后的每周安排。")))return;
        vm.Edit([new(){Activity=JsonKit.Clone(o.Activity),OriginDate=o.OriginDate,Action="DELETE"}]);
    }
    void TableEditor(ScheduleTable? original)
    {
        var w=new Window{Title=original is null?"新建行程表":"行程表设置",Width=440,Height=470,Owner=this,WindowStartupLocation=WindowStartupLocation.CenterOwner};var p=new StackPanel{Margin=new(22)};w.Content=p;
        var name=Ui.Input(original?.Name??"");var color=Ui.Choices(["浅杏 · 工作","鼠尾草 · 个人","淡粉 · 休闲","浅黄 · 学习"]);string[] colors=["#E5BD99","#C2C3A5","#E8C6BB","#E8D9A7"];
        color.SelectedIndex=Math.Max(0,Array.IndexOf(colors,original?.Color));
        var from=new DatePicker{SelectedDate=original?.ValidFrom?.ToDateTime(TimeOnly.MinValue)};var until=new DatePicker{SelectedDate=original?.ValidUntil?.ToDateTime(TimeOnly.MinValue)};
        Ui.Field(p,"名称",name);Ui.Field(p,"颜色",color);Ui.Field(p,"生效起日（可留空）",from);Ui.Field(p,"结束日期（可留空）",until);
        p.Children.Add(Ui.Button("保存",()=>{if(string.IsNullOrWhiteSpace(name.Text)||name.Text.Trim().Length>60)throw new ArgumentException("名称应为1至60字。");if(from.SelectedDate>until.SelectedDate)throw new ArgumentException("结束日早于起日。");vm.Mutate(s=>{var t=original is null?new ScheduleTable():s.Tables.Single(t=>t.Id==original.Id);t.Name=name.Text.Trim();t.Color=colors[color.SelectedIndex];t.ValidFrom=from.SelectedDate is {} a?DateOnly.FromDateTime(a):null;t.ValidUntil=until.SelectedDate is {} b?DateOnly.FromDateTime(b):null;t.Version++;if(original is null)s.Tables.Add(t);foreach(var l in s.Legs)l.ValidationStatus="STALE";});w.Close();},true));
        if(original is not null)p.Children.Add(Ui.Button("删除行程表",()=>{var count=vm.State.Activities.Count(a=>a.TableId==original.Id);if(!Ui.Confirm($"删除“{original.Name}”及其 {count} 项活动、周期例外和关联路线？此操作不可撤销。"))return;vm.Mutate(s=>{foreach(var id in s.Activities.Where(a=>a.TableId==original.Id).Select(a=>a.Id).ToList())ChangeService.RemoveActivity(s,id);s.Scopes.RemoveAll(x=>x.TableId==original.Id);s.Tables.RemoveAll(t=>t.Id==original.Id);});if(vm.SelectedTableId==original.Id)vm.SelectedTableId=vm.State.Tables.FirstOrDefault()?.Id??"";w.Close();}));
        w.ShowDialog();
    }
    partial void BuildAssistant(Panel host);
    partial void OpenSettings();
    partial void OpenRoutes();
    partial void OnClosedServices();
}
