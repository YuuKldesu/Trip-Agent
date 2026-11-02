using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Planner.Data;

public static class ModelTransport
{
	public static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpRequestMessage request, CancellationToken token)
	{
		token.ThrowIfCancellationRequested();
		try
		{
			HttpResponseMessage httpResponseMessage = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
			if (token.IsCancellationRequested)
			{
				httpResponseMessage.Dispose();
				token.ThrowIfCancellationRequested();
			}
			if (httpResponseMessage.IsSuccessStatusCode)
			{
				return httpResponseMessage;
			}
			int statusCode = (int)httpResponseMessage.StatusCode;
			httpResponseMessage.Dispose();
			throw new ModelServiceException($"模型服务请求失败：HTTP {statusCode}。");
		}
		catch (OperationCanceledException) when (!token.IsCancellationRequested)
		{
			throw ModelServiceException.Timeout();
		}
		catch (HttpRequestException error)
		{
			throw ModelServiceException.Network(error);
		}
	}

	public static async Task<int> ReadAsync(Stream stream, byte[] bytes, CancellationToken token)
	{
		try
		{
			token.ThrowIfCancellationRequested();
			int result = await stream.ReadAsync(bytes, token);
			token.ThrowIfCancellationRequested();
			return result;
		}
		catch (OperationCanceledException) when (!token.IsCancellationRequested)
		{
			throw ModelServiceException.Timeout();
		}
		catch (HttpRequestException error)
		{
			throw ModelServiceException.Network(error);
		}
		catch (IOException)
		{
			throw new ModelServiceException("模型连接失败：连接中断。");
		}
	}
}
