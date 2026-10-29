using System.Windows;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Media;
using Planner.Core;
namespace Planner.Desktop;
public static class AppTheme
{
    static string lastTheme="",lastAccent="";
    static Dictionary<string,string> palette=ThemePalette.Get("暖色","珊瑚");
    sealed class ColorSource:System.ComponentModel.INotifyPropertyChanged{public System.Windows.Media.Color Value {get;private set;}public event PropertyChangedEventHandler? PropertyChanged;public ColorSource(System.Windows.Media.Color value){Value=value;}public void Set(System.Windows.Media.Color value){if(Value==value)return;Value=value;PropertyChanged?.Invoke(this,new(nameof(Value)));}}
    static readonly Dictionary<string,ColorSource> sources=[];
    static SolidColorBrush Live(string key,System.Windows.Media.Color color){var source=new ColorSource(color);sources[key]=source;var brush=new SolidColorBrush();BindingOperations.SetBinding(brush,SolidColorBrush.ColorProperty,new Binding(nameof(ColorSource.Value)){Source=source});return brush;}
    static readonly Dictionary<string,SolidColorBrush> brushes=[];
    static readonly Dictionary<string,SolidColorBrush> tableText=[];
    static readonly Dictionary<string,string> roles=new(StringComparer.OrdinalIgnoreCase);
    static AppTheme()
    {
        void Register(string role,string colors){foreach(var color in colors.Split(' ',StringSplitOptions.RemoveEmptyEntries))roles[color]=role;}
        Register("Canvas","#FBF7F0 #F5F3F0");Register("Surface","#FFFDF9 #FFFAF6 #FAFAFA #FFFFFF");
        Register("Panel","#F1EEEA #F6F0E7 #F0E8DC #F0E3D5 #F0E8DE #EEEAE5 #EEEDEB #F0ECE5 #EEDFD0");
        Register("Text","#44382F #443A32 #443B35 #555555 #686868");Register("Muted","#8B7867 #68605A #77716A #8C7F73 #A39A90 #B8978C");
        Register("Border","#E5D8CA #E6E1DB #DCCDBD #E4D6C8 #AAA49C #E5DBCF #E5DDD3 #E4E0DB #A98164");
        Register("Hover","#F2E6D7 #E2DFDA #D8D4CF #E7D7C7");Register("Selected","#E5D5C5 #E5E1DC #EEE7DF #F4DFD9 #FAECE6");
        Register("Accent","#B76F49 #AC5A4C #C65B40 #9B5142 #8E5339");Register("Warning","#A47820 #986638 #B5A08A #886047");Register("Success","#55715F");Register("Danger","#B95D4D #AF5146 #A45243");Register("Scroll","#B6B3AF #8E8B87 #777470 #606060");Register("Tip","#303030");
    }
    public static Brush Color(string hex)=>roles.TryGetValue(hex,out var role)?Brush(role):Raw(hex);
    public static SolidColorBrush Raw(string hex)=>new((Color)ColorConverter.ConvertFromString(hex));
    public static SolidColorBrush Brush(string role){if(!brushes.TryGetValue(role,out var brush))brushes[role]=brush=Live(role,(System.Windows.Media.Color)ColorConverter.ConvertFromString(palette.GetValueOrDefault(role,palette["Text"])));return brush;}
    static System.Windows.Media.Color TableTextColor(string hex){var c=(System.Windows.Media.Color)ColorConverter.ConvertFromString(hex);return palette["Canvas"]=="#1E1E1E"?System.Windows.Media.Color.FromRgb((byte)((c.R+255)/2),(byte)((c.G+255)/2),(byte)((c.B+255)/2)):c;}
    public static Brush TableText(string hex){if(!tableText.TryGetValue(hex,out var brush))tableText[hex]=brush=Live("table:"+hex,TableTextColor(hex));return brush;}
    public static void Apply(string theme,string accent)
    {
        if(lastTheme==theme&&lastAccent==accent&&Application.Current?.Resources.Contains("Theme.Text")==true)return;lastTheme=theme;lastAccent=accent;
        palette=ThemePalette.Get(theme,accent);foreach(var entry in tableText)sources["table:"+entry.Key].Set(TableTextColor(entry.Key));foreach(var role in palette.Keys){var brush=Brush(role);sources[role].Set((System.Windows.Media.Color)ColorConverter.ConvertFromString(palette[role]));if(Application.Current is {} app)app.Resources["Theme."+role]=brush;}
    }
}
