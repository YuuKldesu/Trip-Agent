using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Planner.Core;
using Planner.Data;
namespace Planner.Desktop;

public sealed class GuidedAssistantPanel:DockPanel,IDisposable
{
    readonly PlannerViewModel vm;readonly Window owner;readonly GuidedAgentClient client;readonly ConversationStore store;
    GuidedConversation conversation;readonly StackPanel log=new(),receipts=new();readonly ScrollViewer transcript;readonly ComboBox branches=new();readonly TextBox input=Ui.Input();readonly TextBlock status=Ui.Text("",12),hint=Ui.Text("",11),composeLabel=Ui.Text("说说你的安排",12,true);readonly Button send,retry,cancel,editCancel;readonly Button latest=Ui.Button("↓ 回到最新",()=>{});readonly TextBlock modelLabel=Ui.Text("",11);bool followLatest=true;readonly DispatcherTimer draftTimer=new(){Interval=TimeSpan.FromMilliseconds(300)};CancellationTokenSource? pending;AgentReviewWindow? review;long serial;bool rendering,disposed,readOnly;string? editId;string previousDraft="",reviewBranch="";long reviewVersion;
    public GuidedConversation Conversation=>conversation;
    public GuidedAssistantPanel(PlannerViewModel model,Window owner,GuidedAgentClient? client=null)
    {
        vm=model;this.owner=owner;this.client=client??new();store=new(vm.DataDirectory);try{conversation=store.Load();foreach(var r in conversation.Batches)GeneratedBatchService.Reconcile(vm.State,r);}catch(Exception ex){conversation=new();readOnly=true;status.Text=ex.Message;}
        conversation.Drafts??=[];
        var head=new DockPanel{Margin=new(0,0,0,8)};DockPanel.SetDock(head,Dock.Top);Children.Add(head);
        Button Icon(string glyph,string title,Action action){var button=ShellIcons.Button(glyph,title,()=>Ui.Safe(action),"Assistant:"+title);button.MinHeight=32;button.Padding=new(7);DockPanel.SetDock(button,Dock.Right);head.Children.Add(button);return button;}
        Icon("\uE712","更多",()=>{var menu=new ContextMenu();var settings=new MenuItem{Header="智能服务设置"};settings.Click+=(_,_)=>{if(owner is MainWindow main)main.OpenSettingsCategory("ai");};menu.Items.Add(settings);var copy=new MenuItem{Header="复制当前对话"};copy.Click+=(_,_)=>Clipboard.SetText(string.Join("\n\n",conversation.Active.Messages.Select(m=>(m.Role=="user"?"你":"助手")+"："+m.Content)));menu.Items.Add(copy);menu.PlacementTarget=head;menu.IsOpen=true;});
        Icon("\uE81C","会话历史",()=>OpenHistory(head));Icon("\uE70F","新对话",()=>{SaveDraft();CancelRequest();Persist(()=>conversation.NewBranch());editId=null;input.Text="";followLatest=true;Render();input.Focus();});head.Children.Add(Ui.Text("行程助手",15,true));
        branches.Tag="ConversationBranches";
        var bottom=new StackPanel();DockPanel.SetDock(bottom,Dock.Bottom);Children.Add(bottom);latest.Visibility=Visibility.Collapsed;latest.HorizontalAlignment=HorizontalAlignment.Center;latest.Click+=(_,_)=>{followLatest=true;transcript?.ScrollToEnd();};bottom.Children.Add(latest);
        var composer=new StackPanel();bottom.Children.Add(new Border{Background=Ui.Brush("#FFFDF9"),BorderBrush=Ui.Brush("#DCCDBD"),BorderThickness=new(1),CornerRadius=new(16),Padding=new(8),Child=composer});
        composeLabel.Visibility=Visibility.Collapsed;composer.Children.Add(composeLabel);input.AcceptsReturn=true;input.TextWrapping=TextWrapping.Wrap;input.MinHeight=58;input.MaxHeight=150;input.MaxLength=8000;input.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;input.HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled;input.Background=Brushes.Transparent;input.BorderThickness=new(0);input.Tag="ConversationInput";input.ToolTip="告诉我你的安排 · Ctrl+Enter发送，Enter换行";composer.Children.Add(input);
        var toolbar=new DockPanel();composer.Children.Add(toolbar);var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};DockPanel.SetDock(actions,Dock.Right);toolbar.Children.Add(actions);
        editCancel=Ui.Button("取消编辑",()=>{editId=null;input.Text=previousDraft;Render(false);});actions.Children.Add(editCancel);retry=Ui.Button("重试",()=>Run(RequestAsync));actions.Children.Add(retry);cancel=Ui.Button("■",CancelRequest);cancel.ToolTip="停止回答";actions.Children.Add(cancel);send=Ui.Button("↑",()=>Run(SendInput),true);send.Tag="ConversationSend";send.ToolTip="发送 · Ctrl+Enter";send.Padding=new(12,5,12,5);actions.Children.Add(send);
        modelLabel.Cursor=Cursors.Hand;modelLabel.VerticalAlignment=VerticalAlignment.Center;modelLabel.TextTrimming=TextTrimming.CharacterEllipsis;modelLabel.ToolTip="在设置中配置智能服务";modelLabel.MouseLeftButtonUp+=(_,_)=>{if(owner is MainWindow main)main.OpenSettingsCategory("ai");};toolbar.Children.Add(modelLabel);
        var transcriptBody=new StackPanel();transcriptBody.Children.Add(log);var process=new Expander{Header="当前状态",Content=status,Margin=new(0,5,0,8),IsExpanded=true};transcriptBody.Children.Add(process);transcriptBody.Children.Add(receipts);
        transcript=new(){Tag="ConversationTranscript",Content=transcriptBody,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};Children.Add(transcript);
        transcript.ScrollChanged+=(_,e)=>{if(e.ExtentHeightChange==0&&e.VerticalChange!=0)followLatest=transcript.VerticalOffset>=transcript.ScrollableHeight-24;latest.Visibility=followLatest?Visibility.Collapsed:Visibility.Visible;};
        input.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Enter&&Keyboard.Modifiers==ModifierKeys.Control){Run(SendInput);e.Handled=true;}};
        draftTimer.Tick+=(_,_)=>{draftTimer.Stop();Ui.Safe(SaveDraft);};input.TextChanged+=(_,_)=>{if(rendering)return;draftTimer.Stop();if(editId is null)draftTimer.Start();SetBusy();};
        input.Text=conversation.Drafts.GetValueOrDefault(conversation.ActiveBranchId,"");Render(false);
    }
    void SaveDraft(){draftTimer.Stop();if(readOnly||editId is not null)return;string id=conversation.ActiveBranchId,text=input.Text;if(conversation.Drafts.GetValueOrDefault(id,"")!=text)Persist(()=>conversation.Drafts[id]=text);}
    void OpenHistory(FrameworkElement anchor)
    {
        SaveDraft();var panel=new StackPanel{Width=260};var query=Ui.Input();query.ToolTip="搜索会话";panel.Children.Add(query);var list=new StackPanel();panel.Children.Add(new ScrollViewer{Content=list,MaxHeight=300,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});var popup=StyledDateEntry.MakePopup(anchor,panel);
        void RefreshHistory(){list.Children.Clear();foreach(var branch in conversation.Branches.Where(b=>(b.Name+" "+string.Join(" ",b.Messages.Select(m=>m.Content))).Contains(query.Text,StringComparison.OrdinalIgnoreCase)).Reverse()){var pick=Ui.Button(branch.Name,()=>{SelectBranch(branch.Id);popup.IsOpen=false;});pick.HorizontalContentAlignment=HorizontalAlignment.Left;pick.Background=branch.Id==conversation.ActiveBranchId?Ui.Brush("#E5E1DC"):Brushes.Transparent;list.Children.Add(pick);}}
        query.TextChanged+=(_,_)=>RefreshHistory();RefreshHistory();popup.IsOpen=true;query.Focus();
    }
    async void Run(Func<Task> action){try{await action();}catch(Exception ex){status.Text=ex.Message;}}
    void Persist(Action change){if(readOnly)throw new InvalidOperationException("对话记录暂不可写，原记录已保留。请先恢复对话文件。");var before=JsonKit.Clone(conversation);try{change();store.Save(conversation);}catch{conversation=before;throw;}}
    async Task SendInput(){string text=input.Text;if(string.IsNullOrWhiteSpace(text)||pending is not null||readOnly)return;followLatest=true;if(editId is {} id){CancelRequest();Persist(()=>conversation.ForkAt(id,text));editId=null;input.Text=previousDraft;Render();await RequestAsync();}else{Persist(()=>{conversation.AddUser(text);conversation.Drafts[conversation.ActiveBranchId]="";});input.Text="";Render();await RequestAsync();}}
    public async Task SendAsync(string text){if(pending is not null)throw new InvalidOperationException("正在回答，可取消后继续。");Persist(()=>conversation.AddUser(text));Render();await RequestAsync();}
    public async Task EditMessageAsync(string messageId,string replacement){SaveDraft();CancelRequest();Persist(()=>conversation.ForkAt(messageId,replacement));editId=null;Render();await RequestAsync();}
    public void SelectBranch(string id){if(!conversation.Branches.Any(b=>b.Id==id))throw new ArgumentException("对话分支不存在。");SaveDraft();CancelRequest();Persist(()=>conversation.ActiveBranchId=id);editId=null;input.Text=conversation.Drafts.GetValueOrDefault(id,"");followLatest=true;Render();}
    void CancelRequest(){serial++;pending?.Cancel();pending=null;if(!disposed){status.Text="已取消回答，已发送消息和原有行程均保留。";SetBusy();}}
    async Task RequestAsync()
    {
        if(disposed||pending is not null||conversation.Active.Messages.LastOrDefault()?.Role!="user")return;var source=new CancellationTokenSource();pending=source;long turn=++serial,version=conversation.Active.Version;string branch=conversation.ActiveBranchId;var messages=conversation.Active.Messages.Select(JsonKit.Clone).ToList();status.Text="正在理解并补齐需求，尚未修改行程…";SetBusy();
        try{var config=new SettingsStore(vm.DataDirectory).Load();hint.Text=GuidedAgentClient.IsLocal(config)?"本地规则引导 · 多项复杂规划可在设置连接模型":"连续对话 · 只发送当前分支的对话内容";var reply=await client.ReplyAsync(config,messages,source.Token);source.Token.ThrowIfCancellationRequested();if(disposed||turn!=serial||conversation.ActiveBranchId!=branch||conversation.Active.Version!=version)return;Persist(()=>conversation.AddAssistant(reply));status.Text=reply.Ready?"需求已汇总，核对后可生成行程草稿。":"可以继续自由补充，我会记住本分支已说明的信息。";Render();}
        catch(OperationCanceledException){if(!disposed&&turn==serial)status.Text="已取消回答，行程未改变。";}
        catch(Exception ex){if(!disposed&&turn==serial)status.Text=ex.Message;}
        finally{if(ReferenceEquals(pending,source))pending=null;source.Dispose();if(!disposed)SetBusy();}
    }
    void SetBusy(){send.IsEnabled=!readOnly&&pending is null&&!string.IsNullOrWhiteSpace(input.Text);send.Visibility=pending is null?Visibility.Visible:Visibility.Collapsed;retry.Visibility=pending is null&&conversation.Active.Messages.LastOrDefault()?.Role=="user"?Visibility.Visible:Visibility.Collapsed;retry.IsEnabled=!readOnly;cancel.Visibility=pending is null?Visibility.Collapsed:Visibility.Visible;editCancel.Visibility=editId is null?Visibility.Collapsed:Visibility.Visible;composeLabel.Visibility=editId is null?Visibility.Collapsed:Visibility.Visible;composeLabel.Text="编辑旧消息 · 发送后建立新分支";}
    void Render(bool scroll=true)
    {
        rendering=true;try{branches.Items.Clear();foreach(var b in conversation.Branches)branches.Items.Add(new ComboBoxItem{Content=b.Name,Tag=b.Id});branches.SelectedIndex=conversation.Branches.FindIndex(b=>b.Id==conversation.ActiveBranchId);}finally{rendering=false;}
        var config=new SettingsStore(vm.DataDirectory).Load();hint.Text=GuidedAgentClient.IsLocal(config)?"本地规则引导 · 多项复杂规划可在设置连接模型":"连续对话 · 只发送当前分支的对话内容";modelLabel.Text=GuidedAgentClient.IsLocal(config)?"本地规则 · 先核对再保存":(string.IsNullOrWhiteSpace(config.Model)?config.AgentProvider:config.Model)+" · 先核对再保存";modelLabel.ToolTip=hint.Text;log.Children.Clear();if(conversation.Active.Messages.Count==0){Bubble("助手","今天想安排什么？告诉我日期、地点和要做的事，我们一起完善。",null);foreach(var sample in new[]{"明天在南京博物院参观，约2小时","周末想去南京大学，先帮我确认校区"}){string example=sample;var suggestion=Ui.Button(example,()=>{input.Text=example;input.Focus();});suggestion.HorizontalContentAlignment=HorizontalAlignment.Left;log.Children.Add(suggestion);}}
        foreach(var message in conversation.Active.Messages)Bubble(message.Role=="user"?"你":"助手",message.Content,message.Role=="user"?message.Id:null);
        if(conversation.Active.Reply is {Ready:true} reply){var summary=new StackPanel();summary.Children.Add(Ui.Text("行程需求汇总",15,true));summary.Children.Add(Ui.Text(reply.Summary.Length>0?reply.Summary:string.Join("\n",reply.Events.Select(e=>$"{e.Day:yyyy-MM-dd} · {e.Title} · {e.Location} · {e.Duration}分钟")),12));bool applied=conversation.Batches.Any(r=>r.BranchId==conversation.ActiveBranchId&&r.SourceVersion==conversation.Active.Version&&r.Status is "APPLIED" or "PENDING");var generate=Ui.Button(applied?"此方案已生成":"生成行程草稿",()=>OpenReview(),true);generate.Tag="ConversationGenerate";generate.IsEnabled=!applied&&!readOnly;summary.Children.Add(generate);log.Children.Add(Ui.Card(summary));}
        RenderReceipts();SetBusy();double offset=transcript.VerticalOffset;if(scroll)Dispatcher.BeginInvoke(()=>{if(followLatest)transcript.ScrollToEnd();else transcript.ScrollToVerticalOffset(offset);},DispatcherPriority.Loaded);
    }
    void Bubble(string who,string text,string? id)
    {
        var panel=new StackPanel();panel.Children.Add(Ui.Text(who,10));var body=new TextBox{Text=text,IsReadOnly=true,TextWrapping=TextWrapping.Wrap,BorderThickness=new(0),Background=Brushes.Transparent,Padding=new(0),Margin=new(0),IsReadOnlyCaretVisible=false};Typography.Set(body,13);panel.Children.Add(body);
        var actions=new StackPanel{Orientation=Orientation.Horizontal};var copy=Ui.Button("复制",()=>Clipboard.SetText(text));copy.Padding=new(5,2,5,2);actions.Children.Add(copy);
        if(id is not null){var edit=Ui.Button("编辑",()=>{SaveDraft();previousDraft=input.Text;editId=id;input.Text=conversation.Active.Messages.Single(m=>m.Id==id).Content;SetBusy();input.Focus();});edit.Tag="EditMessage:"+id;edit.Padding=new(5,2,5,2);actions.Children.Add(edit);}panel.Children.Add(actions);
        var bubble=new Border{Child=panel,Padding=new(10),CornerRadius=new(12),Margin=new(id is null?0:16,0,0,12),Background=id is null?Brushes.Transparent:Ui.Brush("#EEE7DF")};actions.Opacity=0;bubble.MouseEnter+=(_,_)=>actions.Opacity=1;bubble.MouseLeave+=(_,_)=>{if(!actions.IsKeyboardFocusWithin)actions.Opacity=0;};actions.GotKeyboardFocus+=(_,_)=>actions.Opacity=1;log.Children.Add(bubble);
    }
    void RenderReceipts()
    {
        receipts.Children.Clear();foreach(var receipt in conversation.Batches.Where(r=>r.BranchId==conversation.ActiveBranchId&&r.Status is "APPLIED" or "PENDING" or "UNDO_PENDING" or "CONFLICT").Reverse()){
            receipts.Children.Add(Ui.Text($"{receipt.CreatedAt:MM-dd HH:mm} · 生成 {receipt.Activities.Count} 项"+(receipt.Status=="CONFLICT"?" · 需要核对":""),11));var undo=Ui.Button("撤回此批次",()=>UndoBatch(receipt.Id));undo.IsEnabled=receipt.Status=="APPLIED";undo.Tag="UndoBatch:"+receipt.Id;receipts.Children.Add(undo);var show=Ui.Button("查看行程",()=>{if(receipt.Activities.FirstOrDefault() is {} owned&&vm.State.Activities.FirstOrDefault(a=>a.Id==owned.Id)?.StartAt is {} date){if(owner is MainWindow main)main.ReturnToBoard();vm.FocusDate(DateOnly.FromDateTime(date));}});receipts.Children.Add(show);
        }
    }
    public AgentReviewWindow OpenReview()
    {
        if(review is not null){review.Activate();if(reviewBranch!=conversation.ActiveBranchId||reviewVersion!=conversation.Active.Version)throw new InvalidOperationException("另一个分支的草稿仍在打开，请关闭该草稿后再生成当前方案。");return review;}if(conversation.Active.Reply is not{Ready:true} reply)throw new InvalidOperationException("请先通过对话补齐必要信息。");string branch=conversation.ActiveBranchId;long version=conversation.Active.Version;if(conversation.Batches.Any(r=>r.BranchId==branch&&r.SourceVersion==version&&r.Status is "APPLIED" or "PENDING"))throw new InvalidOperationException("此方案已经生成，可先撤回或修改需求后重新推演。");
        string? receiptId=null;var w=new AgentReviewWindow(vm,reply.Events.Select(JsonKit.Clone).ToList(),true){Owner=owner};review=w;reviewBranch=branch;reviewVersion=version;w.CanApply=()=>!disposed&&conversation.ActiveBranchId==branch&&conversation.Active.Version==version;
        w.Applying+=(before,after)=>{if(w.CanApply?.Invoke()!=true)throw new InvalidOperationException("对话已变化，请重新生成。");var receipt=GeneratedBatchService.Capture(before,after,branch);receipt.SourceVersion=version;receipt.Status="PENDING";Persist(()=>conversation.Batches.Add(receipt));receiptId=receipt.Id;};
        w.Applied+=()=>{if(receiptId is {} id)Persist(()=>conversation.Batches.Single(r=>r.Id==id).Status="APPLIED");status.Text="已生成并保存行程，可按批次撤回。";Render(false);};
        w.Closed+=(_,_)=>{review=null;if(receiptId is {} id){var r=conversation.Batches.SingleOrDefault(r=>r.Id==id);if(r is not null&&r.Status=="PENDING"){try{Persist(()=>GeneratedBatchService.Reconcile(vm.State,r));}catch(Exception ex){status.Text=ex.Message;}}}if(!disposed)Render(false);};w.Show();return w;
    }
    public void UndoBatch(string id)
    {
        var receipt=conversation.Batches.SingleOrDefault(r=>r.Id==id)??throw new ArgumentException("生成批次不存在。");var next=GeneratedBatchService.Undo(vm.State,receipt);Persist(()=>receipt.Status="UNDO_PENDING");try{vm.Persist(next);}catch{try{Persist(()=>GeneratedBatchService.Reconcile(vm.State,receipt));}catch{}throw;}
        try{Persist(()=>receipt.Status="UNDONE");status.Text="已撤回对应批次，其他行程保留。";}catch(Exception ex){status.Text="行程已撤回，批次标记将在重新载入后恢复："+ex.Message;}if(!vm.State.Tables.Any(t=>t.Id==vm.SelectedTableId))vm.SelectedTableId=vm.State.Tables.FirstOrDefault()?.Id??"";Render(false);
    }
    public void Dispose(){if(disposed)return;try{SaveDraft();}catch{}draftTimer.Stop();disposed=true;CancelRequest();review?.Close();}
}
