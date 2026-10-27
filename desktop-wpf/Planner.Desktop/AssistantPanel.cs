using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Planner.Core;
using Planner.Data;

namespace Planner.Desktop;
public partial class MainWindow
{
    ChangeDraft? assistantDraft;readonly HashSet<int> chosenOps=[];string assistantText="",assistantMessage="用一句话描述要做的事情，或选中活动后直接手动编辑。";CancellationTokenSource? assistantCancel;
    readonly HashSet<string> scopeTables=[];DateOnly? scopeFrom,scopeUntil;bool futureRules,scopeInitialized;
    DispatcherTimer? reminderTimer;DateTime? reminderSince;bool notifying;
    partial void BuildAssistant(Panel host)
    {
        EnsureReminders();var panel=new StackPanel();panel.Children.Add(Ui.Text("智能辅助填表",16,true));
        if(!scopeInitialized){scopeTables.Add(vm.SelectedTableId);scopeInitialized=true;}
        var selectedIds=scopeTables.ToArray();
        foreach(var t in vm.State.Tables)
        {
            var c=new CheckBox{Content=t.Name,IsChecked=selectedIds.Contains(t.Id)};
            c.Click+=(_,_)=>{if(c.IsChecked==true)scopeTables.Add(t.Id);else scopeTables.Remove(t.Id);assistantDraft=null;assistantCancel?.Cancel();};panel.Children.Add(c);
        }
        var from=new DatePicker{SelectedDate=(scopeFrom??vm.SelectedDate).ToDateTime(TimeOnly.MinValue)};var to=new DatePicker{SelectedDate=(scopeUntil??vm.SelectedDate).ToDateTime(TimeOnly.MinValue)};
        from.SelectedDateChanged+=(_,_)=>{scopeFrom=from.SelectedDate is {} d?DateOnly.FromDateTime(d):null;assistantDraft=null;assistantCancel?.Cancel();};to.SelectedDateChanged+=(_,_)=>{scopeUntil=to.SelectedDate is {} d?DateOnly.FromDateTime(d):null;assistantDraft=null;assistantCancel?.Cancel();};
        Ui.Field(panel,"读取日期 从",from);Ui.Field(panel,"至（含当日）",to);
        var future=new CheckBox{Content="允许本次操作未来每周规则",IsChecked=futureRules};future.Click+=(_,_)=>{futureRules=future.IsChecked==true;assistantDraft=null;assistantCancel?.Cancel();};panel.Children.Add(future);
        panel.Children.Add(Ui.Text("只发送勾选表在上述范围内的必要事项，不含备注。其他生效表仅在本地参与冲突检查。",11));
        var input=new TextBox{Text=assistantText,Height=105,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};input.TextChanged+=(_,_)=>assistantText=input.Text;panel.Children.Add(input);
        var generate=Ui.Button("生成待确认草稿",()=>{},true);generate.IsEnabled=assistantCancel is null;
        generate.Click+=async(_,_)=>
        {
            if(assistantCancel is not null)return;
            try
            {
                if(string.IsNullOrWhiteSpace(input.Text))throw new ArgumentException("先描述要安排或修改的事项。");
                var start=from.SelectedDate??throw new ArgumentException("请选择读取日期。");var end=to.SelectedDate??start;
                var ids=scopeTables.ToArray();var snapshot=vm.State.Copy();var context=ScopeContext.Build(snapshot,ids,start.Date,end.Date.AddDays(1),futureRules);
                assistantCancel=new();assistantMessage="正在生成草稿，可取消；尚未写入行程。";generate.IsEnabled=false;ShowDetails(null);
                var reply=await new LlmClient().BuildAsync(new SettingsStore(vm.DataDirectory).Load(),assistantText,context,assistantCancel.Token);
                assistantCancel.Token.ThrowIfCancellationRequested();
                assistantDraft=new(snapshot.Revision,context,reply);chosenOps.Clear();for(int i=0;i<reply.Operations.Count;i++)chosenOps.Add(i);
                assistantMessage=reply.Message.Length==0?(reply.Operations.Count==0?"请补充更具体的事项和时间。":"请核对每项变更，确认后才保存。 "):reply.Message;
            }
            catch(OperationCanceledException){assistantMessage="已取消请求，原有行程未改变。";}
            catch(Exception ex){assistantMessage=ex.Message;assistantDraft=null;}
            finally{assistantCancel?.Dispose();assistantCancel=null;ShowDetails(null);}
        };
        panel.Children.Add(generate);
        if(assistantCancel is not null)panel.Children.Add(Ui.Button("取消请求",()=>assistantCancel?.Cancel()));
        panel.Children.Add(Ui.Text(assistantMessage,12));
        if(assistantDraft is { } draft)
        {
            panel.Children.Add(Ui.Text($"读取范围 {draft.Context.From:MM-dd}—{draft.Context.Until.AddDays(-1):MM-dd} · {draft.Reply.Operations.Count}项。实际修改范围以每项说明为准。",12,true));
            for(int i=0;i<draft.Reply.Operations.Count;i++)
            {
                var index=i;string text;bool valid=true;
                try{text=DraftService.Describe(vm.State,draft,index);}catch(Exception ex){text="本项未通过检查："+ex.Message;valid=false;chosenOps.Remove(index);}
                var c=new CheckBox{Content=Ui.Text(text,12),IsChecked=chosenOps.Contains(index),IsEnabled=valid};c.Click+=(_,_)=>{if(c.IsChecked==true)chosenOps.Add(index);else chosenOps.Remove(index);};panel.Children.Add(c);
            }
            if(draft.Reply.Operations.Count>0)panel.Children.Add(Ui.Button("核对并写入选中变更",()=>
            {
                var commands=DraftService.Commands(vm.State,draft,chosenOps.Order());if(commands.Count==0)throw new ArgumentException("请至少选择一项操作。");
                var next=ChangeService.Apply(vm.State,commands,draft.Revision);var conflicts=CalendarEngine.AllConflicts(next,draft.Context.From,draft.Context.Until);
                var impact=string.Join("\n\n",chosenOps.Order().Select(i=>DraftService.Describe(vm.State,draft,i)));
                if(!Ui.ConfirmReview($"将写入选中的 {commands.Count} 项变更。请核对实际影响，未来周期可能超出读取日期。\n\n{impact}\n\n本地检查：\n"+string.Join("\n",conflicts.Take(8).Select(x=>x.Message))))return;
                assistantDraft=null;vm.Persist(next);assistantMessage="变更已保存。";ShowDetails(null);
            },true));
        }
        host.Children.Add(Ui.Card(panel));
    }
    partial void OpenSettings()
    {
        var store=new SettingsStore(vm.DataDirectory);var config=store.Load();var w=new Window{Title="服务设置",Width=650,Height=780,Owner=this,WindowStartupLocation=WindowStartupLocation.CenterOwner};var panel=new StackPanel{Margin=new(24)};w.Content=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        panel.Children.Add(Ui.Text("按需连接智能与地图服务",20,true));panel.Children.Add(Ui.Text("保存设置不会自动请求服务。密钥使用当前Windows用户保护，复制软件给他人时不附带Data目录。",12));
        var endpoint=Ui.Input(config.LlmEndpoint);var model=Ui.Input(config.Model);
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
            if(key.Password.Length>0&&(model.Text.Trim().Length==0||endpoint.Text.Trim().Length==0))throw new ArgumentException("填写LLM密钥时也应填写接口地址与模型名称。");
            store.Save(new(){LlmEndpoint=endpoint.Text.Trim(),Model=model.Text.Trim(),LlmKeyProtected=SettingsStore.Protect(key.Password),MapServiceKeyProtected=SettingsStore.Protect(web.Password),MapJsKeyProtected=SettingsStore.Protect(js.Password),MapSecurityProtected=SettingsStore.Protect(security.Password),JsonMode=json.IsChecked==true});w.Close();vm.Notify("服务设置已保存；未发送网络请求");
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
    partial void OnClosedServices(){assistantCancel?.Cancel();reminderTimer?.Stop();}
}
