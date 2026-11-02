using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Planner.Core;
using Planner.Data;

namespace Planner.Desktop;

public static class RepositoryDialogs
{
	public static async Task<int> Choose(Window owner, string title, string description, IReadOnlyList<string> options, CancellationToken token)
	{
		token.ThrowIfCancellationRequested();
		if (options.Count == 0)
		{
			throw new ArgumentException("没有可选方案。");
		}
		TaskCompletionSource<int> result = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
		Window w = new Window
		{
			Owner = owner,
			Title = title,
			Width = 660.0,
			Height = 450.0,
			MinWidth = 520.0,
			MinHeight = 300.0,
			WindowStartupLocation = WindowStartupLocation.CenterOwner,
			Background = Ui.Brush("#FBF7F0")
		};
		DockPanel dockPanel = new DockPanel
		{
			Margin = new Thickness(22.0)
		};
		w.Content = dockPanel;
		TextBlock element = Ui.Text(description, 14.0, bold: true);
		DockPanel.SetDock(element, Dock.Top);
		dockPanel.Children.Add(element);
		StackPanel stackPanel = Ui.Row(Ui.Button("取消", w.Close));
		DockPanel.SetDock(stackPanel, Dock.Bottom);
		dockPanel.Children.Add(stackPanel);
		ListBox list = new ListBox
		{
			ItemsSource = options,
			SelectedIndex = 0,
			Margin = new Thickness(0.0, 10.0, 0.0, 10.0)
		};
		dockPanel.Children.Add(list);
		stackPanel.Children.Add(Ui.Button("确认选择", () =>
		{
			if (list.SelectedIndex >= 0)
			{
				result.TrySetResult(list.SelectedIndex);
				w.Close();
			}
		}, primary: true));
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

	public static async Task<Place> ChoosePlace(Window owner, List<Place> places, CancellationToken token)
	{
		if (places.Count == 1)
		{
			return places[0];
		}
		return places[await Choose(owner, "选择准确地点", "请选择具体地点，找不到时取消并换关键词。", places.Select((Place p) => p?.ToString() + " · " + p.Source).ToArray(), token)];
	}

	public static async Task<TravelEstimate> Travel(Window owner, RepositoryMap map, Place from, Place to, string? mode, DateTime departure, CancellationToken token)
	{
		if (!string.IsNullOrWhiteSpace(mode))
		{
			return await map.TravelAsync(from, to, mode, departure, token);
		}
		List<string> failures = new List<string>();
		List<TravelEstimate> options = new List<TravelEstimate>();
		string[] array = new string[4] { "公交", "步行", "骑行", "驾车" };
		foreach (string mode2 in array)
		{
			try
			{
				List<TravelEstimate> list = options;
				list.Add(await map.TravelAsync(from, to, mode2, departure, token));
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (MapServiceException ex2)
			{
				failures.Add(ex2.Message);
			}
			catch (ArgumentException ex3)
			{
				failures.Add(ex3.Message);
			}
			catch (InvalidOperationException ex4)
			{
				failures.Add(ex4.Message);
			}
		}
		if (options.Count == 0)
		{
			throw new InvalidOperationException((failures.Count > 0) ? string.Join("\n", failures.Distinct()) : "路线查询失败。");
		}
		return options[await Choose(owner, "选择交通方案", "从" + from.Name + "到" + to.Name, options.Select((TravelEstimate x) => $"{x.Source} · 约{x.Minutes}分钟").ToArray(), token)];
	}
}
