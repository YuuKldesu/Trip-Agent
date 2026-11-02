using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Planner.Core;

namespace Planner.Data;

public sealed class RepositoryAgentClient(HttpClient? client = null)
{
	private readonly HttpClient http = client ?? new HttpClient
	{
		Timeout = TimeSpan.FromSeconds(40L)
	};

	public static List<TripRequest> ParseEvents(string text)
	{
		try
		{
			string text2 = text.Trim();
			if (text2.StartsWith("```"))
			{
				int num = text2.IndexOf('\n');
				int num2 = text2.LastIndexOf("```", StringComparison.Ordinal);
				if (num >= 0 && num2 > num)
				{
					string text3 = text2;
					int num3 = num + 1;
					text2 = text3.Substring(num3, num2 - num3);
				}
			}
			using JsonDocument jsonDocument = JsonDocument.Parse(text2);
			List<TripRequest> list = jsonDocument.RootElement.GetProperty("events").Deserialize<List<TripRequest>>(JsonKit.Options);
			bool flag = list == null;
			if (!flag)
			{
				int num3 = list.Count;
				bool flag2 = ((num3 < 1 || num3 > 15) ? true : false);
				flag = flag2;
			}
			if (flag || list.Any((TripRequest r) => r == null))
			{
				throw new JsonException();
			}
			return list;
		}
		catch (Exception ex) when ((ex is JsonException || ex is KeyNotFoundException || ex is InvalidOperationException) ? true : false)
		{
			throw new ArgumentException("未能识别1—15项活动，请补充描述或调整输入。");
		}
	}

	public static TripRequest ParseLocal(string text, DateOnly today)
	{
		if (Regex.IsMatch(text, "然后|接着|最后|再去|之后|随后|再到|回到|返回"))
		{
			throw new ArgumentException("本地规则仅支持单项活动；多项活动请配置模型服务。");
		}
		Match match = Regex.Match(text, "(\\d{4})[-年/](\\d{1,2})[-月/](\\d{1,2})日?");
		Match match2 = Regex.Match(text, "(\\d{1,2})月(\\d{1,2})[日号]");
		DateOnly value;
		if (text.Contains("后天"))
		{
			value = today.AddDays(2);
		}
		else if (text.Contains("明天"))
		{
			value = today.AddDays(1);
		}
		else if (text.Contains("今天"))
		{
			value = today;
		}
		else if (match.Success)
		{
			value = new DateOnly(int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value), int.Parse(match.Groups[3].Value));
		}
		else
		{
			if (!match2.Success)
			{
				throw new ArgumentException("请写明日期，例如明天或2026-11-02。");
			}
			value = new DateOnly(today.Year, int.Parse(match2.Groups[1].Value), int.Parse(match2.Groups[2].Value));
		}
		Match match3 = Regex.Match(text, "(?:在|去|到)([\\u4e00-\\u9fa5A-Za-z0-9]+?)(?:看|吃|参观|游览|开会|学习|逛|拍照|办|做|玩|，|,|\\s)");
		Match match4 = Regex.Match(text, "(\\d+(?:\\.\\d+)?)\\s*(个?小时|分钟)");
		if (!match3.Success || !match4.Success)
		{
			throw new ArgumentException("请明确地点和耗时，例如：明天在南京博物院参观，2小时。");
		}
		int? start = null;
		Match match5 = Regex.Match(text, "(上午|下午|晚上|中午)?\\s*(\\d{1,2})(?:点|:)(\\d{1,2})?");
		if (match5.Success)
		{
			int num = int.Parse(match5.Groups[2].Value);
			int num2 = (match5.Groups[3].Success ? int.Parse(match5.Groups[3].Value) : 0);
			string value2 = match5.Groups[1].Value;
			bool flag = ((value2 == "下午" || value2 == "晚上") ? true : false);
			if (flag && num < 12)
			{
				num += 12;
			}
			if (match5.Groups[1].Value == "中午" && num < 11)
			{
				num += 12;
			}
			if (num > 23 || num2 > 59)
			{
				throw new ArgumentException("时间格式不正确。");
			}
			start = num * 60 + num2;
		}
		Match match6 = Regex.Match(text, "看电影|参观|游览|开会|学习|逛街|拍照|吃饭");
		TripRequest tripRequest = new TripRequest
		{
			Day = value,
			Location = match3.Groups[1].Value,
			Title = (match6.Success ? match6.Value : "活动") + " · " + match3.Groups[1].Value,
			Duration = (int)Math.Round(double.Parse(match4.Groups[1].Value, CultureInfo.InvariantCulture) * (double)((!match4.Groups[2].Value.Contains("小时")) ? 1 : 60)),
			Start = start
		};
		RepositoryPlanner.Validate(tripRequest);
		return tripRequest;
	}

	public async Task<List<TripRequest>> ExtractAsync(ServiceSettings config, string text, CancellationToken token)
	{
		token.ThrowIfCancellationRequested();
		string text2 = config.AgentProvider;
		if (text2 == "AUTO")
		{
			text2 = (string.IsNullOrWhiteSpace(config.LlmEndpoint) ? "LOCAL" : "CUSTOM");
		}
		if (text2 == "LOCAL")
		{
			return new List<TripRequest>(1) { ParseLocal(text, Clock.Today) };
		}
		string llmEndpoint = config.LlmEndpoint;
		string model = config.Model;
		Dictionary<string, object> dictionary = new Dictionary<string, object>();
		if (text2 != "CUSTOM")
		{
			throw new ArgumentException("请在设置中选择自定义 API 或本地规则引导。");
		}
		if (string.IsNullOrWhiteSpace(model))
		{
			throw new ArgumentException("请在服务设置填写模型名称。");
		}
		string text3 = SettingsStore.Unprotect(config.LlmKeyProtected);
		if (string.IsNullOrWhiteSpace(text3) && !LlmClient.Endpoint(llmEndpoint).IsLoopback)
		{
			throw new ArgumentException("请在服务设置填写模型API密钥。");
		}
		string content = "只返回JSON对象，包含events数组，最多15项，按用户顺序提取；字段day(YYYY-MM-DD)、title(具体活动完整名称)、location、duration(整数分钟)、start(午夜起分钟，未指定null)、end(明确结束时刻否则null)、earliest(最早开始分钟否则null)、latest_end(必须结束或到达最晚分钟否则null)、notes(仅对应活动明确要求的摘要，未提供为空)、mode_in(公交/步行/骑行/驾车之一或null)、is_return(返程true，duration为0)。日期继承用户说明；缺少信息用null，不猜测日期地点时长。不生成坐标或交通数据，不安排时间。今天是" + Clock.Today.ToString("yyyy-MM-dd");
		dictionary["model"] = model;
		dictionary["messages"] = new[]
		{
			new
			{
				role = "system",
				content = content
			},
			new
			{
				role = "user",
				content = text
			}
		};
		dictionary["temperature"] = 0;
		if (config.JsonMode)
		{
			dictionary["response_format"] = new
			{
				type = "json_object"
			};
		}
		using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, LlmClient.Endpoint(llmEndpoint));
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", text3);
		request.Content = new StringContent(JsonSerializer.Serialize(dictionary), Encoding.UTF8, "application/json");
		using HttpResponseMessage response = await ModelTransport.SendAsync(http, request, token);
		List<TripRequest> result;
		await using (Stream stream = await response.Content.ReadAsStreamAsync(token))
		{
			using MemoryStream buffer = new MemoryStream();
			byte[] bytes = new byte[8192];
			int num;
			while ((num = await ModelTransport.ReadAsync(stream, bytes, token)) > 0)
			{
				if (buffer.Length + num > 1048576)
				{
					throw new InvalidOperationException("模型响应过长。");
				}
				buffer.Write(bytes, 0, num);
			}
			using JsonDocument jsonDocument = JsonDocument.Parse(buffer.ToArray());
			result = ParseEvents(jsonDocument.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "");
		}
		return result;
	}
}
