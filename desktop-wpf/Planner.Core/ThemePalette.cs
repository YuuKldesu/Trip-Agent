namespace Planner.Core;
public static class ThemePalette
{
    public static readonly string[] Themes=["暖色","浅色","深色"];
    public static readonly Dictionary<string,string> Accents=new(){["珊瑚"]="#B76F49",["蓝色"]="#2563EB",["绿色"]="#21815C",["紫色"]="#8052B8"};
    public static Dictionary<string,string> Get(string theme,string accent)
    {
        bool dark=theme=="深色",warm=theme=="暖色";
        var p=new Dictionary<string,string>{["Canvas"]=dark?"#1E1E1E":warm?"#F5F3F0":"#F4F5F7",["Surface"]=dark?"#252526":warm?"#FFFDF9":"#FFFFFF",["Panel"]=dark?"#202020":warm?"#F1EEEA":"#ECEEF2",["Text"]=dark?"#E4E4E7":"#252B37",["Muted"]=dark?"#B0B2BA":"#626B79",["Border"]=dark?"#45464C":warm?"#E2D9CE":"#D8DDE5",["Hover"]=dark?"#34353B":"#E7EAF0",["Selected"]=dark?"#3B4052":"#DFE8F7",["Warning"]=dark?"#F3BC78":"#966128",["Danger"]=dark?"#F19A97":"#AF5146",["Scroll"]=dark?"#777981":"#ADB1B8",["Tip"]=dark?"#45474F":"#303030",["Success"]=dark?"#8FCCA7":"#55715F",["OnAccent"]="#FFFFFF"};
        p["Accent"]=Accents.GetValueOrDefault(accent,Accents["珊瑚"]);if(dark)p["Accent"]=accent switch{"蓝色"=>"#7BA6FF","绿色"=>"#69CBA4","紫色"=>"#BD9CEB",_=>"#E5A27D"};return p;
    }
}
