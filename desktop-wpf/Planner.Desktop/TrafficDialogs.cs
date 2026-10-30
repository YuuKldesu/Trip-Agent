using System.Windows;
using System.Windows.Controls;
using Planner.Core;
namespace Planner.Desktop;
public static class TrafficDialogs
{
    static async Task<T> Show<T>(Window owner,string title,Action<DockPanel,Action<T>,Action> build,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();var result=new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var w=new Window{Owner=owner,Title=title,Width=520,Height=310,MinWidth=380,MinHeight=240,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=AppTheme.Brush("Canvas")};
        var root=new DockPanel{Margin=new(22)};w.Content=root;
        build(root,value=>{result.TrySetResult(value);w.Close();},w.Close);
        w.Closed+=(_,_)=>result.TrySetCanceled();using var registration=token.Register(()=>w.Dispatcher.BeginInvoke(()=>w.Close()));w.Show();return await result.Task;
    }
    public static Task<string> Location(Window owner,string activity,CancellationToken token)=>Show<string>(owner,"补充地点",(root,done,close)=>
    {
        var actions=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right};DockPanel.SetDock(actions,Dock.Bottom);root.Children.Add(actions);var p=new StackPanel();root.Children.Add(p);p.Children.Add(Ui.Text("“"+activity+"”在哪里？",18,true));p.Children.Add(Ui.Text("填写地点后搜索高德候选；取消可返回编辑。",12));var input=Ui.Input();p.Children.Add(input);actions.Children.Add(Ui.Button("返回编辑",close));actions.Children.Add(Ui.Button("搜索地点",()=>{if(!string.IsNullOrWhiteSpace(input.Text))done(input.Text.Trim());},true));
    },token);
    public static Task<string> Mode(Window owner,TrafficPair pair,string? previous,CancellationToken token)=>Show<string>(owner,"这段路怎么去",(root,done,close)=>
    {
        var actions=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right};DockPanel.SetDock(actions,Dock.Bottom);root.Children.Add(actions);var p=new StackPanel();root.Children.Add(p);p.Children.Add(Ui.Text(pair.From.Title+" → "+pair.To.Title,18,true));p.Children.Add(Ui.Text($"可用间隔 {pair.GapMinutes} 分钟；此选择只用于这段路。",12));var choices=new[]{"公交","步行","骑行","驾车"};var mode=Ui.Choices(new[]{"公交（含地铁与步行）","步行","骑行","驾车"},Math.Max(0,Array.IndexOf(choices,previous)));mode.Tag="TrafficMode";p.Children.Add(mode);actions.Children.Add(Ui.Button("返回编辑",close));actions.Children.Add(Ui.Button("查询这段路",()=>done(choices[mode.SelectedIndex]),true));
    },token);
    public static Task<bool> SaveWarning(Window owner,string message,CancellationToken token)=>Show<bool>(owner,"检查前后行程",(root,done,close)=>
    {
        var actions=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right};DockPanel.SetDock(actions,Dock.Bottom);root.Children.Add(actions);actions.Children.Add(Ui.Button("返回修改",()=>done(false)));actions.Children.Add(Ui.Button("仍然保存",()=>done(true),true));root.Children.Add(new ScrollViewer{Content=Ui.Text(message,13),VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
    },token);
}
