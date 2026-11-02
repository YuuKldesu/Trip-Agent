using System;
using System.Collections.Generic;
using System.Linq;

namespace Planner.Core;

public sealed class ItineraryCandidate
{
	public string Title { get; set; } = "";

	public List<PlannedStop> Stops { get; set; } = new List<PlannedStop>();

	public int TravelSeconds => Stops.Sum((PlannedStop s) => s.Route.DurationSec + s.BufferSec);

	public int? KnownCostFen
	{
		get
		{
			if (!Stops.Any((PlannedStop s) => s.Route.KnownCostFen.HasValue))
			{
				return null;
			}
			return Stops.Sum((PlannedStop s) => s.Route.KnownCostFen.GetValueOrDefault());
		}
	}

	public bool CostComplete => Stops.All((PlannedStop s) => s.Route.CostComplete);

	public int WalkingM => Stops.Sum((PlannedStop s) => s.Route.WalkingM);

	public string Summary
	{
		get
		{
			string text = $"交通约 {Math.Ceiling((double)TravelSeconds / 60.0)} 分钟 · ";
			int? knownCostFen = KnownCostFen;
			string text2;
			if (knownCostFen.HasValue)
			{
				int valueOrDefault = knownCostFen.GetValueOrDefault();
				text2 = $"已知费用 ¥{(double)valueOrDefault / 100.0:0.##}";
			}
			else
			{
				text2 = "费用暂无数据";
			}
			return text + text2 + (CostComplete ? "" : "，另有未知项") + $" · 步行 {WalkingM} 米";
		}
	}
}
