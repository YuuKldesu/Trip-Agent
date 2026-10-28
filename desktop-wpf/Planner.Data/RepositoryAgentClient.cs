using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Planner.Core;
namespace Planner.Data;

public sealed class RepositoryAgentClient(HttpClient? client=null)
{
    readonly HttpClient http=client??new(){Timeout=TimeSpan.FromSeconds(40)};
    public static List<TripRequest> ParseEvents(string text)
    {
        try
        {
            var value=text.Trim();if(value.StartsWith("```")){int a=value.IndexOf('\n'),b=value.LastIndexOf("```",StringComparison.Ordinal);if(a>=0&&b>a)value=value[(a+1)..b];}
            using var doc=JsonDocument.Parse(value);var rows=doc.RootElement.GetProperty("events").Deserialize<List<TripRequest>>(JsonKit.Options);
            if(rows is null||rows.Count is <1 or >15||rows.Any(r=>r is null))throw new JsonException();return rows;
        }
        catch(Exception ex)when(ex is JsonException or KeyNotFoundException or InvalidOperationException){throw new ArgumentException("未能识别1—15项活动，请补充描述或调整输入。");}
    }
    public static TripRequest ParseLocal(string text,DateOnly today)
    {
        if(Regex.IsMatch(text,"然后|接着|最后|再去|之后|随后|再到|回到|返回"))throw new ArgumentException("本地规则仅支持单项活动；多项活动请配置模型服务。");
        DateOnly day;var date=Regex.Match(text,@"(\d{4})[-年/](\d{1,2})[-月/](\d{1,2})日?");var shortDate=Regex.Match(text,@"(\d{1,2})月(\d{1,2})[日号]");
        if(text.Contains("后天"))day=today.AddDays(2);else if(text.Contains("明天"))day=today.AddDays(1);else if(text.Contains("今天"))day=today;
        else if(date.Success)day=new(int.Parse(date.Groups[1].Value),int.Parse(date.Groups[2].Value),int.Parse(date.Groups[3].Value));
        else if(shortDate.Success)day=new(today.Year,int.Parse(shortDate.Groups[1].Value),int.Parse(shortDate.Groups[2].Value));else throw new ArgumentException("请写明日期，例如明天或2026-11-02。");
        var place=Regex.Match(text,@"(?:在|去|到)([\u4e00-\u9fa5A-Za-z0-9]+?)(?:看|吃|参观|游览|开会|学习|逛|拍照|办|做|玩|，|,|\s)");
        var duration=Regex.Match(text,@"(\d+(?:\.\d+)?)\s*(个?小时|分钟)");if(!place.Success||!duration.Success)throw new ArgumentException("请明确地点和耗时，例如：明天在南京博物院参观，2小时。");
        int? start=null;var time=Regex.Match(text,@"(上午|下午|晚上|中午)?\s*(\d{1,2})(?:点|:)(\d{1,2})?");
        if(time.Success){int h=int.Parse(time.Groups[2].Value),m=time.Groups[3].Success?int.Parse(time.Groups[3].Value):0;if((time.Groups[1].Value is "下午" or "晚上")&&h<12)h+=12;if(time.Groups[1].Value=="中午"&&h<11)h+=12;if(h>23||m>59)throw new ArgumentException("时间格式不正确。");start=h*60+m;}
        // The activity verb is consumed by the place match, so retain it from the full request.
        var verb=Regex.Match(text,"看电影|参观|游览|开会|学习|逛街|拍照|吃饭");
        var r=new TripRequest{Day=day,Location=place.Groups[1].Value,Title=(verb.Success?verb.Value:"活动")+" · "+place.Groups[1].Value,Duration=(int)Math.Round(double.Parse(duration.Groups[1].Value,System.Globalization.CultureInfo.InvariantCulture)*(duration.Groups[2].Value.Contains("小时")?60:1)),Start=start};RepositoryPlanner.Validate(r);return r;
    }
    public async Task<List<TripRequest>> ExtractAsync(ServiceSettings config,string text,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();var provider=config.AgentProvider;if(provider=="AUTO")provider=string.IsNullOrWhiteSpace(config.LlmEndpoint)?"LOCAL":"CUSTOM";
        if(provider=="LOCAL")return[ParseLocal(text,Clock.Today)];
        string endpoint=config.LlmEndpoint,model=config.Model;var extra=new Dictionary<string,object?>();
        if(provider=="BAILIAN"){endpoint=BailianEndpoint(config.BailianRegion,config.BailianHost);model="qwen-plus";extra["enable_thinking"]=false;}
        if(provider=="DEEPSEEK"){endpoint="https://api.deepseek.com/v1";model="deepseek-flash";extra["thinking"]=new{type="disabled"};}
        if(string.IsNullOrWhiteSpace(model))throw new ArgumentException("请在服务设置填写模型名称。");
        var key=SettingsStore.Unprotect(config.LlmKeyProtected);if(string.IsNullOrWhiteSpace(key)&&!LlmClient.Endpoint(endpoint).IsLoopback)throw new ArgumentException("请在服务设置填写模型API密钥。");
        string prompt="只返回JSON对象，包含events数组，最多15项，按用户顺序提取；字段day(YYYY-MM-DD)、title(具体活动完整名称)、location、duration(整数分钟)、start(午夜起分钟，未指定null)、end(明确结束时刻否则null)、earliest(最早开始分钟否则null)、latest_end(必须结束或到达最晚分钟否则null)、notes(仅对应活动明确要求的摘要，未提供为空)、mode_in(公交/步行/骑行/驾车之一或null)、is_return(返程true，duration为0)。日期继承用户说明；缺少信息用null，不猜测日期地点时长。不生成坐标或交通数据，不安排时间。今天是"+Clock.Today.ToString("yyyy-MM-dd");
        extra["model"]=model;extra["messages"]=new[]{new{role="system",content=prompt},new{role="user",content=text}};extra["temperature"]=0;
        if(config.JsonMode)extra["response_format"]=new{type="json_object"};
        using var request=new HttpRequestMessage(HttpMethod.Post,LlmClient.Endpoint(endpoint));request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",key);request.Content=new StringContent(JsonSerializer.Serialize(extra),Encoding.UTF8,"application/json");
        using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token);if(!response.IsSuccessStatusCode)throw new InvalidOperationException($"需求识别返回HTTP {(int)response.StatusCode}，请检查服务商、地域、密钥和额度。");
        await using var stream=await response.Content.ReadAsStreamAsync(token);using var buffer=new MemoryStream();var bytes=new byte[8192];int read;while((read=await stream.ReadAsync(bytes,token))>0){if(buffer.Length+read>1024*1024)throw new InvalidOperationException("模型响应过长。");buffer.Write(bytes,0,read);}
        using var doc=JsonDocument.Parse(buffer.ToArray());return ParseEvents(doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()??"");
    }
    public static string BailianEndpoint(string region,string host)
    {
        if(region is not("北京" or "新加坡"))throw new ArgumentException("百炼地域无效。");
        host=host.Trim().ToLowerInvariant().Replace("https://","").TrimEnd('/').Replace("/compatible-mode/v1","");
        if(host.Length==0)host=region=="北京"?"dashscope.aliyuncs.com":"dashscope-intl.aliyuncs.com";
        else if(!Regex.IsMatch(host,region=="北京"?@"^[a-z0-9-]+\.cn-beijing\.maas\.aliyuncs\.com$":@"^[a-z0-9-]+\.ap-southeast-1\.maas\.aliyuncs\.com$"))throw new ArgumentException("百炼API Host与地域不匹配。");
        return "https://"+host+"/compatible-mode/v1";
    }
}
