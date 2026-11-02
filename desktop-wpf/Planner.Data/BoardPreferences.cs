using System.Collections.Generic;

namespace Planner.Data;

public sealed class BoardPreferences
{
	public string Theme { get; set; } = "暖色";

	public string AccentColor { get; set; } = "珊瑚";

	public double FontSize { get; set; } = 14.0;

	public string LayoutMode { get; set; } = "AUTO";

	public bool AlwaysOnTop { get; set; }

	public string Nickname { get; set; } = "我的空间";

	public string Avatar { get; set; } = "我";

	public bool SidebarExpanded { get; set; } = true;

	public bool TablesPanelOpen { get; set; } = true;

	public bool AssistantPanelOpen { get; set; } = true;

	public double AssistantWidth { get; set; } = 280.0;

	public HashSet<string> HiddenTables { get; set; } = new HashSet<string>();

	public double FullWidth { get; set; } = 1480.0;

	public double FullHeight { get; set; } = 920.0;

	public double CompactWidth { get; set; } = 400.0;

	public double CompactHeight { get; set; } = 760.0;

	public HashSet<string> Important { get; set; } = new HashSet<string>();

	public bool ShowCompleted { get; set; } = true;

	public bool ShowIncomplete { get; set; } = true;

	public string View { get; set; } = "MONTH";

	public HashSet<string> Completed { get; set; } = new HashSet<string>();

	public bool IsVisible(string key)
	{
		if (!Completed.Contains(key))
		{
			return ShowIncomplete;
		}
		return ShowCompleted;
	}
}
