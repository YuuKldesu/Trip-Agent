using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Planner.Core;

namespace Planner.Data;
public sealed class ServiceSettings
{
    public string LlmEndpoint {get;set;}="";
    public string Model {get;set;}="";
    public string LlmKeyProtected {get;set;}="";
    public string MapServiceKeyProtected {get;set;}="";
    public string MapJsKeyProtected {get;set;}="";
    public string MapSecurityProtected {get;set;}="";
    public bool JsonMode {get;set;}=true;
}
public sealed class SettingsStore(string directory)
{
    readonly string path=Path.Combine(directory,"services.json");
    public ServiceSettings Load()=>File.Exists(path)?JsonSerializer.Deserialize<ServiceSettings>(File.ReadAllText(path),JsonKit.Options)??new():new();
    public void Save(ServiceSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);var tmp=path+".tmp";File.WriteAllText(tmp,JsonSerializer.Serialize(settings,JsonKit.Options));File.Move(tmp,path,true);
    }
    public static string Protect(string text)=>SecretCodec.Transform(text,true);
    public static string Unprotect(string text)=>SecretCodec.Transform(text,false);
    public static string ReadForSettings(string text){try{return Unprotect(text);}catch(Exception ex) when(ex is FormatException or InvalidOperationException){return "";}}
    static class SecretCodec
    {
        [StructLayout(LayoutKind.Sequential)]struct Blob {public int Length;public IntPtr Data;}
        [DllImport("crypt32.dll",SetLastError=true,CharSet=CharSet.Unicode)]static extern bool CryptProtectData(ref Blob input,string? description,IntPtr entropy,IntPtr reserved,IntPtr prompt,int flags,out Blob output);
        [DllImport("crypt32.dll",SetLastError=true,CharSet=CharSet.Unicode)]static extern bool CryptUnprotectData(ref Blob input,IntPtr description,IntPtr entropy,IntPtr reserved,IntPtr prompt,int flags,out Blob output);
        [DllImport("kernel32.dll")]static extern IntPtr LocalFree(IntPtr ptr);
        public static string Transform(string text,bool protect)
        {
            if(string.IsNullOrEmpty(text))return "";
            if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException("密钥保护需要Windows。");
            var data=protect?Encoding.UTF8.GetBytes(text):Convert.FromBase64String(text);var input=new Blob{Length=data.Length,Data=Marshal.AllocHGlobal(data.Length)};Blob output=default;
            try
            {
                Marshal.Copy(data,0,input.Data,data.Length);
                var success=protect?CryptProtectData(ref input,null,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,1,out output):CryptUnprotectData(ref input,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,1,out output);
                if(!success)throw new InvalidOperationException("无法保护或读取本机密钥；若更换了Windows用户，请重新填写服务设置。");
                var bytes=new byte[output.Length];Marshal.Copy(output.Data,bytes,0,bytes.Length);return protect?Convert.ToBase64String(bytes):Encoding.UTF8.GetString(bytes);
            }
            finally{Marshal.FreeHGlobal(input.Data);if(output.Data!=IntPtr.Zero)LocalFree(output.Data);}
        }
    }
}
