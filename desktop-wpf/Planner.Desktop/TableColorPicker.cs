using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Planner.Core;

namespace Planner.Desktop;

public sealed class TableColorPicker : WrapPanel
{
	public string? SelectedColor { get; private set; }

	public event Action? SelectionChanged;

	public TableColorPicker(string? selected = null)
	{
		SelectedColor = selected;
		Render();
	}

	private void Render()
	{
		Children.Clear();
		List<TableColor> list = TablePalette.Colors.ToList();
		string custom = SelectedColor;
		if (custom != null && !list.Any((TableColor tableColor) => tableColor.Hex.Equals(custom, StringComparison.OrdinalIgnoreCase)))
		{
			list.Add(new TableColor("原有自定义颜色", custom));
		}
		foreach (TableColor c in list)
		{
			Ellipse element = new Ellipse
			{
				Width = 24.0,
				Height = 24.0,
				Fill = Ui.RawBrush(c.Hex)
			};
			Grid grid = new Grid();
			grid.Children.Add(element);
			bool flag = string.Equals(c.Hex, SelectedColor, StringComparison.OrdinalIgnoreCase);
			if (flag)
			{
				grid.Children.Add(new TextBlock
				{
					Text = "✓",
					Foreground = Brushes.White,
					HorizontalAlignment = HorizontalAlignment.Center,
					VerticalAlignment = VerticalAlignment.Center
				});
			}
			Button button = Ui.Button("", () =>
			{
				SelectedColor = c.Hex;
				Render();
				SelectionChanged?.Invoke();
			});
			button.Content = grid;
			button.Tag = "TableColor:" + c.Hex;
			button.ToolTip = c.Name;
			button.Width = 44.0;
			button.Height = 42.0;
			button.Margin = new Thickness(2.0);
			button.Padding = new Thickness(6.0);
			button.Background = (flag ? Ui.Brush("#E7D7C7") : Brushes.Transparent);
			AutomationProperties.SetName(button, c.Name);
			Children.Add(button);
		}
	}
}
