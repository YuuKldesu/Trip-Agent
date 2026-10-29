using Planner.Core;
using System.Text.Json;
namespace Planner.Data;
public sealed class BoardPreferences
{
    public double FontSize {get;set;}=14;
    public string LayoutMode {get;set;}="AUTO";
    public bool AlwaysOnTop {get;set;}
    public string Nickname {get;set;}="我的空间";
    public string Avatar {get;set;}="我";
    public bool SidebarExpanded {get;set;}
    public bool TablesPanelOpen {get;set;}
    public bool AssistantPanelOpen {get;set;}
    public double AssistantWidth {get;set;}=380;
    public HashSet<string> HiddenTables {get;set;}=[];
    public double FullWidth {get;set;}=1480;
    public double FullHeight {get;set;}=920;
    public double CompactWidth {get;set;}=400;
    public double CompactHeight {get;set;}=760;
    public HashSet<string> Important {get;set;}=[];
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
            if(!double.IsFinite(value.AssistantWidth)||value.AssistantWidth is <280 or >700)value.AssistantWidth=380;
            if(value.View is not("MONTH" or "LIST"))value.View="MONTH";
            value.Completed=Keys(value.Completed);value.Important=Keys(value.Important);
            value.HiddenTables=Keys(value.HiddenTables);if(string.IsNullOrWhiteSpace(value.Nickname)||value.Nickname.Length>40)value.Nickname="我的空间";if(value.Avatar is not("我" or "行" or "旅"))value.Avatar="我";
            if(!double.IsFinite(value.FontSize)||value.FontSize is <12 or >22)value.FontSize=14;
            if(value.LayoutMode is not("AUTO" or "FULL" or "COMPACT"))value.LayoutMode="AUTO";
            if(!ValidSize(value.FullWidth,1120,4000))value.FullWidth=1480;
            if(!ValidSize(value.FullHeight,680,3000))value.FullHeight=920;
            if(!ValidSize(value.CompactWidth,320,4000))value.CompactWidth=400;
            if(!ValidSize(value.CompactHeight,460,3000))value.CompactHeight=760;
            return value;
        }
        catch(Exception ex)when(ex is JsonException or IOException or UnauthorizedAccessException){return new();}
    }
    static HashSet<string> Keys(HashSet<string>? keys)=>new((keys??[]).Where(k=>!string.IsNullOrWhiteSpace(k)&&k.Length<200));
    static bool ValidSize(double value,double minimum,double maximum)=>double.IsFinite(value)&&value>=minimum&&value<=maximum;
    public BoardPreferences Update(Action<BoardPreferences> change){var value=Load();change(value);Save(value);return value;}
    public void Save(BoardPreferences value)
    {
        if(value.View is not("MONTH" or "LIST")||value.Completed is null||value.Important is null||value.LayoutMode is not("AUTO" or "FULL" or "COMPACT")||!ValidSize(value.FontSize,12,22)||!ValidSize(value.FullWidth,1120,4000)||!ValidSize(value.FullHeight,680,3000)||!ValidSize(value.CompactWidth,320,4000)||!ValidSize(value.CompactHeight,460,3000))throw new ArgumentException("看板显示设置不合法。");
        Directory.CreateDirectory(directory);var temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{File.WriteAllText(temp,JsonSerializer.Serialize(value,JsonKit.Options));File.Move(temp,path,true);}finally{if(File.Exists(temp))File.Delete(temp);}
    }
}
