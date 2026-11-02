using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace Planner.Desktop;

public sealed class TextSelection
{
	private sealed class SelectionAdorner(TextBlock text, TextSelection selection) : Adorner(text)
	{
		protected override void OnRender(DrawingContext drawing)
		{
			IsHitTestVisible = false;
			if (selection.anchor == null || selection.end == null)
			{
				return;
			}
			TextPointer? textPointer = ((selection.anchor.CompareTo(selection.end) <= 0) ? selection.anchor : selection.end);
			TextPointer position = ((selection.anchor.CompareTo(selection.end) <= 0) ? selection.end : selection.anchor);
			SolidColorBrush brush = new SolidColorBrush(Color.FromArgb(75, 70, 130, 200));
			int num = 0;
			TextPointer textPointer2 = textPointer;
			while (textPointer2.CompareTo(position) < 0 && num++ < 20000)
			{
				TextPointer nextInsertionPosition = textPointer2.GetNextInsertionPosition(LogicalDirection.Forward);
				if (nextInsertionPosition != null)
				{
					Rect characterRect = textPointer2.GetCharacterRect(LogicalDirection.Forward);
					Rect characterRect2 = nextInsertionPosition.GetCharacterRect(LogicalDirection.Backward);
					if (!characterRect.IsEmpty && !characterRect2.IsEmpty && Math.Abs(characterRect.Top - characterRect2.Top) < 2.0)
					{
						drawing.DrawRectangle(brush, null, new Rect(Math.Min(characterRect.X, characterRect2.X), characterRect.Y, Math.Max(1.0, Math.Abs(characterRect2.X - characterRect.X)), characterRect.Height));
					}
					textPointer2 = nextInsertionPosition;
					continue;
				}
				break;
			}
		}
	}

	private readonly Window window;

	private TextBlock? block;

	private TextPointer? anchor;

	private TextPointer? end;

	private Point origin;

	private bool dragging;

	private SelectionAdorner? highlight;

	public string SelectedText
	{
		get
		{
			TextBlock textBlock = block;
			if (textBlock == null || !textBlock.IsVisible || anchor == null || end == null)
			{
				return "";
			}
			return new TextRange(anchor, end).Text;
		}
	}

	public TextSelection(Window window)
	{
		this.window = window;
		window.AddHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(Down), handledEventsToo: true);
		window.AddHandler(Mouse.PreviewMouseMoveEvent, new MouseEventHandler(Move), handledEventsToo: true);
		window.AddHandler(Mouse.PreviewMouseUpEvent, new MouseButtonEventHandler(Up), handledEventsToo: true);
		window.PreviewKeyDown += (object _, KeyEventArgs e) =>
		{
			if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control && SelectedText.Length > 0)
			{
				Clipboard.SetText(SelectedText);
				e.Handled = true;
			}
			if (e.Key == Key.Escape)
			{
				Clear();
			}
		};
		window.AddHandler(ScrollViewer.ScrollChangedEvent, (ScrollChangedEventHandler)((object _, ScrollChangedEventArgs _) =>
		{
			if (!dragging)
			{
				Clear();
			}
		}), handledEventsToo: true);
		window.SizeChanged += (object _, SizeChangedEventArgs _) =>
		{
			Clear();
		};
		window.Closed += (object? _, EventArgs _) =>
		{
			Clear();
		};
	}

	private static DependencyObject? Parent(DependencyObject item)
	{
		if (!(item is Visual))
		{
			return LogicalTreeHelper.GetParent(item);
		}
		return VisualTreeHelper.GetParent(item);
	}

	private static T? Ancestor<T>(DependencyObject? item) where T : DependencyObject
	{
		while (item != null)
		{
			if (item is T result)
			{
				return result;
			}
			item = Parent(item);
		}
		return null;
	}

	private void Down(object sender, MouseButtonEventArgs e)
	{
		if (e.ChangedButton != MouseButton.Left)
		{
			return;
		}
		Clear();
		DependencyObject item = e.OriginalSource as DependencyObject;
		if (Ancestor<TextBoxBase>(item) != null || Ancestor<PasswordBox>(item) != null)
		{
			return;
		}
		TextBlock textBlock = Ancestor<TextBlock>(item);
		bool flag = textBlock == null || textBlock.FontFamily.Source.Contains("MDL2") || textBlock.Text.Length == 0;
		if (!flag)
		{
			bool flag2;
			switch (textBlock.Text)
			{
			case "×":
			case "＋":
			case "○":
			case "✓":
			case "★":
			case "⌄":
				flag2 = true;
				break;
			default:
				flag2 = false;
				break;
			}
			flag = flag2;
		}
		if (!flag)
		{
			block = textBlock;
			origin = e.GetPosition(window);
			anchor = textBlock.GetPositionFromPoint(e.GetPosition(textBlock), snapToText: true);
			end = anchor;
		}
	}

	private void Move(object sender, MouseEventArgs e)
	{
		if (block == null || anchor == null || e.LeftButton != MouseButtonState.Pressed)
		{
			return;
		}
		if (!block.IsVisible)
		{
			Clear();
			return;
		}
		Vector vector = e.GetPosition(window) - origin;
		if (!dragging && Math.Abs(vector.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(vector.Y) < SystemParameters.MinimumVerticalDragDistance)
		{
			return;
		}
		if (!dragging)
		{
			dragging = true;
			Ancestor<SwipeActivityCard>(block)?.EndSwipe(cancelled: true);
			Mouse.Capture(window);
			window.Focus();
			AdornerLayer adornerLayer = AdornerLayer.GetAdornerLayer(block);
			if (adornerLayer != null)
			{
				highlight = new SelectionAdorner(block, this)
				{
					IsHitTestVisible = false
				};
				adornerLayer.Add(highlight);
			}
		}
		end = block.GetPositionFromPoint(e.GetPosition(block), snapToText: true) ?? end;
		highlight?.InvalidateVisual();
		e.Handled = true;
	}

	private void Up(object sender, MouseButtonEventArgs e)
	{
		if (e.ChangedButton == MouseButton.Left && dragging)
		{
			end = block?.GetPositionFromPoint(e.GetPosition(block), snapToText: true) ?? end;
			highlight?.InvalidateVisual();
			dragging = false;
			if (window.IsMouseCaptured)
			{
				window.ReleaseMouseCapture();
			}
			e.Handled = true;
		}
	}

	private void Clear()
	{
		if (dragging && window.IsMouseCaptured)
		{
			window.ReleaseMouseCapture();
		}
		if (highlight != null)
		{
			AdornerLayer.GetAdornerLayer(highlight.AdornedElement)?.Remove(highlight);
		}
		highlight = null;
		block = null;
		anchor = (end = null);
		dragging = false;
	}
}
