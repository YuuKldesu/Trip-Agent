using System.Windows;
using System.Windows.Controls;
using Planner.Core;
using Planner.Data;
namespace Planner.Desktop;
public static class RepositoryDialogs
{
    public static async Task<int> Choose(Window owner,string title,string description,IReadOnlyList<string> options,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();if(options.Count==0)throw new ArgumentException("没有可选方案。");
        var result=new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);var w=new Window{Owner=owner,Title=title,Width=660,Height=450,MinWidth=520,MinHeight=300,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=Ui.Brush("#FBF7F0")};
        var root=new DockPanel{Margin=new(22)};w.Content=root;var info=Ui.Text(description,14,true);DockPanel.SetDock(info,Dock.Top);root.Children.Add(info);
        var buttons=Ui.Row(Ui.Button("取消",w.Close));DockPanel.SetDock(buttons,Dock.Bottom);root.Children.Add(buttons);var list=new ListBox{ItemsSource=options,SelectedIndex=0,Margin=new(0,10,0,10)};root.Children.Add(list);buttons.Children.Add(Ui.Button("确认选择",()=>{if(list.SelectedIndex>=0){result.TrySetResult(list.SelectedIndex);w.Close();}},true));
        w.Closed+=(_,_)=>result.TrySetCanceled();using var registration=token.Register(()=>w.Dispatcher.BeginInvoke(()=>w.Close()));w.Show();return await result.Task;
    }
    public static async Task<Place> ChoosePlace(Window owner,List<Place> places,CancellationToken token)
    {
        if(places.Count==1)return places[0];int index=await Choose(owner,"选择准确地点","请选择具体地点，找不到时取消并换关键词。",places.Select(p=>p+" · "+p.Source).ToArray(),token);return places[index];
    }
    public static async Task<TravelEstimate> Travel(Window owner,RepositoryMap map,Place from,Place to,string? mode,DateTime departure,CancellationToken token)
    {
        if(!string.IsNullOrWhiteSpace(mode))return await map.TravelAsync(from,to,mode,departure,token);
        var options=new List<TravelEstimate>();foreach(string m in new[]{"公交","步行","骑行","驾车"}){try{options.Add(await map.TravelAsync(from,to,m,departure,token));}catch(OperationCanceledException){throw;}catch{}}
        if(options.Count==0)throw new InvalidOperationException("所有交通方式查询失败，请检查地点、密钥和网络。");
        int index=await Choose(owner,"选择交通方案",$"从{from.Name}到{to.Name}",options.Select(x=>$"{x.Source} · 约{x.Minutes}分钟").ToArray(),token);return options[index];
    }
}
