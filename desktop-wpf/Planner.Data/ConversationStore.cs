using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Planner.Core;

namespace Planner.Data;

public sealed class ConversationStore(string directory)
{
	private readonly string path = Path.Combine(directory, "conversations.json");

	public GuidedConversation Load()
	{
		if (!File.Exists(path))
		{
			return new GuidedConversation();
		}
		if (new FileInfo(path).Length > 16777216)
		{
			throw new InvalidOperationException("本地对话记录过大，请先备份并整理，原文件未修改。");
		}
		try
		{
			GuidedConversation guidedConversation = JsonSerializer.Deserialize<GuidedConversation>(File.ReadAllText(path), JsonKit.Options) ?? throw new JsonException();
			Validate(guidedConversation);
			return guidedConversation;
		}
		catch (Exception ex) when ((ex is JsonException || ex is ArgumentException) ? true : false)
		{
			throw new InvalidOperationException("对话记录无法读取，原文件已保留，请从备份恢复或联系维护者。", ex);
		}
	}

	private static void Validate(GuidedConversation value)
	{
		if (value.Branches == null || value.Branches.Count == 0 || value.Batches == null || value.Branches.Any((ConversationBranch b) => b == null || b.Messages == null || b.Messages.Any((ConversationMessage m) =>
		{
			bool flag2 = m == null;
			if (!flag2)
			{
				string role = m.Role;
				bool flag3 = ((role == "user" || role == "assistant") ? true : false);
				flag2 = !flag3;
			}
			return flag2 || m.Content == null;
		})))
		{
			throw new ArgumentException("对话结构不完整。");
		}
		GuidedConversation guidedConversation = value;
		if (guidedConversation.Drafts == null)
		{
			Dictionary<string, string> dictionary = (guidedConversation.Drafts = new Dictionary<string, string>());
		}
		if (value.Drafts.Any((KeyValuePair<string, string> d) => d.Value == null || d.Value.Length > 8000))
		{
			throw new ArgumentException("会话草稿格式无效。");
		}
		if (value.Version < 0 || value.Branches.Select((ConversationBranch b) => b.Id).Distinct().Count() != value.Branches.Count)
		{
			throw new ArgumentException("对话版本或分支标识无效。");
		}
		foreach (ConversationBranch branch in value.Branches)
		{
			ConversationBranch conversationBranch = branch;
			if (conversationBranch.Memory == null)
			{
				AgentMemory agentMemory = (conversationBranch.Memory = new AgentMemory());
			}
			branch.Memory.Validate();
			foreach (ConversationMessage message in branch.Messages)
			{
				message.MemorySnapshot?.Validate();
			}
			if (branch.Messages.Any((ConversationMessage m) => string.IsNullOrWhiteSpace(m.Id)) || branch.Messages.Select((ConversationMessage m) => m.Id).Distinct().Count() != branch.Messages.Count)
			{
				throw new ArgumentException("对话消息标识无效。");
			}
			GuidedReply reply = branch.Reply;
			if (reply == null)
			{
				continue;
			}
			if (string.IsNullOrWhiteSpace(reply.Message) || reply.Summary == null || reply.Events == null || reply.Missing == null || reply.Events.Any((TripRequest e) => e == null) || reply.Missing.Any((string m) => m == null))
			{
				throw new ArgumentException("对话汇总结构不完整。");
			}
			if (!reply.Ready)
			{
				continue;
			}
			int count = reply.Events.Count;
			bool flag = ((count < 1 || count > 15) ? true : false);
			if (flag || reply.Missing.Count != 0)
			{
				throw new ArgumentException("已完成的对话缺少活动。");
			}
			foreach (TripRequest @event in reply.Events)
			{
				RepositoryPlanner.Validate(@event);
			}
		}
		if (value.Batches.Any((GeneratedBatchReceipt b) =>
		{
			bool flag2 = b == null || b.Activities == null || b.Tables == null || b.Places == null || b.Reminders == null || b.Plans == null || b.Legs == null || b.Segments == null || b.Scopes == null || b.Plans.Any((Plan x) => x == null) || b.Legs.Any((PlanLeg x) => x == null) || b.Segments.Any((RouteSegment x) => x == null) || b.Scopes.Any((PlanScope x) => x == null);
			if (!flag2)
			{
				int count2 = b.Activities.Count;
				bool flag3 = ((count2 < 1 || count2 > 15) ? true : false);
				flag2 = flag3;
			}
			return flag2 || b.Activities.Any((Activity a) => a == null || string.IsNullOrWhiteSpace(a.Id)) || b.Tables.Any((ScheduleTable t) => t == null) || b.Places.Any((Place p) => p == null) || b.Reminders.Any((ReminderSetting r) => r == null);
		}))
		{
			throw new ArgumentException("生成批次记录不完整。");
		}
		if (!value.Branches.Any((ConversationBranch b) => b.Id == value.ActiveBranchId) || (value.HasOpenConversation && value.Active.Closed))
		{
			value.ActiveBranchId = value.Branches.FirstOrDefault((ConversationBranch b) => !b.Closed)?.Id ?? value.Branches[0].Id;
		}
	}

	public void Save(GuidedConversation value)
	{
		Validate(value);
		Directory.CreateDirectory(directory);
		using (AcquireWriteLock())
		{
			if (Load().Version != value.Version)
			{
				throw new InvalidOperationException("对话已被其他窗口更新，请重新载入后继续。");
			}
			GuidedConversation guidedConversation = JsonKit.Clone(value);
			guidedConversation.Version++;
			string text = JsonSerializer.Serialize(guidedConversation, JsonKit.Options);
			if (Encoding.UTF8.GetByteCount(text) > 16777216)
			{
				throw new InvalidOperationException("对话记录已达容量限制，请备份整理后继续。");
			}
			string sourceFileName = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
			try
			{
				File.WriteAllText(sourceFileName, text);
				File.Move(sourceFileName, path, overwrite: true);
				value.Version = guidedConversation.Version;
			}
			finally
			{
				if (File.Exists(sourceFileName))
				{
					File.Delete(sourceFileName);
				}
			}
		}
	}

	private FileStream AcquireWriteLock()
	{
		try
		{
			return new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
		}
		catch (IOException innerException)
		{
			throw new InvalidOperationException("对话正在由其他窗口保存，请稍后重试。", innerException);
		}
	}
}
