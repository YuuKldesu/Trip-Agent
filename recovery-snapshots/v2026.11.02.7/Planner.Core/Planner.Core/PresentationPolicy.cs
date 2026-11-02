using System;

namespace Planner.Core;

public static class PresentationPolicy
{
	public static bool Compact(string mode, double width, double height, bool current, double fontSize)
	{
		if (mode == "FULL")
		{
			return false;
		}
		if (mode == "COMPACT")
		{
			return true;
		}
		if (width <= 0.0 || height <= 0.0)
		{
			return current;
		}
		double num = Math.Max(1.0, fontSize / 14.0);
		double num2 = width / height;
		if (!current)
		{
			if (!(width < 1080.0 * num))
			{
				return num2 < 0.95;
			}
			return true;
		}
		if (!(width <= 1200.0 * num))
		{
			return num2 <= 1.1;
		}
		return true;
	}
}
