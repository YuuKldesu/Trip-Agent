using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Planner.Core;
using Planner.Data;
using Planner.Desktop;

static class ConversationRecoveryChecks
{
    public static async Task Run(MainWindow owner,string directory,Action<bool,string> check)
    {
        var vm=new PlannerViewModel(Path.Combine(directory,"chat-recovery"),false);
        new SettingsStore(vm.DataDirectory).Save(new(){AgentProvider="CUSTOM",LlmEndpoint="http://localhost/v1",Model="fixture"});
        int call=0;var day=Clock.Today.AddDays(5);
        string ready=JsonSerializer.Serialize(new{message="请核对草稿",ready=true,events=new[]{new{day=day.ToString("yyyy-MM-dd"),title="参观",location="南京博物院",duration=60}}});
        using var http=new HttpClient(new Handler(_=>
        {
            call++;
            if(call==4)throw new HttpRequestException("模拟连接中断");
            if(call==5)throw new TaskCanceledException("模拟请求超时");
            string json=call==2?"{\"message\":\"你想怎么调整？\",\"ready\":true,\"events\":[]}":ready;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new{choices=new[]{new{message=new{content=json}}}}),Encoding.UTF8,"application/json")});
        }));
        using var panel=new GuidedAssistantPanel(vm,owner,new GuidedAgentClient(http));
        var window=new Window{Owner=owner,Content=panel,Width=520,Height=800};window.Show();
        await panel.SendAsync("明天参观南京博物院一小时");var review=panel.OpenReview();
        await panel.SendAsync("行程表我不满意");
        check(panel.Conversation.Active.Messages.Last().Content=="你想怎么调整？"&&panel.Conversation.Active.Reply is {Ready:false}&&Walk(panel).Any(x=>Equals((x as FrameworkElement)?.Tag,"ConversationDraftIssue")),"生成草稿后补充消息仍显示回复，无效活动数据不启用生成");
        Button(review,"生成行程 →").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));await Task.Delay(30);
        check(vm.State.Activities.Count==0&&Walk(review).OfType<TextBlock>().Any(t=>t.Text.Contains("分支已变动")),"补充消息使旧草稿失效，防止保存旧需求");review.Close();
        await panel.SendAsync("改成一小时");
        foreach(var expander in Walk(panel).OfType<Expander>())expander.IsExpanded=false;
        await panel.SendAsync("现在");var error=Walk(panel).OfType<Border>().Single(x=>Equals(x.Tag,"ConversationError"));
        check(error.Visibility==Visibility.Visible&&Walk(error).OfType<TextBlock>().Any(t=>t.Text.Contains("连接中断")),"状态折叠时连接失败仍在对话区可见");
        Button(panel,"重试").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));await Task.Delay(30);
        check(error.Visibility==Visibility.Visible&&Walk(error).OfType<TextBlock>().Any(t=>t.Text.Contains("请求超时")),"超时与用户主动取消区分，并提供可见重试提示");
        await panel.SendAsync("重新整理一下");check(error.Visibility==Visibility.Collapsed&&panel.Conversation.Active.Messages.Last().Role=="assistant"&&call==6,"失败后可继续发消息，成功时清除错误提示");window.Close();

        var defaults=new PlannerViewModel(Path.Combine(directory,"review-default-table"),false);
        var optional=new AgentReviewWindow(defaults,[new(){Day=day,Title="默认行程表测试",Location="南京博物院",Duration=60,Start=600}],true){Owner=owner};optional.Show();await Task.Delay(80);
        Walk(optional).OfType<TextBox>().Single(t=>Equals(t.Tag,"NewTableName")).Text="   ";
        check(Walk(optional).OfType<TableColorPicker>().Single().SelectedColor is null,"未选颜色也允许继续生成");
        var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(30)};
        timer.Tick+=(_,_)=>{foreach(var dialog in Application.Current.Windows.OfType<Window>().Where(w=>w.Title=="核对变更与实际影响").ToArray())Button(dialog,"确认写入").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));};
        timer.Start();try{Button(optional,"生成行程 →").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));for(int i=0;i<100&&defaults.State.Activities.Count==0;i++)await Task.Delay(30);}finally{timer.Stop();}
        check(defaults.State.Activities.Count==1&&defaults.State.Tables.Any(t=>t.Name==day.ToString("MM-dd")+" 行程计划"&&t.Color==TablePalette.Colors[0].Hex),"名称留空且不选颜色时使用默认值实际保存行程");if(optional.IsVisible)optional.Close();
    }
    static IEnumerable<DependencyObject> Walk(DependencyObject root){yield return root;foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())foreach(var item in Walk(child))yield return item;}
    static Button Button(DependencyObject root,string text)=>Walk(root).OfType<Button>().First(b=>Equals(b.Content,text));
    sealed class Handler(Func<HttpRequestMessage,Task<HttpResponseMessage>> reply):HttpMessageHandler
    {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>reply(request);}
}
