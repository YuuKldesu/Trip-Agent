using System;
using System.Windows;
using System.Windows.Controls;
using Planner.Data;

namespace Planner.Desktop;

public sealed class AppearanceWindow : Window
{
	public AppearanceWindow(PlannerViewModel vm)
	{
		AppearanceWindow appearanceWindow = this;
		Title = "外观与字号";
		Width = 480.0;
		Height = 470.0;
		MinWidth = 360.0;
		MinHeight = 360.0;
		WindowStartupLocation = WindowStartupLocation.CenterOwner;
		Background = Ui.Brush("#FBF7F0");
		DockPanel dockPanel = (DockPanel)(Content = new DockPanel
		{
			Margin = new Thickness(22.0)
		});
		Button element = Ui.Button("完成", Close, primary: true);
		DockPanel.SetDock(element, Dock.Bottom);
		dockPanel.Children.Add(element);
		StackPanel stackPanel = new StackPanel();
		dockPanel.Children.Add(new ScrollViewer
		{
			Content = stackPanel,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto
		});
		stackPanel.Children.Add(Ui.Text("外观与字号", 21.0, bold: true));
		TextBlock value = Ui.Text("", 14.0, bold: true);
		stackPanel.Children.Add(value);
		Slider slider = new Slider
		{
			Minimum = 12.0,
			Maximum = 22.0,
			TickFrequency = 1.0,
			IsSnapToTickEnabled = true,
			Value = vm.Appearance.FontSize,
			SmallChange = 1.0,
			LargeChange = 2.0,
			Margin = new Thickness(0.0, 8.0, 0.0, 18.0)
		};
		stackPanel.Children.Add(slider);
		WrapPanel wrapPanel = new WrapPanel();
		int[] array = new int[4] { 12, 14, 18, 22 };
		for (int i = 0; i < array.Length; i++)
		{
			int num = array[i];
			int selected = num;
			wrapPanel.Children.Add(Ui.Button((num == 14) ? "14 · 标准" : num.ToString(), () =>
			{
				slider.Value = selected;
			}));
		}
		stackPanel.Children.Add(wrapPanel);
		FontLabel();
		slider.ValueChanged += (object _, RoutedPropertyChangedEventArgs<double> e) =>
		{
			double font = Math.Round(e.NewValue);
			if (font != vm.Appearance.FontSize)
			{
				Ui.Safe(() =>
				{
					vm.UpdateAppearance((BoardPreferences p) =>
					{
						p.FontSize = font;
					});
				});
				FontLabel();
			}
		};
		stackPanel.Children.Add(Ui.Text("界面模式", 15.0, bold: true));
		string[] modes = new string[3] { "AUTO", "FULL", "COMPACT" };
		ComboBox mode = Ui.Choices(new _003C_003Ez__ReadOnlyArray<string>(new string[3] { "自动切换", "完整界面", "简洁侧栏" }), Array.IndexOf(modes, vm.Appearance.LayoutMode));
		stackPanel.Children.Add(mode);
		mode.SelectionChanged += (object _, SelectionChangedEventArgs _) =>
		{
			Ui.Safe(() =>
			{
				if (appearanceWindow.Owner is MainWindow mainWindow)
				{
					mainWindow.SetLayoutMode(modes[mode.SelectedIndex]);
				}
				else
				{
					vm.UpdateAppearance((BoardPreferences p) =>
					{
						p.LayoutMode = modes[mode.SelectedIndex];
					});
				}
			});
		};
		stackPanel.Children.Add(Ui.Text("自动模式会结合窗口宽度、纵横比例和字号调整；简洁模式展示当天及随后6天。", 12.0));
		CheckBox pin = new CheckBox
		{
			Content = "简洁模式保持置顶",
			IsChecked = vm.Appearance.AlwaysOnTop
		};
		pin.Click += (object _, RoutedEventArgs _) =>
		{
			Ui.Safe(() =>
			{
				vm.UpdateAppearance((BoardPreferences p) =>
				{
					p.AlwaysOnTop = pin.IsChecked == true;
				});
			});
		};
		stackPanel.Children.Add(pin);
		stackPanel.Children.Add(Ui.Text("星星标记重要；统一右滑切换完成，右键同样可以操作。字号和显示状态保存在本机。", 12.0));
		void FontLabel()
		{
			value.Text = $"正文字号：{vm.Appearance.FontSize:0}（即时预览）";
		}
	}
}
