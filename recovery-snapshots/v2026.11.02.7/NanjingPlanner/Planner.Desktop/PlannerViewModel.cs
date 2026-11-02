using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Planner.Core;
using Planner.Data;

namespace Planner.Desktop;

public sealed class PlannerViewModel : INotifyPropertyChanged
{
	private readonly SqliteStore store;

	public string DataDirectory { get; }

	public PlannerState State { get; private set; }

	public BoardPreferences Appearance { get; private set; }

	public HashSet<string> HiddenTables { get; } = new HashSet<string>();

	public DateOnly Week { get; private set; } = Clock.Monday(Clock.Today);

	public DateOnly SelectedDate { get; set; } = Clock.Today;

	public string SelectedTableId { get; set; } = "";

	public string Status { get; private set; } = "已就绪 · 本地保存";

	public event PropertyChangedEventHandler? PropertyChanged;

	public event Action? Changed;

	public PlannerViewModel(string directory, bool seedDemo = true)
	{
		DataDirectory = directory;
		Appearance = new BoardPreferenceStore(directory).Load();
		bool flag = !File.Exists(Path.Combine(directory, "planner.db"));
		store = new SqliteStore(Path.Combine(directory, "planner.db"));
		State = store.Load();
		if (State.Tables.Count == 0)
		{
			PlannerState plannerState = State.Copy();
			plannerState.Tables.Add(new ScheduleTable
			{
				Name = "工作安排",
				Color = "#E5BD99"
			});
			plannerState.Tables.Add(new ScheduleTable
			{
				Name = "个人安排",
				Color = "#C2C3A5"
			});
			plannerState.Revision++;
			Persist(plannerState);
		}
		SelectedTableId = State.Tables[0].Id;
		foreach (string hiddenTable in Appearance.HiddenTables)
		{
			HiddenTables.Add(hiddenTable);
		}
		if (seedDemo & flag)
		{
			Persist(DemoSchedule.AddToEmpty(State, Clock.Today));
		}
	}

	public void Notify(string? status = null)
	{
		if (status != null)
		{
			Status = status;
		}
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
		Changed?.Invoke();
	}

	public void UpdateAppearance(Action<BoardPreferences> change)
	{
		Appearance = new BoardPreferenceStore(DataDirectory).Update(change);
		Typography.Apply(Appearance.FontSize);
		Notify();
	}

	public void ChangeWeek(int weeks)
	{
		Week = Week.AddDays(weeks * 7);
		SelectedDate = Week;
		Notify();
	}

	public void Today()
	{
		SelectedDate = Clock.Today;
		Week = Clock.Monday(SelectedDate);
		Notify();
	}

	public void FocusDate(DateOnly date)
	{
		SelectedDate = date;
		Week = Clock.Monday(date);
		Notify();
	}

	public void Reload()
	{
		State = store.Load();
		if (!State.Tables.Any((ScheduleTable t) => t.Id == SelectedTableId))
		{
			SelectedTableId = State.Tables.FirstOrDefault()?.Id ?? "";
		}
		Notify("已重新加载本地数据");
	}

	public void Persist(PlannerState next)
	{
		ChangeService.InvalidateChangedPlans(State, next);
		store.Save(next, State.Revision);
		State = next;
		Notify("已保存 · " + Clock.Now.ToString("HH:mm:ss"));
	}

	public void Edit(IEnumerable<EditCommand> commands)
	{
		Persist(ChangeService.Apply(State, commands, State.Revision));
	}

	public void Mutate(Action<PlannerState> action)
	{
		PlannerState plannerState = State.Copy();
		action(plannerState);
		plannerState.Revision++;
		Persist(plannerState);
	}

	public List<Occurrence> WeekItems(bool onlyVisible = true)
	{
		return CalendarEngine.ExpandForDisplay(State, Week.ToDateTime(TimeOnly.MinValue), Week.AddDays(7).ToDateTime(TimeOnly.MinValue), onlyVisible ? (from t in State.Tables
			where !HiddenTables.Contains(t.Id)
			select t.Id) : null);
	}

	public List<Conflict> WeekConflicts()
	{
		return CalendarEngine.AllConflicts(State, Week.ToDateTime(TimeOnly.MinValue), Week.AddDays(7).ToDateTime(TimeOnly.MinValue));
	}

	public void LoadSample()
	{
		if (State.Activities.Count > 0)
		{
			throw new InvalidOperationException("示例只能载入空行程，避免混入已有内容。");
		}
		Persist(DemoSchedule.AddToEmpty(State, Clock.Today));
	}
}
