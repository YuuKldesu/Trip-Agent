using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Planner.Core;

namespace Planner.Data;

public sealed class SettingsStore(string directory)
{
	private static class SecretCodec
	{
		private struct Blob
		{
			public int Length;

			public nint Data;
		}

		[DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
		private static extern bool CryptProtectData(ref Blob input, string? description, nint entropy, nint reserved, nint prompt, int flags, out Blob output);

		[DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
		private static extern bool CryptUnprotectData(ref Blob input, nint description, nint entropy, nint reserved, nint prompt, int flags, out Blob output);

		[DllImport("kernel32.dll")]
		private static extern nint LocalFree(nint ptr);

		public static string Transform(string text, bool protect)
		{
			if (string.IsNullOrEmpty(text))
			{
				return "";
			}
			if (!OperatingSystem.IsWindows())
			{
				throw new PlatformNotSupportedException("密钥保护需要Windows。");
			}
			byte[] array = (protect ? Encoding.UTF8.GetBytes(text) : Convert.FromBase64String(text));
			Blob input = new Blob
			{
				Length = array.Length,
				Data = Marshal.AllocHGlobal(array.Length)
			};
			Blob output = default;
			try
			{
				Marshal.Copy(array, 0, input.Data, array.Length);
				if (!(protect ? CryptProtectData(ref input, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output) : CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output)))
				{
					throw new InvalidOperationException("无法保护或读取本机密钥；若更换了Windows用户，请重新填写服务设置。");
				}
				byte[] array2 = new byte[output.Length];
				Marshal.Copy(output.Data, array2, 0, array2.Length);
				return protect ? Convert.ToBase64String(array2) : Encoding.UTF8.GetString(array2);
			}
			finally
			{
				Marshal.FreeHGlobal(input.Data);
				if (output.Data != IntPtr.Zero)
				{
					LocalFree(output.Data);
				}
			}
		}
	}

	private readonly string path = Path.Combine(directory, "services.json");

	public ServiceSettings Load()
	{
		ServiceSettings serviceSettings = (File.Exists(path) ? (JsonSerializer.Deserialize<ServiceSettings>(File.ReadAllText(path), JsonKit.Options) ?? new ServiceSettings()) : new ServiceSettings());
		if (serviceSettings.AgentProvider == "DEEPSEEK")
		{
			serviceSettings.AgentProvider = "CUSTOM";
			serviceSettings.LlmEndpoint = "https://api.deepseek.com/v1";
			serviceSettings.Model = "deepseek-flash";
		}
		bool flag;
		switch (serviceSettings.AgentProvider)
		{
		case "AUTO":
		case "LOCAL":
		case "CUSTOM":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (!flag)
		{
			serviceSettings.AgentProvider = "LOCAL";
		}
		serviceSettings.AssistantPersona = AssistantPersonas.Normalize(serviceSettings.AssistantPersona);
		return serviceSettings;
	}

	public void Save(ServiceSettings settings)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(path));
		string sourceFileName = path + ".tmp";
		File.WriteAllText(sourceFileName, JsonSerializer.Serialize(settings, JsonKit.Options));
		File.Move(sourceFileName, path, overwrite: true);
	}

	public static string Protect(string text)
	{
		return SecretCodec.Transform(text, protect: true);
	}

	public static string Unprotect(string text)
	{
		return SecretCodec.Transform(text, protect: false);
	}

	public static string ReadForSettings(string text)
	{
		try
		{
			return Unprotect(text);
		}
		catch (Exception ex) when ((ex is FormatException || ex is InvalidOperationException) ? true : false)
		{
			return "";
		}
	}
}
