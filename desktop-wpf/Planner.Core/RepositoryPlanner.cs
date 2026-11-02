using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Planner.Core;

public static class RepositoryPlanner
{
	private sealed record Busy(int Start, int End, Place? Place, string Title, string ActivityId, DateOnly? OriginDate);

	public static void Validate(TripRequest r)
	{
		bool flag = !r.Day.HasValue || string.IsNullOrWhiteSpace(r.Title) || string.IsNullOrWhiteSpace(r.Location) || r.Title.Length > 100 || !r.Duration.HasValue || r.Duration < ((!r.IsReturn) ? ((int?)1) : ((int?)0)) || r.Duration > 10080;
		bool flag2;
		if (!flag)
		{
			int? start = r.Start;
			if (start.HasValue)
			{
				int valueOrDefault = start.GetValueOrDefault();
				if (valueOrDefault < 0 || valueOrDefault >= 1440)
				{
					flag2 = true;
					goto IL_00cf;
				}
			}
			flag2 = false;
			goto IL_00cf;
		}
		goto IL_00d2;
		IL_0107:
		bool flag3 = flag2;
		goto IL_010a;
		IL_013f:
		bool flag4 = flag2;
		goto IL_0142;
		IL_0179:
		bool flag5 = flag2;
		goto IL_017d;
		IL_00cf:
		flag = flag2;
		goto IL_00d2;
		IL_00d2:
		flag3 = flag;
		if (!flag3)
		{
			int? start = r.Earliest;
			if (start.HasValue)
			{
				int valueOrDefault = start.GetValueOrDefault();
				if (valueOrDefault < 0 || valueOrDefault > 1440)
				{
					flag2 = true;
					goto IL_0107;
				}
			}
			flag2 = false;
			goto IL_0107;
		}
		goto IL_010a;
		IL_017d:
		bool flag6 = flag5;
		if (!flag6)
		{
			int valueOrDefault = r.ReminderLeadMin;
			flag2 = ((valueOrDefault < 0 || valueOrDefault > 10080) ? true : false);
			flag6 = flag2;
		}
		if (flag6)
		{
			throw new ArgumentException("请完善日期、活动、地点及1—10080分钟的耗时；返程可为0分钟。");
		}
		DateOnly? endDay = r.EndDay;
		if (endDay.HasValue)
		{
			DateOnly valueOrDefault2 = endDay.GetValueOrDefault();
			if (!r.Start.HasValue || !r.End.HasValue)
			{
				throw new ArgumentException("跨日活动请明确起止日期和时间。");
			}
			int num = (valueOrDefault2.DayNumber - r.Day.Value.DayNumber) * 1440 + r.End.Value - r.Start.Value;
			if (num != r.Duration || num < ((!r.IsReturn) ? 1 : 0))
			{
				throw new ArgumentException("跨日起止时间与耗时不一致，请重新核对。");
			}
		}
		return;
		IL_010a:
		flag4 = flag3;
		if (!flag4)
		{
			int? start = r.LatestEnd;
			if (start.HasValue)
			{
				int valueOrDefault = start.GetValueOrDefault();
				if (valueOrDefault < 0 || valueOrDefault > 11520)
				{
					flag2 = true;
					goto IL_013f;
				}
			}
			flag2 = false;
			goto IL_013f;
		}
		goto IL_0142;
		IL_0142:
		flag5 = flag4;
		if (!flag5)
		{
			int? start = r.End;
			if (start.HasValue)
			{
				int valueOrDefault = start.GetValueOrDefault();
				if (valueOrDefault < 0 || valueOrDefault > 1440)
				{
					flag2 = true;
					goto IL_0179;
				}
			}
			flag2 = false;
			goto IL_0179;
		}
		goto IL_017d;
	}

	public static async Task<RepositoryPlan> PlanAsync(PlannerState state, IReadOnlyList<PreparedTrip> requests, Func<Place, Place, string?, DateTime, CancellationToken, Task<TravelEstimate>> route, CancellationToken token = default(CancellationToken), string? excludeKey = null)
	{
		int count = requests.Count;
		if ((count < 1 || count > 15) ? true : false)
		{
			throw new ArgumentException("每次支持1—15项活动。");
		}
		PlannerState working = state.Copy();
		ScheduleTable provisional = new ScheduleTable
		{
			Name = "本次排程约束"
		};
		working.Tables.Add(provisional);
		Dictionary<DateOnly, int> previousNew = new Dictionary<DateOnly, int>();
		List<PlannedTrip> rows = new List<PlannedTrip>();
		foreach (PreparedTrip item in requests)
		{
			token.ThrowIfCancellationRequested();
			TripRequest e = item.Request;
			Validate(e);
			DateOnly day = e.Day.Value;
			int duration = Math.Max(1, e.Duration.Value);
			DateTime begin = day.ToDateTime(TimeOnly.MinValue);
			DateTime until = begin.AddMinutes((e.Start ?? 480) + duration).Date.AddDays(1.0);
			List<Busy> existing = (from o in CalendarEngine.Expand(working, begin, until)
				where o.Key != excludeKey
				select new Busy((int)(o.Start - begin).TotalMinutes, (int)(o.End - begin).TotalMinutes, working.Places.FirstOrDefault((Place p) => p.Id == o.PlaceId), o.Title, o.Activity.Id, o.OriginDate) into o
				orderby o.Start
				select o).ToList();
			int baseStart = ((!e.Start.HasValue) ? 480 : 0);
			int? start = e.Start;
			int num;
			if (start.HasValue)
			{
				int valueOrDefault = start.GetValueOrDefault();
				num = valueOrDefault + duration;
			}
			else
			{
				num = 1320;
			}
			int finishLimit = num;
			int ordered = Math.Max(e.Earliest.GetValueOrDefault(), previousNew.GetValueOrDefault(day));
			PlannedTrip found = null;
			for (int i = 0; i <= existing.Count; i++)
			{
				Busy prev = ((i > 0) ? existing.Take(i).MaxBy((Busy o) => o.End) : null);
				Busy next = ((i < existing.Count) ? existing[i] : null);
				int low = Math.Max(baseStart, Math.Max(ordered, prev?.End ?? baseStart));
				int high = Math.Min(finishLimit - duration, Math.Min((e.LatestEnd ?? finishLimit) - duration, (next?.Start ?? finishLimit) - duration));
				if (low > high)
				{
					continue;
				}
				start = e.Start;
				if (start.HasValue)
				{
					int valueOrDefault2 = start.GetValueOrDefault();
					if (valueOrDefault2 < low || valueOrDefault2 > high)
					{
						continue;
					}
				}
				TravelEstimate travelEstimate = (((object)prev != null) ? (await Leg(prev.Place, item.Place, e.ModeIn, prev.End)) : new TravelEstimate(0, "无前序行程"));
				TravelEstimate before = travelEstimate;
				low = Math.Max(baseStart, Math.Max(ordered, (prev?.End ?? baseStart) + (((object)prev != null) ? before.Minutes : 0)));
				travelEstimate = (((object)next != null) ? (await Leg(item.Place, next.Place, null, (e.Start ?? low) + duration)) : new TravelEstimate(0, "无后续行程"));
				TravelEstimate travelEstimate2 = travelEstimate;
				high = Math.Min(high, (next?.Start ?? finishLimit) - duration - travelEstimate2.Minutes);
				int num2 = e.Start ?? low;
				if (num2 >= low && num2 <= high)
				{
					found = new PlannedTrip(item, num2, before, travelEstimate2, prev?.Title, next?.Title)
					{
						PreviousActivityId = prev?.ActivityId,
						PreviousOriginDate = prev?.OriginDate,
						PreviousPlaceId = prev?.Place?.Id,
						NextActivityId = next?.ActivityId,
						NextOriginDate = next?.OriginDate,
						NextPlaceId = next?.Place?.Id,
						TravelDepartAt = (((object)prev == null) ? ((DateTime?)null) : new DateTime?(begin.AddMinutes(prev.End)))
					};
					break;
				}
			}
			if ((object)found == null)
			{
				throw new ArgumentException("“" + e.Title + "”无法在指定时间与已有活动及交通之间安排；未指定开始时间时默认在08:00—22:00寻找空档。");
			}
			rows.Add(found);
			if (!working.Places.Any((Place p) => p.Id == item.Place.Id))
			{
				working.Places.Add(JsonKit.Clone(item.Place));
			}
			working.Activities.Add(new Activity
			{
				Id = found.ActivityId,
				TableId = provisional.Id,
				Title = e.Title,
				PlaceId = item.Place.Id,
				StartAt = begin.AddMinutes(found.Start),
				EndAt = begin.AddMinutes(found.Start + duration),
				DurationMin = duration
			});
			previousNew[day] = found.Start + duration;
			async Task<TravelEstimate> Leg(Place? from, Place? to, string? mode, int at)
			{
				if (from == null || to == null)
				{
					throw new ArgumentException("相邻活动有未确认地点，请先完善地点后重试。");
				}
				TravelEstimate travelEstimate3 = await route(from, to, mode, day.ToDateTime(TimeOnly.MinValue).AddMinutes(Math.Max(0, at)), token);
				if (travelEstimate3.Minutes < 0)
				{
					throw new InvalidOperationException("交通耗时不合法。");
				}
				return travelEstimate3;
			}
		}
		int i2;
		for (i2 = 0; i2 < rows.Count; i2++)
		{
			PlannedTrip plannedTrip = rows.FirstOrDefault((PlannedTrip r) => r.PreviousActivityId == rows[i2].ActivityId && !r.PreviousOriginDate.HasValue);
			if ((object)plannedTrip != null)
			{
				rows[i2] = rows[i2] with
				{
					After = plannedTrip.Before,
					NextActivityId = plannedTrip.ActivityId,
					NextOriginDate = null,
					NextPlaceId = plannedTrip.Trip.Place.Id,
					NextTitle = plannedTrip.Trip.Request.Title
				};
			}
		}
		return new RepositoryPlan(state.Revision, rows);
	}

	public static List<EditCommand> Commands(RepositoryPlan plan, string tableId)
	{
		return plan.Rows.Select((PlannedTrip row) =>
		{
			TripRequest request = row.Trip.Request;
			DateTime value = request.Day.Value.ToDateTime(TimeOnly.MinValue).AddMinutes(row.Start);
			int num = Math.Max(1, request.Duration.Value);
			string text = request.Notes + (request.IsReturn ? "\n返程到达标记（存储占1分钟）" : "");
			if (row.PreviousTitle != null)
			{
				text += $"\n前往方式：{row.Before.Source}，约{row.Before.Minutes}分钟";
			}
			return new EditCommand
			{
				Activity = new Activity
				{
					Id = row.ActivityId,
					TableId = tableId,
					Title = request.Title,
					StartAt = value,
					EndAt = value.AddMinutes(num),
					DurationMin = num,
					Note = text.Trim()
				},
				Place = row.Trip.Place,
				ReminderEnabled = request.ReminderEnabled,
				ReminderLeadMin = request.ReminderLeadMin
			};
		}).ToList();
	}
}
