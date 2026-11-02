using System;
using System.Linq;
using System.Text.Json;

namespace Planner.Core;

public static class TransferEndpoints
{
	public static string? PlaceId(PlannerState state, string? activityId, DateOnly? origin)
	{
		Activity activity = state.Activities.FirstOrDefault((Activity a) => a.Id == activityId);
		if (activity == null)
		{
			return null;
		}
		object obj;
		if (origin.HasValue)
		{
			DateOnly date = origin.GetValueOrDefault();
			obj = state.Exceptions.FirstOrDefault((ActivityException e) => e.ActivityId == activityId && e.OriginDate == date && e.Kind == "OVERRIDE");
		}
		else
		{
			obj = null;
		}
		ActivityException ex = (ActivityException)obj;
		if (ex == null || !ex.PlaceOverride)
		{
			return activity.PlaceId;
		}
		return ex.PlaceId;
	}

	public static string? Snapshot(PlannerState state, PlanLeg leg, string field)
	{
		string text = (from s in state.Segments
			where s.LegId == leg.Id
			orderby s.Seq
			select s).FirstOrDefault()?.CostDetailJson;
		if (string.IsNullOrWhiteSpace(text))
		{
			return null;
		}
		try
		{
			using JsonDocument jsonDocument = JsonDocument.Parse(text);
			JsonElement value;
			return (jsonDocument.RootElement.ValueKind == JsonValueKind.Object && jsonDocument.RootElement.TryGetProperty(field, out value) && value.ValueKind == JsonValueKind.String) ? value.GetString() : null;
		}
		catch (JsonException)
		{
			return null;
		}
	}
}
