using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Planner.Core;
using Planner.Data;

namespace Planner.Desktop;

public sealed class GuidedAssistantPanel : DockPanel, IDisposable
{
	private readonly PlannerViewModel vm;

	private readonly Window owner;

	private readonly GuidedAgentClient client;

	private readonly ConversationStore store;

	private readonly TextBlock working = Ui.Text("正在回复…", 12.0);

	private readonly TextBlock errorText = Ui.Text("", 12.0);

	private readonly Border errorBubble = new Border
	{
		Tag = "ConversationError",
		Visibility = Visibility.Collapsed
	};

	private GuidedConversation conversation;

	private readonly StackPanel log = new StackPanel();

	private readonly StackPanel receipts = new StackPanel();

	private readonly ScrollViewer transcript;

	private readonly StackPanel branches = new StackPanel
	{
		Orientation = Orientation.Horizontal
	};

	private readonly TextBox input = Ui.Input();

	private readonly TextBlock status = Ui.Text("", 12.0);

	private readonly TextBlock hint = Ui.Text("", 11.0);

	private readonly TextBlock composeLabel = Ui.Text("说说你的安排", 12.0, bold: true);

	private readonly Button send;

	private readonly Button retry;

	private readonly Button cancel;

	private readonly Button editCancel;

	private readonly Button latest = Ui.Button("↓ 回到最新", () =>
	{
	});

	private readonly TextBlock modelLabel = Ui.Text("", 11.0);

	private bool followLatest = true;

	private readonly DispatcherTimer draftTimer = new DispatcherTimer
	{
		Interval = TimeSpan.FromMilliseconds(300L)
	};

	private readonly ConversationRequests requests = new ConversationRequests();

	private AgentReviewWindow? review;

	private bool rendering;

	private bool disposed;

	private bool readOnly;

	private string currentPersona = "";

	private string? editId;

	private string previousDraft = "";

	private string reviewBranch = "";

	private long reviewVersion;

	private CancellationTokenSource? pending
	{
		get
		{
			ConversationRequest conversationRequest = requests.Get(conversation.ActiveBranchId);
			if (conversationRequest == null || !conversationRequest.Running)
			{
				return null;
			}
			return conversationRequest.Cancellation;
		}
	}

	public GuidedConversation Conversation => conversation;

	public GuidedAssistantPanel(PlannerViewModel model, Window owner, GuidedAgentClient? client = null)
	{
		GuidedAssistantPanel guidedAssistantPanel = this;
		vm = model;
		this.owner = owner;
		this.client = client ?? new GuidedAgentClient();
		store = new ConversationStore(vm.DataDirectory);
		try
		{
			conversation = store.Load();
			foreach (GeneratedBatchReceipt batch in conversation.Batches)
			{
				GeneratedBatchService.Reconcile(vm.State, batch);
			}
		}
		catch (Exception ex)
		{
			conversation = new GuidedConversation();
			readOnly = true;
			status.Text = ex.Message;
		}
		GuidedConversation guidedConversation = conversation;
		if (guidedConversation.Drafts == null)
		{
			Dictionary<string, string> dictionary = (guidedConversation.Drafts = new Dictionary<string, string>());
		}
		DockPanel head = new DockPanel
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 8.0)
		};
		DockPanel.SetDock(head, Dock.Top);
		Children.Add(head);
		Icon("\ue712", "更多", (Button button) =>
		{
			ContextMenu contextMenu = new ContextMenu();
			MenuItem menuItem = new MenuItem
			{
				Header = "智能服务设置"
			};
			menuItem.Click += (object _, RoutedEventArgs _) =>
			{
				if (owner is MainWindow mainWindow)
				{
					mainWindow.OpenSettingsCategory("ai");
				}
			};
			contextMenu.Items.Add(menuItem);
			Ui.OpenMenu(contextMenu, button);
		});
		Icon("\ue81c", "会话历史", (Button button) =>
		{
			guidedAssistantPanel.OpenHistory(button);
		});
		Icon("\ue70f", "新对话", (Button _) =>
		{
			guidedAssistantPanel.NewConversation();
		});
		head.Children.Add(Ui.Text("行程助手", 15.0, bold: true));
		branches.Tag = "ConversationBranches";
		ScrollViewer element = new ScrollViewer
		{
			Tag = "ConversationTabs",
			Content = branches,
			HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
			VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
			Margin = new Thickness(0.0, 0.0, 0.0, 8.0)
		};
		DockPanel.SetDock(element, Dock.Top);
		Children.Add(element);
		StackPanel stackPanel = new StackPanel();
		DockPanel.SetDock(stackPanel, Dock.Bottom);
		Children.Add(stackPanel);
		latest.Visibility = Visibility.Collapsed;
		latest.HorizontalAlignment = HorizontalAlignment.Center;
		latest.Click += (object _, RoutedEventArgs _) =>
		{
			guidedAssistantPanel.followLatest = true;
			guidedAssistantPanel.transcript?.ScrollToEnd();
		};
		stackPanel.Children.Add(latest);
		StackPanel stackPanel2 = new StackPanel();
		stackPanel.Children.Add(new Border
		{
			Background = Ui.Brush("#FFFDF9"),
			BorderBrush = Ui.Brush("#DCCDBD"),
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(16.0),
			Padding = new Thickness(8.0),
			Child = stackPanel2
		});
		composeLabel.Visibility = Visibility.Collapsed;
		stackPanel2.Children.Add(composeLabel);
		input.AcceptsReturn = true;
		input.TextWrapping = TextWrapping.Wrap;
		input.MinHeight = 58.0;
		input.MaxHeight = 150.0;
		input.MaxLength = 8000;
		input.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
		input.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
		input.Background = Brushes.Transparent;
		input.BorderThickness = new Thickness(0.0);
		input.Tag = "ConversationInput";
		input.ToolTip = "告诉我你的安排 · Ctrl+Enter发送，Enter换行";
		stackPanel2.Children.Add(input);
		StackPanel stackPanel3 = new StackPanel();
		stackPanel2.Children.Add(stackPanel3);
		WrapPanel wrapPanel = new WrapPanel
		{
			HorizontalAlignment = HorizontalAlignment.Right
		};
		editCancel = Ui.Button("取消编辑", () =>
		{
			guidedAssistantPanel.editId = null;
			guidedAssistantPanel.input.Text = guidedAssistantPanel.previousDraft;
			guidedAssistantPanel.Render(scroll: false);
		});
		wrapPanel.Children.Add(editCancel);
		retry = Ui.Button("重试", () =>
		{
			guidedAssistantPanel.Run(guidedAssistantPanel.RequestAsync);
		});
		wrapPanel.Children.Add(retry);
		cancel = Ui.Button("■", CancelRequest);
		cancel.ToolTip = "停止回答";
		wrapPanel.Children.Add(cancel);
		send = Ui.Button("↑", () =>
		{
			guidedAssistantPanel.Run(guidedAssistantPanel.SendInput);
		}, primary: true);
		send.Tag = "ConversationSend";
		send.ToolTip = "发送 · Ctrl+Enter";
		send.Padding = new Thickness(12.0, 5.0, 12.0, 5.0);
		wrapPanel.Children.Add(send);
		modelLabel.Cursor = Cursors.Hand;
		modelLabel.VerticalAlignment = VerticalAlignment.Center;
		modelLabel.Tag = "AssistantModelLabel";
		modelLabel.TextTrimming = TextTrimming.None;
		modelLabel.TextWrapping = TextWrapping.Wrap;
		modelLabel.ToolTip = "在设置中配置智能服务";
		modelLabel.MouseLeftButtonUp += (object _, MouseButtonEventArgs _) =>
		{
			if (owner is MainWindow mainWindow)
			{
				mainWindow.OpenSettingsCategory("ai");
			}
		};
		stackPanel3.Children.Add(modelLabel);
		stackPanel3.Children.Add(wrapPanel);
		StackPanel stackPanel4 = new StackPanel
		{
			Children = { (UIElement)log }
		};
		working.Tag = "ConversationWorking";
		stackPanel4.Children.Add(working);
		StackPanel child = new StackPanel
		{
			Children = 
			{
				(UIElement)Ui.Text("本轮未完成", 12.0, bold: true),
				(UIElement)errorText
			}
		};
		errorBubble.Child = child;
		errorBubble.Padding = new Thickness(12.0);
		errorBubble.CornerRadius = new CornerRadius(12.0);
		errorBubble.Background = AppTheme.Brush("Surface");
		errorBubble.BorderBrush = AppTheme.Brush("Border");
		errorBubble.BorderThickness = new Thickness(1.0);
		errorBubble.Margin = new Thickness(0.0, 0.0, 16.0, 12.0);
		stackPanel4.Children.Add(errorBubble);
		Expander element2 = new Expander
		{
			Header = "当前状态",
			Content = status,
			Margin = new Thickness(0.0, 5.0, 0.0, 8.0),
			IsExpanded = true
		};
		stackPanel4.Children.Add(element2);
		stackPanel4.Children.Add(receipts);
		transcript = new ScrollViewer
		{
			Tag = "ConversationTranscript",
			Content = stackPanel4,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
			HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
		};
		Children.Add(transcript);
		transcript.ScrollChanged += (object _, ScrollChangedEventArgs e) =>
		{
			if (e.ExtentHeightChange == 0.0 && e.VerticalChange != 0.0)
			{
				guidedAssistantPanel.followLatest = guidedAssistantPanel.transcript.VerticalOffset >= guidedAssistantPanel.transcript.ScrollableHeight - 24.0;
			}
			guidedAssistantPanel.latest.Visibility = (guidedAssistantPanel.followLatest ? Visibility.Collapsed : Visibility.Visible);
		};
		input.PreviewKeyDown += (object _, KeyEventArgs e) =>
		{
			if (e.Key == Key.Return && Keyboard.Modifiers == ModifierKeys.Control)
			{
				guidedAssistantPanel.Run(guidedAssistantPanel.SendInput);
				e.Handled = true;
			}
		};
		draftTimer.Tick += (object? _, EventArgs _) =>
		{
			guidedAssistantPanel.draftTimer.Stop();
			Ui.Safe(guidedAssistantPanel.SaveDraft);
		};
		input.TextChanged += (object _, TextChangedEventArgs _) =>
		{
			if (!guidedAssistantPanel.rendering)
			{
				guidedAssistantPanel.draftTimer.Stop();
				if (guidedAssistantPanel.editId == null)
				{
					guidedAssistantPanel.draftTimer.Start();
				}
				guidedAssistantPanel.SetBusy();
			}
		};
		input.Text = conversation.Drafts.GetValueOrDefault(conversation.ActiveBranchId, "");
		Render(scroll: false);
		Button Icon(string glyph, string title, Action<Button> action)
		{
			Button button = null;
			button = ShellIcons.Button(glyph, title, () =>
			{
				Ui.Safe(() =>
				{
					action(button);
				});
			}, "Assistant:" + title);
			Button button2 = button;
			double width = (button.Height = 36.0);
			button2.Width = width;
			Button button3 = button;
			width = (button.MinHeight = 36.0);
			button3.MinWidth = width;
			button.Padding = new Thickness(4.0);
			DockPanel.SetDock(button, Dock.Right);
			head.Children.Add(button);
			return button;
		}
	}

	public void RefreshPreferences()
	{
		if (!disposed)
		{
			string assistantPersona = new SettingsStore(vm.DataDirectory).Load().AssistantPersona;
			if (!(assistantPersona == currentPersona))
			{
				currentPersona = assistantPersona;
				Render(scroll: false);
			}
		}
	}

	private void SaveDraft()
	{
		draftTimer.Stop();
		if (readOnly || editId != null || !conversation.HasOpenConversation)
		{
			return;
		}
		string id = conversation.ActiveBranchId;
		string text = input.Text;
		if (conversation.Drafts.GetValueOrDefault(id, "") != text)
		{
			Persist(() =>
			{
				conversation.Drafts[id] = text;
			});
		}
	}

	private void OpenHistory(FrameworkElement anchor)
	{
		SaveDraft();
		StackPanel stackPanel = new StackPanel
		{
			Width = 260.0
		};
		TextBox query = Ui.Input();
		query.ToolTip = "搜索会话";
		stackPanel.Children.Add(query);
		StackPanel list = new StackPanel();
		stackPanel.Children.Add(new ScrollViewer
		{
			Content = list,
			MaxHeight = 300.0,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto
		});
		Popup popup = StyledDateEntry.MakePopup(anchor, stackPanel);
		query.TextChanged += (object _, TextChangedEventArgs _) =>
		{
			RefreshHistory();
		};
		RefreshHistory();
		popup.IsOpen = true;
		query.Focus();
		void RefreshHistory()
		{
			list.Children.Clear();
			foreach (ConversationBranch branch in conversation.Branches.Where((ConversationBranch b) => (b.Name + " " + string.Join(" ", b.Messages.Select((ConversationMessage m) => m.Content))).Contains(query.Text, StringComparison.OrdinalIgnoreCase)).Reverse())
			{
				Button button = Ui.Button(branch.Name, () =>
				{
					SelectBranch(branch.Id);
					popup.IsOpen = false;
				});
				button.HorizontalContentAlignment = HorizontalAlignment.Left;
				button.Background = ((branch.Id == conversation.ActiveBranchId) ? Ui.Brush("#E5E1DC") : Brushes.Transparent);
				list.Children.Add(button);
			}
		}
	}

	private void ShowFailure(string message)
	{
		status.Text = "本轮未完成，消息和已有行程均保留。";
		errorText.Text = message;
		errorBubble.Visibility = Visibility.Visible;
		if (followLatest)
		{
			Dispatcher.BeginInvoke((Action)(() =>
			{
				transcript.ScrollToEnd();
			}), DispatcherPriority.Loaded);
		}
	}

	private async void Run(Func<Task> action)
	{
		try
		{
			await action();
		}
		catch (Exception ex)
		{
			ShowFailure(ex.Message);
		}
	}

	private void Persist(Action change)
	{
		if (readOnly)
		{
			throw new InvalidOperationException("对话记录暂不可写，原记录已保留。请先恢复对话文件。");
		}
		GuidedConversation guidedConversation = JsonKit.Clone(conversation);
		try
		{
			change();
			store.Save(conversation);
		}
		catch
		{
			conversation = guidedConversation;
			throw;
		}
	}

	private async Task SendInput()
	{
		string text = input.Text;
		if (string.IsNullOrWhiteSpace(text) || pending != null || readOnly || !conversation.HasOpenConversation)
		{
			return;
		}
		followLatest = true;
		string id = editId;
		if (id != null)
		{
			CancelRequest();
			Persist(() =>
			{
				conversation.ForkAt(id, text);
			});
			editId = null;
			input.Text = previousDraft;
			Render();
			await RequestAsync();
		}
		else
		{
			Persist(() =>
			{
				conversation.AddUser(text);
				conversation.Drafts[conversation.ActiveBranchId] = "";
			});
			input.Text = "";
			Render();
			await RequestAsync();
		}
	}

	public async Task SendAsync(string text)
	{
		if (pending != null)
		{
			throw new InvalidOperationException("正在回答，可取消后继续。");
		}
		Persist(() =>
		{
			conversation.AddUser(text);
		});
		Render();
		await RequestAsync();
	}

	public async Task ConfirmPlaceAsync(string candidateId)
	{
		if (pending == null && !disposed && !readOnly)
		{
			Persist(() =>
			{
				conversation.ConfirmPlace(candidateId);
			});
			Render();
			await RequestAsync();
		}
	}

	public async Task EditMessageAsync(string messageId, string replacement)
	{
		SaveDraft();
		CancelRequest();
		Persist(() =>
		{
			conversation.ForkAt(messageId, replacement);
		});
		editId = null;
		Render();
		await RequestAsync();
	}

	public void SelectBranch(string id)
	{
		if (!(id == conversation.ActiveBranchId) || conversation.Active.Closed)
		{
			if (!conversation.Branches.Any((ConversationBranch b) => b.Id == id))
			{
				throw new ArgumentException("对话分支不存在。");
			}
			SaveDraft();
			Persist(() =>
			{
				conversation.OpenBranch(id);
			});
			editId = null;
			input.Text = conversation.Drafts.GetValueOrDefault(id, "");
			followLatest = true;
			Render();
		}
	}

	private void CancelRequest()
	{
		requests.Cancel(conversation.ActiveBranchId);
		if (!disposed)
		{
			RefreshRequestUi();
			SetBusy();
		}
	}

	private void RefreshRequestUi()
	{
		ConversationRequest conversationRequest = requests.Get(conversation.ActiveBranchId);
		status.Text = conversationRequest?.Status ?? "";
		errorText.Text = conversationRequest?.Failure ?? "";
		errorBubble.Visibility = (string.IsNullOrEmpty(conversationRequest?.Failure) ? Visibility.Collapsed : Visibility.Visible);
		working.Text = conversationRequest?.Status ?? "正在回复…";
	}

	private async Task RequestAsync()
	{
		if (disposed || pending != null || conversation.Active.Closed || conversation.Active.Messages.LastOrDefault()?.Role != "user")
		{
			return;
		}
		string branch = conversation.ActiveBranchId;
		long version = conversation.Active.Version;
		List<ConversationMessage> messages = conversation.Active.Messages.Select(JsonKit.Clone).ToList();
		AgentMemory original = JsonKit.Clone(conversation.Active.Memory);
		PlannerState state = vm.State.Copy();
		ConversationRequest job = requests.Start(branch, version);
		using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(job.Cancellation.Token);
		deadline.CancelAfter(TimeSpan.FromSeconds(120L));
		RefreshRequestUi();
		SetBusy();
		try
		{
			ServiceSettings config = new SettingsStore(vm.DataDirectory).Load();
			Progress<string> progress = new Progress<string>((string text2) =>
			{
				if (!disposed && requests.Owns(job) && job.Running)
				{
					job.Status = text2;
					if (conversation.ActiveBranchId == branch)
					{
						RefreshRequestUi();
						SetBusy();
					}
				}
			});
			GuidedReply reply = await client.ReplyWithToolsAsync(config, messages, original, state, progress, deadline.Token);
			deadline.Token.ThrowIfCancellationRequested();
			if (!disposed && requests.Owns(job))
			{
				bool added = false;
				Persist(() =>
				{
					added = conversation.TryAddAssistant(branch, version, reply);
				});
				ConversationRequest conversationRequest = job;
				string text;
				if (added)
				{
					text = (reply.Ready ? "需求已汇总，核对后可生成行程草稿。" : "可以继续自由补充，我会记住本分支已说明的信息。");
				}
				else
				{
					text = "对话已更新，本轮旧结果未写入。";
				}
				conversationRequest.Status = text;
			}
		}
		catch (OperationCanceledException)
		{
			if (!disposed && requests.Owns(job))
			{
				job.Status = (job.Cancellation.IsCancellationRequested ? "已取消回答，行程未改变。" : "本轮未完成，消息和已有行程均保留。");
				if (!job.Cancellation.IsCancellationRequested)
				{
					job.Failure = "模型连接失败：请求超时。";
				}
			}
		}
		catch (Exception ex2)
		{
			if (!disposed && requests.Owns(job))
			{
				job.Status = "本轮未完成，消息和已有行程均保留。";
				job.Failure = ex2.Message;
			}
		}
		finally
		{
			requests.Finish(job);
			if (!disposed)
			{
				if (conversation.ActiveBranchId == branch)
				{
					Render(scroll: false);
				}
				else
				{
					RenderTabs();
				}
			}
		}
	}

	private void SetBusy()
	{
		input.IsEnabled = conversation.HasOpenConversation && !readOnly;
		working.Visibility = ((pending == null) ? Visibility.Collapsed : Visibility.Visible);
		send.IsEnabled = conversation.HasOpenConversation && !readOnly && pending == null && !string.IsNullOrWhiteSpace(input.Text);
		send.Visibility = ((pending != null) ? Visibility.Collapsed : Visibility.Visible);
		retry.Visibility = ((conversation.Active.Closed || pending != null || !(conversation.Active.Messages.LastOrDefault()?.Role == "user")) ? Visibility.Collapsed : Visibility.Visible);
		retry.IsEnabled = !readOnly;
		cancel.Visibility = ((pending == null) ? Visibility.Collapsed : Visibility.Visible);
		editCancel.Visibility = ((editId == null) ? Visibility.Collapsed : Visibility.Visible);
		composeLabel.Visibility = ((editId == null) ? Visibility.Collapsed : Visibility.Visible);
		composeLabel.Text = "编辑旧消息 · 发送后建立新分支";
	}

	private void Render(bool scroll = true)
	{
		RefreshRequestUi();
		RenderTabs();
		if (!conversation.HasOpenConversation)
		{
			log.Children.Clear();
			log.Children.Add(Ui.Text("新建会话，开始规划行程", 15.0, bold: true));
			log.Children.Add(Ui.Button("＋ 新建会话", NewConversation, primary: true));
			receipts.Children.Clear();
			errorBubble.Visibility = Visibility.Collapsed;
			status.Text = "";
			input.Text = "";
			SetBusy();
			return;
		}
		ServiceSettings serviceSettings = new SettingsStore(vm.DataDirectory).Load();
		currentPersona = serviceSettings.AssistantPersona;
		hint.Text = (GuidedAgentClient.IsLocal(serviceSettings) ? "本地规则引导 · 多项复杂规划可在设置连接模型" : "引导对话 · 使用当前分支记忆、地点与相关日期行程查询结果");
		modelLabel.Text = (GuidedAgentClient.IsLocal(serviceSettings) ? "本地规则 · 先核对再保存" : ((string.IsNullOrWhiteSpace(serviceSettings.Model) ? serviceSettings.AgentProvider : serviceSettings.Model) + " · 先核对再保存"));
		TextBlock textBlock = modelLabel;
		textBlock.Text = textBlock.Text + " · " + AssistantPersonas.Name(serviceSettings.AssistantPersona);
		modelLabel.ToolTip = hint.Text;
		log.Children.Clear();
		if (conversation.Active.Messages.Count == 0)
		{
			Bubble("助手", AssistantPersonas.Greeting(serviceSettings.AssistantPersona), null);
			string[] array = new string[2] { "明天在南京博物院参观，约2小时", "周末想去南京大学，先帮我确认校区" };
			foreach (string text in array)
			{
				string example = text;
				Button button = Ui.Button(example, () =>
				{
					input.Text = example;
					input.Focus();
				});
				button.Content = new TextBlock
				{
					Text = example,
					TextWrapping = TextWrapping.Wrap
				};
				button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
				log.Children.Add(button);
			}
		}
		foreach (ConversationMessage message in conversation.Active.Messages)
		{
			Bubble((message.Role == "user") ? "你" : "助手", message.Content, (message.Role == "user") ? message.Id : null);
		}
		GuidedReply reply = conversation.Active.Reply;
		if (reply != null)
		{
			List<DraftChange> changes = reply.Changes;
			if (changes != null && changes.Count > 0)
			{
				StackPanel stackPanel = new StackPanel();
				stackPanel.Children.Add(Ui.Text("本次方案改动", 13.0, bold: true));
				foreach (DraftChange change in reply.Changes)
				{
					stackPanel.Children.Add(Ui.Text(change.Title + " · " + change.Detail, 12.0));
				}
				log.Children.Add(Ui.Card(stackPanel));
			}
		}
		GuidedReply reply2 = conversation.Active.Reply;
		if (reply2 != null)
		{
			string draftIssue = reply2.DraftIssue;
			if (draftIssue != null && draftIssue.Length > 0)
			{
				TextBlock textBlock2 = Ui.Text(reply2.DraftIssue, 12.0);
				textBlock2.Tag = "ConversationDraftIssue";
				log.Children.Add(Ui.Card(textBlock2));
			}
		}
		GuidedReply reply3 = conversation.Active.Reply;
		if (reply3 != null && reply3.Ready)
		{
			StackPanel stackPanel2 = new StackPanel();
			stackPanel2.Children.Add(Ui.Text("行程需求汇总", 15.0, bold: true));
			stackPanel2.Children.Add(Ui.Text((reply3.Summary.Length > 0) ? reply3.Summary : string.Join("\n", reply3.Events.Select((TripRequest e) => $"{e.Day:yyyy-MM-dd} · {e.Title} · {e.Location} · {e.Duration}分钟")), 12.0));
			bool flag = conversation.Batches.Any((GeneratedBatchReceipt r) =>
			{
				bool flag2 = r.BranchId == conversation.ActiveBranchId && r.SourceVersion == conversation.Active.Version;
				if (flag2)
				{
					string text2 = r.Status;
					bool flag3 = ((text2 == "APPLIED" || text2 == "PENDING") ? true : false);
					flag2 = flag3;
				}
				return flag2;
			});
			Button button2 = Ui.Button(flag ? "此方案已生成" : "生成行程草稿", () =>
			{
				OpenReview();
			}, primary: true);
			button2.Tag = "ConversationGenerate";
			button2.IsEnabled = !flag && !readOnly;
			stackPanel2.Children.Add(button2);
			log.Children.Add(Ui.Card(stackPanel2));
		}
		foreach (string item in conversation.Active.Reply?.ToolErrors ?? new List<string>())
		{
			TextBlock textBlock3 = Ui.Text(item, 12.0);
			textBlock3.Tag = "MapServiceError";
			textBlock3.Foreground = AppTheme.Brush("Danger");
			log.Children.Add(Ui.Card(textBlock3));
		}
		RenderAgentContext();
		RenderReceipts();
		SetBusy();
		double offset = transcript.VerticalOffset;
		if (!scroll)
		{
			return;
		}
		Dispatcher.BeginInvoke((Action)(() =>
		{
			if (followLatest)
			{
				transcript.ScrollToEnd();
			}
			else
			{
				transcript.ScrollToVerticalOffset(offset);
			}
		}), DispatcherPriority.Loaded);
	}

	private void NewConversation()
	{
		SaveDraft();
		Persist(() =>
		{
			conversation.NewBranch();
		});
		editId = null;
		input.Text = "";
		followLatest = true;
		Render();
		input.Focus();
	}

	public void CloseConversation(string id)
	{
		CloseConversations(id, "CURRENT");
	}

	public void CloseConversations(string id, string mode)
	{
		List<string> list = conversation.CloseTargets(id, mode);
		if (list.Count == 0)
		{
			return;
		}
		bool flag = list.Contains(conversation.ActiveBranchId);
		if (flag)
		{
			if (editId != null)
			{
				Persist(() =>
				{
					conversation.Drafts[conversation.ActiveBranchId] = input.Text;
				});
				editId = null;
			}
			else
			{
				SaveDraft();
			}
			CancelRequest();
		}
		foreach (string item in list)
		{
			requests.Cancel(item);
		}
		Persist(() =>
		{
			conversation.CloseMany(id, mode);
		});
		if (flag)
		{
			input.Text = (conversation.HasOpenConversation ? conversation.Drafts.GetValueOrDefault(conversation.ActiveBranchId, "") : "");
			Render(scroll: false);
		}
		else
		{
			RenderTabs();
		}
	}

	private void RenameTab(string id, TextBlock name)
	{
		Ui.InlineRename(name, (string value) =>
		{
			Persist(() =>
			{
				conversation.RenameBranch(id, value);
			});
		}, () =>
		{
			RenderTabs();
		});
	}

	private void RenderTabs()
	{
		rendering = true;
		try
		{
			branches.Children.Clear();
			foreach (ConversationBranch item in conversation.Branches.Where((ConversationBranch b) => !b.Closed))
			{
				string id = item.Id;
				TextBlock name = Ui.Text(item.Name, 12.0);
				name.Margin = new Thickness(0.0);
				name.MaxWidth = 150.0;
				name.TextWrapping = TextWrapping.NoWrap;
				name.TextTrimming = TextTrimming.CharacterEllipsis;
				Button button = Ui.Button("", () =>
				{
					SelectBranch(id);
				});
				button.Content = name;
				button.Tag = "ConversationTab:" + id;
				string name2 = item.Name;
				ConversationRequest conversationRequest = requests.Get(id);
				button.ToolTip = name2 + ((conversationRequest != null && conversationRequest.Running) ? " · 正在回答" : "");
				button.Background = ((id == conversation.ActiveBranchId) ? AppTheme.Brush("Selected") : Brushes.Transparent);
				Border element = Ui.Tab(button, () =>
				{
					CloseConversation(id);
				}, "CloseConversation:" + id);
				branches.Children.Add(element);
				button.PreviewMouseLeftButtonDown += (object _, MouseButtonEventArgs e) =>
				{
					if (e.ClickCount == 2)
					{
						e.Handled = true;
						RenameTab(id, name);
					}
				};
				ContextMenu contextMenu = new ContextMenu();
				MenuItem menuItem = new MenuItem
				{
					Header = "重命名"
				};
				menuItem.Click += (object _, RoutedEventArgs _) =>
				{
					RenameTab(id, name);
				};
				contextMenu.Items.Add(menuItem);
				Ui.AddTabClosing(contextMenu, (string mode) =>
				{
					CloseConversations(id, mode);
				}, (string mode) => conversation.CloseTargets(id, mode).Count > 0);
				Ui.AttachMenu(element, contextMenu);
			}
			Button button2 = Ui.Button("＋", NewConversation);
			button2.Tag = "NewConversationTab";
			button2.ToolTip = "新建会话";
			button2.Width = 28.0;
			button2.Padding = new Thickness(2.0);
			button2.Margin = new Thickness(0.0);
			button2.Background = Brushes.Transparent;
			branches.Children.Add(button2);
		}
		finally
		{
			rendering = false;
		}
	}

	private void RenderAgentContext()
	{
		string branch = conversation.ActiveBranchId;
		long version = conversation.Active.Version;
		AgentMemory memory = conversation.Active.Memory;
		if (memory.DraftHistory.Count > 0)
		{
			Button button = Ui.Button("恢复上一版草稿", () =>
			{
				Run(RestoreDraftAsync);
			});
			button.Tag = "AssistantRestoreDraft";
			button.IsEnabled = pending == null && !readOnly && editId == null;
			log.Children.Add(button);
		}
		foreach (PlaceCandidates candidate in memory.Candidates)
		{
			StackPanel stackPanel = new StackPanel();
			stackPanel.Children.Add(Ui.Text(((candidate.Places.Count > 0) ? "选择地点 · " : "地点查询 · ") + candidate.Query, 13.0, bold: true));
			stackPanel.Children.Add(Ui.Text((string.IsNullOrEmpty(candidate.Source) ? "地图" : candidate.Source) + "查询成功 · " + ((candidate.Places.Count > 0) ? $"返回 {candidate.Places.Count} 个候选" : "未找到匹配地点") + $" · {candidate.QueryAt:HH:mm}", 11.0));
			foreach (Place place in candidate.Places)
			{
				string id = place.Id;
				Button button2 = Ui.Button("", () =>
				{
					if (conversation.ActiveBranchId == branch && conversation.Active.Version == version)
					{
						Run(() => ConfirmPlaceAsync(id));
					}
				});
				button2.Tag = "AgentPlace:" + id;
				button2.HorizontalContentAlignment = HorizontalAlignment.Stretch;
				button2.IsEnabled = pending == null;
				button2.Content = new TextBlock
				{
					Text = place?.ToString() + "\n" + place.Source,
					TextWrapping = TextWrapping.Wrap
				};
				stackPanel.Children.Add(button2);
			}
			log.Children.Add(Ui.Card(stackPanel));
		}
		if (memory.Facts.Count + memory.SelectedPlaces.Count + memory.Routes.Count + memory.Missing.Count <= 0)
		{
			return;
		}
		StackPanel stackPanel2 = new StackPanel();
		foreach (KeyValuePair<string, MemoryFact> fact in memory.Facts)
		{
			stackPanel2.Children.Add(Ui.Text((fact.Value.Confirmed ? "已确认 · " : "建议 · ") + fact.Key + "：" + fact.Value.Value + "\n依据：“" + fact.Value.Evidence + "”", 11.0));
		}
		foreach (Place value3 in memory.SelectedPlaces.Values)
		{
			stackPanel2.Children.Add(Ui.Text("已选地点 · " + value3?.ToString() + "\n" + value3.Source, 11.0));
		}
		foreach (AgentRoute route in memory.Routes)
		{
			string value = memory.SelectedPlaces.GetValueOrDefault(route.FromRef)?.Name ?? "起点";
			string value2 = memory.SelectedPlaces.GetValueOrDefault(route.ToRef)?.Name ?? "终点";
			UIElementCollection children = stackPanel2.Children;
			string text = $"{value} → {value2} · {route.Mode} · {route.Minutes}分钟\n{route.DepartAt:MM-dd HH:mm}出发 · {route.Source}\n查询于{route.QueryAt:MM-dd HH:mm} · ";
			int? knownCostFen = route.KnownCostFen;
			string text2;
			if (knownCostFen.HasValue)
			{
				int valueOrDefault = knownCostFen.GetValueOrDefault();
				text2 = $"已知费用¥{(double)valueOrDefault / 100.0:0.##}" + (route.CostComplete ? "" : "，其余未知");
			}
			else
			{
				text2 = "费用未知";
			}
			children.Add(Ui.Text(text + text2 + " · 拥挤度未知", 11.0));
		}
		if (memory.Missing.Count > 0)
		{
			stackPanel2.Children.Add(Ui.Text("待确认：" + string.Join("、", memory.Missing), 11.0));
		}
		log.Children.Add(new Expander
		{
			Header = "已记住的行程信息",
			Tag = "AgentMemory",
			Content = stackPanel2,
			Margin = new Thickness(0.0, 0.0, 0.0, 10.0)
		});
	}

	private async Task RestoreDraftAsync()
	{
		if (pending != null || readOnly || editId != null)
		{
			return;
		}
		string unsent = input.Text;
		string branch = conversation.ActiveBranchId;
		input.Text = "恢复上一版草稿，并重新核对已有日程与路线。";
		try
		{
			await SendInput();
		}
		finally
		{
			if (!disposed && conversation.ActiveBranchId == branch && string.IsNullOrEmpty(input.Text))
			{
				input.Text = unsent;
				SaveDraft();
			}
			else if (!readOnly && !string.IsNullOrEmpty(unsent) && string.IsNullOrEmpty(conversation.Drafts.GetValueOrDefault(branch)))
			{
				Persist(() =>
				{
					conversation.Drafts[branch] = unsent;
				});
			}
		}
	}

	private void Bubble(string who, string text, string? id)
	{
		StackPanel stackPanel = new StackPanel();
		stackPanel.Children.Add(Ui.Text(who, 10.0));
		TextBox textBox = new TextBox
		{
			Text = text,
			IsReadOnly = true,
			TextWrapping = TextWrapping.Wrap,
			BorderThickness = new Thickness(0.0),
			Background = Brushes.Transparent,
			Padding = new Thickness(0.0),
			Margin = new Thickness(0.0),
			HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
			IsReadOnlyCaretVisible = false
		};
		Typography.Set(textBox, 13.0);
		stackPanel.Children.Add(textBox);
		StackPanel actions = new StackPanel
		{
			Orientation = Orientation.Horizontal
		};
		if (id != null)
		{
			Button button = Ui.Button("编辑", () =>
			{
				SaveDraft();
				previousDraft = input.Text;
				editId = id;
				input.Text = conversation.Active.Messages.Single((ConversationMessage m) => m.Id == id).Content;
				SetBusy();
				input.Focus();
			});
			button.Tag = "EditMessage:" + id;
			button.Padding = new Thickness(5.0, 2.0, 5.0, 2.0);
			actions.Children.Add(button);
			stackPanel.Children.Add(actions);
		}
		bool flag = who == "助手";
		Border border = new Border
		{
			Tag = (flag ? "AssistantBubble" : "UserBubble"),
			Child = stackPanel,
			Padding = new Thickness(12.0),
			CornerRadius = new CornerRadius(12.0),
			Margin = new Thickness((!flag) ? 16 : 0, 0.0, flag ? 16 : 0, 12.0),
			Background = AppTheme.Brush(flag ? "Surface" : "Selected"),
			BorderBrush = AppTheme.Brush("Border"),
			BorderThickness = new Thickness(flag ? 1 : 0)
		};
		actions.Opacity = 0.0;
		border.MouseEnter += (object _, MouseEventArgs _) =>
		{
			actions.Opacity = 1.0;
		};
		border.MouseLeave += (object _, MouseEventArgs _) =>
		{
			if (!actions.IsKeyboardFocusWithin)
			{
				actions.Opacity = 0.0;
			}
		};
		actions.GotKeyboardFocus += (object _, KeyboardFocusChangedEventArgs _) =>
		{
			actions.Opacity = 1.0;
		};
		log.Children.Add(border);
	}

	private void RenderReceipts()
	{
		receipts.Children.Clear();
		foreach (GeneratedBatchReceipt receipt in conversation.Batches.Where((GeneratedBatchReceipt r) =>
		{
			bool flag = r.BranchId == conversation.ActiveBranchId;
			if (flag)
			{
				bool flag2;
				switch (r.Status)
				{
				case "APPLIED":
				case "PENDING":
				case "UNDO_PENDING":
				case "CONFLICT":
					flag2 = true;
					break;
				default:
					flag2 = false;
					break;
				}
				flag = flag2;
			}
			return flag;
		}).Reverse())
		{
			receipts.Children.Add(Ui.Text($"{receipt.CreatedAt:MM-dd HH:mm} · 生成 {receipt.Activities.Count} 项" + ((receipt.Status == "CONFLICT") ? " · 需要核对" : ""), 11.0));
			Button button = Ui.Button("撤回此批次", () =>
			{
				UndoBatch(receipt.Id);
			});
			button.IsEnabled = receipt.Status == "APPLIED";
			button.Tag = "UndoBatch:" + receipt.Id;
			receipts.Children.Add(button);
			Button element = Ui.Button("查看行程", () =>
			{
				Activity owned = receipt.Activities.FirstOrDefault();
				if (owned != null)
				{
					DateTime? dateTime = vm.State.Activities.FirstOrDefault((Activity a) => a.Id == owned.Id)?.StartAt;
					if (dateTime.HasValue)
					{
						DateTime valueOrDefault = dateTime.GetValueOrDefault();
						if (owner is MainWindow mainWindow)
						{
							mainWindow.ReturnToBoard();
						}
						vm.FocusDate(DateOnly.FromDateTime(valueOrDefault));
					}
				}
			});
			receipts.Children.Add(element);
		}
	}

	public AgentReviewWindow OpenReview()
	{
		if (review != null)
		{
			review.Activate();
			if (reviewBranch != conversation.ActiveBranchId || reviewVersion != conversation.Active.Version)
			{
				throw new InvalidOperationException("另一个分支的草稿仍在打开，请关闭该草稿后再生成当前方案。");
			}
			return review;
		}
		GuidedReply reply = conversation.Active.Reply;
		if (reply == null || !reply.Ready)
		{
			throw new InvalidOperationException("请先通过对话补齐必要信息。");
		}
		string branch = conversation.ActiveBranchId;
		long version = conversation.Active.Version;
		if (conversation.Batches.Any((GeneratedBatchReceipt r) =>
		{
			bool flag = r.BranchId == branch && r.SourceVersion == version;
			if (flag)
			{
				string text = r.Status;
				bool flag2 = ((text == "APPLIED" || text == "PENDING") ? true : false);
				flag = flag2;
			}
			return flag;
		}))
		{
			throw new InvalidOperationException("此方案已经生成，可先撤回或修改需求后重新推演。");
		}
		string receiptId = null;
		AgentReviewWindow w = new AgentReviewWindow(vm, reply.Events.Select(JsonKit.Clone).ToList(), preferNewTable: true, null, conversation.Active.Memory.SelectedPlaces, conversation.Active.Memory.Routes)
		{
			Owner = owner
		};
		review = w;
		reviewBranch = branch;
		reviewVersion = version;
		w.CanApply = () => !disposed && conversation.ActiveBranchId == branch && conversation.Active.Version == version;
		w.Applying += (PlannerState before, PlannerState after) =>
		{
			Func<bool>? canApply = w.CanApply;
			if (canApply == null || !canApply())
			{
				throw new InvalidOperationException("对话已变化，请重新生成。");
			}
			GeneratedBatchReceipt receipt = GeneratedBatchService.Capture(before, after, branch);
			receipt.SourceVersion = version;
			receipt.Status = "PENDING";
			Persist(() =>
			{
				conversation.Batches.Add(receipt);
			});
			receiptId = receipt.Id;
		};
		w.Applied += () =>
		{
			if (receiptId != null)
			{
				string id = receiptId;
				Persist(() =>
				{
					conversation.Batches.Single((GeneratedBatchReceipt r) => r.Id == id).Status = "APPLIED";
				});
			}
			status.Text = "已生成并保存行程，可按批次撤回。";
			Render(scroll: false);
		};
		w.Closed += (object? _, EventArgs _) =>
		{
			review = null;
			if (receiptId != null)
			{
				string id = receiptId;
				GeneratedBatchReceipt r = conversation.Batches.SingleOrDefault((GeneratedBatchReceipt generatedBatchReceipt) => generatedBatchReceipt.Id == id);
				if (r != null && r.Status == "PENDING")
				{
					try
					{
						Persist(() =>
						{
							GeneratedBatchService.Reconcile(vm.State, r);
						});
					}
					catch (Exception ex)
					{
						status.Text = ex.Message;
					}
				}
			}
			if (!disposed)
			{
				Render(scroll: false);
			}
		};
		w.Show();
		return w;
	}

	public void UndoBatch(string id)
	{
		GeneratedBatchReceipt receipt = conversation.Batches.SingleOrDefault((GeneratedBatchReceipt r) => r.Id == id) ?? throw new ArgumentException("生成批次不存在。");
		PlannerState next = GeneratedBatchService.Undo(vm.State, receipt);
		Persist(() =>
		{
			receipt.Status = "UNDO_PENDING";
		});
		try
		{
			vm.Persist(next);
		}
		catch
		{
			try
			{
				Persist(() =>
				{
					GeneratedBatchService.Reconcile(vm.State, receipt);
				});
			}
			catch
			{
			}
			throw;
		}
		try
		{
			Persist(() =>
			{
				receipt.Status = "UNDONE";
			});
			status.Text = "已撤回对应批次，其他行程保留。";
		}
		catch (Exception ex)
		{
			status.Text = "行程已撤回，批次标记将在重新载入后恢复：" + ex.Message;
		}
		if (!vm.State.Tables.Any((ScheduleTable t) => t.Id == vm.SelectedTableId))
		{
			vm.SelectedTableId = vm.State.Tables.FirstOrDefault()?.Id ?? "";
		}
		Render(scroll: false);
	}

	public void Dispose()
	{
		if (!disposed)
		{
			try
			{
				SaveDraft();
			}
			catch
			{
			}
			draftTimer.Stop();
			disposed = true;
			requests.CancelAll();
			review?.Close();
		}
	}
}
