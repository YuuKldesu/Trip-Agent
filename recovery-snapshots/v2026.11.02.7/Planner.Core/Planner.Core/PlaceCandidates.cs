using System;
using System.Collections.Generic;

namespace Planner.Core;

public sealed class PlaceCandidates
{
	public string Source { get; set; } = "";

	public string Query { get; set; } = "";

	public List<Place> Places { get; set; } = new List<Place>();

	public DateTime QueryAt { get; set; } = Clock.Now;
}
