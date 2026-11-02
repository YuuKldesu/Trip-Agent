using System;

namespace Planner.Core;

public sealed class ReminderDelivery : Entity
{
	public string ActivityId { get; set; } = "";

	public string InstanceKey { get; set; } = "";

	public DateTime DueAt { get; set; }

	public string Status { get; set; } = "NOTIFIED";

	public DateTime RecordedAt { get; set; } = Clock.Now;
}
