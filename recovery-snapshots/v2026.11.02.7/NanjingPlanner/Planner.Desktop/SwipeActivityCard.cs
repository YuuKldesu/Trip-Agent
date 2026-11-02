using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Planner.Core;

namespace Planner.Desktop;

public sealed class SwipeActivityCard : Border
{
	private readonly SwipeTracker tracker = new SwipeTracker();

	private readonly TranslateTransform translation = new TranslateTransform();

	private bool tracking;

	private TouchDevice? touch;

	public BoardEntry Entry { get; }

	public Button Star { get; }

	public Button Completion { get; }

	public bool IsCompleted { get; private set; }

	public bool IsImportant { get; private set; }

	public event Action? ToggleRequested;

	public event Action? ImportantRequested;

	public event Action? EditRequested;

	public SwipeActivityCard(BoardEntry entry)
	{
		Entry = entry;
		Tag = entry;
		Focusable = true;
		CornerRadius = new CornerRadius(16.0);
		Background = Ui.Brush("#FFFDF9");
		BorderBrush = Ui.Brush("#E6E1DB");
		BorderThickness = new Thickness(1.0);
		Padding = new Thickness(10.0);
		Margin = new Thickness(0.0, 0.0, 0.0, 8.0);
		RenderTransform = translation;
		Completion = Ui.Button("○", () =>
		{
			ToggleRequested?.Invoke();
		});
		Completion.Background = Brushes.Transparent;
		Completion.Padding = new Thickness(2.0);
		Completion.Margin = new Thickness(0.0, 0.0, 4.0, 0.0);
		Completion.ToolTip = "标记完成";
		Typography.Set(Completion, 16.0);
		Star = Ui.Button("☆", () =>
		{
			ImportantRequested?.Invoke();
		});
		Star.Background = Brushes.Transparent;
		Star.Padding = new Thickness(3.0, 0.0, 3.0, 0.0);
		Star.Margin = new Thickness(2.0, 0.0, 0.0, 0.0);
		Star.MinWidth = 28.0;
		Star.FontFamily = new FontFamily("Segoe UI Symbol");
		Typography.Set(Star, 18.0);
		AutomationProperties.SetName(this, entry.Occurrence.Title);
		Star.ToolTip = "标记重要";
		Unloaded += (object _, RoutedEventArgs _) =>
		{
			CancelSwipe();
		};
	}

	public void SetState(bool completed, bool important, bool animate = false)
	{
		bool flag = IsCompleted != completed;
		IsCompleted = completed;
		IsImportant = important;
		Completion.Content = (completed ? "✓" : "○");
		Completion.ToolTip = (completed ? "恢复未完成" : "标记完成");
		Completion.Foreground = AppTheme.Brush("Accent");
		Star.Content = "★";
		Star.Foreground = Ui.Brush("#A47820");
		Star.Visibility = ((!important) ? Visibility.Collapsed : Visibility.Visible);
		Star.ToolTip = "取消收藏";
		AutomationProperties.SetName(Star, "取消收藏：" + Entry.Occurrence.Title);
		ToolTip = new ToolTip
		{
			MaxWidth = 380.0,
			Content = new TextBlock
			{
				Text = $"{Entry.Occurrence.Title}\n{Entry.Occurrence.Start:yyyy-MM-dd HH:mm} — {Entry.Occurrence.End:MM-dd HH:mm}\n" + (completed ? "已完成" : "待进行") + " · 点击编辑，右键更多操作",
				TextWrapping = TextWrapping.Wrap,
				MaxWidth = 350.0
			}
		};
		if (animate & flag)
		{
			AnimateOpacity();
			return;
		}
		BeginAnimation(UIElement.OpacityProperty, null);
		Opacity = (completed ? 0.55 : 1.0);
	}

	public Point GesturePosition(Point local)
	{
		if (!(Parent is UIElement relativeTo))
		{
			return local;
		}
		return TranslatePoint(local, relativeTo);
	}

	public void BeginSwipe(Point point)
	{
		tracking = true;
		tracker.Begin(point.X, point.Y);
		translation.BeginAnimation(TranslateTransform.XProperty, null);
		translation.X = 0.0;
		BeginAnimation(UIElement.OpacityProperty, null);
		Opacity = (IsCompleted ? 0.55 : 1.0);
	}

	public void ContinueSwipe(Point point)
	{
		if (tracking)
		{
			tracker.Move(point.X, point.Y);
			if (tracker.Horizontal)
			{
				double num = SwipeTracker.Threshold(ActualWidth);
				double num2 = Math.Min(1.0, Math.Max(0.0, 0.0 - tracker.DeltaX) / num);
				translation.X = Math.Clamp(tracker.DeltaX * 0.75, (0.0 - num) * 0.6, num * 1.15);
				Opacity = (IsCompleted ? (0.55 + num2 * 0.45) : (1.0 - num2 * 0.45));
			}
		}
	}

	public void EndSwipe(bool cancelled = false)
	{
		if (tracking)
		{
			tracking = false;
			if (cancelled)
			{
				tracker.Cancel();
			}
			SwipeOutcome swipeOutcome = tracker.End(ActualWidth);
			if (IsMouseCaptured)
			{
				ReleaseMouseCapture();
			}
			TouchDevice touchDevice = touch;
			if (touchDevice != null)
			{
				touch = null;
				ReleaseTouchCapture(touchDevice);
			}
			switch (swipeOutcome)
			{
			case SwipeOutcome.Complete:
				ToggleRequested?.Invoke();
				break;
			case SwipeOutcome.Collect:
				ImportantRequested?.Invoke();
				break;
			case SwipeOutcome.Tap:
				EditRequested?.Invoke();
				break;
			}
			translation.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(220L))
			{
				EasingFunction = new CubicEase
				{
					EasingMode = EasingMode.EaseOut
				}
			});
			AnimateOpacity();
		}
	}

	private void AnimateOpacity()
	{
		BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(IsCompleted ? 0.55 : 1.0, TimeSpan.FromMilliseconds(220L)));
	}

	private void CancelSwipe()
	{
		if (tracking)
		{
			EndSwipe(cancelled: true);
		}
	}

	private bool Interactive(DependencyObject? target)
	{
		while (target != null && target != this)
		{
			if ((target is ButtonBase || target is TextBoxBase || target is Selector) ? true : false)
			{
				return true;
			}
			target = ((target is Visual) ? VisualTreeHelper.GetParent(target) : LogicalTreeHelper.GetParent(target));
		}
		return false;
	}

	protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
	{
		base.OnMouseLeftButtonDown(e);
		if (e.StylusDevice == null && !Interactive(e.OriginalSource as DependencyObject))
		{
			BeginSwipe(GesturePosition(e.GetPosition(this)));
			CaptureMouse();
			e.Handled = true;
		}
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		base.OnMouseMove(e);
		if (tracking && touch == null)
		{
			if (e.LeftButton != MouseButtonState.Pressed)
			{
				CancelSwipe();
				return;
			}
			ContinueSwipe(GesturePosition(e.GetPosition(this)));
			e.Handled = true;
		}
	}

	protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
	{
		base.OnMouseLeftButtonUp(e);
		if (tracking && touch == null)
		{
			EndSwipe();
			e.Handled = true;
		}
	}

	protected override void OnLostMouseCapture(MouseEventArgs e)
	{
		base.OnLostMouseCapture(e);
		if (touch == null)
		{
			CancelSwipe();
		}
	}

	protected override void OnTouchDown(TouchEventArgs e)
	{
		base.OnTouchDown(e);
		if (!Interactive(e.OriginalSource as DependencyObject) && !tracking)
		{
			touch = e.TouchDevice;
			BeginSwipe(GesturePosition(e.GetTouchPoint(this).Position));
		}
	}

	protected override void OnTouchMove(TouchEventArgs e)
	{
		base.OnTouchMove(e);
		if (touch == e.TouchDevice)
		{
			ContinueSwipe(GesturePosition(e.GetTouchPoint(this).Position));
			if (tracker.Horizontal)
			{
				CaptureTouch(e.TouchDevice);
				e.Handled = true;
			}
		}
	}

	protected override void OnTouchUp(TouchEventArgs e)
	{
		base.OnTouchUp(e);
		if (touch == e.TouchDevice)
		{
			EndSwipe();
			e.Handled = true;
		}
	}

	protected override void OnLostTouchCapture(TouchEventArgs e)
	{
		base.OnLostTouchCapture(e);
		if (touch == e.TouchDevice)
		{
			CancelSwipe();
		}
	}

	protected override void OnPreviewKeyDown(KeyEventArgs e)
	{
		if (e.Key == Key.Return && (Keyboard.Modifiers & ModifierKeys.Shift) != 0)
		{
			ToggleRequested?.Invoke();
			e.Handled = true;
		}
		else if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
		{
			ImportantRequested?.Invoke();
			e.Handled = true;
		}
		base.OnPreviewKeyDown(e);
	}
}
