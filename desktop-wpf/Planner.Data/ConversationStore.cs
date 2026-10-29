using System.Text.Json;
using Planner.Core;
namespace Planner.Data;
public sealed class ConversationStore(string directory)
{
    readonly string path=Path.Combine(directory,"conversations.json");
    public GuidedConversation Load()
    {
        if(!File.Exists(path))return new();if(new FileInfo(path).Length>16*1024*1024)throw new InvalidOperationException("本地对话记录过大，请先备份并整理，原文件未修改。");
        GuidedConversation value;try{value=JsonSerializer.Deserialize<GuidedConversation>(File.ReadAllText(path),JsonKit.Options)??throw new JsonException();Validate(value);}catch(Exception e)when(e is JsonException or ArgumentException){throw new InvalidOperationException("对话记录无法读取，原文件已保留，请从备份恢复或联系维护者。",e);}return value;
    }
    static void Validate(GuidedConversation value)
    {
        if(value.Branches is null||value.Branches.Count==0||value.Batches is null||value.Branches.Any(b=>b is null||b.Messages is null||b.Messages.Any(m=>m is null||m.Role is not("user" or "assistant")||m.Content is null)))throw new ArgumentException("对话结构不完整。");
        if(value.Version<0||value.Branches.Select(b=>b.Id).Distinct().Count()!=value.Branches.Count)throw new ArgumentException("对话版本或分支标识无效。");
        foreach(var b in value.Branches){if(b.Messages.Any(m=>string.IsNullOrWhiteSpace(m.Id))||b.Messages.Select(m=>m.Id).Distinct().Count()!=b.Messages.Count)throw new ArgumentException("对话消息标识无效。");if(b.Reply is {} r){if(string.IsNullOrWhiteSpace(r.Message)||r.Summary is null||r.Events is null||r.Missing is null||r.Events.Any(e=>e is null)||r.Missing.Any(m=>m is null))throw new ArgumentException("对话汇总结构不完整。");if(r.Ready){if(r.Events.Count is <1 or >15||r.Missing.Count!=0)throw new ArgumentException("已完成的对话缺少活动。");foreach(var e in r.Events)RepositoryPlanner.Validate(e);}}}
        if(value.Batches.Any(b=>b is null||b.Activities is null||b.Tables is null||b.Places is null||b.Reminders is null||b.Activities.Count is <1 or >15||b.Activities.Any(a=>a is null||string.IsNullOrWhiteSpace(a.Id))||b.Tables.Any(t=>t is null)||b.Places.Any(p=>p is null)||b.Reminders.Any(r=>r is null)))throw new ArgumentException("生成批次记录不完整。");
        if(!value.Branches.Any(b=>b.Id==value.ActiveBranchId))value.ActiveBranchId=value.Branches[0].Id;
    }
    public void Save(GuidedConversation value)
    {
        Validate(value);Directory.CreateDirectory(directory);using var fileLock=AcquireWriteLock();var prior=Load();if(prior.Version!=value.Version)throw new InvalidOperationException("对话已被其他窗口更新，请重新载入后继续。");var copy=JsonKit.Clone(value);copy.Version++;string json=JsonSerializer.Serialize(copy,JsonKit.Options);if(System.Text.Encoding.UTF8.GetByteCount(json)>16*1024*1024)throw new InvalidOperationException("对话记录已达容量限制，请备份整理后继续。");string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{File.WriteAllText(temp,json);File.Move(temp,path,true);value.Version=copy.Version;}finally{if(File.Exists(temp))File.Delete(temp);}
    }
    FileStream AcquireWriteLock(){try{return new FileStream(path+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}catch(IOException e){throw new InvalidOperationException("对话正在由其他窗口保存，请稍后重试。",e);}}
}
