using System.Collections.Generic;
using System.Linq;

namespace Planner.Core;

public sealed class PlannerState
{
	public long Revision { get; set; }

	public List<ScheduleTable> Tables { get; set; } = new List<ScheduleTable>();

	public List<Place> Places { get; set; } = new List<Place>();

	public List<Activity> Activities { get; set; } = new List<Activity>();

	public List<RecurrenceRule> Rules { get; set; } = new List<RecurrenceRule>();

	public List<ActivityException> Exceptions { get; set; } = new List<ActivityException>();

	public List<Plan> Plans { get; set; } = new List<Plan>();

	public List<PlanScope> Scopes { get; set; } = new List<PlanScope>();

	public List<PlanLeg> Legs { get; set; } = new List<PlanLeg>();

	public List<RouteSegment> Segments { get; set; } = new List<RouteSegment>();

	public List<ReminderSetting> Reminders { get; set; } = new List<ReminderSetting>();

	public List<ReminderDelivery> Deliveries { get; set; } = new List<ReminderDelivery>();

	public PlannerState Copy()
	{
		return JsonKit.Clone(this);
	}

	public string PlaceName(string? id)
	{
		return Places.FirstOrDefault((Place p) => p.Id == id)?.Name ?? "未填地点";
	}
}
