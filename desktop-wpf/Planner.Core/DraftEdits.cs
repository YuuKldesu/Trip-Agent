using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Planner.Core;

public static class DraftEdits
{
	private static readonly HashSet<string> Allowed = new HashSet<string>
	{
		"day", "end_day", "title", "location", "place_ref", "duration", "start", "end", "earliest", "latest_end",
		"notes", "mode_in", "is_return", "reminder_enabled", "reminder_lead_min"
	};

	public static void EnsureIds(List<TripRequest> events)
	{
		if (events == null || events.Count > 15 || events.Any((TripRequest e) => e == null))
		{
			throw new ArgumentException("草稿活动格式无效。");
		}
		foreach (TripRequest @event in events)
		{
			if (string.IsNullOrWhiteSpace(@event.EventId))
			{
				@event.EventId = Guid.NewGuid().ToString("N");
			}
		}
		if (events.Any((TripRequest e) => e.EventId.Length > 100) || events.Select((TripRequest e) => e.EventId).Distinct().Count() != events.Count)
		{
			throw new ArgumentException("草稿活动编号无效或重复。");
		}
	}

	public static void Resolve(GuidedReply reply, AgentMemory memory)
	{
		if (reply.DraftPatches == null || reply.DraftPatches.Count > 30)
		{
			throw new ArgumentException("每轮最多30个草稿修改操作。");
		}
		string text;
		if (string.IsNullOrWhiteSpace(reply.DraftAction))
		{
			text = ((reply.Events.Count > 0) ? "replace" : "keep");
		}
		else
		{
			text = reply.DraftAction;
		}
		bool flag;
		switch (text)
		{
		case "keep":
		case "replace":
		case "patch":
		case "restore":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (!flag)
		{
			throw new ArgumentException("draft_action须为keep、replace、patch或restore。");
		}
		if ((text != "patch" && reply.DraftPatches.Count > 0) || (text != "replace" && reply.Events.Count > 0))
		{
			throw new ArgumentException("保留、局部修改和恢复时不要同时提交完整events。");
		}
		reply.DraftAction = text;
		if (text == "keep")
		{
			if (reply.Ready)
			{
				throw new ArgumentException("普通讨论不能声明ready=true；重新核对草稿请使用patch和空修改数组。");
			}
			reply.Changes = new List<DraftChange>();
			return;
		}
		List<TripRequest> list = JsonKit.Clone(memory.Draft);
		EnsureIds(list);
		Dictionary<string, MemoryFact> dictionary = null;
		List<TripRequest> list2;
		if (text == "restore")
		{
			if (memory.DraftHistory.Count == 0)
			{
				throw new ArgumentException("没有可以恢复的上一版草稿。");
			}
			List<DraftRevision> draftHistory = memory.DraftHistory;
			DraftRevision draftRevision = draftHistory[draftHistory.Count - 1];
			list2 = JsonKit.Clone(draftRevision.Events);
			dictionary = JsonKit.Clone(draftRevision.Facts);
		}
		else if (text == "replace")
		{
			list2 = JsonKit.Clone(reply.Events);
			foreach (TripRequest e in list2)
			{
				if (!list.Any((TripRequest old) => old.EventId == e.EventId))
				{
					e.EventId = "";
				}
			}
		}
		else
		{
			list2 = JsonKit.Clone(list);
			foreach (DraftPatch patch in reply.DraftPatches)
			{
				if (patch == null)
				{
					throw new ArgumentException("修改操作不能为空。");
				}
				if (patch.Op == "add")
				{
					if (patch.Event == null || !string.IsNullOrEmpty(patch.EventId))
					{
						throw new ArgumentException("新增活动需event且不能指定已有编号。");
					}
					TripRequest tripRequest = JsonKit.Clone(patch.Event);
					tripRequest.EventId = Guid.NewGuid().ToString("N");
					int num = patch.Position ?? list2.Count;
					if (num < 0 || num > list2.Count)
					{
						throw new ArgumentException("新增位置超出草稿范围。");
					}
					list2.Insert(num, tripRequest);
					continue;
				}
				int num2 = list2.FindIndex((TripRequest tripRequest2) => tripRequest2.EventId == patch.EventId);
				if (num2 < 0)
				{
					throw new ArgumentException("找不到要修改的活动编号，请使用CURRENT_MEMORY.draft中的event_id。");
				}
				if (patch.Op == "remove")
				{
					list2.RemoveAt(num2);
					continue;
				}
				if (patch.Op == "move")
				{
					int? position = patch.Position;
					if (position.HasValue)
					{
						int valueOrDefault = position.GetValueOrDefault();
						if (valueOrDefault >= 0 && valueOrDefault < list2.Count)
						{
							TripRequest item = list2[num2];
							list2.RemoveAt(num2);
							list2.Insert(valueOrDefault, item);
							continue;
						}
					}
					throw new ArgumentException("移动位置为从0开始的有效下标。");
				}
				if (patch.Op != "update" || patch.Fields.ValueKind != JsonValueKind.Object)
				{
					throw new ArgumentException("局部修改需op=update和fields对象。");
				}
				JsonObject jsonObject = JsonSerializer.SerializeToNode(list2[num2], JsonKit.Options).AsObject();
				HashSet<string> hashSet = new HashSet<string>();
				foreach (JsonProperty item2 in patch.Fields.EnumerateObject())
				{
					if (!Allowed.Contains(item2.Name) || !hashSet.Add(item2.Name))
					{
						throw new ArgumentException("不允许的草稿字段：" + item2.Name);
					}
					jsonObject[item2.Name] = JsonNode.Parse(item2.Value.GetRawText());
				}
				try
				{
					list2[num2] = jsonObject.Deserialize<TripRequest>(JsonKit.Options) ?? throw new JsonException();
				}
				catch (JsonException innerException)
				{
					throw new ArgumentException("局部修改字段类型无效，请检查日期、整数分钟和布尔值。", innerException);
				}
			}
		}
		EnsureIds(list2);
		if (list2.Any((TripRequest tripRequest2) => tripRequest2.Title == null || tripRequest2.Location == null || tripRequest2.Notes == null || tripRequest2.Title.Length > 100 || tripRequest2.Location.Length > 500 || tripRequest2.Notes.Length > 4000))
		{
			throw new ArgumentException("活动文字字段无效或过长。");
		}
		reply.Events = list2;
		if (reply.Ready)
		{
			if (list2.Count == 0 || reply.Missing.Count > 0)
			{
				throw new ArgumentException("可核对草稿须有活动且没有缺项。");
			}
			foreach (TripRequest item3 in list2)
			{
				RepositoryPlanner.Validate(item3);
			}
		}
		reply.Changes = Describe(list, list2);
		if (dictionary != null)
		{
			memory.Facts = dictionary;
		}
	}

	public static void Commit(GuidedReply reply, AgentMemory memory, AgentMemory before)
	{
		if (reply.DraftAction == "keep")
		{
			return;
		}
		bool flag = JsonSerializer.Serialize(before.Draft, JsonKit.Options) != JsonSerializer.Serialize(reply.Events, JsonKit.Options);
		if (reply.DraftAction == "restore")
		{
			memory.DraftHistory.RemoveAt(memory.DraftHistory.Count - 1);
		}
		else if (flag && before.Draft.Count > 0)
		{
			memory.DraftHistory.Add(new DraftRevision
			{
				Events = JsonKit.Clone(before.Draft),
				Facts = JsonKit.Clone(before.Facts)
			});
			if (memory.DraftHistory.Count > 5)
			{
				memory.DraftHistory.RemoveAt(0);
			}
		}
		memory.Draft = JsonKit.Clone(reply.Events);
		memory.DraftStatus = (reply.Ready ? "READY_FOR_REVIEW" : "INCOMPLETE");
	}

	public static List<DraftChange> Describe(List<TripRequest> before, List<TripRequest> next)
	{
		List<DraftChange> list = new List<DraftChange>();
		foreach (TripRequest item in before.Where((TripRequest tripRequest2) => next.All((TripRequest n) => n.EventId != tripRequest2.EventId)))
		{
			list.Add(new DraftChange
			{
				Title = item.Title,
				Detail = "已移除"
			});
		}
		for (int num = 0; num < next.Count; num++)
		{
			TripRequest e = next[num];
			TripRequest tripRequest = before.FirstOrDefault((TripRequest o) => o.EventId == e.EventId);
			if (tripRequest == null)
			{
				list.Add(new DraftChange
				{
					Title = e.Title,
					Detail = "已新增"
				});
				continue;
			}
			List<string> list2 = new List<string>();
			if (e.Day != tripRequest.Day)
			{
				list2.Add($"日期：{tripRequest.Day:yyyy-MM-dd} → {e.Day:yyyy-MM-dd}");
			}
			if (e.Start != tripRequest.Start)
			{
				list2.Add("开始：" + Minute(tripRequest.Start) + " → " + Minute(e.Start));
			}
			if (e.Duration != tripRequest.Duration)
			{
				list2.Add($"耗时：{tripRequest.Duration} → {e.Duration}分钟");
			}
			if (e.PlaceRef != tripRequest.PlaceRef || e.Location != tripRequest.Location)
			{
				list2.Add("地点：" + tripRequest.Location + " → " + e.Location);
			}
			if (before.FindIndex((TripRequest o) => o.EventId == e.EventId) != num)
			{
				list2.Add("顺序：第" + (num + 1) + "项");
			}
			if (JsonSerializer.Serialize(tripRequest, JsonKit.Options) != JsonSerializer.Serialize(e, JsonKit.Options) && list2.Count == 0)
			{
				list2.Add("活动细节已更新");
			}
			if (list2.Count > 0)
			{
				list.Add(new DraftChange
				{
					Title = e.Title,
					Detail = string.Join("；", list2)
				});
			}
		}
		return list;
	}

	private static string Minute(int? value)
	{
		if (value.HasValue)
		{
			int valueOrDefault = value.GetValueOrDefault();
			return $"{valueOrDefault / 60:00}:{valueOrDefault % 60:00}";
		}
		return "自动安排";
	}
}
