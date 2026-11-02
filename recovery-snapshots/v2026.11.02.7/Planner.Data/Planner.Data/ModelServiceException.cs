using System;
using System.Net.Http;

namespace Planner.Data;

public sealed class ModelServiceException : InvalidOperationException
{
	public ModelServiceException(string message)
		: base(message)
	{
	}

	public static ModelServiceException Network(HttpRequestException error)
	{
		return new ModelServiceException("模型连接失败：" + error.HttpRequestError switch
		{
			HttpRequestError.NameResolutionError => "域名解析失败。", 
			HttpRequestError.SecureConnectionError => "安全连接失败。", 
			HttpRequestError.ProxyTunnelError => "代理连接失败。", 
			_ => "网络连接异常。", 
		});
	}

	public static ModelServiceException Timeout()
	{
		return new ModelServiceException("模型连接失败：请求超时。");
	}
}
