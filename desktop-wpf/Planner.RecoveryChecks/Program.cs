using System.Collections;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Net;
using Planner.Core;
using Planner.Data;

if(args.Length!=1)throw new ArgumentException("Pass the original v2026.11.02.7 package directory containing the DLLs.");
var context=new ReferenceContext(Path.GetFullPath(args[0]));
var originalCore=context.LoadFromAssemblyName(new AssemblyName("Planner.Core"));
var originalData=context.LoadFromAssemblyName(new AssemblyName("Planner.Data"));
var tests=new List<(string,Func<Task>)>();
void Test(string name,Action body)=>tests.Add((name,()=>{body();return Task.CompletedTask;}));
void Equal(object? expected,object? actual){if(!Equals(expected,actual))throw new Exception($"expected {expected}, got {actual}");}
string Json(object? value)=>JsonSerializer.Serialize(value);
string TypeName(Type t)=>t.IsGenericType?t.GetGenericTypeDefinition().FullName+"["+string.Join(",",t.GetGenericArguments().Select(TypeName))+"]":t.FullName??t.Name;
string Shape(Assembly assembly)=>string.Join("\n",assembly.GetExportedTypes().SelectMany(t=>
 new[]{"TYPE "+TypeName(t)}.Concat(t.GetMembers(BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly).Select(m=>m switch
 {
  MethodInfo method=>"METHOD "+TypeName(t)+" "+method.Name+" "+TypeName(method.ReturnType)+"("+string.Join(",",method.GetParameters().Select(p=>TypeName(p.ParameterType)))+")",
  ConstructorInfo ctor=>"CTOR "+TypeName(t)+"("+string.Join(",",ctor.GetParameters().Select(p=>TypeName(p.ParameterType)))+")",
  PropertyInfo p=>"PROP "+TypeName(t)+" "+p.Name+" "+TypeName(p.PropertyType),
  FieldInfo f=>"FIELD "+TypeName(t)+" "+f.Name+" "+TypeName(f.FieldType),
  _=>m.MemberType+" "+TypeName(t)+" "+m.Name
 }))).Order(StringComparer.Ordinal));
object? Static(Assembly assembly,string type,string method,params object?[] values)=>assembly.GetType(type)!.GetMethod(method)!.Invoke(null,values);

Test("Core public API matches original release",()=>Equal(Shape(originalCore),Shape(typeof(AgentMemory).Assembly)));
Test("Data public API matches original release",()=>Equal(Shape(originalData),Shape(typeof(GuidedAgentClient).Assembly)));
Test("tool and character prompt text match original release",()=>{
 Equal(originalData.GetType("Planner.Data.AgentPrompt")!.GetField("Rules")!.GetRawConstantValue(),AgentPrompt.Rules);
 foreach(var mode in new[]{"DEFAULT","KURISU","invalid"}){
  Equal(Static(originalData,"Planner.Data.AssistantPersonas","Prompt",mode),AssistantPersonas.Prompt(mode));
  Equal(Static(originalData,"Planner.Data.AssistantPersonas","Greeting",mode),AssistantPersonas.Greeting(mode));
 }
});
Test("theme palettes match original across all theme and accent combinations",()=>{
 foreach(var theme in ThemePalette.Themes)foreach(var accent in ThemePalette.Accents.Keys)
  Equal(Json(Static(originalCore,"Planner.Core.ThemePalette","Get",theme,accent)),Json(ThemePalette.Get(theme,accent)));
});
Test("literal place extraction matches original release",()=>{
 foreach(var text in new[]{"明天在南京博物院参观，约2小时","周末去南京大学仙林校区参观","从这里到那里坐公交要多久？","重新搜索南京图书馆","先去玄武湖，再去夫子庙","今天在新街口吃饭"})
  Equal(Json(Static(originalData,"Planner.Data.PlaceQueries","FromMessage",text)),Json(PlaceQueries.FromMessage(text)));
});
Test("batch close modes match original release",()=>{
 foreach(var mode in new[]{"CURRENT","OTHERS","RIGHT","ALL"})foreach(var anchor in new[]{"a","b","c"})
  Equal(Json(Static(originalCore,"Planner.Core.TabClosing","Targets",new List<string>{"a","b","c"},anchor,mode)),Json(TabClosing.Targets(new List<string>{"a","b","c"},anchor,mode)));
});
Test("background reply is scoped to original branch and rejects stale results",()=>{
 var c=new GuidedConversation();c.AddUser("a");string a=c.ActiveBranchId;long version=c.Active.Version;
 var b=c.NewBranch();c.AddUser("b");Equal(true,c.TryAddAssistant(a,version,new(){Message="reply a"}));Equal(b.Id,c.ActiveBranchId);Equal(1,b.Messages.Count);
 Equal(false,c.TryAddAssistant(a,version,new(){Message="stale"}));Equal("reply a",c.Branches.Single(x=>x.Id==a).Messages.Last().Content);
});
Test("request cancellation remains independent across conversations",()=>{
 var requests=new ConversationRequests();var a=requests.Start("a",1);var b=requests.Start("b",1);
 requests.Cancel("a");Equal(true,a.Cancellation.IsCancellationRequested);Equal(false,b.Cancellation.IsCancellationRequested);
 requests.Finish(a);requests.CancelAll();Equal(true,b.Cancellation.IsCancellationRequested);requests.Finish(b);
});
Test("closing last workspace page expands assistant",()=>{
 var layout=new WorkspaceLayout();layout.Show("settings");layout.Close("board");Equal(false,layout.AssistantOnly);layout.Close("settings");Equal(true,layout.AssistantOnly);layout.Show("board");Equal(false,layout.AssistantOnly);
});
Test("draft patch changes one activity while retaining other data",()=>{
 var memory=new AgentMemory{Draft=[new(){EventId="a",Title="first",Duration=60,Location="玄武湖"},new(){EventId="b",Title="second",Duration=90,Location="南京博物院"}]};
 var reply=new GuidedReply{DraftAction="patch",DraftPatches=[new(){Op="update",EventId="a",Fields=JsonSerializer.SerializeToElement(new{duration=30})}]};
 DraftEdits.Resolve(reply,memory);Equal(30,reply.Events[0].Duration);Equal(90,reply.Events[1].Duration);Equal("南京博物院",reply.Events[1].Location);
});
const string Poi="""{"status":"1","pois":[{"id":"museum","name":"南京博物院","cityname":"南京市","address":"中山东路321号","location":"118.848,32.044"}]}""";
async Task<string> Search(Assembly assembly,string response)
{
 int calls=0;using var http=new HttpClient(new FixtureHandler(request=>{
  calls++;Equal("/v3/place/text",request.RequestUri!.AbsolutePath);return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(response)});
 }));
 object client=Activator.CreateInstance(assembly.GetType("Planner.Data.AmapClient")!,"fixture",http)!;
 try{
  var task=(Task)client.GetType().GetMethod("SearchPlacesAsync")!.Invoke(client,["南京博物院",CancellationToken.None])!;await task;
  var items=(IEnumerable)task.GetType().GetProperty("Result")!.GetValue(task)!;
  var rows=items.Cast<object>().Select(p=>new[]{"Name","Address","Lng","Lat","Status","Source"}.ToDictionary(n=>n,n=>p.GetType().GetProperty(n)!.GetValue(p))).ToList();
  Equal(1,calls);return Json(rows);
 }catch(Exception e){return e.GetType().FullName+":"+e.Message;}
}
tests.Add(("Amap POI parsing and rejection match original release",async()=>{
 foreach(var json in new[]{Poi,Poi.Replace("南京市","苏州市"),Poi.Replace("118.848,32.044","bad"),"{\"status\":\"1\",\"pois\":[]}","{\"status\":\"0\",\"infocode\":\"10001\"}"})
  Equal(await Search(originalData,json),await Search(typeof(AmapClient).Assembly,json));
}));
int passed=0;foreach(var (name,body) in tests){try{await body();passed++;Console.WriteLine("PASS "+name);}catch(Exception e){Console.WriteLine("FAIL "+name+": "+e.Message);}}
Console.WriteLine($"RESULT {passed}/{tests.Count} passed");return passed==tests.Count?0:1;
sealed class ReferenceContext(string directory):AssemblyLoadContext(isCollectible:true)
{
 protected override Assembly? Load(AssemblyName name)=>name.Name is "Planner.Core" or "Planner.Data"?LoadFromAssemblyPath(Path.Combine(directory,name.Name+".dll")):null;
}
sealed class FixtureHandler(Func<HttpRequestMessage,Task<HttpResponseMessage>> respond):HttpMessageHandler
{
 protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>respond(request);
}
