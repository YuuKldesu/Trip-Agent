using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Planner.Core;
using Planner.Data;

namespace Planner.Desktop;
public partial class MainWindow
{
    partial void OpenRoutes()=>new RouteWindow(vm){Owner=this}.ShowDialog();
}
public sealed class RouteWindow:Window
{
    readonly PlannerViewModel vm;readonly DatePicker date;readonly TextBox originText=Ui.Input(),earliest=Ui.Input("08:00");
    readonly ComboBox originList=new(){MinWidth=290},bike=Ui.Choices(["共享单车（可用性及费用未知）","自有自行车（初始在出发点）"]);
    readonly TextBox parking=Ui.Input("5");readonly CheckBox replan=new(){Content="重排当前表内的灵活事项（保留其他表和固定事项）"};
    readonly Dictionary<string,CheckBox> modes=[];readonly StackPanel results=new();readonly TextBlock message=Ui.Text("先确认出发点，再生成当天路线。所有生效表均参与检查。",12);
    readonly Grid mapHost=new();readonly WebView2 map=new();CancellationTokenSource? cancel;PlannerState? draftState;PlanningRequest? draftRequest;long revision;bool mapReady;List<ItineraryCandidate> candidates=[];
    readonly Button generate;readonly ServiceSettings config;readonly AmapClient provider;
    public RouteWindow(PlannerViewModel model)
    {
        vm=model;config=new SettingsStore(vm.DataDirectory).Load();provider=new(SettingsStore.Unprotect(config.MapServiceKeyProtected));
        Title="当天规划 · 地图与路线";Width=1320;Height=900;MinWidth=1040;MinHeight=700;WindowStartupLocation=WindowStartupLocation.CenterOwner;Background=Ui.Brush("#FBF7F0");FontFamily=new("Microsoft YaHei UI");FontSize=13;
        var root=new Grid{Margin=new(18)};root.ColumnDefinitions.Add(new(){Width=new(390)});root.ColumnDefinitions.Add(new());Content=root;
        var left=new StackPanel{Margin=new(0,0,18,0)};root.Children.Add(new ScrollViewer{Content=left,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
        left.Children.Add(Ui.Text("把这一天串起来",23,true));date=new(){SelectedDate=vm.SelectedDate.ToDateTime(TimeOnly.MinValue)};Ui.Field(left,"规划日期",date);
        Ui.Field(left,"出发点（须选择搜索结果）",originText);left.Children.Add(Ui.Button("搜索南京市内地点",()=>SearchOrigin()));left.Children.Add(originList);Ui.Field(left,"最早出发时间 HH:mm",earliest);
        left.Children.Add(Ui.Text("交通方式（可混合）",14,true));var row=new WrapPanel();foreach(var mode in new[]{"WALK","BIKE","BUS","METRO","CAR","TAXI"}){var box=new CheckBox{Content=AmapClient.Name(mode),IsChecked=mode is "WALK" or "BUS" or "METRO",Margin=new(0,4,15,8)};modes[mode]=box;row.Children.Add(box);}left.Children.Add(row);
        left.Children.Add(Ui.Text("公交与地铁包含必要的接驳步行；“步行”勾选项允许另选全程步行方案。",11));
        Ui.Field(left,"骑行资源",bike);Ui.Field(left,"自驾停车缓冲（分钟）",parking);left.Children.Add(Ui.Text("选择自驾或自有自行车，表示车辆初始在出发点。离开车辆后不能在异地直接取车。",11));left.Children.Add(replan);
        generate=Ui.Button("生成可选方案",()=>Generate(),true);left.Children.Add(generate);left.Children.Add(Ui.Button("取消当前查询",()=>cancel?.Cancel()));left.Children.Add(message);left.Children.Add(results);
        var right=new Grid();Grid.SetColumn(right,1);root.Children.Add(right);right.RowDefinitions.Add(new(){Height=new(50)});right.RowDefinitions.Add(new());right.RowDefinitions.Add(new(){Height=new(70)});
        right.Children.Add(Ui.Text("南京 · 当天路线预览",20,true));Grid.SetRow(mapHost,1);right.Children.Add(mapHost);
        var note=Ui.Text("交通时间是本次查询参考，不实时监测；拥堵仅展示接口返回数据，乘客拥挤程度暂无数据。候选搜索最多12项、100次查询，未找到方案不等于不存在。",12);Grid.SetRow(note,2);right.Children.Add(note);
        Loaded+=async(_,_)=>{await InitializeMap();ShowSaved();};Closed+=(_,_)=>{cancel?.Cancel();map.Dispose();};
        date.SelectedDateChanged+=(_,_)=>{InvalidateDraft();ShowSaved();};originList.SelectionChanged+=(_,_)=>InvalidateDraft();earliest.TextChanged+=(_,_)=>InvalidateDraft();parking.TextChanged+=(_,_)=>InvalidateDraft();bike.SelectionChanged+=(_,_)=>InvalidateDraft();replan.Click+=(_,_)=>InvalidateDraft();foreach(var box in modes.Values)box.Click+=(_,_)=>InvalidateDraft();
    }
    void InvalidateDraft(){cancel?.Cancel();draftState=null;draftRequest=null;candidates=[];results.Children.Clear();message.Text="规划条件已改变，请生成新的方案。";}
    async void SearchOrigin()=>await Run(async token=>{var list=await provider.SearchPlacesAsync(originText.Text.Trim(),token);originList.ItemsSource=list;originList.SelectedIndex=list.Count==1?0:-1;message.Text=list.Count==0?"未找到市内地点，请使用更具体的名称。":"请选择准确的出发地点。";});
    async Task Run(Func<CancellationToken,Task> action)
    {
        if(cancel is not null)return;cancel=new();generate.IsEnabled=false;
        try{await action(cancel.Token);}catch(OperationCanceledException){message.Text="查询已取消，尚未保存。";}catch(Exception ex){message.Text=ex.Message;}finally{cancel?.Dispose();cancel=null;generate.IsEnabled=true;}
    }
    async void Generate()=>await Run(async token=>
    {
        if(date.SelectedDate is not {} d||!TimeOnly.TryParseExact(earliest.Text.Trim(),"HH:mm",out var time))throw new ArgumentException("请选择日期并填写合法的出发时间。");
        if(originList.SelectedItem is not Place origin)throw new ArgumentException("请搜索并选择出发点。");
        if(!int.TryParse(parking.Text,out var mins)||mins<0||mins>60)throw new ArgumentException("停车缓冲应为0至60分钟。");
        revision=vm.State.Revision;var state=vm.State.Copy();var day=DateOnly.FromDateTime(d);var items=CalendarEngine.Expand(state,d.Date,d.Date.AddDays(1));
        if(items.Count==0)throw new ArgumentException("当天没有生效事项，请先手动填表或使用智能填表。");
        foreach(var placeId in items.Select(x=>x.PlaceId).Distinct())
        {
            var place=state.Places.FirstOrDefault(x=>x.Id==placeId);if(place is null)throw new ArgumentException("部分事项未填地点，请先编辑补充。");
            if(place.Status=="VERIFIED")continue;
            message.Text="正在确认地点："+place.Name;
            var matches=await provider.SearchPlacesAsync(place.Name,token);var choice=ChoosePlace(place.Name,matches);if(choice is null)throw new OperationCanceledException();
            choice.Id=place.Id;state.Places[state.Places.IndexOf(place)]=choice;
        }
        var request=new PlanningRequest{Date=day,Origin=origin,Earliest=day.ToDateTime(time),Modes=modes.Where(x=>x.Value.IsChecked==true).Select(x=>x.Key).ToList(),ReplanFlexible=replan.IsChecked==true,EditableTables=[vm.SelectedTableId],BikeResource=bike.SelectedIndex==1?"OWN_BIKE":"SHARED_BIKE",ParkingMinutes=mins};
        message.Text="正在比较路线与时间窗，可取消；尚未写入行程。";results.Children.Clear();candidates=await new ItineraryPlanner(provider).GenerateAsync(state,request,token);token.ThrowIfCancellationRequested();
        if(vm.State.Revision!=revision)throw new InvalidOperationException("查询期间行程已发生变化，请重新生成。");
        draftState=state;draftRequest=request;message.Text=candidates.Count==0?"当前搜索范围内未找到可行方案。可尝试提前出发、放宽灵活时间窗或增加交通方式。":$"找到 {candidates.Count} 个不同方案，点击预览，再确认填入行程。";
        if(request.Warnings.Count>0)message.Text+="\n"+string.Join("\n",request.Warnings.Distinct().Take(5));
        foreach(var candidate in candidates)AddCandidate(candidate);if(candidates.Count>0)await Preview(candidates[0],request.Origin);
    });
    Place? ChoosePlace(string name,List<Place> list)
    {
        if(list.Count==0)throw new ArgumentException($"“{name}”未找到市内地点，请先修改名称或服务配置。");
        var w=new Window{Title="请确认地点 · "+name,Width=620,Height=420,Owner=this,WindowStartupLocation=WindowStartupLocation.CenterOwner};var p=new DockPanel{Margin=new(18)};var choices=new ListBox{ItemsSource=list};Place? selected=null;
        var ok=Ui.Button("使用选中的地点",()=>{if(choices.SelectedItem is not Place choice)throw new ArgumentException("请选择一个地点。");selected=JsonKit.Clone(choice);w.Close();},true);DockPanel.SetDock(ok,Dock.Bottom);p.Children.Add(ok);p.Children.Add(choices);w.Content=p;w.ShowDialog();return selected;
    }
    void AddCandidate(ItineraryCandidate candidate)
    {
        var p=new StackPanel();p.Children.Add(Ui.Text(candidate.Title,15,true));p.Children.Add(Ui.Text(candidate.Summary,12));
        for(int stopIndex=0;stopIndex<candidate.Stops.Count;stopIndex++)
        {
            var stop=candidate.Stops[stopIndex];var index=stopIndex;
            p.Children.Add(Ui.Text($"{stop.Depart:HH:mm} 出发 → {stop.Arrive:HH:mm} 到达\n{stop.Start:HH:mm}—{stop.End:HH:mm} {stop.Original.Title}\n{stop.Route.Label} · {stop.Route.CostText}\n{stop.Route.CostNote}",12));
            var congestion=string.Join("；",stop.Route.Segments.Select(x=>x.Congestion).Where(x=>!string.IsNullOrWhiteSpace(x)).Distinct());if(congestion.Length>0)p.Children.Add(Ui.Text("道路拥堵参考："+congestion,11));
            p.Children.Add(Ui.Button("更换此段交通",()=>ChangeLeg(candidate,index)));
        }
        p.Children.Add(Ui.Button("在地图预览",async()=>await Preview(candidate,draftRequest!.Origin)));
        p.Children.Add(Ui.Button("确认此方案并填入行程",()=>
        {
            if(draftState is null||draftRequest is null)throw new InvalidOperationException("请重新生成方案。");
            var next=PlanService.Confirm(vm.State,draftState,draftRequest,candidate,revision);
            if(!Ui.Confirm($"将保存 {draftRequest.Date:yyyy-MM-dd} 的路线方案。\n{candidate.Summary}\n灵活事项的变动将同时保存。交通数据仅作本次查询参考。\n\n确认？"))return;
            vm.Persist(next);draftState=null;message.Text="方案已保存；相关活动改动后需要重新规划。";results.Children.Clear();ShowSaved();
        },true));results.Children.Add(Ui.Card(p));
    }
    async void ChangeLeg(ItineraryCandidate candidate,int index)=>await Run(async token=>
    {
        if(draftState is null||draftRequest is null)throw new ArgumentException("请先重新生成方案。");
        var state=draftState;var request=draftRequest;var stop=candidate.Stops[index];var from=index==0?request.Origin:state.Places.Single(p=>p.Id==candidate.Stops[index-1].Original.PlaceId);var to=state.Places.Single(p=>p.Id==stop.Original.PlaceId);
        var choices=new List<RouteOption>();foreach(var mode in TransportPolicy.QueryModes(request.Modes))
        {
            try{choices.AddRange(await provider.RoutesAsync(from,to,mode,stop.Depart,token));}catch(InvalidOperationException ex){message.Text=ex.Message;}
        }
        choices=choices.Where(x=>TransportPolicy.Allows(x,request.Modes)).ToList();if(choices.Count==0)throw new ArgumentException("此段未返回可用的其他路线。");
        var w=new Window{Title="更换交通 · "+stop.Original.Title,Width=710,Height=450,Owner=this,WindowStartupLocation=WindowStartupLocation.CenterOwner};var panel=new DockPanel{Margin=new(18)};var list=new ListBox{ItemsSource=choices};RouteOption? selected=null;
        var choose=Ui.Button("使用所选路线并检查衔接",()=>
        {
            if(list.SelectedItem is not RouteOption route)throw new ArgumentException("请选择路线。");var copy=JsonKit.Clone(route);foreach(var segment in copy.Segments.Where(x=>x.Mode=="BIKE"))segment.ResourceType=request.BikeResource;
            if(copy.Uses("BIKE")&&request.BikeResource=="OWN_BIKE"){copy.KnownCostFen=0;copy.CostComplete=true;copy.CostNote="自有自行车，不计购置维护费用";}
            int buffer=copy.Uses("CAR")?request.ParkingMinutes*60:0;var altered=new ItineraryCandidate{Title=candidate.Title+" · 自选交通",Stops=[..candidate.Stops]};altered.Stops[index]=stop with{Route=copy,BufferSec=buffer,Arrive=stop.Depart.AddSeconds(copy.DurationSec+buffer)};
            PlanService.ValidateResources(state,request,altered);selected=copy;candidate.Stops=altered.Stops;w.Close();
        },true);DockPanel.SetDock(choose,Dock.Bottom);panel.Children.Add(choose);panel.Children.Add(list);w.Content=panel;w.ShowDialog();
        if(selected is null)return;results.Children.Clear();foreach(var option in candidates)AddCandidate(option);await Preview(candidate,request.Origin);message.Text="已替换此段并复核整日衔接，确认方案后才保存。";
    });
    void ShowSaved()
    {
        if(date.SelectedDate is not {} d)return;var plan=vm.State.Plans.FirstOrDefault(x=>x.PlanDate==DateOnly.FromDateTime(d));if(plan is null)return;
        var p=new StackPanel();p.Children.Add(Ui.Text("已保存方案 · "+(plan.Status=="CONFIRMED"?"已确认":"需重新规划"),15,true));var legs=vm.State.Legs.Where(x=>x.PlanId==plan.Id).OrderBy(x=>x.Seq).ToList();
        foreach(var leg in legs)
        {
            var a=vm.State.Activities.FirstOrDefault(x=>x.Id==leg.ToActivityId);var parts=vm.State.Segments.Where(x=>x.LegId==leg.Id).OrderBy(x=>x.Seq).ToList();
            var fee=leg.KnownCostFen is {} amount?$"已知 ¥{amount/100.0:0.##}"+(leg.CostComplete?"":" + 未知项"):"费用暂无数据";
            p.Children.Add(Ui.Text($"{leg.DepartAt:HH:mm} → {leg.ArriveAt:HH:mm}  {a?.Title}\n"+string.Join(" + ",parts.Select(x=>AmapClient.Name(x.Mode)))+$"\n{fee} · {leg.CostNote}\n查询 {leg.QueryAt:MM-dd HH:mm} · {(leg.ValidationStatus=="VALID"?"已核验":"需重新核验")}",12));
        }
        results.Children.Add(Ui.Card(p));
        if(mapReady)_=map.ExecuteScriptAsync("renderPlan("+JsonSerializer.Serialize(new{origin=vm.State.Places.FirstOrDefault(x=>x.Id==plan.StartPlaceId),segments=legs.SelectMany(l=>vm.State.Segments.Where(x=>x.LegId==l.Id)).ToList(),stops=legs.Select(l=>vm.State.Places.FirstOrDefault(p=>p.Id==CalendarEngine.Expand(vm.State,d.Date,d.Date.AddDays(1)).FirstOrDefault(o=>o.Activity.Id==l.ToActivityId&&o.OriginDate==l.ToOriginDate)?.PlaceId))},JsonKit.Options)+")");
    }
    async Task InitializeMap()
    {
        try
        {
            var js=SettingsStore.Unprotect(config.MapJsKeyProtected);var security=SettingsStore.Unprotect(config.MapSecurityProtected);
            if(js.Length==0||security.Length==0){mapHost.Children.Add(Ui.Card(Ui.Text("地图尚未配置\n\n在“服务设置”填写高德Web端JS Key和安全密钥后，可在此查看真实底图与路线。文字方案查询使用Web服务Key。",16)));return;}
            mapHost.Children.Add(map);var env=await CoreWebView2Environment.CreateAsync(null,Path.Combine(vm.DataDirectory,"Browser"));await map.EnsureCoreWebView2Async(env);
            map.CoreWebView2.Settings.AreDevToolsEnabled=false;map.CoreWebView2.Settings.AreDefaultContextMenusEnabled=false;map.CoreWebView2.Settings.IsStatusBarEnabled=false;
            map.CoreWebView2.NewWindowRequested+=(_,e)=>e.Handled=true;map.CoreWebView2.PermissionRequested+=(_,e)=>e.State=CoreWebView2PermissionState.Deny;
            map.CoreWebView2.NavigationStarting+=(_,e)=>{if(!e.Uri.StartsWith("about:",StringComparison.OrdinalIgnoreCase))e.Cancel=true;};
            var html=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"MapPage.html")).Replace("__CONFIG__",JsonSerializer.Serialize(new{key=js,security}));
            var loaded=new TaskCompletionSource();map.CoreWebView2.NavigationCompleted+=(_,_)=>loaded.TrySetResult();map.NavigateToString(html);await loaded.Task.WaitAsync(TimeSpan.FromSeconds(15));mapReady=true;
        }
        catch(Exception){mapHost.Children.Clear();mapHost.Children.Add(Ui.Card(Ui.Text("内嵌地图未能启动。请安装Microsoft Edge WebView2运行时并检查地图配置；文字方案仍可使用。",15)));}
    }
    async Task Preview(ItineraryCandidate candidate,Place origin)
    {
        if(!mapReady)return;var state=draftState??vm.State;var payload=new{origin,segments=candidate.Stops.SelectMany(x=>x.Route.Segments),stops=candidate.Stops.Select(x=>state.Places.First(p=>p.Id==x.Original.PlaceId))};await map.ExecuteScriptAsync("renderPlan("+JsonSerializer.Serialize(payload,JsonKit.Options)+")");
    }
}
