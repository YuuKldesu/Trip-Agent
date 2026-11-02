using System.Net;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Planner.Core;
using Planner.Data;
using Planner.Desktop;
static class AgentToolChecks
{
 public static async Task Run(Window owner,string root,Action<bool,string> check)
 {
  string directory=Path.Combine(root,"agent-tool-flow");var vm=new PlannerViewModel(directory,false);new SettingsStore(directory).Save(new(){AgentProvider="CUSTOM",LlmEndpoint="http://localhost/v1",Model="fixture"});
  int calls=0;using var http=new HttpClient(new Handler(async request=>{
   calls++;using var body=JsonDocument.Parse(await request.Content!.ReadAsStringAsync());var messages=body.RootElement.GetProperty("messages");string prompt=messages[0].GetProperty("content").GetString()!;
   string encoded=prompt.Split("# CURRENT_MEMORY（只读快照）\n")[1].Split("\n# USER_MESSAGE_IDS")[0];var memory=JsonSerializer.Deserialize<AgentMemory>(encoded,JsonKit.Options)!;
   string response;
   if(memory.SelectedPlaces.Count<2)
   {
    if(memory.Candidates.Count==0&&messages.GetArrayLength()<4)response="{\"tool_requests\":[{\"name\":\"search_places\",\"arguments\":{\"query\":\"玄武湖\"}},{\"name\":\"search_places\",\"arguments\":{\"query\":\"夫子庙\"}}]}";
    else response="{\"message\":\"请选择地点入口，我会计算衔接。\",\"summary\":\"10点到14点玄武湖，之后夫子庙30分钟，公交\",\"missing\":[\"地点确认\"],\"ready\":false,\"events\":[]}";
   }
   else
   {
    var from=memory.SelectedPlaces.Values.Single(p=>p.Name=="玄武湖");var to=memory.SelectedPlaces.Values.Single(p=>p.Name=="夫子庙");
    if(!messages.EnumerateArray().Any(m=>(m.GetProperty("content").GetString()??"").StartsWith("TOOL_RESULTS")))response=JsonSerializer.Serialize(new{tool_requests=new object[]{new{name="read_schedule",arguments=new{from_day="2026-11-03",to_day="2026-11-03"}},new{name="query_route",arguments=new{from_ref=from.Id,to_ref=to.Id,mode="公交",depart_at="2026-11-03T14:00:00+08:00"}}}});
    else response=JsonSerializer.Serialize(new{message="路线已计算，请核对后生成。",summary="玄武湖10—14点，然后公交到夫子庙停留30分钟。",missing=Array.Empty<string>(),ready=true,events=new object[]{new{day="2026-11-03",title="玄武湖游玩",location="玄武湖",duration=240,start=600,place_ref=from.Id},new{day="2026-11-03",title="夫子庙游览",location="夫子庙",duration=30,mode_in="公交",place_ref=to.Id}}});
   }
   return new(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new{choices=new[]{new{message=new{content=response}}}}),Encoding.UTF8,"application/json")};
  }));
  using var panel=new GuidedAssistantPanel(vm,owner,new GuidedAgentClient(http));var window=new Window{Content=panel,Owner=owner,Width=430,Height=800};window.Show();
  try
  {
   await panel.SendAsync("2026年11月3日10到14点玄武湖，之后公交去夫子庙半小时");
   check(panel.Conversation.Active.Memory.Candidates.Count==2,"助手真实工具往返后展示两个地点候选组");
   var candidate=Walk(panel).OfType<Button>().First(b=>b.Tag is string t&&t.StartsWith("AgentPlace:"));candidate.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
   for(int i=0;i<100&&panel.Conversation.Active.Messages.Last().Role!="assistant";i++)await Task.Delay(20);
   check(panel.Conversation.Active.Memory.SelectedPlaces.Count==1,"候选按钮把用户确认记录入当前分支");
   var remaining=panel.Conversation.Active.Memory.Candidates.SelectMany(c=>c.Places).Single();await panel.ConfirmPlaceAsync(remaining.Id);
   check(panel.Conversation.Active.Reply is {Ready:true},"已选地点、日程和路线查询后启用草稿");
   check(panel.Conversation.Active.Memory.Routes.Count==1&&panel.Conversation.Active.Memory.ScheduleReads.Count==1,"实际路线与日程结果持久化到分支记忆");
   check(vm.State.Activities.Count==0,"工具查询和需求确认不提前写入活动数据库");
   var route=panel.Conversation.Active.Memory.Routes.Single();var review=panel.OpenReview();await Task.Delay(250);
   var searches=Walk(review).OfType<PlaceSearchBox>().ToArray();check(searches.Length==2&&searches.All(s=>s.SelectedPlace is not null),"对话中确认的地点原样传入核对页不重复要求选择");
   var begins=Walk(review).OfType<TimeEntry>().ToArray();check(begins[2].Text==new TimeOnly(14,0).AddMinutes(route.Minutes).ToString("HH:mm")&&begins[3].Text==new TimeOnly(14,30).AddMinutes(route.Minutes).ToString("HH:mm"),"核对页按14点加实际路程再加30分钟填写后续时段");
   var accept=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(25)};accept.Tick+=(_,_)=>{foreach(var dialog in Application.Current.Windows.OfType<Window>().Where(w=>w.Title=="核对变更与实际影响").ToArray())Walk(dialog).OfType<Button>().Single(b=>Equals(b.Content,"确认写入")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));};
   accept.Start();try{Walk(review).OfType<Button>().Single(b=>Equals(b.Content,"生成行程 →")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));for(int i=0;i<100&&vm.State.Activities.Count==0;i++)await Task.Delay(25);}finally{accept.Stop();}
   check(vm.State.Activities.Count==2&&vm.State.Legs.Count==1,"工具生成的两个活动和独立交通卡片原子保存");
   var batch=panel.Conversation.Batches.Single();panel.UndoBatch(batch.Id);check(vm.State.Activities.Count==0&&vm.State.Legs.Count==0,"工具对话生成批次可完整撤回活动及交通卡片");
   var loaded=new ConversationStore(directory).Load();check(loaded.Active.Memory.SelectedPlaces.Count==2&&loaded.Active.Memory.Routes.Count==1,"重启读取保留已选地点与路线记忆");
   string original=panel.Conversation.ActiveBranchId;var first=panel.Conversation.Active.Messages.First(m=>m.Role=="user");await panel.EditMessageAsync(first.Id,"改成只去玄武湖");check(panel.Conversation.ActiveBranchId!=original&&panel.Conversation.Active.Memory.SelectedPlaces.Count==0,"编辑较早消息的新分支不继承未来地点确认");
   panel.SelectBranch(original);check(panel.Conversation.Active.Memory.SelectedPlaces.Count==2,"旧分支仍可恢复其已选地点与草稿");check(Walk(panel).OfType<Expander>().Any(e=>Equals(e.Tag,"AgentMemory")),"对话中可以展开查看结构化行程记忆");
   check(calls<16,"工具对话查询次数有限且未触发真实网络");
   var slowVm=new PlannerViewModel(Path.Combine(root,"agent-tool-cancel"),false);new SettingsStore(slowVm.DataDirectory).Save(new(){AgentProvider="CUSTOM",LlmEndpoint="http://localhost/v1",Model="fixture"});var entered=new TaskCompletionSource();var release=new TaskCompletionSource();
   using var slowHttp=new HttpClient(new Handler(async _=>{entered.TrySetResult();await release.Task;return new(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new{choices=new[]{new{message=new{content="{\"message\":\"旧回复\",\"ready\":false,\"events\":[]}"}}}}))};}));
   using var slowPanel=new GuidedAssistantPanel(slowVm,owner,new GuidedAgentClient(slowHttp));var work=slowPanel.SendAsync("查一下明天");await entered.Task;string oldBranch=slowPanel.Conversation.ActiveBranchId;slowPanel.Conversation.NewBranch();string freshBranch=slowPanel.Conversation.ActiveBranchId;slowPanel.SelectBranch(oldBranch);slowPanel.SelectBranch(freshBranch);release.SetResult();await work;
   check(slowPanel.Conversation.Active.Messages.Count==0&&slowPanel.Conversation.Active.Memory.SelectedPlaces.Count==0,"取消并切换分支后旧工具回包不污染新分支");
  }
  finally{window.Close();}
 }
 static IEnumerable<DependencyObject> Walk(DependencyObject root){yield return root;foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())foreach(var item in Walk(child))yield return item;}
 sealed class Handler(Func<HttpRequestMessage,Task<HttpResponseMessage>> reply):HttpMessageHandler{protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken token)=>reply(r);}
}
