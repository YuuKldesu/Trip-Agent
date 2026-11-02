using System;

namespace Planner.Core;

public sealed record Occurrence(Activity Activity, DateOnly? OriginDate, string Title, string? PlaceId, DateTime Start, DateTime End, int ExceptionVersion = 0)
{
	public string Key => Activity.Id + ":" + (OriginDate?.ToString("yyyy-MM-dd") ?? "once");

	public string VersionKey => Activity.Version + ":" + ExceptionVersion;
}
