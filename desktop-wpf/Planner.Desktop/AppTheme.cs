using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Planner.Core;

namespace Planner.Desktop;

public static class AppTheme
{
	private sealed class ColorSource : INotifyPropertyChanged
	{
		public Color Value { get; private set; }

		public event PropertyChangedEventHandler? PropertyChanged;

		public ColorSource(Color value)
		{
			Value = value;
		}

		public void Set(Color value)
		{
			if (!(Value == value))
			{
				Value = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Value"));
			}
		}
	}

	private static string lastTheme;

	private static string lastAccent;

	private static Dictionary<string, string> palette;

	private static readonly Dictionary<string, ColorSource> sources;

	private static readonly Dictionary<string, SolidColorBrush> brushes;

	private static readonly Dictionary<string, SolidColorBrush> tableText;

	private static readonly Dictionary<string, string> roles;

	private static SolidColorBrush Live(string key, Color color)
	{
		ColorSource colorSource = new ColorSource(color);
		sources[key] = colorSource;
		SolidColorBrush solidColorBrush = new SolidColorBrush();
		BindingOperations.SetBinding(solidColorBrush, SolidColorBrush.ColorProperty, new Binding("Value")
		{
			Source = colorSource
		});
		return solidColorBrush;
	}

	static AppTheme()
	{
		lastTheme = "";
		lastAccent = "";
		palette = ThemePalette.Get("暖色", "珊瑚");
		sources = new Dictionary<string, ColorSource>();
		brushes = new Dictionary<string, SolidColorBrush>();
		tableText = new Dictionary<string, SolidColorBrush>();
		roles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		Register("Canvas", "#FBF7F0 #F5F3F0");
		Register("Surface", "#FFFDF9 #FFFAF6 #FAFAFA #FFFFFF");
		Register("Panel", "#F1EEEA #F6F0E7 #F0E8DC #F0E3D5 #F0E8DE #EEEAE5 #EEEDEB #F0ECE5 #EEDFD0");
		Register("Text", "#44382F #443A32 #443B35 #555555 #686868");
		Register("Muted", "#8B7867 #68605A #77716A #8C7F73 #A39A90 #B8978C");
		Register("Border", "#E5D8CA #E6E1DB #DCCDBD #E4D6C8 #AAA49C #E5DBCF #E5DDD3 #E4E0DB #A98164");
		Register("Hover", "#F2E6D7 #E2DFDA #D8D4CF #E7D7C7");
		Register("Selected", "#E5D5C5 #E5E1DC #EEE7DF #F4DFD9 #FAECE6");
		Register("Accent", "#B76F49 #AC5A4C #C65B40 #9B5142 #8E5339");
		Register("Warning", "#A47820 #986638 #B5A08A #886047");
		Register("Success", "#55715F");
		Register("Danger", "#B95D4D #AF5146 #A45243");
		Register("Scroll", "#B6B3AF #8E8B87 #777470 #606060");
		Register("Tip", "#303030");
		static void Register(string role, string colors)
		{
			string[] array = colors.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			foreach (string key in array)
			{
				roles[key] = role;
			}
		}
	}

	public static Brush Color(string hex)
	{
		if (!roles.TryGetValue(hex, out string value))
		{
			return Raw(hex);
		}
		return Brush(value);
	}

	public static SolidColorBrush Raw(string hex)
	{
		return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
	}

	public static SolidColorBrush Brush(string role)
	{
		if (!brushes.TryGetValue(role, out SolidColorBrush value))
		{
			value = (brushes[role] = Live(role, (Color)ColorConverter.ConvertFromString(palette.GetValueOrDefault(role, palette["Text"]))));
		}
		return value;
	}

	private static Color TableTextColor(string hex)
	{
		Color result = (Color)ColorConverter.ConvertFromString(hex);
		if (!ThemePalette.IsDark(lastTheme))
		{
			return result;
		}
		return System.Windows.Media.Color.FromRgb((byte)((result.R + 255) / 2), (byte)((result.G + 255) / 2), (byte)((result.B + 255) / 2));
	}

	public static Brush TableText(string hex)
	{
		if (!tableText.TryGetValue(hex, out SolidColorBrush value))
		{
			value = (tableText[hex] = Live("table:" + hex, TableTextColor(hex)));
		}
		return value;
	}

	public static void Apply(string theme, string accent)
	{
		if (lastTheme == theme && lastAccent == accent)
		{
			Application current = Application.Current;
			if (current != null && current.Resources.Contains("Theme.Text"))
			{
				return;
			}
		}
		lastTheme = theme;
		lastAccent = accent;
		palette = ThemePalette.Get(theme, accent);
		foreach (KeyValuePair<string, SolidColorBrush> item in tableText)
		{
			sources["table:" + item.Key].Set(TableTextColor(item.Key));
		}
		foreach (string key in palette.Keys)
		{
			SolidColorBrush value = Brush(key);
			sources[key].Set((Color)ColorConverter.ConvertFromString(palette[key]));
			Application current4 = Application.Current;
			if (current4 != null)
			{
				current4.Resources["Theme." + key] = value;
			}
		}
	}
}
