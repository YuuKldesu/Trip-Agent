using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using Planner.Core;

namespace Planner.Desktop;

public sealed class StyledDateEntry : DatePicker
{
	private Popup? picker;

	public FrameworkElement? PlacementAnchor { get; set; }

	public StyledDateEntry()
	{
		MinHeight = 36.0;
		Padding = new Thickness(0.0);
		FrameworkElementFactory frameworkElementFactory = new FrameworkElementFactory(typeof(Border));
		frameworkElementFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(8.0));
		frameworkElementFactory.SetValue(Border.BackgroundProperty, AppTheme.Brush("Surface"));
		frameworkElementFactory.SetValue(Border.BorderBrushProperty, AppTheme.Brush("Border"));
		frameworkElementFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1.0));
		FrameworkElementFactory frameworkElementFactory2 = new FrameworkElementFactory(typeof(Button));
		frameworkElementFactory2.SetValue(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch);
		frameworkElementFactory2.SetValue(Control.PaddingProperty, new Thickness(10.0, 6.0, 10.0, 6.0));
		frameworkElementFactory2.SetValue(FrameworkElement.MarginProperty, new Thickness(0.0));
		frameworkElementFactory2.SetValue(Control.BackgroundProperty, Brushes.Transparent);
		frameworkElementFactory2.SetValue(FrameworkElement.MinHeightProperty, 34.0);
		FrameworkElementFactory frameworkElementFactory3 = new FrameworkElementFactory(typeof(DockPanel));
		FrameworkElementFactory frameworkElementFactory4 = new FrameworkElementFactory(typeof(Viewbox));
		frameworkElementFactory4.SetValue(DockPanel.DockProperty, Dock.Right);
		frameworkElementFactory4.SetValue(FrameworkElement.WidthProperty, 20.0);
		frameworkElementFactory4.SetValue(FrameworkElement.HeightProperty, 20.0);
		frameworkElementFactory4.SetValue(Viewbox.StretchProperty, Stretch.Uniform);
		frameworkElementFactory4.SetValue(FrameworkElement.MarginProperty, new Thickness(10.0, 0.0, 0.0, 0.0));
		frameworkElementFactory4.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
		FrameworkElementFactory frameworkElementFactory5 = new FrameworkElementFactory(typeof(TextBlock));
		frameworkElementFactory5.SetValue(TextBlock.TextProperty, "\ue787");
		frameworkElementFactory5.SetValue(TextBlock.FontFamilyProperty, new FontFamily("Segoe MDL2 Assets"));
		frameworkElementFactory5.SetValue(TextBlock.FontSizeProperty, 16.0);
		frameworkElementFactory5.SetValue(FrameworkElement.MarginProperty, new Thickness(1.0));
		frameworkElementFactory4.AppendChild(frameworkElementFactory5);
		frameworkElementFactory3.AppendChild(frameworkElementFactory4);
		FrameworkElementFactory frameworkElementFactory6 = new FrameworkElementFactory(typeof(TextBlock));
		frameworkElementFactory6.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
		frameworkElementFactory6.SetBinding(TextBlock.TextProperty, new Binding("SelectedDate")
		{
			Source = this,
			StringFormat = "{0:yyyy/MM/dd}",
			TargetNullValue = "选择日期"
		});
		frameworkElementFactory3.AppendChild(frameworkElementFactory6);
		frameworkElementFactory2.AppendChild(frameworkElementFactory3);
		frameworkElementFactory.AppendChild(frameworkElementFactory2);
		frameworkElementFactory2.AddHandler(ButtonBase.ClickEvent, (RoutedEventHandler)((object _, RoutedEventArgs e) =>
		{
			OpenPicker();
			e.Handled = true;
		}));
		Template = new ControlTemplate(typeof(DatePicker))
		{
			VisualTree = frameworkElementFactory
		};
		Unloaded += (object _, RoutedEventArgs _) =>
		{
			if (picker != null)
			{
				picker.IsOpen = false;
			}
		};
	}

	public void OpenPicker()
	{
		Popup? popup = picker;
		if (popup != null && popup.IsOpen)
		{
			picker.IsOpen = false;
			return;
		}
		Calendar calendar = new Calendar
		{
			SelectedDate = (SelectedDate ?? Clock.Today.ToDateTime(TimeOnly.MinValue)),
			DisplayDate = (SelectedDate ?? Clock.Now),
			Background = Ui.Brush("#FFFDF9"),
			BorderThickness = new Thickness(0.0),
			Foreground = Ui.Brush("#443A32"),
			FirstDayOfWeek = DayOfWeek.Monday
		};
		Style style = new Style(typeof(CalendarDayButton));
		style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
		style.Setters.Add(new Setter(Control.ForegroundProperty, Ui.Brush("#443A32")));
		style.Setters.Add(new Setter(Control.FontSizeProperty, 14.0));
		FrameworkElementFactory frameworkElementFactory = new FrameworkElementFactory(typeof(Border));
		frameworkElementFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(9.0));
		frameworkElementFactory.SetBinding(Border.BackgroundProperty, new Binding("Background")
		{
			RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
		});
		FrameworkElementFactory frameworkElementFactory2 = new FrameworkElementFactory(typeof(ContentPresenter));
		frameworkElementFactory2.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
		frameworkElementFactory2.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
		frameworkElementFactory2.SetValue(FrameworkElement.MarginProperty, new Thickness(5.0));
		frameworkElementFactory.AppendChild(frameworkElementFactory2);
		style.Setters.Add(new Setter(Control.TemplateProperty, new ControlTemplate(typeof(CalendarDayButton))
		{
			VisualTree = frameworkElementFactory
		}));
		Trigger trigger = new Trigger
		{
			Property = CalendarDayButton.IsSelectedProperty,
			Value = true
		};
		trigger.Setters.Add(new Setter(Control.BackgroundProperty, Ui.Brush("#AC5A4C")));
		trigger.Setters.Add(new Setter(Control.ForegroundProperty, AppTheme.Brush("Canvas")));
		style.Triggers.Add(trigger);
		Trigger trigger2 = new Trigger
		{
			Property = CalendarDayButton.IsInactiveProperty,
			Value = true
		};
		trigger2.Setters.Add(new Setter(UIElement.OpacityProperty, 0.45));
		style.Triggers.Add(trigger2);
		calendar.CalendarDayButtonStyle = style;
		StackPanel stackPanel = new StackPanel();
		stackPanel.Children.Add(Ui.Text("选择日期", 15.0, bold: true));
		stackPanel.Children.Add(calendar);
		WrapPanel wrapPanel = new WrapPanel
		{
			HorizontalAlignment = HorizontalAlignment.Right
		};
		stackPanel.Children.Add(wrapPanel);
		wrapPanel.Children.Add(Ui.Button("今天", () =>
		{
			calendar.DisplayDate = Clock.Now;
			calendar.SelectedDate = Clock.Today.ToDateTime(TimeOnly.MinValue);
		}));
		wrapPanel.Children.Add(Ui.Button("清除", () =>
		{
			SelectedDate = null;
			picker.IsOpen = false;
		}));
		wrapPanel.Children.Add(Ui.Button("取消", () =>
		{
			picker.IsOpen = false;
		}));
		wrapPanel.Children.Add(Ui.Button("确定日期", () =>
		{
			SelectedDate = calendar.SelectedDate;
			picker.IsOpen = false;
		}, primary: true));
		picker = MakePopup(PlacementAnchor ?? this, stackPanel);
		picker.IsOpen = true;
	}

	internal static Popup MakePopup(FrameworkElement target, UIElement child)
	{
		return new Popup
		{
			PlacementTarget = target,
			Placement = PlacementMode.Custom,
			CustomPopupPlacementCallback = (Size popup, Size size, Point _) => Ui.PopupPlacements(target, popup, size),
			StaysOpen = false,
			AllowsTransparency = true,
			Child = new Border
			{
				Background = Ui.Brush("#FFFDF9"),
				BorderBrush = Ui.Brush("#E5DBCF"),
				BorderThickness = new Thickness(1.0),
				CornerRadius = new CornerRadius(17.0),
				Padding = new Thickness(14.0),
				MaxWidth = Math.Max(120.0, Math.Min(SystemParameters.WorkArea.Width, Window.GetWindow(target)?.ActualWidth ?? SystemParameters.WorkArea.Width) - 24.0),
				Child = new ScrollViewer
				{
					Content = child,
					VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
					HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
					MaxHeight = Math.Max(120.0, Math.Min(SystemParameters.WorkArea.Height, Window.GetWindow(target)?.ActualHeight ?? SystemParameters.WorkArea.Height) - 64.0)
				}
			}
		};
	}
}
