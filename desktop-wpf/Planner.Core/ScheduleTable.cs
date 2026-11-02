using System;

namespace Planner.Core;

public sealed class ScheduleTable : Entity
{
	public string Name { get; set; } = "我的行程";

	public string Color { get; set; } = "#D5A078";

	public bool Enabled { get; set; } = true;

	public DateOnly? ValidFrom { get; set; }

	public DateOnly? ValidUntil { get; set; }

	public int Version { get; set; } = 1;

	public DateTime UpdatedAt { get; set; } = Clock.Now;

	public bool Active(DateOnly date)
	{
		if (Enabled)
		{
			if (ValidFrom.HasValue)
			{
				DateOnly value = date;
				DateOnly? validFrom = ValidFrom;
				if (!(value >= validFrom))
				{
					goto IL_0070;
				}
			}
			if (ValidUntil.HasValue)
			{
				DateOnly value = date;
				DateOnly? validFrom = ValidUntil;
				return value <= validFrom;
			}
			return true;
		}
		goto IL_0070;
		IL_0070:
		return false;
	}
}
