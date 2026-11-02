using System.Collections.Generic;

namespace Planner.Core;

public sealed class ConversationBranch : Entity
{
	public string Name { get; set; } = "初始对话";

	public List<ConversationMessage> Messages { get; set; } = new List<ConversationMessage>();

	public GuidedReply? Reply { get; set; }

	public long Version { get; set; }

	public AgentMemory Memory { get; set; } = new AgentMemory();

	public bool Closed { get; set; }
}
