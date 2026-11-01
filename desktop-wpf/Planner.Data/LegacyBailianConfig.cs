using System.Text.Json;
namespace Planner.Data;
// Read only when explicitly requested in Settings. Never uploads or rewrites the legacy file.
public sealed class LegacyBailianConfig
{
    public string Key {get;private init;}="";
    public string SelectedProvider {get;private init;}="本地规则";
    public bool UsedBailian=>SelectedProvider=="阿里云百炼";
    public string Region {get;private init;}="北京";
    public string Host {get;private init;}="";
    public Uri Endpoint=>LlmClient.Endpoint(RepositoryAgentClient.BailianEndpoint(Region,Host));
    public static string DefaultPath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".nanjing_trip_agent","config.json");
    public override string ToString()=>"v1.12百炼配置（"+Region+"）";
    public static LegacyBailianConfig Load(string path)
    {
        if(new FileInfo(path).Length>65536)throw new ArgumentException("旧版配置文件过大，请选择v1.12的config.json。");
        return Parse(File.ReadAllText(path));
    }
    public static LegacyBailianConfig Parse(string json)
    {
        if(json.Length>65536)throw new ArgumentException("旧版配置文件过大。");
        try
        {
            using var doc=JsonDocument.Parse(json);var root=doc.RootElement;if(root.ValueKind!=JsonValueKind.Object)throw new JsonException();
            string Text(string name,string fallback=""){if(!root.TryGetProperty(name,out var value))return fallback;if(value.ValueKind!=JsonValueKind.String)throw new JsonException();return value.GetString()??fallback;}
            string key=ModelHttp.NormalizeKey(Text("bailian_key"));if(key.Length==0)throw new ArgumentException("此文件没有百炼密钥，请选择v1.12实际使用的config.json。");
            string provider=Text("llm_provider");if(provider.Length==0)provider=Text("deepseek_key").Length>0?"DeepSeek":"本地规则";if(provider is not("本地规则" or "DeepSeek" or "阿里云百炼"))provider="未识别";
            var result=new LegacyBailianConfig{SelectedProvider=provider,Key=key,Region=Text("bailian_region","北京"),Host=Text("bailian_host").Trim()};_=result.Endpoint;return result;
        }
        catch(JsonException){throw new ArgumentException("旧版配置格式不正确，请选择v1.12的config.json。");}
    }
}
