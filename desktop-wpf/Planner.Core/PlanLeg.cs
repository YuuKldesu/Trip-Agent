using System;

namespace Planner.Core;

public sealed class PlanLeg : Entity
{
	public int? KnownCostFen { get; set; }

	public bool CostComplete { get; set; }

	public string CostNote { get; set; } = "费用快照暂无数据";

	public string PlanId { get; set; } = "";

	public int Seq { get; set; }

	public string? FromActivityId { get; set; }

	public DateOnly? FromOriginDate { get; set; }

	public string ToActivityId { get; set; } = "";

	public DateOnly? ToOriginDate { get; set; }

	public DateTime DepartAt { get; set; }

	public DateTime ArriveAt { get; set; }

	public int BufferSec { get; set; }

	public DateTime QueryAt { get; set; }

	public DateTime? RequestedDepartAt { get; set; }

	public string ValidationStatus { get; set; } = "UNVERIFIED";
}
