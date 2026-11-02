using System;

namespace Planner.Core;

public sealed class ConversationMessage : Entity
{
	public string Role { get; set; } = "user";

	public string Content { get; set; } = "";

	public DateTime CreatedAt { get; set; } = Clock.Now;

	public AgentMemory? MemorySnapshot { get; set; }
}
