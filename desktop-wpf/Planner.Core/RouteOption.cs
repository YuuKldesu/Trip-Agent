using System;
using System.Collections.Generic;
using System.Linq;

namespace Planner.Core;

public sealed class RouteOption
{
	public string Label { get; set; } = "";

	public int DurationSec { get; set; }

	public int DistanceM { get; set; }

	public int? KnownCostFen { get; set; }

	public bool CostComplete { get; set; }

	public string CostNote { get; set; } = "";

	public DateTime QueryAt { get; set; } = Clock.Now;

	public List<RouteSegment> Segments { get; set; } = new List<RouteSegment>();

	public int WalkingM => Segments.Where((RouteSegment s) => s.Mode == "WALK").Sum((RouteSegment s) => s.DistanceM.GetValueOrDefault());

	public string CostText
	{
		get
		{
			int? knownCostFen = KnownCostFen;
			if (knownCostFen.HasValue)
			{
				int valueOrDefault = knownCostFen.GetValueOrDefault();
				return $"已知 ¥{(double)valueOrDefault / 100.0:0.##}" + (CostComplete ? "" : " + 未知项");
			}
			return "费用暂无数据";
		}
	}

	public bool Uses(string mode)
	{
		return Segments.Any((RouteSegment s) => s.Mode == mode);
	}

	public override string ToString()
	{
		return $"{Label} · {Math.Ceiling((double)DurationSec / 60.0)}分钟 · {CostText}";
	}
}
