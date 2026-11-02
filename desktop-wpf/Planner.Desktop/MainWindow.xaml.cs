using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using Planner.Core;
using Planner.Data;

namespace Planner.Desktop;

public partial class MainWindow : Window, IComponentConnector
{
	private GuidedAssistantPanel? guidedPanel;

	private DispatcherTimer? reminderTimer;

	private DateTime? reminderSince;

	private bool notifying;

	private readonly PlannerViewModel vm;

	private readonly StackPanel tables = new StackPanel();

	private readonly DockPanel details = new DockPanel();

	private readonly TextBlock status = Ui.Text("", 11.0);

	private readonly ScheduleBoard calendar;

	private readonly DockPanel navigation = new DockPanel();

	private readonly Border left;

	private readonly Border right;

	private readonly FrameworkElement center;

	private readonly FrameworkElement brand;

	private readonly Button compactSettings;

	private bool compact;

	private bool changingLayout;

	private bool closing;

	private double fullWidth;

	private double fullHeight;

	private double compactWidth;

	private double compactHeight;

	private SettingsPage? settingsPage;

	private readonly Grid workspace = new Grid();

	private readonly Button boardTab = Ui.Button("行程看板", () =>
	{
	});

	private readonly Button settingsTab = Ui.Button("设置", () =>
	{
	});

	private string workspacePage = "board";

	private readonly WorkspaceLayout layout = new WorkspaceLayout();

	private FrameworkElement? boardTabHost;

	private FrameworkElement? settingsTabHost;

	private EditorWindow? activeEditor;

	private readonly DispatcherTimer dateTimer = new DispatcherTimer
	{
		Interval = TimeSpan.FromSeconds(10L)
	};

	private bool? navigationExpanded;

	private Thumb? assistantSplitter;

	public bool IsCompact => compact;

	private void EnsureReminders()
	{
		if (reminderTimer != null)
		{
			return;
		}
		reminderSince = Clock.Now;
		reminderTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(20L)
		};
		reminderTimer.Tick += (object? _, EventArgs _) =>
		{
			Ui.Safe(() =>
			{
				if (!notifying)
				{
					DateTime now = Clock.Now;
					List<DueReminder> due = ReminderEngine.Due(vm.State, now, reminderSince ?? now);
					reminderSince = now;
					if (due.Count != 0)
					{
						notifying = true;
						try
						{
							vm.Mutate((PlannerState s) =>
							{
								foreach (DueReminder item in due)
								{
									s.Deliveries.Add(new ReminderDelivery
									{
										ActivityId = item.ActivityId,
										InstanceKey = item.Key,
										DueAt = item.DueAt
									});
								}
							});
							MessageBox.Show(string.Join("\n\n", due.Select((DueReminder r) => $"{r.Label} · {r.Title}\n计划提醒 {r.DueAt:HH:mm}")), "行程提醒", MessageBoxButton.OK, MessageBoxImage.Asterisk);
						}
						finally
						{
							notifying = false;
						}
					}
				}
			});
		};
		reminderTimer.Start();
		Dispatcher.BeginInvoke((Action)(() =>
		{
			Ui.Safe(() =>
			{
				List<DueReminder> missed = ReminderEngine.MissedToday(vm.State, Clock.Now);
				if (missed.Count != 0)
				{
					vm.Mutate((PlannerState s) =>
					{
						foreach (DueReminder item2 in missed)
						{
							s.Deliveries.Add(new ReminderDelivery
							{
								ActivityId = item2.ActivityId,
								InstanceKey = item2.Key,
								DueAt = item2.DueAt,
								Status = "MISSED"
							});
						}
					});
					MessageBox.Show("软件关闭时不会发送提醒。今天此前有以下未提示事项，现汇总一次：\n\n" + string.Join("\n", from r in missed.Take(12)
						select $"{r.DueAt:HH:mm} · {r.Title}"), "今日提醒汇总", MessageBoxButton.OK, MessageBoxImage.Asterisk);
				}
			});
		}));
	}

	public MainWindow(PlannerViewModel model)
	{
		InitializeComponent();
		vm = model;
		vm.UpdateAppearance((BoardPreferences p) =>
		{
			p.SidebarExpanded = true;
			p.TablesPanelOpen = true;
			p.AssistantPanelOpen = true;
			p.AssistantWidth = 280.0;
		});
		DataContext = vm;
		Typography.Apply(vm.Appearance.FontSize);
		Typography.Set(this, 13.0);
		fullWidth = vm.Appearance.FullWidth;
		fullHeight = vm.Appearance.FullHeight;
		compactWidth = vm.Appearance.CompactWidth;
		compactHeight = vm.Appearance.CompactHeight;
		Root.RowDefinitions.Add(new RowDefinition());
		Root.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		Root.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(62.0)
		});
		Root.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(0.0)
		});
		Root.ColumnDefinitions.Add(new ColumnDefinition());
		Root.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(0.0)
		});
		navigation.Background = Ui.Brush("#F1EEEA");
		navigation.Margin = new Thickness(0.0);
		Grid.SetRowSpan(navigation, 2);
		Root.Children.Add(navigation);
		left = new Border
		{
			Visibility = Visibility.Collapsed
		};
		DockPanel dockPanel = (DockPanel)(center = new DockPanel
		{
			Margin = new Thickness(14.0, 10.0, 14.0, 0.0)
		});
		Grid.SetColumn(dockPanel, 2);
		Root.Children.Add(dockPanel);
		DockPanel dockPanel2 = new DockPanel
		{
			Margin = new Thickness(2.0, 0.0, 2.0, 10.0)
		};
		DockPanel.SetDock(dockPanel2, Dock.Top);
		dockPanel.Children.Add(dockPanel2);
		WrapPanel wrapPanel = new WrapPanel
		{
			HorizontalAlignment = HorizontalAlignment.Right
		};
		DockPanel.SetDock(wrapPanel, Dock.Right);
		dockPanel2.Children.Add(wrapPanel);
		compactSettings = ShellIcons.Button("\ue713", "设置", () =>
		{
			OpenSettings();
		}, "NavSettingsCompact");
		wrapPanel.Children.Add(compactSettings);
		wrapPanel.Children.Add(Ui.Button("＋ 添加行程", () =>
		{
			OpenEditor(null);
		}, primary: true));
		StackPanel element = (StackPanel)(brand = new StackPanel
		{
			Children =
			{
				(UIElement)Ui.Text("南京市内智能行程表", 21.0, bold: true),
				(UIElement)Ui.Text("把每一天安排得从容一点", 11.0)
			}
		});
		dockPanel2.Children.Add(element);
		calendar = new ScheduleBoard(vm)
		{
			Focusable = true
		};
		calendar.MonthSpaceRequested += () =>
		{
			vm.UpdateAppearance((BoardPreferences p) =>
			{
				p.AssistantPanelOpen = false;
			});
		};
		StackPanel stackPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			Margin = new Thickness(0.0, 0.0, 0.0, 8.0)
		};
		DockPanel.SetDock(stackPanel, Dock.Top);
		dockPanel.Children.Add(stackPanel);
		boardTabHost = Ui.Tab(boardTab, () =>
		{
			CloseWorkspace("board");
		}, "CloseWorkspace:board");
		settingsTabHost = Ui.Tab(settingsTab, () =>
		{
			CloseWorkspace("settings");
		}, "CloseWorkspace:settings");
		(FrameworkElement, string)[] array = new (FrameworkElement, string)[2]
		{
			(boardTabHost, "board"),
			(settingsTabHost, "settings")
		};
		for (int num = 0; num < array.Length; num++)
		{
			(FrameworkElement Host, string Page) pair = array[num];
			ContextMenu menu = new ContextMenu();
			Ui.AddTabClosing(menu, (string mode) =>
			{
				CloseWorkspaceTabs(pair.Page, mode);
			}, (string mode) => TabClosing.Targets(layout.OpenPages, pair.Page, mode).Count > 0);
			Ui.AttachMenu(pair.Host, menu);
		}
		stackPanel.Children.Add(boardTabHost);
		stackPanel.Children.Add(settingsTabHost);
		settingsTabHost.Visibility = Visibility.Collapsed;
		boardTab.Tag = "WorkspaceBoard";
		settingsTab.Tag = "WorkspaceSettings";
		boardTab.Click += (object _, RoutedEventArgs _) =>
		{
			ShowBoard();
		};
		settingsTab.Click += (object _, RoutedEventArgs _) =>
		{
			ShowWorkspace("settings");
		};
		dockPanel.Children.Add(workspace);
		workspace.Children.Add(calendar);
		calendar.AddRequested += (DateTime d) =>
		{
			OpenEditor(null, d);
		};
		calendar.EditRequested += (Occurrence o) =>
		{
			OpenEditor(o);
		};
		calendar.Selected += ShowDetails;
		calendar.DeleteRequested += Delete;
		right = new Border
		{
			Background = Ui.Brush("#F6F0E7"),
			BorderBrush = Ui.Brush("#E5DBCF"),
			BorderThickness = new Thickness(1.0, 0.0, 0.0, 0.0),
			Padding = new Thickness(12.0),
			Child = details
		};
		Grid.SetColumn(right, 3);
		Root.Children.Add(right);
		BuildAssistant(details);
		AddAssistantSplitter();
		Grid.SetRow(status, 1);
		Grid.SetColumn(status, 1);
		Grid.SetColumnSpan(status, 3);
		status.Margin = new Thickness(14.0, 5.0, 14.0, 5.0);
		Root.Children.Add(status);
		dateTimer.Tick += (object? _, EventArgs _) =>
		{
			calendar.CheckDateChange(Clock.Today);
		};
		Activated += (object? _, EventArgs _) =>
		{
			calendar.CheckDateChange(Clock.Today);
		};
		dateTimer.Start();
		vm.Changed += Refresh;
		SizeChanged += (object _, SizeChangedEventArgs _) =>
		{
			if (!changingLayout)
			{
				RememberSize();
				ApplyLayout();
			}
		};
		Loaded += (object _, RoutedEventArgs _) =>
		{
			changingLayout = true;
			try
			{
				Rect workArea = SystemParameters.WorkArea;
				bool flag = vm.Appearance.LayoutMode == "COMPACT";
				Width = Math.Min(flag ? compactWidth : fullWidth, workArea.Width);
				Height = Math.Min(flag ? compactHeight : fullHeight, workArea.Height);
			}
			finally
			{
				changingLayout = false;
			}
			ApplyLayout();
		};
		PreviewKeyDown += (object _, KeyEventArgs e) =>
		{
			if (e.Key == Key.B && Keyboard.Modifiers == ModifierKeys.Control)
			{
				ToggleSidebar();
				e.Handled = true;
			}
		};
		Closing += (object? _, CancelEventArgs _) =>
		{
			closing = true;
			RememberSize();
			new BoardPreferenceStore(vm.DataDirectory).Update((BoardPreferences p) =>
			{
				p.FullWidth = fullWidth;
				p.FullHeight = fullHeight;
				p.CompactWidth = compactWidth;
				p.CompactHeight = compactHeight;
			});
		};
		Closed += (object? _, EventArgs _) =>
		{
			dateTimer.Stop();
			vm.Changed -= Refresh;
			OnClosedServices();
		};
		Refresh();
	}

	private void Refresh()
	{
		if (!closing)
		{
			AppTheme.Apply(vm.Appearance.Theme, vm.Appearance.AccentColor);
			guidedPanel?.RefreshPreferences();
			Typography.Apply(vm.Appearance.FontSize);
			BuildNavigation();
			ApplyLayout();
		}
	}

	public void ReturnToBoard()
	{
		ShowBoard();
	}

	private void ShowBoard()
	{
		ShowWorkspace("board");
		Activate();
		calendar.Focus();
	}

	private void ShowWorkspace(string page)
	{
		layout.Show(page);
		SyncWorkspace();
	}

	public void CloseWorkspace(string page)
	{
		layout.Close(page);
		if (layout.AssistantOnly && !vm.Appearance.AssistantPanelOpen)
		{
			vm.UpdateAppearance((BoardPreferences p) =>
			{
				p.AssistantPanelOpen = true;
			});
		}
		SyncWorkspace();
	}

	public void CloseWorkspaceTabs(string page, string mode)
	{
		layout.CloseMany(page, mode);
		if (layout.AssistantOnly && !vm.Appearance.AssistantPanelOpen)
		{
			vm.UpdateAppearance((BoardPreferences p) =>
			{
				p.AssistantPanelOpen = true;
			});
		}
		SyncWorkspace();
	}

	private void SyncWorkspace()
	{
		workspacePage = layout.Page;
		calendar.Visibility = ((!(workspacePage == "board")) ? Visibility.Collapsed : Visibility.Visible);
		if (settingsPage != null)
		{
			settingsPage.Visibility = ((!(workspacePage == "settings")) ? Visibility.Collapsed : Visibility.Visible);
		}
		if (boardTabHost != null)
		{
			boardTabHost.Visibility = ((!layout.BoardOpen) ? Visibility.Collapsed : Visibility.Visible);
		}
		if (settingsTabHost != null)
		{
			settingsTabHost.Visibility = ((!layout.SettingsOpen) ? Visibility.Collapsed : Visibility.Visible);
		}
		boardTab.Background = ((workspacePage == "board") ? AppTheme.Brush("Selected") : Brushes.Transparent);
		settingsTab.Background = ((workspacePage == "settings") ? AppTheme.Brush("Selected") : Brushes.Transparent);
		BuildNavigation();
		ApplyLayout();
	}

	public void ToggleTables()
	{
		vm.UpdateAppearance((BoardPreferences p) =>
		{
			p.SidebarExpanded = true;
			p.TablesPanelOpen = !p.TablesPanelOpen;
		});
	}

	private void ToggleSidebar()
	{
		vm.UpdateAppearance((BoardPreferences p) =>
		{
			p.SidebarExpanded = !p.SidebarExpanded;
		});
	}

	public void ToggleAssistant()
	{
		if (layout.AssistantOnly && vm.Appearance.AssistantPanelOpen)
		{
			layout.Show((layout.Page.Length > 0) ? layout.Page : "board");
			SyncWorkspace();
		}
		vm.UpdateAppearance((BoardPreferences p) =>
		{
			p.AssistantPanelOpen = !p.AssistantPanelOpen;
		});
	}

	private void RememberSize()
	{
		if (!IsLoaded || WindowState != WindowState.Normal)
		{
			return;
		}
		double actualWidth;
		if (compact)
		{
			actualWidth = ActualWidth;
			if (actualWidth >= 320.0 && actualWidth <= 800.0)
			{
				actualWidth = ActualHeight;
				if (actualWidth >= 460.0 && actualWidth <= 3000.0)
				{
					compactWidth = ActualWidth;
					compactHeight = ActualHeight;
					return;
				}
			}
		}
		if (compact)
		{
			return;
		}
		actualWidth = ActualWidth;
		if (actualWidth >= 1120.0 && actualWidth <= 4000.0)
		{
			actualWidth = ActualHeight;
			if (actualWidth >= 680.0 && actualWidth <= 3000.0)
			{
				fullWidth = ActualWidth;
				fullHeight = ActualHeight;
			}
		}
	}

	public void SetLayoutMode(string mode)
	{
		bool flag;
		switch (mode)
		{
		case "AUTO":
		case "FULL":
		case "COMPACT":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (!flag)
		{
			throw new ArgumentException("未知界面模式。");
		}
		RememberSize();
		vm.UpdateAppearance((BoardPreferences p) =>
		{
			p.LayoutMode = mode;
		});
		if (mode != "AUTO")
		{
			changingLayout = true;
			try
			{
				Rect workArea = SystemParameters.WorkArea;
				Width = Math.Min((mode == "COMPACT") ? compactWidth : fullWidth, workArea.Width);
				Height = Math.Min((mode == "COMPACT") ? compactHeight : fullHeight, workArea.Height);
				Left = Math.Clamp(Left, workArea.Left, Math.Max(workArea.Left, workArea.Right - Width));
				Top = Math.Clamp(Top, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - Height));
			}
			finally
			{
				changingLayout = false;
			}
		}
		ApplyLayout();
	}

	private void ApplyLayout()
	{
		if (changingLayout || closing)
		{
			return;
		}
		changingLayout = true;
		try
		{
			bool flag = layout.AssistantOnly && vm.Appearance.AssistantPanelOpen;
			compact = !flag && PresentationPolicy.Compact(vm.Appearance.LayoutMode, (ActualWidth > 0.0) ? ActualWidth : Width, (ActualHeight > 0.0) ? ActualHeight : Height, compact, vm.Appearance.FontSize);
			MinWidth = Math.Min(flag ? 360 : ((vm.Appearance.LayoutMode == "FULL") ? 1120 : 320), SystemParameters.WorkArea.Width);
			MinHeight = Math.Min((flag || compact) ? 460 : 680, SystemParameters.WorkArea.Height);
			DockPanel dockPanel = navigation;
			Visibility visibility = (brand.Visibility = (compact ? Visibility.Collapsed : Visibility.Visible));
			dockPanel.Visibility = visibility;
			left.Visibility = Visibility.Collapsed;
			right.Visibility = ((compact || !vm.Appearance.AssistantPanelOpen) ? Visibility.Collapsed : Visibility.Visible);
			Root.ColumnDefinitions[0].Width = new GridLength((!compact) ? ((flag && ((ActualWidth > 0.0) ? ActualWidth : Width) < 760.0) ? 56 : (vm.Appearance.SidebarExpanded ? 260 : 56)) : 0);
			Root.ColumnDefinitions[1].Width = new GridLength(0.0);
			bool flag2 = !compact && layout.AssistantOnly && vm.Appearance.AssistantPanelOpen;
			center.Visibility = (flag2 ? Visibility.Collapsed : Visibility.Visible);
			Grid.SetColumn(right, flag2 ? 2 : 3);
			Grid.SetColumnSpan(right, (!flag2) ? 1 : 2);
			if (navigationExpanded != (vm.Appearance.SidebarExpanded && (!flag || !(((ActualWidth > 0.0) ? ActualWidth : Width) < 760.0))))
			{
				BuildNavigation();
			}
			double num = ((ActualWidth > 0.0) ? ActualWidth : Width) - Root.ColumnDefinitions[0].Width.Value;
			Root.ColumnDefinitions[3].Width = new GridLength((!flag2 && right.Visibility == Visibility.Visible) ? WorkspaceLayout.AssistantWidth(vm.Appearance.AssistantWidth, num) : 0.0);
			if (assistantSplitter != null)
			{
				assistantSplitter.Visibility = ((flag2 || right.Visibility != Visibility.Visible) ? Visibility.Collapsed : Visibility.Visible);
			}
			calendar.SetResponsiveWidth(Math.Max(0.0, num - Root.ColumnDefinitions[3].Width.Value - 28.0));
			compactSettings.Visibility = ((!compact) ? Visibility.Collapsed : Visibility.Visible);
			center.Margin = (compact ? new Thickness(8.0, 6.0, 8.0, 0.0) : new Thickness(14.0, 10.0, 14.0, 0.0));
			Topmost = compact && vm.Appearance.AlwaysOnTop;
			Title = (compact ? "今日行程" : "南京市内智能行程表");
			calendar.SetCompact(compact);
			status.Text = vm.Status;
		}
		finally
		{
			changingLayout = false;
		}
	}

	public void OpenSettingsCategory(string category)
	{
		if (settingsPage == null)
		{
			settingsPage = new SettingsPage(vm, this, category);
			workspace.Children.Add(settingsPage);
		}
		else
		{
			settingsPage.SelectCategory(category);
		}
		ShowWorkspace("settings");
	}

	private void OpenTables()
	{
		if (compact)
		{
			OpenSettingsCategory("appearance");
		}
		else if (!vm.Appearance.TablesPanelOpen)
		{
			ToggleTables();
		}
	}

	private void ShowDetails(Occurrence? occurrence)
	{
		if ((object)occurrence != null)
		{
			vm.Notify($"{occurrence.Title} · {occurrence.Start:MM-dd HH:mm}—{occurrence.End:MM-dd HH:mm}");
		}
		else
		{
			details.Children.Clear();
			BuildAssistant(details);
		}
	}

	private void OpenEditor(Occurrence? o, DateTime? initial = null)
	{
		if (vm.State.Tables.Count == 0)
		{
			throw new ArgumentException("请先新建一份行程表。");
		}
		if (activeEditor != null)
		{
			activeEditor.Activate();
			return;
		}
		EditorWindow w = new EditorWindow(vm, o, initial)
		{
			Owner = this
		};
		activeEditor = w;
		w.Closed += (object? _, EventArgs _) =>
		{
			activeEditor = null;
			DateOnly? savedDate = w.SavedDate;
			if (savedDate.HasValue)
			{
				DateOnly valueOrDefault = savedDate.GetValueOrDefault();
				calendar.NavigateTo(valueOrDefault);
			}
		};
		w.Show();
	}

	private void Delete(Occurrence o)
	{
		if (Ui.Confirm("删除“" + o.Title + "”？\n" + ((!o.OriginDate.HasValue) ? "此项活动及关联路线和提醒将移除。" : "仅取消这次，不影响之后的每周安排。")))
		{
			vm.Edit(new _003C_003Ez__ReadOnlySingleElementList<EditCommand>(new EditCommand
			{
				Activity = JsonKit.Clone(o.Activity),
				OriginDate = o.OriginDate,
				Action = "DELETE"
			}));
		}
	}

	private void TableEditor(ScheduleTable? original)
	{
		Window w = new Window
		{
			Title = ((original == null) ? "新建行程表" : "行程表设置"),
			Width = 460.0,
			Height = Math.Min(640.0, SystemParameters.WorkArea.Height),
			Owner = this,
			WindowStartupLocation = WindowStartupLocation.CenterOwner,
			MinWidth = 340.0,
			MinHeight = 360.0
		};
		StackPanel stackPanel = new StackPanel
		{
			Margin = new Thickness(22.0)
		};
		w.Content = new ScrollViewer
		{
			Content = stackPanel,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto
		};
		string defaultName = original?.Name ?? (Clock.Today.ToString("MM-dd") + " 行程计划");
		TextBox name = Ui.Input(original?.Name ?? "");
		name.ToolTip = "可留空，默认使用“" + defaultName + "”";
		TableColorPicker colors = new TableColorPicker(original?.Color);
		StyledDateEntry from = new StyledDateEntry
		{
			SelectedDate = original?.ValidFrom?.ToDateTime(TimeOnly.MinValue)
		};
		StyledDateEntry until = new StyledDateEntry
		{
			SelectedDate = original?.ValidUntil?.ToDateTime(TimeOnly.MinValue)
		};
		Ui.Field(stackPanel, "名称（可选）", name);
		stackPanel.Children.Add(Ui.Text("行程表主色", 13.0, bold: true));
		stackPanel.Children.Add(colors);
		Ui.Field(stackPanel, "生效起日（可留空）", from);
		Ui.Field(stackPanel, "结束日期（可留空）", until);
		Button element = Ui.Button("保存", () =>
		{
			if (name.Text.Trim().Length > 60)
			{
				throw new ArgumentException("名称不能超过60字，可留空使用默认名称。");
			}
			if (from.SelectedDate > until.SelectedDate)
			{
				throw new ArgumentException("结束日早于起日。");
			}
			vm.Mutate((PlannerState s) =>
			{
				ScheduleTable scheduleTable = ((original == null) ? new ScheduleTable() : s.Tables.Single((ScheduleTable t) => t.Id == original.Id));
				scheduleTable.Name = (string.IsNullOrWhiteSpace(name.Text) ? defaultName : name.Text.Trim());
				scheduleTable.Color = colors.SelectedColor ?? original?.Color ?? TablePalette.Colors[0].Hex;
				DateTime? selectedDate = from.SelectedDate;
				DateOnly? validFrom;
				if (selectedDate.HasValue)
				{
					DateTime valueOrDefault = selectedDate.GetValueOrDefault();
					validFrom = DateOnly.FromDateTime(valueOrDefault);
				}
				else
				{
					validFrom = null;
				}
				scheduleTable.ValidFrom = validFrom;
				selectedDate = until.SelectedDate;
				DateOnly? validUntil;
				if (selectedDate.HasValue)
				{
					DateTime valueOrDefault2 = selectedDate.GetValueOrDefault();
					validUntil = DateOnly.FromDateTime(valueOrDefault2);
				}
				else
				{
					validUntil = null;
				}
				scheduleTable.ValidUntil = validUntil;
				scheduleTable.Version++;
				if (original == null)
				{
					s.Tables.Add(scheduleTable);
				}
				foreach (PlanLeg leg in s.Legs)
				{
					leg.ValidationStatus = "STALE";
				}
			});
			w.Close();
		}, primary: true);
		stackPanel.Children.Add(element);
		if (original != null)
		{
			stackPanel.Children.Add(Ui.Button("删除行程表", () =>
			{
				int value = Enumerable.Count(vm.State.Activities, (Activity a) => a.TableId == original.Id);
				if (Ui.Confirm($"删除“{original.Name}”及其 {value} 项活动、周期例外和关联路线？此操作不可撤销。"))
				{
					vm.Mutate((PlannerState s) =>
					{
						foreach (string item in (from a in s.Activities
							where a.TableId == original.Id
							select a.Id).ToList())
						{
							ChangeService.RemoveActivity(s, item);
						}
						s.Scopes.RemoveAll((PlanScope x) => x.TableId == original.Id);
						s.Tables.RemoveAll((ScheduleTable t) => t.Id == original.Id);
					});
					if (vm.SelectedTableId == original.Id)
					{
						vm.SelectedTableId = vm.State.Tables.FirstOrDefault()?.Id ?? "";
					}
					w.Close();
				}
			}));
		}
		w.ShowDialog();
	}

	private void BuildAssistant(Panel host)
	{
		EnsureReminders();
		if (guidedPanel == null)
		{
			guidedPanel = new GuidedAssistantPanel(vm, this);
		}
		host.Children.Add(guidedPanel);
	}

	private void OpenSettings()
	{
		OpenSettingsCategory("appearance");
	}

	private void OpenRoutes()
	{
		RouteWindow routeWindow = new RouteWindow(vm);
		routeWindow.Owner = this;
		routeWindow.ShowDialog();
	}

	private void OnClosedServices()
	{
		guidedPanel?.Dispose();
		reminderTimer?.Stop();
	}

	private void BuildNavigation()
	{
		if (tables.Parent is Panel panel)
		{
			panel.Children.Remove(tables);
		}
		navigation.Children.Clear();
		bool expanded = vm.Appearance.SidebarExpanded && (!layout.AssistantOnly || !(((ActualWidth > 0.0) ? ActualWidth : Width) < 760.0));
		navigationExpanded = expanded;
		DockPanel dockPanel = new DockPanel
		{
			Margin = new Thickness(5.0, 8.0, 5.0, 10.0)
		};
		DockPanel.SetDock(dockPanel, Dock.Top);
		navigation.Children.Add(dockPanel);
		Button element = ShellIcons.Button("\ue700", "显示/隐藏侧边栏", ToggleSidebar, "NavToggle");
		DockPanel.SetDock(element, Dock.Right);
		dockPanel.Children.Add(element);
		if (expanded && vm.Appearance.Theme == "万圣节")
		{
			Button element2 = SeasonalDecorations.Badge();
			DockPanel.SetDock(element2, Dock.Right);
			dockPanel.Children.Add(element2);
		}
		if (expanded)
		{
			Button element3 = ShellIcons.Button("\ue721", "搜索行程和行程表", OpenSearch, "NavSearch");
			DockPanel.SetDock(element3, Dock.Right);
			dockPanel.Children.Add(element3);
			dockPanel.Children.Add(new TextBlock
			{
				Text = "南京行程",
				FontSize = 18.0,
				FontWeight = FontWeights.SemiBold,
				Margin = new Thickness(10.0, 0.0, 0.0, 0.0),
				VerticalAlignment = VerticalAlignment.Center
			});
		}
		StackPanel stackPanel = new StackPanel
		{
			Margin = new Thickness(5.0, 6.0, 5.0, 10.0)
		};
		DockPanel.SetDock(stackPanel, Dock.Bottom);
		navigation.Children.Add(stackPanel);
		if (!expanded && vm.Appearance.Theme == "万圣节")
		{
			stackPanel.Children.Add(SeasonalDecorations.Badge());
		}
		Button button = ShellIcons.Button("\ue77b", vm.Appearance.Nickname, () =>
		{
			OpenSettingsCategory("profile");
		}, "NavProfile", expanded);
		if (button.Content is StackPanel stackPanel2)
		{
			stackPanel2.Children.RemoveAt(0);
			stackPanel2.Children.Insert(0, new Border
			{
				Width = 24.0,
				Height = 24.0,
				CornerRadius = new CornerRadius(12.0),
				Background = Ui.Brush("#D8B6A0"),
				Child = new TextBlock
				{
					Text = vm.Appearance.Avatar,
					FontSize = 14.0,
					HorizontalAlignment = HorizontalAlignment.Center,
					VerticalAlignment = VerticalAlignment.Center
				}
			});
		}
		stackPanel.Children.Add(button);
		Button button2 = ShellIcons.Button("\ue713", "设置", () =>
		{
			OpenSettings();
		}, "NavSettings", expanded);
		if (workspacePage == "settings")
		{
			button2.Background = AppTheme.Brush("Selected");
		}
		stackPanel.Children.Add(button2);
		StackPanel content = new StackPanel
		{
			Margin = new Thickness(5.0, 0.0, 5.0, 0.0)
		};
		navigation.Children.Add(new ScrollViewer
		{
			Content = content,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
			HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
		});
		if (!expanded)
		{
			Add("\ue721", "搜索行程和行程表", OpenSearch, "NavSearch");
		}
		Add("\ue70f", "新建行程", () =>
		{
			OpenEditor(null);
		}, "NavNew");
		Add("\ue787", "行程看板", ShowBoard, "NavBoard", workspacePage == "board");
		Add("\ue8f1", "我的行程表", ToggleTables, "NavTables", vm.Appearance.TablesPanelOpen & expanded);
		if (expanded && vm.Appearance.TablesPanelOpen)
		{
			BuildTableRows();
			content.Children.Add(tables);
			Button button3 = Ui.Button("＋ 新建行程表", () =>
			{
				TableEditor(null);
			});
			button3.Background = Brushes.Transparent;
			button3.HorizontalContentAlignment = HorizontalAlignment.Left;
			content.Children.Add(button3);
		}
		Add("\ue707", "地图与路线", () =>
		{
			OpenRoutes();
		}, "NavMap");
		Add("\ue99a", "智能助手", ToggleAssistant, "NavAssistant", vm.Appearance.AssistantPanelOpen);
		void Add(string glyph, string label, Action action, string tag, bool active = false)
		{
			Button button4 = ShellIcons.Button(glyph, label, action, tag, expanded);
			if (expanded)
			{
				button4.HorizontalContentAlignment = HorizontalAlignment.Left;
			}
			if (active)
			{
				button4.Background = Ui.Brush("#E5E1DC");
			}
			content.Children.Add(button4);
		}
	}

	private void BuildTableRows()
	{
		tables.Children.Clear();
		foreach (ScheduleTable table2 in vm.State.Tables)
		{
			ScheduleTable table = table2;
			Grid grid = new Grid
			{
				Margin = new Thickness(14.0, 1.0, 2.0, 1.0)
			};
			grid.ColumnDefinitions.Add(new ColumnDefinition());
			grid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = GridLength.Auto
			});
			StackPanel stackPanel = new StackPanel
			{
				Orientation = Orientation.Horizontal
			};
			stackPanel.Children.Add(new Border
			{
				Background = Ui.RawBrush(table.Color),
				Width = 8.0,
				Height = 8.0,
				CornerRadius = new CornerRadius(4.0),
				Margin = new Thickness(0.0, 0.0, 8.0, 0.0)
			});
			TextBlock tableName = new TextBlock
			{
				Text = table.Name,
				TextTrimming = TextTrimming.CharacterEllipsis,
				VerticalAlignment = VerticalAlignment.Center,
				MaxWidth = 120.0
			};
			stackPanel.Children.Add(tableName);
			Button button = Ui.Button("", () =>
			{
				vm.SelectedTableId = table.Id;
				vm.Notify("新行程默认加入：" + table.Name);
			});
			button.Content = stackPanel;
			button.Padding = new Thickness(8.0);
			button.HorizontalContentAlignment = HorizontalAlignment.Left;
			button.Background = ((table.Id == vm.SelectedTableId) ? Ui.Brush("#E5E1DC") : Brushes.Transparent);
			grid.Children.Add(button);
			button.PreviewMouseLeftButtonDown += (object _, MouseButtonEventArgs e) =>
			{
				if (e.ClickCount == 2)
				{
					e.Handled = true;
					Rename();
				}
			};
			StackPanel stackPanel2 = new StackPanel
			{
				Orientation = Orientation.Horizontal
			};
			Grid.SetColumn(stackPanel2, 1);
			grid.Children.Add(stackPanel2);
			Button button2 = Ui.Button(vm.HiddenTables.Contains(table.Id) ? "◌" : "◉", () =>
			{
				if (!vm.HiddenTables.Add(table.Id))
				{
					vm.HiddenTables.Remove(table.Id);
				}
				vm.UpdateAppearance((BoardPreferences p) =>
				{
					p.HiddenTables = new HashSet<string>(vm.HiddenTables);
				});
			});
			button2.ToolTip = "显示/隐藏“" + table.Name + "”（不改变是否参与排程）";
			button2.Padding = new Thickness(5.0);
			button2.Background = Brushes.Transparent;
			stackPanel2.Children.Add(button2);
			ContextMenu menu = new ContextMenu();
			MenuItem menuItem = new MenuItem
			{
				Header = "重命名"
			};
			menuItem.Click += (object _, RoutedEventArgs _) =>
			{
				Rename();
			};
			menu.Items.Add(menuItem);
			MenuItem menuItem2 = new MenuItem
			{
				Header = "行程表设置…"
			};
			menuItem2.Click += (object _, RoutedEventArgs _) =>
			{
				TableEditor(table);
			};
			menu.Items.Add(menuItem2);
			MenuItem active = new MenuItem
			{
				Header = "参与排程",
				IsCheckable = true,
				IsChecked = table.Enabled
			};
			active.Click += (object _, RoutedEventArgs _) =>
			{
				Ui.Safe(() =>
				{
					if (Ui.Confirm("更改“" + table.Name + "”是否参与排程？"))
					{
						vm.Mutate((PlannerState s) =>
						{
							ScheduleTable scheduleTable = s.Tables.Single((ScheduleTable t) => t.Id == table.Id);
							scheduleTable.Enabled = active.IsChecked;
							scheduleTable.Version++;
							foreach (PlanLeg leg in s.Legs)
							{
								leg.ValidationStatus = "STALE";
							}
							foreach (Plan plan in s.Plans)
							{
								plan.Status = "STALE";
							}
						});
					}
				});
			};
			menu.Items.Add(active);
			Button more = Ui.Button("…", () =>
			{
			});
			more.Click += (object _, RoutedEventArgs _) =>
			{
				Ui.OpenMenu(menu, more);
			};
			more.Tag = "TableEdit:" + table.Id;
			more.ToolTip = "行程表操作";
			more.Padding = new Thickness(5.0);
			more.Background = Brushes.Transparent;
			Ui.AttachMenu(more, menu);
			Ui.AttachMenu(grid, menu);
			stackPanel2.Children.Add(more);
			more.Opacity = 0.0;
			grid.MouseEnter += (object _, MouseEventArgs _) =>
			{
				more.Opacity = 1.0;
			};
			grid.MouseLeave += (object _, MouseEventArgs _) =>
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
			tables.Children.Add(grid);
			void Rename()
			{
				Ui.InlineRename(tableName, (string value) =>
				{
					vm.Mutate((PlannerState state) =>
					{
						if (value.Length > 60)
						{
							throw new ArgumentException("名称不能超过60字。");
						}
						ScheduleTable scheduleTable = state.Tables.Single((ScheduleTable t) => t.Id == table.Id);
						scheduleTable.Name = value;
						scheduleTable.Version++;
					});
				});
			}
		}
	}

	private void OpenSearch()
	{
		Window window = new Window
		{
			Title = "搜索行程与行程表",
			Owner = this,
			Width = 680.0,
			Height = 560.0,
			MinWidth = 380.0,
			MinHeight = 320.0,
			WindowStartupLocation = WindowStartupLocation.CenterOwner
		};
		DockPanel dockPanel = new DockPanel
		{
			Margin = new Thickness(20.0)
		};
		window.Content = dockPanel;
		TextBox input = Ui.Input();
		input.Tag = "LocalSearchInput";
		input.ToolTip = "搜索标题、地点、备注和行程表名称";
		DockPanel.SetDock(input, Dock.Top);
		dockPanel.Children.Add(input);
		TextBlock count = Ui.Text("输入关键词，搜索本机保存的内容（包含已完成行程）", 11.0);
		DockPanel.SetDock(count, Dock.Top);
		dockPanel.Children.Add(count);
		StackPanel results = new StackPanel();
		dockPanel.Children.Add(new ScrollViewer
		{
			Content = results,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
			HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
		});
		input.TextChanged += (object _, TextChangedEventArgs _) =>
		{
			results.Children.Clear();
			List<LocalSearchHit> list = LocalSearch.Find(vm.State, input.Text, Clock.Today);
			count.Text = ((input.Text.Trim().Length == 0) ? "搜索本地行程和行程表" : ("找到 " + list.Count + " 项" + ((list.Count > 200) ? "，显示前200项，请细化关键词" : "")));
			foreach (LocalSearchHit hit in list.Take(200))
			{
				StackPanel content = new StackPanel
				{
					Children =
					{
						(UIElement)Ui.Text(hit.Title, 14.0, bold: true),
						(UIElement)Ui.Text(hit.Detail, 11.0)
					}
				};
				Button button = Ui.Button("", () =>
				{
					window.Close();
					OpenSearchHit(hit);
				});
				button.Content = content;
				button.HorizontalContentAlignment = HorizontalAlignment.Left;
				button.Background = Brushes.Transparent;
				results.Children.Add(button);
			}
		};
		window.Loaded += (object _, RoutedEventArgs _) =>
		{
			input.Focus();
		};
		window.PreviewKeyDown += (object _, KeyEventArgs e) =>
		{
			if (e.Key == Key.Escape)
			{
				window.Close();
				e.Handled = true;
			}
		};
		window.Show();
	}

	private void OpenSearchHit(LocalSearchHit hit)
	{
		vm.SelectedTableId = hit.TableId;
		vm.UpdateAppearance((BoardPreferences p) =>
		{
			p.SidebarExpanded = true;
			p.TablesPanelOpen = true;
		});
		if (hit.Kind == "table")
		{
			return;
		}
		DateOnly? day = hit.Day;
		if (day.HasValue)
		{
			DateOnly valueOrDefault = day.GetValueOrDefault();
			vm.HiddenTables.Remove(hit.TableId);
			vm.UpdateAppearance((BoardPreferences p) =>
			{
				p.HiddenTables = new HashSet<string>(vm.HiddenTables);
			});
			ShowBoard();
			calendar.NavigateTo(valueOrDefault);
			Occurrence occurrence = CalendarEngine.ExpandForDisplay(vm.State, valueOrDefault.ToDateTime(TimeOnly.MinValue), valueOrDefault.AddDays(1).ToDateTime(TimeOnly.MinValue), new _003C_003Ez__ReadOnlySingleElementList<string>(hit.TableId)).FirstOrDefault((Occurrence o) => o.Activity.Id == hit.Id);
			if ((object)occurrence != null)
			{
				OpenEditor(occurrence);
			}
		}
	}

	private void AddAssistantSplitter()
	{
		Thumb thumb = (assistantSplitter = new Thumb
		{
			Width = 6.0,
			HorizontalAlignment = HorizontalAlignment.Left,
			Cursor = Cursors.SizeWE,
			Background = Brushes.Transparent,
			ToolTip = "拖动调整助手宽度"
		});
		Grid.SetColumn(thumb, 3);
		Root.Children.Add(thumb);
		double width = vm.Appearance.AssistantWidth;
		thumb.DragStarted += (object _, DragStartedEventArgs _) =>
		{
			width = Root.ColumnDefinitions[3].ActualWidth;
		};
		thumb.DragDelta += (object _, DragDeltaEventArgs e) =>
		{
			width = WorkspaceLayout.AssistantWidth(width - e.HorizontalChange, ActualWidth - Root.ColumnDefinitions[0].ActualWidth);
			Root.ColumnDefinitions[3].Width = new GridLength(width);
		};
		thumb.DragCompleted += (object _, DragCompletedEventArgs _) =>
		{
			vm.UpdateAppearance((BoardPreferences p) =>
			{
				p.AssistantWidth = width;
			});
		};
	}
}
