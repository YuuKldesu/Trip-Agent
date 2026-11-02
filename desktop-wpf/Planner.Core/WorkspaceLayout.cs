using System;
using System.Collections.Generic;
using System.Linq;

namespace Planner.Core;

public sealed class WorkspaceLayout
{
	public bool BoardOpen { get; private set; } = true;

	public bool SettingsOpen { get; private set; }

	public string Page { get; private set; } = "board";

	public bool AssistantOnly
	{
		get
		{
			if (!BoardOpen)
			{
				return !SettingsOpen;
			}
			return false;
		}
	}

	public List<string> OpenPages => (BoardOpen ? new List<string> { "board" } : new List<string>()).Concat((!SettingsOpen) ? Array.Empty<string>() : new string[1] { "settings" }).ToList();

	public void Show(string page)
	{
		if ((!(page == "board") && !(page == "settings")) || 1 == 0)
		{
			throw new ArgumentException("未知页面。");
		}
		if (page == "board")
		{
			BoardOpen = true;
		}
		else
		{
			SettingsOpen = true;
		}
		Page = page;
	}

	public void Close(string page)
	{
		if (page == "board")
		{
			BoardOpen = false;
		}
		else
		{
			if (!(page == "settings"))
			{
				throw new ArgumentException("未知页面。");
			}
			SettingsOpen = false;
		}
		if (Page == page)
		{
			WorkspaceLayout workspaceLayout = this;
			string page2;
			if (BoardOpen)
			{
				page2 = "board";
			}
			else
			{
				page2 = (SettingsOpen ? "settings" : "");
			}
			workspaceLayout.Page = page2;
		}
	}

	public void CloseMany(string anchor, string mode)
	{
		foreach (string item in TabClosing.Targets(OpenPages, anchor, mode))
		{
			Close(item);
		}
	}

	public static double AssistantWidth(double requested, double available)
	{
		return Math.Clamp(requested, 280.0, Math.Min(700.0, Math.Max(280.0, available - 660.0)));
	}
}
