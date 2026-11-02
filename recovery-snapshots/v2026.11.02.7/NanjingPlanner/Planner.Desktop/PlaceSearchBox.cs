using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Planner.Core;
using Planner.Data;

namespace Planner.Desktop;

public sealed class PlaceSearchBox : StackPanel
{
	private readonly Func<string, CancellationToken, Task<List<Place>>> search;

	private CancellationTokenSource? request;

	private long serial;

	private bool setting;

	private bool closed;

	public TextBox QueryInput { get; } = Ui.Input();

	public Button SearchButton { get; }

	public ListBox Results { get; } = new ListBox
	{
		MaxHeight = 180.0,
		MinHeight = 60.0,
		Visibility = Visibility.Collapsed
	};

	public TextBlock Feedback { get; } = Ui.Text("输入地点关键词，点击搜索或按回车。", 12.0);

	public Place? SelectedPlace => Results.SelectedItem as Place;

	public event Action? Changed;

	public PlaceSearchBox(Func<string, CancellationToken, Task<List<Place>>> search)
	{
		this.search = search;
		Ui.Field(this, "地点关键词", QueryInput);
		SearchButton = Ui.Button("搜索地点", () =>
		{
		});
		SearchButton.Tag = "PlaceSearchButton";
		Children.Add(SearchButton);
		Children.Add(Feedback);
		Children.Add(Results);
		FrameworkElementFactory frameworkElementFactory = new FrameworkElementFactory(typeof(TextBlock));
		frameworkElementFactory.SetBinding(TextBlock.TextProperty, new Binding());
		frameworkElementFactory.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
		frameworkElementFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(6.0));
		Results.ItemTemplate = new DataTemplate
		{
			VisualTree = frameworkElementFactory
		};
		QueryInput.TextChanged += (object _, TextChangedEventArgs _) =>
		{
			serial++;
			request?.Cancel();
			setting = true;
			Results.ItemsSource = null;
			setting = false;
			Results.Visibility = Visibility.Collapsed;
			Feedback.Text = "地点已修改，请重新搜索确认。";
			SetBusy(busy: false);
			Changed?.Invoke();
		};
		QueryInput.KeyDown += async (object _, KeyEventArgs e) =>
		{
			if (e.Key == Key.Return)
			{
				e.Handled = true;
				await SearchAsync();
			}
		};
		SearchButton.Click += async (object _, RoutedEventArgs _) =>
		{
			await SearchAsync();
		};
		Results.SelectionChanged += (object _, SelectionChangedEventArgs _) =>
		{
			if (!setting)
			{
				Place selectedPlace = SelectedPlace;
				if (selectedPlace != null)
				{
					Feedback.Text = "已选择：" + selectedPlace?.ToString() + "\n" + selectedPlace.Source;
					Feedback.Foreground = Ui.Brush("#55715F");
				}
				Changed?.Invoke();
			}
		};
		Unloaded += (object _, RoutedEventArgs _) =>
		{
			closed = true;
			serial++;
			request?.Cancel();
		};
		Loaded += (object _, RoutedEventArgs _) =>
		{
			closed = false;
		};
	}

	public void SelectConfirmed(Place place)
	{
		request?.Cancel();
		serial++;
		QueryInput.Text = place.Name;
		setting = true;
		Results.ItemsSource = new Place[1] { JsonKit.Clone(place) };
		Results.SelectedIndex = 0;
		setting = false;
		Results.Visibility = Visibility.Visible;
		Feedback.Text = "已在对话中确认：" + place?.ToString() + "\n" + place.Source;
		Changed?.Invoke();
	}

	private void SetBusy(bool busy)
	{
		SearchButton.IsEnabled = !busy;
		SearchButton.Content = (busy ? "正在搜索…" : "搜索地点");
	}

	public async Task SearchAsync()
	{
		if (closed)
		{
			return;
		}
		request?.Cancel();
		request?.Dispose();
		request = new CancellationTokenSource();
		CancellationToken token = request.Token;
		long own = ++serial;
		string text = QueryInput.Text.Trim();
		setting = true;
		Results.ItemsSource = null;
		setting = false;
		Results.Visibility = Visibility.Collapsed;
		Changed?.Invoke();
		if (text.Length == 0)
		{
			Feedback.Text = "请先输入地点名称。";
			SetBusy(busy: false);
			return;
		}
		Feedback.Foreground = Ui.Brush("#886047");
		Feedback.Text = "正在搜索“" + text + "”…";
		SetBusy(busy: true);
		try
		{
			List<Place> list = await search(text, token);
			if (!closed && own == serial && !token.IsCancellationRequested)
			{
				setting = true;
				Results.ItemsSource = list;
				Results.SelectedIndex = ((list.Count != 1) ? (-1) : 0);
				setting = false;
				Results.Visibility = ((list.Count <= 0) ? Visibility.Collapsed : Visibility.Visible);
				TextBlock feedback = Feedback;
				string text2;
				if (list.Count != 0)
				{
					text2 = ((list.Count == 1) ? ("已选择：" + list[0]?.ToString() + "\n" + list[0].Source) : $"找到{list.Count}个地点，请点击准确名称/地址确认。");
				}
				else
				{
					text2 = "没有找到匹配地点，请尝试景点名称、入口或更短的关键词。";
				}
				feedback.Text = text2;
				Feedback.Foreground = Ui.Brush((list.Count == 0) ? "#A45243" : "#55715F");
				Changed?.Invoke();
			}
		}
		catch (OperationCanceledException)
		{
			if (own == serial && !closed)
			{
				Feedback.Text = "搜索已取消，可以重试。";
			}
		}
		catch (Exception ex2)
		{
			if (own == serial && !closed)
			{
				Feedback.Text = ((ex2 is MapServiceException) ? ex2.Message : ("搜索失败：" + ex2.Message));
				Feedback.Foreground = Ui.Brush("#A45243");
			}
		}
		finally
		{
			if (own == serial && !closed)
			{
				SetBusy(busy: false);
			}
		}
	}
}
