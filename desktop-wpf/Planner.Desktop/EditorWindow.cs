using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Planner.Core;
using Planner.Data;

namespace Planner.Desktop;

public sealed class EditorWindow : Window
{
	private readonly PlannerViewModel vm;

	private readonly Occurrence? occurrence;

	private readonly long openedRevision;

	private readonly CancellationTokenSource cancel = new CancellationTokenSource();

	private readonly TextBox title;

	private readonly TextBox place;

	private readonly TextBox windowStart;

	private readonly TextBox windowEnd;

	private readonly TextBox note;

	private readonly TextBox lead;

	private readonly TimeEntry start;

	private readonly TimeEntry end;

	private readonly DatePicker date;

	private readonly DatePicker endDate;

	private readonly DatePicker until;

	private readonly ComboBox table;

	private readonly ComboBox kind;

	private readonly ComboBox constraint;

	private readonly ComboBox scope;

	private readonly CheckBox reminder = new CheckBox
	{
		Content = "启用提醒"
	};

	private readonly CheckBox overnight = new CheckBox
	{
		Content = "结束于次日"
	};

	private readonly CheckBox[] weekdays;

	private readonly StackPanel form = new StackPanel();

	private readonly TextBlock trafficHint = Ui.Text("保存时自动检查前后行程是否来得及；缺少信息时再询问。", 11.0);

	private readonly TextBlock durationLabel = Ui.Text("", 12.0);

	private readonly TextBlock status = Ui.Text("", 11.0);

	private bool closing;

	private bool syncingTimes;

	private DateTime previousStart;

	public DateOnly? SavedDate { get; private set; }

	public string DurationText => durationLabel.Text;

	private async Task<(PlannerState, List<TrafficSnapshot>)> CheckAdjacentAsync(PlannerState proposed, Occurrence target, EditCommand command)
	{
		List<TrafficPair> pairs = AdjacentTraffic.Around(proposed, target);
		List<TrafficSnapshot> results = new List<TrafficSnapshot>();
		List<string> warnings = new List<string>();
		if (pairs.Count == 0)
		{
			return (proposed, results);
		}
		Dictionary<string, TrafficSnapshot> stored = new TrafficSnapshotStore(vm.DataDirectory).Load();
		ServiceSettings serviceSettings = new SettingsStore(vm.DataDirectory).Load();
		bool online = false;
		RepositoryMap map = null;
		string setupError = "尚未配置高德 Web 服务 Key，无法查询实际交通时间。";
		try
		{
			online = SettingsStore.Unprotect(serviceSettings.MapServiceKeyProtected).Length > 0;
			if (online)
			{
				map = new RepositoryMap(serviceSettings);
			}
		}
		catch
		{
			setupError = "地图服务配置无法读取，请在设置中重新保存密钥。";
		}
		Dictionary<string, Place> resolved = new Dictionary<string, Place>();
		bool appliedTarget = false;
		for (int index = 0; index < pairs.Count; index++)
		{
			cancel.Token.ThrowIfCancellationRequested();
			TrafficPair pair = pairs[index];
			string fingerprint = pair.Fingerprint(proposed);
			TrafficSnapshot trafficSnapshot = stored.GetValueOrDefault(fingerprint);
			if (!(trafficSnapshot?.Minutes).HasValue)
			{
				try
				{
					Place from = proposed.Places.FirstOrDefault((Place p) => p.Id == pair.From.PlaceId);
					Place to = proposed.Places.FirstOrDefault((Place p) => p.Id == pair.To.PlaceId);
					bool num = from != null && to != null && from.Id == to.Id && from.Lng.HasValue && from.Lat.HasValue && from.Status != "UNVERIFIED";
					string mode = "同一地点";
					TravelEstimate travelEstimate;
					if (num)
					{
						travelEstimate = new TravelEstimate(0, "同一地点");
					}
					else
					{
						if (!online)
						{
							throw new InvalidOperationException(setupError);
						}
						from = await Resolve(pair.From);
						to = await Resolve(pair.To);
						Place valueOrDefault = resolved.GetValueOrDefault(target.Key);
						if (valueOrDefault != null && !appliedTarget && valueOrDefault.Id != target.PlaceId)
						{
							command.Place = valueOrDefault;
							command.Activity.PlaceId = valueOrDefault.Id;
							proposed = ChangeService.Apply(vm.State, new _003C_003Ez__ReadOnlySingleElementList<EditCommand>(command), openedRevision);
							Occurrence occurrence = CalendarEngine.ExpandForDisplay(proposed, target.Start.Date, target.End.Date.AddDays(1.0)).First((Occurrence o) => o.Start == target.Start && o.End == target.End && o.Title == target.Title && o.Activity.TableId == target.Activity.TableId);
							for (int num2 = 0; num2 < pairs.Count; num2++)
							{
								TrafficPair trafficPair = pairs[num2];
								pairs[num2] = ((trafficPair.From.Key == target.Key) ? new TrafficPair(occurrence, trafficPair.To) : new TrafficPair(trafficPair.From, occurrence));
							}
							pair = pairs[index];
							appliedTarget = true;
							target = occurrence;
							resolved[target.Key] = valueOrDefault;
							fingerprint = pair.Fingerprint(proposed);
						}
						mode = await TrafficDialogs.Mode(this, pair, stored.Values.FirstOrDefault((TrafficSnapshot x) => x.FromKey == pair.From.Key && x.ToKey == pair.To.Key)?.Mode, cancel.Token);
						status.Text = "正在检查：" + pair.From.Title + " → " + pair.To.Title;
						travelEstimate = await map.TravelAsync(from, to, mode, pair.From.End, cancel.Token);
						travelEstimate = travelEstimate with
						{
							Source = from.Name + " → " + to.Name + " · " + travelEstimate.Source
						};
					}
					trafficSnapshot = new TrafficSnapshot
					{
						Fingerprint = fingerprint,
						FromKey = pair.From.Key,
						ToKey = pair.To.Key,
						Minutes = travelEstimate.Minutes,
						Mode = mode,
						Source = travelEstimate.Source
					};
				}
				catch (OperationCanceledException)
				{
					throw;
				}
				catch (Exception ex2)
				{
					trafficSnapshot = new TrafficSnapshot
					{
						Fingerprint = fingerprint,
						FromKey = pair.From.Key,
						ToKey = pair.To.Key,
						Source = ex2.Message
					};
					warnings.Add(pair.From.Title + " → " + pair.To.Title + "：交通未核验。" + ex2.Message);
				}
			}
			results.Add(trafficSnapshot);
			int? minutes = trafficSnapshot.Minutes;
			if (minutes.HasValue)
			{
				int valueOrDefault2 = minutes.GetValueOrDefault();
				if (valueOrDefault2 > pair.GapMinutes)
				{
					warnings.Add(pair.From.Title + " → " + pair.To.Title + "：" + AdjacentTraffic.Label(pair, trafficSnapshot));
				}
			}
		}
		if (command.Scope == "FUTURE")
		{
			warnings.Add("交通结果仅针对本次日期；后续重复日期需按当天安排重新检查。");
		}
		bool flag = warnings.Count > 0;
		if (flag)
		{
			flag = !(await TrafficDialogs.SaveWarning(this, string.Join("\n\n", warnings), cancel.Token));
		}
		if (flag)
		{
			throw new OperationCanceledException();
		}
		return (proposed, results);
		async Task<Place> Resolve(Occurrence o)
		{
			if (resolved.TryGetValue(o.Key, out Place value))
			{
				return value;
			}
			Place place = proposed.Places.FirstOrDefault((Place p) => p.Id == o.PlaceId);
			if ((place?.Lng).HasValue && place.Lat.HasValue && place.Status != "UNVERIFIED")
			{
				return resolved[o.Key] = place;
			}
			string text = place?.Name ?? "";
			if (text.Length == 0)
			{
				text = await TrafficDialogs.Location(this, o.Title, cancel.Token);
			}
			status.Text = "正在查询地点：" + text;
			Window owner = this;
			Place place3 = await RepositoryDialogs.ChoosePlace(owner, await map.SearchAsync(text, cancel.Token), cancel.Token);
			resolved[o.Key] = place3;
			return place3;
		}
	}

	public EditorWindow(PlannerViewModel model, Occurrence? item = null, DateTime? initial = null)
	{
		EditorWindow editorWindow = this;
		vm = model;
		occurrence = item;
		openedRevision = vm.State.Revision;
		Title = (((object)item == null) ? "自定义添加" : "编辑行程");
		Width = 820.0;
		Height = 760.0;
		MinWidth = 440.0;
		MinHeight = 360.0;
		WindowStartupLocation = WindowStartupLocation.CenterOwner;
		Background = Ui.Brush("#FBF7F0");
		FontFamily = new FontFamily("Microsoft YaHei UI");
		Rect workArea = SystemParameters.WorkArea;
		Width = Math.Min(Width, workArea.Width);
		Height = Math.Min(Height, workArea.Height);
		MinWidth = Math.Min(MinWidth, workArea.Width);
		MinHeight = Math.Min(MinHeight, workArea.Height);
		DockPanel dockPanel = (DockPanel)(Content = new DockPanel
		{
			Margin = new Thickness(20.0)
		});
		Grid grid = new Grid
		{
			Margin = new Thickness(0.0, 12.0, 0.0, 0.0)
		};
		grid.ColumnDefinitions.Add(new ColumnDefinition());
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		DockPanel.SetDock(grid, Dock.Bottom);
		dockPanel.Children.Add(grid);
		StackPanel stackPanel = Ui.Row(Ui.Button("取消", Close));
		Grid.SetColumn(stackPanel, 1);
		grid.Children.Add(stackPanel);
		status.MinWidth = 0.0;
		grid.Children.Add(status);
		Button save = Ui.Button("保存", () =>
		{
		}, primary: true);
		stackPanel.Children.Add(save);
		save.Click += async (object _, RoutedEventArgs _) =>
		{
			if (!editorWindow.closing)
			{
				if (Ui.ValidateRequired(editorWindow))
				{
					save.IsEnabled = false;
					editorWindow.form.IsEnabled = false;
					editorWindow.status.Text = "正在检查前后行程…";
					try
					{
						await editorWindow.SaveAsync();
						return;
					}
					catch (OperationCanceledException)
					{
						if (!editorWindow.closing)
						{
							editorWindow.status.Text = "已返回编辑，尚未保存。";
						}
						return;
					}
					catch (Exception ex2)
					{
						editorWindow.status.Text = ex2.Message;
						return;
					}
					finally
					{
						if (!editorWindow.closing)
						{
							save.IsEnabled = true;
							editorWindow.form.IsEnabled = true;
						}
					}
				}
				editorWindow.status.Text = "请填写标记的必填项。";
			}
		};
		StackPanel stackPanel2 = form;
		dockPanel.Children.Add(new ScrollViewer
		{
			Content = stackPanel2,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
			HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
			HorizontalContentAlignment = HorizontalAlignment.Stretch
		});
		DateTime dateTime = item?.Start ?? initial ?? vm.SelectedDate.ToDateTime(new TimeOnly(9, 0));
		DateTime dateTime2 = item?.End ?? dateTime.AddHours(1.0);
		title = Ui.Input(item?.Title ?? "");
		title.Tag = "ActivityTitle";
		Typography.Set(title, 14.0);
		title.FontWeight = FontWeights.SemiBold;
		title.AcceptsReturn = false;
		title.TextWrapping = TextWrapping.NoWrap;
		title.MinLines = (title.MaxLines = 1);
		title.MinHeight = 32.0;
		title.Padding = new Thickness(10.0, 5.0, 10.0, 5.0);
		Ui.Field(stackPanel2, ((object)item == null) ? "添加一项安排" : "调整这项安排", title);
		stackPanel2.Children.Add(Ui.Text(((object)item == null) ? "填写活动、日期与时间，其他信息可以稍后完善。" : (new BoardPreferenceStore(vm.DataDirectory).Load().Completed.Contains(item.Key) ? "已完成" : "待进行"), 12.0));
		place = Ui.Input(((object)item == null) ? "" : vm.State.PlaceName(item.PlaceId));
		if (item?.PlaceId == null)
		{
			place.Text = "";
		}
		date = new StyledDateEntry
		{
			SelectedDate = dateTime.Date,
			Tag = "StartDate"
		};
		endDate = new StyledDateEntry
		{
			SelectedDate = dateTime2.Date,
			Tag = "EndDate"
		};
		until = new StyledDateEntry();
		start = new TimeEntry(dateTime.ToString("HH:mm"))
		{
			Tag = "StartTime",
			DateContext = dateTime.Date
		};
		end = new TimeEntry(dateTime2.ToString("HH:mm"))
		{
			Tag = "EndTime",
			DateContext = dateTime2.Date
		};
		previousStart = dateTime;
		AddPair(stackPanel2, "开始日期", date, "结束日期", endDate);
		AddPair(stackPanel2, "开始时间", start, "结束时间", end);
		Ui.Field(stackPanel2, "地点", place);
		start.Changed += StartChanged;
		end.Changed += EndChanged;
		start.DateTimeChanged += (DateTime _) =>
		{
			editorWindow.StartChanged();
		};
		end.DateTimeChanged += (DateTime _) =>
		{
			editorWindow.EndChanged();
		};
		date.SelectedDateChanged += (object? _, SelectionChangedEventArgs _) =>
		{
			if (!editorWindow.syncingTimes)
			{
				DateTime? selectedDate = editorWindow.date.SelectedDate;
				if (selectedDate.HasValue)
				{
					DateTime valueOrDefault = selectedDate.GetValueOrDefault();
					editorWindow.start.DateContext = valueOrDefault.Date;
					editorWindow.StartChanged();
				}
			}
		};
		endDate.SelectedDateChanged += (object? _, SelectionChangedEventArgs _) =>
		{
			if (!editorWindow.syncingTimes)
			{
				DateTime? selectedDate = editorWindow.endDate.SelectedDate;
				if (selectedDate.HasValue)
				{
					DateTime valueOrDefault = selectedDate.GetValueOrDefault();
					editorWindow.end.DateContext = valueOrDefault.Date;
					editorWindow.EndChanged();
				}
			}
		};
		WrapPanel wrapPanel = new WrapPanel
		{
			Tag = "QuickDurations",
			Margin = new Thickness(0.0, 3.0, 0.0, 0.0)
		};
		int[] array = new int[4] { 30, 60, 90, 120 };
		for (int num2 = 0; num2 < array.Length; num2++)
		{
			int num3 = array[num2];
			int duration = num3;
			Button button = Ui.Button(num3 + " 分钟", () =>
			{
				if (TimeOnly.TryParseExact(editorWindow.start.Text, "HH:mm", out var result))
				{
					DateTime? selectedDate = editorWindow.date.SelectedDate;
					if (selectedDate.HasValue)
					{
						DateTime valueOrDefault = selectedDate.GetValueOrDefault();
						editorWindow.SetEnd(valueOrDefault.Date.Add(result.ToTimeSpan()).AddMinutes(duration));
					}
				}
			});
			button.Tag = "QuickDuration:" + num3;
			button.Padding = new Thickness(10.0, 4.0, 10.0, 4.0);
			button.Margin = new Thickness(0.0, 0.0, 6.0, 6.0);
			button.MinHeight = 30.0;
			Typography.Set(button, 12.0);
			wrapPanel.Children.Add(button);
		}
		stackPanel2.Children.Add(wrapPanel);
		durationLabel.Tag = "DurationSummary";
		durationLabel.Margin = new Thickness(0.0, 0.0, 0.0, 8.0);
		stackPanel2.Children.Add(durationLabel);
		stackPanel2.Children.Add(trafficHint);
		note = Ui.Input(item?.Activity.Note ?? "");
		note.AcceptsReturn = true;
		note.TextWrapping = TextWrapping.Wrap;
		note.MinHeight = 90.0;
		Ui.Field(stackPanel2, "备注", note);
		table = new ComboBox();
		foreach (ScheduleTable table in vm.State.Tables)
		{
			this.table.Items.Add(new ComboBoxItem
			{
				Content = table.Name,
				Tag = table.Id
			});
		}
		this.table.SelectedIndex = Math.Max(0, vm.State.Tables.FindIndex((ScheduleTable t) => t.Id == (item?.Activity.TableId ?? editorWindow.vm.SelectedTableId)));
		kind = Ui.Choices(new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "具体某一天", "每周重复" }), (item?.Activity.Kind == "WEEKLY") ? 1 : 0);
		constraint = Ui.Choices(new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "固定时间", "允许规划时调整" }), (item?.Activity.ConstraintType == "FLEXIBLE") ? 1 : 0);
		IEnumerable<string> values;
		if (!(item?.Activity.Kind == "WEEKLY"))
		{
			IEnumerable<string> enumerable = new _003C_003Ez__ReadOnlySingleElementList<string>("此项活动");
			values = enumerable;
		}
		else
		{
			IEnumerable<string> enumerable = new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "仅这一次", "从所选日期起调整整套规则" });
			values = enumerable;
		}
		scope = Ui.Choices(values);
		RecurrenceRule rule = vm.State.Rules.FirstOrDefault((RecurrenceRule r) => r.ActivityId == item?.Activity.Id);
		weekdays = (from i in Enumerable.Range(0, 7)
			select new CheckBox
			{
				Content = (new string[7] { "一", "二", "三", "四", "五", "六", "日" })[i],
				IsChecked = (rule != null && (rule.WeekMask & (1 << i)) != 0)
			}).ToArray();
		until.SelectedDate = rule?.ValidUntil?.ToDateTime(TimeOnly.MinValue);
		windowStart = Ui.Input(item?.Activity.WindowStart?.ToString("HH:mm") ?? rule?.WindowFrom ?? "08:00");
		windowEnd = Ui.Input(item?.Activity.WindowEnd?.ToString("HH:mm") ?? "22:00");
		ReminderSetting reminderSetting = vm.State.Reminders.FirstOrDefault((ReminderSetting r) => r.ActivityId == item?.Activity.Id);
		reminder.IsChecked = reminderSetting?.Enabled ?? false;
		lead = Ui.Input((reminderSetting?.LeadMin ?? 15).ToString());
		lead.Width = Math.Max(64.0, 44.0 * Typography.Scale);
		StackPanel stackPanel3 = new StackPanel();
		AddPair(stackPanel3, "所属行程表", this.table, "重复方式", kind);
		UIElementCollection children = stackPanel3.Children;
		UIElement[] items = weekdays;
		children.Add(Ui.Row(items));
		Ui.Field(stackPanel3, "重复结束日（可留空）", until);
		if (item?.Activity.Kind == "WEEKLY")
		{
			Ui.Field(stackPanel3, "修改影响范围", scope);
		}
		Ui.Field(stackPanel3, "排程约束", constraint);
		AddPair(stackPanel3, "灵活范围开始", windowStart, "灵活范围结束", windowEnd);
		WrapPanel wrapPanel2 = new WrapPanel
		{
			Margin = new Thickness(0.0, 8.0, 0.0, 8.0),
			Tag = "ReminderRow"
		};
		reminder.VerticalAlignment = VerticalAlignment.Center;
		reminder.Margin = new Thickness(0.0, 0.0, 18.0, 0.0);
		wrapPanel2.Children.Add(reminder);
		StackPanel stackPanel4 = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			VerticalAlignment = VerticalAlignment.Center
		};
		TextBlock textBlock = Ui.Text("提前", 12.0);
		textBlock.Margin = new Thickness(0.0, 0.0, 8.0, 0.0);
		textBlock.VerticalAlignment = VerticalAlignment.Center;
		stackPanel4.Children.Add(textBlock);
		lead.Tag = "ReminderLead";
		lead.Width = Math.Max(64.0, 44.0 * Typography.Scale);
		lead.Margin = new Thickness(0.0);
		lead.Padding = new Thickness(6.0, 4.0, 6.0, 4.0);
		lead.MinHeight = 32.0;
		lead.TextAlignment = TextAlignment.Center;
		Typography.Set(lead, 12.0);
		lead.VerticalAlignment = VerticalAlignment.Center;
		stackPanel4.Children.Add(lead);
		TextBlock textBlock2 = Ui.Text("分钟", 12.0);
		textBlock2.Margin = new Thickness(8.0, 0.0, 0.0, 0.0);
		textBlock2.VerticalAlignment = VerticalAlignment.Center;
		stackPanel4.Children.Add(textBlock2);
		wrapPanel2.Children.Add(stackPanel4);
		stackPanel3.Children.Add(wrapPanel2);
		stackPanel2.Children.Add(new Expander
		{
			Header = Ui.Text("更多设置 · 行程表、周期与提醒"),
			Content = stackPanel3,
			IsExpanded = (item?.Activity.Kind == "WEEKLY"),
			Margin = new Thickness(0.0, 14.0, 0.0, 10.0)
		});
		if (item?.Activity.Kind == "WEEKLY")
		{
			scope.SelectionChanged += (object _, SelectionChangedEventArgs _) =>
			{
				UpdateScope();
			};
			UpdateScope();
		}
		vm.Changed += UpdateMetrics;
		UpdateDuration();
		PreviewKeyDown += (object _, KeyEventArgs e) =>
		{
			if (e.Key == Key.Escape)
			{
				editorWindow.Close();
				e.Handled = true;
			}
		};
		Closed += (object? _, EventArgs _) =>
		{
			editorWindow.vm.Changed -= UpdateMetrics;
			editorWindow.closing = true;
			editorWindow.cancel.Cancel();
		};
		void UpdateMetrics()
		{
			editorWindow.lead.Width = Math.Max(64.0, 44.0 * Typography.Scale);
		}
		void UpdateScope()
		{
			bool isEnabled = editorWindow.scope.SelectedIndex == 1;
			foreach (Control item2 in new Control[7] { editorWindow.table, editorWindow.kind, editorWindow.constraint, editorWindow.note, editorWindow.until, editorWindow.windowStart, editorWindow.windowEnd }.Concat(editorWindow.weekdays))
			{
				item2.IsEnabled = isEnabled;
			}
		}
	}

	private void StartChanged()
	{
		if (syncingTimes || !TimeOnly.TryParseExact(start.Text, "HH:mm", out var result))
		{
			return;
		}
		DateTime next = start.DateContext.Date.Add(result.ToTimeSpan());
		syncingTimes = true;
		try
		{
			DateTime? selectedDate = endDate.SelectedDate;
			if (selectedDate.HasValue)
			{
				DateTime valueOrDefault = selectedDate.GetValueOrDefault();
				if (TimeOnly.TryParseExact(end.Text, "HH:mm", out var result2) && valueOrDefault.Date.Add(result2.ToTimeSpan()) > previousStart)
				{
					(DateTime, DateTime) tuple = DateTimeSelection.MoveStart(previousStart, valueOrDefault.Date.Add(result2.ToTimeSpan()), next);
					endDate.SelectedDate = tuple.Item2.Date;
					end.DateContext = tuple.Item2.Date;
					end.Text = tuple.Item2.ToString("HH:mm");
				}
			}
			date.SelectedDate = next.Date;
			previousStart = next;
		}
		finally
		{
			syncingTimes = false;
		}
		UpdateDuration();
	}

	private void EndChanged()
	{
		if (syncingTimes)
		{
			return;
		}
		if (TimeOnly.TryParseExact(end.Text, "HH:mm", out var _) && endDate.SelectedDate != end.DateContext.Date)
		{
			syncingTimes = true;
			try
			{
				endDate.SelectedDate = end.DateContext.Date;
			}
			finally
			{
				syncingTimes = false;
			}
		}
		UpdateDuration();
	}

	private void SetEnd(DateTime value)
	{
		syncingTimes = true;
		try
		{
			endDate.SelectedDate = value.Date;
			end.DateContext = value.Date;
			end.Text = value.ToString("HH:mm");
		}
		finally
		{
			syncingTimes = false;
		}
		UpdateDuration();
	}

	private void UpdateDuration()
	{
		DateTime? selectedDate = date.SelectedDate;
		if (selectedDate.HasValue)
		{
			DateTime valueOrDefault = selectedDate.GetValueOrDefault();
			selectedDate = endDate.SelectedDate;
			if (selectedDate.HasValue)
			{
				DateTime valueOrDefault2 = selectedDate.GetValueOrDefault();
				if (TimeOnly.TryParseExact(start.Text, "HH:mm", out var result) && TimeOnly.TryParseExact(end.Text, "HH:mm", out var result2))
				{
					int num = (int)(valueOrDefault2.Date.Add(result2.ToTimeSpan()) - valueOrDefault.Date.Add(result.ToTimeSpan())).TotalMinutes;
					overnight.IsChecked = valueOrDefault2.Date > valueOrDefault.Date;
					durationLabel.Text = ((num > 0) ? ($"耗时 {num} 分钟" + ((valueOrDefault2.Date > valueOrDefault.Date) ? $" · 跨 {(valueOrDefault2.Date - valueOrDefault.Date).Days} 天" : "")) : "结束日期时间应晚于开始");
					return;
				}
			}
		}
		durationLabel.Text = "请填写日期和HH:mm格式时间";
	}

	private static void AddPair(Panel parent, string left, Control first, string right, Control second)
	{
		Grid row = new Grid
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 4.0),
			HorizontalAlignment = HorizontalAlignment.Stretch
		};
		row.ColumnDefinitions.Add(new ColumnDefinition());
		row.ColumnDefinitions.Add(new ColumnDefinition());
		row.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		row.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		double minWidth = (second.MinWidth = 0.0);
		first.MinWidth = minWidth;
		StackPanel a = new StackPanel();
		StackPanel b = new StackPanel();
		Ui.Field(a, left, first);
		Ui.Field(b, right, second);
		row.Children.Add(a);
		row.Children.Add(b);
		parent.Children.Add(row);
		bool? narrow = null;
		row.SizeChanged += (object _, SizeChangedEventArgs _) =>
		{
			Reflow();
		};
		row.Loaded += (object _, RoutedEventArgs _) =>
		{
			Reflow();
		};
		void Reflow()
		{
			bool flag = row.ActualWidth < 540.0 * Typography.Scale;
			if (narrow != flag)
			{
				narrow = flag;
				Grid.SetColumn(b, (!flag) ? 1 : 0);
				Grid.SetRow(b, flag ? 1 : 0);
				Grid.SetColumnSpan(a, (!flag) ? 1 : 2);
				Grid.SetColumnSpan(b, (!flag) ? 1 : 2);
				a.Margin = (flag ? new Thickness(0.0) : new Thickness(0.0, 0.0, 8.0, 0.0));
				b.Margin = (flag ? new Thickness(0.0) : new Thickness(8.0, 0.0, 0.0, 0.0));
			}
		}
	}

	private async Task SaveAsync()
	{
		if (vm.State.Revision != openedRevision)
		{
			throw new InvalidOperationException("相关行程已发生变化，请关闭后重新打开编辑。 ");
		}
		DateOnly day = DateOnly.FromDateTime(date.SelectedDate ?? throw new ArgumentException("请选择日期。"));
		if (!TimeOnly.TryParseExact(start.Text, "HH:mm", out var result) || !TimeOnly.TryParseExact(end.Text, "HH:mm", out var result2))
		{
			throw new ArgumentException("时间请使用HH:mm格式，例如09:30。");
		}
		DateTime begin = day.ToDateTime(result);
		DateTime finish = (endDate.SelectedDate ?? throw new ArgumentException("请选择结束日期。")).Date.Add(result2.ToTimeSpan());
		if (finish <= begin)
		{
			throw new ArgumentException("结束日期时间应晚于开始；跨日请设置正确的结束日期。");
		}
		Activity a = (((object)occurrence == null) ? new Activity() : JsonKit.Clone(occurrence.Activity));
		a.Title = title.Text.Trim();
		a.TableId = (string)((ComboBoxItem)table.SelectedItem).Tag;
		a.StartAt = begin;
		a.EndAt = finish;
		a.DurationMin = (int)(finish - begin).TotalMinutes;
		a.Note = note.Text;
		a.Kind = ((kind.SelectedIndex == 1) ? "WEEKLY" : "ONCE");
		a.ConstraintType = ((constraint.SelectedIndex == 1) ? "FLEXIBLE" : "FIXED");
		EditWindows.Apply(a, day, windowStart.Text, windowEnd.Text, occurrence?.Activity.Kind == "WEEKLY" && scope.SelectedIndex == 0);
		Place place = null;
		if (!string.IsNullOrWhiteSpace(this.place.Text))
		{
			place = vm.State.Places.FirstOrDefault((Place p) => p.Id == occurrence?.PlaceId && p.Name == this.place.Text.Trim());
			if (place == null)
			{
				place = new Place
				{
					Name = this.place.Text.Trim(),
					InputName = this.place.Text.Trim()
				};
			}
			a.PlaceId = place.Id;
		}
		else
		{
			a.PlaceId = null;
		}
		RecurrenceRule rule = null;
		if (a.Kind == "WEEKLY")
		{
			RecurrenceRule recurrenceRule = new RecurrenceRule
			{
				ActivityId = a.Id,
				ValidFrom = day
			};
			DateTime? selectedDate = until.SelectedDate;
			DateOnly? validUntil;
			if (selectedDate.HasValue)
			{
				DateTime valueOrDefault = selectedDate.GetValueOrDefault();
				validUntil = DateOnly.FromDateTime(valueOrDefault);
			}
			else
			{
				validUntil = null;
			}
			recurrenceRule.ValidUntil = validUntil;
			recurrenceRule.StartTime = start.Text;
			recurrenceRule.WeekMask = weekdays.Select((CheckBox w, int i) => (w.IsChecked == true) ? (1 << i) : 0).Sum();
			recurrenceRule.WindowFrom = ((a.ConstraintType == "FLEXIBLE") ? windowStart.Text : null);
			recurrenceRule.WindowSpanMin = ((a.ConstraintType == "FLEXIBLE") ? new int?((int)(a.WindowEnd - a.WindowStart).Value.TotalMinutes) : ((int?)null));
			rule = recurrenceRule;
		}
		if (!int.TryParse(lead.Text, out var result3) || result3 < 0 || result3 > 10080)
		{
			throw new ArgumentException("提醒提前分钟应为0至10080。");
		}
		if (occurrence?.Activity.Kind == "WEEKLY" && scope.SelectedIndex == 0 && a.Kind != "WEEKLY")
		{
			throw new ArgumentException("单次修改保留原周期，请通过日期和时间调整本次安排。");
		}
		EditCommand command = new EditCommand
		{
			Activity = a,
			Rule = ((occurrence?.Activity.Kind == "WEEKLY" && scope.SelectedIndex == 0) ? null : rule),
			Place = place,
			OriginDate = occurrence?.OriginDate,
			Scope = ((scope.SelectedIndex == 1) ? "FUTURE" : "ONCE"),
			SplitDate = (occurrence?.OriginDate).GetValueOrDefault(day),
			ReminderEnabled = (reminder.IsChecked == true),
			ReminderLeadMin = result3
		};
		PlannerState plannerState = ChangeService.Apply(vm.State, new _003C_003Ez__ReadOnlySingleElementList<EditCommand>(command), vm.State.Revision);
		List<Conflict> list = EditConflictGuard.ForEdit(vm.State, plannerState, day);
		if (list.Count > 0)
		{
			throw new ArgumentException("所选时间或未来周期与已有行程冲突，请调整时间。\n" + string.Join("\n", from p in list.Take(4)
				select p.Message));
		}
		List<TrafficSnapshot> snapshots = new List<TrafficSnapshot>();
		if (!AdjacentTraffic.SameTimingAndPlace(occurrence, begin, finish, place, vm.State))
		{
			List<Occurrence> source = CalendarEngine.ExpandForDisplay(plannerState, begin.Date, finish.Date.AddDays(1.0));
			Occurrence target = source.FirstOrDefault((Occurrence o) => o.Start == begin && o.End == finish && o.Activity.Id == a.Id) ?? source.FirstOrDefault((Occurrence o) => o.Start == begin && o.End == finish && o.Title == a.Title && o.Activity.TableId == a.TableId);
			if ((object)target != null && plannerState.Tables.Any((ScheduleTable t) => t.Id == target.Activity.TableId && t.Active(day)))
			{
				(PlannerState, List<TrafficSnapshot>) tuple = await CheckAdjacentAsync(plannerState, target, command);
				plannerState = tuple.Item1;
				snapshots = tuple.Item2;
				list = EditConflictGuard.ForEdit(vm.State, plannerState, day);
				if (list.Count > 0)
				{
					throw new ArgumentException("地点或时间调整后与已有安排冲突，请返回修改。");
				}
			}
		}
		cancel.Token.ThrowIfCancellationRequested();
		if (vm.State.Revision != openedRevision)
		{
			throw new InvalidOperationException("行程已有变化，请重新打开编辑。");
		}
		if (command.Scope == "FUTURE")
		{
			List<ActivityException> list2 = vm.State.Exceptions.Where((ActivityException e) =>
			{
				if (e.ActivityId == a.Id)
				{
					DateOnly originDate = e.OriginDate;
					DateOnly? splitDate = command.SplitDate;
					return originDate >= splitDate;
				}
				return false;
			}).ToList();
			string value = string.Join("\n", list2.Take(15).Select((ActivityException e) =>
			{
				string text = $"{e.OriginDate:yyyy-MM-dd} · ";
				if ((rule.WeekMask & (1 << (int)(e.OriginDate.DayOfWeek + 6) % 7)) == 0)
				{
					goto IL_0096;
				}
				if (rule.ValidUntil.HasValue)
				{
					DateOnly originDate = e.OriginDate;
					DateOnly? validUntil2 = rule.ValidUntil;
					if (!(originDate <= validUntil2))
					{
						goto IL_0096;
					}
				}
				string text2 = "迁移并保留";
				goto IL_00bb;
				IL_00bb:
				return text + text2;
				IL_0096:
				text2 = (("OVERRIDE" == e.Kind) ? "转为单次活动" : "取消日期不再产生实例");
				goto IL_00bb;
			}));
			if (!Ui.Confirm($"从 {command.SplitDate:yyyy-MM-dd} 起调整周期。\n此前记录不变；未来例外 {list2.Count} 项：\n{value}\n" + ((list2.Count > 15) ? "其余同类例外按相同规则处理。\n" : "") + "确认保存？"))
			{
				return;
			}
		}
		cancel.Token.ThrowIfCancellationRequested();
		if (vm.State.Revision != openedRevision)
		{
			throw new InvalidOperationException("行程已有变化，请重新打开编辑。");
		}
		new TrafficSnapshotStore(vm.DataDirectory).Save(snapshots);
		vm.Persist(plannerState);
		SavedDate = day;
		Close();
	}
}
