using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Planner.Core;

public interface IMapProvider
{
	Task<List<Place>> SearchPlacesAsync(string text, CancellationToken token);

	Task<List<RouteOption>> RoutesAsync(Place from, Place to, string mode, DateTime depart, CancellationToken token);
}
