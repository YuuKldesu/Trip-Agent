using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Planner.Desktop;

public partial class App : Application
{
	public App()
	{
		EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, (RoutedEventHandler)((object sender, RoutedEventArgs e) =>
		{
			if (sender is Window window && e.OriginalSource == window)
			{
				AppChrome.Attach(window);
				if (!window.Resources.Contains("TextSelection"))
				{
					window.Resources["TextSelection"] = new TextSelection(window);
				}
			}
		}));
	}

	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);
		int num = Array.IndexOf(e.Args, "--data-dir");
		string data = ((num >= 0 && num + 1 < e.Args.Length) ? Path.GetFullPath(e.Args[num + 1]) : Path.Combine(AppContext.BaseDirectory, "Data"));
		DispatcherUnhandledException += (object s, DispatcherUnhandledExceptionEventArgs x) =>
		{
			MessageBox.Show(x.Exception.Message, "操作未完成", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			x.Handled = true;
		};
		try
		{
			Directory.CreateDirectory(data);
			PlannerViewModel vm = new PlannerViewModel(data);
			AppTheme.Apply(vm.Appearance.Theme, vm.Appearance.AccentColor);
			MainWindow window = new MainWindow(vm);
			MainWindow = window;
			window.Show();
			if (e.Args.Contains("--smoke"))
			{
				if (vm.State.Activities.Count == 0)
				{
					vm.LoadSample();
				}
				window.Dispatcher.BeginInvoke((Func<Task>)(async () =>
				{
					await Task.Delay(800);
					window.UpdateLayout();
					Capture(window, "ui-smoke.png");
					EditorWindow editor = new EditorWindow(vm, vm.WeekItems().First())
					{
						Owner = window
					};
					editor.Show();
					await Task.Delay(200);
					Capture(editor, "editor-smoke.png");
					editor.Close();
					RouteWindow route = new RouteWindow(vm)
					{
						Owner = window
					};
					route.Show();
					await Task.Delay(200);
					Capture(route, "route-smoke.png");
					route.Close();
					File.WriteAllText(Path.Combine(data, "smoke-result.txt"), $"WPF rendered main/editor/routes; main={window.ActualWidth}x{window.ActualHeight}; activities={vm.State.Activities.Count}; revision={vm.State.Revision}");
					await Task.Delay(300);
					Shutdown();
				}), DispatcherPriority.ApplicationIdle);
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show("无法打开本地数据：" + ex.Message, "启动失败", MessageBoxButton.OK, MessageBoxImage.Hand);
			Shutdown(1);
		}
		void Capture(Window target, string name)
		{
			target.UpdateLayout();
			FrameworkElement frameworkElement = (FrameworkElement)target.Content;
			RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap((int)Math.Ceiling(frameworkElement.ActualWidth), (int)Math.Ceiling(frameworkElement.ActualHeight), 96.0, 96.0, PixelFormats.Pbgra32);
			renderTargetBitmap.Render(frameworkElement);
			PngBitmapEncoder pngBitmapEncoder = new PngBitmapEncoder
			{
				Frames = { BitmapFrame.Create(renderTargetBitmap) }
			};
			using FileStream stream = File.Create(Path.Combine(data, name));
			pngBitmapEncoder.Save(stream);
		}
	}
}
