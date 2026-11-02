using System;

namespace Planner.Core;

public sealed class SwipeTracker
{
	private double x;

	private double y;

	private bool active;

	private bool moved;

	private bool vertical;

	private bool horizontal;

	public double DeltaX { get; private set; }

	public bool Horizontal
	{
		get
		{
			if (horizontal)
			{
				return !vertical;
			}
			return false;
		}
	}

	public void Begin(double startX, double startY)
	{
		x = startX;
		y = startY;
		active = true;
		moved = (vertical = (horizontal = false));
		DeltaX = 0.0;
	}

	public void Move(double currentX, double currentY)
	{
		if (active)
		{
			DeltaX = currentX - x;
			double value = currentY - y;
			if (Math.Abs(DeltaX) > 4.0 || Math.Abs(value) > 4.0)
			{
				moved = true;
			}
			if (!horizontal && Math.Abs(value) > 8.0 && Math.Abs(value) > Math.Abs(DeltaX))
			{
				vertical = true;
			}
			if (!vertical && Math.Abs(DeltaX) > 8.0 && Math.Abs(DeltaX) > Math.Abs(value) * 1.25)
			{
				horizontal = true;
			}
		}
	}

	public static double Threshold(double width)
	{
		return Math.Clamp(width * 0.3, 30.0, 80.0);
	}

	public SwipeOutcome End(double width)
	{
		int result = (active ? ((horizontal && !vertical && Math.Abs(DeltaX) >= Threshold(width)) ? ((DeltaX < 0.0) ? 2 : 3) : ((!moved) ? 1 : 0)) : 0);
		Cancel();
		return (SwipeOutcome)result;
	}

	public void Cancel()
	{
		active = (moved = (vertical = (horizontal = false)));
		DeltaX = 0.0;
	}
}
