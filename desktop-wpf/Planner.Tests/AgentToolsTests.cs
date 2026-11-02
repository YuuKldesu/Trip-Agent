using Planner.Core;
using Planner.Data;
using System.Text.Json;
using System.Net;
using System.Text;
static class AgentToolsTests
{
 static void Equal<T>(T want,T got){if(!Equals(want,got))throw new Exception($"expected {want}, got {got}");}
 static void Reject(Action action){try{action();}catch(ArgumentException){return;}throw new Exception("expected rejection");}
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
 }
}
