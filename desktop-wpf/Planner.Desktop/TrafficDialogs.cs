using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Planner.Core;

namespace Planner.Desktop;

public static class TrafficDialogs
{
	private static async Task<T> Show<T>(Window owner, string title, Action<DockPanel, Action<T>, Action> build, CancellationToken token)
	{
		token.ThrowIfCancellationRequested();
		TaskCompletionSource<T> result = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
		Window w = new Window
		{
			Owner = owner,
			Title = title,
			Width = 520.0,
			Height = 310.0,
			MinWidth = 380.0,
			MinHeight = 240.0,
			WindowStartupLocation = WindowStartupLocation.CenterOwner,
			Background = AppTheme.Brush("Canvas")
		};
		DockPanel dockPanel = new DockPanel
		{
			Margin = new Thickness(22.0)
		};
		w.Content = dockPanel;
		build(dockPanel, (T value) =>
		{
			result.TrySetResult(value);
			w.Close();
		}, w.Close);
		w.Closed += (object? _, EventArgs _) =>
		{
			result.TrySetCanceled();
		};
		using (token.Register(() =>
		{
			w.Dispatcher.BeginInvoke((Action)(() =>
			{
				w.Close();
			}));
		}))
		{
			w.Show();
			return await result.Task;
		}
	}

	public static Task<string> Location(Window owner, string activity, CancellationToken token)
	{
		return Show(owner, "补充地点", (DockPanel root, Action<string> done, Action close) =>
		{
			WrapPanel wrapPanel = new WrapPanel
			{
				HorizontalAlignment = HorizontalAlignment.Right
			};
			DockPanel.SetDock(wrapPanel, Dock.Bottom);
			root.Children.Add(wrapPanel);
			StackPanel stackPanel = new StackPanel();
			root.Children.Add(stackPanel);
			stackPanel.Children.Add(Ui.Text("“" + activity + "”在哪里？", 18.0, bold: true));
			stackPanel.Children.Add(Ui.Text("填写地点后搜索高德候选；取消可返回编辑。", 12.0));
			TextBox input = Ui.Input();
			stackPanel.Children.Add(input);
			wrapPanel.Children.Add(Ui.Button("返回编辑", close));
			wrapPanel.Children.Add(Ui.Button("搜索地点", () =>
			{
				if (!string.IsNullOrWhiteSpace(input.Text))
				{
					done(input.Text.Trim());
				}
			}, primary: true));
		}, token);
	}

	public static Task<string> Mode(Window owner, TrafficPair pair, string? previous, CancellationToken token)
	{
		return Show(owner, "这段路怎么去", (DockPanel root, Action<string> done, Action close) =>
		{
			WrapPanel wrapPanel = new WrapPanel
			{
				HorizontalAlignment = HorizontalAlignment.Right
			};
			DockPanel.SetDock(wrapPanel, Dock.Bottom);
			root.Children.Add(wrapPanel);
			StackPanel stackPanel = new StackPanel();
			root.Children.Add(stackPanel);
			stackPanel.Children.Add(Ui.Text(pair.From.Title + " → " + pair.To.Title, 18.0, bold: true));
			stackPanel.Children.Add(Ui.Text($"可用间隔 {pair.GapMinutes} 分钟；此选择只用于这段路。", 12.0));
			string[] choices = new string[4] { "公交", "步行", "骑行", "驾车" };
			ComboBox mode = Ui.Choices(new string[4] { "公交（含地铁与步行）", "步行", "骑行", "驾车" }, Math.Max(0, Array.IndexOf(choices, previous)));
			mode.Tag = "TrafficMode";
			stackPanel.Children.Add(mode);
			wrapPanel.Children.Add(Ui.Button("返回编辑", close));
			wrapPanel.Children.Add(Ui.Button("查询这段路", () =>
			{
				done(choices[mode.SelectedIndex]);
			}, primary: true));
		}, token);
	}

	public static Task<bool> SaveWarning(Window owner, string message, CancellationToken token)
	{
		return Show(owner, "检查前后行程", (DockPanel root, Action<bool> done, Action close) =>
		{
			WrapPanel wrapPanel = new WrapPanel
			{
				HorizontalAlignment = HorizontalAlignment.Right
			};
			DockPanel.SetDock(wrapPanel, Dock.Bottom);
			root.Children.Add(wrapPanel);
			wrapPanel.Children.Add(Ui.Button("返回修改", () =>
			{
				done(obj: false);
			}));
			wrapPanel.Children.Add(Ui.Button("仍然保存", () =>
			{
				done(obj: true);
			}, primary: true));
			root.Children.Add(new ScrollViewer
			{
				Content = Ui.Text(message),
				VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
				HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
			});
		}, token);
	}
}
