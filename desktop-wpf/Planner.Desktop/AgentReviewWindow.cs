using System.Windows;
using System.Windows.Controls;
using Planner.Core;
using Planner.Data;
namespace Planner.Desktop;

public sealed class AgentReviewWindow:Window
{
    sealed class Card
    {
        public required TripRequest Original;public required TextBox Title;public required DatePicker Day;public required TimeEntry Begin;public required TimeEntry End;public required TextBox Duration;public required TextBox Deadline;public required TextBox Query;public required ComboBox Places;public required ComboBox Mode;public required TextBox Note;public required TextBlock Status;public Place? Chosen;
    }
    readonly List<Card> cards=[];readonly PlannerViewModel vm;readonly RepositoryMap map;readonly ComboBox table;readonly TextBlock status=Ui.Text("",12);readonly CancellationTokenSource cancel=new();readonly StackPanel panel=new();bool closing;
    public AgentReviewWindow(PlannerViewModel model,IReadOnlyList<TripRequest> requests)
    {
        vm=model;map=new(new SettingsStore(vm.DataDirectory).Load());Title="完善行程";Width=960;Height=790;MinWidth=760;MinHeight=600;WindowStartupLocation=WindowStartupLocation.CenterOwner;Background=Ui.Brush("#FBF7F0");FontFamily=new("Microsoft YaHei UI");
        var root=new DockPanel{Margin=new(24)};Content=root;var heading=new StackPanel();heading.Children.Add(Ui.Text("完善行程",22,true));heading.Children.Add(Ui.Text("核对活动，选择准确地点，补充时间后计算路线。开始留空表示由系统安排。",12));table=new();foreach(var t in vm.State.Tables)table.Items.Add(new ComboBoxItem{Content=t.Name,Tag=t.Id});table.SelectedIndex=Math.Max(0,vm.State.Tables.FindIndex(t=>t.Id==vm.SelectedTableId));Ui.Field(heading,"保存到行程表",table);DockPanel.SetDock(heading,Dock.Top);root.Children.Add(heading);
        var footer=new DockPanel();DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);var buttons=Ui.Row(Ui.Button("取消",Close));DockPanel.SetDock(buttons,Dock.Right);footer.Children.Add(buttons);footer.Children.Add(status);var generate=Ui.Button("生成行程 →",()=>{},true);buttons.Children.Add(generate);generate.Click+=async(_,_)=>{generate.IsEnabled=false;panel.IsEnabled=false;table.IsEnabled=false;try{await Generate();}catch(OperationCanceledException){if(!closing)status.Text="已取消，可调整后重试。";}catch(Exception ex){if(!closing)status.Text=ex.Message;}finally{if(!closing){generate.IsEnabled=true;panel.IsEnabled=true;table.IsEnabled=true;}}};
        root.Children.Add(new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});for(int i=0;i<requests.Count;i++)AddCard(requests[i],i);Closed+=(_,_)=>{closing=true;cancel.Cancel();};
    }
    void AddCard(TripRequest request,int index)
    {
        var p=new StackPanel();p.Children.Add(Ui.Text($"{index+1:00} · {request.Title??"待补充活动"}",16,true));
        var card=new Card{Original=JsonKit.Clone(request),Title=Ui.Input(request.Title??""),Day=new(){SelectedDate=request.Day?.ToDateTime(TimeOnly.MinValue)},Begin=new(ClockText(request.Start)),End=new(ClockText(request.End??(request.Start+request.Duration))),Duration=Ui.Input(request.Duration?.ToString()??""),Deadline=Ui.Input(ClockText(request.LatestEnd)),Query=Ui.Input(request.Location??""),Places=new(){DisplayMemberPath="Name"},Mode=Ui.Choices(["选择交通时再决定","公交","步行","骑行","驾车"],Math.Max(0,Array.IndexOf(new[]{"选择交通时再决定","公交","步行","骑行","驾车"},request.ModeIn))),Note=Ui.Input(request.Notes??""),Status=Ui.Text("请搜索并选择地点",11)};
        Pair(p,"活动名称",card.Title,"日期",card.Day);Pair(p,"开始（可留空）",card.Begin,"结束（可留空）",card.End);Pair(p,"耗时（分钟）",card.Duration,"最晚结束/到达（可留空）",card.Deadline);
        bool syncing=false;
        void Calc(){if(syncing)return;try{syncing=true;if(Minute(card.Begin.Text) is {} start&&Minute(card.End.Text) is {} end&&end>=start)card.Duration.Text=(end-start).ToString();}finally{syncing=false;}}
        card.Duration.TextChanged+=(_,_)=>{if(syncing)return;try{syncing=true;if(int.TryParse(card.Duration.Text,out var minutes)&&minutes>=0&&Minute(card.Begin.Text) is {} start&&start+minutes<1440)card.End.Text=ClockText(start+minutes);}catch(ArgumentException){}finally{syncing=false;}};
        card.Begin.Changed+=()=>{try{Calc();}catch(ArgumentException){}};card.End.Changed+=()=>{try{Calc();}catch(ArgumentException){}};
        var quick=new WrapPanel();foreach(int duration in new[]{30,60,90,120})quick.Children.Add(Ui.Button(duration+"分钟",()=>{card.Duration.Text=duration.ToString();if(Minute(card.Begin.Text) is {} m)card.End.Text=ClockText(m+duration);}));p.Children.Add(quick);
        Ui.Field(p,"地点关键词",card.Query);var search=Ui.Button("搜索地点",()=>{});p.Children.Add(search);Ui.Field(p,"准确地点",card.Places);p.Children.Add(card.Status);
        card.Query.TextChanged+=(_,_)=>{card.Chosen=null;card.Places.ItemsSource=null;card.Status.Text="地点已改动，请重新搜索并选择。";};card.Places.SelectionChanged+=(_,_)=>{card.Chosen=card.Places.SelectedItem as Place;if(card.Chosen is {} chosen)card.Status.Text=chosen.Source??"已选择";};
        search.Click+=async(_,_)=>{search.IsEnabled=false;string query=card.Query.Text;try{var found=await map.SearchAsync(query,cancel.Token);if(!closing&&query==card.Query.Text){card.Places.ItemsSource=found;card.Places.SelectedIndex=found.Count==1?0:-1;card.Status.Text=found.Count==1?found[0].Source??"已选地点":"请选择准确地点";}}catch(OperationCanceledException){}catch(Exception ex){if(!closing)card.Status.Text=ex.Message;}finally{if(!closing)search.IsEnabled=true;}};
        Ui.Field(p,"前往此处",card.Mode);Ui.Field(p,"备注",card.Note);
        if(index>0){var compare=Ui.Button("比较与上一项之间的交通",()=>{});p.Children.Add(compare);compare.Click+=async(_,_)=>{compare.IsEnabled=false;try{var before=cards[index-1].Chosen??throw new ArgumentException("请先选择上一项地点。");var here=card.Chosen??throw new ArgumentException("请先选择此项地点。");var estimate=await RepositoryDialogs.Travel(this,map,before,here,null,(card.Day.SelectedDate??Clock.Now).Date,cancel.Token);card.Mode.SelectedItem=estimate.Source.Split(" · ")[0];card.Status.Text=$"{estimate.Source} · 约{estimate.Minutes}分钟";}catch(OperationCanceledException){}catch(Exception ex){if(!closing)card.Status.Text=ex.Message;}finally{if(!closing)compare.IsEnabled=true;}};}
        cards.Add(card);panel.Children.Add(Ui.Card(p));
    }
    static string ClockText(int? minute)=>minute is {} m?$"{m/60:00}:{m%60:00}":"";
    static int? Minute(string text){if(string.IsNullOrWhiteSpace(text))return null;if(!TimeOnly.TryParseExact(text.Trim(),"HH:mm",out var t))throw new ArgumentException("时间请使用HH:mm格式。");return t.Hour*60+t.Minute;}
    static void Pair(Panel p,string left,Control first,string right,Control second){var grid=new Grid();grid.ColumnDefinitions.Add(new());grid.ColumnDefinitions.Add(new());var a=new StackPanel{Margin=new(0,0,10,0)};var b=new StackPanel{Margin=new(10,0,0,0)};Ui.Field(a,left,first);Ui.Field(b,right,second);Grid.SetColumn(b,1);grid.Children.Add(a);grid.Children.Add(b);p.Children.Add(grid);}
    async Task Generate()
    {
        cancel.Token.ThrowIfCancellationRequested();var prepared=new List<PreparedTrip>();
        foreach(var c in cards)
        {
            var r=JsonKit.Clone(c.Original);r.Title=c.Title.Text.Trim();r.Day=c.Day.SelectedDate is {} d?DateOnly.FromDateTime(d):null;r.Start=Minute(c.Begin.Text);r.End=Minute(c.End.Text);r.LatestEnd=Minute(c.Deadline.Text);r.Location=c.Query.Text.Trim();r.Notes=c.Note.Text.Trim();r.Duration=int.TryParse(c.Duration.Text,out var duration)?duration:null;r.ModeIn=c.Mode.SelectedIndex>0?c.Mode.Text:null;
            if(r.Start is {} begin&&r.End is {} end){if(end<begin)throw new ArgumentException("结束时间必须晚于开始时间。");r.Duration=end-begin;}
            RepositoryPlanner.Validate(r);var place=c.Chosen??throw new ArgumentException($"请为“{r.Title}”搜索并选择准确地点。");r.Location=place.Name;prepared.Add(new(r,place));
        }
        status.Text="正在核对路线与时间…";var snapshot=vm.State.Copy();var cache=new Dictionary<string,TravelEstimate>();
        var plan=await RepositoryPlanner.PlanAsync(snapshot,prepared,async(from,to,mode,depart,token)=>{string key=$"{from.Id}/{to.Id}/{mode}/{depart:O}";if(cache.TryGetValue(key,out var known))return known;var found=await RepositoryDialogs.Travel(this,map,from,to,mode,depart,token);cache[key]=found;return found;},cancel.Token);
        cancel.Token.ThrowIfCancellationRequested();string summary=string.Join("\n\n",plan.Rows.Select(r=>$"{r.Trip.Request.Day:yyyy-MM-dd} {ClockText(r.Start)}—{ClockText(r.Start+r.Trip.Request.Duration)}\n{r.Trip.Request.Title} · {r.Trip.Place.Name}\n{r.Trip.Request.Notes}\n前往：{r.Before.Minutes}分钟（{r.Before.Source}）；后续：{r.After.Minutes}分钟（{r.After.Source}）"));
        if(!Ui.ConfirmReview("你的行程\n核对地点、时间和路程后，一次保存全部活动。\n\n"+summary)){status.Text="已返回修改，原行程未变。";return;}
        cancel.Token.ThrowIfCancellationRequested();var tableId=(string)((ComboBoxItem)table.SelectedItem).Tag;var commands=RepositoryPlanner.Commands(plan,tableId);vm.Persist(ChangeService.Apply(vm.State,commands,plan.Revision));vm.FocusDate(prepared[0].Request.Day!.Value);Close();
    }
}
