using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Planner.Core;
using Planner.Data;
namespace Planner.Desktop;

public sealed class GuidedAssistantPanel:DockPanel,IDisposable
{
    readonly PlannerViewModel vm;readonly Window owner;readonly GuidedAgentClient client;readonly ConversationStore store;
    GuidedConversation conversation;readonly StackPanel log=new(),receipts=new();readonly ScrollViewer transcript;readonly ComboBox branches=new();readonly TextBox input=Ui.Input();readonly TextBlock status=Ui.Text("",12),hint=Ui.Text("",11),composeLabel=Ui.Text("说说你的安排",12,true);readonly Button send,retry,cancel,editCancel;CancellationTokenSource? pending;AgentReviewWindow? review;long serial;bool rendering,disposed,readOnly;string? editId;string previousDraft="",reviewBranch="";long reviewVersion;
    public GuidedConversation Conversation=>conversation;
    public GuidedAssistantPanel(PlannerViewModel model,Window owner,GuidedAgentClient? client=null)
    {
        vm=model;this.owner=owner;this.client=client??new();store=new(vm.DataDirectory);try{conversation=store.Load();foreach(var r in conversation.Batches)GeneratedBatchService.Reconcile(vm.State,r);}catch(Exception ex){conversation=new();readOnly=true;status.Text=ex.Message;}
        var assistantIcon=ShellIcons.Glyph(ShellIcons.Assistant);assistantIcon.Margin=new(0,0,8,0);var head=Ui.Row(assistantIcon,Ui.Text("智能行程助手",16,true));DockPanel.SetDock(head,Dock.Top);Children.Add(head);var tools=new DockPanel();DockPanel.SetDock(tools,Dock.Top);Children.Add(tools);var fresh=Ui.Button("新对话",()=>{CancelRequest();Persist(()=>conversation.NewBranch());editId=null;input.Text="";Render();});DockPanel.SetDock(fresh,Dock.Right);tools.Children.Add(fresh);branches.Tag="ConversationBranches";branches.MinWidth=100;tools.Children.Add(branches);branches.SelectionChanged+=(_,_)=>{if(!rendering&&branches.SelectedItem is ComboBoxItem{Tag:string id})Ui.Safe(()=>SelectBranch(id));};DockPanel.SetDock(hint,Dock.Top);Children.Add(hint);
        var bottom=new StackPanel();DockPanel.SetDock(bottom,Dock.Bottom);Children.Add(bottom);bottom.Children.Add(composeLabel);input.AcceptsReturn=true;input.TextWrapping=TextWrapping.Wrap;input.Height=105;input.MaxLength=8000;input.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;input.Tag="ConversationInput";bottom.Children.Add(input);var actions=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right};bottom.Children.Add(actions);editCancel=Ui.Button("取消编辑",()=>{editId=null;input.Text=previousDraft;Render(false);});actions.Children.Add(editCancel);retry=Ui.Button("重试本轮",()=>Run(RequestAsync));actions.Children.Add(retry);cancel=Ui.Button("取消回答",CancelRequest);actions.Children.Add(cancel);send=Ui.Button("发送",()=>Run(SendInput),true);send.Tag="ConversationSend";actions.Children.Add(send);bottom.Children.Add(status);bottom.Children.Add(receipts);
        transcript=new(){Content=log,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,MaxHeight=460,MinHeight=160};Children.Add(transcript);input.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Enter&&(Keyboard.Modifiers&ModifierKeys.Control)!=0){Run(SendInput);e.Handled=true;}};Render(false);
    }
    async void Run(Func<Task> action){try{await action();}catch(Exception ex){status.Text=ex.Message;}}
    void Persist(Action change){if(readOnly)throw new InvalidOperationException("对话记录暂不可写，原记录已保留。请先恢复对话文件。");var before=JsonKit.Clone(conversation);try{change();store.Save(conversation);}catch{conversation=before;throw;}}
    async Task SendInput(){string text=input.Text;if(editId is {} id){editId=null;input.Text="";await EditMessageAsync(id,text);}else{input.Text="";await SendAsync(text);}}
    public async Task SendAsync(string text){if(pending is not null)throw new InvalidOperationException("正在回答，可取消后继续。");Persist(()=>conversation.AddUser(text));Render();await RequestAsync();}
    public async Task EditMessageAsync(string messageId,string replacement){CancelRequest();Persist(()=>conversation.ForkAt(messageId,replacement));editId=null;Render();await RequestAsync();}
    public void SelectBranch(string id){if(!conversation.Branches.Any(b=>b.Id==id))throw new ArgumentException("对话分支不存在。");CancelRequest();Persist(()=>conversation.ActiveBranchId=id);editId=null;input.Text="";Render();}
    void CancelRequest(){serial++;pending?.Cancel();pending=null;if(!disposed){status.Text="已取消回答，已发送消息和原有行程均保留。";SetBusy();}}
    async Task RequestAsync()
    {
        if(disposed||pending is not null||conversation.Active.Messages.LastOrDefault()?.Role!="user")return;var source=new CancellationTokenSource();pending=source;long turn=++serial,version=conversation.Active.Version;string branch=conversation.ActiveBranchId;var messages=conversation.Active.Messages.Select(JsonKit.Clone).ToList();status.Text="正在理解并补齐需求，尚未修改行程…";SetBusy();
        try{var config=new SettingsStore(vm.DataDirectory).Load();hint.Text=GuidedAgentClient.IsLocal(config)?"本地规则引导 · 多项复杂规划可在设置连接模型":"连续对话 · 只发送当前分支的对话内容";var reply=await client.ReplyAsync(config,messages,source.Token);source.Token.ThrowIfCancellationRequested();if(disposed||turn!=serial||conversation.ActiveBranchId!=branch||conversation.Active.Version!=version)return;Persist(()=>conversation.AddAssistant(reply));status.Text=reply.Ready?"需求已汇总，核对后可生成行程草稿。":"可以继续自由补充，我会记住本分支已说明的信息。";Render();}
        catch(OperationCanceledException){if(!disposed&&turn==serial)status.Text="已取消回答，行程未改变。";}
        catch(Exception ex){if(!disposed&&turn==serial)status.Text=ex.Message;}
        finally{if(ReferenceEquals(pending,source))pending=null;source.Dispose();if(!disposed)SetBusy();}
    }
    void SetBusy(){send.IsEnabled=!readOnly&&pending is null;retry.Visibility=pending is null&&conversation.Active.Messages.LastOrDefault()?.Role=="user"?Visibility.Visible:Visibility.Collapsed;retry.IsEnabled=!readOnly;cancel.Visibility=pending is null?Visibility.Collapsed:Visibility.Visible;editCancel.Visibility=editId is null?Visibility.Collapsed:Visibility.Visible;composeLabel.Text=editId is null?"说说你的安排 · Ctrl+Enter发送":"编辑旧消息 · 发送后建立新分支";}
    void Render(bool scroll=true)
    {
        rendering=true;try{branches.Items.Clear();foreach(var b in conversation.Branches)branches.Items.Add(new ComboBoxItem{Content=b.Name,Tag=b.Id});branches.SelectedIndex=conversation.Branches.FindIndex(b=>b.Id==conversation.ActiveBranchId);}finally{rendering=false;}
        var config=new SettingsStore(vm.DataDirectory).Load();hint.Text=GuidedAgentClient.IsLocal(config)?"本地规则引导 · 多项复杂规划可在设置连接模型":"连续对话 · 只发送当前分支的对话内容";log.Children.Clear();if(conversation.Active.Messages.Count==0)Bubble("助手","这次想安排些什么？先说说要做的事，我会根据还缺少的信息继续询问。",null);
        foreach(var message in conversation.Active.Messages)Bubble(message.Role=="user"?"你":"助手",message.Content,message.Role=="user"?message.Id:null);
        if(conversation.Active.Reply is {Ready:true} reply){var summary=new StackPanel();summary.Children.Add(Ui.Text("行程需求汇总",15,true));summary.Children.Add(Ui.Text(reply.Summary.Length>0?reply.Summary:string.Join("\n",reply.Events.Select(e=>$"{e.Day:yyyy-MM-dd} · {e.Title} · {e.Location} · {e.Duration}分钟")),12));bool applied=conversation.Batches.Any(r=>r.BranchId==conversation.ActiveBranchId&&r.SourceVersion==conversation.Active.Version&&r.Status is "APPLIED" or "PENDING");var generate=Ui.Button(applied?"此方案已生成":"生成行程草稿",()=>OpenReview(),true);generate.Tag="ConversationGenerate";generate.IsEnabled=!applied&&!readOnly;summary.Children.Add(generate);log.Children.Add(Ui.Card(summary));}
        RenderReceipts();SetBusy();if(scroll)Dispatcher.BeginInvoke(()=>transcript.ScrollToEnd(),System.Windows.Threading.DispatcherPriority.Loaded);
    }
    void Bubble(string who,string text,string? id)
    {
        var panel=new StackPanel();var head=new DockPanel();if(id is not null){var edit=Ui.Button("编辑",()=>{previousDraft=input.Text;editId=id;input.Text=conversation.Active.Messages.Single(m=>m.Id==id).Content;SetBusy();input.Focus();});edit.Tag="EditMessage:"+id;edit.Padding=new(5,2,5,2);Typography.Set(edit,11);DockPanel.SetDock(edit,Dock.Right);head.Children.Add(edit);}head.Children.Add(Ui.Text(who,11));panel.Children.Add(head);panel.Children.Add(Ui.Text(text,13));var bubble=Ui.Card(panel,id is null?"#FFFDF9":"#EEE1D1");bubble.Padding=new(11);bubble.Margin=new(id is null?0:14,0,0,10);log.Children.Add(bubble);
    }
    void RenderReceipts()
    {
        receipts.Children.Clear();foreach(var receipt in conversation.Batches.Where(r=>r.BranchId==conversation.ActiveBranchId&&r.Status is "APPLIED" or "PENDING" or "UNDO_PENDING" or "CONFLICT").Reverse()){
            receipts.Children.Add(Ui.Text($"{receipt.CreatedAt:MM-dd HH:mm} · 生成 {receipt.Activities.Count} 项"+(receipt.Status=="CONFLICT"?" · 需要核对":""),11));var undo=Ui.Button("撤回此批次",()=>UndoBatch(receipt.Id));undo.IsEnabled=receipt.Status=="APPLIED";undo.Tag="UndoBatch:"+receipt.Id;receipts.Children.Add(undo);
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
    public void Dispose(){if(disposed)return;disposed=true;CancelRequest();review?.Close();}
}
