using System;

namespace Planner.Core;

public sealed class EditCommand
{
	public string Action { get; set; } = "UPSERT";

	public Activity Activity { get; set; } = new Activity();

	public RecurrenceRule? Rule { get; set; }

	public DateOnly? OriginDate { get; set; }

	public string Scope { get; set; } = "ONCE";

	public DateOnly? SplitDate { get; set; }

	public bool ReminderEnabled { get; set; }

	public int ReminderLeadMin { get; set; } = 15;

	public Place? Place { get; set; }
}
