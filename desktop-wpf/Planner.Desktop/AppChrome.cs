using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;

namespace Planner.Desktop;

public static class AppChrome
{
	private struct NativePoint
	{
		public int X;

		public int Y;
	}

	private struct NativeRect
	{
		public int Left;

		public int Top;

		public int Right;

		public int Bottom;
	}

	private struct MinMaxInfo
	{
		public NativePoint Reserved;

		public NativePoint MaxSize;

		public NativePoint MaxPosition;

		public NativePoint MinTrackSize;

		public NativePoint MaxTrackSize;
	}

	private struct MonitorInfo
	{
		public int Size;

		public NativeRect Monitor;

		public NativeRect Work;

		public uint Flags;
	}

	private static readonly DependencyProperty AttachedProperty = DependencyProperty.RegisterAttached("Attached", typeof(bool), typeof(AppChrome), new PropertyMetadata(false));

	public static void Attach(Window window)
	{
		if ((bool)window.GetValue(AttachedProperty) || !(window.Content is UIElement element))
		{
			return;
		}
		window.SetValue(AttachedProperty, true);
		if (window.ReadLocalValue(Control.BackgroundProperty) == DependencyProperty.UnsetValue)
		{
			window.Background = Ui.Brush("#FBF7F0");
		}
		if (window.ReadLocalValue(Control.ForegroundProperty) == DependencyProperty.UnsetValue)
		{
			window.Foreground = Ui.Brush("#443A32");
		}
		window.FontFamily = new FontFamily("Microsoft YaHei UI");
		Typography.Set(window, 13.0);
		ResizeMode resizeMode = window.ResizeMode;
		bool flag = (uint)(resizeMode - 2) <= 1u;
		bool flag2 = flag;
		window.UseLayoutRounding = true;
		window.SnapsToDevicePixels = true;
		window.WindowStyle = WindowStyle.None;
		WindowChrome.SetWindowChrome(window, new WindowChrome
		{
			CaptionHeight = 38.0,
			ResizeBorderThickness = new Thickness(flag2 ? 6 : 0),
			CornerRadius = new CornerRadius(0.0),
			NonClientFrameEdges = NonClientFrameEdges.None,
			GlassFrameThickness = new Thickness(0.0),
			UseAeroCaptionButtons = false
		});
		window.Content = null;
		Grid grid = new Grid
		{
			Background = window.Background
		};
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(38.0)
		});
		grid.RowDefinitions.Add(new RowDefinition());
		DockPanel dockPanel = new DockPanel
		{
			Tag = "AppCaption",
			Background = Ui.Brush("#EEDFD0"),
			LastChildFill = true
		};
		grid.Children.Add(dockPanel);
		StackPanel actions = new StackPanel
		{
			Orientation = Orientation.Horizontal
		};
		DockPanel.SetDock(actions, Dock.Right);
		dockPanel.Children.Add(actions);
		Action("\ue921", "最小化", "WindowMinimize", () =>
		{
			window.WindowState = WindowState.Minimized;
		});
		Button maximize = Action("\ue922", "最大化或还原", "WindowMaximize", () =>
		{
			window.WindowState = ((window.WindowState != WindowState.Maximized) ? WindowState.Maximized : WindowState.Normal);
		});
		maximize.IsEnabled = flag2;
		Action("\ue8bb", "关闭", "WindowClose", window.Close);
		TextBlock textBlock = new TextBlock
		{
			FontSize = 12.0,
			Margin = new Thickness(15.0, 0.0, 8.0, 0.0),
			VerticalAlignment = VerticalAlignment.Center,
			TextTrimming = TextTrimming.CharacterEllipsis
		};
		textBlock.SetBinding(TextBlock.TextProperty, new Binding("Title")
		{
			Source = window
		});
		dockPanel.Children.Add(textBlock);
		Grid.SetRow(element, 1);
		grid.Children.Add(element);
		Border content = new Border
		{
			Tag = "AppWindowFrame",
			Background = window.Background,
			BorderBrush = Ui.Brush("#A98164"),
			BorderThickness = new Thickness(1.5),
			CornerRadius = new CornerRadius(0.0),
			ClipToBounds = true,
			Child = grid
		};
		window.Content = content;
		window.StateChanged += (object? _, EventArgs _) =>
		{
			maximize.Content = ShellIcons.Glyph((window.WindowState == WindowState.Maximized) ? "\ue923" : "\ue922", 12.0);
		};
		nint handle = new WindowInteropHelper(window).Handle;
		HwndSource source = HwndSource.FromHwnd(handle);
		if (source == null)
		{
			return;
		}
		SyncBackground();
		Brush watched = window.Background;
		EventHandler repaint = (object? _, EventArgs _) =>
		{
			SyncBackground();
		};
		watched.Changed += repaint;
		window.Closed += (object? _, EventArgs _) =>
		{
			watched.Changed -= repaint;
		};
		int value = 2;
		try
		{
			DwmSetWindowAttribute(handle, 33, ref value, 4);
		}
		catch (DllNotFoundException)
		{
		}
		catch (EntryPointNotFoundException)
		{
		}
		source.AddHook(delegate(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
		{
			if (message == 20)
			{
				handled = true;
				return new IntPtr(1);
			}
			return ConstrainMaximize(window, hwnd, message, wParam, lParam, ref handled);
		});
		Button Action(string glyph, string name, string tag, Action run)
		{
			Button button = Ui.Button("", run);
			button.Content = ShellIcons.Glyph(glyph, 12.0);
			button.Width = 44.0;
			button.Height = 36.0;
			button.MinHeight = 36.0;
			button.Padding = new Thickness(0.0);
			button.Margin = new Thickness(0.0);
			button.Background = Brushes.Transparent;
			button.HorizontalContentAlignment = HorizontalAlignment.Center;
			button.ToolTip = name;
			button.Tag = tag;
			AutomationProperties.SetName(button, name);
			WindowChrome.SetIsHitTestVisibleInChrome(button, hitTestVisible: true);
			actions.Children.Add(button);
			return button;
		}
		void SyncBackground()
		{
			if (!source.IsDisposed)
			{
				HwndTarget compositionTarget = source.CompositionTarget;
				if (compositionTarget != null && window.Background is SolidColorBrush solidColorBrush)
				{
					compositionTarget.BackgroundColor = solidColorBrush.Color;
				}
			}
		}
	}

	private static nint ConstrainMaximize(Window window, nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
	{
		if (message != 36)
		{
			return IntPtr.Zero;
		}
		nint num = MonitorFromWindow(hwnd, 2u);
		MonitorInfo info = new MonitorInfo
		{
			Size = Marshal.SizeOf<MonitorInfo>()
		};
		if (num == IntPtr.Zero || !GetMonitorInfo(num, ref info))
		{
			return IntPtr.Zero;
		}
		MinMaxInfo structure = Marshal.PtrToStructure<MinMaxInfo>(lParam);
		Matrix matrix = HwndSource.FromHwnd(hwnd)?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
		structure.MinTrackSize.X = Math.Max(structure.MinTrackSize.X, (int)Math.Ceiling(window.MinWidth * matrix.M11));
		structure.MinTrackSize.Y = Math.Max(structure.MinTrackSize.Y, (int)Math.Ceiling(window.MinHeight * matrix.M22));
		if (double.IsFinite(window.MaxWidth))
		{
			structure.MaxTrackSize.X = Math.Max(structure.MinTrackSize.X, (int)Math.Floor(window.MaxWidth * matrix.M11));
		}
		if (double.IsFinite(window.MaxHeight))
		{
			structure.MaxTrackSize.Y = Math.Max(structure.MinTrackSize.Y, (int)Math.Floor(window.MaxHeight * matrix.M22));
		}
		structure.MaxPosition = new NativePoint
		{
			X = info.Work.Left - info.Monitor.Left,
			Y = info.Work.Top - info.Monitor.Top
		};
		structure.MaxSize = new NativePoint
		{
			X = info.Work.Right - info.Work.Left,
			Y = info.Work.Bottom - info.Work.Top
		};
		Marshal.StructureToPtr(structure, lParam, fDeleteOld: false);
		handled = true;
		return IntPtr.Zero;
	}

	[DllImport("dwmapi.dll")]
	private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);

	[DllImport("user32.dll")]
	private static extern nint MonitorFromWindow(nint hwnd, uint flags);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
}
