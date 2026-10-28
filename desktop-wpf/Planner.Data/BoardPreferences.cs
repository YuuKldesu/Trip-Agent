using Planner.Core;
using System.Text.Json;
namespace Planner.Data;
public sealed class BoardPreferences
{
    public bool ShowCompleted {get;set;}=true;
    public bool ShowIncomplete {get;set;}=true;
    public bool IsVisible(string key)=>Completed.Contains(key)?ShowCompleted:ShowIncomplete;
    public string View {get;set;}="MONTH";
    public HashSet<string> Completed {get;set;}=[];
}
public sealed class BoardPreferenceStore(string directory)
{
    readonly string path=Path.Combine(directory,"board.json");
    public BoardPreferences Load()
    {
        try
        {
            if(!File.Exists(path))return new();var value=JsonSerializer.Deserialize<BoardPreferences>(File.ReadAllText(path),JsonKit.Options)??new();
            if(value.View is not("MONTH" or "LIST"))value.View="MONTH";
            value.Completed=new((value.Completed??[]).Where(k=>!string.IsNullOrWhiteSpace(k)&&k.Length<200));return value;
        }
        catch(Exception ex)when(ex is JsonException or IOException or UnauthorizedAccessException){return new();}
    }
    public void Save(BoardPreferences value)
    {
        if(value.View is not("MONTH" or "LIST")||value.Completed is null)throw new ArgumentException("看板显示设置不合法。");
        Directory.CreateDirectory(directory);var temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{File.WriteAllText(temp,JsonSerializer.Serialize(value,JsonKit.Options));File.Move(temp,path,true);}finally{if(File.Exists(temp))File.Delete(temp);}
    }
}
