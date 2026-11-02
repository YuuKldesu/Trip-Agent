using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Planner.Desktop;

public static class ShellIcons
{
	public const string Menu = "\ue700";

	public const string Board = "\ue787";

	public const string Tables = "\ue8f1";

	public const string Map = "\ue707";

	public const string Assistant = "\ue99a";

	public const string Profile = "\ue77b";

	public const string Settings = "\ue713";

	public static Viewbox Glyph(string value, double size = 20.0)
	{
		return new Viewbox
		{
			Width = size + 4.0,
			Height = size + 4.0,
			Stretch = Stretch.Uniform,
			VerticalAlignment = VerticalAlignment.Center,
			HorizontalAlignment = HorizontalAlignment.Center,
			Child = new TextBlock
			{
				Text = value,
				FontFamily = new FontFamily("Segoe MDL2 Assets"),
				FontSize = size,
				Margin = new Thickness(1.0),
				TextWrapping = TextWrapping.NoWrap
			}
		};
	}

	public static Button Button(string glyph, string label, Action click, string tag, bool expanded = false)
	{
		StackPanel stackPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal
		};
		stackPanel.Children.Add((glyph == "\ue700") ? SidebarMark() : Glyph(glyph));
		if (expanded)
		{
			stackPanel.Children.Add(new TextBlock
			{
				Text = label,
				Margin = new Thickness(12.0, 0.0, 0.0, 0.0),
				VerticalAlignment = VerticalAlignment.Center,
				MaxWidth = 126.0
			});
		}
		Button button = Ui.Button("", click);
		button.Content = stackPanel;
		button.Tag = tag;
		button.Style = (Style)Application.Current.FindResource("NavigationIconButton");
		button.MinHeight = 44.0;
		button.Padding = (expanded ? new Thickness(10.0) : new Thickness(6.0));
		button.Margin = new Thickness(0.0, 2.0, 0.0, 2.0);
		button.HorizontalContentAlignment = ((!expanded) ? HorizontalAlignment.Center : HorizontalAlignment.Left);
		StackPanel stackPanel2 = new StackPanel
		{
			Orientation = Orientation.Horizontal
		};
		stackPanel2.Children.Add(new TextBlock
		{
			Text = label,
			VerticalAlignment = VerticalAlignment.Center
		});
		if (tag == "NavToggle")
		{
			stackPanel2.Children.Add(new Border
			{
				CornerRadius = new CornerRadius(8.0),
				Background = Ui.Brush("#606060"),
				Padding = new Thickness(7.0, 2.0, 7.0, 2.0),
				Margin = new Thickness(10.0, 0.0, 0.0, 0.0),
				Child = new TextBlock
				{
					Text = "Ctrl+B",
					Foreground = Brushes.White,
					FontSize = 12.0,
					FontWeight = FontWeights.SemiBold
				}
			});
		}
		button.ToolTip = new ToolTip
		{
			Content = stackPanel2,
			Placement = PlacementMode.Right,
			HorizontalOffset = 8.0
		};
		ToolTipService.SetInitialShowDelay(button, 300);
		ToolTipService.SetShowDuration(button, 10000);
		AutomationProperties.SetName(button, label);
		return button;
	}

	private static FrameworkElement SidebarMark()
	{
		return new Path
		{
			Data = Geometry.Parse("M5,2 L19,2 A3,3 0 0 1 22,5 L22,19 A3,3 0 0 1 19,22 L5,22 A3,3 0 0 1 2,19 L2,5 A3,3 0 0 1 5,2 M9,2 L9,22"),
			Stroke = Ui.Brush("#686868"),
			StrokeThickness = 1.7,
			StrokeStartLineCap = PenLineCap.Round,
			StrokeEndLineCap = PenLineCap.Round,
			Width = 19.0,
			Height = 19.0,
			Stretch = Stretch.Uniform
		};
	}
}
