using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Planner.Core;

public sealed record TrafficPair(Occurrence From, Occurrence To)
{
	public int GapMinutes => (int)Math.Floor((To.Start - From.End).TotalMinutes);

	public string Fingerprint(PlannerState state)
	{
		return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new object[2]
		{
			Stamp(From, state),
			Stamp(To, state)
		}, JsonKit.Options))));
	}

	private static object Stamp(Occurrence o, PlannerState state)
	{
		Place place = state.Places.FirstOrDefault((Place p) => p.Id == o.PlaceId);
		return new
		{
			Key = o.Key,
			Start = o.Start,
			End = o.End,
			PlaceId = o.PlaceId,
			Name = place?.Name,
			Lng = place?.Lng,
			Lat = place?.Lat
		};
	}
}
