using System.Net;
using System.Text;
using System.Text.Json;
using Planner.Core;
using Planner.Data;

static class ConversationRecoveryTests
{
    static void Check(bool value){if(!value)throw new Exception("conversation recovery assertion failed");}
    public static void Register(Action<string,Action> test)
    {
        test("incomplete draft preserves the answer but never enables generation",()=>
        {
            foreach(var events in new[]{"[]","[{\"day\":\"现在\",\"title\":\"去公共卫生间\",\"location\":\"中山陵附近公共卫生间\",\"duration\":10}]","[{\"day\":\"2026-11-02\",\"title\":\"参观\",\"location\":\"南京博物院\",\"duration\":0}]"})
            {
                var reply=GuidedAgentClient.ParseTurn("{\"message\":\"你希望调整哪些内容？\",\"ready\":true,\"events\":"+events+"}");
                Check(reply.Message=="你希望调整哪些内容？"&&!reply.Ready&&reply.Events.Count==0&&reply.DraftIssue.Length>0);
                string dir=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString());
                try{var c=new GuidedConversation();c.AddUser("行程表我不满意");c.AddAssistant(reply);new ConversationStore(dir).Save(c);var read=new ConversationStore(dir).Load();Check(read.Active.Messages.Last().Content==reply.Message&&!read.Active.Reply!.Ready&&read.Active.Reply.DraftIssue.Length>0);}
                finally{if(Directory.Exists(dir))Directory.Delete(dir,true);}
            }
        });
        test("plain conversational answer is preserved without enabling a plan",()=>
        {
            var reply=GuidedAgentClient.ParseTurn("你想调整时间、地点还是行程节奏？");
            Check(reply.Message=="你想调整时间、地点还是行程节奏？"&&!reply.Ready&&reply.Events.Count==0&&reply.DraftIssue.Length==0);
        });
        test("malformed structured turn is repaired once using the same user history",()=>
        {
            int calls=0;using var http=new HttpClient(new Handler(async request=>
            {
                calls++;using var doc=JsonDocument.Parse(await request.Content!.ReadAsStringAsync());var messages=doc.RootElement.GetProperty("messages");
                Check(messages[1].GetProperty("content").GetString()=="不满意");
                if(calls==2)Check(messages.GetArrayLength()==3&&messages[2].GetProperty("role").GetString()=="system");
                string content=calls==1?"{broken":"你希望先改哪个部分？";
                return new(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new{choices=new[]{new{message=new{content}}}}),Encoding.UTF8,"application/json")};
            }));
            var reply=new GuidedAgentClient(http).ReplyAsync(new(){AgentProvider="CUSTOM",LlmEndpoint="http://localhost/v1",Model="fixture"},[new(){Content="不满意"}],default).GetAwaiter().GetResult();
            Check(calls==2&&reply.Message=="你希望先改哪个部分？"&&!reply.Ready);
        });
        test("invalid responses without usable text remain errors rather than fake replies",()=>
        {
            foreach(string text in new[]{"{broken","{}","{\"message\":null}","{\"message\":\"\"}"})
            {bool rejected=false;try{GuidedAgentClient.ParseTurn(text);}catch(ArgumentException){rejected=true;}Check(rejected);}
        });
        test("follow up after ready draft and malformed activity data retains complete history",()=>
        {
            int calls=0;var c=new GuidedConversation();
            using var http=new HttpClient(new Handler(async request=>
            {
                calls++;using var doc=JsonDocument.Parse(await request.Content!.ReadAsStringAsync());var messages=doc.RootElement.GetProperty("messages");
                Check(messages.GetArrayLength()==calls*2);Check(messages[0].GetProperty("content").GetString()!.Contains("当前北京时间："));
                if(calls==2)Check(messages[3].GetProperty("content").GetString()=="行程表我不满意");
                if(calls==3)Check(messages[4].GetProperty("content").GetString()=="你想怎么调整？");
                string payload=calls==1?"{\"message\":\"请核对草稿\",\"ready\":true,\"events\":[{\"day\":\"2026-11-02\",\"title\":\"参观\",\"location\":\"南京博物院\",\"duration\":120}]}":calls==2?"{\"message\":\"你想怎么调整？\",\"ready\":true,\"events\":[]}":"{\"message\":\"改成一小时，请核对\",\"ready\":true,\"events\":[{\"day\":\"2026-11-02\",\"title\":\"参观\",\"location\":\"南京博物院\",\"duration\":60}]}";
                return new(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new{choices=new[]{new{message=new{content=payload}}}}),Encoding.UTF8,"application/json")};
            }));
            var client=new GuidedAgentClient(http);var config=new ServiceSettings{AgentProvider="CUSTOM",LlmEndpoint="http://localhost/v1",Model="fixture"};
            foreach(string text in new[]{"明天参观南京博物院两小时","行程表我不满意","改成一小时"}){c.AddUser(text);c.AddAssistant(client.ReplyAsync(config,c.Active.Messages,default).GetAwaiter().GetResult());}
            Check(calls==3&&c.Active.Messages.Count==6&&c.Active.Reply!.Ready&&c.Active.Reply.Events.Single().Duration==60);
        });
    }
    sealed class Handler(Func<HttpRequestMessage,Task<HttpResponseMessage>> reply):HttpMessageHandler
    {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)=>reply(request);}
}
