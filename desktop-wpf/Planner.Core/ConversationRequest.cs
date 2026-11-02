using System;
using System.Threading;

namespace Planner.Core;

public sealed class ConversationRequest(string branchId, long version) : IDisposable
{
	public string BranchId { get; } = branchId;

	public long Version { get; } = version;

	public CancellationTokenSource Cancellation { get; } = new CancellationTokenSource();

	public bool Running { get; internal set; } = true;

	public string Status { get; set; } = "正在理解并补齐需求，尚未修改行程…";

	public string Failure { get; set; } = "";

	public void Dispose()
	{
		Cancellation.Dispose();
	}
}
