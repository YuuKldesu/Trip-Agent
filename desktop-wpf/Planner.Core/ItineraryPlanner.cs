using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Planner.Core;

public sealed class ItineraryPlanner(IMapProvider provider)
{
	private sealed record Node(List<PlannedStop> Stops, List<Occurrence> Left, Place At, DateTime Time, Place Car, Place Bike);

	public async Task<List<ItineraryCandidate>> GenerateAsync(PlannerState state, PlanningRequest request, CancellationToken token)
	{
		if (request.Modes.Count == 0 || request.Modes.Any((string x) => !new ReadOnlySpan<string>(new string[6] { "WALK", "BIKE", "BUS", "METRO", "CAR", "TAXI" }).Contains(x)))
		{
			throw new ArgumentException("请选择市内交通方式。");
		}
		if (request.Origin.Status != "VERIFIED" || !request.Origin.Lng.HasValue || !request.Origin.Lat.HasValue)
		{
			throw new ArgumentException("请先从地点搜索中确认出发点。");
		}
		DateTime day = request.Date.ToDateTime(TimeOnly.MinValue);
		List<Occurrence> all = (from x in CalendarEngine.Expand(state, day, day.AddDays(1.0))
			orderby x.Start
			select x).ToList();
		if (all.Count == 0)
		{
			throw new ArgumentException("当天没有生效事项。");
		}
		if (all.Count > 12)
		{
			throw new ArgumentException("一次自动规划最多12项，请拆分事项或缩小生效范围。");
		}
		if (all.Any((Occurrence x) => x.Start < day || x.End > day.AddDays(1.0)))
		{
			throw new ArgumentException("当天含跨日事项，请先手动安排跨日部分。");
		}
		if (request.Earliest < day || request.Earliest >= day.AddDays(1.0))
		{
			throw new ArgumentException("最早出发时间须在当天。");
		}
		Dictionary<string, Place> places = state.Places.ToDictionary((Place x) => x.Id);
		foreach (Occurrence item6 in all)
		{
			if (item6.PlaceId == null || !places.TryGetValue(item6.PlaceId, out var value) || value.Status != "VERIFIED" || !value.Lng.HasValue || !value.Lat.HasValue)
			{
				throw new ArgumentException("请先确认“" + item6.Title + "”的市内地点。");
			}
		}
		Dictionary<string, List<RouteOption>> cache = new Dictionary<string, List<RouteOption>>();
		int queries = 0;
		List<ItineraryCandidate> completed = new List<ItineraryCandidate>();
		string[] labels = new string[3] { "交通用时较少", "已知费用优先（未知项另计）", "步行较少" };
		int policy;
		for (policy = 0; policy < 3; policy++)
		{
			List<Node> list = new List<Node>(1)
			{
				new Node(new List<PlannedStop>(), all, request.Origin, request.Earliest, request.Origin, request.Origin)
			};
			for (int depth = 0; depth < all.Count; depth++)
			{
				if (list.Count <= 0)
				{
					break;
				}
				List<Node> next = new List<Node>();
				foreach (Node node in list)
				{
					Occurrence fixedNext = (from x in node.Left
						where !Flexible(x)
						orderby x.Start
						select x).FirstOrDefault();
					List<Occurrence> list2 = (from x in node.Left.Where(Flexible)
						orderby Window(x).Item2
						select x).Take(3).ToList();
					if ((object)fixedNext != null)
					{
						list2.Add(fixedNext);
					}
					foreach (Occurrence item in list2)
					{
						Place destination = places[item.PlaceId];
						foreach (RouteOption item7 in await Options(node.At, destination, node.Time))
						{
							if ((item7.Uses("CAR") && !Same(node.At, node.Car)) || (item7.Uses("BIKE") && request.BikeResource == "OWN_BIKE" && !Same(node.At, node.Bike)))
							{
								continue;
							}
							int num = (item7.Uses("CAR") ? (Math.Clamp(request.ParkingMinutes, 0, 60) * 60) : 0);
							DateTime dateTime = node.Time.AddSeconds(item7.DurationSec + num);
							DateTime dateTime2;
							DateTime dateTime3;
							if (Flexible(item))
							{
								(DateTime, DateTime) tuple = Window(item);
								DateTime item2 = tuple.Item1;
								DateTime item3 = tuple.Item2;
								dateTime2 = ((dateTime > item2) ? dateTime : item2);
								dateTime3 = dateTime2 + (item.End - item.Start);
								if (dateTime3 > item3 || dateTime3 > day.AddDays(1.0) || ((object)fixedNext != null && dateTime3 > fixedNext.Start))
								{
									continue;
								}
							}
							else
							{
								dateTime2 = item.Start;
								dateTime3 = item.End;
								if (dateTime > dateTime2)
								{
									continue;
								}
							}
							PlannedStop item4 = new PlannedStop(item, dateTime2, dateTime3, item7, node.Time, dateTime, num);
							List<PlannedStop> stops = node.Stops;
							List<PlannedStop> list3 = new List<PlannedStop>(1 + stops.Count);
							list3.AddRange(stops);
							list3.Add(item4);
							next.Add(new Node(list3, node.Left.Where((Occurrence x) => x.Key != item.Key).ToList(), destination, dateTime3, item7.Uses("CAR") ? destination : node.Car, (item7.Uses("BIKE") && request.BikeResource == "OWN_BIKE") ? destination : node.Bike));
						}
					}
				}
				list = next.OrderBy((Node n) => Score(n, policy)).Take(6).ToList();
			}
			Node node2 = list.FirstOrDefault((Node x) => x.Left.Count == 0);
			if ((object)node2 != null)
			{
				completed.Add(new ItineraryCandidate
				{
					Title = labels[policy],
					Stops = node2.Stops
				});
			}
		}
		return completed.DistinctBy((ItineraryCandidate x) => string.Join("|", x.Stops.Select((PlannedStop s) => $"{s.Original.Key}:{s.Start:O}:{s.Route.Label}"))).ToList();
		bool Flexible(Occurrence x)
		{
			if (request.ReplanFlexible && request.EditableTables.Contains(x.Activity.TableId) && x.Activity.ConstraintType == "FLEXIBLE")
			{
				return x.ExceptionVersion == 0;
			}
			return false;
		}
		async Task<List<RouteOption>> Options(Place a, Place b, DateTime at)
		{
			if (Same(a, b))
			{
				return new List<RouteOption>(1)
				{
					new RouteOption
					{
						Label = "同一地点",
						CostComplete = true,
						KnownCostFen = 0,
						Segments = new List<RouteSegment>()
					}
				};
			}
			List<RouteOption> options = new List<RouteOption>();
			List<string> list4 = TransportPolicy.QueryModes(request.Modes);
			foreach (string mode in list4)
			{
				token.ThrowIfCancellationRequested();
				string key = $"{a.Id}|{b.Id}|{mode}|{at:yyyyMMddHHmm}";
				if (!cache.TryGetValue(key, out List<RouteOption> value2))
				{
					if (++queries > 100)
					{
						continue;
					}
					try
					{
						value2 = await provider.RoutesAsync(a, b, mode, at, token);
					}
					catch (InvalidOperationException ex)
					{
						value2 = new List<RouteOption>();
						string item5 = mode + " 查询不可用：" + ex.Message;
						if (!request.Warnings.Contains(item5))
						{
							request.Warnings.Add(item5);
						}
					}
					cache[key] = value2;
				}
				foreach (RouteOption item8 in value2)
				{
					if (TransportPolicy.Allows(item8, request.Modes))
					{
						RouteOption routeOption = JsonKit.Clone(item8);
						foreach (RouteSegment item9 in routeOption.Segments.Where((RouteSegment s) => s.Mode == "BIKE"))
						{
							item9.ResourceType = request.BikeResource;
						}
						if (routeOption.Uses("BIKE") && request.BikeResource == "OWN_BIKE")
						{
							routeOption.CostComplete = true;
							routeOption.KnownCostFen = 0;
							routeOption.CostNote = "自有自行车，不计购置维护费用";
						}
						options.Add(routeOption);
					}
				}
			}
			return options;
		}
		static double Score(Node n, int num2)
		{
			return num2 switch
			{
				1 => (double)n.Stops.Sum((PlannedStop s) => s.Route.KnownCostFen.GetValueOrDefault() + ((!s.Route.CostComplete) ? 50000 : 0)) + (double)n.Stops.Sum((PlannedStop s) => s.Route.DurationSec) / 60.0,
				2 => (double)n.Stops.Sum((PlannedStop s) => s.Route.WalkingM) + (double)n.Stops.Sum((PlannedStop s) => s.Route.DurationSec) / 100.0,
				_ => n.Stops.Sum((PlannedStop s) => s.Route.DurationSec + s.BufferSec),
			};
		}
		(DateTime, DateTime) Window(Occurrence x)
		{
			DateOnly? originDate = x.OriginDate;
			if (originDate.HasValue)
			{
				DateOnly valueOrDefault = originDate.GetValueOrDefault();
				RecurrenceRule recurrenceRule = state.Rules.Single((RecurrenceRule q) => q.ActivityId == x.Activity.Id);
				DateTime item5 = valueOrDefault.ToDateTime(TimeOnly.Parse(recurrenceRule.WindowFrom ?? recurrenceRule.StartTime));
				return (item5, item5.AddMinutes(recurrenceRule.WindowSpanMin ?? x.Activity.DurationMin));
			}
			return (x.Activity.WindowStart ?? x.Start, x.Activity.WindowEnd ?? x.End);
		}
	}

	private static bool Same(Place a, Place b)
	{
		if (!(a.Id == b.Id))
		{
			if (a.Lng.HasValue && b.Lng.HasValue && a.Lat.HasValue && b.Lat.HasValue && Math.Abs(a.Lng.Value - b.Lng.Value) < 1E-05)
			{
				return Math.Abs(a.Lat.Value - b.Lat.Value) < 1E-05;
			}
			return false;
		}
		return true;
	}
}
