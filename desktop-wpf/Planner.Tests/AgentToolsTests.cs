using Planner.Core;
using Planner.Data;
using System.Text.Json;
using System.Net;
using System.Text;
static class AgentToolsTests
{
 static void Equal<T>(T want,T got){if(!Equals(want,got))throw new Exception($"expected {want}, got {got}");}
 static void Reject(Action action){try{action();}catch(ArgumentException){return;}throw new Exception("expected rejection");}
 static HttpResponseMessage Response(string content)=>new(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new{choices=new[]{new{message=new{content}}}}),Encoding.UTF8,"application/json")};
 static object Run(AgentToolSession session,string name,string args)=>session.ExecuteAsync(new(){Name=name,Arguments=JsonDocument.Parse(args).RootElement.Clone()},default).GetAwaiter().GetResult();
 static string Text(object value)=>JsonSerializer.Serialize(value,new JsonSerializerOptions(JsonKit.Options){Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping});
 static ServiceSettings Config()=>new(){AgentProvider="CUSTOM",LlmEndpoint="http://localhost/v1",Model="fixture"};
 public static void Register(Action<string,Action> test)
 {
  test("memory patches require exact user provenance and replace facts",()=>{
   var c=new GuidedConversation();var u=c.AddUser("明天去玄武湖，停留30分钟");var m=new AgentMemory();
   m.Apply([new(){Key="activity.1.duration",Value="30",SourceMessageId=u.Id,Evidence="30分钟",Confirmed=true}],c.Active.Messages);
   Equal("30",m.Facts.Single().Value.Value);var v=c.AddUser("改成60分钟");m.Apply([new(){Key="activity.1.duration",Value="60",SourceMessageId=v.Id,Evidence="60分钟",Confirmed=true}],c.Active.Messages);Equal(1,m.Facts.Count);Equal("60",m.Facts.Single().Value.Value);
   Reject(()=>m.Apply([new(){Key="date",Value="fake",SourceMessageId="missing",Evidence="明天",Confirmed=true}],c.Active.Messages));
   c.AddAssistant(new(){Message="建议公交"});Reject(()=>m.Apply([new(){Key="mode",Value="公交",SourceMessageId=c.Active.Messages.Last().Id,Evidence="公交",Confirmed=true}],c.Active.Messages));
   m.Apply([new(){Key="suggested.mode",Value="公交",SourceMessageId=c.Active.Messages.Last().Id,Evidence="公交",Confirmed=false}],c.Active.Messages);Equal(false,m.Facts["suggested.mode"].Confirmed);
  });
  test("branch fork restores exact prefix memory and selected places",()=>{
   var c=new GuidedConversation();var first=c.AddUser("明天去玄武湖");var m=new AgentMemory();m.Apply([new(){Key="goal",Value="玄武湖",SourceMessageId=first.Id,Evidence="玄武湖",Confirmed=true}],c.Active.Messages);c.AddAssistant(new(){Message="选入口",Memory=m});
   var old=c.AddUser("然后去夫子庙");var future=JsonKit.Clone(m);future.Facts["future"]=new(){Value="夫子庙"};future.SelectedPlaces["p"]=new(){Name="未来地点"};c.AddAssistant(new(){Message="好",Memory=future});
   var branch=c.ForkAt(old.Id,"只去玄武湖");Equal(true,branch.Memory.Facts.ContainsKey("goal"));Equal(false,branch.Memory.Facts.ContainsKey("future"));Equal(0,branch.Memory.SelectedPlaces.Count);Equal(0,branch.Memory.Routes.Count);
  });
  test("memory snapshots survive store restart and new turns invalidate routes",()=>{
   var d=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString());try{var c=new GuidedConversation();c.AddUser("明天");var m=new AgentMemory();m.SelectedPlaces["p"]=new(){Name="入口",Lng=118.8,Lat=32.0};m.Routes.Add(new(){FromRef="p",ToRef="q",Minutes=35});c.AddAssistant(new(){Message="已查路线",Memory=m});var store=new ConversationStore(d);store.Save(c);var restored=store.Load();Equal(35,restored.Active.Memory.Routes.Single().Minutes);restored.AddUser("改到后天");Equal(0,restored.Active.Memory.Routes.Count);Equal("入口",restored.Active.Memory.SelectedPlaces["p"].Name);}finally{if(Directory.Exists(d))Directory.Delete(d,true);}
  });
  test("tools expose candidates but never let model confirm places and redact schedule notes",()=>{
   var state=new PlannerState();state.Tables.Add(new(){Id="t",Name="个人"});state.Tables.Add(new(){Id="off",Name="关闭",Enabled=false});state.Activities.Add(new(){Id="a",TableId="t",Title="约会",StartAt=new(2026,11,3,10,0,0),EndAt=new(2026,11,3,11,0,0),Note="private-note"});state.Activities.Add(new(){TableId="off",Title="hidden-event",StartAt=new(2026,11,3,10,0,0),EndAt=new(2026,11,3,11,0,0)});
   var memory=new AgentMemory();var session=new AgentToolSession(memory,state,new RepositoryMap(new()));var result=Text(Run(session,"search_places","{\"query\":\"玄武湖\"}"));Equal(true,result.Contains("未核实"));Equal(1,memory.Candidates.Count);Equal(0,memory.SelectedPlaces.Count);
   result=Text(Run(session,"query_route","{\"from_ref\":\"fake\",\"to_ref\":\"fake2\",\"mode\":\"公交\",\"depart_at\":\"2026-11-03T14:00:00+08:00\"}"));Equal(true,result.Contains("error"));
   result=Text(Run(session,"read_schedule","{\"from_day\":\"2026-11-03\",\"to_day\":\"2026-11-03\"}"));Equal(true,result.Contains("约会"));Equal(false,result.Contains("private-note"));Equal(false,result.Contains("hidden-event"));
   result=Text(Run(session,"read_schedule","{\"from_day\":\"2026-01-01\",\"to_day\":\"2026-12-31\"}"));Equal(true,result.Contains("error"));
  });
  test("tool loop reinjects real tool results and structured memory into model context",()=>{
   var c=new GuidedConversation();c.AddUser("明天去玄武湖");int calls=0;using var http=new HttpClient(new FixtureHandler(async request=>{
    using var body=JsonDocument.Parse(await request.Content!.ReadAsStringAsync());string all=body.RootElement.GetProperty("messages").GetRawText();calls++;
    Equal(true,all.Contains("CURRENT_MEMORY"));Equal(true,all.Contains(c.Active.Messages[0].Id));
    if(calls==1)return Response("{\"tool_requests\":[{\"name\":\"search_places\",\"arguments\":{\"query\":\"玄武湖\"}}]}");
    Equal(true,all.Contains("TOOL_RESULTS"));Equal(true,all.Contains("query_at"));return Response("{\"message\":\"请选择入口\",\"summary\":\"玄武湖\",\"ready\":false,\"missing\":[\"地点确认\"],\"events\":[]}");
   }));var reply=new GuidedAgentClient(http).ReplyWithToolsAsync(Config(),c.Active.Messages,new(),new PlannerState(),null,default).GetAwaiter().GetResult();Equal(2,calls);Equal(1,reply.Memory!.Candidates.Count);Equal(false,reply.Ready);Equal(0,c.Active.Memory.Candidates.Count);
  });
  test("model invented place refs cannot enable draft and tool loops are bounded",()=>{
   var c=new GuidedConversation();c.AddUser("明天去玄武湖");using var http=new HttpClient(new FixtureHandler(_=>Task.FromResult(Response("{\"message\":\"完成\",\"summary\":\"测试\",\"missing\":[],\"ready\":true,\"events\":[{\"day\":\"2026-11-03\",\"title\":\"游玩\",\"location\":\"玄武湖\",\"duration\":30,\"place_ref\":\"invented\"}]}"))));var reply=new GuidedAgentClient(http).ReplyWithToolsAsync(Config(),c.Active.Messages,new(),new(),null,default).GetAwaiter().GetResult();Equal(false,reply.Ready);Equal(true,reply.Missing.Count>0);
   int rounds=0;using var looping=new HttpClient(new FixtureHandler(_=>{rounds++;return Task.FromResult(Response("{\"tool_requests\":[{\"name\":\"search_places\",\"arguments\":{\"query\":\"玄武湖\"}}]}"));}));reply=new GuidedAgentClient(looping).ReplyWithToolsAsync(Config(),c.Active.Messages,new(),new(),null,default).GetAwaiter().GetResult();Equal(true,rounds<=6);Equal(false,reply.Ready);
  });
  test("tool routes use confirmed places only and cancellation leaves original memory unchanged",()=>{
   var memory=new AgentMemory();memory.SelectedPlaces["a"]=new(){Id="a",Name="起点",Lng=118.8,Lat=32.0};memory.SelectedPlaces["b"]=new(){Id="b",Name="终点",Lng=118.81,Lat=32.01};var session=new AgentToolSession(memory,new(),new RepositoryMap(new()));string result=Text(Run(session,"query_route","{\"from_ref\":\"a\",\"to_ref\":\"b\",\"mode\":\"公交\",\"depart_at\":\"2026-11-03T14:00:00+08:00\"}"));Equal(true,result.Contains("粗估"));Equal(1,memory.Routes.Count);Equal(null,memory.Routes[0].KnownCostFen);
   using var cancelled=new CancellationTokenSource();cancelled.Cancel();try{new GuidedAgentClient().ReplyWithToolsAsync(Config(),[new(){Content="测试"}],memory,new(),null,cancelled.Token).GetAwaiter().GetResult();throw new Exception("not cancelled");}catch(OperationCanceledException){}Equal(1,memory.Routes.Count);
  });

  test("ready plan cannot bypass relevant schedule or actual transfer queries",()=>{
   var memory=new AgentMemory();memory.SelectedPlaces["a"]=new(){Id="a",Name="玄武湖",Lng=118.8,Lat=32.0};memory.SelectedPlaces["b"]=new(){Id="b",Name="夫子庙",Lng=118.81,Lat=32.01};
   string ready="{\"message\":\"完成\",\"summary\":\"已安排\",\"ready\":true,\"missing\":[],\"events\":[{\"day\":\"2026-11-03\",\"title\":\"游玩\",\"location\":\"玄武湖\",\"duration\":240,\"start\":600,\"place_ref\":\"a\"},{\"day\":\"2026-11-03\",\"title\":\"游览\",\"location\":\"夫子庙\",\"duration\":30,\"mode_in\":\"公交\",\"place_ref\":\"b\"}]}";
   using var http=new HttpClient(new FixtureHandler(_=>Task.FromResult(Response(ready))));var reply=new GuidedAgentClient(http).ReplyWithToolsAsync(Config(),[new(){Content="明天两个地方"}],memory,new(),null,default).GetAwaiter().GetResult();Equal(false,reply.Ready);Equal(true,reply.Missing.Count>0);
  });

  test("read schedule supplies stored occurrence endpoint references without exposing private notes",()=>{
   var state=new PlannerState();state.Tables.Add(new(){Id="t"});state.Places.Add(new(){Id="stored",Name="既有地点",Lng=118.8,Lat=32.0});state.Activities.Add(new(){TableId="t",Title="跨日固定事项",PlaceId="stored",StartAt=new(2026,11,2,23,0,0),EndAt=new(2026,11,3,1,0,0),Note="secret"});var memory=new AgentMemory();var session=new AgentToolSession(memory,state,new RepositoryMap(new()));var result=Text(Run(session,"read_schedule","{\"from_day\":\"2026-11-03\",\"to_day\":\"2026-11-03\"}"));Equal(true,result.Contains("跨日固定事项"));Equal(false,result.Contains("secret"));Equal(true,memory.SelectedPlaces.ContainsKey("stored"));Equal("stored",memory.ScheduleReads.Single().Events.Single().PlaceRef);
  });
  test("unconfirmed suggestion cannot overwrite confirmed requirement",()=>{
   var m=new AgentMemory();var messages=new[]{new ConversationMessage{Id="u",Content="公交"},new ConversationMessage{Id="a",Role="assistant",Content="建议步行"}};m.Apply([new(){Key="mode",Value="公交",Confirmed=true,SourceMessageId="u",Evidence="公交"}],messages);Reject(()=>m.Apply([new(){Key="mode",Value="步行",Confirmed=false,SourceMessageId="a",Evidence="步行"}],messages));Equal("公交",m.Facts["mode"].Value);
  });

  test("partial follow up preserves earlier draft as stale memory instead of dropping it",()=>{
   var c=new GuidedConversation();c.AddUser("明天玄武湖2小时");var m=new AgentMemory{Draft=[new(){Title="玄武湖",Day=new(2026,11,3),Location="玄武湖",Duration=120}]};c.AddAssistant(new(){Message="已记录",Memory=m});c.AddUser("不满意");using var http=new HttpClient(new FixtureHandler(_=>Task.FromResult(Response("{\"message\":\"想改哪部分？\",\"ready\":false,\"missing\":[\"修改方向\"],\"events\":[]}"))));var reply=new GuidedAgentClient(http).ReplyWithToolsAsync(Config(),c.Active.Messages,c.Active.Memory,new(),null,default).GetAwaiter().GetResult();Equal(1,reply.Memory!.Draft.Count);Equal(false,reply.Ready);
  });
  test("failed route query returns explicit error and does not fabricate a route",()=>{
   var m=new AgentMemory();var result=Text(Run(new AgentToolSession(m,new(),new RepositoryMap(new())),"query_route","{\"from_ref\":\"a\",\"to_ref\":\"b\",\"mode\":\"公交\",\"depart_at\":\"bad\"}"));Equal(true,result.Contains("error"));Equal(0,m.Routes.Count);
  });

  test("agent tool executes actual Amap HTTP parsing and returns measured route snapshot",()=>{
   int count=0;using var http=new HttpClient(new FixtureHandler(request=>{count++;string path=request.RequestUri!.AbsolutePath;string json=path.Contains("district")?"{\"status\":\"1\",\"districts\":[{\"polyline\":\"118,31;119,31;119,33;118,33;118,31\"}]}":path.Contains("place/text")?"{\"status\":\"1\",\"pois\":[{\"id\":\"fixture-poi\",\"name\":\"南京入口\",\"address\":\"测试地址\",\"location\":\"118.8,32\",\"adcode\":\"320102\"}]}":"{\"status\":\"1\",\"route\":{\"paths\":[{\"distance\":\"1200\",\"cost\":{\"duration\":\"2100\"},\"steps\":[{\"instruction\":\"测试路线\",\"polyline\":\"118.8,32;118.81,32.01\"}]}]}}";return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(json)});}));
   var memory=new AgentMemory();var map=new RepositoryMap(new(),provider:new AmapClient("fixture-map-key",http));var tool=new AgentToolSession(memory,new(),map);string result=Text(Run(tool,"search_places","{\"query\":\"南京入口\"}"));Equal(true,result.Contains("高德"));var place=memory.Candidates.Single().Places.Single();memory.SelectedPlaces[place.Id]=place;memory.SelectedPlaces["b"]=new(){Id="b",Name="终点",Lng=118.81,Lat=32.01};result=Text(Run(tool,"query_route",JsonSerializer.Serialize(new{from_ref=place.Id,to_ref="b",mode="步行",depart_at="2026-11-03T14:00:00+08:00"})));Equal(true,result.Contains("高德查询快照"));Equal(35,memory.Routes.Single().Minutes);Equal(0,memory.Routes.Single().KnownCostFen);Equal(3,count);Equal(false,result.Contains("fixture-map-key"));
  });

  test("review confirmed memory cannot change a value while citing genuine user text",()=>{
   var m=new AgentMemory();var user=new ConversationMessage{Content="明天去玄武湖，停留30分钟",CreatedAt=new(2026,11,2,10,0,0)};
   Reject(()=>m.Apply([new(){Key="activity.1.duration",Value="120",SourceMessageId=user.Id,Evidence="30分钟",Confirmed=true}],[user]));
   Reject(()=>m.Apply([new(){Key="trip.date",Value="2030-01-01",SourceMessageId=user.Id,Evidence="明天",Confirmed=true}],[user]));
   m.Apply([new(){Key="trip.date",Value="2026-11-03",SourceMessageId=user.Id,Evidence="明天",Confirmed=true}],[user]);Equal("2026-11-03",m.Facts["trip.date"].Value);
  });
  test("review failed oversized tool result is atomic and cannot mark schedule checked",()=>{
   var state=new PlannerState();state.Tables.Add(new(){Id="t"});for(int i=0;i<70;i++)state.Activities.Add(new(){TableId="t",Title=new string('长',100),StartAt=new(2026,11,3,10,0,0),EndAt=new(2026,11,3,11,0,0)});var memory=new AgentMemory();var result=Text(Run(new AgentToolSession(memory,state,new RepositoryMap(new())),"read_schedule","{\"from_day\":\"2026-11-03\",\"to_day\":\"2026-11-03\"}"));Equal(true,result.Contains("error"));Equal(0,memory.ScheduleReads.Count);Equal(0,memory.SelectedPlaces.Count);
  });
  test("review readiness covers all overnight occupied days",()=>{
   var memory=new AgentMemory();memory.SelectedPlaces["a"]=new(){Id="a",Name="玄武湖"};memory.SelectedPlaces["b"]=new(){Id="b",Name="夫子庙"};memory.ScheduleReads.Add(new(){FromDay=new(2026,11,3),ToDay=new(2026,11,3)});
   string overnight="{\"message\":\"完成\",\"summary\":\"跨日\",\"ready\":true,\"missing\":[],\"events\":[{\"day\":\"2026-11-03\",\"end_day\":\"2026-11-04\",\"title\":\"跨日活动\",\"location\":\"玄武湖\",\"duration\":180,\"start\":1380,\"end\":120,\"place_ref\":\"a\"}]}";
   using var http=new HttpClient(new FixtureHandler(_=>Task.FromResult(Response(overnight))));var reply=new GuidedAgentClient(http).ReplyWithToolsAsync(Config(),[new(){Content="跨日安排"}],memory,new(),null,default).GetAwaiter().GetResult();Equal(false,reply.Ready);
  });
  test("review automatic route cannot reuse arbitrary departure on same day",()=>{
   var memory=new AgentMemory();memory.SelectedPlaces["a"]=new(){Id="a",Name="玄武湖"};memory.SelectedPlaces["b"]=new(){Id="b",Name="夫子庙"};memory.ScheduleReads.Add(new(){FromDay=new(2026,11,3),ToDay=new(2026,11,3)});
   memory.Routes.Add(new(){FromRef="a",ToRef="b",Mode="公交",DepartAt=new(2026,11,3,8,0,0),Minutes=35});string automatic="{\"message\":\"完成\",\"summary\":\"下午活动\",\"ready\":true,\"missing\":[],\"events\":[{\"day\":\"2026-11-03\",\"title\":\"游玩\",\"location\":\"玄武湖\",\"duration\":120,\"earliest\":840,\"place_ref\":\"a\"},{\"day\":\"2026-11-03\",\"title\":\"游览\",\"location\":\"夫子庙\",\"duration\":30,\"mode_in\":\"公交\",\"place_ref\":\"b\"}]}";
   using var http2=new HttpClient(new FixtureHandler(_=>Task.FromResult(Response(automatic))));var reply=new GuidedAgentClient(http2).ReplyWithToolsAsync(Config(),[new(){Content="下午2点后安排"}],memory,new(),null,default).GetAwaiter().GetResult();Equal(false,reply.Ready);
  });

 }
}
