using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Planner.Core;
using Planner.Data;

namespace Planner.Desktop;

public sealed class AgentReviewWindow : Window
{
	private sealed class Card
	{
		public required TripRequest Original;

		public required TextBox Title;

		public required DatePicker Day;

		public required TimeEntry Begin;

		public required TimeEntry End;

		public required TextBox Duration;

		public required TextBox Deadline;

		public required TextBox Query;

		public required PlaceSearchBox Search;

		public required ComboBox Mode;

		public required TextBox Note;

		public required TextBlock Status;

		public required DatePicker EndDay;

		public required CheckBox Reminder;

		public required TextBox Lead;

		public Place? Chosen;

		public bool AutoStart;

		public int DeadlineDayOffset;

		public Border? Traffic;

		public Border? OutgoingTraffic;
	}

	private readonly Button calculate = Ui.Button("计算时间与路程", () =>
	{
	});

	private readonly DispatcherTimer previewTimer = new DispatcherTimer
	{
		Interval = TimeSpan.FromMilliseconds(350L)
	};

	private CancellationTokenSource? previewCancel;

	private readonly Dictionary<string, TravelEstimate> routeCache = new Dictionary<string, TravelEstimate>();

	private long draftVersion;

	private bool applyingPreview;

	private bool saving;

	private bool timerWired;

	private readonly IReadOnlyDictionary<string, Place> confirmedPlaces;

	private readonly List<Card> cards = new List<Card>();

	private readonly PlannerViewModel vm;

	private readonly RepositoryMap map;

	private readonly ComboBox table;

	private readonly TextBlock status = Ui.Text("", 12.0);

	private readonly CancellationTokenSource cancel = new CancellationTokenSource();

	private readonly StackPanel panel = new StackPanel();

	private readonly TextBox newName = Ui.Input();

	private readonly TableColorPicker newColor = new TableColorPicker();

	private readonly string defaultTableName;

	private bool closing;

	private bool committed;

	public Func<bool>? CanApply { get; set; }

	public event Action<PlannerState, PlannerState>? Applying;

	public event Action? Applied;

	private void WireScheduling(Card c, Func<bool> syncing)
	{
		c.Begin.Changed += () =>
		{
			if (!applyingPreview && !syncing())
			{
				c.AutoStart = string.IsNullOrWhiteSpace(c.Begin.Text);
				DraftChanged();
			}
		};
		c.End.Changed += () =>
		{
			if (!applyingPreview && !syncing())
			{
				if (!string.IsNullOrWhiteSpace(c.Begin.Text))
				{
					c.AutoStart = false;
				}
				else
				{
					applyingPreview = true;
					try
					{
						c.Deadline.Text = c.End.Text;
					}
					finally
					{
						applyingPreview = false;
					}
				}
				DraftChanged(c);
			}
		};
		c.Duration.TextChanged += (object _, TextChangedEventArgs _) =>
		{
			Changed();
		};
		c.Day.SelectedDateChanged += (object? _, SelectionChangedEventArgs _) =>
		{
			Changed();
		};
		c.EndDay.SelectedDateChanged += (object? _, SelectionChangedEventArgs _) =>
		{
			if (!applyingPreview && !syncing() && !string.IsNullOrWhiteSpace(c.Begin.Text))
			{
				c.AutoStart = false;
			}
			Changed();
		};
		c.Mode.SelectionChanged += (object _, SelectionChangedEventArgs _) =>
		{
			Changed();
		};
		c.Search.Changed += Changed;
		c.Title.TextChanged += (object _, TextChangedEventArgs _) =>
		{
			Changed();
		};
		c.Note.TextChanged += (object _, TextChangedEventArgs _) =>
		{
			Changed();
		};
		c.Deadline.TextChanged += (object _, TextChangedEventArgs _) =>
		{
			Changed();
		};
		c.Lead.TextChanged += (object _, TextChangedEventArgs _) =>
		{
			Changed();
		};
		c.Reminder.Checked += (object _, RoutedEventArgs _) =>
		{
			Changed();
		};
		c.Reminder.Unchecked += (object _, RoutedEventArgs _) =>
		{
			Changed();
		};
		if (timerWired)
		{
			return;
		}
		timerWired = true;
		previewTimer.Tick += async (object? _, EventArgs _) =>
		{
			previewTimer.Stop();
			try
			{
				await CalculateAsync(chooseRoute: false);
			}
			catch (OperationCanceledException)
			{
			}
			catch (Exception ex2)
			{
				if (!closing)
				{
					status.Text = ex2.Message;
				}
			}
		};
		void Changed()
		{
			if (!syncing())
			{
				DraftChanged();
			}
		}
	}

	private void DraftChanged(Card? editingEnd = null)
	{
		if (applyingPreview || closing)
		{
			return;
		}
		draftVersion++;
		previewCancel?.Cancel();
		previewTimer.Stop();
		applyingPreview = true;
		try
		{
			foreach (Card card in cards)
			{
				if (card.Traffic != null)
				{
					card.Traffic.Visibility = Visibility.Collapsed;
				}
				if (card.OutgoingTraffic != null)
				{
					card.OutgoingTraffic.Visibility = Visibility.Collapsed;
				}
				if (card.AutoStart)
				{
					card.Begin.Text = "";
					if (card != editingEnd)
					{
						card.End.Text = "";
					}
				}
			}
		}
		finally
		{
			applyingPreview = false;
		}
		if (IsLoaded && !saving)
		{
			status.Text = "安排有变动，正在重新核对时间与路程…";
			previewTimer.Start();
		}
	}

	private List<PreparedTrip> ReadDraft()
	{
		List<PreparedTrip> list = new List<PreparedTrip>();
		foreach (Card card in cards)
		{
			TripRequest tripRequest = JsonKit.Clone(card.Original);
			tripRequest.Title = card.Title.Text.Trim();
			DateTime? selectedDate = card.Day.SelectedDate;
			DateOnly? day;
			if (selectedDate.HasValue)
			{
				DateTime valueOrDefault = selectedDate.GetValueOrDefault();
				day = DateOnly.FromDateTime(valueOrDefault);
			}
			else
			{
				day = null;
			}
			tripRequest.Day = day;
			tripRequest.Start = (card.AutoStart ? ((int?)null) : Minute(card.Begin.Text));
			tripRequest.End = (card.AutoStart ? ((int?)null) : Minute(card.End.Text));
			int? num = Minute(card.Deadline.Text);
			int? latestEnd;
			if (num.HasValue)
			{
				int valueOrDefault2 = num.GetValueOrDefault();
				latestEnd = valueOrDefault2 + card.DeadlineDayOffset * 1440;
			}
			else
			{
				latestEnd = null;
			}
			tripRequest.LatestEnd = latestEnd;
			tripRequest.Location = card.Query.Text.Trim();
			tripRequest.Notes = card.Note.Text.Trim();
			tripRequest.Duration = (int.TryParse(card.Duration.Text, out var result) ? new int?(result) : ((int?)null));
			tripRequest.ModeIn = ((card.Mode.SelectedIndex > 0) ? card.Mode.Text : null);
			tripRequest.EndDay = null;
			num = tripRequest.Start;
			if (num.HasValue)
			{
				int valueOrDefault3 = num.GetValueOrDefault();
				num = tripRequest.End;
				if (num.HasValue)
				{
					int valueOrDefault4 = num.GetValueOrDefault();
					DateOnly? day2 = tripRequest.Day;
					if (day2.HasValue)
					{
						DateOnly valueOrDefault5 = day2.GetValueOrDefault();
						selectedDate = card.EndDay.SelectedDate;
						if (selectedDate.HasValue)
						{
							DateTime valueOrDefault6 = selectedDate.GetValueOrDefault();
							tripRequest.EndDay = DateOnly.FromDateTime(valueOrDefault6);
							int num2 = (tripRequest.EndDay.Value.DayNumber - valueOrDefault5.DayNumber) * 1440 + valueOrDefault4 - valueOrDefault3;
							if (num2 < ((!tripRequest.IsReturn) ? 1 : 0))
							{
								throw new ArgumentException("结束日期时间必须晚于开始。");
							}
							tripRequest.Duration = num2;
						}
					}
				}
			}
			tripRequest.ReminderEnabled = card.Reminder.IsChecked == true;
			if (!int.TryParse(card.Lead.Text, out var result2))
			{
				throw new ArgumentException("提醒提前分钟应为0—10080。");
			}
			tripRequest.ReminderLeadMin = result2;
			RepositoryPlanner.Validate(tripRequest);
			Place place = card.Chosen ?? throw new ArgumentException("请为“" + tripRequest.Title + "”搜索并选择准确地点。");
			tripRequest.Location = place.Name;
			list.Add(new PreparedTrip(tripRequest, place));
		}
		return list;
	}

	private async Task<RepositoryPlan?> CalculateAsync(bool chooseRoute)
	{
		previewTimer.Stop();
		previewCancel?.Cancel();
		previewCancel?.Dispose();
		previewCancel = CancellationTokenSource.CreateLinkedTokenSource(cancel.Token);
		CancellationToken token = previewCancel.Token;
		long own = draftVersion;
		List<PreparedTrip> prepared = ReadDraft();
		status.Text = "正在计算时间与路程…";
		RepositoryPlan repositoryPlan = await RepositoryPlanner.PlanAsync(vm.State.Copy(), prepared, async (Place from, Place to, string? mode, DateTime departure, CancellationToken ct) =>
		{
			if (routeCache.TryGetValue(CacheKey(mode), out TravelEstimate value2))
			{
				return value2;
			}
			if (string.IsNullOrWhiteSpace(mode) && !chooseRoute)
			{
				throw new ArgumentException("请为后续活动选择交通方式，或点击“计算时间与路程”比较方案。");
			}
			TravelEstimate travelEstimate = await RepositoryDialogs.Travel(this, map, from, to, mode, departure, ct);
			ct.ThrowIfCancellationRequested();
			string text2 = mode ?? travelEstimate.Source.Split(" · ")[0];
			routeCache[CacheKey(text2)] = travelEstimate;
			routeCache[CacheKey(mode)] = travelEstimate;
			if (string.IsNullOrWhiteSpace(mode))
			{
				int num2 = prepared.FindIndex((PreparedTrip p) => p.Place.Id == to.Id);
				if (num2 >= 0)
				{
					prepared[num2].Request.ModeIn = text2;
					applyingPreview = true;
					try
					{
						cards[num2].Mode.SelectedItem = text2;
					}
					finally
					{
						applyingPreview = false;
					}
				}
			}
			return travelEstimate;
			string CacheKey(string? chosen)
			{
				return $"{from.Id}/{to.Id}/{chosen}/{departure:O}";
			}
		}, token);
		token.ThrowIfCancellationRequested();
		if (closing || own != draftVersion)
		{
			return null;
		}
		applyingPreview = true;
		try
		{
			for (int num = 0; num < cards.Count; num++)
			{
				Card card = cards[num];
				PlannedTrip row = repositoryPlan.Rows[num];
				DateTime dateTime = row.Trip.Request.Day.Value.ToDateTime(TimeOnly.MinValue).AddMinutes(row.Start);
				DateTime value = dateTime.AddMinutes(row.Trip.Request.Duration.Value);
				card.Begin.DateContext = dateTime.Date;
				card.Day.SelectedDate = dateTime.Date;
				card.Begin.Text = dateTime.ToString("HH:mm");
				card.End.DateContext = value.Date;
				card.EndDay.SelectedDate = value.Date;
				card.End.Text = value.ToString("HH:mm");
				if (row.Before.Minutes > 0)
				{
					DateTime? travelDepartAt = row.TravelDepartAt;
					if (travelDepartAt.HasValue)
					{
						DateTime valueOrDefault = travelDepartAt.GetValueOrDefault();
						StackPanel stackPanel = new StackPanel();
						stackPanel.Children.Add(Ui.Text("交通｜" + (prepared.Select((PreparedTrip x) => x.Place).Concat(vm.State.Places).FirstOrDefault((Place p) => p.Id == row.PreviousPlaceId)?.Name ?? row.PreviousTitle) + " → " + card.Chosen?.Name, 15.0, bold: true));
						stackPanel.Children.Add(Ui.Text($"{valueOrDefault:MM-dd HH:mm}—{valueOrDefault.AddMinutes(row.Before.Minutes):MM-dd HH:mm} · {row.Before.Minutes}分钟", 13.0, bold: true));
						stackPanel.Children.Add(Ui.Text(row.Before.Source, 12.0));
						card.Traffic.Child = stackPanel;
						card.Traffic.Visibility = Visibility.Visible;
						goto IL_048b;
					}
				}
				card.Traffic.Visibility = Visibility.Collapsed;
				goto IL_048b;
				IL_048b:
				card.OutgoingTraffic.Visibility = Visibility.Collapsed;
				if (row.After.Minutes > 0 && row.NextActivityId != null && !repositoryPlan.Rows.Any((PlannedTrip r) => r.ActivityId == row.NextActivityId))
				{
					string text = prepared.Select((PreparedTrip x) => x.Place).Concat(vm.State.Places).FirstOrDefault((Place p) => p.Id == row.NextPlaceId)?.Name ?? row.NextTitle ?? "后续活动";
					StackPanel stackPanel2 = new StackPanel();
					stackPanel2.Children.Add(Ui.Text("交通｜" + card.Chosen?.Name + " → " + text, 15.0, bold: true));
					stackPanel2.Children.Add(Ui.Text($"{value:MM-dd HH:mm}—{value.AddMinutes(row.After.Minutes):MM-dd HH:mm} · {row.After.Minutes}分钟", 13.0, bold: true));
					stackPanel2.Children.Add(Ui.Text(row.After.Source, 12.0));
					card.OutgoingTraffic.Child = stackPanel2;
					card.OutgoingTraffic.Visibility = Visibility.Visible;
				}
			}
		}
		finally
		{
			applyingPreview = false;
		}
		status.Text = "已填入实际时间；交通将作为独立卡片保存。请核对后生成。";
		return repositoryPlan;
	}

	public AgentReviewWindow(PlannerViewModel model, IReadOnlyList<TripRequest> requests, bool preferNewTable = false, RepositoryMap? mapProvider = null, IReadOnlyDictionary<string, Place>? confirmedPlaces = null, IReadOnlyList<AgentRoute>? knownRoutes = null)
	{
		this.confirmedPlaces = ((confirmedPlaces == null) ? new Dictionary<string, Place>() : confirmedPlaces.ToDictionary((KeyValuePair<string, Place> p) => p.Key, (KeyValuePair<string, Place> p) => JsonKit.Clone(p.Value)));
		foreach (AgentRoute item in knownRoutes ?? Array.Empty<AgentRoute>())
		{
			if ((Clock.Now - item.QueryAt).TotalMinutes < 10.0 && this.confirmedPlaces.ContainsKey(item.FromRef) && this.confirmedPlaces.ContainsKey(item.ToRef))
			{
				string value = ((item.Mode == "打车") ? "驾车" : item.Mode);
				routeCache[$"{item.FromRef}/{item.ToRef}/{value}/{item.DepartAt:O}"] = new TravelEstimate(item.Minutes, item.Source)
				{
					Option = ((item.Option == null) ? null : JsonKit.Clone(item.Option))
				};
			}
		}
		vm = model;
		map = mapProvider ?? new RepositoryMap(new SettingsStore(vm.DataDirectory).Load());
		Title = "完善行程";
		Width = 960.0;
		Height = 790.0;
		MinWidth = 760.0;
		MinHeight = 600.0;
		WindowStartupLocation = WindowStartupLocation.CenterOwner;
		Background = Ui.Brush("#FBF7F0");
		FontFamily = new FontFamily("Microsoft YaHei UI");
		DockPanel dockPanel = (DockPanel)(Content = new DockPanel
		{
			Margin = new Thickness(24.0)
		});
		StackPanel heading = new StackPanel();
		heading.Children.Add(Ui.Text("完善行程", 22.0, bold: true));
		heading.Children.Add(Ui.Text("核对活动，选择准确地点，补充时间后计算路线。开始留空表示由系统安排。", 12.0));
		table = new ComboBox();
		foreach (ScheduleTable table in vm.State.Tables)
		{
			this.table.Items.Add(new ComboBoxItem
			{
				Content = table.Name,
				Tag = table.Id
			});
		}
		this.table.Items.Add(new ComboBoxItem
		{
			Content = "＋ 新建行程表",
			Tag = "NEW"
		});
		this.table.SelectedIndex = (preferNewTable ? (this.table.Items.Count - 1) : Math.Max(0, vm.State.Tables.FindIndex((ScheduleTable t) => t.Id == vm.SelectedTableId)));
		Ui.Field(heading, "保存到行程表", this.table);
		defaultTableName = (requests.FirstOrDefault()?.Day ?? Clock.Today).ToString("MM-dd") + " 行程计划";
		newName.Text = defaultTableName;
		newName.ToolTip = "可留空，保存时自动命名为“" + defaultTableName + "”";
		newName.Tag = "NewTableName";
		newColor.Width = 340.0;
		StackPanel newFields = new StackPanel();
		Ui.Field(newFields, "新行程表名称（可选）", newName);
		newFields.Children.Add(Ui.Text("行程表主色", 12.0, bold: true));
		newFields.Children.Add(newColor);
		heading.Children.Add(newFields);
		this.table.SelectionChanged += (object _, SelectionChangedEventArgs _) =>
		{
			TargetChanged();
		};
		TargetChanged();
		DockPanel dockPanel3 = new DockPanel();
		DockPanel.SetDock(dockPanel3, Dock.Bottom);
		dockPanel.Children.Add(dockPanel3);
		StackPanel stackPanel = Ui.Row(Ui.Button("取消", Close));
		DockPanel.SetDock(stackPanel, Dock.Right);
		dockPanel3.Children.Add(stackPanel);
		dockPanel3.Children.Add(status);
		Button generate = Ui.Button("生成行程 →", () =>
		{
		}, primary: true);
		stackPanel.Children.Add(calculate);
		stackPanel.Children.Add(generate);
		calculate.Click += async (object _, RoutedEventArgs _) =>
		{
			calculate.IsEnabled = false;
			try
			{
				await CalculateAsync(chooseRoute: true);
			}
			catch (OperationCanceledException)
			{
			}
			catch (Exception ex2)
			{
				if (!closing)
				{
					status.Text = ex2.Message;
				}
			}
			finally
			{
				if (!closing)
				{
					calculate.IsEnabled = true;
				}
			}
		};
		generate.Click += async (object _, RoutedEventArgs _) =>
		{
			if (!committed)
			{
				if (Ui.ValidateRequired(this))
				{
					generate.IsEnabled = false;
					panel.IsEnabled = false;
					heading.IsEnabled = false;
					try
					{
						saving = true;
						await Generate();
						return;
					}
					catch (OperationCanceledException)
					{
						if (!closing)
						{
							status.Text = "已取消，可调整后重试。";
						}
						return;
					}
					catch (Exception ex2)
					{
						if (!closing)
						{
							status.Text = ex2.Message;
						}
						return;
					}
					finally
					{
						saving = false;
						if (!closing && !committed)
						{
							generate.IsEnabled = true;
							panel.IsEnabled = true;
							heading.IsEnabled = true;
						}
					}
				}
				status.Text = "请填写标记的必填项。";
			}
		};
		StackPanel content = new StackPanel
		{
			Children = 
			{
				(UIElement)heading,
				(UIElement)panel
			}
		};
		dockPanel.Children.Add(new ScrollViewer
		{
			Content = content,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto
		});
		for (int num = 0; num < requests.Count; num++)
		{
			AddCard(requests[num], num);
		}
		Closed += (object? _, EventArgs _) =>
		{
			closing = true;
			cancel.Cancel();
			previewCancel?.Cancel();
			previewTimer.Stop();
		};
		Loaded += async (object _, RoutedEventArgs _) =>
		{
			generate.IsEnabled = false;
			try
			{
				await ResolvePlacesAsync();
				await CalculateAsync(chooseRoute: false);
			}
			catch (OperationCanceledException)
			{
			}
			catch (Exception ex2)
			{
				if (!closing)
				{
					status.Text = ex2.Message;
				}
			}
			finally
			{
				if (!closing && !committed)
				{
					generate.IsEnabled = true;
				}
			}
		};
		void TargetChanged()
		{
			newFields.Visibility = ((!((string)((ComboBoxItem)this.table.SelectedItem)?.Tag == "NEW")) ? Visibility.Collapsed : Visibility.Visible);
		}
	}

	private void AddCard(TripRequest request, int index)
	{
		StackPanel stackPanel = new StackPanel();
		stackPanel.Children.Add(Ui.Text($"{index + 1:00} · {request.Title ?? "待补充活动"}", 16.0, bold: true));
		DateTime dateContext = (request.Day ?? Clock.Today).ToDateTime(TimeOnly.MinValue);
		DateTime dateTime = dateContext.AddMinutes(request.Start.GetValueOrDefault() + request.Duration.GetValueOrDefault());
		DateTime dateTime2 = request.EndDay?.ToDateTime(TimeOnly.MinValue) ?? dateTime.Date;
		Card card = new Card
		{
			Original = JsonKit.Clone(request),
			AutoStart = !request.Start.HasValue,
			Title = Ui.Input(request.Title ?? ""),
			Day = new StyledDateEntry
			{
				SelectedDate = request.Day?.ToDateTime(TimeOnly.MinValue)
			},
			EndDay = new StyledDateEntry
			{
				SelectedDate = dateTime2
			},
			Begin = new TimeEntry(ClockText(request.Start))
			{
				DateContext = dateContext
			},
			End = new TimeEntry(ClockText(request.End ?? (request.Start + request.Duration)))
			{
				DateContext = dateTime2
			},
			Duration = Ui.Input(request.Duration?.ToString() ?? ""),
			Deadline = Ui.Input(ClockText(request.LatestEnd ?? ((!request.Start.HasValue) ? request.End : ((int?)null)))),
			DeadlineDayOffset = (request.LatestEnd ?? ((!request.Start.HasValue) ? request.End : ((int?)null)).GetValueOrDefault()) / 1440,
			Query = Ui.Input(request.Location ?? ""),
			Search = new PlaceSearchBox(map.SearchAsync),
			Mode = Ui.Choices(new _003C_003Ez__ReadOnlyArray<string>(new string[5] { "选择交通时再决定", "公交", "步行", "骑行", "驾车" }), Math.Max(0, Array.IndexOf(new string[5] { "选择交通时再决定", "公交", "步行", "骑行", "驾车" }, request.ModeIn))),
			Note = Ui.Input(request.Notes ?? ""),
			Status = Ui.Text("请搜索并选择地点", 11.0),
			Reminder = new CheckBox
			{
				Content = "启用出发提醒",
				IsChecked = request.ReminderEnabled
			},
			Lead = Ui.Input(request.ReminderLeadMin.ToString())
		};
		Pair(stackPanel, "活动名称", card.Title, "开始日期", card.Day);
		Pair(stackPanel, "开始（可留空）", card.Begin, "结束（可留空）", card.End);
		Pair(stackPanel, "结束日期", card.EndDay, "耗时（分钟）", card.Duration);
		Ui.Field(stackPanel, (card.DeadlineDayOffset > 0) ? ("最晚结束/到达（截止" + dateContext.AddDays(card.DeadlineDayOffset).ToString("yyyy/MM/dd") + "，可留空）") : "最晚结束/到达（可留空）", card.Deadline);
		bool syncing = false;
		card.Duration.TextChanged += (object _, TextChangedEventArgs _) =>
		{
			FinishByDuration();
		};
		card.Begin.Changed += BeginChanged;
		card.Begin.DateTimeChanged += (DateTime _) =>
		{
			BeginChanged();
		};
		card.End.Changed += EndChanged;
		card.End.DateTimeChanged += (DateTime _) =>
		{
			EndChanged();
		};
		card.Day.SelectedDateChanged += (object? _, SelectionChangedEventArgs _) =>
		{
			if (!syncing)
			{
				DateTime? selectedDate = card.Day.SelectedDate;
				if (selectedDate.HasValue)
				{
					DateTime valueOrDefault = selectedDate.GetValueOrDefault();
					card.Begin.DateContext = valueOrDefault.Date;
				}
				FinishByDuration();
			}
		};
		card.EndDay.SelectedDateChanged += (object? _, SelectionChangedEventArgs _) =>
		{
			if (!syncing)
			{
				DateTime? selectedDate = card.EndDay.SelectedDate;
				if (selectedDate.HasValue)
				{
					DateTime valueOrDefault = selectedDate.GetValueOrDefault();
					card.End.DateContext = valueOrDefault.Date;
				}
				EndChanged();
			}
		};
		WrapPanel wrapPanel = new WrapPanel();
		int[] array = new int[4] { 30, 60, 90, 120 };
		for (int num = 0; num < array.Length; num++)
		{
			int duration = array[num];
			wrapPanel.Children.Add(Ui.Button(duration + "分钟", () =>
			{
				card.Duration.Text = duration.ToString();
			}));
		}
		stackPanel.Children.Add(wrapPanel);
		card.Search.QueryInput.Text = card.Query.Text;
		card.Query = card.Search.QueryInput;
		card.Status = card.Search.Feedback;
		stackPanel.Children.Add(card.Search);
		card.Search.Changed += () =>
		{
			card.Chosen = card.Search.SelectedPlace;
		};
		string placeRef = request.PlaceRef;
		if (placeRef != null && confirmedPlaces.TryGetValue(placeRef, out Place value))
		{
			card.Search.SelectConfirmed(value);
		}
		Ui.Field(stackPanel, "前往此处", card.Mode);
		card.Note.AcceptsReturn = true;
		card.Note.TextWrapping = TextWrapping.Wrap;
		card.Note.MinHeight = 70.0;
		Ui.Field(stackPanel, "备注", card.Note);
		stackPanel.Children.Add(card.Reminder);
		Ui.Field(stackPanel, "提前分钟", card.Lead);
		if (index > 0)
		{
			Button button = Ui.Button("比较与上一项之间的交通", () =>
			{
			});
			stackPanel.Children.Add(button);
			button.Click += async (object _, RoutedEventArgs _) =>
			{
				card.Mode.SelectedIndex = 0;
				routeCache.Clear();
				try
				{
					await CalculateAsync(chooseRoute: true);
				}
				catch (OperationCanceledException)
				{
				}
				catch (Exception ex2)
				{
					if (!closing)
					{
						status.Text = ex2.Message;
					}
				}
			};
		}
		cards.Add(card);
		card.Traffic = Ui.Card(new StackPanel(), "#F0E8DC");
		card.Traffic.Tag = "DraftTraffic";
		card.Traffic.Visibility = Visibility.Collapsed;
		panel.Children.Add(card.Traffic);
		panel.Children.Add(Ui.Card(stackPanel));
		card.OutgoingTraffic = Ui.Card(new StackPanel(), "#F0E8DC");
		card.OutgoingTraffic.Tag = "DraftOutgoingTraffic";
		card.OutgoingTraffic.Visibility = Visibility.Collapsed;
		panel.Children.Add(card.OutgoingTraffic);
		WireScheduling(card, () => syncing);
		void BeginChanged()
		{
			if (!syncing)
			{
				try
				{
					syncing = true;
					card.Day.SelectedDate = card.Begin.DateContext.Date;
				}
				finally
				{
					syncing = false;
				}
				FinishByDuration();
			}
		}
		void EndChanged()
		{
			if (syncing)
			{
				return;
			}
			try
			{
				syncing = true;
				card.EndDay.SelectedDate = card.End.DateContext.Date;
				int? num2 = Minute(card.Begin.Text);
				if (num2.HasValue)
				{
					int valueOrDefault = num2.GetValueOrDefault();
					num2 = Minute(card.End.Text);
					if (num2.HasValue)
					{
						int valueOrDefault2 = num2.GetValueOrDefault();
						DateTime? selectedDate = card.Day.SelectedDate;
						if (selectedDate.HasValue)
						{
							DateTime valueOrDefault3 = selectedDate.GetValueOrDefault();
							int num3 = (int)(card.End.DateContext.Date - valueOrDefault3.Date).TotalMinutes + valueOrDefault2 - valueOrDefault;
							if (num3 >= 0)
							{
								card.Duration.Text = num3.ToString();
							}
						}
					}
				}
			}
			catch (ArgumentException)
			{
			}
			finally
			{
				syncing = false;
			}
		}
		void FinishByDuration()
		{
			if (syncing)
			{
				return;
			}
			try
			{
				syncing = true;
				if (int.TryParse(card.Duration.Text, out var result) && result >= 0)
				{
					int? num2 = Minute(card.Begin.Text);
					if (num2.HasValue)
					{
						int valueOrDefault = num2.GetValueOrDefault();
						DateTime? selectedDate = card.Day.SelectedDate;
						if (selectedDate.HasValue)
						{
							DateTime dateTime3 = selectedDate.GetValueOrDefault().Date.AddMinutes(valueOrDefault + result);
							card.End.DateContext = dateTime3.Date;
							card.EndDay.SelectedDate = dateTime3.Date;
							card.End.Text = dateTime3.ToString("HH:mm");
						}
					}
				}
			}
			catch (ArgumentException)
			{
			}
			finally
			{
				syncing = false;
			}
		}
	}

	private static string ClockText(int? minute)
	{
		if (minute.HasValue)
		{
			int valueOrDefault = minute.GetValueOrDefault();
			return $"{valueOrDefault / 60 % 24:00}:{valueOrDefault % 60:00}";
		}
		return "";
	}

	private async Task ResolvePlacesAsync()
	{
		foreach (Card card in cards)
		{
			cancel.Token.ThrowIfCancellationRequested();
			if (!string.IsNullOrWhiteSpace(card.Query.Text) && card.Chosen == null)
			{
				await card.Search.SearchAsync();
			}
		}
	}

	private static int? Minute(string text)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return null;
		}
		if (!TimeOnly.TryParseExact(text.Trim(), "HH:mm", out var result))
		{
			throw new ArgumentException("时间请使用HH:mm格式。");
		}
		return result.Hour * 60 + result.Minute;
	}

	private static void Pair(Panel p, string left, Control first, string right, Control second)
	{
		Grid grid = new Grid();
		grid.ColumnDefinitions.Add(new ColumnDefinition());
		grid.ColumnDefinitions.Add(new ColumnDefinition());
		StackPanel element = new StackPanel
		{
			Margin = new Thickness(0.0, 0.0, 10.0, 0.0)
		};
		StackPanel element2 = new StackPanel
		{
			Margin = new Thickness(10.0, 0.0, 0.0, 0.0)
		};
		Ui.Field(element, left, first);
		Ui.Field(element2, right, second);
		Grid.SetColumn(element2, 1);
		grid.Children.Add(element);
		grid.Children.Add(element2);
		p.Children.Add(grid);
	}

	private async Task Generate()
	{
		cancel.Token.ThrowIfCancellationRequested();
		Func<bool>? canApply = CanApply;
		if (canApply != null && !canApply())
		{
			throw new InvalidOperationException("对话分支已变动，请回到对应分支或重新生成草稿。");
		}
		if (!(table.SelectedItem is ComboBoxItem { Tag: var tag }) || !(tag is string selectedTarget))
		{
			throw new ArgumentException("请选择保存到哪一份行程表。");
		}
		if (selectedTarget == "NEW" && newName.Text.Trim().Length > 60)
		{
			throw new ArgumentException("行程表名称不能超过60字，可留空使用默认名称。");
		}
		RepositoryPlan repositoryPlan = (await CalculateAsync(chooseRoute: true)) ?? throw new InvalidOperationException("草稿正在变化，请等待重新计算后再保存。");
		List<PreparedTrip> list = repositoryPlan.Rows.Select((PlannedTrip r) => r.Trip).ToList();
		cancel.Token.ThrowIfCancellationRequested();
		string text = string.Join("\n\n", repositoryPlan.Rows.Select((PlannedTrip r) =>
		{
			DateTime value = r.Trip.Request.Day.Value.ToDateTime(TimeOnly.MinValue).AddMinutes(r.Start);
			return $"{value:yyyy-MM-dd HH:mm}—{value.AddMinutes(r.Trip.Request.Duration.Value):yyyy-MM-dd HH:mm}\n{r.Trip.Request.Title} · {r.Trip.Place.Name}\n{r.Trip.Request.Notes}\n前往：{r.Before.Minutes}分钟（{r.Before.Source}）；后续：{r.After.Minutes}分钟（{r.After.Source}）";
		}));
		long num = draftVersion;
		if (!Ui.ConfirmReview("你的行程\n核对地点、时间和路程后，一次保存全部活动。\n\n" + text))
		{
			status.Text = "已返回修改，原行程未变。";
			return;
		}
		cancel.Token.ThrowIfCancellationRequested();
		if (num != draftVersion)
		{
			throw new InvalidOperationException("草稿已变动，请重新计算并核对。");
		}
		Func<bool>? canApply2 = CanApply;
		if (canApply2 != null && !canApply2())
		{
			throw new InvalidOperationException("对话分支已变动，未保存此次草稿。");
		}
		PlannerState plannerState = vm.State.Copy();
		PlannerState plannerState2 = plannerState.Copy();
		string text2 = selectedTarget;
		if (text2 == "NEW")
		{
			ScheduleTable scheduleTable = new ScheduleTable
			{
				Name = (string.IsNullOrWhiteSpace(newName.Text) ? defaultTableName : newName.Text.Trim()),
				Color = (newColor.SelectedColor ?? TablePalette.Colors[0].Hex)
			};
			plannerState2.Tables.Add(scheduleTable);
			text2 = scheduleTable.Id;
		}
		PlannerState plannerState3 = GeneratedPlanService.Apply(plannerState2, repositoryPlan, text2);
		Applying?.Invoke(plannerState, plannerState3);
		cancel.Token.ThrowIfCancellationRequested();
		vm.Persist(plannerState3);
		committed = true;
		try
		{
			Applied?.Invoke();
		}
		catch (Exception ex)
		{
			vm.Notify("行程已保存，对话记录将在重启后恢复：" + ex.Message);
		}
		vm.FocusDate(list[0].Request.Day.Value);
		Close();
	}
}
