using System.Text.Json;

namespace Planner.Core;

public sealed class DraftPatch
{
	public string Op { get; set; } = "";

	public string EventId { get; set; } = "";

	public JsonElement Fields { get; set; }

	public TripRequest? Event { get; set; }

	public int? Position { get; set; }
}
