using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Planner.Core;
namespace Planner.Data;

public sealed class GuidedAgentClient(HttpClient? client=null)
{
    readonly HttpClient http=client??new(){Timeout=TimeSpan.FromSeconds(60)};
    public const string SystemPrompt="""
你是南京市内行程规划助手。用自然、简洁的中文连续对话引导用户，不扮演角色，不要求用户按表单格式回答。
目标：用户说明要做的事，你记住同一分支的完整上下文，逐步收集足够信息，汇总后提出可供核对的活动草稿。不要操作数据库。
规则：
1. 先理解目标；从已有对话提取已明确的日期、可用时间、固定安排、事项、地点、耗时、出行方式、预算、节奏及提醒偏好。不重复询问已回答的内容。每轮只问一到两个最影响规划的缺项；允许用户自由补充、纠正和让你提出备选建议。
2. 日期、地点和每项耗时未明确时继续问；若用户要求你推荐地点或时长，可以提出明确标为建议的备选并请用户选择，不把建议当作用户已确认的事实。可选偏好未给出不阻塞完成；不要强制固定轮数。
3. 用户提供时间范围时用earliest/latest_end约束而非擅自设定固定start。明确指定的开始/结束才写start/end；没有指定开始时间则null，由软件排程。仅限市内公交（含地铁）、步行、骑行、驾车/打车；打车用驾车路线并在notes写明打车。排除火车飞机。
4. 不虚构坐标、价格、拥挤程度、营业/预约状态或真实路程；准确地点和交通由软件之后查询并由用户确认。不同活动分别提取，不丢失用户的限制，完整限制写入summary与对应notes。
5. 活动按用户期望顺序，最多15项，duration为1—10080整数分钟；返程is_return=true允许duration=0。跨日必须明确end_day（YYYY-MM-DD），且duration要包含跨越天数。不猜测午夜归属；不明确时继续询问。
6. 只输出一个JSON对象，不能使用Markdown围栏外的说明。结构：message(当前自然语言回答或追问)，summary(全部已确认需求的摘要)，missing(缺失关键项名称数组)，ready(布尔)，events(活动数组)。
7. events每项字段：day(YYYY-MM-DD或null)、end_day(明确结束日期或null)、title(完整名称)、location(地点关键词)、duration(整数分钟或null)、start(当天午夜起分钟或null)、end(结束日期当天午夜起分钟或null)、earliest(当天最早分钟或null)、latest_end(相对开始日期午夜的最晚结束分钟或null)、notes(活动限制及提醒偏好)、mode_in(公交/步行/骑行/驾车或null)、is_return(布尔)。不提供内部ID或修改操作。
8. 只有关键项足够且用户已选定建议时ready=true、missing=[]，汇总并说明需核对。否则ready=false并继续问；不能把未完成信息交给用户自己填表作为默认结束。用户要求先看草稿时，明确指出待确认项，ready仍为false。
""";
    public static bool IsLocal(ServiceSettings config)=>config.AgentProvider=="LOCAL"||(config.AgentProvider=="AUTO"&&string.IsNullOrWhiteSpace(config.LlmEndpoint));
    public static GuidedReply Parse(string text)
    {
        try
        {
            text=text.Trim();if(text.StartsWith("```")){int start=text.IndexOf('\n'),end=text.LastIndexOf("```",StringComparison.Ordinal);if(start>=0&&end>start)text=text[(start+1)..end];}
            var reply=JsonSerializer.Deserialize<GuidedReply>(text,JsonKit.Options)??throw new JsonException();
            if(string.IsNullOrWhiteSpace(reply.Message)||reply.Message.Length>12000||reply.Summary is null||reply.Summary.Length>20000||reply.Missing is null||reply.Events is null||reply.Events.Count>15||reply.Events.Any(e=>e is null)||reply.Missing.Any(m=>m is null||m.Length>500))throw new JsonException();
            if(reply.Ready){if(reply.Events.Count==0||reply.Missing.Count!=0)throw new ArgumentException("助手尚未提供完整可核对的活动，请继续补充需求。");foreach(var r in reply.Events)RepositoryPlanner.Validate(r);}
            return reply;
        }
        catch(Exception e)when(e is JsonException or FormatException or InvalidOperationException){throw new ArgumentException("助手返回的对话格式不完整，本轮未生成行程，可重试或补充说明。",e);}
    }
    public async Task<GuidedReply> ReplyAsync(ServiceSettings config,IReadOnlyList<ConversationMessage> messages,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();if(messages.Count==0||messages.Count>100||messages.Any(m=>m.Role is not("user" or "assistant")||string.IsNullOrWhiteSpace(m.Content)||m.Content.Length>20000))throw new ArgumentException("对话内容无效或过长，请新建对话后继续。");
        if(IsLocal(config))return LocalReply(messages,Clock.Today);
        var provider=config.AgentProvider=="AUTO"?"CUSTOM":config.AgentProvider;string endpoint=config.LlmEndpoint,model=config.Model;var body=new Dictionary<string,object?>();
        if(provider=="BAILIAN"){endpoint=RepositoryAgentClient.BailianEndpoint(config.BailianRegion,config.BailianHost);model="qwen-plus";body["enable_thinking"]=false;}else if(provider=="DEEPSEEK"){endpoint="https://api.deepseek.com/v1";model="deepseek-flash";body["thinking"]=new{type="disabled"};}else if(provider!="CUSTOM")throw new ArgumentException("请在设置中选择有效的智能服务。");
        if(string.IsNullOrWhiteSpace(model))throw new ArgumentException("请先在设置中填写模型名称。");var uri=LlmClient.Endpoint(endpoint);var key=SettingsStore.Unprotect(config.LlmKeyProtected);if(string.IsNullOrWhiteSpace(key)&&!uri.IsLoopback)throw new ArgumentException("请先在设置中填写模型密钥。");
        var history=new List<object>{new{role="system",content=SystemPrompt+"\n当前北京时间日期："+Clock.Today.ToString("yyyy-MM-dd")}};history.AddRange(messages.Select(m=>(object)new{role=m.Role,content=m.Content}));body["model"]=model;body["messages"]=history;body["temperature"]=.2;if(config.JsonMode)body["response_format"]=new{type="json_object"};
        using var request=new HttpRequestMessage(HttpMethod.Post,uri);request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",key);request.Content=new StringContent(JsonSerializer.Serialize(body),Encoding.UTF8,"application/json");using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token);if(!response.IsSuccessStatusCode)throw new InvalidOperationException($"对话服务返回HTTP {(int)response.StatusCode}，本轮未写入行程，请检查服务设置后重试。");
        await using var stream=await response.Content.ReadAsStreamAsync(token);using var buffer=new MemoryStream();byte[] bytes=new byte[8192];int n;while((n=await stream.ReadAsync(bytes,token))>0){if(buffer.Length+n>1024*1024)throw new InvalidOperationException("对话响应过长，本轮未写入行程。");buffer.Write(bytes,0,n);}token.ThrowIfCancellationRequested();
        try{using var doc=JsonDocument.Parse(buffer.ToArray());return Parse(doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()??"");}catch(Exception e)when(e is JsonException or KeyNotFoundException or IndexOutOfRangeException){throw new ArgumentException("对话服务未返回有效回复，本轮未写入行程。",e);}
    }
    public static GuidedReply LocalReply(IReadOnlyList<ConversationMessage> messages,DateOnly today)
    {
        string text=string.Join("\n",messages.Where(m=>m.Role=="user").Select(m=>m.Content));var missing=new List<string>();
        if(!Regex.IsMatch(text,@"今天|明天|后天|\d{4}[-年/]\d{1,2}[-月/]\d{1,2}|\d{1,2}月\d{1,2}[日号]"))missing.Add("日期");
        if(!Regex.IsMatch(text,@"(?:在|去|到)[\u4e00-\u9fa5A-Za-z0-9]+?(?:看|吃|参观|游览|开会|学习|逛|拍照|办|做|玩|，|,|\s)"))missing.Add("具体地点和事项");
        if(!Regex.IsMatch(text,@"\d+(?:\.\d+)?\s*(?:个?小时|分钟)"))missing.Add("每项耗时");
        var reply=new GuidedReply{Summary=string.Join("；",messages.Where(m=>m.Role=="user").Select(m=>m.Content)),Missing=missing};
        if(missing.Count>0){reply.Message=missing[0]=="日期"?"这次具体安排在哪一天？例如明天，或写明年月日。":missing[0]=="具体地点和事项"?"日期我记住了。接下来想去哪里、做什么？可以说“在南京博物院参观”。":"地点和事项已记住。预计需要多久？例如2小时；如果还有其他事项，也可以一起补充。";return reply;}
        try{reply.Events=[RepositoryAgentClient.ParseLocal(text,today)];reply.Ready=true;reply.Message="信息已足够安排这一项活动。请核对需求汇总，再生成草稿；具体地点与交通将在下一步确认。";}catch(ArgumentException e){reply.Missing=["需要进一步确认的活动细节"];reply.Message="本地规则暂不能可靠理解这组安排。"+e.Message+"你可以把单项活动的日期、地点和耗时补充清楚，或在设置中连接模型以继续多项行程对话。";}return reply;
    }
}
