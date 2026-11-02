using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Planner.Core;

public sealed class GuidedConversation
{
	public long Version { get; set; }

	public string ActiveBranchId { get; set; }

	public List<ConversationBranch> Branches { get; set; } = new List<ConversationBranch>();

	public List<GeneratedBatchReceipt> Batches { get; set; } = new List<GeneratedBatchReceipt>();

	public Dictionary<string, string> Drafts { get; set; } = new Dictionary<string, string>();

	[JsonIgnore]
	public ConversationBranch Active => Branches.First((ConversationBranch b) => b.Id == ActiveBranchId);

	[JsonIgnore]
	public bool HasOpenConversation => Branches.Any((ConversationBranch b) => !b.Closed);

	public GuidedConversation()
	{
		ConversationBranch conversationBranch = new ConversationBranch();
		Branches.Add(conversationBranch);
		ActiveBranchId = conversationBranch.Id;
	}

	public void RenameBranch(string id, string name)
	{
		name = name.Trim();
		int length = name.Length;
		bool flag = ((length < 1 || length > 60) ? true : false);
		if (flag || name.Any(char.IsControl))
		{
			throw new ArgumentException("会话名称请填写1—60字。");
		}
		Branches.Single((ConversationBranch b) => b.Id == id).Name = name;
	}

	public void CloseBranch(string id)
	{
		int num = Branches.FindIndex((ConversationBranch b) => b.Id == id);
		if (num < 0)
		{
			throw new ArgumentException("会话不存在。");
		}
		Branches[num].Closed = true;
		if (ActiveBranchId == id)
		{
			ActiveBranchId = Branches.Skip(num + 1).FirstOrDefault((ConversationBranch b) => !b.Closed)?.Id ?? Branches.Take(num).LastOrDefault((ConversationBranch b) => !b.Closed)?.Id ?? id;
		}
	}

	public void OpenBranch(string id)
	{
		Branches.Single((ConversationBranch b) => b.Id == id).Closed = false;
		ActiveBranchId = id;
	}

	public List<string> CloseTargets(string id, string mode)
	{
		return TabClosing.Targets((from b in Branches
			where !b.Closed
			select b.Id).ToList(), id, mode);
	}

	public void CloseMany(string id, string mode)
	{
		foreach (string item in CloseTargets(id, mode))
		{
			CloseBranch(item);
		}
	}

	public ConversationBranch NewBranch()
	{
		ConversationBranch conversationBranch = new ConversationBranch
		{
			Name = "新对话 " + (Branches.Count + 1)
		};
		Branches.Add(conversationBranch);
		ActiveBranchId = conversationBranch.Id;
		return conversationBranch;
	}

	public ConversationMessage AddUser(string text)
	{
		if (Active.Closed)
		{
			throw new InvalidOperationException("请先打开或新建会话。");
		}
		text = text.Trim();
		int length = text.Length;
		if ((length < 1 || length > 8000) ? true : false)
		{
			throw new ArgumentException("每条消息请填写1—8000字。");
		}
		if (Active.Messages.Count >= 100)
		{
			throw new ArgumentException("此分支已较长，请新建对话后继续。");
		}
		Active.Memory.Routes.Clear();
		if (Active.Memory.Draft.Count > 0)
		{
			Active.Memory.DraftStatus = "STALE";
		}
		ConversationMessage conversationMessage = new ConversationMessage
		{
			Content = text
		};
		Active.Messages.Add(conversationMessage);
		Active.Reply = null;
		Active.Version++;
		return conversationMessage;
	}

	public void AddAssistant(GuidedReply reply)
	{
		if (Active.Closed)
		{
			throw new InvalidOperationException("会话已关闭。");
		}
		if (reply.Memory != null)
		{
			reply.Memory.Validate();
			Active.Memory = JsonKit.Clone(reply.Memory);
		}
		Active.Messages.Add(new ConversationMessage
		{
			Role = "assistant",
			Content = reply.Message,
			MemorySnapshot = JsonKit.Clone(Active.Memory)
		});
		Active.Reply = JsonKit.Clone(reply);
		Active.Version++;
	}

	public bool TryAddAssistant(string branchId, long expectedVersion, GuidedReply reply)
	{
		ConversationBranch conversationBranch = Branches.SingleOrDefault((ConversationBranch b) => b.Id == branchId);
		if (conversationBranch == null || conversationBranch.Closed || conversationBranch.Version != expectedVersion || conversationBranch.Messages.LastOrDefault()?.Role != "user")
		{
			return false;
		}
		if (reply.Memory != null)
		{
			reply.Memory.Validate();
			conversationBranch.Memory = JsonKit.Clone(reply.Memory);
		}
		conversationBranch.Messages.Add(new ConversationMessage
		{
			Role = "assistant",
			Content = reply.Message,
			MemorySnapshot = JsonKit.Clone(conversationBranch.Memory)
		});
		conversationBranch.Reply = JsonKit.Clone(reply);
		conversationBranch.Version++;
		return true;
	}

	public ConversationMessage ConfirmPlace(string candidateId)
	{
		Place place = Active.Memory.Candidates.SelectMany((PlaceCandidates c) => c.Places).FirstOrDefault((Place p) => p.Id == candidateId) ?? throw new ArgumentException("地点候选已失效，请重新搜索。");
		if (!Active.Memory.SelectedPlaces.ContainsKey(place.Id) && Active.Memory.SelectedPlaces.Count >= 30)
		{
			throw new ArgumentException("已选地点过多，请新建对话。");
		}
		ConversationMessage conversationMessage = AddUser("我确认选择地点：" + place?.ToString() + "（地点引用：" + place.Id + "）");
		Active.Memory.SelectedPlaces[place.Id] = JsonKit.Clone(place);
		Active.Memory.Candidates.RemoveAll((PlaceCandidates c) => c.Places.Any((Place p) => p.Id == place.Id));
		conversationMessage.MemorySnapshot = JsonKit.Clone(Active.Memory);
		return conversationMessage;
	}

	public ConversationBranch ForkAt(string messageId, string replacement)
	{
		ConversationBranch active = Active;
		int num = active.Messages.FindIndex((ConversationMessage m) => m.Id == messageId && m.Role == "user");
		if (num < 0)
		{
			throw new ArgumentException("只能编辑当前分支中已发送的用户消息。");
		}
		replacement = replacement.Trim();
		int length = replacement.Length;
		if ((length < 1 || length > 8000) ? true : false)
		{
			throw new ArgumentException("消息应为1—8000字。");
		}
		ConversationBranch conversationBranch = new ConversationBranch
		{
			Name = $"分支 {Branches.Count + 1} · 重写第 {active.Messages.Take(num + 1).Count((ConversationMessage m) => m.Role == "user")} 条",
			Messages = active.Messages.Take(num).Select(JsonKit.Clone).ToList()
		};
		conversationBranch.Memory = JsonKit.Clone(conversationBranch.Messages.LastOrDefault((ConversationMessage m) => m.MemorySnapshot != null)?.MemorySnapshot ?? new AgentMemory());
		Branches.Add(conversationBranch);
		ActiveBranchId = conversationBranch.Id;
		AddUser(replacement);
		return conversationBranch;
	}
}
