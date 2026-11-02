using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Planner.Desktop;

public static class SeasonalDecorations
{
	private static Image Picture(bool ghost, double size)
	{
		DrawingGroup drawingGroup = new DrawingGroup();
		using (DrawingContext drawingContext = drawingGroup.Open())
		{
			if (ghost)
			{
				drawingContext.DrawGeometry(AppTheme.Raw("#F8EDD9"), null, Geometry.Parse("M 6,30 L 6,15 C 6,1 30,1 30,15 L 30,30 L 24,26 L 19,30 L 14,26 L 9,30 Z"));
				drawingContext.DrawEllipse(AppTheme.Raw("#47304F"), null, new Point(14.0, 16.0), 2.0, 3.0);
				drawingContext.DrawEllipse(AppTheme.Raw("#47304F"), null, new Point(23.0, 16.0), 2.0, 3.0);
			}
			else
			{
				drawingContext.DrawRoundedRectangle(AppTheme.Raw("#85AC78"), null, new Rect(17.0, 2.0, 4.0, 9.0), 2.0, 2.0);
				drawingContext.DrawEllipse(AppTheme.Raw("#DE7233"), null, new Point(11.0, 21.0), 9.0, 12.0);
				drawingContext.DrawEllipse(AppTheme.Raw("#F28B3B"), null, new Point(25.0, 21.0), 9.0, 12.0);
				drawingContext.DrawEllipse(AppTheme.Raw("#FFAB5C"), null, new Point(18.0, 21.0), 9.0, 13.0);
				SolidColorBrush brush = AppTheme.Raw("#2E1C31");
				drawingContext.DrawGeometry(brush, null, Geometry.Parse("M 8,20 L 13,14 L 16,21 Z M 20,21 L 24,14 L 29,20 Z M 10,25 L 15,28 L 18,25 L 22,28 L 27,24 L 24,31 L 14,31 Z"));
			}
		}
		return new Image
		{
			Source = new DrawingImage(drawingGroup),
			Width = size,
			Height = size,
			Stretch = Stretch.Uniform,
			IsHitTestVisible = false
		};
	}

	public static Button Badge()
	{
		int treats = 0;
		Button button = Ui.Button("", () =>
		{
		});
		button.Tag = "HalloweenTreat";
		button.Content = Picture(ghost: false, 26.0);
		button.Width = 36.0;
		button.Height = 36.0;
		button.Padding = new Thickness(4.0);
		button.Margin = new Thickness(2.0);
		button.Background = Brushes.Transparent;
		button.ToolTip = "不给糖就捣蛋";
		AutomationProperties.SetName(button, "万圣节南瓜彩蛋");
		StackPanel stackPanel = new StackPanel
		{
			MaxWidth = 240.0
		};
		stackPanel.Children.Add(Picture(ghost: true, 36.0));
		TextBlock message = Ui.Text("", 13.0, bold: true);
		message.TextAlignment = TextAlignment.Center;
		stackPanel.Children.Add(message);
		Popup popup = StyledDateEntry.MakePopup(button, Ui.Card(stackPanel));
		string[] lines = new string[3] { "得到一颗南瓜糖。今晚也给行程留一点惊喜。", "小幽灵路过：记得给自己留些休息时间。", "不给糖就……帮你核对明天的行程！" };
		button.Click += (object _, RoutedEventArgs _) =>
		{
			message.Text = lines[treats++ % lines.Length];
			popup.IsOpen = true;
		};
		button.Unloaded += (object _, RoutedEventArgs _) =>
		{
			popup.IsOpen = false;
		};
		return button;
	}
}
