using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Planner.Core;
using Planner.Data;

namespace Planner.Desktop;
public partial class MainWindow
{
    string assistantText="",assistantMessage="描述要做的事，系统识别后可逐项完善；开始时间可以留空。";CancellationTokenSource? assistantCancel;AgentReviewWindow? agentReview;
    DispatcherTimer? reminderTimer;DateTime? reminderSince;bool notifying;
    partial void BuildAssistant(Panel host)
    {
        EnsureReminders();var panel=new StackPanel();panel.Children.Add(Ui.Text("智能行程助手",16,true));panel.Children.Add(Ui.Text("描述需求 → 完善活动 → 选择交通 → 核对保存",11));
        var input=new TextBox{Text=assistantText,Height=115,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};input.TextChanged+=(_,_)=>assistantText=input.Text;panel.Children.Add(input);
        var generate=Ui.Button("识别并完善行程",()=>{},true);generate.IsEnabled=assistantCancel is null&&agentReview is null;panel.Children.Add(generate);
        generate.Click+=async(_,_)=>
        {
            if(assistantCancel is not null||agentReview is not null)return;
            try
            {
                if(string.IsNullOrWhiteSpace(assistantText))throw new ArgumentException("请先描述要安排的事项。");
                assistantCancel=new();assistantMessage="正在识别活动，尚未修改行程。";ShowDetails(null);
                var reply=await new RepositoryAgentClient().ExtractAsync(new SettingsStore(vm.DataDirectory).Load(),assistantText,assistantCancel.Token);assistantCancel.Token.ThrowIfCancellationRequested();
                var review=new AgentReviewWindow(vm,reply){Owner=this};agentReview=review;review.Closed+=(_,_)=>{agentReview=null;assistantMessage="可以继续描述新需求，或点击日期空白手动添加。";ShowDetails(null);};review.Show();assistantMessage="请在完善行程窗口中核对活动，确认后才会保存。";
            }
            catch(OperationCanceledException){assistantMessage="已取消，原有行程未改变。";}
            catch(Exception ex){assistantMessage=ex.Message;}
            finally{assistantCancel?.Dispose();assistantCancel=null;if(IsLoaded)ShowDetails(null);}
        };
        if(assistantCancel is not null)panel.Children.Add(Ui.Button("取消识别",()=>assistantCancel?.Cancel()));
        panel.Children.Add(Ui.Text(assistantMessage,12));panel.Children.Add(Ui.Text("未配置模型时可用本地单项规则，例如：明天下午2点在南京博物院参观，2小时。",11));host.Children.Add(Ui.Card(panel));
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
    partial void OnClosedServices(){assistantCancel?.Cancel();agentReview?.Close();reminderTimer?.Stop();}
}
