using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Planner.Core;

public static class GeneratedBatchService
{
	private static bool Same<T>(T a, T b)
	{
		return JsonSerializer.Serialize(a, JsonKit.Options) == JsonSerializer.Serialize(b, JsonKit.Options);
	}

	public static GeneratedBatchReceipt Capture(PlannerState before, PlannerState after, string branchId)
	{
		if (before.Activities.Any((Activity a) => !after.Activities.Any((Activity b) => b.Id == a.Id && Same(a, b))))
		{
			throw new ArgumentException("生成批次只能新增活动，不能覆盖已有行程。");
		}
		List<Activity> added = after.Activities.Where((Activity a) => before.Activities.All((Activity b) => b.Id != a.Id)).ToList();
		int count = added.Count;
		if ((count < 1 || count > 15) ? true : false)
		{
			throw new ArgumentException("生成批次需要包含1—15项新增活动。");
		}
		return new GeneratedBatchReceipt
		{
			BranchId = branchId,
			ExpectedRevision = before.Revision,
			Activities = JsonKit.Clone(added),
			Tables = JsonKit.Clone(after.Tables.Where((ScheduleTable t) => before.Tables.All((ScheduleTable b) => b.Id != t.Id)).ToList()),
			Places = JsonKit.Clone(after.Places.Where((Place p) => before.Places.All((Place b) => b.Id != p.Id)).ToList()),
			Plans = JsonKit.Clone(after.Plans.Where((Plan x) => before.Plans.All((Plan b) => b.Id != x.Id)).ToList()),
			Legs = JsonKit.Clone(after.Legs.Where((PlanLeg x) => before.Legs.All((PlanLeg b) => b.Id != x.Id)).ToList()),
			Segments = JsonKit.Clone(after.Segments.Where((RouteSegment x) => before.Segments.All((RouteSegment b) => b.Id != x.Id)).ToList()),
			Scopes = JsonKit.Clone(after.Scopes.Where((PlanScope x) => before.Scopes.All((PlanScope b) => b.PlanId != x.PlanId || b.TableId != x.TableId)).ToList()),
			Reminders = JsonKit.Clone(after.Reminders.Where((ReminderSetting r) => added.Any((Activity a) => a.Id == r.ActivityId)).ToList())
		};
	}

	public static PlannerState Undo(PlannerState state, GeneratedBatchReceipt receipt)
	{
		bool flag;
		switch (receipt.Status)
		{
		case "UNDONE":
		case "NOT_APPLIED":
		case "CONFLICT":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (flag || receipt.Activities.Count == 0)
		{
			throw new InvalidOperationException("此批次当前不能撤回，请先重新核对记录。");
		}
		HashSet<string> ids = receipt.Activities.Select((Activity a) => a.Id).ToHashSet();
		foreach (Activity original in receipt.Activities)
		{
			Activity activity = state.Activities.FirstOrDefault((Activity a) => a.Id == original.Id);
			if (activity != null && !Same(activity, original))
			{
				throw new InvalidOperationException("生成的行程已被手动修改，本次未撤回，请先核对修改。");
			}
		}
		HashSet<string> ownedLegs = receipt.Legs.Select((PlanLeg x) => x.Id).ToHashSet();
		HashSet<string> ownedPlans = receipt.Plans.Select((Plan x) => x.Id).ToHashSet();
		foreach (PlanLeg original2 in receipt.Legs)
		{
			PlanLeg planLeg = state.Legs.FirstOrDefault((PlanLeg x) => x.Id == original2.Id);
			if (planLeg != null)
			{
				PlanLeg planLeg2 = JsonKit.Clone(planLeg);
				if (planLeg2.ValidationStatus == "STALE")
				{
					planLeg2.ValidationStatus = original2.ValidationStatus;
				}
				if (!Same(planLeg2, original2))
				{
					throw new InvalidOperationException("生成的交通安排已被修改，本次未撤回。");
				}
			}
		}
		foreach (Plan original3 in receipt.Plans)
		{
			Plan plan = state.Plans.FirstOrDefault((Plan x) => x.Id == original3.Id);
			if (plan != null)
			{
				Plan plan2 = JsonKit.Clone(plan);
				if (plan2.Status == "STALE")
				{
					plan2.Status = original3.Status;
				}
				if (!Same(plan2, original3))
				{
					throw new InvalidOperationException("生成的路线方案已被修改，本次未撤回。");
				}
			}
		}
		if (state.Segments.Any((RouteSegment s) => ownedLegs.Contains(s.LegId) && !receipt.Segments.Any((RouteSegment old) => Same(old, s))) || receipt.Segments.Any((RouteSegment old) => state.Segments.Any((RouteSegment s) => s.Id == old.Id && !Same(s, old))) || state.Scopes.Any((PlanScope s) => ownedPlans.Contains(s.PlanId) && !receipt.Scopes.Any((PlanScope old) => Same(old, s))))
		{
			throw new InvalidOperationException("生成的交通明细已被修改或关联，本次未撤回。");
		}
		if (state.Legs.Any((PlanLeg l) =>
		{
			if (!ownedLegs.Contains(l.Id))
			{
				if (!ownedPlans.Contains(l.PlanId) && !ids.Contains(l.ToActivityId))
				{
					string fromActivityId = l.FromActivityId;
					if (fromActivityId != null)
					{
						return ids.Contains(fromActivityId);
					}
					return false;
				}
				return true;
			}
			return false;
		}) || state.Rules.Any((RecurrenceRule r) => ids.Contains(r.ActivityId)) || state.Exceptions.Any((ActivityException e) => ids.Contains(e.ActivityId)))
		{
			throw new InvalidOperationException("生成行程已被路线或周期安排使用，本次未撤回，请先处理关联安排。");
		}
		foreach (ReminderSetting now in state.Reminders.Where((ReminderSetting r) => ids.Contains(r.ActivityId)))
		{
			if (!receipt.Reminders.Any((ReminderSetting old) => Same(now, old)))
			{
				throw new InvalidOperationException("生成行程的提醒已被修改，本次未撤回。");
			}
		}
		if (receipt.Reminders.Any((ReminderSetting old) => state.Activities.Any((Activity a) => a.Id == old.ActivityId) && !state.Reminders.Any((ReminderSetting a) => Same(a, old))))
		{
			throw new InvalidOperationException("生成行程的提醒已被修改，本次未撤回。");
		}
		PlannerState next = state.Copy();
		next.Segments.RemoveAll((RouteSegment s) => ownedLegs.Contains(s.LegId));
		next.Legs.RemoveAll((PlanLeg l) => ownedLegs.Contains(l.Id));
		next.Scopes.RemoveAll((PlanScope s) => ownedPlans.Contains(s.PlanId) || (receipt.Scopes.Any((PlanScope old) => Same(old, s)) && !next.Legs.Any((PlanLeg l) => l.PlanId == s.PlanId && next.Activities.Any((Activity a) => a.TableId == s.TableId && (a.Id == l.ToActivityId || a.Id == l.FromActivityId)))));
		next.Plans.RemoveAll((Plan plan3) => ownedPlans.Contains(plan3.Id));
		foreach (string item in ids)
		{
			ChangeService.RemoveActivity(next, item);
		}
		foreach (ScheduleTable t in receipt.Tables)
		{
			ScheduleTable scheduleTable = next.Tables.FirstOrDefault((ScheduleTable x) => x.Id == t.Id);
			if (scheduleTable != null && Same(scheduleTable, t) && !next.Activities.Any((Activity a) => a.TableId == t.Id) && !next.Scopes.Any((PlanScope s) => s.TableId == t.Id))
			{
				next.Tables.Remove(scheduleTable);
			}
		}
		foreach (Place p in receipt.Places)
		{
			if (!next.Activities.Any((Activity a) => a.PlaceId == p.Id) && !next.Exceptions.Any((ActivityException e) => e.PlaceId == p.Id) && !next.Plans.Any((Plan plan3) => plan3.StartPlaceId == p.Id))
			{
				next.Places.RemoveAll((Place x) => x.Id == p.Id && Same(x, p));
			}
		}
		next.Revision++;
		return next;
	}

	public static void Reconcile(PlannerState state, GeneratedBatchReceipt receipt)
	{
		string status = receipt.Status;
		if ((!(status == "PENDING") && !(status == "UNDO_PENDING")) || 1 == 0)
		{
			return;
		}
		bool flag = receipt.Status == "UNDO_PENDING";
		int num = Enumerable.Count(receipt.Activities, (Activity a) => state.Activities.Any((Activity x) => x.Id == a.Id));
		GeneratedBatchReceipt generatedBatchReceipt = receipt;
		string status2;
		if (num == receipt.Activities.Count)
		{
			status2 = "APPLIED";
		}
		else if (num != 0)
		{
			status2 = "CONFLICT";
		}
		else
		{
			status2 = (flag ? "UNDONE" : "NOT_APPLIED");
		}
		generatedBatchReceipt.Status = status2;
	}
}
