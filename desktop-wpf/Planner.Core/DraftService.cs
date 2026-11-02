using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Planner.Core;

public static class DraftService
{
	public static string Describe(PlannerState state, ChangeDraft draft, int index)
	{
		EditCommand command = Commands(state, draft, new _003C_003Ez__ReadOnlySingleElementList<int>(index)).Single();
		Activity a = command.Activity;
		ScopeItem scopeItem = draft.Context.Items.FirstOrDefault((ScopeItem x) =>
		{
			if (x.ActivityId == a.Id)
			{
				if (command.OriginDate.HasValue)
				{
					DateOnly? originDate2 = x.OriginDate;
					DateOnly? originDate3 = command.OriginDate;
					if (originDate2.HasValue != originDate3.HasValue)
					{
						return false;
					}
					if (!originDate2.HasValue)
					{
						return true;
					}
					return originDate2.GetValueOrDefault() == originDate3.GetValueOrDefault();
				}
				return true;
			}
			return false;
		});
		StringBuilder stringBuilder = new StringBuilder();
		StringBuilder stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder3 = stringBuilder2;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(3, 2, stringBuilder2);
		string value;
		if (command.Action == "DELETE")
		{
			value = "删除";
		}
		else
		{
			value = (((object)scopeItem == null) ? "新增" : "修改");
		}
		handler.AppendFormatted(value);
		handler.AppendLiteral(" · ");
		handler.AppendFormatted(a.Title);
		stringBuilder3.AppendLine(ref handler);
		stringBuilder.AppendLine("行程表：" + state.Tables.Single((ScheduleTable t) => t.Id == a.TableId).Name);
		if ((object)scopeItem != null)
		{
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder4 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(6, 3, stringBuilder2);
			handler.AppendLiteral("原：");
			handler.AppendFormatted(scopeItem.StartAt, "yyyy-MM-dd HH:mm");
			handler.AppendLiteral("—");
			handler.AppendFormatted(scopeItem.EndAt, "MM-dd HH:mm");
			handler.AppendLiteral(" · ");
			handler.AppendFormatted(scopeItem.Place);
			stringBuilder4.AppendLine(ref handler);
		}
		if (command.Action == "DELETE")
		{
			stringBuilder.AppendLine((!command.OriginDate.HasValue) ? "实际影响：仅此项活动" : ("实际影响：仅取消原发生日期 " + command.OriginDate.Value.ToString("yyyy-MM-dd") + " 这一次，保留后续周期。"));
			return stringBuilder.ToString();
		}
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder5 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(6, 3, stringBuilder2);
		handler.AppendLiteral("拟：");
		handler.AppendFormatted(a.StartAt, "yyyy-MM-dd HH:mm");
		handler.AppendLiteral("—");
		handler.AppendFormatted(a.EndAt, "MM-dd HH:mm");
		handler.AppendLiteral(" · ");
		handler.AppendFormatted(command.Place?.Name ?? state.PlaceName(a.PlaceId));
		stringBuilder5.AppendLine(ref handler);
		RecurrenceRule rule = command.Rule;
		if (rule != null && ((object)scopeItem == null || command.Scope == "FUTURE" || !scopeItem.OriginDate.HasValue))
		{
			string[] source = new string[7] { "周一", "周二", "周三", "周四", "周五", "周六", "周日" };
			DateOnly start = ((!(command.Scope == "FUTURE")) ? rule.ValidFrom : (command.SplitDate ?? rule.ValidFrom));
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder6 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(10, 2, stringBuilder2);
			handler.AppendLiteral("实际影响：");
			handler.AppendFormatted(((object)scopeItem == null) ? "新增未来每周规则" : "调整未来每周规则");
			handler.AppendLiteral("，从 ");
			handler.AppendFormatted(start, "yyyy-MM-dd");
			handler.AppendLiteral(" 起");
			stringBuilder6.AppendLine(ref handler);
			stringBuilder.AppendLine(string.Join("、", source.Where((string _, int i) => (rule.WeekMask & (1 << i)) != 0)) + $" · {rule.StartTime}开始 · {a.DurationMin}分钟");
			DateOnly? validUntil = rule.ValidUntil;
			string value2;
			if (validUntil.HasValue)
			{
				DateOnly valueOrDefault = validUntil.GetValueOrDefault();
				value2 = $"至 {valueOrDefault:yyyy-MM-dd}（含）";
			}
			else
			{
				value2 = "不设结束日，持续每周重复";
			}
			stringBuilder.AppendLine(value2);
			if ((object)scopeItem != null && command.Scope == "FUTURE")
			{
				List<ActivityException> list = state.Exceptions.Where((ActivityException e) => e.ActivityId == a.Id && e.OriginDate >= start).ToList();
				stringBuilder2 = stringBuilder;
				StringBuilder stringBuilder7 = stringBuilder2;
				handler = new StringBuilder.AppendInterpolatedStringHandler(9, 1, stringBuilder2);
				handler.AppendLiteral("未来单日例外 ");
				handler.AppendFormatted(list.Count);
				handler.AppendLiteral("项：");
				stringBuilder7.AppendLine(ref handler);
				foreach (ActivityException item in list.Take(15))
				{
					int num;
					if ((rule.WeekMask & (1 << (int)(item.OriginDate.DayOfWeek + 6) % 7)) != 0)
					{
						if (rule.ValidUntil.HasValue)
						{
							DateOnly originDate = item.OriginDate;
							validUntil = rule.ValidUntil;
							num = ((originDate <= validUntil) ? 1 : 0);
						}
						else
						{
							num = 1;
						}
					}
					else
					{
						num = 0;
					}
					bool flag = (byte)num != 0;
					StringBuilder stringBuilder8 = stringBuilder;
					string text = $"{item.OriginDate:yyyy-MM-dd} · ";
					string text2;
					if (flag)
					{
						text2 = "迁移并保留";
					}
					else
					{
						text2 = ((item.Kind == "OVERRIDE") ? "转为独立单次活动，保留提醒" : "取消日期不再产生实例");
					}
					stringBuilder8.AppendLine(text + text2);
				}
				if (list.Count > 15)
				{
					stringBuilder.AppendLine("其余例外按相同规则处理。");
				}
			}
		}
		else
		{
			DateOnly? validUntil = command.OriginDate;
			string value3;
			if (validUntil.HasValue)
			{
				DateOnly valueOrDefault2 = validUntil.GetValueOrDefault();
				value3 = $"实际影响：仅原发生日期 {valueOrDefault2:yyyy-MM-dd} 这一次；其余周期不变。";
			}
			else
			{
				value3 = "实际影响：仅此项具体日期活动。";
			}
			stringBuilder.AppendLine(value3);
		}
		return stringBuilder.ToString().TrimEnd();
	}

	public static List<EditCommand> Commands(PlannerState state, ChangeDraft draft, IEnumerable<int> selected)
	{
		if (state.Revision != draft.Revision)
		{
			throw new InvalidOperationException("草稿生成后行程已改变，请重新生成并预览。");
		}
		List<EditCommand> list = new List<EditCommand>();
		HashSet<string> hashSet = new HashSet<string>();
		ScopeContext context = draft.Context;
		if (draft.Reply.Operations.Count > 50)
		{
			throw new ArgumentException("单次操作过多，请缩小范围。");
		}
		foreach (int item in selected.Distinct())
		{
			if (item < 0 || item >= draft.Reply.Operations.Count)
			{
				throw new ArgumentException("草稿选项已失效。");
			}
			AiOperation op = draft.Reply.Operations[item];
			bool flag;
			switch (op.Action)
			{
			case "ADD":
			case "UPDATE":
			case "DELETE":
				flag = true;
				break;
			default:
				flag = false;
				break;
			}
			if (!flag)
			{
				throw new ArgumentException("智能助手返回了不支持的操作。");
			}
			ScopeItem target = null;
			Activity a;
			if (op.Action == "ADD")
			{
				a = new Activity
				{
					TableId = (op.TableId ?? context.TableIds.First())
				};
			}
			else
			{
				List<ScopeItem> list2 = context.Items.Where((ScopeItem x) =>
				{
					if (x.ActivityId == op.TargetActivityId)
					{
						if (op.OriginDate.HasValue)
						{
							DateOnly? originDate2 = x.OriginDate;
							DateOnly? originDate3 = op.OriginDate;
							if (originDate2.HasValue != originDate3.HasValue)
							{
								return false;
							}
							if (!originDate2.HasValue)
							{
								return true;
							}
							return originDate2.GetValueOrDefault() == originDate3.GetValueOrDefault();
						}
						return true;
					}
					return false;
				}).ToList();
				if (list2.Count != 1)
				{
					throw new ArgumentException("修改或删除目标不明确，或不在授权范围；请明确日期后重试。");
				}
				target = list2.Single();
				if (!hashSet.Add(target.ActivityId + ":" + target.OriginDate.ToString()))
				{
					throw new ArgumentException("同一实例包含重复操作，请重新生成草稿。");
				}
				a = JsonKit.Clone(state.Activities.Single((Activity x) => x.Id == target.ActivityId));
				a.StartAt = target.StartAt;
				a.EndAt = target.EndAt;
				a.Title = target.Title;
				DateOnly? originDate = target.OriginDate;
				if (originDate.HasValue)
				{
					DateOnly origin = originDate.GetValueOrDefault();
					ActivityException ex = state.Exceptions.FirstOrDefault((ActivityException e) => e.ActivityId == a.Id && e.OriginDate == origin);
					if (ex != null && ex.PlaceOverride)
					{
						a.PlaceId = ex.PlaceId;
					}
				}
			}
			if (!context.TableIds.Contains(a.TableId) || (op.TableId != null && !context.TableIds.Contains(op.TableId)))
			{
				throw new ArgumentException("目标行程表未授权。");
			}
			if (op.Action == "DELETE")
			{
				if (target.StartAt < context.From || target.EndAt > context.Until)
				{
					throw new ArgumentException("删除跨出了选定日期，请扩大范围后再操作。");
				}
				list.Add(new EditCommand
				{
					Action = "DELETE",
					Activity = a,
					OriginDate = target.OriginDate
				});
				continue;
			}
			a.Title = op.Title ?? a.Title;
			a.TableId = op.TableId ?? a.TableId;
			a.StartAt = op.StartAt ?? a.StartAt;
			a.EndAt = op.EndAt ?? a.EndAt;
			if (!a.StartAt.HasValue || !a.EndAt.HasValue || a.StartAt < context.From || a.EndAt > context.Until)
			{
				throw new ArgumentException("草稿起止时间不完整或超出授权日期，请补充或扩大范围。");
			}
			a.DurationMin = (int)(a.EndAt - a.StartAt).Value.TotalMinutes;
			a.ConstraintType = op.ConstraintType;
			a.WindowStart = op.WindowStart;
			a.WindowEnd = op.WindowEnd;
			if (a.ConstraintType == "FLEXIBLE" && (a.StartAt < a.WindowStart || a.EndAt > a.WindowEnd))
			{
				throw new ArgumentException("活动时间应在允许的灵活范围内。");
			}
			Place place = null;
			if (op.Place != null)
			{
				place = state.Places.FirstOrDefault((Place p) => p.Name == op.Place.Trim()) ?? new Place
				{
					Name = op.Place.Trim(),
					InputName = op.Place.Trim()
				};
				a.PlaceId = place.Id;
			}
			RecurrenceRule rule = null;
			if (op.WeekMask > 0)
			{
				if (!context.AllowFutureRules && (!op.UntilDate.HasValue || op.UntilDate >= DateOnly.FromDateTime(context.Until)))
				{
					throw new ArgumentException("周期超出所选日期；请明确允许未来周期规则，或提供范围内的结束日。");
				}
				a.Kind = "WEEKLY";
				rule = new RecurrenceRule
				{
					ActivityId = a.Id,
					ValidFrom = DateOnly.FromDateTime(a.StartAt.Value),
					ValidUntil = op.UntilDate,
					WeekMask = op.WeekMask,
					StartTime = a.StartAt.Value.ToString("HH:mm"),
					WindowFrom = op.WindowStart?.ToString("HH:mm"),
					WindowSpanMin = ((op.WindowStart.HasValue && op.WindowEnd.HasValue) ? new int?((int)(op.WindowEnd - op.WindowStart).Value.TotalMinutes) : ((int?)null))
				};
			}
			bool flag2 = (object)target != null && op.Scope == "FUTURE";
			if (flag2 && !context.AllowFutureRules)
			{
				throw new ArgumentException("修改未来周期尚未明确授权。");
			}
			ReminderSetting reminderSetting = state.Reminders.FirstOrDefault((ReminderSetting r) => r.ActivityId == a.Id);
			list.Add(new EditCommand
			{
				Activity = a,
				Rule = rule,
				OriginDate = target?.OriginDate,
				Scope = (flag2 ? "FUTURE" : "ONCE"),
				SplitDate = target?.OriginDate,
				Place = place,
				ReminderEnabled = (reminderSetting?.Enabled ?? false),
				ReminderLeadMin = (reminderSetting?.LeadMin ?? 15)
			});
		}
		ChangeService.Apply(state, list, state.Revision);
		return list;
	}
}
