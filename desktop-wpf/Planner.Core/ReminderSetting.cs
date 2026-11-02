namespace Planner.Core;

public sealed class ReminderSetting
{
	public string ActivityId { get; set; } = "";

	public bool Enabled { get; set; }

	public int LeadMin { get; set; } = 15;

	public string Basis { get; set; } = "DEPARTURE";
}
