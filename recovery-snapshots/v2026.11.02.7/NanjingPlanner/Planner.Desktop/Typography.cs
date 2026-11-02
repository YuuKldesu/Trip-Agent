using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace Planner.Desktop;

public static class Typography
{
	private static readonly HashSet<double> sizes = new HashSet<double>();

	private static double current = 14.0;

	public static double Scale => current / 13.0;

	private static string Key(double nominal)
	{
		return "Planner.Font." + nominal.ToString(CultureInfo.InvariantCulture);
	}

	public static void Set(FrameworkElement target, double nominal)
	{
		sizes.Add(nominal);
		Application application = Application.Current;
		if (application != null)
		{
			application.Resources[Key(nominal)] = nominal * current / 13.0;
		}
		target.SetResourceReference((target is TextBlock) ? TextBlock.FontSizeProperty : Control.FontSizeProperty, Key(nominal));
	}

	public static void Apply(double value)
	{
		current = value;
		sizes.Add(13.0);
		Application application = Application.Current;
		if (application == null)
		{
			return;
		}
		foreach (double size in sizes)
		{
			application.Resources[Key(size)] = size * current / 13.0;
		}
	}
}
