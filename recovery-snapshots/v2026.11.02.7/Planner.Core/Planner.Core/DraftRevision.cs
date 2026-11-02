using System.Collections.Generic;

namespace Planner.Core;

public sealed class DraftRevision
{
	public List<TripRequest> Events { get; set; } = new List<TripRequest>();

	public Dictionary<string, MemoryFact> Facts { get; set; } = new Dictionary<string, MemoryFact>();
}
