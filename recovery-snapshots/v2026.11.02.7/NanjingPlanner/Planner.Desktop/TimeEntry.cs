using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Planner.Core;

namespace Planner.Desktop;

public sealed class TimeEntry : UserControl
{
	private readonly TextBox input;

	private Popup? popup;

	public string Text
	{
		get
		{
			return input.Text;
		}
		set
		{
			input.Text = value;
		}
	}

	public DateTime DateContext { get; set; } = Clock.Today.ToDateTime(TimeOnly.MinValue);

	public event Action? Changed;

	public event Action<DateTime>? DateTimeChanged;

	public TimeEntry(string value)
	{
		Margin = new Thickness(0.0, 3.0, 0.0, 8.0);
		MinHeight = 36.0;
		Grid grid = new Grid();
		Content = new Border
		{
			CornerRadius = new CornerRadius(8.0),
			Background = AppTheme.Brush("Surface"),
			BorderBrush = AppTheme.Brush("Border"),
			BorderThickness = new Thickness(1.0),
			Child = grid
		};
		input = Ui.Input(value);
		input.Margin = new Thickness(0.0);
		input.Padding = new Thickness(10.0, 6.0, 36.0, 6.0);
		input.BorderThickness = new Thickness(0.0);
		input.Background = Brushes.Transparent;
		input.MinHeight = 34.0;
		grid.Children.Add(input);
		input.TextChanged += (object _, TextChangedEventArgs _) =>
		{
			Changed?.Invoke();
		};
		Viewbox viewbox = ShellIcons.Glyph("\ue823", 16.0);
		viewbox.HorizontalAlignment = HorizontalAlignment.Right;
		viewbox.Margin = new Thickness(0.0, 0.0, 10.0, 0.0);
		viewbox.IsHitTestVisible = false;
		grid.Children.Add(viewbox);
		ToolTip = "点击选择时间，也可输入 HH:mm";
		input.PreviewMouseLeftButtonUp += (object _, MouseButtonEventArgs e) =>
		{
			OpenPicker();
			e.Handled = true;
		};
		PreviewKeyDown += (object _, KeyEventArgs e) =>
		{
			if (e.Key == Key.Escape)
			{
				Popup? popup = this.popup;
				if (popup != null && popup.IsOpen)
				{
					this.popup.IsOpen = false;
					e.Handled = true;
					return;
				}
			}
			if (e.Key == Key.Down && (Keyboard.Modifiers & ModifierKeys.Alt) != 0)
			{
				OpenPicker();
				e.Handled = true;
			}
		};
		Unloaded += (object _, RoutedEventArgs _) =>
		{
			if (popup != null)
			{
				popup.IsOpen = false;
			}
		};
	}

	public void OpenPicker()
	{
		Popup? popup = this.popup;
		if (popup != null && popup.IsOpen)
		{
			this.popup.IsOpen = false;
			return;
		}
		TimeOnly timeOnly = (TimeOnly.TryParseExact(Text, "HH:mm", out var result) ? result : new TimeOnly(9, 0));
		TimeWheelPicker timeWheelPicker = new TimeWheelPicker(DateContext.Date.Add(timeOnly.ToTimeSpan()));
		this.popup = StyledDateEntry.MakePopup(this, timeWheelPicker);
		timeWheelPicker.Confirmed += (DateTime value) =>
		{
			DateContext = value.Date;
			Text = value.ToString("HH:mm");
			DateTimeChanged?.Invoke(value);
			this.popup.IsOpen = false;
		};
		timeWheelPicker.Cancelled += () =>
		{
			this.popup.IsOpen = false;
		};
		this.popup.IsOpen = true;
	}
}
