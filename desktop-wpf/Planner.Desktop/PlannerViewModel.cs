using System.ComponentModel;
using System.IO;
using Planner.Core;
using Planner.Data;
using Activity=Planner.Core.Activity;

namespace Planner.Desktop;
public sealed class PlannerViewModel : INotifyPropertyChanged
{
    readonly SqliteStore store;
    public string DataDirectory {get;}
    public PlannerState State {get;private set;}
    public HashSet<string> HiddenTables {get;}=[];
    public DateOnly Week {get;private set;}=Clock.Monday(Clock.Today);
    public DateOnly SelectedDate {get;set;}=Clock.Today;
    public string SelectedTableId {get;set;}="";
    public string Status {get;private set;}="已就绪 · 本地保存";
    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? Changed;
    public PlannerViewModel(string directory)
    {
        DataDirectory=directory;store=new(Path.Combine(directory,"planner.db"));State=store.Load();
        if(State.Tables.Count==0){var n=State.Copy();n.Tables.Add(new(){Name="工作安排",Color="#E5BD99"});n.Tables.Add(new(){Name="个人安排",Color="#C2C3A5"});n.Revision++;Persist(n);}
        SelectedTableId=State.Tables[0].Id;
    }
    public void Notify(string? status=null){if(status is not null)Status=status;PropertyChanged?.Invoke(this,new(null));Changed?.Invoke();}
    public void ChangeWeek(int weeks){Week=Week.AddDays(weeks*7);SelectedDate=Week;Notify();}
    public void Today(){SelectedDate=Clock.Today;Week=Clock.Monday(SelectedDate);Notify();}
    public void FocusDate(DateOnly date){SelectedDate=date;Week=Clock.Monday(date);Notify();}
    public void Reload(){State=store.Load();if(!State.Tables.Any(t=>t.Id==SelectedTableId))SelectedTableId=State.Tables.FirstOrDefault()?.Id??"";Notify("已重新加载本地数据");}
    public void Persist(PlannerState next)
    {
        ChangeService.InvalidateChangedPlans(State,next);
        store.Save(next,State.Revision);State=next;Notify("已保存 · "+Clock.Now.ToString("HH:mm:ss"));
    }
    public void Edit(IEnumerable<EditCommand> commands){Persist(ChangeService.Apply(State,commands,State.Revision));}
    public void Mutate(Action<PlannerState> action){var next=State.Copy();action(next);next.Revision++;Persist(next);}
    public List<Occurrence> WeekItems(bool onlyVisible=true)=>CalendarEngine.ExpandForDisplay(State,Week.ToDateTime(TimeOnly.MinValue),Week.AddDays(7).ToDateTime(TimeOnly.MinValue),onlyVisible?State.Tables.Where(t=>!HiddenTables.Contains(t.Id)).Select(t=>t.Id):null);
    public List<Conflict> WeekConflicts()=>CalendarEngine.AllConflicts(State,Week.ToDateTime(TimeOnly.MinValue),Week.AddDays(7).ToDateTime(TimeOnly.MinValue));
    public void LoadSample()
    {
        if(State.Activities.Count>0)throw new InvalidOperationException("示例只能载入空行程，避免混入已有内容。");
        var work=State.Tables[0].Id;var personal=State.Tables.Last().Id;var monday=Week;
        var commands=new List<EditCommand>{new(){Activity=new(){TableId=work,Title="每周学习安排（示例）",Kind="WEEKLY",DurationMin=120},Rule=new(){ValidFrom=monday,WeekMask=31,StartTime="09:00"}},new(){Activity=new(){TableId=personal,Title="整理本周计划（示例）",StartAt=monday.AddDays(2).ToDateTime(new(14,0)),EndAt=monday.AddDays(2).ToDateTime(new(15,30)),DurationMin=90}},new(){Activity=new(){TableId=personal,Title="图书馆阅读（示例）",StartAt=monday.AddDays(5).ToDateTime(new(10,0)),EndAt=monday.AddDays(5).ToDateTime(new(12,0)),DurationMin=120},Place=new(){Name="南京图书馆",InputName="南京图书馆"}}};
        Edit(commands);
    }
}
