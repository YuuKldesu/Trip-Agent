using System;
using System.Collections.Generic;
using System.Linq;

namespace Planner.Core;

public static class DemoSchedule
{
	public static PlannerState AddToEmpty(PlannerState state, DateOnly today)
	{
		if (state.Activities.Count > 0 || state.Tables.Count == 0)
		{
			return state;
		}
		List<EditCommand> list = new List<EditCommand>();
		int[] array = new int[7] { 1, 3, 0, 5, 2, 1, 4 };
		string[] array2 = new string[5] { "晨间阅读", "资料整理", "午后散步", "课程讨论", "晚间复盘" };
		for (int i = 0; i < array.Length; i++)
		{
			for (int j = 0; j < array[i]; j++)
			{
				DateTime value = today.AddDays(i - 2).ToDateTime(new TimeOnly(8 + j * 2, 0));
				list.Add(new EditCommand
				{
					Activity = new Activity
					{
						TableId = state.Tables[(i + j) % state.Tables.Count].Id,
						Title = array2[j] + "（示例）",
						StartAt = value,
						EndAt = value.AddMinutes((j % 2 == 0) ? 60 : 90),
						DurationMin = ((j % 2 == 0) ? 60 : 90),
						Note = ((i == 3 && j == 1) ? "示例长备注：带上笔记本与资料，整理最近的讨论内容，核对尚未完成的事项。日期格会随活动内容增长，其他日期的安排数量刻意不同，便于检查界面布局。" : "可直接编辑或删除这项示例行程。")
					},
					Place = new Place
					{
						Name = ((j % 2 == 0) ? "南京图书馆" : "新街口"),
						InputName = ((j % 2 == 0) ? "南京图书馆" : "新街口"),
						Lng = ((j % 2 == 0) ? 118.797 : 118.7857),
						Lat = ((j % 2 == 0) ? 32.042 : 32.0417),
						Status = "UNVERIFIED",
						Source = "示例坐标，非实地核验"
					}
				});
			}
		}
		DateTime value2 = today.AddDays(4).ToDateTime(new TimeOnly(23, 0));
		list.Add(new EditCommand
		{
			Activity = new Activity
			{
				TableId = state.Tables.Last().Id,
				Title = "跨日整理（示例）",
				StartAt = value2,
				EndAt = value2.AddHours(2.0),
				DurationMin = 120,
				Note = "用于检查跨日续段。"
			}
		});
		return ChangeService.Apply(state, list, state.Revision);
	}
}
