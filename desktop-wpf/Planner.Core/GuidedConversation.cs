using System.Text.Json.Serialization;
namespace Planner.Core;

public sealed class ConversationMessage:Entity
{
    public string Role {get;set;}="user";
    public string Content {get;set;}="";
    public DateTime CreatedAt {get;set;}=Clock.Now;
}
public sealed class GuidedReply
{
    public string Message {get;set;}="";
    public string Summary {get;set;}="";
    public List<string> Missing {get;set;}=[];
    public List<TripRequest> Events {get;set;}=[];
    public bool Ready {get;set;}
}
public sealed class ConversationBranch:Entity
{
    public string Name {get;set;}="初始对话";
    public List<ConversationMessage> Messages {get;set;}=[];
    public GuidedReply? Reply {get;set;}
    public long Version {get;set;}
}
public sealed class GuidedConversation
{
    public long Version {get;set;}
    public string ActiveBranchId {get;set;}
    public List<ConversationBranch> Branches {get;set;}=[];
    public List<GeneratedBatchReceipt> Batches {get;set;}=[];
    public Dictionary<string,string> Drafts {get;set;}=[];
    [JsonIgnore] public ConversationBranch Active=>Branches.First(b=>b.Id==ActiveBranchId);
    public GuidedConversation(){var b=new ConversationBranch();Branches.Add(b);ActiveBranchId=b.Id;}
    public ConversationBranch NewBranch(){var b=new ConversationBranch{Name="新对话 "+(Branches.Count+1)};Branches.Add(b);ActiveBranchId=b.Id;return b;}
    public ConversationMessage AddUser(string text)
    {
        text=text.Trim();if(text.Length is <1 or >8000)throw new ArgumentException("每条消息请填写1—8000字。");if(Active.Messages.Count>=100)throw new ArgumentException("此分支已较长，请新建对话后继续。");
        var m=new ConversationMessage{Content=text};Active.Messages.Add(m);Active.Reply=null;Active.Version++;return m;
    }
    public void AddAssistant(GuidedReply reply){Active.Messages.Add(new(){Role="assistant",Content=reply.Message});Active.Reply=JsonKit.Clone(reply);Active.Version++;}
    public ConversationBranch ForkAt(string messageId,string replacement)
    {
        var old=Active;int index=old.Messages.FindIndex(m=>m.Id==messageId&&m.Role=="user");if(index<0)throw new ArgumentException("只能编辑当前分支中已发送的用户消息。");replacement=replacement.Trim();if(replacement.Length is <1 or >8000)throw new ArgumentException("消息应为1—8000字。");
        var branch=new ConversationBranch{Name=$"分支 {Branches.Count+1} · 重写第 {old.Messages.Take(index+1).Count(m=>m.Role=="user")} 条",Messages=old.Messages.Take(index).Select(JsonKit.Clone).ToList()};Branches.Add(branch);ActiveBranchId=branch.Id;AddUser(replacement);return branch;
    }
}
