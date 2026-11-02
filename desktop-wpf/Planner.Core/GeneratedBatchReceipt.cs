using System;
using System.Collections.Generic;

namespace Planner.Core;

public sealed class GeneratedBatchReceipt : Entity
{
	public string BranchId { get; set; } = "";

	public string Status { get; set; } = "APPLIED";

	public DateTime CreatedAt { get; set; } = Clock.Now;

	public long ExpectedRevision { get; set; }

	public long SourceVersion { get; set; }

	public List<Activity> Activities { get; set; } = new List<Activity>();

	public List<ScheduleTable> Tables { get; set; } = new List<ScheduleTable>();

	public List<Place> Places { get; set; } = new List<Place>();

	public List<ReminderSetting> Reminders { get; set; } = new List<ReminderSetting>();

	public List<Plan> Plans { get; set; } = new List<Plan>();

	public List<PlanLeg> Legs { get; set; } = new List<PlanLeg>();

	public List<RouteSegment> Segments { get; set; } = new List<RouteSegment>();

	public List<PlanScope> Scopes { get; set; } = new List<PlanScope>();
}
