using System;
using System.Collections.Generic;
using System.Linq;

namespace Planner.Core;

public sealed class ScopeContext
{
	public List<string> TableIds { get; set; } = new List<string>();

	public List<ScopeTable> Tables { get; set; } = new List<ScopeTable>();

	public DateTime From { get; set; }

	public DateTime Until { get; set; }

	public bool AllowFutureRules { get; set; }

	public List<ScopeItem> Items { get; set; } = new List<ScopeItem>();

	public static ScopeContext Build(PlannerState state, IEnumerable<string> tables, DateTime from, DateTime until, bool future = false)
	{
		List<string> ids = tables.Distinct().ToList();
		if (ids.Count == 0 || ids.Any((string id) => !state.Tables.Any((ScheduleTable t) => t.Id == id)))
		{
			throw new ArgumentException("请选择允许智能助手读取的行程表。");
		}
		return new ScopeContext
		{
			TableIds = ids,
			Tables = (from t in state.Tables
				where ids.Contains(t.Id)
				select new ScopeTable(t.Id, t.Name, t.Enabled, t.ValidFrom, t.ValidUntil)).ToList(),
			From = from,
			Until = until,
			AllowFutureRules = future,
			Items = (from o in CalendarEngine.ExpandForDisplay(state, @from, until, ids)
				select new ScopeItem(o.Activity.Id, o.OriginDate, o.Activity.TableId, o.Title, state.PlaceName(o.PlaceId), o.Start, o.End, o.Activity.ConstraintType)).ToList()
		};
	}
}
