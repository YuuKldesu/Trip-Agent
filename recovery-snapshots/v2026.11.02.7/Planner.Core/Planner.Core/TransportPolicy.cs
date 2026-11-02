using System.Collections.Generic;
using System.Linq;

namespace Planner.Core;

public static class TransportPolicy
{
	public static bool Allows(RouteOption route, IReadOnlyCollection<string> modes)
	{
		bool transit = route.Uses("BUS") || route.Uses("METRO");
		return route.Segments.All((RouteSegment s) => modes.Contains(s.Mode) || ((s.Mode == "WALK") & transit));
	}

	public static List<string> QueryModes(IReadOnlyCollection<string> modes)
	{
		List<string> list = modes.Where((string m) =>
		{
			bool flag = ((m == "BUS" || m == "METRO") ? true : false);
			return !flag;
		}).ToList();
		if (modes.Contains("BUS") && modes.Contains("METRO"))
		{
			list.Add("TRANSIT");
		}
		else if (modes.Contains("BUS"))
		{
			list.Add("BUS");
		}
		else if (modes.Contains("METRO"))
		{
			list.Add("METRO");
		}
		return list;
	}
}
