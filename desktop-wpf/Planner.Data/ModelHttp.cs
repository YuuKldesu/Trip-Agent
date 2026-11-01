using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace Planner.Data;

// Match the v1.12 key treatment at both save and request boundaries, including existing configs.
public static class ModelHttp
{
    public static string NormalizeKey(string key)
    {
        key=key.Trim();
        if(key.StartsWith("Bearer ",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("模型密钥只填写Key本身，不要包含Bearer前缀。");
        if(key.Any(char.IsWhiteSpace))throw new ArgumentException("模型密钥中间含有空白或换行，请重新完整复制密钥。");
        return key;
    }
    public static HttpRequestMessage Request(Uri endpoint,string key,string json)
    {
        key=NormalizeKey(key);var request=new HttpRequestMessage(HttpMethod.Post,endpoint);
        if(key.Length>0)request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",key);
        request.Content=new StringContent(json,Encoding.UTF8,"application/json");return request;
    }
    public static async Task EnsureSuccessAsync(HttpResponseMessage response,Uri endpoint,string service,CancellationToken token,string key="")
    {
        if(response.IsSuccessStatusCode)return;token.ThrowIfCancellationRequested();
        string code="";
        try
        {
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromSeconds(3));
            using var buffer=new MemoryStream();await using var stream=await response.Content.ReadAsStreamAsync(timeout.Token);byte[] bytes=new byte[4096];
            while(buffer.Length<65536){int n=await stream.ReadAsync(bytes.AsMemory(0,(int)Math.Min(bytes.Length,65536-buffer.Length)),timeout.Token);if(n==0)break;buffer.Write(bytes,0,n);}
            using var json=JsonDocument.Parse(buffer.ToArray());var root=json.RootElement;
            if(root.ValueKind==JsonValueKind.Object)
            {
                var error=root.TryGetProperty("error",out var nested)&&nested.ValueKind==JsonValueKind.Object?nested:root;
                if(error.TryGetProperty("code",out var value)&&value.ValueKind==JsonValueKind.String)code=value.GetString()??"";
                if(code.Length==0&&error.TryGetProperty("type",out value)&&value.ValueKind==JsonValueKind.String)code=value.GetString()??"";
            }
        }
        catch(OperationCanceledException) when(!token.IsCancellationRequested){ }
        catch(OperationCanceledException){throw;}
        catch(Exception ex) when(ex is JsonException or IOException or HttpRequestException or InvalidOperationException){ }
        // Never surface provider messages/bodies: they may echo credentials, prompts, or personal content.
        if(!Regex.IsMatch(code,@"^[A-Za-z0-9_.-]{1,80}$")||Regex.IsMatch(code,@"sk[-_]|bearer",RegexOptions.IgnoreCase)||(key.Length>0&&code.Contains(key,StringComparison.Ordinal)))code="";
        string hint=response.StatusCode switch
        {
            HttpStatusCode.Unauthorized=>"鉴权失败。请核对本次密钥、地域与接口地址是否对应；旧版能用时请对照旧版配置。",
            HttpStatusCode.Forbidden=>"服务拒绝访问，请检查密钥的模型权限、业务空间和访问限制。",
            HttpStatusCode.PaymentRequired=>"服务额度或余额不足，请检查服务账户。",
            HttpStatusCode.TooManyRequests=>"请求被限流或触及配额，请稍后重试并检查服务配额。",
            _ when (int)response.StatusCode>=500=>"模型服务暂时异常，请稍后重试。",
            _=>"请求未被服务接受，请核对服务配置。"
        };
        string target=endpoint.Host;
        if(response.RequestMessage?.RequestUri is {} actual&&!actual.Host.Equals(endpoint.Host,StringComparison.OrdinalIgnoreCase))target+="（重定向至 "+actual.Host+"）";
        throw new InvalidOperationException($"{service}返回 HTTP {(int)response.StatusCode}。\n{hint}\n请求服务：{target}"+(code.Length>0?"\n服务错误代码："+code:"")+"\n本轮未写入行程。");
    }
}
