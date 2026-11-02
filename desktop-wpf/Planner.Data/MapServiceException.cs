using System;
using System.Linq;
using System.Net.Http;

namespace Planner.Data;

public sealed class MapServiceException : InvalidOperationException
{
	public MapServiceException(string message)
		: base(message)
	{
	}

	public static MapServiceException Network(HttpRequestException error)
	{
		return new MapServiceException("高德连接失败：" + error.HttpRequestError switch
		{
			HttpRequestError.NameResolutionError => "域名解析失败。",
			HttpRequestError.SecureConnectionError => "安全连接失败。",
			HttpRequestError.ProxyTunnelError => "代理连接失败。",
			_ => "网络连接异常。",
		});
	}

	public static MapServiceException Timeout()
	{
		return new MapServiceException("高德连接失败：请求超时。");
	}

	public static MapServiceException Rejected(string code)
	{
		string text;
		switch (code)
		{
		case "10001":
			text = "Key 无效。";
			break;
		case "10003":
			text = "调用额度已用尽。";
			break;
		case "10004":
			text = "请求频率超限。";
			break;
		default:
		{
			int length = code.Length;
			text = "错误代码 " + ((length > 0 && length < 12 && code.All(char.IsAsciiDigit)) ? code : "未知") + "。";
			break;
		}
		}
		return new MapServiceException("高德服务拒绝请求：" + text);
	}
}
