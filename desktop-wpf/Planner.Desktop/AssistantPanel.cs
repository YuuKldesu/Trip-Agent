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
    partial void OpenSettings()
    {
        var store=new SettingsStore(vm.DataDirectory);var config=store.Load();var w=new Window{Title="服务设置",Width=650,Height=780,Owner=this,WindowStartupLocation=WindowStartupLocation.CenterOwner};var panel=new StackPanel{Margin=new(24)};w.Content=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        panel.Children.Add(Ui.Text("按需连接智能与地图服务",20,true));panel.Children.Add(Ui.Text("保存设置不会自动请求服务。密钥使用当前Windows用户保护，复制软件给他人时不附带Data目录。",12));
        var endpoint=Ui.Input(config.LlmEndpoint);var model=Ui.Input(config.Model);
        string[] providers=["AUTO","LOCAL","CUSTOM","BAILIAN","DEEPSEEK"];var provider=Ui.Choices(["自动兼容旧设置","本地规则","自定义兼容接口","阿里云百炼","DeepSeek"],Math.Max(0,Array.IndexOf(providers,config.AgentProvider)));var region=Ui.Choices(["北京","新加坡"],config.BailianRegion=="新加坡"?1:0);var host=Ui.Input(config.BailianHost);Ui.Field(panel,"需求识别服务",provider);Ui.Field(panel,"百炼地域",region);Ui.Field(panel,"百炼业务空间API Host（可留空）",host);
        bool reset=false;
        PasswordBox Secret(string value){var text=SettingsStore.ReadForSettings(value);if(value.Length>0&&text.Length==0)reset=true;return new(){Password=text,Padding=new(9),Margin=new(0,3,0,10)};}
        var key=Secret(config.LlmKeyProtected);var web=Secret(config.MapServiceKeyProtected);var js=Secret(config.MapJsKeyProtected);var security=Secret(config.MapSecurityProtected);
        if(reset)panel.Children.Add(Ui.Text("部分旧密钥无法由当前Windows用户解密，已将对应输入框留空，请重新填写后保存。",12,true));
        var json=new CheckBox{Content="使用JSON响应模式（接口不支持时可关闭）",IsChecked=config.JsonMode};
        Ui.Field(panel,"LLM接口基础地址（含/v1；也可填写完整/chat/completions）",endpoint);Ui.Field(panel,"模型名称",model);Ui.Field(panel,"LLM API密钥",key);panel.Children.Add(json);
        panel.Children.Add(Ui.Text("高德开放平台",17,true));Ui.Field(panel,"Web服务Key（地点与路线查询）",web);Ui.Field(panel,"Web端JS Key（内嵌地图）",js);Ui.Field(panel,"JS安全密钥 securityJsCode",security);
        panel.Children.Add(Ui.Text("三个地图字段来自相应应用配置。交通价格、拥堵等以接口实际返回为准，未提供的数据不编造。",12));
        panel.Children.Add(Ui.Button("保存到本机",()=>
        {
            if(endpoint.Text.Trim().Length>0)_=LlmClient.Endpoint(endpoint.Text);
            if(providers[provider.SelectedIndex]=="BAILIAN")_=RepositoryAgentClient.BailianEndpoint(region.Text,host.Text);
            if(providers[provider.SelectedIndex] is "AUTO" or "CUSTOM"&&key.Password.Length>0&&(model.Text.Trim().Length==0||endpoint.Text.Trim().Length==0))throw new ArgumentException("填写LLM密钥时也应填写接口地址与模型名称。");
            store.Save(new(){AgentProvider=providers[provider.SelectedIndex],BailianRegion=region.Text,BailianHost=host.Text.Trim(),LlmEndpoint=endpoint.Text.Trim(),Model=model.Text.Trim(),LlmKeyProtected=SettingsStore.Protect(key.Password),MapServiceKeyProtected=SettingsStore.Protect(web.Password),MapJsKeyProtected=SettingsStore.Protect(js.Password),MapSecurityProtected=SettingsStore.Protect(security.Password),JsonMode=json.IsChecked==true});w.Close();vm.Notify("服务设置已保存；未发送网络请求");
        },true));w.ShowDialog();
    }
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
