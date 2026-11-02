using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Planner.Core;

public sealed class PlanningRequest
{
	public DateOnly Date { get; set; }

	public Place Origin { get; set; } = new Place();

	public DateTime Earliest { get; set; }

	public List<string> Modes { get; set; }

	public bool ReplanFlexible { get; set; }

	public HashSet<string> EditableTables { get; set; }

	public string BikeResource { get; set; }

	public int ParkingMinutes { get; set; }

	public List<string> Warnings { get; set; }

	public PlanningRequest()
	{
		int num = 2;
		List<string> list = new List<string>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<string> span = CollectionsMarshal.AsSpan(list);
		span[0] = "WALK";
		span[1] = "BUS";
		Modes = list;
		EditableTables = new HashSet<string>();
		BikeResource = "SHARED_BIKE";
		ParkingMinutes = 5;
		Warnings = new List<string>();
	}
}
