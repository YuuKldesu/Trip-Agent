using System;
using System.Collections.Generic;
using System.Linq;

namespace Planner.Core;

public static class GeoBoundary
{
	public static bool CoversSegment(IReadOnlyList<IReadOnlyList<GeoPoint>> boundaries, GeoPoint a, GeoPoint b)
	{
		if (!Inside(a) || !Inside(b))
		{
			return false;
		}
		List<double> list = new List<double> { 0.0, 1.0 };
		double num = b.Lng - a.Lng;
		double num2 = b.Lat - a.Lat;
		foreach (IReadOnlyList<GeoPoint> boundary in boundaries)
		{
			int num3 = 0;
			int index = boundary.Count - 1;
			while (num3 < boundary.Count)
			{
				GeoPoint geoPoint = boundary[index];
				GeoPoint geoPoint2 = boundary[num3];
				double num4 = geoPoint2.Lng - geoPoint.Lng;
				double num5 = geoPoint2.Lat - geoPoint.Lat;
				double num6 = num * num5 - num2 * num4;
				if (!(Math.Abs(num6) < 1E-16))
				{
					double num7 = ((geoPoint.Lng - a.Lng) * num5 - (geoPoint.Lat - a.Lat) * num4) / num6;
					double num8 = ((geoPoint.Lng - a.Lng) * num2 - (geoPoint.Lat - a.Lat) * num) / num6;
					if (num7 > 0.0 && num7 < 1.0 && num8 >= 0.0 && num8 <= 1.0)
					{
						list.Add(num7);
					}
				}
				index = num3++;
			}
		}
		list.Sort();
		for (int i = 1; i < list.Count; i++)
		{
			double num9 = (list[i - 1] + list[i]) / 2.0;
			if (!Inside(new GeoPoint(a.Lng + num9 * num, a.Lat + num9 * num2)))
			{
				return false;
			}
		}
		return true;
		bool Inside(GeoPoint p)
		{
			return boundaries.Any((IReadOnlyList<GeoPoint> x) => Contains(x, p));
		}
	}

	public static bool Contains(IReadOnlyList<GeoPoint> polygon, GeoPoint point)
	{
		if (polygon.Count < 3 || !double.IsFinite(point.Lng) || !double.IsFinite(point.Lat))
		{
			return false;
		}
		bool flag = false;
		int num = 0;
		int index = polygon.Count - 1;
		while (num < polygon.Count)
		{
			GeoPoint geoPoint = polygon[index];
			GeoPoint geoPoint2 = polygon[num];
			if (Math.Abs((point.Lat - geoPoint.Lat) * (geoPoint2.Lng - geoPoint.Lng) - (point.Lng - geoPoint.Lng) * (geoPoint2.Lat - geoPoint.Lat)) < 1E-10 && point.Lng >= Math.Min(geoPoint.Lng, geoPoint2.Lng) - 1E-10 && point.Lng <= Math.Max(geoPoint.Lng, geoPoint2.Lng) + 1E-10 && point.Lat >= Math.Min(geoPoint.Lat, geoPoint2.Lat) - 1E-10 && point.Lat <= Math.Max(geoPoint.Lat, geoPoint2.Lat) + 1E-10)
			{
				return true;
			}
			if (geoPoint.Lat > point.Lat != geoPoint2.Lat > point.Lat && point.Lng < (geoPoint2.Lng - geoPoint.Lng) * (point.Lat - geoPoint.Lat) / (geoPoint2.Lat - geoPoint.Lat) + geoPoint.Lng)
			{
				flag = !flag;
			}
			index = num++;
		}
		return flag;
	}
}
