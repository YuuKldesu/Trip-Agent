using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Planner.Core;

namespace Planner.Data;

public sealed class AgentToolSession(AgentMemory memory, PlannerState state, RepositoryMap map, IProgress<string>? progress = null)
{
	private int count;

	private readonly Dictionary<string, object> cache = new Dictionary<string, object>();

	public List<string> Errors { get; } = new List<string>();

	public async Task<object> ExecuteAsync(AgentToolRequest request, CancellationToken token)
	{
		if (request.Name == "compare_routes")
		{
			return await CompareRoutesAsync(request, token);
		}
		token.ThrowIfCancellationRequested();
		if (++count > 6)
		{
			return new
			{
				error = "本轮工具查询已达6次上限，请先询问用户后继续。"
			};
		}
		AgentMemory working = JsonKit.Clone(memory);
		try
		{
			if (request.Arguments.ValueKind != JsonValueKind.Object)
			{
				throw new ArgumentException("工具参数必须是对象。");
			}
			string cacheKey = request.Name + JsonSerializer.Serialize(request.Arguments, JsonKit.Options);
			if (cache.TryGetValue(cacheKey, out object value))
			{
				return value;
			}
			object obj;
			switch (request.Name)
			{
			case "search_places":
			{
				string query = Text("query");
				progress?.Report("正在查找地点：" + query);
				List<Place> list2 = (await map.SearchAsync(query, token)).Take(8).Select(JsonKit.Clone).ToList();
				token.ThrowIfCancellationRequested();
				working.Candidates.RemoveAll((PlaceCandidates c) => c.Query == query);
				if (working.Candidates.Count >= 16)
				{
					working.Candidates.RemoveAt(0);
				}
				working.Candidates.Add(new PlaceCandidates
				{
					Query = query,
					Places = list2,
					Source = (map.IsOnline ? "高德" : "本地示例")
				});
				obj = new
				{
					query = query,
					query_at = Clock.Now,
					requires_user_selection = true,
					candidates = list2.Select((Place p) => new
					{
						place_ref = p.Id,
						Name = p.Name,
						Address = p.Address,
						Source = p.Source,
						Status = p.Status
					}),
					note = "候选不是已确认地点，须由用户点击选择。"
				};
				break;
			}
			case "query_route":
			{
				string fromRef = Text("from_ref");
				string toRef = Text("to_ref");
				string mode = Text("mode");
				if (!working.SelectedPlaces.TryGetValue(fromRef, out Place from) || !working.SelectedPlaces.TryGetValue(toRef, out Place to))
				{
					throw new ArgumentException("路线端点必须来自用户已选择的地点引用，先搜索并请用户确认。");
				}
				bool flag;
				switch (mode)
				{
				case "公交":
				case "步行":
				case "骑行":
				case "驾车":
				case "打车":
					flag = true;
					break;
				default:
					flag = false;
					break;
				}
				if (!flag)
				{
					throw new ArgumentException("仅支持市内公交、步行、骑行、驾车或打车。");
				}
				if (!DateTimeOffset.TryParse(Text("depart_at"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
				{
					throw new ArgumentException("请提供带日期的出发时间。");
				}
				DateTime depart = result.ToOffset(TimeSpan.FromHours(8)).DateTime;
				progress?.Report("正在计算路线：" + from.Name + " → " + to.Name);
				TravelEstimate travelEstimate = await map.TravelAsync(from, to, (mode == "打车") ? "驾车" : mode, depart, token);
				token.ThrowIfCancellationRequested();
				AgentRoute agentRoute = new AgentRoute
				{
					FromRef = fromRef,
					ToRef = toRef,
					Mode = mode,
					DepartAt = depart,
					Minutes = travelEstimate.Minutes,
					Source = travelEstimate.Source,
					KnownCostFen = ((mode == "打车") ? ((int?)null) : travelEstimate.Option?.KnownCostFen),
					CostComplete = (mode != "打车" && (travelEstimate.Option?.CostComplete ?? false)),
					Option = ((travelEstimate.Option == null) ? null : JsonKit.Clone(travelEstimate.Option))
				};
				working.Routes.RemoveAll((AgentRoute r) => r.FromRef == fromRef && r.ToRef == toRef && r.Mode == mode && r.DepartAt == depart);
				if (working.Routes.Count >= 32)
				{
					working.Routes.RemoveAt(0);
				}
				working.Routes.Add(agentRoute);
				obj = new
				{
					from_ref = fromRef,
					to_ref = toRef,
					from = from.Name,
					to = to.Name,
					mode = mode,
					depart_at = depart,
					arrival_at = depart.AddMinutes(agentRoute.Minutes),
					minutes = agentRoute.Minutes,
					source = agentRoute.Source,
					query_at = agentRoute.QueryAt,
					known_cost_fen = ((mode == "打车") ? ((int?)null) : agentRoute.KnownCostFen),
					cost_complete = (mode != "打车" && agentRoute.CostComplete),
					crowding = "未知",
					note = ((mode == "打车") ? "使用驾车路线，不能将道路费用视为打车报价。" : "路线为查询时快照，未知费用不补猜。")
				};
				break;
			}
			case "read_schedule":
			{
				if (!DateOnly.TryParseExact(Text("from_day"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var first) || !DateOnly.TryParseExact(Text("to_day"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var last) || last < first || last.DayNumber - first.DayNumber >= 7 || last == DateOnly.MaxValue)
				{
					throw new ArgumentException("日程读取需提供1—7天的有效日期范围。");
				}
				progress?.Report("正在核对已有行程：" + first.ToString("MM-dd") + "—" + last.ToString("MM-dd"));
				List<Occurrence> list = CalendarEngine.Expand(state, first.ToDateTime(TimeOnly.MinValue), last.AddDays(1).ToDateTime(TimeOnly.MinValue));
				foreach (Occurrence item in list.Take(80))
				{
					string id = item.PlaceId;
					if (id != null && !working.SelectedPlaces.ContainsKey(id) && working.SelectedPlaces.Count < 30)
					{
						Place place = state.Places.FirstOrDefault((Place p) => p.Id == id);
						if (place != null)
						{
							working.SelectedPlaces[id] = JsonKit.Clone(place);
						}
					}
				}
				ScheduleRead scheduleRead = new ScheduleRead
				{
					FromDay = first,
					ToDay = last,
					Revision = state.Revision,
					Truncated = (list.Count > 80),
					Events = list.Take(80).Select((Occurrence o) =>
					{
						ScheduleReadItem scheduleReadItem = new ScheduleReadItem
						{
							Title = o.Title,
							Start = o.Start,
							End = o.End,
							Table = state.Tables.FirstOrDefault((ScheduleTable t) => t.Id == o.Activity.TableId)?.Name,
							Place = state.Places.FirstOrDefault((Place p) => p.Id == o.PlaceId)?.Name
						};
						string placeId = o.PlaceId;
						scheduleReadItem.PlaceRef = ((placeId != null && working.SelectedPlaces.ContainsKey(placeId)) ? placeId : null);
						return scheduleReadItem;
					}).ToList()
				};
				working.ScheduleReads.RemoveAll((ScheduleRead r) => r.FromDay == first && r.ToDay == last);
				if (working.ScheduleReads.Count >= 8)
				{
					working.ScheduleReads.RemoveAt(0);
				}
				working.ScheduleReads.Add(scheduleRead);
				obj = new
				{
					schedule = scheduleRead,
					conflicts = from c in CalendarEngine.Conflicts(list).Take(30)
						select c.Message
				};
				break;
			}
			default:
				throw new ArgumentException("未知工具，仅允许search_places、query_route、compare_routes、read_schedule。");
			}
			if (JsonSerializer.Serialize(obj, JsonKit.Options).Length > 24000)
			{
				throw new ArgumentException("结果过多，请缩小查询范围。");
			}
			AgentPrompt.MemoryJson(working);
			token.ThrowIfCancellationRequested();
			memory.Candidates = working.Candidates;
			memory.SelectedPlaces = working.SelectedPlaces;
			memory.Routes = working.Routes;
			memory.ScheduleReads = working.ScheduleReads;
			cache[cacheKey] = obj;
			return obj;
		}
		catch (MapServiceException ex)
		{
			if (!Errors.Contains(ex.Message))
			{
				Errors.Add(ex.Message);
			}
			progress?.Report(ex.Message);
			return new
			{
				error = ex.Message,
				query_at = Clock.Now
			};
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception ex3) when ((ex3 is ArgumentException || ex3 is InvalidOperationException || ex3 is HttpRequestException || ex3 is JsonException || ex3 is FormatException) ? true : false)
		{
			string error;
			if (ex3 is HttpRequestException)
			{
				error = "工具网络查询失败，请稍后重试。";
			}
			else
			{
				bool flag2 = ((ex3 is JsonException || ex3 is FormatException) ? true : false);
				error = (flag2 ? "工具返回格式异常，请重新查询。" : ex3.Message);
			}
			return new
			{
				error = error,
				query_at = Clock.Now
			};
		}
		string Text(string key)
		{
			if (!request.Arguments.TryGetProperty(key, out var value2) || value2.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value2.GetString()) || value2.GetString().Length > 200)
			{
				throw new ArgumentException("缺少有效工具参数：" + key);
			}
			return value2.GetString();
		}
	}

	private async Task<object> CompareRoutesAsync(AgentToolRequest request, CancellationToken token)
	{
		token.ThrowIfCancellationRequested();
		JsonElement arguments = request.Arguments;
		JsonElement value = default;
		bool flag = arguments.ValueKind != JsonValueKind.Object || !arguments.TryGetProperty("modes", out value) || value.ValueKind != JsonValueKind.Array;
		if (!flag)
		{
			int arrayLength = value.GetArrayLength();
			bool flag2 = ((arrayLength < 1 || arrayLength > 3) ? true : false);
			flag = flag2;
		}
		if (flag)
		{
			return new
			{
				error = "比较路线需modes数组，包含1—3种市内交通方式。"
			};
		}
		List<string> list = (from m in value.EnumerateArray()
			select (m.ValueKind != JsonValueKind.String) ? null : m.GetString()).ToList();
		if (list.Any((string m) =>
		{
			bool flag3;
			switch (m)
			{
			case "公交":
			case "步行":
			case "骑行":
			case "驾车":
			case "打车":
				flag3 = true;
				break;
			default:
				flag3 = false;
				break;
			}
			return !flag3;
		}) || list.Distinct().Count() != list.Count)
		{
			return new
			{
				error = "交通方式无效或重复。"
			};
		}
		if (count + list.Count > 6)
		{
			return new
			{
				error = "剩余查询额度不足以完成比较，请减少方式后重试。"
			};
		}
		if (!arguments.TryGetProperty("from_ref", out var from) || !arguments.TryGetProperty("to_ref", out var to) || !arguments.TryGetProperty("depart_at", out var depart))
		{
			return new
			{
				error = "请提供起终点引用和出发时间。"
			};
		}
		List<object> results = new List<object>();
		foreach (string mode in list)
		{
			JsonElement arguments2 = JsonSerializer.SerializeToElement(new
			{
				from_ref = from,
				to_ref = to,
				depart_at = depart,
				mode = mode
			});
			results.Add(new
			{
				mode = mode,
				result = await ExecuteAsync(new AgentToolRequest
				{
					Name = "query_route",
					Arguments = arguments2
				}, token)
			});
		}
		return new
		{
			comparisons = results,
			note = "各方式独立查询，失败方式不影响其他结果；费用不全时不能断言最便宜。"
		};
	}
}
