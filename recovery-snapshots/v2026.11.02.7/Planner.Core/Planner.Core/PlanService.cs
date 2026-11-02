using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Planner.Core;

public static class PlanService
{
	public static PlannerState Confirm(PlannerState current, PlannerState resolved, PlanningRequest request, ItineraryCandidate candidate, long revision)
	{
		if (current.Revision != revision || resolved.Revision != revision)
		{
			throw new InvalidOperationException("行程已变化，请重新生成路线。");
		}
		if (candidate.Stops.Count == 0)
		{
			throw new ArgumentException("方案为空。");
		}
		ValidateResources(resolved, request, candidate);
		DateTime dateTime = request.Date.ToDateTime(TimeOnly.MinValue);
		List<Occurrence> list = CalendarEngine.Expand(current, dateTime, dateTime.AddDays(1.0));
		if (list.Count != candidate.Stops.Count || candidate.Stops.Select((PlannedStop x) => x.Original.Key).Distinct().Count() != list.Count)
		{
			throw new ArgumentException("方案事项不完整。");
		}
		List<EditCommand> list2 = new List<EditCommand>();
		foreach (PlannedStop stop in candidate.Stops)
		{
			Occurrence occurrence = list.SingleOrDefault((Occurrence x) => x.Key == stop.Original.Key) ?? throw new ArgumentException("方案实例无效。");
			if (occurrence.VersionKey != stop.Original.VersionKey)
			{
				throw new InvalidOperationException("事项版本已变化。");
			}
			if (stop.Arrive > stop.Start || stop.End <= stop.Start)
			{
				throw new ArgumentException("路线到达时间不满足活动要求。");
			}
			if (stop.Start != occurrence.Start || stop.End != occurrence.End)
			{
				if (!request.ReplanFlexible || !request.EditableTables.Contains(occurrence.Activity.TableId) || occurrence.Activity.ConstraintType != "FLEXIBLE")
				{
					throw new ArgumentException("方案不能移动固定事项或其他行程表。");
				}
				Activity a = JsonKit.Clone(occurrence.Activity);
				a.StartAt = stop.Start;
				a.EndAt = stop.End;
				ReminderSetting reminderSetting = current.Reminders.FirstOrDefault((ReminderSetting x) => x.ActivityId == a.Id);
				list2.Add(new EditCommand
				{
					Activity = a,
					OriginDate = occurrence.OriginDate,
					ReminderEnabled = (reminderSetting?.Enabled ?? false),
					ReminderLeadMin = (reminderSetting?.LeadMin ?? 15)
				});
			}
		}
		PlannerState plannerState = resolved.Copy();
		foreach (Place place in plannerState.Places)
		{
			Place place2 = current.Places.FirstOrDefault((Place x) => x.Id == place.Id);
			if (place2 == null || (place2.Lng == place.Lng && place2.Lat == place.Lat))
			{
				continue;
			}
			foreach (Activity item in plannerState.Activities.Where((Activity x) => x.PlaceId == place.Id))
			{
				ChangeService.Invalidate(plannerState, item.Id);
			}
		}
		plannerState = ChangeService.Apply(plannerState, list2, revision);
		List<Plan> source = plannerState.Plans.Where((Plan x) => x.PlanDate == request.Date).ToList();
		HashSet<string> oldIds = source.Select((Plan x) => x.Id).ToHashSet();
		HashSet<string> legs = (from x in plannerState.Legs
			where oldIds.Contains(x.PlanId)
			select x.Id).ToHashSet();
		plannerState.Segments.RemoveAll((RouteSegment x) => legs.Contains(x.LegId));
		plannerState.Legs.RemoveAll((PlanLeg x) => oldIds.Contains(x.PlanId));
		plannerState.Scopes.RemoveAll((PlanScope x) => oldIds.Contains(x.PlanId));
		plannerState.Plans.RemoveAll((Plan x) => oldIds.Contains(x.Id));
		Plan plan = new Plan
		{
			PlanDate = request.Date,
			StartPlaceId = request.Origin.Id,
			EarliestDepart = request.Earliest,
			Version = (source.FirstOrDefault()?.Version ?? 0) + 1,
			ResourceJson = JsonSerializer.Serialize(new { request.Modes, request.BikeResource, request.ParkingMinutes }, JsonKit.Options)
		};
		if (!plannerState.Places.Any((Place x) => x.Id == request.Origin.Id))
		{
			plannerState.Places.Add(JsonKit.Clone(request.Origin));
		}
		plannerState.Plans.Add(plan);
		foreach (ScheduleTable item2 in plannerState.Tables.Where((ScheduleTable x) => x.Active(request.Date)))
		{
			plannerState.Scopes.Add(new PlanScope
			{
				PlanId = plan.Id,
				TableId = item2.Id,
				TableVersion = item2.Version,
				Role = ((request.EditableTables.Contains(item2.Id) && request.ReplanFlexible) ? "EDITABLE" : "CONSTRAINT")
			});
		}
		PlannedStop plannedStop = null;
		int num = 0;
		foreach (PlannedStop stop2 in candidate.Stops)
		{
			if ((object)plannedStop != null && stop2.Depart < plannedStop.End)
			{
				throw new ArgumentException("路线与上一事项重叠。");
			}
			PlanLeg planLeg = new PlanLeg
			{
				PlanId = plan.Id,
				Seq = num++,
				FromActivityId = plannedStop?.Original.Activity.Id,
				FromOriginDate = plannedStop?.Original.OriginDate,
				ToActivityId = stop2.Original.Activity.Id,
				ToOriginDate = stop2.Original.OriginDate,
				DepartAt = stop2.Depart,
				ArriveAt = stop2.Arrive,
				BufferSec = stop2.BufferSec,
				QueryAt = stop2.Route.QueryAt,
				RequestedDepartAt = stop2.Depart,
				ValidationStatus = "VALID",
				KnownCostFen = stop2.Route.KnownCostFen,
				CostComplete = stop2.Route.CostComplete,
				CostNote = stop2.Route.CostNote
			};
			plannerState.Legs.Add(planLeg);
			int num2 = 0;
			foreach (RouteSegment segment in stop2.Route.Segments)
			{
				RouteSegment routeSegment = JsonKit.Clone(segment);
				routeSegment.Id = Guid.NewGuid().ToString("N");
				routeSegment.LegId = planLeg.Id;
				routeSegment.Seq = num2++;
				plannerState.Segments.Add(routeSegment);
			}
			plannedStop = stop2;
		}
		return plannerState;
	}

	public static void ValidateResources(PlannerState state, PlanningRequest request, ItineraryCandidate candidate)
	{
		Place a = request.Origin;
		Place b = request.Origin;
		Place b2 = request.Origin;
		DateTime dateTime = request.Earliest;
		foreach (PlannedStop stop in candidate.Stops)
		{
			Place place = state.Places.SingleOrDefault((Place x) => x.Id == stop.Original.PlaceId) ?? throw new ArgumentException("目的地已失效。");
			if (!TransportPolicy.Allows(stop.Route, request.Modes))
			{
				throw new ArgumentException("方案包含未选择的交通方式。");
			}
			if (stop.Route.Uses("CAR") && !Same(a, b))
			{
				throw new ArgumentException("自驾车辆仍在此前地点，不能异地取车。");
			}
			if (stop.Route.Uses("BIKE") && request.BikeResource == "OWN_BIKE" && !Same(a, b2))
			{
				throw new ArgumentException("自有自行车仍在此前地点，不能异地取车。");
			}
			if (stop.Depart < dateTime || stop.Arrive != stop.Depart.AddSeconds(stop.Route.DurationSec + stop.BufferSec) || stop.Arrive > stop.Start)
			{
				throw new ArgumentException("该路段与活动时间不能衔接，请重新规划。");
			}
			if (stop.Route.Uses("CAR"))
			{
				b = place;
			}
			if (stop.Route.Uses("BIKE") && request.BikeResource == "OWN_BIKE")
			{
				b2 = place;
			}
			a = place;
			dateTime = stop.End;
		}
		static bool Same(Place place2, Place place3)
		{
			if (!(place2.Id == place3.Id))
			{
				double? lng = place2.Lng;
				if (lng.HasValue)
				{
					double valueOrDefault = lng.GetValueOrDefault();
					lng = place3.Lng;
					if (lng.HasValue)
					{
						double valueOrDefault2 = lng.GetValueOrDefault();
						lng = place2.Lat;
						if (lng.HasValue)
						{
							double valueOrDefault3 = lng.GetValueOrDefault();
							lng = place3.Lat;
							if (lng.HasValue)
							{
								double valueOrDefault4 = lng.GetValueOrDefault();
								if (Math.Abs(valueOrDefault - valueOrDefault2) < 1E-05)
								{
									return Math.Abs(valueOrDefault3 - valueOrDefault4) < 1E-05;
								}
							}
						}
					}
				}
				return false;
			}
			return true;
		}
	}
}
