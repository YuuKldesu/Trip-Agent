using System;

namespace Planner.Core;

public sealed record PlannedTrip(PreparedTrip Trip, int Start, TravelEstimate Before, TravelEstimate After, string? PreviousTitle, string? NextTitle)
{
	public string ActivityId { get; init; } = Guid.NewGuid().ToString("N");

	public string? PreviousActivityId { get; init; }

	public DateOnly? PreviousOriginDate { get; init; }

	public string? PreviousPlaceId { get; init; }

	public string? NextActivityId { get; init; }

	public DateOnly? NextOriginDate { get; init; }

	public string? NextPlaceId { get; init; }

	public DateTime? TravelDepartAt { get; init; }
}
