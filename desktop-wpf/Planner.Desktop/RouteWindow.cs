using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Planner.Core;
using Planner.Data;

namespace Planner.Desktop;

public sealed class RouteWindow : Window
{
	private readonly PlannerViewModel vm;

	private readonly DatePicker date;

	private readonly TextBox originText = Ui.Input();

	private readonly TextBox earliest = Ui.Input("08:00");

	private readonly ComboBox originList = new ComboBox
	{
		MinWidth = 290.0
	};

	private readonly ComboBox bike = Ui.Choices(new _003C_003Ez__ReadOnlyArray<string>(new string[2] { "共享单车（可用性及费用未知）", "自有自行车（初始在出发点）" }));

	private readonly TextBox parking = Ui.Input("5");

	private readonly CheckBox replan = new CheckBox
	{
		Content = "重排当前表内的灵活事项（保留其他表和固定事项）"
	};

	private readonly Dictionary<string, CheckBox> modes = new Dictionary<string, CheckBox>();

	private readonly StackPanel results = new StackPanel();

	private readonly TextBlock message = Ui.Text("先确认出发点，再生成当天路线。所有生效表均参与检查。", 12.0);

	private readonly Grid mapHost = new Grid();

	private readonly WebView2 map = new WebView2();

	private CancellationTokenSource? cancel;

	private PlannerState? draftState;

	private PlanningRequest? draftRequest;

	private long revision;

	private bool mapReady;

	private List<ItineraryCandidate> candidates = new List<ItineraryCandidate>();

	private readonly Button generate;

	private readonly ServiceSettings config;

	private readonly AmapClient provider;

	public RouteWindow(PlannerViewModel model)
	{
		vm = model;
		config = new SettingsStore(vm.DataDirectory).Load();
		provider = new AmapClient(SettingsStore.Unprotect(config.MapServiceKeyProtected));
		Title = "当天规划 · 地图与路线";
		Width = 1320.0;
		Height = 900.0;
		MinWidth = 1040.0;
		MinHeight = 700.0;
		WindowStartupLocation = WindowStartupLocation.CenterOwner;
		Background = Ui.Brush("#FBF7F0");
		FontFamily = new FontFamily("Microsoft YaHei UI");
		FontSize = 13.0;
		Grid grid = new Grid
		{
			Margin = new Thickness(18.0)
		};
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(390.0)
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition());
		Content = grid;
		StackPanel stackPanel = new StackPanel
		{
			Margin = new Thickness(0.0, 0.0, 18.0, 0.0)
		};
		grid.Children.Add(new ScrollViewer
		{
			Content = stackPanel,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto
		});
		stackPanel.Children.Add(Ui.Text("把这一天串起来", 23.0, bold: true));
		date = new StyledDateEntry
		{
			SelectedDate = vm.SelectedDate.ToDateTime(TimeOnly.MinValue)
		};
		Ui.Field(stackPanel, "规划日期", date, required: true);
		Ui.Field(stackPanel, "出发点", originText, required: true);
		stackPanel.Children.Add(Ui.Button("搜索南京市内地点", () =>
		{
			SearchOrigin();
		}));
		stackPanel.Children.Add(originList);
		Ui.Field(stackPanel, "最早出发时间 HH:mm", earliest, required: true);
		stackPanel.Children.Add(Ui.Text("交通方式（可混合）", 14.0, bold: true));
		WrapPanel wrapPanel = new WrapPanel();
		string[] array = new string[6] { "WALK", "BIKE", "BUS", "METRO", "CAR", "TAXI" };
		foreach (string text in array)
		{
			CheckBox checkBox = new CheckBox
			{
				Content = AmapClient.Name(text)
			};
			CheckBox checkBox2 = checkBox;
			bool value;
			switch (text)
			{
			case "WALK":
			case "BUS":
			case "METRO":
				value = true;
				break;
			default:
				value = false;
				break;
			}
			checkBox2.IsChecked = value;
			checkBox.Margin = new Thickness(0.0, 4.0, 15.0, 8.0);
			CheckBox checkBox3 = checkBox;
			modes[text] = checkBox3;
			wrapPanel.Children.Add(checkBox3);
		}
		stackPanel.Children.Add(wrapPanel);
		stackPanel.Children.Add(Ui.Text("公交与地铁包含必要的接驳步行；“步行”勾选项允许另选全程步行方案。", 11.0));
		Ui.Field(stackPanel, "骑行资源", bike);
		Ui.Field(stackPanel, "自驾停车缓冲（分钟）", parking);
		stackPanel.Children.Add(Ui.Text("选择自驾或自有自行车，表示车辆初始在出发点。离开车辆后不能在异地直接取车。", 11.0));
		stackPanel.Children.Add(replan);
		generate = Ui.Button("生成可选方案", () =>
		{
			Generate();
		}, primary: true);
		stackPanel.Children.Add(generate);
		stackPanel.Children.Add(Ui.Button("取消当前查询", () =>
		{
			cancel?.Cancel();
		}));
		stackPanel.Children.Add(message);
		stackPanel.Children.Add(results);
		Grid grid2 = new Grid();
		Grid.SetColumn(grid2, 1);
		grid.Children.Add(grid2);
		grid2.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(50.0)
		});
		grid2.RowDefinitions.Add(new RowDefinition());
		grid2.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(70.0)
		});
		grid2.Children.Add(Ui.Text("南京 · 当天路线预览", 20.0, bold: true));
		Grid.SetRow(mapHost, 1);
		grid2.Children.Add(mapHost);
		TextBlock element = Ui.Text("交通时间是本次查询参考，不实时监测；拥堵仅展示接口返回数据，乘客拥挤程度暂无数据。候选搜索最多12项、100次查询，未找到方案不等于不存在。", 12.0);
		Grid.SetRow(element, 2);
		grid2.Children.Add(element);
		Loaded += async (object _, RoutedEventArgs _) =>
		{
			await InitializeMap();
			ShowSaved();
		};
		Closed += (object? _, EventArgs _) =>
		{
			cancel?.Cancel();
			map.Dispose();
		};
		date.SelectedDateChanged += (object? _, SelectionChangedEventArgs _) =>
		{
			InvalidateDraft();
			ShowSaved();
		};
		originList.SelectionChanged += (object _, SelectionChangedEventArgs _) =>
		{
			InvalidateDraft();
		};
		earliest.TextChanged += (object _, TextChangedEventArgs _) =>
		{
			InvalidateDraft();
		};
		parking.TextChanged += (object _, TextChangedEventArgs _) =>
		{
			InvalidateDraft();
		};
		bike.SelectionChanged += (object _, SelectionChangedEventArgs _) =>
		{
			InvalidateDraft();
		};
		replan.Click += (object _, RoutedEventArgs _) =>
		{
			InvalidateDraft();
		};
		foreach (CheckBox value2 in modes.Values)
		{
			value2.Click += (object _, RoutedEventArgs _) =>
			{
				InvalidateDraft();
			};
		}
	}

	private void InvalidateDraft()
	{
		cancel?.Cancel();
		draftState = null;
		draftRequest = null;
		candidates = new List<ItineraryCandidate>();
		results.Children.Clear();
		message.Text = "规划条件已改变，请生成新的方案。";
	}

	private async void SearchOrigin()
	{
		await Run(async (CancellationToken token) =>
		{
			List<Place> list = await provider.SearchPlacesAsync(originText.Text.Trim(), token);
			originList.ItemsSource = list;
			originList.SelectedIndex = ((list.Count != 1) ? (-1) : 0);
			message.Text = ((list.Count == 0) ? "未找到市内地点，请使用更具体的名称。" : "请选择准确的出发地点。");
		});
	}

	private async Task Run(Func<CancellationToken, Task> action)
	{
		if (cancel != null)
		{
			return;
		}
		cancel = new CancellationTokenSource();
		generate.IsEnabled = false;
		try
		{
			await action(cancel.Token);
		}
		catch (OperationCanceledException)
		{
			message.Text = "查询已取消，尚未保存。";
		}
		catch (Exception ex2)
		{
			message.Text = ex2.Message;
		}
		finally
		{
			cancel?.Dispose();
			cancel = null;
			generate.IsEnabled = true;
		}
	}

	private async void Generate()
	{
		await Run(async (CancellationToken token) =>
		{
			if (!Ui.ValidateRequired(this))
			{
				message.Text = "请填写标记的必填项。";
				return;
			}
			DateTime? selectedDate = date.SelectedDate;
			if (selectedDate.HasValue)
			{
				DateTime valueOrDefault = selectedDate.GetValueOrDefault();
				if (TimeOnly.TryParseExact(earliest.Text.Trim(), "HH:mm", out var time))
				{
					object selectedItem = originList.SelectedItem;
					if (!(selectedItem is Place origin))
					{
						throw new ArgumentException("请搜索并选择出发点。");
					}
					if (!int.TryParse(parking.Text, out var mins) || mins < 0 || mins > 60)
					{
						throw new ArgumentException("停车缓冲应为0至60分钟。");
					}
					revision = vm.State.Revision;
					PlannerState state = vm.State.Copy();
					DateOnly day = DateOnly.FromDateTime(valueOrDefault);
					List<Occurrence> list = CalendarEngine.Expand(state, valueOrDefault.Date, valueOrDefault.Date.AddDays(1.0));
					if (list.Count == 0)
					{
						throw new ArgumentException("当天没有生效事项，请先手动填表或使用智能填表。");
					}
					foreach (string placeId in list.Select((Occurrence x) => x.PlaceId).Distinct())
					{
						Place place = state.Places.FirstOrDefault((Place x) => x.Id == placeId);
						if (place == null)
						{
							throw new ArgumentException("部分事项未填地点，请先编辑补充。");
						}
						if (!(place.Status == "VERIFIED"))
						{
							message.Text = "正在确认地点：" + place.Name;
							List<Place> list2 = await provider.SearchPlacesAsync(place.Name, token);
							Place place2 = ChoosePlace(place.Name, list2);
							if (place2 == null)
							{
								throw new OperationCanceledException();
							}
							place2.Id = place.Id;
							state.Places[state.Places.IndexOf(place)] = place2;
						}
					}
					PlanningRequest request = new PlanningRequest
					{
						Date = day,
						Origin = origin,
						Earliest = day.ToDateTime(time),
						Modes = (from x in modes
							where x.Value.IsChecked == true
							select x.Key).ToList(),
						ReplanFlexible = (replan.IsChecked == true),
						EditableTables = new HashSet<string> { vm.SelectedTableId },
						BikeResource = ((bike.SelectedIndex == 1) ? "OWN_BIKE" : "SHARED_BIKE"),
						ParkingMinutes = mins
					};
					message.Text = "正在比较路线与时间窗，可取消；尚未写入行程。";
					results.Children.Clear();
					candidates = await new ItineraryPlanner(provider).GenerateAsync(state, request, token);
					token.ThrowIfCancellationRequested();
					if (vm.State.Revision != revision)
					{
						throw new InvalidOperationException("查询期间行程已发生变化，请重新生成。");
					}
					draftState = state;
					draftRequest = request;
					message.Text = ((candidates.Count == 0) ? "当前搜索范围内未找到可行方案。可尝试提前出发、放宽灵活时间窗或增加交通方式。" : $"找到 {candidates.Count} 个不同方案，点击预览，再确认填入行程。");
					if (request.Warnings.Count > 0)
					{
						TextBlock textBlock = message;
						textBlock.Text = textBlock.Text + "\n" + string.Join("\n", request.Warnings.Distinct().Take(5));
					}
					foreach (ItineraryCandidate candidate in candidates)
					{
						AddCandidate(candidate);
					}
					if (candidates.Count > 0)
					{
						await Preview(candidates[0], request.Origin);
					}
					return;
				}
			}
			throw new ArgumentException("请选择日期并填写合法的出发时间。");
		});
	}

	private Place? ChoosePlace(string name, List<Place> list)
	{
		if (list.Count == 0)
		{
			throw new ArgumentException("“" + name + "”未找到市内地点，请先修改名称或服务配置。");
		}
		Window w = new Window
		{
			Title = "请确认地点 · " + name,
			Width = 620.0,
			Height = 420.0,
			Owner = this,
			WindowStartupLocation = WindowStartupLocation.CenterOwner
		};
		DockPanel dockPanel = new DockPanel
		{
			Margin = new Thickness(18.0)
		};
		ListBox choices = new ListBox
		{
			ItemsSource = list
		};
		Place selected = null;
		Button element = Ui.Button("使用选中的地点", () =>
		{
			if (!(choices.SelectedItem is Place value))
			{
				throw new ArgumentException("请选择一个地点。");
			}
			selected = JsonKit.Clone(value);
			w.Close();
		}, primary: true);
		DockPanel.SetDock(element, Dock.Bottom);
		dockPanel.Children.Add(element);
		dockPanel.Children.Add(choices);
		w.Content = dockPanel;
		w.ShowDialog();
		return selected;
	}

	private void AddCandidate(ItineraryCandidate candidate)
	{
		StackPanel stackPanel = new StackPanel();
		stackPanel.Children.Add(Ui.Text(candidate.Title, 15.0, bold: true));
		stackPanel.Children.Add(Ui.Text(candidate.Summary, 12.0));
		for (int i = 0; i < candidate.Stops.Count; i++)
		{
			PlannedStop plannedStop = candidate.Stops[i];
			int index = i;
			stackPanel.Children.Add(Ui.Text($"{plannedStop.Depart:HH:mm} 出发 → {plannedStop.Arrive:HH:mm} 到达\n{plannedStop.Start:HH:mm}—{plannedStop.End:HH:mm} {plannedStop.Original.Title}\n{plannedStop.Route.Label} · {plannedStop.Route.CostText}\n{plannedStop.Route.CostNote}", 12.0));
			string text = string.Join("；", (from x in plannedStop.Route.Segments
				select x.Congestion into x
				where !string.IsNullOrWhiteSpace(x)
				select x).Distinct());
			if (text.Length > 0)
			{
				stackPanel.Children.Add(Ui.Text("道路拥堵参考：" + text, 11.0));
			}
			stackPanel.Children.Add(Ui.Button("更换此段交通", () =>
			{
				ChangeLeg(candidate, index);
			}));
		}
		stackPanel.Children.Add(Ui.Button("在地图预览", async () =>
		{
			await Preview(candidate, draftRequest.Origin);
		}));
		stackPanel.Children.Add(Ui.Button("确认此方案并填入行程", () =>
		{
			if (draftState == null || draftRequest == null)
			{
				throw new InvalidOperationException("请重新生成方案。");
			}
			PlannerState next = PlanService.Confirm(vm.State, draftState, draftRequest, candidate, revision);
			if (Ui.Confirm($"将保存 {draftRequest.Date:yyyy-MM-dd} 的路线方案。\n{candidate.Summary}\n灵活事项的变动将同时保存。交通数据仅作本次查询参考。\n\n确认？"))
			{
				vm.Persist(next);
				draftState = null;
				message.Text = "方案已保存；相关活动改动后需要重新规划。";
				results.Children.Clear();
				ShowSaved();
			}
		}, primary: true));
		results.Children.Add(Ui.Card(stackPanel));
	}

	private async void ChangeLeg(ItineraryCandidate candidate, int index)
	{
		await Run(async (CancellationToken token) =>
		{
			if (draftState == null || draftRequest == null)
			{
				throw new ArgumentException("请先重新生成方案。");
			}
			PlannerState state = draftState;
			PlanningRequest request = draftRequest;
			PlannedStop stop = candidate.Stops[index];
			Place from = ((index == 0) ? request.Origin : state.Places.Single((Place p) => p.Id == candidate.Stops[index - 1].Original.PlaceId));
			Place to = state.Places.Single((Place p) => p.Id == stop.Original.PlaceId);
			List<RouteOption> choices = new List<RouteOption>();
			foreach (string item in TransportPolicy.QueryModes(request.Modes))
			{
				try
				{
					List<RouteOption> list = choices;
					list.AddRange(await provider.RoutesAsync(from, to, item, stop.Depart, token));
				}
				catch (InvalidOperationException ex)
				{
					message.Text = ex.Message;
				}
			}
			choices = choices.Where((RouteOption x) => TransportPolicy.Allows(x, request.Modes)).ToList();
			if (choices.Count == 0)
			{
				throw new ArgumentException("此段未返回可用的其他路线。");
			}
			Window w = new Window
			{
				Title = "更换交通 · " + stop.Original.Title,
				Width = 710.0,
				Height = 450.0,
				Owner = this,
				WindowStartupLocation = WindowStartupLocation.CenterOwner
			};
			DockPanel dockPanel = new DockPanel
			{
				Margin = new Thickness(18.0)
			};
			ListBox list2 = new ListBox
			{
				ItemsSource = choices
			};
			RouteOption selected = null;
			Button element = Ui.Button("使用所选路线并检查衔接", () =>
			{
				RouteOption routeOption = JsonKit.Clone((list2.SelectedItem as RouteOption) ?? throw new ArgumentException("请选择路线。"));
				foreach (RouteSegment item2 in routeOption.Segments.Where((RouteSegment x) => x.Mode == "BIKE"))
				{
					item2.ResourceType = request.BikeResource;
				}
				if (routeOption.Uses("BIKE") && request.BikeResource == "OWN_BIKE")
				{
					routeOption.KnownCostFen = 0;
					routeOption.CostComplete = true;
					routeOption.CostNote = "自有自行车，不计购置维护费用";
				}
				int num = (routeOption.Uses("CAR") ? (request.ParkingMinutes * 60) : 0);
				ItineraryCandidate itineraryCandidate = new ItineraryCandidate
				{
					Title = candidate.Title + " · 自选交通",
					Stops = candidate.Stops.ToList()
				};
				itineraryCandidate.Stops[index] = stop with
				{
					Route = routeOption,
					BufferSec = num,
					Arrive = stop.Depart.AddSeconds(routeOption.DurationSec + num)
				};
				PlanService.ValidateResources(state, request, itineraryCandidate);
				selected = routeOption;
				candidate.Stops = itineraryCandidate.Stops;
				w.Close();
			}, primary: true);
			DockPanel.SetDock(element, Dock.Bottom);
			dockPanel.Children.Add(element);
			dockPanel.Children.Add(list2);
			w.Content = dockPanel;
			w.ShowDialog();
			if (selected != null)
			{
				results.Children.Clear();
				foreach (ItineraryCandidate candidate2 in candidates)
				{
					AddCandidate(candidate2);
				}
				await Preview(candidate, request.Origin);
				message.Text = "已替换此段并复核整日衔接，确认方案后才保存。";
			}
		});
	}

	private void ShowSaved()
	{
		DateTime? selectedDate = date.SelectedDate;
		if (!selectedDate.HasValue)
		{
			return;
		}
		DateTime d = selectedDate.GetValueOrDefault();
		Plan plan = vm.State.Plans.FirstOrDefault((Plan x) => x.PlanDate == DateOnly.FromDateTime(d));
		if (plan == null)
		{
			return;
		}
		StackPanel stackPanel = new StackPanel();
		stackPanel.Children.Add(Ui.Text("已保存方案 · " + ((plan.Status == "CONFIRMED") ? "已确认" : "需重新规划"), 15.0, bold: true));
		List<PlanLeg> list = (from x in vm.State.Legs
			where x.PlanId == plan.Id
			orderby x.Seq
			select x).ToList();
		foreach (PlanLeg leg in list)
		{
			Activity activity = vm.State.Activities.FirstOrDefault((Activity x) => x.Id == leg.ToActivityId);
			List<RouteSegment> source = (from x in vm.State.Segments
				where x.LegId == leg.Id
				orderby x.Seq
				select x).ToList();
			int? knownCostFen = leg.KnownCostFen;
			string text;
			if (knownCostFen.HasValue)
			{
				int valueOrDefault = knownCostFen.GetValueOrDefault();
				text = $"已知 ¥{(double)valueOrDefault / 100.0:0.##}" + (leg.CostComplete ? "" : " + 未知项");
			}
			else
			{
				text = "费用暂无数据";
			}
			string value = text;
			stackPanel.Children.Add(Ui.Text($"{leg.DepartAt:HH:mm} → {leg.ArriveAt:HH:mm}  {activity?.Title}\n" + string.Join(" + ", source.Select((RouteSegment x) => AmapClient.Name(x.Mode))) + $"\n{value} · {leg.CostNote}\n查询 {leg.QueryAt:MM-dd HH:mm} · {((leg.ValidationStatus == "VALID") ? "已核验" : "需重新核验")}", 12.0));
		}
		results.Children.Add(Ui.Card(stackPanel));
		if (!mapReady)
		{
			return;
		}
		map.ExecuteScriptAsync("renderPlan(" + JsonSerializer.Serialize(new
		{
			origin = vm.State.Places.FirstOrDefault((Place x) => x.Id == plan.StartPlaceId),
			segments = list.SelectMany((PlanLeg l) => vm.State.Segments.Where((RouteSegment x) => x.LegId == l.Id)).ToList(),
			stops = list.Select((PlanLeg l) => vm.State.Places.FirstOrDefault((Place p) => p.Id == CalendarEngine.Expand(vm.State, d.Date, d.Date.AddDays(1.0)).FirstOrDefault((Occurrence o) =>
			{
				if (o.Activity.Id == l.ToActivityId)
				{
					DateOnly? originDate = o.OriginDate;
					DateOnly? toOriginDate = l.ToOriginDate;
					if (originDate.HasValue != toOriginDate.HasValue)
					{
						return false;
					}
					if (!originDate.HasValue)
					{
						return true;
					}
					return originDate.GetValueOrDefault() == toOriginDate.GetValueOrDefault();
				}
				return false;
			})?.PlaceId))
		}, JsonKit.Options) + ")");
	}

	private async Task InitializeMap()
	{
		_ = 2;
		try
		{
			string js = SettingsStore.Unprotect(config.MapJsKeyProtected);
			string security = SettingsStore.Unprotect(config.MapSecurityProtected);
			if (js.Length == 0 || security.Length == 0)
			{
				mapHost.Children.Add(Ui.Card(Ui.Text("地图尚未配置\n\n在“服务设置”填写高德Web端JS Key和安全密钥后，可在此查看真实底图与路线。文字方案查询使用Web服务Key。", 16.0)));
				return;
			}
			mapHost.Children.Add(map);
			CoreWebView2Environment environment = await CoreWebView2Environment.CreateAsync(null, Path.Combine(vm.DataDirectory, "Browser"));
			await map.EnsureCoreWebView2Async(environment);
			map.CoreWebView2.Settings.AreDevToolsEnabled = false;
			map.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
			map.CoreWebView2.Settings.IsStatusBarEnabled = false;
			map.CoreWebView2.NewWindowRequested += (object? _, CoreWebView2NewWindowRequestedEventArgs e) =>
			{
				e.Handled = true;
			};
			map.CoreWebView2.PermissionRequested += (object? _, CoreWebView2PermissionRequestedEventArgs e) =>
			{
				e.State = CoreWebView2PermissionState.Deny;
			};
			map.CoreWebView2.NavigationStarting += (object? _, CoreWebView2NavigationStartingEventArgs e) =>
			{
				if (!e.Uri.StartsWith("about:", StringComparison.OrdinalIgnoreCase))
				{
					e.Cancel = true;
				}
			};
			string htmlContent = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "MapPage.html")).Replace("__CONFIG__", JsonSerializer.Serialize(new
			{
				key = js,
				security = security
			}));
			TaskCompletionSource loaded = new TaskCompletionSource();
			map.CoreWebView2.NavigationCompleted += (object? _, CoreWebView2NavigationCompletedEventArgs _) =>
			{
				loaded.TrySetResult();
			};
			map.NavigateToString(htmlContent);
			await loaded.Task.WaitAsync(TimeSpan.FromSeconds(15L));
			mapReady = true;
		}
		catch (Exception)
		{
			mapHost.Children.Clear();
			mapHost.Children.Add(Ui.Card(Ui.Text("内嵌地图未能启动。请安装Microsoft Edge WebView2运行时并检查地图配置；文字方案仍可使用。", 15.0)));
		}
	}

	private async Task Preview(ItineraryCandidate candidate, Place origin)
	{
		if (!mapReady)
		{
			return;
		}
		PlannerState state = draftState ?? vm.State;
		var value = new
		{
			origin = origin,
			segments = candidate.Stops.SelectMany((PlannedStop x) => x.Route.Segments),
			stops = candidate.Stops.Select((PlannedStop x) => state.Places.First((Place p) => p.Id == x.Original.PlaceId))
		};
		await map.ExecuteScriptAsync("renderPlan(" + JsonSerializer.Serialize(value, JsonKit.Options) + ")");
	}
}
