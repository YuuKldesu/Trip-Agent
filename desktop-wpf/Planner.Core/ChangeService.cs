using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Planner.Core;

public static class ChangeService
{
	public static PlannerState Apply(PlannerState state, IEnumerable<EditCommand> commands, long expectedRevision)
	{
		if (state.Revision != expectedRevision)
		{
			throw new InvalidOperationException("行程已发生变化，请重新预览后确认。");
		}
		List<EditCommand> list = commands.ToList();
		foreach (IGrouping<string, EditCommand> group in from c in list
			group c by c.Activity.Id)
		{
			Activity original = state.Activities.SingleOrDefault((Activity x) => x.Id == group.Key);
			if (original != null && group.Any((EditCommand c) => c.Activity.Version != original.Version))
			{
				throw new InvalidOperationException("该活动已被修改，请重新打开。");
			}
			if (group.Count() > 1 && (original?.Kind != "WEEKLY" || group.Any((EditCommand c) => c.Scope == "FUTURE" || !c.OriginDate.HasValue) || group.Select((EditCommand c) => c.OriginDate).Distinct().Count() != group.Count()))
			{
				throw new ArgumentException("同一活动的批次操作范围重叠，请分别确认。");
			}
		}
		PlannerState plannerState = state.Copy();
		foreach (EditCommand item in list)
		{
			EditCommand editCommand = JsonKit.Clone(item);
			Activity a = editCommand.Activity;
			Activity existing = plannerState.Activities.SingleOrDefault((Activity x) => x.Id == a.Id);
			if (existing?.Kind == "WEEKLY" && a.Kind != "WEEKLY" && editCommand.Action != "DELETE")
			{
				throw new ArgumentException("周期编辑保留重复类型；需要单次活动时请新建，或仅修改当前实例。");
			}
			if (existing?.Kind == "WEEKLY" && editCommand.OriginDate.HasValue && editCommand.Scope != "FUTURE" && a.TableId != existing.TableId && editCommand.Action != "DELETE")
			{
				throw new ArgumentException("单次覆盖不能更换所属表；请调整整套规则，或新建目标表中的单次活动。");
			}
			int reminderLeadMin = editCommand.ReminderLeadMin;
			if ((reminderLeadMin < 0 || reminderLeadMin > 10080) ? true : false)
			{
				throw new ArgumentException("提醒提前分钟应为0至10080。");
			}
			string action = editCommand.Action;
			if ((!(action == "UPSERT") && !(action == "DELETE")) || 1 == 0)
			{
				throw new ArgumentException("未知编辑操作。");
			}
			if (editCommand.Action == "DELETE")
			{
				if (existing == null)
				{
					throw new ArgumentException("要删除的活动已不存在。");
				}
				if (existing.Kind == "WEEKLY")
				{
					DateOnly? originDate = editCommand.OriginDate;
					if (originDate.HasValue)
					{
						DateOnly origin = originDate.GetValueOrDefault();
						if (editCommand.Scope != "FUTURE")
						{
							if (!plannerState.Rules.Single((RecurrenceRule r) => r.ActivityId == existing.Id).Matches(origin))
							{
								throw new ArgumentException("此日期不是原周期实例。");
							}
							plannerState.Exceptions.RemoveAll((ActivityException e) => e.ActivityId == existing.Id && e.OriginDate == origin);
							plannerState.Exceptions.Add(new ActivityException
							{
								ActivityId = existing.Id,
								OriginDate = origin,
								Kind = "CANCEL"
							});
							existing.Version++;
							goto IL_0584;
						}
					}
				}
				if (existing.Kind == "WEEKLY" && editCommand.Scope == "FUTURE")
				{
					DateOnly? originDate = editCommand.SplitDate;
					if (originDate.HasValue)
					{
						DateOnly cutoff = originDate.GetValueOrDefault();
						RecurrenceRule recurrenceRule = plannerState.Rules.Single((RecurrenceRule r) => r.ActivityId == existing.Id);
						if (cutoff <= recurrenceRule.ValidFrom)
						{
							RemoveActivity(plannerState, existing.Id);
						}
						else
						{
							recurrenceRule.ValidUntil = cutoff.AddDays(-1);
							plannerState.Exceptions.RemoveAll((ActivityException e) => e.ActivityId == existing.Id && e.OriginDate >= cutoff);
							existing.Version++;
						}
						goto IL_0584;
					}
				}
				RemoveActivity(plannerState, existing.Id);
				goto IL_0584;
			}
			if (editCommand.Place != null)
			{
				Place place = editCommand.Place;
				Place place2 = plannerState.Places.FirstOrDefault((Place x) => x.Id == place.Id);
				if (place2 == null)
				{
					plannerState.Places.Add(place);
				}
				else
				{
					plannerState.Places[plannerState.Places.IndexOf(place2)] = place;
				}
				a.PlaceId = place.Id;
			}
			ValidateActivity(plannerState, a, editCommand.Rule, existing);
			if (existing?.Kind == "WEEKLY")
			{
				DateOnly? originDate = editCommand.OriginDate;
				if (originDate.HasValue)
				{
					DateOnly date = originDate.GetValueOrDefault();
					if (editCommand.Scope != "FUTURE")
					{
						if (!plannerState.Rules.Single((RecurrenceRule r) => r.ActivityId == a.Id).Matches(date))
						{
							throw new ArgumentException("此日期不属于原周期规则。");
						}
						if (!a.StartAt.HasValue || !a.EndAt.HasValue || a.EndAt <= a.StartAt)
						{
							throw new ArgumentException("请填写本次活动的起止时间。");
						}
						ActivityException ex = plannerState.Exceptions.FirstOrDefault((ActivityException e) => e.ActivityId == a.Id && e.OriginDate == date);
						plannerState.Exceptions.RemoveAll((ActivityException e) => e.ActivityId == a.Id && e.OriginDate == date);
						plannerState.Exceptions.Add(new ActivityException
						{
							Id = (ex?.Id ?? Guid.NewGuid().ToString("N")),
							ActivityId = a.Id,
							OriginDate = date,
							StartAt = a.StartAt,
							EndAt = a.EndAt,
							Title = a.Title,
							PlaceId = a.PlaceId,
							PlaceOverride = true,
							Version = (ex?.Version ?? 0) + 1
						});
						existing.Version++;
						goto IL_09bf;
					}
				}
			}
			if (existing?.Kind == "WEEKLY" && editCommand.Scope == "FUTURE")
			{
				DateOnly? originDate = editCommand.SplitDate;
				if (originDate.HasValue)
				{
					DateOnly valueOrDefault = originDate.GetValueOrDefault();
					SplitRule(plannerState, existing, a, editCommand.Rule ?? throw new ArgumentException("缺少新周期规则。"), valueOrDefault);
					goto IL_09bf;
				}
			}
			a.UpdatedAt = Clock.Now;
			a.Version = (existing?.Version ?? 0) + 1;
			if (existing != null)
			{
				plannerState.Activities.Remove(existing);
			}
			plannerState.Activities.Add(a);
			plannerState.Rules.RemoveAll((RecurrenceRule r) => r.ActivityId == a.Id);
			if (a.Kind == "WEEKLY")
			{
				RecurrenceRule rule = editCommand.Rule;
				rule.ActivityId = a.Id;
				plannerState.Rules.Add(rule);
				a.StartAt = null;
				a.EndAt = null;
			}
			goto IL_09bf;
			IL_09bf:
			string savedId = (plannerState.Activities.Any((Activity x) => x.Id == a.Id) ? a.Id : existing?.Id);
			if (savedId != null && plannerState.Activities.Any((Activity x) => x.Id == savedId))
			{
				plannerState.Reminders.RemoveAll((ReminderSetting r) => r.ActivityId == savedId);
				plannerState.Reminders.Add(new ReminderSetting
				{
					ActivityId = savedId,
					Enabled = editCommand.ReminderEnabled,
					LeadMin = editCommand.ReminderLeadMin
				});
			}
			Invalidate(plannerState, a.Id);
			continue;
			IL_0584:
			Invalidate(plannerState, a.Id);
		}
		InvalidateChangedPlans(state, plannerState);
		plannerState.Revision = expectedRevision + 1;
		return plannerState;
	}

	public static void ValidateActivity(PlannerState state, Activity a, RecurrenceRule? rule, Activity? existing = null)
	{
		if (!state.Tables.Any((ScheduleTable t) => t.Id == a.TableId))
		{
			throw new ArgumentException("请选择所属行程表。");
		}
		if (string.IsNullOrWhiteSpace(a.Title) || a.Title.Trim().Length > 100)
		{
			throw new ArgumentException("事项名称应为1至100字。");
		}
		a.Title = a.Title.Trim();
		string kind = a.Kind;
		bool flag = ((kind == "ONCE" || kind == "WEEKLY") ? true : false);
		bool flag2 = !flag;
		if (!flag2)
		{
			string constraintType = a.ConstraintType;
			bool flag3 = ((constraintType == "FIXED" || constraintType == "FLEXIBLE") ? true : false);
			flag2 = !flag3;
		}
		if (flag2)
		{
			throw new ArgumentException("活动类型不合法。");
		}
		if (a.DurationMin <= 0 || a.DurationMin > 10080)
		{
			throw new ArgumentException("活动时长应大于0且不超过7天。");
		}
		DateTime? startAt = a.StartAt;
		if (startAt.HasValue)
		{
			DateTime valueOrDefault = startAt.GetValueOrDefault();
			startAt = a.EndAt;
			if (startAt.HasValue)
			{
				DateTime valueOrDefault2 = startAt.GetValueOrDefault();
				if (valueOrDefault2 <= valueOrDefault)
				{
					throw new ArgumentException("结束时间必须晚于开始时间。");
				}
			}
		}
		if (a.Kind == "ONCE" && (!a.StartAt.HasValue || !a.EndAt.HasValue))
		{
			throw new ArgumentException("一次性活动必须有起止时间。");
		}
		if (a.ConstraintType == "FLEXIBLE" && a.Kind == "ONCE" && (!a.WindowStart.HasValue || !a.WindowEnd.HasValue || a.WindowEnd - a.WindowStart < TimeSpan.FromMinutes(a.DurationMin)))
		{
			throw new ArgumentException("灵活活动需填写能够容纳活动的时间范围。");
		}
		if (a.ConstraintType == "FLEXIBLE" && a.Kind == "ONCE" && (a.StartAt < a.WindowStart || a.EndAt > a.WindowEnd))
		{
			throw new ArgumentException("活动时间应在允许的灵活范围内。");
		}
		if (a.Kind == "WEEKLY" && (existing == null || rule != null))
		{
			flag2 = rule == null;
			if (!flag2)
			{
				int weekMask = rule.WeekMask;
				flag = ((weekMask < 1 || weekMask > 127) ? true : false);
				flag2 = flag;
			}
			if (flag2 || !TimeOnly.TryParseExact(rule.StartTime, "HH:mm", out var result))
			{
				throw new ArgumentException("请填写合法重复星期及共同开始时间。");
			}
			if (rule.ValidUntil < rule.ValidFrom)
			{
				throw new ArgumentException("周期结束日期不能早于开始日期。");
			}
			if (a.ConstraintType == "FLEXIBLE" && (!TimeOnly.TryParse(rule.WindowFrom, out result) || rule.WindowSpanMin < a.DurationMin || !rule.WindowSpanMin.HasValue))
			{
				throw new ArgumentException("灵活周期需设置足够的时间窗。");
			}
		}
	}

	private static void SplitRule(PlannerState state, Activity old, Activity replacement, RecurrenceRule rule, DateOnly split)
	{
		RecurrenceRule recurrenceRule = state.Rules.Single((RecurrenceRule r) => r.ActivityId == old.Id);
		if (split < recurrenceRule.ValidFrom)
		{
			throw new ArgumentException("拆分日期不能早于原规则开始日。");
		}
		List<ActivityException> list = state.Exceptions.Where((ActivityException e) => e.ActivityId == old.Id && e.OriginDate >= split).ToList();
		string newId = Guid.NewGuid().ToString("N");
		replacement.Id = newId;
		replacement.Version = 1;
		replacement.StartAt = null;
		replacement.EndAt = null;
		rule.ActivityId = newId;
		rule.ValidFrom = split;
		if (rule.ValidUntil < split)
		{
			throw new ArgumentException("新规则结束日早于生效日。");
		}
		state.Activities.Add(replacement);
		state.Rules.Add(rule);
		ReminderSetting reminder = state.Reminders.FirstOrDefault((ReminderSetting x) => x.ActivityId == old.Id);
		CopyReminder(newId);
		Dictionary<DateOnly, (string Id, DateOnly? Origin)> mapping = new Dictionary<DateOnly, (string, DateOnly?)>();
		foreach (ActivityException item in list)
		{
			if (rule.Matches(item.OriginDate))
			{
				item.ActivityId = newId;
				mapping[item.OriginDate] = (newId, item.OriginDate);
			}
			else if (item.Kind == "OVERRIDE")
			{
				Activity activity = JsonKit.Clone(replacement);
				activity.Id = Guid.NewGuid().ToString("N");
				activity.Kind = "ONCE";
				activity.StartAt = item.StartAt;
				activity.EndAt = item.EndAt;
				activity.Title = item.Title ?? old.Title;
				activity.PlaceId = (item.PlaceOverride ? item.PlaceId : old.PlaceId);
				activity.ConstraintType = "FIXED";
				activity.DurationMin = (int)(item.EndAt - item.StartAt).Value.TotalMinutes;
				state.Activities.Add(activity);
				CopyReminder(activity.Id);
				mapping[item.OriginDate] = (activity.Id, null);
				state.Exceptions.Remove(item);
			}
			else
			{
				state.Exceptions.Remove(item);
			}
		}
		Invalidate(state, old.Id);
		HashSet<string> obsolete = new HashSet<string>();
		foreach (PlanLeg leg in state.Legs)
		{
			if (leg.FromActivityId == old.Id && leg.FromOriginDate >= split)
			{
				(string, DateOnly?)? tuple = Target(leg.FromOriginDate);
				if (tuple.HasValue)
				{
					(leg.FromActivityId, leg.FromOriginDate) = tuple.GetValueOrDefault();
				}
				else
				{
					obsolete.Add(leg.Id);
				}
			}
			if (leg.ToActivityId == old.Id && leg.ToOriginDate >= split)
			{
				(string, DateOnly?)? tuple = Target(leg.ToOriginDate);
				if (tuple.HasValue)
				{
					(leg.ToActivityId, leg.ToOriginDate) = tuple.GetValueOrDefault();
				}
				else
				{
					obsolete.Add(leg.Id);
				}
			}
		}
		state.Segments.RemoveAll((RouteSegment s) => obsolete.Contains(s.LegId));
		state.Legs.RemoveAll((PlanLeg l) => obsolete.Contains(l.Id));
		foreach (ReminderDelivery item2 in state.Deliveries.Where((ReminderDelivery x) => x.ActivityId == old.Id).ToList())
		{
			string instanceKey = item2.InstanceKey;
			int num = item2.InstanceKey.LastIndexOf(':') + 1;
			if (DateOnly.TryParseExact(instanceKey.Substring(num, instanceKey.Length - num), "yyyy-MM-dd", out var result) && !(result < split))
			{
				(string, DateOnly?)? tuple = Target(result);
				if (tuple.HasValue)
				{
					(string, DateOnly?) valueOrDefault = tuple.GetValueOrDefault();
					item2.ActivityId = valueOrDefault.Item1;
					item2.InstanceKey = valueOrDefault.Item1 + ":" + (valueOrDefault.Item2?.ToString("yyyy-MM-dd") ?? "once");
				}
				else
				{
					state.Deliveries.Remove(item2);
				}
			}
		}
		if (split == recurrenceRule.ValidFrom)
		{
			RemoveActivity(state, old.Id);
			return;
		}
		recurrenceRule.ValidUntil = split.AddDays(-1);
		old.Version++;
		void CopyReminder(string id)
		{
			if (reminder != null)
			{
				ReminderSetting reminderSetting = JsonKit.Clone(reminder);
				reminderSetting.ActivityId = id;
				state.Reminders.Add(reminderSetting);
			}
		}
		(string Id, DateOnly? Origin)? Target(DateOnly? date)
		{
			if (date.HasValue)
			{
				DateOnly valueOrDefault2 = date.GetValueOrDefault();
				if (valueOrDefault2 >= split)
				{
					if (!mapping.TryGetValue(valueOrDefault2, out (string, DateOnly?) value))
					{
						if (!rule.Matches(valueOrDefault2))
						{
							return null;
						}
						return (newId, valueOrDefault2);
					}
					return value;
				}
			}
			return null;
		}
	}

	public static void RemoveActivity(PlannerState state, string id)
	{
		Invalidate(state, id);
		HashSet<string> legs = (from l in state.Legs
			where l.FromActivityId == id || l.ToActivityId == id
			select l.Id).ToHashSet();
		state.Segments.RemoveAll((RouteSegment s) => legs.Contains(s.LegId));
		state.Legs.RemoveAll((PlanLeg l) => legs.Contains(l.Id));
		state.Activities.RemoveAll((Activity a) => a.Id == id);
		state.Rules.RemoveAll((RecurrenceRule r) => r.ActivityId == id);
		state.Exceptions.RemoveAll((ActivityException e) => e.ActivityId == id);
		state.Reminders.RemoveAll((ReminderSetting r) => r.ActivityId == id);
		state.Deliveries.RemoveAll((ReminderDelivery r) => r.ActivityId == id);
	}

	public static void Invalidate(PlannerState state, string activityId)
	{
		foreach (string item in (from l in state.Legs
			where l.FromActivityId == activityId || l.ToActivityId == activityId
			select l.PlanId).ToHashSet())
		{
			InvalidatePlan(state, item);
		}
	}

	public static void InvalidatePlan(PlannerState state, string id)
	{
		foreach (Plan item in state.Plans.Where((Plan p) => p.Id == id))
		{
			item.Status = "STALE";
		}
		foreach (PlanLeg item2 in state.Legs.Where((PlanLeg l) => l.PlanId == id))
		{
			item2.ValidationStatus = "STALE";
		}
	}

	public static void InvalidateChangedPlans(PlannerState before, PlannerState after)
	{
		foreach (Plan item in after.Plans.Where((Plan p) => before.Plans.Any((Plan old) => old.Id == p.Id)))
		{
			if (Fingerprint(before, item.PlanDate) != Fingerprint(after, item.PlanDate))
			{
				InvalidatePlan(after, item.Id);
			}
		}
		static string Fingerprint(PlannerState s, DateOnly date)
		{
			DateTime dateTime = date.ToDateTime(TimeOnly.MinValue);
			return JsonSerializer.Serialize(from o in CalendarEngine.Expand(s, dateTime, dateTime.AddDays(1.0))
				select new
				{
					Key = o.Key,
					Title = o.Title,
					Start = o.Start,
					End = o.End,
					PlaceId = o.PlaceId,
					TableId = o.Activity.TableId,
					Place = s.Places.FirstOrDefault((Place p) => p.Id == o.PlaceId)
				}, JsonKit.Options);
		}
	}
}
