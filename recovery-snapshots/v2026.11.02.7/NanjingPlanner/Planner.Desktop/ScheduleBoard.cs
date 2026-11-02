using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Planner.Core;
using Planner.Data;

namespace Planner.Desktop;

public sealed class ScheduleBoard : Grid
{
	private const string BackgroundColor = "#F5F3F0";

	private const string Accent = "#C65B40";

	private readonly PlannerViewModel vm;

	private BoardPreferences preferences;

	private Dictionary<string, TrafficSnapshot> trafficSnapshots = new Dictionary<string, TrafficSnapshot>();

	private readonly Grid header = new Grid();

	private readonly Grid body = new Grid();

	private readonly Button caption;

	private readonly StackPanel monthNavigation = new StackPanel
	{
		Orientation = Orientation.Horizontal
	};

	private readonly Dictionary<string, Button> modeButtons = new Dictionary<string, Button>();

	private ScrollViewer? scroll;

	private Grid? monthGrid;

	private readonly List<(DateOnly Date, FrameworkElement Heading)> listHeadings = new List<(DateOnly, FrameworkElement)>();

	private readonly Dictionary<string, (string Key, UIElement Content, ScrollViewer? Scroll, List<(DateOnly Date, FrameworkElement Heading)> Headings)> cache = new Dictionary<string, (string, UIElement, ScrollViewer, List<(DateOnly, FrameworkElement)>)>();

	private readonly Dictionary<string, (double X, double Y)> scrollPositions = new Dictionary<string, (double, double)>();

	private string displayedView = "";

	private readonly StackPanel modes;

	private readonly WrapPanel filters;

	private readonly CheckBox completedFilter;

	private readonly CheckBox incompleteFilter;

	private readonly TextBlock heading;

	private DateOnly displayToday = Clock.Today;

	private DateOnly renderedToday;

	private bool compact;

	private DateOnly agendaDate = Clock.Today;

	private long renderedRevision = -1L;

	private string renderedVisibility = "";

	private DateOnly month;

	private DateOnly lastSelection;

	private DateOnly listHorizon = Clock.Today.AddDays(180);

	private DateOnly? listJump;

	private DateOnly? listVisibleDate;

	private string view;

	private bool resetScroll = true;

	private bool rendering;

	private bool narrowList;

	public string ViewMode
	{
		get
		{
			if (!compact)
			{
				if (!narrowList || !(view == "MONTH"))
				{
					return view;
				}
				return "LIST";
			}
			return "AGENDA";
		}
	}

	public bool IsCompact => compact;

	public DateOnly AgendaDate => agendaDate;

	public DateOnly DisplayMonth => month;

	public event Action<DateTime>? AddRequested;

	public event Action<Occurrence>? EditRequested;

	public event Action<Occurrence>? Selected;

	public event Action<Occurrence>? DeleteRequested;

	public event Action? MonthSpaceRequested;

	public void SetResponsiveWidth(double width)
	{
		bool flag = width < (double)(narrowList ? 690 : 650) * Typography.Scale;
		if (flag != narrowList)
		{
			narrowList = flag;
			if (flag)
			{
				listJump = vm.SelectedDate;
			}
			resetScroll = false;
			Render();
		}
	}

	public ScheduleBoard(PlannerViewModel model)
	{
		vm = model;
		Background = Brushes.Transparent;
		preferences = vm.Appearance;
		view = preferences.View;
		month = new DateOnly(vm.SelectedDate.Year, vm.SelectedDate.Month, 1);
		lastSelection = vm.SelectedDate;
		if (ViewMode == "LIST")
		{
			listJump = displayToday;
		}
		Resources.MergedDictionaries.Add(new ResourceDictionary
		{
			Source = new Uri("/NanjingPlanner;component/BoardStyles.xaml", UriKind.Relative)
		});
		Grid grid = new Grid
		{
			RowDefinitions = 
			{
				new RowDefinition
				{
					Height = GridLength.Auto
				},
				new RowDefinition
				{
					Height = GridLength.Auto
				},
				new RowDefinition()
			}
		};
		Children.Add(new Border
		{
			CornerRadius = new CornerRadius(6.0),
			Background = Ui.Brush("#FBF7F0"),
			BorderBrush = Ui.Brush("#E5DDD3"),
			BorderThickness = new Thickness(1.0),
			Padding = new Thickness(9.0),
			Child = grid
		});
		header.Margin = new Thickness(4.0, 2.0, 2.0, 7.0);
		grid.Children.Add(header);
		Grid.SetRow(body, 2);
		grid.Children.Add(body);
		header.ColumnDefinitions.Add(new ColumnDefinition());
		header.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		WrapPanel wrapPanel = new WrapPanel
		{
			VerticalAlignment = VerticalAlignment.Center
		};
		header.Children.Add(wrapPanel);
		heading = Ui.Text("行程看板", 19.0, bold: true);
		heading.Foreground = Ui.Brush("#443B35");
		heading.Margin = new Thickness(0.0, 5.0, 12.0, 4.0);
		wrapPanel.Children.Add(heading);
		caption = Button("", PickDate);
		caption.ContentTemplate = (DataTemplate)FindResource("WrappingBoardCaption");
		caption.FontWeight = FontWeights.SemiBold;
		caption.ToolTip = "点击选择日期";
		wrapPanel.Children.Add(caption);
		Button button = Button("‹", () =>
		{
			Shift(-1);
		});
		Typography.Set(button, 22.0);
		button.Padding = new Thickness(8.0, 0.0, 8.0, 3.0);
		button.ToolTip = "上月";
		monthNavigation.Children.Add(button);
		Button button2 = Button("›", () =>
		{
			Shift(1);
		});
		Typography.Set(button2, 22.0);
		button2.Padding = new Thickness(8.0, 0.0, 8.0, 3.0);
		button2.ToolTip = "下月";
		monthNavigation.Children.Add(button2);
		wrapPanel.Children.Add(monthNavigation);
		Button button3 = Button("今天", () =>
		{
			NavigateTo(displayToday);
		});
		button3.Background = AppTheme.Brush("Surface");
		wrapPanel.Children.Add(button3);
		modes = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			VerticalAlignment = VerticalAlignment.Top
		};
		Grid.SetColumn(modes, 1);
		header.Children.Add(modes);
		(string, string)[] array = new (string, string)[2]
		{
			("MONTH", "月历"),
			("LIST", "列表")
		};
		for (int num = 0; num < array.Length; num++)
		{
			(string, string) tuple = array[num];
			string key = tuple.Item1;
			string item = tuple.Item2;
			Button button4 = Button(item, () =>
			{
				SetView(key);
			});
			modeButtons[key] = button4;
			modes.Children.Add(button4);
		}
		filters = new WrapPanel
		{
			Margin = new Thickness(8.0, 0.0, 0.0, 5.0)
		};
		Grid.SetRow(filters, 1);
		grid.Children.Add(filters);
		completedFilter = new CheckBox
		{
			Content = "显示已完成",
			IsChecked = preferences.ShowCompleted,
			Margin = new Thickness(0.0, 0.0, 16.0, 0.0)
		};
		incompleteFilter = new CheckBox
		{
			Content = "显示未完成",
			IsChecked = preferences.ShowIncomplete
		};
		completedFilter.Click += (object _, RoutedEventArgs _) =>
		{
			Ui.Safe(() =>
			{
				SaveFilter(completedFilter.IsChecked == true, incompleteFilter.IsChecked == true);
			});
		};
		incompleteFilter.Click += (object _, RoutedEventArgs _) =>
		{
			Ui.Safe(() =>
			{
				SaveFilter(completedFilter.IsChecked == true, incompleteFilter.IsChecked == true);
			});
		};
		filters.Children.Add(completedFilter);
		filters.Children.Add(incompleteFilter);
		vm.Changed += OnChanged;
		Loaded += (object _, RoutedEventArgs _) =>
		{
			Render();
		};
		Render();
	}

	private Button Button(string text, Action action)
	{
		Button button = new Button();
		button.Content = text;
		button.Style = (Style)FindResource("BoardButton");
		button.Click += (object _, RoutedEventArgs _) =>
		{
			Ui.Safe(action);
		};
		return button;
	}

	public void SetView(string mode)
	{
		if (mode == "MONTH" && narrowList)
		{
			MonthSpaceRequested?.Invoke();
		}
		string text = mode;
		if ((!(text == "MONTH") && !(text == "LIST")) || 1 == 0)
		{
			throw new ArgumentException("未知看板视图。");
		}
		if (!(mode == view))
		{
			listVisibleDate = null;
			if (mode == "LIST")
			{
				listJump = displayToday;
				resetScroll = true;
			}
			else
			{
				resetScroll = false;
			}
			vm.UpdateAppearance((BoardPreferences p) =>
			{
				p.View = mode;
			});
		}
	}

	public void NavigateTo(DateOnly date)
	{
		if (compact)
		{
			agendaDate = date;
		}
		month = new DateOnly(date.Year, date.Month, 1);
		listVisibleDate = null;
		listJump = date;
		if (date > listHorizon)
		{
			listHorizon = date.AddDays(180);
		}
		resetScroll = true;
		vm.FocusDate(date);
	}

	public void CheckDateChange(DateOnly today)
	{
		if (today == displayToday)
		{
			return;
		}
		DateOnly dateOnly = displayToday;
		displayToday = today;
		cache.Clear();
		bool flag = vm.SelectedDate == dateOnly;
		if (agendaDate == dateOnly)
		{
			agendaDate = today;
		}
		if (flag)
		{
			if (month == new DateOnly(dateOnly.Year, dateOnly.Month, 1))
			{
				month = new DateOnly(today.Year, today.Month, 1);
			}
			lastSelection = today;
		}
		if (listHorizon < today.AddDays(180))
		{
			listHorizon = today.AddDays(180);
		}
		if (flag)
		{
			vm.FocusDate(today);
		}
		else
		{
			vm.Notify();
		}
	}

	private void Shift(int direction)
	{
		if (compact)
		{
			agendaDate = agendaDate.AddDays(direction);
			resetScroll = true;
			Render();
			return;
		}
		DateOnly dateOnly2;
		if (ViewMode == "LIST")
		{
			DateOnly? dateOnly = listVisibleDate;
			if (dateOnly.HasValue)
			{
				DateOnly valueOrDefault = dateOnly.GetValueOrDefault();
				dateOnly2 = new DateOnly(valueOrDefault.Year, valueOrDefault.Month, 1);
				goto IL_0070;
			}
		}
		dateOnly2 = month;
		goto IL_0070;
		IL_0070:
		DateOnly dateOnly3 = dateOnly2;
		NavigateTo(dateOnly3.AddMonths(direction));
	}

	public void SetCompact(bool value)
	{
		if (compact != value)
		{
			compact = value;
			resetScroll = false;
			Render();
		}
	}

	private void SaveFilter(bool completed, bool incomplete)
	{
		vm.UpdateAppearance((BoardPreferences p) =>
		{
			p.ShowCompleted = completed;
			p.ShowIncomplete = incomplete;
		});
	}

	private string VisibilityKey()
	{
		return string.Join(',', VisibleTables());
	}

	private string CacheKey()
	{
		return $"{displayToday}|{vm.State.Revision}|{month}|{listHorizon}|{agendaDate}|{VisibilityKey()}|{preferences.FontSize}|{preferences.ShowCompleted}|{preferences.ShowIncomplete}|{string.Join(',', preferences.Completed.Order())}|{string.Join(',', preferences.Important.Order())}";
	}

	private void OnChanged()
	{
		BoardPreferences boardPreferences = preferences;
		preferences = vm.Appearance;
		bool flag = boardPreferences.View == preferences.View;
		view = preferences.View;
		completedFilter.IsChecked = preferences.ShowCompleted;
		incompleteFilter.IsChecked = preferences.ShowIncomplete;
		if (((renderedToday == displayToday) & flag) && renderedRevision == vm.State.Revision && renderedVisibility == VisibilityKey() && lastSelection == vm.SelectedDate && boardPreferences.FontSize == preferences.FontSize && boardPreferences.ShowCompleted == preferences.ShowCompleted && boardPreferences.ShowIncomplete == preferences.ShowIncomplete && preferences.ShowCompleted && preferences.ShowIncomplete && body.Children.Count > 0)
		{
			foreach (SwipeActivityCard item in Descendants(body).OfType<SwipeActivityCard>())
			{
				string key = item.Entry.Occurrence.Key;
				item.SetState(preferences.Completed.Contains(key), preferences.Important.Contains(key), animate: true);
			}
			cache.Clear();
			cache[ViewMode] = (CacheKey(), body.Children[0], scroll, listHeadings.ToList());
			return;
		}
		if (lastSelection != vm.SelectedDate)
		{
			List<DateOnly> list = BoardProjection.MonthDays(month);
			if (!(ViewMode == "LIST") && !list.Contains(vm.SelectedDate))
			{
				month = new DateOnly(vm.SelectedDate.Year, vm.SelectedDate.Month, 1);
				listVisibleDate = null;
				resetScroll = true;
			}
			if (ViewMode == "LIST")
			{
				listJump = vm.SelectedDate;
				if (vm.SelectedDate > listHorizon)
				{
					listHorizon = vm.SelectedDate.AddDays(180);
				}
			}
			lastSelection = vm.SelectedDate;
		}
		Render();
	}

	public void Render()
	{
		if (rendering)
		{
			return;
		}
		rendering = true;
		try
		{
			if (scroll != null && displayedView.Length > 0)
			{
				scrollPositions[displayedView] = (scroll.HorizontalOffset, scroll.VerticalOffset);
			}
			(double, double) position = (resetScroll ? (0.0, 0.0) : scrollPositions.GetValueOrDefault(ViewMode));
			double offset = position.Item2;
			displayedView = ViewMode;
			string text = CacheKey();
			renderedToday = displayToday;
			renderedRevision = vm.State.Revision;
			renderedVisibility = VisibilityKey();
			heading.Text = (compact ? "今日行程" : "行程看板");
			StackPanel stackPanel = modes;
			Visibility visibility = (filters.Visibility = (compact ? Visibility.Collapsed : Visibility.Visible));
			stackPanel.Visibility = visibility;
			monthNavigation.Visibility = ((!compact && !(ViewMode == "MONTH")) ? Visibility.Collapsed : Visibility.Visible);
			body.Children.Clear();
			if (cache.TryGetValue(ViewMode, out (string, UIElement, ScrollViewer, List<(DateOnly, FrameworkElement)>) value) && value.Item1 == text)
			{
				body.Children.Add(value.Item2);
				scroll = value.Item3;
				monthGrid = scroll?.Content as Grid;
				listHeadings.Clear();
				listHeadings.AddRange(value.Item4);
				UpdateSelectedDay();
				Button button = caption;
				object content;
				if (compact)
				{
					content = $"{agendaDate:M月d日}";
				}
				else
				{
					content = ((ViewMode == "MONTH") ? $"{month.Year} 年 {month.Month} 月" : "全部行程 · 按日期");
				}
				button.Content = content;
				UpdateModes();
				resetScroll = false;
				ScrollViewer reused = scroll;
				Dispatcher.BeginInvoke((Action)(() =>
				{
					if (scroll == reused)
					{
						FitRows();
						reused?.ScrollToHorizontalOffset(position.Item1);
						reused?.ScrollToVerticalOffset(position.Item2);
						JumpToListDate();
						UpdateListCaption();
					}
				}), DispatcherPriority.Loaded);
				return;
			}
			monthGrid = null;
			scroll = null;
			listHeadings.Clear();
			trafficSnapshots = new TrafficSnapshotStore(vm.DataDirectory).Load();
			UpdateModes();
			caption.Content = (compact ? $"{agendaDate:M月d日}" : $"{month.Year} 年 {month.Month} 月");
			if (compact)
			{
				BuildAgenda();
			}
			else if (ViewMode == "MONTH")
			{
				BuildMonth();
			}
			else
			{
				BuildList();
			}
			cache[ViewMode] = (text, body.Children[0], scroll, listHeadings.ToList());
			resetScroll = false;
			ScrollViewer current = scroll;
			if (current == null)
			{
				return;
			}
			Dispatcher.BeginInvoke((Action)(() =>
			{
				if (scroll == current)
				{
					current.ScrollToVerticalOffset(offset);
					FitRows();
					JumpToListDate();
					UpdateListCaption();
				}
			}));
		}
		finally
		{
			rendering = false;
		}
	}

	private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
	{
		yield return root;
		foreach (DependencyObject item in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
		{
			foreach (DependencyObject item2 in Descendants(item))
			{
				yield return item2;
			}
		}
	}

	private void UpdateSelectedDay()
	{
		if (monthGrid == null)
		{
			return;
		}
		foreach (Button item in Descendants(monthGrid).OfType<Button>())
		{
			if (item.Tag is DateOnly dateOnly)
			{
				item.FontWeight = ((dateOnly == vm.SelectedDate) ? FontWeights.Bold : FontWeights.SemiBold);
			}
		}
	}

	private void UpdateModes()
	{
		foreach (var (text2, button2) in modeButtons)
		{
			button2.Background = ((text2 == ViewMode) ? Ui.Brush("#C65B40") : Brushes.White);
			button2.Foreground = ((text2 == ViewMode) ? Brushes.White : Ui.Brush("#68605A"));
		}
	}

	private List<BoardDay> DisplayDays(DateOnly from, DateOnly until)
	{
		return (from d in BoardProjection.Range(vm.State, @from, until, VisibleTables())
			select new BoardDay(d.Date, d.Entries.Where((BoardEntry e) => preferences.IsVisible(e.Occurrence.Key)).ToList())).ToList();
	}

	private IEnumerable<string> VisibleTables()
	{
		return from t in vm.State.Tables
			where !vm.HiddenTables.Contains(t.Id)
			select t.Id;
	}

	private Dictionary<string, List<Conflict>> Conflicts(DateOnly from, DateOnly until)
	{
		Dictionary<string, List<Conflict>> dictionary = new Dictionary<string, List<Conflict>>();
		Dictionary<string, Occurrence> dictionary2 = CalendarEngine.Expand(vm.State, from.ToDateTime(TimeOnly.MinValue), until.ToDateTime(TimeOnly.MinValue)).ToDictionary((Occurrence o) => o.Key);
		foreach (Conflict item in CalendarEngine.AllConflicts(vm.State, from.ToDateTime(TimeOnly.MinValue), until.ToDateTime(TimeOnly.MinValue)))
		{
			Conflict conflict = item;
			if (conflict.Kind != "TIME_OVERLAP")
			{
				string secondKey = conflict.SecondKey;
				if (secondKey != null && dictionary2.TryGetValue(conflict.FirstKey, out var value) && dictionary2.TryGetValue(secondKey, out var value2))
				{
					TrafficPair trafficPair = new TrafficPair(value, value2);
					TrafficSnapshot valueOrDefault = trafficSnapshots.GetValueOrDefault(trafficPair.Fingerprint(vm.State));
					int? num = valueOrDefault?.Minutes;
					if (num.HasValue)
					{
						int valueOrDefault2 = num.GetValueOrDefault();
						if (valueOrDefault2 <= trafficPair.GapMinutes)
						{
							continue;
						}
						conflict = new Conflict("TRAVEL_SHORT", value.Key, value2.Key, AdjacentTraffic.Label(trafficPair, valueOrDefault));
					}
				}
			}
			foreach (string item2 in new string[2] { conflict.FirstKey, conflict.SecondKey }.Where((string k) => k != null).Cast<string>())
			{
				if (!dictionary.TryGetValue(item2, out var value3))
				{
					value3 = (dictionary[item2] = new List<Conflict>());
				}
				value3.Add(conflict);
			}
		}
		return dictionary;
	}

	private void BuildMonth()
	{
		List<DateOnly> list = BoardProjection.MonthDays(month);
		List<BoardDay> list2 = DisplayDays(list[0], list[list.Count - 1].AddDays(1));
		Dictionary<string, List<Conflict>> conflicts = Conflicts(list[0], list[list.Count - 1].AddDays(1));
		Grid grid = new Grid();
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(31.0)
		});
		grid.RowDefinitions.Add(new RowDefinition());
		body.Children.Add(grid);
		Border border = new Border
		{
			ClipToBounds = true,
			Margin = new Thickness(0.0, 0.0, SystemParameters.VerticalScrollBarWidth, 0.0)
		};
		grid.Children.Add(border);
		Grid weekdays = new Grid
		{
			HorizontalAlignment = HorizontalAlignment.Left
		};
		border.Child = weekdays;
		monthGrid = new Grid
		{
			Tag = "ResponsiveMonthGrid",
			Background = Ui.Brush("#EEEAE5"),
			HorizontalAlignment = HorizontalAlignment.Stretch
		};
		Grid renderedMonth = monthGrid;
		monthGrid.SizeChanged += (object _, SizeChangedEventArgs _) =>
		{
			weekdays.Width = renderedMonth.ActualWidth;
		};
		for (int num = 0; num < 7; num++)
		{
			weekdays.ColumnDefinitions.Add(new ColumnDefinition());
			monthGrid.ColumnDefinitions.Add(new ColumnDefinition());
			TextBlock textBlock = Ui.Text((new string[7] { "周一", "周二", "周三", "周四", "周五", "周六", "周日" })[num], 11.0, bold: true);
			textBlock.Foreground = Ui.Brush("#77716A");
			textBlock.HorizontalAlignment = HorizontalAlignment.Center;
			textBlock.Margin = new Thickness(0.0, 6.0, 0.0, 0.0);
			Grid.SetColumn(textBlock, num);
			weekdays.Children.Add(textBlock);
		}
		for (int num2 = 0; num2 < list.Count / 7; num2++)
		{
			monthGrid.RowDefinitions.Add(new RowDefinition
			{
				Height = GridLength.Auto,
				MinHeight = 88.0
			});
		}
		scroll = new ScrollViewer
		{
			Tag = "MonthViewport",
			Content = monthGrid,
			VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
			HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
		};
		scroll.ScrollChanged += (object _, ScrollChangedEventArgs e) =>
		{
			weekdays.RenderTransform = new TranslateTransform(0.0 - e.HorizontalOffset, 0.0);
			if (e.ViewportHeightChange != 0.0 || e.ViewportWidthChange != 0.0)
			{
				FitRows();
			}
		};
		Grid.SetRow(scroll, 1);
		grid.Children.Add(scroll);
		scroll.SizeChanged += (object _, SizeChangedEventArgs _) =>
		{
			FitRows();
		};
		scroll.Loaded += (object _, RoutedEventArgs _) =>
		{
			FitRows();
		};
		for (int num3 = 0; num3 < list2.Count; num3++)
		{
			BoardDay day = list2[num3];
			bool flag = day.Date.Month == month.Month;
			StackPanel stackPanel = new StackPanel();
			Border cell = new Border
			{
				Background = AppTheme.Brush(flag ? "Surface" : "Panel"),
				BorderBrush = AppTheme.Brush("Border"),
				BorderThickness = new Thickness(0.0, 0.0, 1.0, 1.0),
				CornerRadius = new CornerRadius(0.0),
				Margin = new Thickness(0.0),
				Child = stackPanel,
				Tag = day.Date
			};
			Grid.SetRow(cell, num3 / 7);
			Grid.SetColumn(cell, num3 % 7);
			monthGrid.Children.Add(cell);
			DockPanel dockPanel = new DockPanel();
			Button button = Button(day.Date.Day + ((day.Date == displayToday) ? " · 今天" : ""), () =>
			{
				SelectDay(day.Date);
			});
			button.Content = new TextBlock
			{
				Text = day.Date.Day + ((day.Date == displayToday) ? " · 今天" : ""),
				TextWrapping = TextWrapping.Wrap
			};
			button.Tag = day.Date;
			button.Padding = new Thickness(8.0, 4.0, 8.0, 4.0);
			button.Margin = new Thickness(5.0);
			button.HorizontalAlignment = HorizontalAlignment.Left;
			button.Background = ((day.Date == displayToday) ? AppTheme.Brush("Accent") : Brushes.Transparent);
			button.HorizontalContentAlignment = HorizontalAlignment.Left;
			Typography.Set(button, 13.0);
			button.FontWeight = FontWeights.SemiBold;
			Button button2 = button;
			string role;
			if (day.Date == displayToday)
			{
				role = "Canvas";
			}
			else
			{
				role = (flag ? "Text" : "Muted");
			}
			button2.Foreground = AppTheme.Brush(role);
			dockPanel.Children.Add(button);
			stackPanel.Children.Add(new Border
			{
				Background = Brushes.Transparent,
				CornerRadius = new CornerRadius(0.0),
				Child = dockPanel
			});
			StackPanel stackPanel2 = new StackPanel
			{
				Margin = new Thickness(5.0, 2.0, 5.0, 5.0)
			};
			stackPanel.Children.Add(stackPanel2);
			foreach (BoardEntry entry in day.Entries)
			{
				stackPanel2.Children.Add(ActivityCard(entry, list: false, conflicts));
			}
			cell.MouseLeftButtonUp += (object _, MouseButtonEventArgs e) =>
			{
				DependencyObject dependencyObject = e.OriginalSource as DependencyObject;
				while (dependencyObject != null && dependencyObject != cell)
				{
					bool flag2 = ((dependencyObject is ButtonBase || dependencyObject is TextBox) ? true : false);
					if (flag2 || (dependencyObject is Border border2 && border2.Tag is BoardEntry))
					{
						return;
					}
					dependencyObject = VisualTreeHelper.GetParent(dependencyObject);
				}
				Add(day.Date);
				e.Handled = true;
			};
			ContextMenu contextMenu = new ContextMenu();
			MenuItem menuItem = new MenuItem
			{
				Header = "添加当天行程"
			};
			menuItem.Click += (object _, RoutedEventArgs _) =>
			{
				Add(day.Date);
			};
			contextMenu.Items.Add(menuItem);
			Ui.AttachMenu(cell, contextMenu);
		}
	}

	private void FitRows()
	{
		if (monthGrid == null || scroll == null || monthGrid.RowDefinitions.Count == 0)
		{
			return;
		}
		double num = BoardLayout.RowMinimum((scroll.ViewportHeight > 0.0) ? scroll.ViewportHeight : scroll.ActualHeight, monthGrid.RowDefinitions.Count);
		foreach (RowDefinition item in (IEnumerable<RowDefinition>)monthGrid.RowDefinitions)
		{
			if (Math.Abs(item.MinHeight - num) > 0.1)
			{
				item.MinHeight = num;
			}
		}
	}

	private void BuildList()
	{
		StackPanel stackPanel = new StackPanel
		{
			Margin = new Thickness(8.0, 4.0, 8.0, 12.0)
		};
		List<(DateOnly From, DateOnly Until)> list = ContinuousList.Windows(vm.State, listHorizon, VisibleTables());
		List<BoardDay> list2 = (from d in list.SelectMany(((DateOnly From, DateOnly Until) w) => DisplayDays(w.From, w.Until))
			where d.Entries.Count > 0
			select d).ToList();
		if (!list2.Any((BoardDay d) => d.Date == displayToday))
		{
			list2.Add(new BoardDay(displayToday, Array.Empty<BoardEntry>()));
			list2 = list2.OrderBy((BoardDay d) => d.Date).ToList();
		}
		Dictionary<string, List<Conflict>> dictionary = new Dictionary<string, List<Conflict>>();
		foreach (var item in list)
		{
			foreach (var (key, list4) in Conflicts(item.From, item.Until))
			{
				if (!dictionary.TryGetValue(key, out var value))
				{
					dictionary[key] = list4;
				}
				else
				{
					value.AddRange(list4);
				}
			}
		}
		scroll = new ScrollViewer
		{
			Content = stackPanel,
			PanningMode = PanningMode.VerticalOnly,
			VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
			HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
		};
		scroll.ScrollChanged += (object _, ScrollChangedEventArgs e) =>
		{
			UpdateListCaption();
			if (e.VerticalChange > 0.0)
			{
				ScrollViewer scrollViewer = scroll;
				if (scrollViewer != null && scrollViewer.ScrollableHeight > 0.0 && scrollViewer.VerticalOffset >= scrollViewer.ScrollableHeight - 120.0 && vm.State.Rules.Any((RecurrenceRule r) => !r.ValidUntil.HasValue && vm.State.Activities.Any((Activity a) => a.Id == r.ActivityId && !vm.HiddenTables.Contains(a.TableId))))
				{
					listHorizon = listHorizon.AddDays(180);
					Dispatcher.BeginInvoke((Action)(() =>
					{
						Render();
					}));
				}
			}
		};
		body.Children.Add(scroll);
		caption.Content = "全部行程 · 按日期";
		if (list2.All((BoardDay d) => d.Entries.Count == 0))
		{
			stackPanel.Children.Add(Ui.Text("没有符合显示设置的行程", 18.0, bold: true));
			stackPanel.Children.Add(Button("＋ 添加行程", () =>
			{
				Add(vm.SelectedDate);
			}));
		}
		Dictionary<string, TrafficSnapshot> dictionary2 = trafficSnapshots;
		BoardEntry boardEntry = null;
		foreach (BoardDay item2 in list2)
		{
			DockPanel dockPanel = new DockPanel
			{
				Margin = new Thickness(0.0, 16.0, 0.0, 10.0)
			};
			TextBlock textBlock = Ui.Text(item2.Entries.Count + " 项", 12.0);
			textBlock.Foreground = Ui.Brush("#A39A90");
			DockPanel.SetDock(textBlock, Dock.Right);
			dockPanel.Children.Add(textBlock);
			dockPanel.Children.Add(Ui.Text($"{item2.Date.Day}  周{(new string[7] { "一", "二", "三", "四", "五", "六", "日" })[(int)(item2.Date.DayOfWeek + 6) % 7]}    {item2.Date:yyyy年M月}", 17.0, bold: true));
			stackPanel.Children.Add(dockPanel);
			listHeadings.Add((item2.Date, dockPanel));
			if (item2.Date == displayToday && item2.Entries.Count == 0)
			{
				stackPanel.Children.Add(Ui.Text("今天暂无安排"));
				stackPanel.Children.Add(Button("＋ 添加今天的行程", () =>
				{
					Add(displayToday);
				}));
			}
			foreach (BoardEntry entry in item2.Entries)
			{
				if (entry.Transfer == null && (object)boardEntry != null)
				{
					BoardEntry boardEntry2 = boardEntry;
					if (boardEntry2.Occurrence.Key != entry.Occurrence.Key)
					{
						TrafficPair pair = new TrafficPair(boardEntry2.Occurrence, entry.Occurrence);
						int gapMinutes = pair.GapMinutes;
						TrafficSnapshot trafficSnapshot = dictionary2.GetValueOrDefault(pair.Fingerprint(vm.State));
						if (trafficSnapshot == null)
						{
							PlanLeg planLeg = vm.State.Legs.FirstOrDefault((PlanLeg l) =>
							{
								if (l.FromActivityId == pair.From.Activity.Id)
								{
									DateOnly? fromOriginDate = l.FromOriginDate;
									DateOnly? originDate = pair.From.OriginDate;
									if (fromOriginDate.HasValue == originDate.HasValue && (!fromOriginDate.HasValue || fromOriginDate.GetValueOrDefault() == originDate.GetValueOrDefault()) && l.ToActivityId == pair.To.Activity.Id)
									{
										originDate = l.ToOriginDate;
										fromOriginDate = pair.To.OriginDate;
										if (originDate.HasValue == fromOriginDate.HasValue && (!originDate.HasValue || originDate.GetValueOrDefault() == fromOriginDate.GetValueOrDefault()) && l.ValidationStatus == "VALID" && l.DepartAt >= pair.From.End)
										{
											return l.ArriveAt <= pair.To.Start;
										}
									}
								}
								return false;
							});
							if (planLeg != null)
							{
								trafficSnapshot = new TrafficSnapshot
								{
									Minutes = (int)Math.Ceiling((planLeg.ArriveAt - planLeg.DepartAt).TotalMinutes),
									Source = "已保存的地图路线快照"
								};
							}
						}
						int? num = trafficSnapshot?.Minutes;
						int num2;
						if (num.HasValue)
						{
							int valueOrDefault = num.GetValueOrDefault();
							num2 = ((valueOrDefault > gapMinutes) ? 1 : 0);
						}
						else
						{
							num2 = 0;
						}
						bool flag = (byte)num2 != 0;
						if (gapMinutes >= 0 && ((gapMinutes < 60) | flag))
						{
							TextBlock textBlock2 = Ui.Text("◷  " + AdjacentTraffic.Label(pair, trafficSnapshot), 11.0, flag);
							textBlock2.Foreground = AppTheme.Brush(flag ? "Danger" : "Muted");
							textBlock2.Margin = new Thickness(0.0);
							stackPanel.Children.Add(new Border
							{
								Background = AppTheme.Brush(flag ? "Selected" : "Panel"),
								Padding = new Thickness(9.0, 6.0, 9.0, 6.0),
								Margin = new Thickness(Math.Max(94.0, 58.0 * Typography.Scale) + 8.0, 0.0, 0.0, 7.0),
								HorizontalAlignment = HorizontalAlignment.Left,
								Child = textBlock2,
								ToolTip = (trafficSnapshot?.Source ?? "这里只表示时间间隔，尚未查询交通时间。")
							});
						}
					}
				}
				stackPanel.Children.Add(TimelineRow(entry, dictionary));
				if (entry.Transfer == null)
				{
					boardEntry = entry;
				}
			}
		}
	}

	private void JumpToListDate()
	{
		if (compact || ViewMode != "LIST")
		{
			return;
		}
		DateOnly? dateOnly = listJump;
		if (!dateOnly.HasValue)
		{
			return;
		}
		DateOnly target = dateOnly.GetValueOrDefault();
		if (scroll?.Content is StackPanel stackPanel)
		{
			listJump = null;
			stackPanel.UpdateLayout();
			(DateOnly, FrameworkElement) tuple = listHeadings.FirstOrDefault(((DateOnly Date, FrameworkElement Heading) h) => h.Date >= target);
			if (tuple.Item2 == null && listHeadings.Count > 0)
			{
				List<(DateOnly Date, FrameworkElement Heading)> list = listHeadings;
				tuple = list[list.Count - 1];
			}
			if (tuple.Item2 != null)
			{
				double y = tuple.Item2.TranslatePoint(new Point(0.0, 0.0), stackPanel).Y;
				stackPanel.MinHeight = Math.Max(stackPanel.ActualHeight, y + scroll.ViewportHeight);
				stackPanel.UpdateLayout();
				scroll.ScrollToVerticalOffset(y);
			}
		}
	}

	private void UpdateListCaption()
	{
		if (compact || ViewMode != "LIST" || !(scroll?.Content is StackPanel relativeTo) || listHeadings.Count == 0)
		{
			return;
		}
		DateOnly value = listHeadings[0].Date;
		foreach (var listHeading in listHeadings)
		{
			if (!(listHeading.Heading.TranslatePoint(new Point(0.0, 0.0), relativeTo).Y <= scroll.VerticalOffset + 8.0))
			{
				break;
			}
			(value, _) = listHeading;
		}
		listVisibleDate = value;
		caption.Content = $"{value:yyyy年M月d日}";
	}

	private FrameworkElement ActivityCard(BoardEntry entry, bool list, Dictionary<string, List<Conflict>> conflicts)
	{
		PlanLeg transfer = entry.Transfer;
		if (transfer != null)
		{
			Border border = Ui.Card(new StackPanel
			{
				Children = 
				{
					(UIElement)Ui.Text("交通安排 · " + entry.TimeText, 12.0, bold: true),
					(UIElement)Ui.Text(entry.Occurrence.Title, 14.0, bold: true),
					(UIElement)Ui.Text(entry.DurationText, 11.0),
					(UIElement)Ui.Text(entry.Occurrence.Activity.Note ?? "", 11.0),
					(UIElement)Ui.Text("随起终点安排核对；可通过重新规划更新交通。", 10.0)
				}
			}, "#F0ECE5");
			border.Tag = entry;
			border.BorderBrush = Ui.Brush((transfer.ValidationStatus == "STALE") ? "#B95D4D" : "#B5A08A");
			return border;
		}
		Occurrence o = entry.Occurrence;
		ScheduleTable scheduleTable = vm.State.Tables.Single((ScheduleTable t) => t.Id == o.Activity.TableId);
		bool flag = scheduleTable.Active(DateOnly.FromDateTime(o.Start));
		conflicts.TryGetValue(o.Key, out List<Conflict> value);
		SwipeActivityCard swipeActivityCard = new SwipeActivityCard(entry)
		{
			BorderBrush = Ui.RawBrush(scheduleTable.Color),
			BorderThickness = new Thickness(4.0, 1.0, 1.0, 1.0)
		};
		StackPanel stackPanel = new StackPanel();
		Panel panel = (list ? ((Panel)new DockPanel()) : ((Panel)new WrapPanel()));
		if (!list)
		{
			swipeActivityCard.CornerRadius = new CornerRadius(5.0);
			swipeActivityCard.Padding = new Thickness(8.0);
			swipeActivityCard.Margin = new Thickness(0.0, 0.0, 0.0, 6.0);
			DockPanel.SetDock(swipeActivityCard.Completion, Dock.Left);
			panel.Children.Add(swipeActivityCard.Completion);
		}
		DockPanel.SetDock(swipeActivityCard.Star, Dock.Right);
		panel.Children.Add(swipeActivityCard.Star);
		TextBlock textBlock = Ui.Text(entry.TimeText + (entry.ContinuesFromYesterday ? " · 续" : ""), 12.0, bold: true);
		textBlock.Foreground = Ui.Brush("#9B5142");
		textBlock.Margin = new Thickness(0.0, 3.0, 0.0, 5.0);
		textBlock.Visibility = (list ? Visibility.Collapsed : Visibility.Visible);
		if (list)
		{
			panel.Children.Add(textBlock);
		}
		else
		{
			stackPanel.Children.Add(textBlock);
		}
		stackPanel.Children.Add(panel);
		TextBlock textBlock2 = Ui.Text(o.Title, list ? 15 : 14, bold: true);
		textBlock2.Margin = new Thickness(0.0, 0.0, 0.0, 6.0);
		textBlock2.Foreground = Ui.Brush("#443B35");
		stackPanel.Children.Add(textBlock2);
		stackPanel.Children.Add(Muted("地点：" + vm.State.PlaceName(o.PlaceId), 12.0));
		stackPanel.Children.Add(Muted(entry.DurationText, 11.0));
		if (entry.ContinuityText.Length > 0)
		{
			stackPanel.Children.Add(Muted(entry.ContinuityText, 11.0));
		}
		if (!string.IsNullOrWhiteSpace(o.Activity.Note))
		{
			stackPanel.Children.Add(Muted("备注：" + o.Activity.Note, 12.0));
		}
		TextBlock textBlock3 = Muted(scheduleTable.Name + ((!o.OriginDate.HasValue) ? "" : " · 每周") + ((!flag) ? " · 未生效" : ""), 11.0);
		textBlock3.Foreground = AppTheme.TableText(scheduleTable.Color);
		stackPanel.Children.Add(textBlock3);
		if (value != null && value.Count > 0)
		{
			string text;
			if (value.Any((Conflict w) => w.Kind == "TIME_OVERLAP"))
			{
				text = "! 时间冲突";
			}
			else
			{
				text = (value.Any((Conflict w) => w.Kind == "TRAVEL_SHORT") ? "! 交通时间不足" : "◷ 交通待核验");
			}
			TextBlock textBlock4 = Ui.Text(text, 11.0, bold: true);
			textBlock4.Foreground = Ui.Brush(value.Any((Conflict w) => w.Kind == "TIME_OVERLAP") ? "#AF5146" : "#986638");
			textBlock4.Margin = new Thickness(0.0, 3.0, 0.0, 0.0);
			stackPanel.Children.Add(textBlock4);
		}
		swipeActivityCard.Child = stackPanel;
		swipeActivityCard.SetState(preferences.Completed.Contains(o.Key), preferences.Important.Contains(o.Key));
		swipeActivityCard.ToggleRequested += () =>
		{
			Ui.Safe(() =>
			{
				ToggleCompleted(o.Key);
			});
		};
		swipeActivityCard.ImportantRequested += () =>
		{
			Ui.Safe(() =>
			{
				vm.UpdateAppearance((BoardPreferences p) =>
				{
					if (!p.Important.Add(o.Key))
					{
						p.Important.Remove(o.Key);
					}
				});
			});
		};
		swipeActivityCard.EditRequested += () =>
		{
			SelectOccurrence(o);
			EditRequested?.Invoke(o);
		};
		ContextMenu menu = new ContextMenu();
		menu.Opened += (object _, RoutedEventArgs _) =>
		{
			menu.Items.Clear();
			(string, Action)[] array = new (string, Action)[5]
			{
				("查看详情", () =>
				{
					SelectOccurrence(o);
				}),
				("编辑活动", () =>
				{
					SelectOccurrence(o);
					EditRequested?.Invoke(o);
				}),
				(vm.Appearance.Completed.Contains(o.Key) ? "恢复未完成" : "标记完成", () =>
				{
					ToggleCompleted(o.Key);
				}),
				(vm.Appearance.Important.Contains(o.Key) ? "取消收藏" : "收藏", () =>
				{
					vm.UpdateAppearance((BoardPreferences p) =>
					{
						if (!p.Important.Add(o.Key))
						{
							p.Important.Remove(o.Key);
						}
					});
				}),
				("删除这一次", () =>
				{
					DeleteRequested?.Invoke(o);
				})
			};
			for (int num = 0; num < array.Length; num++)
			{
				(string, Action) tuple = array[num];
				string item = tuple.Item1;
				Action action = tuple.Item2;
				MenuItem menuItem = new MenuItem
				{
					Header = item
				};
				menuItem.Click += (object obj, RoutedEventArgs e) =>
				{
					Ui.Safe(action);
				};
				menu.Items.Add(menuItem);
			}
		};
		Ui.AttachMenu(swipeActivityCard, menu);
		if (!list)
		{
			Button more = Ui.Button("…", () =>
			{
			});
			more.Click += (object _, RoutedEventArgs _) =>
			{
				Ui.OpenMenu(menu, more);
			};
			more.ToolTip = "更多行程操作";
			more.Padding = new Thickness(3.0, 0.0, 3.0, 0.0);
			more.Background = Brushes.Transparent;
			DockPanel.SetDock(more, Dock.Right);
			panel.Children.Insert(0, more);
			more.Opacity = 0.0;
			swipeActivityCard.MouseEnter += (object _, MouseEventArgs _) =>
			{
				more.Opacity = 1.0;
			};
			swipeActivityCard.MouseLeave += (object _, MouseEventArgs _) =>
			{
				if (!more.IsKeyboardFocusWithin)
				{
					more.Opacity = 0.0;
				}
			};
			more.GotKeyboardFocus += (object _, KeyboardFocusChangedEventArgs _) =>
			{
				more.Opacity = 1.0;
			};
		}
		return swipeActivityCard;
	}

	private static TextBlock Muted(string text, double size)
	{
		TextBlock textBlock = Ui.Text(text, size);
		textBlock.Foreground = Ui.Brush("#77716A");
		textBlock.Margin = new Thickness(0.0, 0.0, 0.0, 4.0);
		return textBlock;
	}

	private void ToggleCompleted(string key)
	{
		vm.UpdateAppearance((BoardPreferences p) =>
		{
			if (!p.Completed.Add(key))
			{
				p.Completed.Remove(key);
			}
		});
	}

	private void SelectDay(DateOnly date)
	{
		vm.FocusDate(date);
	}

	private void SelectOccurrence(Occurrence o)
	{
		vm.FocusDate(DateOnly.FromDateTime(o.Start));
		Selected?.Invoke(o);
	}

	private void Add(DateOnly date)
	{
		SelectDay(date);
		AddRequested?.Invoke(date.ToDateTime(new TimeOnly(9, 0)));
	}

	private FrameworkElement TimelineRow(BoardEntry entry, Dictionary<string, List<Conflict>> conflicts)
	{
		Grid grid = new Grid
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 9.0)
		};
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(Math.Max(compact ? 66 : 94, 58.0 * Typography.Scale))
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition());
		StackPanel stackPanel = new StackPanel
		{
			Margin = new Thickness(4.0, 12.0, 4.0, 10.0),
			VerticalAlignment = VerticalAlignment.Center
		};
		TextBlock[] array = new TextBlock[3]
		{
			Ui.Text(entry.Start.ToString("HH:mm"), compact ? 13 : 15, bold: true),
			Ui.Text(entry.DayMinutes + " 分钟", 11.0),
			Ui.Text(entry.TimeText.Split('—')[1], compact ? 13 : 15, bold: true)
		};
		foreach (TextBlock textBlock in array)
		{
			textBlock.HorizontalAlignment = HorizontalAlignment.Center;
			stackPanel.Children.Add(textBlock);
		}
		grid.Children.Add(new Border
		{
			CornerRadius = new CornerRadius(14.0),
			Background = Ui.Brush("#EEEDEB"),
			Child = stackPanel,
			Margin = new Thickness(0.0, 0.0, 8.0, 0.0)
		});
		FrameworkElement frameworkElement = ActivityCard(entry, list: true, conflicts);
		frameworkElement.Margin = new Thickness(0.0);
		Grid.SetColumn(frameworkElement, 1);
		grid.Children.Add(frameworkElement);
		return grid;
	}

	private void BuildAgenda()
	{
		List<BoardDay> list = DisplayDays(agendaDate, agendaDate.AddDays(7));
		Dictionary<string, List<Conflict>> conflicts = Conflicts(agendaDate, agendaDate.AddDays(7));
		StackPanel stackPanel = new StackPanel
		{
			Margin = new Thickness(2.0)
		};
		scroll = new ScrollViewer
		{
			Content = stackPanel,
			PanningMode = PanningMode.VerticalOnly,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
			HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
		};
		body.Children.Add(scroll);
		foreach (BoardDay day in list)
		{
			string? text;
			if (day.Date == displayToday)
			{
				text = "今天";
			}
			else
			{
				text = ((day.Date == displayToday.AddDays(1)) ? "明天" : day.Date.ToString("M月d日"));
			}
			string text2 = text + " · 周" + (new string[7] { "一", "二", "三", "四", "五", "六", "日" })[(int)(day.Date.DayOfWeek + 6) % 7];
			StackPanel stackPanel2 = new StackPanel();
			stackPanel2.Children.Add(new Border
			{
				Background = Ui.Brush((day.Date == displayToday) ? "#AC5A4C" : "#F4DFD9"),
				CornerRadius = new CornerRadius(15.0, 15.0, 0.0, 0.0),
				Padding = new Thickness(12.0, 7.0, 12.0, 5.0),
				Child = new TextBlock
				{
					Text = text2,
					Foreground = Ui.Brush((day.Date == displayToday) ? "#FFFAF6" : "#AC5A4C"),
					FontWeight = FontWeights.SemiBold,
					TextWrapping = TextWrapping.Wrap
				}
			});
			StackPanel stackPanel3 = new StackPanel
			{
				Margin = new Thickness(8.0)
			};
			foreach (BoardEntry entry in day.Entries)
			{
				stackPanel3.Children.Add(TimelineRow(entry, conflicts));
			}
			if (day.Entries.Count == 0)
			{
				stackPanel3.Children.Add(Button("暂无安排 · 点击添加", () =>
				{
					Add(day.Date);
				}));
			}
			stackPanel2.Children.Add(stackPanel3);
			stackPanel.Children.Add(new Border
			{
				Background = Ui.Brush("#EEEDEB"),
				CornerRadius = new CornerRadius(16.0),
				Margin = new Thickness(0.0, 0.0, 0.0, 12.0),
				Child = stackPanel2,
				Tag = day.Date
			});
		}
	}

	private void PickDate()
	{
		StyledDateEntry picker = new StyledDateEntry
		{
			SelectedDate = vm.SelectedDate.ToDateTime(TimeOnly.MinValue),
			PlacementAnchor = caption
		};
		picker.SelectedDateChanged += (object? _, SelectionChangedEventArgs _) =>
		{
			DateTime? selectedDate = picker.SelectedDate;
			if (selectedDate.HasValue)
			{
				DateTime valueOrDefault = selectedDate.GetValueOrDefault();
				NavigateTo(DateOnly.FromDateTime(valueOrDefault));
			}
		};
		picker.OpenPicker();
	}
}
