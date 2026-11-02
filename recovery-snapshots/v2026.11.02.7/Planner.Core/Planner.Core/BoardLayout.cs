using System;

namespace Planner.Core;

public static class BoardLayout
{
	public static double RowMinimum(double viewport, int rows)
	{
		return Math.Max(88.0, viewport / (double)Math.Max(1, rows));
	}
}
