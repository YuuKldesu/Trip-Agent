using System.Collections.Generic;

namespace Planner.Core;

public static class ThemePalette
{
	public static readonly string[] Themes = new string[4] { "暖色", "浅色", "深色", "万圣节" };

	public static readonly Dictionary<string, string> Accents = new Dictionary<string, string>
	{
		["珊瑚"] = "#B76F49",
		["蓝色"] = "#2563EB",
		["绿色"] = "#21815C",
		["紫色"] = "#8052B8"
	};

	public static bool IsDark(string theme)
	{
		if (theme == "深色" || theme == "万圣节")
		{
			return true;
		}
		return false;
	}

	public static Dictionary<string, string> Get(string theme, string accent)
	{
		bool flag = IsDark(theme);
		bool flag2 = theme == "暖色";
		Dictionary<string, string> dictionary = new Dictionary<string, string>
		{
			["Canvas"] = (flag ? "#1E1E1E" : (flag2 ? "#F5F3F0" : "#F4F5F7")),
			["Surface"] = (flag ? "#252526" : (flag2 ? "#FFFDF9" : "#FFFFFF")),
			["Panel"] = (flag ? "#202020" : (flag2 ? "#F1EEEA" : "#ECEEF2")),
			["Text"] = (flag ? "#E4E4E7" : "#252B37"),
			["Muted"] = (flag ? "#B0B2BA" : "#626B79"),
			["Border"] = (flag ? "#45464C" : (flag2 ? "#E2D9CE" : "#D8DDE5")),
			["Hover"] = (flag ? "#34353B" : "#E7EAF0"),
			["Selected"] = (flag ? "#3B4052" : "#DFE8F7"),
			["Warning"] = (flag ? "#F3BC78" : "#966128"),
			["Danger"] = (flag ? "#F19A97" : "#AF5146"),
			["Scroll"] = (flag ? "#777981" : "#ADB1B8"),
			["Tip"] = (flag ? "#45474F" : "#303030"),
			["Success"] = (flag ? "#8FCCA7" : "#55715F"),
			["OnAccent"] = "#FFFFFF"
		};
		dictionary["Accent"] = Accents.GetValueOrDefault(accent, Accents["珊瑚"]);
		if (flag)
		{
			Dictionary<string, string> dictionary2 = dictionary;
			dictionary2["Accent"] = accent switch
			{
				"蓝色" => "#7BA6FF",
				"绿色" => "#69CBA4",
				"紫色" => "#BD9CEB",
				_ => "#E5A27D",
			};
		}
		if (theme == "万圣节")
		{
			dictionary["Canvas"] = "#191320";
			dictionary["Surface"] = "#251C2E";
			dictionary["Panel"] = "#201726";
			dictionary["Text"] = "#F8EDD9";
			dictionary["Muted"] = "#C5B4CC";
			dictionary["Border"] = "#594362";
			dictionary["Hover"] = "#3B2946";
			dictionary["Selected"] = "#50334E";
			dictionary["Accent"] = "#FFAB5C";
			dictionary["OnAccent"] = "#241329";
			dictionary["Warning"] = "#FFD080";
			dictionary["Danger"] = "#FFABA5";
			dictionary["Success"] = "#ACD8B3";
			dictionary["Scroll"] = "#9A7CA5";
			dictionary["Tip"] = "#3B2946";
		}
		return dictionary;
	}
}
