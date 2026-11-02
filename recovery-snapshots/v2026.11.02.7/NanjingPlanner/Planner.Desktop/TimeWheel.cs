using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Planner.Core;

namespace Planner.Desktop;

public sealed class TimeWheel : Border
{
	private readonly bool hours;

	private readonly Func<DateTime> current;

	private readonly Action<DateTime> changed;

	private readonly StackPanel rows = new StackPanel();

	private Point anchor;

	private double residual;

	private bool dragging;

	private bool moved;

	private TouchDevice? touch;

	private double RowHeight => Math.Max(44.0, Math.Ceiling(36.0 * Typography.Scale));

	public TimeWheel(bool hours, Func<DateTime> current, Action<DateTime> changed)
	{
		this.hours = hours;
		this.current = current;
		this.changed = changed;
		Width = 100.0;
		Height = 220.0;
		Child = rows;
		Background = Brushes.Transparent;
		Focusable = true;
		Refresh();
		PreviewMouseWheel += (object _, MouseWheelEventArgs e) =>
		{
			RollBy((e.Delta <= 0) ? 1 : (-1));
			e.Handled = true;
		};
		PreviewMouseLeftButtonDown += (object _, MouseButtonEventArgs e) =>
		{
			if (e.StylusDevice == null)
			{
				anchor = e.GetPosition(this);
				residual = 0.0;
				moved = false;
				dragging = true;
				CaptureMouse();
				Focus();
				e.Handled = true;
			}
		};
		PreviewMouseMove += (object _, MouseEventArgs e) =>
		{
			if (dragging && e.LeftButton == MouseButtonState.Pressed)
			{
				Drag(e.GetPosition(this));
				e.Handled = true;
			}
		};
		PreviewMouseLeftButtonUp += (object _, MouseButtonEventArgs e) =>
		{
			if (dragging)
			{
				dragging = false;
				if (!moved)
				{
					RollBy(Math.Clamp((int)(e.GetPosition(this).Y / RowHeight) - 2, -2, 2));
				}
				ReleaseMouseCapture();
				e.Handled = true;
			}
		};
		LostMouseCapture += (object _, MouseEventArgs _) =>
		{
			dragging = false;
		};
		PreviewTouchDown += (object? _, TouchEventArgs e) =>
		{
			touch = e.TouchDevice;
			anchor = e.GetTouchPoint(this).Position;
			residual = 0.0;
			moved = false;
			CaptureTouch(touch);
			e.Handled = true;
		};
		PreviewTouchMove += (object? _, TouchEventArgs e) =>
		{
			if (touch == e.TouchDevice)
			{
				Drag(e.GetTouchPoint(this).Position);
				e.Handled = true;
			}
		};
		PreviewTouchUp += (object? _, TouchEventArgs e) =>
		{
			if (touch == e.TouchDevice)
			{
				if (!moved)
				{
					RollBy(Math.Clamp((int)(e.GetTouchPoint(this).Position.Y / RowHeight) - 2, -2, 2));
				}
				ReleaseTouchCapture(touch);
				touch = null;
				e.Handled = true;
			}
		};
		LostTouchCapture += (object? _, TouchEventArgs _) =>
		{
			touch = null;
		};
		PreviewKeyDown += (object _, KeyEventArgs e) =>
		{
			Key key = e.Key;
			if ((key == Key.Up || key == Key.Down) ? true : false)
			{
				RollBy((e.Key != Key.Up) ? 1 : (-1));
				e.Handled = true;
			}
		};
	}

	private void Drag(Point point)
	{
		double num = point.Y - anchor.Y;
		if (Math.Abs(num) > 5.0)
		{
			moved = true;
		}
		residual -= num;
		anchor = point;
		int num2 = (int)(residual / RowHeight);
		if (num2 != 0)
		{
			residual -= (double)num2 * RowHeight;
			RollBy(num2);
		}
	}

	public void RollBy(int steps)
	{
		if (steps != 0)
		{
			Ui.Safe(() =>
			{
				changed(DateTimeSelection.Roll(current(), hours, steps));
			});
		}
	}

	public void Refresh()
	{
		rows.Children.Clear();
		Height = RowHeight * 5.0;
		DateTime dateTime = current();
		for (int i = -2; i <= 2; i++)
		{
			int delta = i;
			DateTime dateTime2;
			try
			{
				dateTime2 = DateTimeSelection.Roll(dateTime, hours, i);
			}
			catch (ArgumentException)
			{
				dateTime2 = dateTime;
			}
			Button button = Ui.Button((hours ? dateTime2.Hour : dateTime2.Minute).ToString("00"), () =>
			{
				RollBy(delta);
			});
			button.Height = RowHeight;
			button.MinHeight = RowHeight;
			button.Margin = new Thickness(0.0);
			button.Padding = new Thickness(0.0);
			button.Background = Ui.Brush((i == 0) ? "#F0E3D5" : "#FFFDF9");
			button.Foreground = Ui.Brush((i == 0) ? "#8E5339" : "#8C7F73");
			Typography.Set(button, (i == 0) ? 28 : 21);
			Button button2 = button;
			double opacity;
			if (Math.Abs(i) == 2)
			{
				opacity = 0.35;
			}
			else
			{
				opacity = ((i == 0) ? 1.0 : 0.7);
			}
			button2.Opacity = opacity;
			rows.Children.Add(button);
		}
	}
}
