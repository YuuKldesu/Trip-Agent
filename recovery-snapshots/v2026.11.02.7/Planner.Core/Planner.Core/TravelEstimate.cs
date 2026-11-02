namespace Planner.Core;

public sealed record TravelEstimate(int Minutes, string Source)
{
	public RouteOption? Option { get; init; }
}
