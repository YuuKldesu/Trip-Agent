using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Planner.Core;
namespace Planner.Desktop;
public partial class MainWindow
{
    void BuildNavigation()
    {
        if(tables.Parent is Panel previous)previous.Children.Remove(tables);
        navigation.Children.Clear();bool expanded=vm.Appearance.SidebarExpanded;
        var head=new DockPanel{Margin=new(5,8,5,10)};DockPanel.SetDock(head,Dock.Top);navigation.Children.Add(head);
        var toggle=ShellIcons.Button(ShellIcons.Menu,"显示/隐藏侧边栏",ToggleSidebar,"NavToggle");DockPanel.SetDock(toggle,Dock.Right);head.Children.Add(toggle);
        if(expanded){var search=ShellIcons.Button("\uE721","搜索行程和行程表",OpenSearch,"NavSearch");DockPanel.SetDock(search,Dock.Right);head.Children.Add(search);head.Children.Add(new TextBlock{Text="南京行程",FontSize=18,FontWeight=FontWeights.SemiBold,Margin=new(10,0,0,0),VerticalAlignment=VerticalAlignment.Center});}
        var bottom=new StackPanel{Margin=new(5,6,5,10)};DockPanel.SetDock(bottom,Dock.Bottom);navigation.Children.Add(bottom);
        var profile=ShellIcons.Button(ShellIcons.Profile,vm.Appearance.Nickname,()=>OpenSettingsCategory("profile"),"NavProfile",expanded);if(profile.Content is StackPanel profileRow){profileRow.Children.RemoveAt(0);profileRow.Children.Insert(0,new Border{Width=24,Height=24,CornerRadius=new(12),Background=Ui.Brush("#D8B6A0"),Child=new TextBlock{Text=vm.Appearance.Avatar,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center}});}bottom.Children.Add(profile);bottom.Children.Add(ShellIcons.Button(ShellIcons.Settings,"设置",()=>OpenSettings(),"NavSettings",expanded));
        var content=new StackPanel{Margin=new(5,0,5,0)};navigation.Children.Add(new ScrollViewer{Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
        void Add(string glyph,string label,Action action,string tag,bool active=false){var b=ShellIcons.Button(glyph,label,action,tag,expanded);if(expanded)b.HorizontalContentAlignment=HorizontalAlignment.Left;if(active)b.Background=Ui.Brush("#E5E1DC");content.Children.Add(b);}
        if(!expanded)Add("\uE721","搜索行程和行程表",OpenSearch,"NavSearch");
        Add("\uE70F","新建行程",()=>OpenEditor(null),"NavNew");Add(ShellIcons.Board,"行程看板",ShowBoard,"NavBoard",true);
        Add(ShellIcons.Tables,"我的行程表",ToggleTables,"NavTables",vm.Appearance.TablesPanelOpen&&expanded);
        if(expanded&&vm.Appearance.TablesPanelOpen){BuildTableRows();content.Children.Add(tables);var create=Ui.Button("＋ 新建行程表",()=>TableEditor(null));create.Background=Brushes.Transparent;create.HorizontalContentAlignment=HorizontalAlignment.Left;content.Children.Add(create);}
        Add(ShellIcons.Map,"地图与路线",()=>OpenRoutes(),"NavMap");Add(ShellIcons.Assistant,"智能助手",ToggleAssistant,"NavAssistant",vm.Appearance.AssistantPanelOpen);
    }
    void BuildTableRows()
    {
        tables.Children.Clear();foreach(var table in vm.State.Tables)
        {
            var row=new Grid{Margin=new(14,1,2,1)};row.ColumnDefinitions.Add(new());row.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
            var title=new StackPanel{Orientation=Orientation.Horizontal};title.Children.Add(new Border{Background=Ui.Brush(table.Color),Width=8,Height=8,CornerRadius=new(4),Margin=new(0,0,8,0)});title.Children.Add(new TextBlock{Text=table.Name,TextTrimming=TextTrimming.CharacterEllipsis,VerticalAlignment=VerticalAlignment.Center,MaxWidth=120});
            var pick=Ui.Button("",()=>{vm.SelectedTableId=table.Id;vm.Notify("新行程默认加入："+table.Name);});pick.Content=title;pick.Padding=new(8);pick.HorizontalContentAlignment=HorizontalAlignment.Left;pick.Background=table.Id==vm.SelectedTableId?Ui.Brush("#E5E1DC"):Brushes.Transparent;row.Children.Add(pick);
            var actions=new StackPanel{Orientation=Orientation.Horizontal};Grid.SetColumn(actions,1);row.Children.Add(actions);
            var eye=Ui.Button(vm.HiddenTables.Contains(table.Id)?"◌":"◉",()=>{if(!vm.HiddenTables.Add(table.Id))vm.HiddenTables.Remove(table.Id);vm.UpdateAppearance(p=>p.HiddenTables=new(vm.HiddenTables));});eye.ToolTip="显示/隐藏“"+table.Name+"”（不改变是否参与排程）";eye.Padding=new(5);eye.Background=Brushes.Transparent;actions.Children.Add(eye);
            var menu=new ContextMenu();var edit=new MenuItem{Header="重命名、配色与删除…"};edit.Click+=(_,_)=>TableEditor(table);menu.Items.Add(edit);var active=new MenuItem{Header="参与排程",IsCheckable=true,IsChecked=table.Enabled};active.Click+=(_,_)=>Ui.Safe(()=>{if(!Ui.Confirm("更改“"+table.Name+"”是否参与排程？"))return;vm.Mutate(s=>{var t=s.Tables.Single(t=>t.Id==table.Id);t.Enabled=active.IsChecked;t.Version++;foreach(var leg in s.Legs)leg.ValidationStatus="STALE";foreach(var plan in s.Plans)plan.Status="STALE";});});menu.Items.Add(active);
            var more=Ui.Button("…",()=>menu.IsOpen=true);more.Tag="TableEdit:"+table.Id;more.ToolTip="行程表操作";more.Padding=new(5);more.Background=Brushes.Transparent;more.ContextMenu=menu;menu.PlacementTarget=more;actions.Children.Add(more);more.Opacity=0;row.MouseEnter+=(_,_)=>more.Opacity=1;row.MouseLeave+=(_,_)=>{if(!more.IsKeyboardFocusWithin)more.Opacity=0;};more.GotKeyboardFocus+=(_,_)=>more.Opacity=1;tables.Children.Add(row);
        }
    }
    void OpenSearch()
    {
        var window=new Window{Title="搜索行程与行程表",Owner=this,Width=680,Height=560,MinWidth=380,MinHeight=320,WindowStartupLocation=WindowStartupLocation.CenterOwner};
        var panel=new DockPanel{Margin=new(20)};window.Content=panel;var input=Ui.Input();input.Tag="LocalSearchInput";input.ToolTip="搜索标题、地点、备注和行程表名称";DockPanel.SetDock(input,Dock.Top);panel.Children.Add(input);var count=Ui.Text("输入关键词，搜索本机保存的内容（包含已完成行程）",11);DockPanel.SetDock(count,Dock.Top);panel.Children.Add(count);var results=new StackPanel();panel.Children.Add(new ScrollViewer{Content=results,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
        input.TextChanged+=(_,_)=>{results.Children.Clear();var found=LocalSearch.Find(vm.State,input.Text,Clock.Today);count.Text=input.Text.Trim().Length==0?"搜索本地行程和行程表":"找到 "+found.Count+" 项"+(found.Count>200?"，显示前200项，请细化关键词":"");foreach(var hit in found.Take(200)){var content=new StackPanel();content.Children.Add(Ui.Text(hit.Title,14,true));content.Children.Add(Ui.Text(hit.Detail,11));var pick=Ui.Button("",()=>{window.Close();OpenSearchHit(hit);});pick.Content=content;pick.HorizontalContentAlignment=HorizontalAlignment.Left;pick.Background=Brushes.Transparent;results.Children.Add(pick);}};
        window.Loaded+=(_,_)=>input.Focus();window.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape){window.Close();e.Handled=true;}};window.Show();
    }
    void OpenSearchHit(LocalSearchHit hit)
    {
        vm.SelectedTableId=hit.TableId;vm.UpdateAppearance(p=>{p.SidebarExpanded=true;p.TablesPanelOpen=true;});
        if(hit.Kind=="table")return;
        if(hit.Day is not {} day)return;
        vm.HiddenTables.Remove(hit.TableId);vm.UpdateAppearance(p=>p.HiddenTables=new(vm.HiddenTables));calendar.NavigateTo(day);
        var occurrence=CalendarEngine.ExpandForDisplay(vm.State,day.ToDateTime(TimeOnly.MinValue),day.AddDays(1).ToDateTime(TimeOnly.MinValue),[hit.TableId]).FirstOrDefault(o=>o.Activity.Id==hit.Id);
        if(occurrence is not null)OpenEditor(occurrence);
    }
    void AddAssistantSplitter()
    {
        var grip=new Thumb{Width=6,HorizontalAlignment=HorizontalAlignment.Left,Cursor=Cursors.SizeWE,Background=Brushes.Transparent,ToolTip="拖动调整助手宽度"};Grid.SetColumn(grip,3);Root.Children.Add(grip);grip.SetBinding(VisibilityProperty,new System.Windows.Data.Binding(nameof(Visibility)){Source=right});
        double width=vm.Appearance.AssistantWidth;grip.DragStarted+=(_,_)=>width=Root.ColumnDefinitions[3].ActualWidth;grip.DragDelta+=(_,e)=>{width=Math.Clamp(width-e.HorizontalChange,280,Math.Min(700,Math.Max(280,ActualWidth-Root.ColumnDefinitions[0].ActualWidth-420)));Root.ColumnDefinitions[3].Width=new(width);};grip.DragCompleted+=(_,_)=>vm.UpdateAppearance(p=>p.AssistantWidth=width);
    }
}
