using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Planner.Core;

public static class GeneratedPlanService
{
	private sealed record Edge(string From, DateOnly? FromOrigin, string? FromPlace, string To, DateOnly? ToOrigin, string? ToPlace, DateTime Depart, TravelEstimate Estimate, string? Mode);

	public static PlannerState Apply(PlannerState state, RepositoryPlan plan, string tableId)
	{
		PlannerState plannerState = ChangeService.Apply(state, RepositoryPlanner.Commands(plan, tableId), plan.Revision);
		Dictionary<string, Edge> edges = new Dictionary<string, Edge>();
		foreach (PlannedTrip row in plan.Rows)
		{
			string previousActivityId = row.PreviousActivityId;
			if (previousActivityId != null)
			{
				DateTime? travelDepartAt = row.TravelDepartAt;
				if (travelDepartAt.HasValue)
				{
					DateTime valueOrDefault = travelDepartAt.GetValueOrDefault();
					Add(new Edge(previousActivityId, row.PreviousOriginDate, row.PreviousPlaceId, row.ActivityId, null, row.Trip.Place.Id, valueOrDefault, row.Before, row.Trip.Request.ModeIn));
				}
			}
		}
		foreach (PlannedTrip row2 in plan.Rows)
		{
			string nextActivityId = row2.NextActivityId;
			if (nextActivityId != null)
			{
				DateTime depart = row2.Trip.Request.Day.Value.ToDateTime(TimeOnly.MinValue).AddMinutes(row2.Start + row2.Trip.Request.Duration.Value);
				Add(new Edge(row2.ActivityId, null, row2.Trip.Place.Id, nextActivityId, row2.NextOriginDate, row2.NextPlaceId, depart, row2.After, null));
			}
		}
		foreach (Edge edge in edges.Values)
		{
			DateOnly day = DateOnly.FromDateTime(edge.Depart);
			Plan daily = plannerState.Plans.FirstOrDefault((Plan p) => p.PlanDate == day);
			if (daily == null)
			{
				daily = new Plan
				{
					PlanDate = day,
					Status = "CONFIRMED",
					EarliestDepart = edge.Depart
				};
				plannerState.Plans.Add(daily);
			}
			Activity activity = plannerState.Activities.Single((Activity a) => a.Id == edge.From);
			Activity activity2 = plannerState.Activities.Single((Activity a) => a.Id == edge.To);
			foreach (string id in new string[3] { tableId, activity.TableId, activity2.TableId }.Distinct())
			{
				if (!plannerState.Scopes.Any((PlanScope s) => s.PlanId == daily.Id && s.TableId == id))
				{
					plannerState.Scopes.Add(new PlanScope
					{
						PlanId = daily.Id,
						TableId = id,
						TableVersion = plannerState.Tables.Single((ScheduleTable t) => t.Id == id).Version
					});
				}
			}
			RouteOption option = edge.Estimate.Option;
			PlanLeg planLeg = new PlanLeg
			{
				PlanId = daily.Id,
				Seq = (from l in plannerState.Legs
					where l.PlanId == daily.Id
					select l.Seq).DefaultIfEmpty(0).Max() + 1,
				FromActivityId = edge.From,
				FromOriginDate = edge.FromOrigin,
				ToActivityId = edge.To,
				ToOriginDate = edge.ToOrigin,
				DepartAt = edge.Depart,
				ArriveAt = edge.Depart.AddMinutes(edge.Estimate.Minutes),
				RequestedDepartAt = edge.Depart,
				QueryAt = (option?.QueryAt ?? Clock.Now),
				ValidationStatus = ((option == null) ? "UNVERIFIED" : "VALID"),
				KnownCostFen = option?.KnownCostFen,
				CostComplete = (option?.CostComplete ?? false),
				CostNote = (option?.CostNote ?? "粗估交通，费用未核验")
			};
			plannerState.Legs.Add(planLeg);
			List<RouteSegment> list = option?.Segments.Select(JsonKit.Clone).ToList() ?? new List<RouteSegment>();
			if (list.Count == 0)
			{
				string text = edge.Mode ?? edge.Estimate.Source.Split(" · ")[0];
				List<RouteSegment> list2 = list;
				RouteSegment routeSegment = new RouteSegment();
				RouteSegment routeSegment2 = routeSegment;
				routeSegment2.Mode = text switch
				{
					"公交" => "BUS", 
					"骑行" => "BIKE", 
					"驾车" => "CAR", 
					_ => "WALK", 
				};
				routeSegment.Instruction = edge.Estimate.Source;
				routeSegment.DurationSec = edge.Estimate.Minutes * 60;
				routeSegment.Provider = edge.Estimate.Source;
				list2.Add(routeSegment);
			}
			int num = 0;
			foreach (RouteSegment item in list)
			{
				item.Id = Guid.NewGuid().ToString("N");
				item.LegId = planLeg.Id;
				num = (item.Seq = num + 1);
				if (num == 1)
				{
					item.CostDetailJson = JsonSerializer.Serialize(new
					{
						provider_cost_detail = item.CostDetailJson,
						from_name = plannerState.PlaceName(edge.FromPlace),
						to_name = plannerState.PlaceName(edge.ToPlace),
						ownership_table_id = tableId
					});
				}
				plannerState.Segments.Add(item);
			}
		}
		return plannerState;
		void Add(Edge edge2)
		{
			if (edge2.Estimate.Minutes > 0)
			{
				string key = $"{edge2.From}:{edge2.FromOrigin:yyyy-MM-dd}>{edge2.To}:{edge2.ToOrigin:yyyy-MM-dd}";
				edges.TryAdd(key, edge2);
			}
		}
	}
}
