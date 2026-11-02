using System;
using System.Collections.Generic;
using System.Linq;

namespace Planner.Core;

public sealed class ConversationRequests
{
	private readonly Dictionary<string, ConversationRequest> items = new Dictionary<string, ConversationRequest>();

	public ConversationRequest? Get(string id)
	{
		return items.GetValueOrDefault(id);
	}

	public ConversationRequest Start(string id, long version)
	{
		ConversationRequest conversationRequest = Get(id);
		if (conversationRequest != null && conversationRequest.Running)
		{
			throw new InvalidOperationException("此会话正在回答。");
		}
		ConversationRequest conversationRequest2 = new ConversationRequest(id, version);
		items[id] = conversationRequest2;
		return conversationRequest2;
	}

	public bool Owns(ConversationRequest item)
	{
		return Get(item.BranchId) == item;
	}

	public void Cancel(string id)
	{
		ConversationRequest conversationRequest = Get(id);
		if (conversationRequest != null && conversationRequest.Running)
		{
			conversationRequest.Status = "已取消回答，行程未改变。";
			conversationRequest.Cancellation.Cancel();
			conversationRequest.Running = false;
		}
	}

	public void Finish(ConversationRequest item)
	{
		item.Running = false;
		item.Dispose();
	}

	public void CancelAll()
	{
		foreach (string item in items.Keys.ToList())
		{
			Cancel(item);
		}
	}
}
