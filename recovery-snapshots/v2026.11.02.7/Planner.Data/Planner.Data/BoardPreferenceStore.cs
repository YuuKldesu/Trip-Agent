using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Planner.Core;

namespace Planner.Data;

public sealed class BoardPreferenceStore(string directory)
{
	private readonly string path = Path.Combine(directory, "board.json");

	public BoardPreferences Load()
	{
		try
		{
			if (!File.Exists(path))
			{
				return new BoardPreferences();
			}
			BoardPreferences boardPreferences = JsonSerializer.Deserialize<BoardPreferences>(File.ReadAllText(path), JsonKit.Options) ?? new BoardPreferences();
			if (!ThemePalette.Themes.Contains(boardPreferences.Theme))
			{
				boardPreferences.Theme = "暖色";
			}
			if (!ThemePalette.Accents.ContainsKey(boardPreferences.AccentColor))
			{
				boardPreferences.AccentColor = "珊瑚";
			}
			bool flag = !double.IsFinite(boardPreferences.AssistantWidth);
			if (!flag)
			{
				double assistantWidth = boardPreferences.AssistantWidth;
				bool flag2 = ((assistantWidth < 280.0 || assistantWidth > 700.0) ? true : false);
				flag = flag2;
			}
			if (flag)
			{
				boardPreferences.AssistantWidth = 280.0;
			}
			string view = boardPreferences.View;
			if ((!(view == "MONTH") && !(view == "LIST")) || 1 == 0)
			{
				boardPreferences.View = "MONTH";
			}
			boardPreferences.Completed = Keys(boardPreferences.Completed);
			boardPreferences.Important = Keys(boardPreferences.Important);
			boardPreferences.HiddenTables = Keys(boardPreferences.HiddenTables);
			if (string.IsNullOrWhiteSpace(boardPreferences.Nickname) || boardPreferences.Nickname.Length > 40)
			{
				boardPreferences.Nickname = "我的空间";
			}
			switch (boardPreferences.Avatar)
			{
			case "我":
			case "行":
			case "旅":
				flag = true;
				break;
			default:
				flag = false;
				break;
			}
			if (!flag)
			{
				boardPreferences.Avatar = "我";
			}
			flag = !double.IsFinite(boardPreferences.FontSize);
			if (!flag)
			{
				double assistantWidth = boardPreferences.FontSize;
				bool flag2 = ((assistantWidth < 12.0 || assistantWidth > 22.0) ? true : false);
				flag = flag2;
			}
			if (flag)
			{
				boardPreferences.FontSize = 14.0;
			}
			switch (boardPreferences.LayoutMode)
			{
			case "AUTO":
			case "FULL":
			case "COMPACT":
				flag = true;
				break;
			default:
				flag = false;
				break;
			}
			if (!flag)
			{
				boardPreferences.LayoutMode = "AUTO";
			}
			if (!ValidSize(boardPreferences.FullWidth, 1120.0, 4000.0))
			{
				boardPreferences.FullWidth = 1480.0;
			}
			if (!ValidSize(boardPreferences.FullHeight, 680.0, 3000.0))
			{
				boardPreferences.FullHeight = 920.0;
			}
			if (!ValidSize(boardPreferences.CompactWidth, 320.0, 4000.0))
			{
				boardPreferences.CompactWidth = 400.0;
			}
			if (!ValidSize(boardPreferences.CompactHeight, 460.0, 3000.0))
			{
				boardPreferences.CompactHeight = 760.0;
			}
			return boardPreferences;
		}
		catch (Exception ex) when ((ex is JsonException || ex is IOException || ex is UnauthorizedAccessException) ? true : false)
		{
			return new BoardPreferences();
		}
	}

	private static HashSet<string> Keys(HashSet<string>? keys)
	{
		return new HashSet<string>((keys ?? new HashSet<string>()).Where((string k) => !string.IsNullOrWhiteSpace(k) && k.Length < 200));
	}

	private static bool ValidSize(double value, double minimum, double maximum)
	{
		if (double.IsFinite(value) && value >= minimum)
		{
			return value <= maximum;
		}
		return false;
	}

	public BoardPreferences Update(Action<BoardPreferences> change)
	{
		BoardPreferences boardPreferences = Load();
		change(boardPreferences);
		Save(boardPreferences);
		return boardPreferences;
	}

	public void Save(BoardPreferences value)
	{
		string view = value.View;
		bool flag = ((view == "MONTH" || view == "LIST") ? true : false);
		bool flag2 = !flag || value.Completed == null || value.Important == null;
		if (!flag2)
		{
			bool flag3;
			switch (value.LayoutMode)
			{
			case "AUTO":
			case "FULL":
			case "COMPACT":
				flag3 = true;
				break;
			default:
				flag3 = false;
				break;
			}
			flag2 = !flag3;
		}
		if (flag2 || !ValidSize(value.FontSize, 12.0, 22.0) || !ValidSize(value.FullWidth, 1120.0, 4000.0) || !ValidSize(value.FullHeight, 680.0, 3000.0) || !ValidSize(value.CompactWidth, 320.0, 4000.0) || !ValidSize(value.CompactHeight, 460.0, 3000.0))
		{
			throw new ArgumentException("看板显示设置不合法。");
		}
		Directory.CreateDirectory(directory);
		string sourceFileName = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
		try
		{
			File.WriteAllText(sourceFileName, JsonSerializer.Serialize(value, JsonKit.Options));
			File.Move(sourceFileName, path, overwrite: true);
		}
		finally
		{
			if (File.Exists(sourceFileName))
			{
				File.Delete(sourceFileName);
			}
		}
	}
}
