using System;
using System.Collections.Generic;

namespace Planner.Core;

public sealed class ScheduleRead
{
	public DateOnly FromDay { get; set; }

	public DateOnly ToDay { get; set; }

	public long Revision { get; set; }

	public DateTime QueryAt { get; set; } = Clock.Now;

	public bool Truncated { get; set; }

	public List<ScheduleReadItem> Events { get; set; } = new List<ScheduleReadItem>();
}
