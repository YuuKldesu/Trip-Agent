using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Planner.Core;
using Planner.Data;

namespace Planner.Desktop;

public sealed class SettingsPage : UserControl
{
	private readonly PlannerViewModel vm;

	private readonly MainWindow owner;

	private readonly ComboBox categoryPicker = Ui.Choices(new _003C_003Ez__ReadOnlyArray<string>(new string[6] { "外观与布局", "看板显示", "个人资料", "智能服务", "地图服务", "关于与数据" }));

	private readonly TextBox search = Ui.Input();

	private readonly StackPanel content = new StackPanel();

	private readonly List<(string Category, string Keywords, FrameworkElement Row)> rows = new List<(string, string, FrameworkElement)>();

	private readonly Dictionary<string, Button> categories = new Dictionary<string, Button>();

	private readonly TextBlock searchStatus = Ui.Text("", 11.0);

	private TextBlock? searchPlaceholder;

	private Button? clearSearch;

	private string category;

	public SettingsPage(PlannerViewModel model, MainWindow owner, string initial = "appearance")
	{
		vm = model;
		this.owner = owner;
		category = initial;
		Tag = "SettingsPage";
		HorizontalContentAlignment = HorizontalAlignment.Stretch;
		DockPanel dockPanel = (DockPanel)(Content = new DockPanel
		{
			Margin = new Thickness(20.0)
		});
		Viewbox viewbox = ShellIcons.Glyph("\ue713");
		viewbox.Margin = new Thickness(0.0, 0.0, 10.0, 0.0);
		StackPanel element = Ui.Row(viewbox, Ui.Text("设置", 22.0, bold: true));
		DockPanel.SetDock(element, Dock.Top);
		dockPanel.Children.Add(element);
		search.Tag = "SettingsSearch";
		search.ToolTip = "搜索设置";
		search.TextChanged += (object _, TextChangedEventArgs _) =>
		{
			Filter();
		};
		FrameworkElement element2 = BuildSearch(dockPanel);
		DockPanel.SetDock(element2, Dock.Top);
		dockPanel.Children.Add(element2);
		Grid body = new Grid
		{
			Margin = new Thickness(0.0, 10.0, 0.0, 0.0)
		};
		body.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(154.0)
		});
		body.ColumnDefinitions.Add(new ColumnDefinition());
		dockPanel.Children.Add(body);
		StackPanel nav = new StackPanel
		{
			Margin = new Thickness(0.0, 0.0, 16.0, 0.0)
		};
		body.Children.Add(nav);
		(string, string)[] array = new (string, string)[6]
		{
			("appearance", "外观与布局"),
			("board", "看板显示"),
			("profile", "个人资料"),
			("ai", "智能服务"),
			("maps", "地图服务"),
			("about", "关于与数据")
		};
		for (int num = 0; num < array.Length; num++)
		{
			(string, string) tuple = array[num];
			string key = tuple.Item1;
			Button button = Ui.Button(tuple.Item2, () =>
			{
				SelectCategory(key);
			});
			button.Tag = "SettingsCategory:" + key;
			button.HorizontalContentAlignment = HorizontalAlignment.Left;
			categories[key] = button;
			nav.Children.Add(button);
		}
		ScrollViewer element3 = new ScrollViewer
		{
			Content = content,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
			HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
		};
		Grid.SetColumn(element3, 1);
		body.Children.Add(element3);
		body.SizeChanged += (object _, SizeChangedEventArgs _) =>
		{
			bool flag = body.ActualWidth < 600.0;
			body.ColumnDefinitions[0].Width = new GridLength((!flag) ? 154 : 0);
			nav.Visibility = (flag ? Visibility.Collapsed : Visibility.Visible);
		};
		categoryPicker.Tag = "SettingsCategoryPicker";
		categoryPicker.SelectionChanged += (object _, SelectionChangedEventArgs _) =>
		{
			if (categoryPicker.SelectedIndex >= 0)
			{
				SelectCategory((new string[6] { "appearance", "board", "profile", "ai", "maps", "about" })[categoryPicker.SelectedIndex]);
			}
		};
		DockPanel.SetDock(categoryPicker, Dock.Top);
		dockPanel.Children.Insert(dockPanel.Children.IndexOf(body), categoryPicker);
		body.SizeChanged += (object _, SizeChangedEventArgs _) =>
		{
			categoryPicker.Visibility = ((!(body.ActualWidth < 600.0)) ? Visibility.Collapsed : Visibility.Visible);
		};
		Appearance();
		Board();
		Profile();
		Services();
		Add("about", "关于 版本 数据 保存 备份 提醒", new StackPanel
		{
			Children = 
			{
				(UIElement)Ui.Text("南京市内智能行程表", 19.0, bold: true),
				(UIElement)Ui.Text("版本：" + typeof(SettingsPage).Assembly.GetName().Version, 12.0),
				(UIElement)Ui.Text("管理日常安排，通过连续对话完善行程。"),
				(UIElement)Ui.Text("数据目录：" + vm.DataDirectory, 12.0),
				(UIElement)Ui.Text("关闭软件后备份整个Data目录；分享软件时不要附带个人数据或密钥。提醒仅在软件运行时生效。", 12.0)
			}
		});
		SelectCategory(category);
	}

	private void Add(string c, string keywords, FrameworkElement row)
	{
		rows.Add((c, keywords, row));
		row.Margin = new Thickness(14.0, 0.0, 10.0, 20.0);
		content.Children.Add(row);
	}

	public void SelectCategory(string selected)
	{
		category = selected;
		categoryPicker.SelectedIndex = Array.IndexOf(new string[6] { "appearance", "board", "profile", "ai", "maps", "about" }, selected);
		search.Text = "";
		Filter();
	}

	private void Filter()
	{
		string text = search.Text.Trim();
		int num = 0;
		foreach (var row in rows)
		{
			bool flag = ((text.Length > 0) ? row.Keywords.Contains(text, StringComparison.OrdinalIgnoreCase) : (row.Category == category));
			row.Row.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
			if (flag)
			{
				num++;
			}
		}
		foreach (var (text3, button2) in categories)
		{
			button2.Background = Ui.Brush((text.Length == 0 && text3 == category) ? "#E5D5C5" : "#F2E6D7");
		}
		if (searchPlaceholder != null)
		{
			searchPlaceholder.Visibility = ((search.Text.Length != 0) ? Visibility.Collapsed : Visibility.Visible);
		}
		if (clearSearch != null)
		{
			clearSearch.Visibility = ((search.Text.Length == 0) ? Visibility.Collapsed : Visibility.Visible);
		}
		TextBlock textBlock = searchStatus;
		string text4;
		if (text.Length != 0)
		{
			text4 = ((num == 0) ? "未找到相关设置，试试“主题”“字号”或“API”。" : $"找到 {num} 组相关设置");
		}
		else
		{
			text4 = "仅搜索本页设置，不搜索行程。";
		}
		textBlock.Text = text4;
	}

	private FrameworkElement BuildSearch(FrameworkElement host)
	{
		StackPanel stackPanel = new StackPanel();
		stackPanel.HorizontalAlignment = HorizontalAlignment.Left;
		stackPanel.MaxWidth = 560.0;
		stackPanel.Margin = new Thickness(0.0, 4.0, 0.0, 0.0);
		stackPanel.SetBinding(FrameworkElement.WidthProperty, new Binding("ActualWidth")
		{
			Source = host
		});
		Grid grid = new Grid
		{
			MinHeight = 34.0
		};
		Border element = new Border
		{
			Background = AppTheme.Brush("Surface"),
			BorderBrush = AppTheme.Brush("Border"),
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(8.0),
			Child = grid
		};
		stackPanel.Children.Add(element);
		search.Margin = new Thickness(0.0);
		search.Padding = new Thickness(34.0, 6.0, 34.0, 6.0);
		search.BorderThickness = new Thickness(0.0);
		search.Background = Brushes.Transparent;
		search.MinHeight = 34.0;
		Typography.Set(search, 13.0);
		grid.Children.Add(search);
		AutomationProperties.SetName(search, "搜索设置");
		Viewbox viewbox = ShellIcons.Glyph("\ue721", 15.0);
		viewbox.Margin = new Thickness(10.0, 0.0, 0.0, 0.0);
		viewbox.HorizontalAlignment = HorizontalAlignment.Left;
		viewbox.IsHitTestVisible = false;
		grid.Children.Add(viewbox);
		searchPlaceholder = Ui.Text("搜索设置，例如：主题、字号、API", 12.0);
		searchPlaceholder.TextWrapping = TextWrapping.NoWrap;
		searchPlaceholder.TextTrimming = TextTrimming.CharacterEllipsis;
		searchPlaceholder.Tag = "SettingsSearchPlaceholder";
		searchPlaceholder.Margin = new Thickness(34.0, 0.0, 34.0, 0.0);
		searchPlaceholder.VerticalAlignment = VerticalAlignment.Center;
		searchPlaceholder.IsHitTestVisible = false;
		searchPlaceholder.Foreground = AppTheme.Brush("Muted");
		grid.Children.Add(searchPlaceholder);
		clearSearch = Ui.Button("×", () =>
		{
			search.Clear();
			search.Focus();
		});
		clearSearch.Tag = "SettingsSearchClear";
		clearSearch.ToolTip = "清除搜索";
		clearSearch.Padding = new Thickness(5.0, 0.0, 5.0, 0.0);
		clearSearch.Margin = new Thickness(0.0, 0.0, 5.0, 0.0);
		clearSearch.Background = Brushes.Transparent;
		clearSearch.HorizontalAlignment = HorizontalAlignment.Right;
		clearSearch.VerticalAlignment = VerticalAlignment.Center;
		grid.Children.Add(clearSearch);
		searchStatus.Tag = "SettingsSearchStatus";
		searchStatus.Foreground = AppTheme.Brush("Muted");
		stackPanel.Children.Add(searchStatus);
		return stackPanel;
	}

	private void Appearance()
	{
		StackPanel stackPanel = new StackPanel();
		stackPanel.Children.Add(Ui.Text("颜色主题", 19.0, bold: true));
		stackPanel.Children.Add(Ui.Text("基础主题与强调色即时预览；行程表颜色独立保留。", 12.0));
		ComboBox theme = Ui.Choices(ThemePalette.Themes, Array.IndexOf(ThemePalette.Themes, vm.Appearance.Theme));
		theme.Tag = "SettingsTheme";
		Ui.Field(stackPanel, "基础主题", theme);
		string[] array = ThemePalette.Accents.Keys.ToArray();
		ComboBox accent = Ui.Choices(array, Array.IndexOf(array, vm.Appearance.AccentColor));
		accent.Tag = "SettingsAccent";
		Ui.Field(stackPanel, "强调色", accent);
		StackPanel seasonal = new StackPanel
		{
			Tag = "HalloweenThemePreview",
			Visibility = ((!(vm.Appearance.Theme == "万圣节")) ? Visibility.Collapsed : Visibility.Visible)
		};
		seasonal.Children.Add(Ui.Row(SeasonalDecorations.Badge(), Ui.Text("南瓜橙 × 夜幕紫", 14.0, bold: true)));
		seasonal.Children.Add(Ui.Text("点一下南瓜，今晚有一颗糖。万圣节主题使用固定强调色，切回其他主题后恢复原强调色。", 12.0));
		Border seasonalCard = Ui.Card(seasonal);
		seasonalCard.Visibility = seasonal.Visibility;
		stackPanel.Children.Add(seasonalCard);
		theme.SelectionChanged += (object _, SelectionChangedEventArgs _) =>
		{
			vm.UpdateAppearance((BoardPreferences p) =>
			{
				p.Theme = (string)theme.SelectedItem;
			});
			seasonal.Visibility = ((!(vm.Appearance.Theme == "万圣节")) ? Visibility.Collapsed : Visibility.Visible);
			seasonalCard.Visibility = seasonal.Visibility;
			accent.IsEnabled = vm.Appearance.Theme != "万圣节";
		};
		accent.IsEnabled = vm.Appearance.Theme != "万圣节";
		accent.SelectionChanged += (object _, SelectionChangedEventArgs _) =>
		{
			vm.UpdateAppearance((BoardPreferences p) =>
			{
				p.AccentColor = (string)accent.SelectedItem;
			});
		};
		stackPanel.Children.Add(Ui.Button("恢复默认主题", () =>
		{
			theme.SelectedIndex = 0;
			accent.SelectedIndex = 0;
		}));
		Add("appearance", "外观 主题 颜色 浅色 深色 暖色 强调色 万圣节 Halloween 南瓜 彩蛋", stackPanel);
		StackPanel stackPanel2 = new StackPanel();
		stackPanel2.Children.Add(Ui.Text("界面字号", 18.0, bold: true));
		TextBlock label = Ui.Text($"正文字号：{vm.Appearance.FontSize:0}");
		stackPanel2.Children.Add(label);
		Slider slider = new Slider
		{
			Minimum = 12.0,
			Maximum = 22.0,
			Value = vm.Appearance.FontSize,
			TickFrequency = 1.0,
			IsSnapToTickEnabled = true,
			Margin = new Thickness(0.0, 12.0, 0.0, 16.0),
			Tag = "SettingsFont"
		};
		stackPanel2.Children.Add(slider);
		slider.ValueChanged += (object _, RoutedPropertyChangedEventArgs<double> e) =>
		{
			Ui.Safe(() =>
			{
				vm.UpdateAppearance((BoardPreferences p) =>
				{
					p.FontSize = Math.Round(e.NewValue);
				});
			});
			label.Text = $"正文字号：{vm.Appearance.FontSize:0}";
		};
		stackPanel2.Children.Add(Ui.Text("标题、正文和时间按比例调整，即时预览。", 12.0));
		Add("appearance", "外观 字号 字体 大小", stackPanel2);
		StackPanel stackPanel3 = new StackPanel();
		stackPanel3.Children.Add(Ui.Text("界面模式", 17.0, bold: true));
		string[] modes = new string[3] { "AUTO", "FULL", "COMPACT" };
		ComboBox choice = Ui.Choices(new _003C_003Ez__ReadOnlyArray<string>(new string[3] { "自动切换", "完整界面", "简洁侧栏" }), Array.IndexOf(modes, vm.Appearance.LayoutMode));
		choice.Tag = "SettingsLayout";
		stackPanel3.Children.Add(choice);
		choice.SelectionChanged += (object _, SelectionChangedEventArgs _) =>
		{
			Ui.Safe(() =>
			{
				owner.SetLayoutMode(modes[choice.SelectedIndex]);
			});
		};
		stackPanel3.Children.Add(Ui.Text("简洁模式以今天与接下来六天的纵向行程为主。自动模式根据窗口比例和字号切换。", 12.0));
		CheckBox pin = new CheckBox
		{
			Content = "简洁模式保持置顶",
			IsChecked = vm.Appearance.AlwaysOnTop,
			Tag = "SettingsPin"
		};
		pin.Click += (object _, RoutedEventArgs _) =>
		{
			vm.UpdateAppearance((BoardPreferences p) =>
			{
				p.AlwaysOnTop = pin.IsChecked == true;
			});
		};
		stackPanel3.Children.Add(pin);
		stackPanel3.Children.Add(Ui.Text("Ctrl+B 显示或隐藏侧边栏；左滑完成、右滑收藏，右键提供同样的操作。", 12.0));
		Add("appearance", "外观 界面 模式 自动 完整 简洁 置顶 侧边栏 快捷键 手势", stackPanel3);
	}

	private void Board()
	{
		StackPanel stackPanel = new StackPanel();
		stackPanel.Children.Add(Ui.Text("看板显示", 19.0, bold: true));
		(string, bool)[] array = new (string, bool)[2]
		{
			("显示已完成", true),
			("显示未完成", false)
		};
		for (int i = 0; i < array.Length; i++)
		{
			(string, bool) tuple = array[i];
			string item = tuple.Item1;
			bool completed = tuple.Item2;
			CheckBox check = new CheckBox
			{
				Content = item,
				IsChecked = (completed ? vm.Appearance.ShowCompleted : vm.Appearance.ShowIncomplete)
			};
			check.Click += (object _, RoutedEventArgs _) =>
			{
				vm.UpdateAppearance((BoardPreferences p) =>
				{
					if (completed)
					{
						p.ShowCompleted = check.IsChecked == true;
					}
					else
					{
						p.ShowIncomplete = check.IsChecked == true;
					}
				});
			};
			stackPanel.Children.Add(check);
		}
		stackPanel.Children.Add(Ui.Text("列表按日期连续排列；月历直接显示时间、地点、耗时和备注。", 12.0));
		Add("board", "看板 月历 列表 完成 未完成 显示", stackPanel);
	}

	private void Profile()
	{
		StackPanel p = new StackPanel();
		p.Children.Add(Ui.Text("本地个人资料", 19.0, bold: true));
		p.Children.Add(Ui.Text("头像", 13.0, bold: true));
		WrapPanel wrapPanel = new WrapPanel();
		p.Children.Add(wrapPanel);
		string[] array = new string[3] { "我", "行", "旅" };
		foreach (string a in array)
		{
			Button button = Ui.Button(a, () =>
			{
				vm.UpdateAppearance((BoardPreferences x) =>
				{
					x.Avatar = a;
				});
			});
			button.ToolTip = "使用“" + a + "”头像";
			wrapPanel.Children.Add(button);
		}
		TextBox nickname = Ui.Input(vm.Appearance.Nickname);
		nickname.MaxLength = 40;
		nickname.Tag = "SettingsNickname";
		Ui.Field(p, "昵称", nickname);
		p.Children.Add(Ui.Button("保存个人资料", () =>
		{
			if (Ui.ValidateRequired(p))
			{
				vm.UpdateAppearance((BoardPreferences x) =>
				{
					x.Nickname = nickname.Text.Trim();
				});
			}
		}, primary: true));
		p.Children.Add(Ui.Text("仅保存在本机，不需要注册或登录。", 12.0));
		Add("profile", "个人 资料 头像 昵称 账号", p);
	}

	private void Services()
	{
		SettingsStore store = new SettingsStore(vm.DataDirectory);
		ServiceSettings serviceSettings = store.Load();
		StackPanel ai = new StackPanel();
		ai.Children.Add(Ui.Text("智能服务", 19.0, bold: true));
		ai.Children.Add(Ui.Text("保存配置不会发起网络请求。模型仅收到当前对话，不默认上传数据库。", 12.0));
		string[] providers = new string[3] { "AUTO", "LOCAL", "CUSTOM" };
		ComboBox provider = Ui.Choices(new _003C_003Ez__ReadOnlyArray<string>(new string[3] { "自动兼容旧设置", "本地规则引导", "自定义 API（OpenAI 兼容）" }), Math.Max(0, Array.IndexOf(providers, serviceSettings.AgentProvider)));
		TextBox endpoint = Ui.Input(serviceSettings.LlmEndpoint);
		TextBox model = Ui.Input(serviceSettings.Model);
		bool reset = false;
		PasswordBox key = Secret(serviceSettings.LlmKeyProtected);
		PasswordBox web = Secret(serviceSettings.MapServiceKeyProtected);
		PasswordBox js = Secret(serviceSettings.MapJsKeyProtected);
		PasswordBox security = Secret(serviceSettings.MapSecurityProtected);
		CheckBox json = new CheckBox
		{
			Content = "使用JSON响应模式",
			IsChecked = serviceSettings.JsonMode
		};
		Ui.Field(ai, "对话服务", provider);
		ai.Children.Add(Ui.Text("自定义 API：填写服务商提供的接口地址、模型名称和密钥。支持 OpenAI Chat Completions 兼容协议。", 12.0));
		Ui.Field(ai, "兼容接口地址（/v1 或完整 chat/completions）", endpoint);
		Ui.Field(ai, "模型名称", model);
		Ui.Field(ai, "模型API密钥", key);
		ai.Children.Add(json);
		ComboBox persona = Ui.Choices(AssistantPersonas.Names, Array.IndexOf(AssistantPersonas.Modes, serviceSettings.AssistantPersona));
		persona.Tag = "SettingsAssistantPersona";
		Ui.Field(ai, "助手模式", persona);
		ai.Children.Add(Ui.Text("角色模式仅改变表达风格，不改变地图查询、行程核对或保存规则。即时保存，从下一轮对话生效；本地规则仅提供简单语气，完整角色对话需连接模型。", 12.0));
		persona.SelectionChanged += (object _, SelectionChangedEventArgs _) =>
		{
			Ui.Safe(() =>
			{
				ServiceSettings serviceSettings2 = store.Load();
				serviceSettings2.AssistantPersona = AssistantPersonas.Modes[persona.SelectedIndex];
				store.Save(serviceSettings2);
				vm.Notify("助手模式已切换为" + AssistantPersonas.Name(serviceSettings2.AssistantPersona) + "，从下一轮对话生效");
			});
		};
		provider.SelectionChanged += (object _, SelectionChangedEventArgs _) =>
		{
			RequiredServiceFields();
		};
		endpoint.TextChanged += (object _, TextChangedEventArgs _) =>
		{
			RequiredServiceFields();
		};
		key.PasswordChanged += (object _, RoutedEventArgs _) =>
		{
			RequiredServiceFields();
		};
		RequiredServiceFields();
		StackPanel stackPanel = new StackPanel();
		stackPanel.Children.Add(Ui.Text("地图服务", 19.0, bold: true));
		Ui.Field(stackPanel, "Web服务Key（地点与路线查询）", web);
		Ui.Field(stackPanel, "Web端JS Key（内嵌地图）", js);
		Ui.Field(stackPanel, "JS安全密钥 securityJsCode", security);
		stackPanel.Children.Add(Ui.Text("交通费用与拥堵以接口实际返回为准，未知数据不会编造。", 12.0));
		if (reset)
		{
			ai.Children.Add(Ui.Text("部分旧密钥无法由当前Windows用户解密，请重新填写后再保存。", 12.0, bold: true));
			stackPanel.Children.Add(Ui.Text("部分旧密钥无法解密，请重新填写后再保存。", 12.0, bold: true));
		}
		ai.Children.Add(Ui.Button("保存服务配置", Save, primary: true));
		stackPanel.Children.Add(Ui.Button("保存服务配置", Save, primary: true));
		Add("ai", "智能 模型 AI API LLM OpenAI 接口 密钥 本地规则 引导 助手 模式 角色 牧濑红莉栖 まきせ くりす Kurisu", ai);
		Add("maps", "地图 高德 路线 API Key securityJsCode 密钥", stackPanel);
		void RequiredServiceFields()
		{
			bool flag = providers[provider.SelectedIndex] == "CUSTOM" || (providers[provider.SelectedIndex] == "AUTO" && (endpoint.Text.Length > 0 || key.Password.Length > 0));
			Ui.SetRequired(endpoint, flag);
			Ui.SetRequired(model, flag);
			bool flag2 = Uri.TryCreate(endpoint.Text.Trim(), UriKind.Absolute, out Uri result) && result.IsLoopback;
			Ui.SetRequired(key, flag && !flag2);
		}
		void Save()
		{
			if (Ui.ValidateRequired(ai))
			{
				if (endpoint.Text.Trim().Length > 0)
				{
					LlmClient.Endpoint(endpoint.Text);
				}
				string text = providers[provider.SelectedIndex];
				bool flag = ((text == "AUTO" || text == "CUSTOM") ? true : false);
				if (flag && key.Password.Length > 0 && (model.Text.Trim().Length == 0 || endpoint.Text.Trim().Length == 0))
				{
					throw new ArgumentException("填写模型密钥时也应填写接口地址与模型名称。");
				}
				store.Save(new ServiceSettings
				{
					AgentProvider = providers[provider.SelectedIndex],
					LlmEndpoint = endpoint.Text.Trim(),
					Model = model.Text.Trim(),
					LlmKeyProtected = SettingsStore.Protect(key.Password),
					MapServiceKeyProtected = SettingsStore.Protect(web.Password),
					MapJsKeyProtected = SettingsStore.Protect(js.Password),
					MapSecurityProtected = SettingsStore.Protect(security.Password),
					JsonMode = (json.IsChecked == true),
					AssistantPersona = AssistantPersonas.Modes[persona.SelectedIndex]
				});
				vm.Notify("服务配置已保存，未发送网络请求");
			}
		}
		PasswordBox Secret(string original)
		{
			string text = SettingsStore.ReadForSettings(original);
			if (original.Length > 0 && text.Length == 0)
			{
				reset = true;
			}
			return new PasswordBox
			{
				Password = text,
				Padding = new Thickness(9.0),
				Margin = new Thickness(0.0, 3.0, 0.0, 10.0)
			};
		}
	}
}
