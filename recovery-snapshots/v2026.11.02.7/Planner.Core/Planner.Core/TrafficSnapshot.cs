using System;

namespace Planner.Core;

public sealed class TrafficSnapshot
{
	public string Fingerprint { get; set; } = "";

	public string FromKey { get; set; } = "";

	public string ToKey { get; set; } = "";

	public int? Minutes { get; set; }

	public string Mode { get; set; } = "";

	public string Source { get; set; } = "";

	public DateTime CheckedAt { get; set; } = Clock.Now;

	public bool Fresh(DateTime now)
	{
		if (CheckedAt <= now.AddMinutes(1.0))
		{
			return now - CheckedAt < TimeSpan.FromHours(24);
		}
		return false;
	}
}
