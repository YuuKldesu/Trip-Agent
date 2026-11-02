using System;
using System.Collections.Generic;
using System.Linq;

namespace Planner.Core;

public sealed class AgentMemory
{
	public Dictionary<string, MemoryFact> Facts { get; set; } = new Dictionary<string, MemoryFact>();

	public Dictionary<string, Place> SelectedPlaces { get; set; } = new Dictionary<string, Place>();

	public List<PlaceCandidates> Candidates { get; set; } = new List<PlaceCandidates>();

	public List<AgentRoute> Routes { get; set; } = new List<AgentRoute>();

	public List<ScheduleRead> ScheduleReads { get; set; } = new List<ScheduleRead>();

	public List<TripRequest> Draft { get; set; } = new List<TripRequest>();

	public string DraftStatus { get; set; } = "NONE";

	public List<DraftRevision> DraftHistory { get; set; } = new List<DraftRevision>();

	public List<string> Missing { get; set; } = new List<string>();

	public void Apply(IReadOnlyList<MemoryUpdate> updates, IReadOnlyList<ConversationMessage> messages)
	{
		if (updates.Count > 40)
		{
			throw new ArgumentException("本轮记忆更新过多。");
		}
		Dictionary<string, MemoryFact> dictionary = JsonKit.Clone(Facts);
		foreach (MemoryUpdate u in updates)
		{
			if (u == null || string.IsNullOrWhiteSpace(u.Key) || u.Key.Length > 100 || string.IsNullOrWhiteSpace(u.Value) || u.Value.Length > 2000 || string.IsNullOrWhiteSpace(u.Evidence) || u.Evidence.Length > 2000)
			{
				throw new ArgumentException("记忆更新格式不完整。");
			}
			ConversationMessage conversationMessage = messages.FirstOrDefault((ConversationMessage m) => m.Id == u.SourceMessageId);
			if (conversationMessage == null || !conversationMessage.Content.Contains(u.Evidence, StringComparison.Ordinal) || (u.Confirmed && conversationMessage.Role != "user"))
			{
				throw new ArgumentException("记忆缺少当前分支用户原文依据，请重新确认。");
			}
			if (u.Confirmed && !MemoryEvidence.Matches(u, conversationMessage))
			{
				throw new ArgumentException("确认值与用户原文不一致；请逐字记录原文，或使用可验证的日期/分钟规范化值。不能确认的推断请作为独立建议。");
			}
			if (dictionary.TryGetValue(u.Key, out var value) && value.Confirmed && !u.Confirmed)
			{
				throw new ArgumentException("建议不能覆盖已确认需求，请使用独立建议字段。");
			}
			if (dictionary.TryGetValue(u.Key, out var prior) && messages.ToList().FindIndex((ConversationMessage m) => m.Id == prior.SourceMessageId) > messages.ToList().FindIndex((ConversationMessage m) => m.Id == u.SourceMessageId))
			{
				throw new ArgumentException("不能用更早的消息覆盖新的需求。");
			}
			dictionary[u.Key] = new MemoryFact
			{
				Value = u.Value,
				SourceMessageId = u.SourceMessageId,
				Evidence = u.Evidence,
				Confirmed = u.Confirmed
			};
		}
		if (dictionary.Count > 80)
		{
			throw new ArgumentException("行程记忆已达上限，请新建对话。");
		}
		Facts = dictionary;
	}

	public void Validate()
	{
		DraftEdits.EnsureIds(Draft);
		if (DraftHistory == null)
		{
			List<DraftRevision> list = (DraftHistory = new List<DraftRevision>());
		}
		if (DraftHistory.Count > 5 || DraftHistory.Any((DraftRevision r) => r == null || r.Events == null || r.Events.Count > 15 || r.Events.Any((TripRequest e) => e == null) || r.Facts == null || r.Facts.Count > 80))
		{
			throw new ArgumentException("草稿历史格式无效。");
		}
		if (ScheduleReads == null || ScheduleReads.Count > 8 || ScheduleReads.Any((ScheduleRead r) => r == null || r.Events == null || r.Events.Count > 80) || Facts == null || SelectedPlaces == null || Candidates == null || Routes == null || Draft == null || Missing == null || Facts.Count > 80 || SelectedPlaces.Count > 30 || Candidates.Count > 16 || Routes.Count > 32 || Draft.Count > 15 || Facts.Any((KeyValuePair<string, MemoryFact> x) => x.Value == null || x.Key.Length > 100 || x.Value.Value == null || x.Value.Value.Length > 2000) || SelectedPlaces.Any((KeyValuePair<string, Place> x) => x.Value == null) || Candidates.Any((PlaceCandidates x) => x == null || x.Places == null || x.Places.Count > 8 || x.Places.Any((Place p) => p == null)))
		{
			throw new ArgumentException("行程记忆格式无效。");
		}
	}
}
