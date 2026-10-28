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
        if(!value.Branches.Any(b=>b.Id==value.ActiveBranchId))value.ActiveBranchId=value.Branches[0].Id;
    }
    public void Save(GuidedConversation value)
    {
        Validate(value);var prior=Load();if(prior.Version!=value.Version)throw new InvalidOperationException("对话已被其他窗口更新，请重新载入后继续。");var copy=JsonKit.Clone(value);copy.Version++;string json=JsonSerializer.Serialize(copy,JsonKit.Options);if(System.Text.Encoding.UTF8.GetByteCount(json)>16*1024*1024)throw new InvalidOperationException("对话记录已达容量限制，请备份整理后继续。");Directory.CreateDirectory(directory);string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{File.WriteAllText(temp,json);File.Move(temp,path,true);value.Version=copy.Version;}finally{if(File.Exists(temp))File.Delete(temp);}
    }
}
