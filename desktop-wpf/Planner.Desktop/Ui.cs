using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Planner.Desktop;

public static class Ui
{
	private static readonly ConditionalWeakTable<Control, TextBlock> fieldErrors = new ConditionalWeakTable<Control, TextBlock>();

	private static readonly DependencyProperty RequiredProperty = DependencyProperty.RegisterAttached("Required", typeof(bool), typeof(Ui), new PropertyMetadata(false));

	public static Brush Brush(string hex)
	{
		try
		{
			return AppTheme.Color(hex);
		}
		catch
		{
			return AppTheme.Brush("Muted");
		}
	}

	public static Brush RawBrush(string hex)
	{
		return AppTheme.Raw(hex);
	}

	public static TextBlock Text(string text, double size = 13.0, bool bold = false)
	{
		TextBlock textBlock = new TextBlock
		{
			Text = text,
			FontWeight = (bold ? FontWeights.SemiBold : FontWeights.Normal),
			TextWrapping = TextWrapping.Wrap,
			Margin = new Thickness(0.0, 3.0, 0.0, 7.0)
		};
		Typography.Set(textBlock, size);
		return textBlock;
	}

	public static Button Button(string text, Action action, bool primary = false)
	{
		Button button = new Button
		{
			Content = text
		};
		if (primary)
		{
			button.Background = Brush("#B76F49");
			button.Foreground = AppTheme.Brush("Canvas");
		}
		button.Click += (object _, RoutedEventArgs _) =>
		{
			Safe(action);
		};
		return button;
	}

	public static void Safe(Action action)
	{
		try
		{
			action();
		}
		catch (Exception ex)
		{
			MessageBox.Show(ex.Message, "操作未完成", MessageBoxButton.OK, MessageBoxImage.Exclamation);
		}
	}

	public static bool Confirm(string text)
	{
		return MessageBox.Show(text, "请确认", MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK;
	}

	public static bool ConfirmReview(string text)
	{
		Window w = new Window
		{
			Title = "核对变更与实际影响",
			Width = 720.0,
			Height = 680.0,
			WindowStartupLocation = WindowStartupLocation.CenterScreen
		};
		DockPanel dockPanel = new DockPanel
		{
			Margin = new Thickness(20.0)
		};
		bool accepted = false;
		StackPanel stackPanel = Row(Button("取消", w.Close), Button("确认写入", () =>
		{
			accepted = true;
			w.Close();
		}, primary: true));
		stackPanel.HorizontalAlignment = HorizontalAlignment.Right;
		DockPanel.SetDock(stackPanel, Dock.Bottom);
		dockPanel.Children.Add(stackPanel);
		dockPanel.Children.Add(new ScrollViewer
		{
			Content = Text(text),
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto
		});
		w.Content = dockPanel;
		w.ShowDialog();
		return accepted;
	}

	public static TextBox Input(string value = "")
	{
		return new TextBox
		{
			Text = value
		};
	}

	public static ComboBox Choices(IEnumerable<string> values, int selected = 0)
	{
		ComboBox comboBox = new ComboBox();
		foreach (string value in values)
		{
			comboBox.Items.Add(value);
		}
		comboBox.SelectedIndex = selected;
		return comboBox;
	}

	public static TextBlock FieldLabel(string title, bool required = false)
	{
		title = Regex.Replace(title, "（可选[^）]*）|（可留空）|，可留空", "");
		TextBlock textBlock = Text(title, 12.0, bold: true);
		if (required)
		{
			textBlock.Inlines.Clear();
			textBlock.Inlines.Add(new Run("* ")
			{
				Foreground = Brushes.Firebrick
			});
			textBlock.Inlines.Add(new Run(title));
		}
		return textBlock;
	}

	public static void Field(Panel panel, string title, Control input, bool required = false)
	{
		bool flag = required;
		bool flag2;
		switch (title)
		{
		case "耗时（分钟）":
		case "调整这项安排":
		case "保存到行程表":
		case "添加一项安排":
		case "开始日期":
		case "开始时间":
		case "活动名称":
		case "结束日期":
		case "结束时间":
		case "所属行程表":
		case "昵称":
			flag2 = true;
			break;
		default:
			flag2 = false;
			break;
		}
		required = flag | flag2;
		panel.Children.Add(FieldLabel(title));
		panel.Children.Add(input);
		SetRequired(input, required);
	}

	public static void SetRequired(Control input, bool required)
	{
		input.SetValue(RequiredProperty, required);
		if (!(input.Parent is Panel panel))
		{
			return;
		}
		int num = panel.Children.IndexOf(input);
		if (num > 0 && panel.Children[num - 1] is TextBlock { Text: var text } textBlock)
		{
			string text2 = text.TrimStart(new char[2] { '*', ' ' });
			textBlock.Inlines.Clear();
			if (required)
			{
				textBlock.Inlines.Add(new Run("* ")
				{
					Foreground = Brushes.Firebrick
				});
			}
			textBlock.Inlines.Add(new Run(text2));
		}
		if (required && !fieldErrors.TryGetValue(input, out TextBlock _))
		{
			TextBlock textBlock2 = Text("此项为必填。", 11.0);
			textBlock2.Foreground = Brushes.Firebrick;
			textBlock2.Margin = new Thickness(0.0, 0.0, 0.0, 6.0);
			textBlock2.Visibility = Visibility.Collapsed;
			panel.Children.Insert(num + 1, textBlock2);
			fieldErrors.Add(input, textBlock2);
			input.LostKeyboardFocus += (object _, KeyboardFocusChangedEventArgs _) =>
			{
				if (fieldErrors.TryGetValue(input, out TextBlock value3) && !Empty(input))
				{
					value3.Visibility = Visibility.Collapsed;
				}
			};
		}
		if (!required && fieldErrors.TryGetValue(input, out TextBlock value2))
		{
			value2.Visibility = Visibility.Collapsed;
		}
	}

	private static bool Empty(Control control)
	{
		if (!(control is TextBox textBox))
		{
			if (!(control is PasswordBox passwordBox))
			{
				if (!(control is ComboBox comboBox))
				{
					if (!(control is StyledDateEntry { SelectedDate: var selectedDate }))
					{
						if (!(control is DatePicker { SelectedDate: var selectedDate2 }))
						{
							if (control is TimeEntry timeEntry)
							{
								return string.IsNullOrWhiteSpace(timeEntry.Text);
							}
							return false;
						}
						return !selectedDate2.HasValue;
					}
					return !selectedDate.HasValue;
				}
				return comboBox.SelectedIndex < 0;
			}
			return passwordBox.Password.Length == 0;
		}
		return string.IsNullOrWhiteSpace(textBox.Text);
	}

	public static bool ValidateRequired(DependencyObject root)
	{
		bool flag = true;
		foreach (DependencyObject item in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
		{
			if (item is Control control && (bool)control.GetValue(RequiredProperty) && control.IsVisible && fieldErrors.TryGetValue(control, out TextBlock value))
			{
				bool flag2 = Empty(control);
				value.Visibility = ((!flag2) ? Visibility.Collapsed : Visibility.Visible);
				if (flag2 & flag)
				{
					control.Focus();
					control.BringIntoView();
					flag = false;
				}
			}
			flag = ValidateRequired(item) & flag;
		}
		return flag;
	}

	public static Border Tab(Button label, Action close, string tag)
	{
		label.Margin = new Thickness(0.0);
		label.Padding = new Thickness(9.0, 5.0, 3.0, 5.0);
		label.MinHeight = 28.0;
		Button x = Button("×", close);
		x.Tag = tag;
		x.ToolTip = "关闭";
		Button button = x;
		double width = (x.Height = 20.0);
		button.Width = width;
		Button button2 = x;
		width = (x.MinHeight = 0.0);
		button2.MinWidth = width;
		x.Padding = new Thickness(0.0);
		x.Margin = new Thickness(2.0, 0.0, 5.0, 0.0);
		x.FontSize = 12.0;
		x.Background = Brushes.Transparent;
		x.Opacity = 0.55;
		x.MouseEnter += (object _, MouseEventArgs _) =>
		{
			x.Opacity = 1.0;
		};
		x.MouseLeave += (object _, MouseEventArgs _) =>
		{
			x.Opacity = 0.55;
		};
		StackPanel stackPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal
		};
		stackPanel.Children.Add(label);
		stackPanel.Children.Add(x);
		Border border = new Border();
		border.Child = stackPanel;
		border.CornerRadius = new CornerRadius(7.0);
		border.Margin = new Thickness(0.0, 0.0, 4.0, 0.0);
		border.SetBinding(Border.BackgroundProperty, new Binding("Background")
		{
			Source = label
		});
		return border;
	}

	public static void OpenMenu(ContextMenu menu, FrameworkElement anchor)
	{
		PlaceMenu(menu, anchor, atMouse: false);
		menu.IsOpen = true;
	}

	public static CustomPopupPlacement[] PopupPlacements(FrameworkElement anchor, Size popup, Size target, Point? mouse = null)
	{
		Point point = mouse ?? new Point(0.0, target.Height + 3.0);
		Window window = Window.GetWindow(anchor);
		if (window != null && window.IsLoaded)
		{
			Point location = anchor.PointFromScreen(window.PointToScreen(default));
			Rect rect = new Rect(location, new Size(window.ActualWidth, window.ActualHeight));
			double value = ((point.Y + popup.Height > rect.Bottom) ? ((mouse?.Y ?? 0.0) - popup.Height - 3.0) : point.Y);
			point = new Point(Math.Clamp(point.X, rect.Left, Math.Max(rect.Left, rect.Right - popup.Width)), Math.Clamp(value, rect.Top, Math.Max(rect.Top, rect.Bottom - popup.Height)));
		}
		return new CustomPopupPlacement[1]
		{
			new CustomPopupPlacement(point, PopupPrimaryAxis.None)
		};
	}

	private static void PlaceMenu(ContextMenu menu, FrameworkElement anchor, bool atMouse)
	{
		menu.PlacementTarget = anchor;
		menu.Placement = PlacementMode.Custom;
		double horizontalOffset = (menu.VerticalOffset = 0.0);
		menu.HorizontalOffset = horizontalOffset;
		Point? mouse = (atMouse ? new Point?(Mouse.GetPosition(anchor)) : ((Point?)null));
		menu.CustomPopupPlacementCallback = (Size popup, Size target, Point _) => PopupPlacements(anchor, popup, target, mouse);
	}

	public static void AttachMenu(FrameworkElement owner, ContextMenu menu)
	{
		owner.ContextMenu = menu;
		owner.ContextMenuOpening += (object _, ContextMenuEventArgs _) =>
		{
			PlaceMenu(menu, owner, atMouse: true);
		};
	}

	public static void AddTabClosing(ContextMenu menu, Action<string> close, Func<string, bool> available)
	{
		(string, string)[] array = new (string, string)[4]
		{
			("CURRENT", "关闭当前标签"),
			("OTHERS", "关闭其他标签"),
			("RIGHT", "关闭右侧标签"),
			("ALL", "关闭全部标签")
		};
		for (int i = 0; i < array.Length; i++)
		{
			(string, string) tuple = array[i];
			string mode = tuple.Item1;
			string item = tuple.Item2;
			MenuItem item2 = new MenuItem
			{
				Header = item,
				Tag = "TabClose:" + mode
			};
			item2.Click += (object _, RoutedEventArgs _) =>
			{
				Safe(() =>
				{
					close(mode);
				});
			};
			menu.Items.Add(item2);
			menu.Opened += (object _, RoutedEventArgs _) =>
			{
				try
				{
					item2.IsEnabled = available(mode);
				}
				catch (ArgumentException)
				{
					item2.IsEnabled = false;
				}
			};
		}
	}

	public static TextBox? InlineRename(TextBlock label, Action<string> save, Action? finished = null)
	{
		if (!label.IsLoaded)
		{
			return null;
		}
		DependencyObject parent = LogicalTreeHelper.GetParent(label);
		FrameworkElement current = label;
		Panel panel = parent as Panel;
		Action<FrameworkElement> replace;
		if (panel != null)
		{
			replace = (FrameworkElement value) =>
			{
				int num = panel.Children.IndexOf(current);
				if (num >= 0)
				{
					panel.Children.RemoveAt(num);
					panel.Children.Insert(num, value);
					current = value;
				}
			};
		}
		else
		{
			ContentControl content = parent as ContentControl;
			if (content == null)
			{
				return null;
			}
			replace = (FrameworkElement value) =>
			{
				if (content.Content == current)
				{
					content.Content = value;
					current = value;
				}
			};
		}
		TextBox editor = new TextBox
		{
			Text = label.Text,
			MaxLength = 60,
			Width = Math.Max(1.0, label.ActualWidth),
			Height = Math.Max(1.0, label.ActualHeight),
			MinWidth = 0.0,
			MinHeight = 0.0,
			Margin = new Thickness(0.0),
			Padding = new Thickness(0.0),
			BorderThickness = new Thickness(0.0),
			Background = Brushes.Transparent,
			Foreground = label.Foreground,
			FontSize = label.FontSize,
			FontFamily = label.FontFamily,
			FontWeight = label.FontWeight,
			VerticalContentAlignment = VerticalAlignment.Center,
			TextWrapping = TextWrapping.NoWrap,
			HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
			VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
			AcceptsReturn = false,
			AcceptsTab = false,
			Tag = "InlineRename"
		};
		bool done = false;
		editor.PreviewKeyDown += (object _, KeyEventArgs e) =>
		{
			Key key = e.Key;
			if ((key == Key.Return || key == Key.Escape) ? true : false)
			{
				e.Handled = true;
				Finish(e.Key == Key.Return);
			}
		};
		editor.LostKeyboardFocus += (object _, KeyboardFocusChangedEventArgs _) =>
		{
			Finish(commit: true);
		};
		replace(editor);
		editor.Dispatcher.BeginInvoke((Action)(() =>
		{
			if (editor.IsLoaded && current == editor)
			{
				editor.Focus();
				editor.SelectAll();
			}
		}), DispatcherPriority.Input);
		return editor;
		void Finish(bool commit)
		{
			if (!done)
			{
				done = true;
				replace(label);
				if (commit && !string.IsNullOrWhiteSpace(editor.Text))
				{
					Safe(() =>
					{
						save(editor.Text.Trim());
					});
				}
				finished?.Invoke();
			}
		}
	}

	public static StackPanel Row(params UIElement[] items)
	{
		StackPanel stackPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal
		};
		foreach (UIElement element in items)
		{
			stackPanel.Children.Add(element);
		}
		return stackPanel;
	}

	public static Border Card(UIElement child, string color = "#FFFDF9")
	{
		return new Border
		{
			Background = Brush(color),
			CornerRadius = new CornerRadius(12.0),
			BorderBrush = Brush("#E5D8CA"),
			BorderThickness = new Thickness(1.0),
			Padding = new Thickness(14.0),
			Margin = new Thickness(0.0, 0.0, 0.0, 12.0),
			Child = child
		};
	}
}
