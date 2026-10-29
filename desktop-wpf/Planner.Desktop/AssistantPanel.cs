using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Planner.Core;
using Planner.Data;

namespace Planner.Desktop;
public partial class MainWindow
{
    GuidedAssistantPanel? guidedPanel;
    DispatcherTimer? reminderTimer;DateTime? reminderSince;bool notifying;
    partial void BuildAssistant(Panel host)
    {
        EnsureReminders();guidedPanel??=new GuidedAssistantPanel(vm,this);host.Children.Add(guidedPanel);
    }
    partial void OpenSettings()=>OpenSettingsCategory("appearance");
    void EnsureReminders()
    {
        if(reminderTimer is not null)return;
        reminderSince=Clock.Now;reminderTimer=new(){Interval=TimeSpan.FromSeconds(20)};
        reminderTimer.Tick+=(_,_)=>Ui.Safe(()=>
        {
            if(notifying)return;var now=Clock.Now;var due=ReminderEngine.Due(vm.State,now,reminderSince??now);reminderSince=now;
            if(due.Count==0)return;notifying=true;
            try{vm.Mutate(s=>{foreach(var r in due)s.Deliveries.Add(new(){ActivityId=r.ActivityId,InstanceKey=r.Key,DueAt=r.DueAt});});MessageBox.Show(string.Join("\n\n",due.Select(r=>$"{r.Label} · {r.Title}\n计划提醒 {r.DueAt:HH:mm}")),"行程提醒",MessageBoxButton.OK,MessageBoxImage.Information);}finally{notifying=false;}
        });reminderTimer.Start();
        Dispatcher.BeginInvoke(()=>Ui.Safe(()=>
        {
            var missed=ReminderEngine.MissedToday(vm.State,Clock.Now);if(missed.Count==0)return;
            vm.Mutate(s=>{foreach(var r in missed)s.Deliveries.Add(new(){ActivityId=r.ActivityId,InstanceKey=r.Key,DueAt=r.DueAt,Status="MISSED"});});
            MessageBox.Show("软件关闭时不会发送提醒。今天此前有以下未提示事项，现汇总一次：\n\n"+string.Join("\n",missed.Take(12).Select(r=>$"{r.DueAt:HH:mm} · {r.Title}")),"今日提醒汇总",MessageBoxButton.OK,MessageBoxImage.Information);
        }));
    }
    partial void OnClosedServices(){guidedPanel?.Dispose();reminderTimer?.Stop();}
}
