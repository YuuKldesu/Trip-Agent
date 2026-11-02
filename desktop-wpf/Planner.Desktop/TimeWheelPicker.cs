using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Planner.Desktop;

public sealed class TimeWheelPicker : StackPanel
{
	private DateTime value;

	private readonly TextBlock day = Ui.Text("", 14.0, bold: true);

	private readonly TimeWheel hour;

	private readonly TimeWheel minute;

	private readonly TextBox manual;

	private readonly TextBlock status = Ui.Text("", 11.0);

	public DateTime Value => value;

	public event Action<DateTime>? Confirmed;

	public event Action? Cancelled;

	public TimeWheelPicker(DateTime initial)
	{
		value = initial;
		Width = 290.0;
		Children.Add(Ui.Text("选择时间 · 24小时制", 16.0, bold: true));
		Children.Add(day);
		Grid grid = new Grid
		{
			ColumnDefinitions =
			{
				new ColumnDefinition(),
				new ColumnDefinition()
			}
		};
		TextBlock textBlock = Ui.Text("时", 12.0);
		textBlock.HorizontalAlignment = HorizontalAlignment.Center;
		grid.Children.Add(textBlock);
		TextBlock textBlock2 = Ui.Text("分", 12.0);
		textBlock2.HorizontalAlignment = HorizontalAlignment.Center;
		Grid.SetColumn(textBlock2, 1);
		grid.Children.Add(textBlock2);
		Children.Add(grid);
		Grid grid2 = new Grid
		{
			ColumnDefinitions =
			{
				new ColumnDefinition(),
				new ColumnDefinition
				{
					Width = new GridLength(24.0)
				},
				new ColumnDefinition()
			}
		};
		hour = new TimeWheel(hours: true, () => value, SetValue);
		minute = new TimeWheel(hours: false, () => value, SetValue);
		grid2.Children.Add(hour);
		Grid.SetColumn(minute, 2);
		grid2.Children.Add(minute);
		TextBlock textBlock3 = Ui.Text(":", 26.0, bold: true);
		textBlock3.VerticalAlignment = VerticalAlignment.Center;
		textBlock3.HorizontalAlignment = HorizontalAlignment.Center;
		Grid.SetColumn(textBlock3, 1);
		grid2.Children.Add(textBlock3);
		Children.Add(grid2);
		manual = Ui.Input(value.ToString("HH:mm"));
		manual.Tag = "PickerTimeInput";
		Ui.Field(this, "直接输入（HH:mm）", manual);
		Children.Add(status);
		WrapPanel wrapPanel = new WrapPanel
		{
			HorizontalAlignment = HorizontalAlignment.Right
		};
		wrapPanel.Children.Add(Ui.Button("取消", () =>
		{
			Cancelled?.Invoke();
		}));
		wrapPanel.Children.Add(Ui.Button("确定时间", Confirm, primary: true));
		Children.Add(wrapPanel);
		SetValue(initial);
		manual.KeyDown += (object _, KeyEventArgs e) =>
		{
			if (e.Key == Key.Return)
			{
				Confirm();
				e.Handled = true;
			}
			if (e.Key == Key.Escape)
			{
				Cancelled?.Invoke();
				e.Handled = true;
			}
		};
	}

	private void SetValue(DateTime next)
	{
		value = next;
		day.Text = value.ToString("yyyy/MM/dd dddd", CultureInfo.GetCultureInfo("zh-CN"));
		manual.Text = value.ToString("HH:mm");
		status.Text = "滚动或拖动数字，跨过边界会同步调整日期。";
		hour.Refresh();
		minute.Refresh();
	}

	private void Confirm()
	{
		if (!TimeOnly.TryParseExact(manual.Text.Trim(), "HH:mm", out var result))
		{
			status.Text = "请输入00:00—23:59，例如10:05。";
		}
		else
		{
			Confirmed?.Invoke(value.Date.Add(result.ToTimeSpan()));
		}
	}
}
