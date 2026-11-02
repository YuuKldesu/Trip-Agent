using System.Collections.Generic;

namespace Planner.Core;

public sealed class AiReply
{
	public string Message { get; set; } = "";

	public List<AiOperation> Operations { get; set; } = new List<AiOperation>();
}
