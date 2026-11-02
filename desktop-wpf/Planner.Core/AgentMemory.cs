namespace Planner.Core;

public sealed class MemoryUpdate
{
 public string Key {get;set;}="";
 public string Value {get;set;}="";
 public string SourceMessageId {get;set;}="";
 public string Evidence {get;set;}="";
 public bool Confirmed {get;set;}
}
public sealed class MemoryFact
{
 public string Value {get;set;}="";
 public string SourceMessageId {get;set;}="";
 public string Evidence {get;set;}="";
 public bool Confirmed {get;set;}
}
public sealed class PlaceCandidates
{
 public string Query {get;set;}="";
 public List<Place> Places {get;set;}=[];
 public DateTime QueryAt {get;set;}=Clock.Now;
}
public sealed class AgentRoute
{
 public string FromRef {get;set;}="";
 public string ToRef {get;set;}="";
 public string Mode {get;set;}="";
 public DateTime DepartAt {get;set;}=Clock.Now;
 public int Minutes {get;set;}
 public string Source {get;set;}="";
 public DateTime QueryAt {get;set;}=Clock.Now;
 public RouteOption? Option {get;set;}
 public int? KnownCostFen {get;set;}
 public bool CostComplete {get;set;}
}
public sealed class ScheduleRead
{
 public DateOnly FromDay {get;set;}
 public DateOnly ToDay {get;set;}
 public long Revision {get;set;}
 public DateTime QueryAt {get;set;}=Clock.Now;
 public bool Truncated {get;set;}
 public List<ScheduleReadItem> Events {get;set;}=[];
}
public sealed class ScheduleReadItem
{
 public string Title {get;set;}="";
 public string? Place {get;set;}
 public string? PlaceRef {get;set;}
 public string? Table {get;set;}
 public DateTime Start {get;set;}=Clock.Now;
 public DateTime End {get;set;}=Clock.Now;
}
public sealed class AgentMemory
{
 public Dictionary<string,MemoryFact> Facts {get;set;}=[];
 public Dictionary<string,Place> SelectedPlaces {get;set;}=[];
 public List<PlaceCandidates> Candidates {get;set;}=[];
 public List<AgentRoute> Routes {get;set;}=[];
 public List<ScheduleRead> ScheduleReads {get;set;}=[];
 public List<TripRequest> Draft {get;set;}=[];
 public string DraftStatus {get;set;}="NONE";
 public List<string> Missing {get;set;}=[];
 public void Apply(IReadOnlyList<MemoryUpdate> updates,IReadOnlyList<ConversationMessage> messages)
 {
  if(updates.Count>40)throw new ArgumentException("本轮记忆更新过多。");
  var next=JsonKit.Clone(Facts);
  foreach(var u in updates)
  {
   if(u is null||string.IsNullOrWhiteSpace(u.Key)||u.Key.Length>100||string.IsNullOrWhiteSpace(u.Value)||u.Value.Length>2000||string.IsNullOrWhiteSpace(u.Evidence)||u.Evidence.Length>2000)throw new ArgumentException("记忆更新格式不完整。");
   var source=messages.FirstOrDefault(m=>m.Id==u.SourceMessageId);
   if(source is null||!source.Content.Contains(u.Evidence,StringComparison.Ordinal)||u.Confirmed&&source.Role!="user")throw new ArgumentException("记忆缺少当前分支用户原文依据，请重新确认。");
   if(u.Confirmed&&!MemoryEvidence.Matches(u,source))throw new ArgumentException("确认值与用户原文不一致；请逐字记录原文，或使用可验证的日期/分钟规范化值。不能确认的推断请作为独立建议。");
   // A late model response cannot overwrite a correction with an older message.
   if(next.TryGetValue(u.Key,out var confirmed)&&confirmed.Confirmed&&!u.Confirmed)throw new ArgumentException("建议不能覆盖已确认需求，请使用独立建议字段。");
   if(next.TryGetValue(u.Key,out var prior)&&messages.ToList().FindIndex(m=>m.Id==prior.SourceMessageId)>messages.ToList().FindIndex(m=>m.Id==u.SourceMessageId))throw new ArgumentException("不能用更早的消息覆盖新的需求。");
   next[u.Key]=new(){Value=u.Value,SourceMessageId=u.SourceMessageId,Evidence=u.Evidence,Confirmed=u.Confirmed};
  }
  if(next.Count>80)throw new ArgumentException("行程记忆已达上限，请新建对话。");Facts=next;
 }
 public void Validate()
 {
  if(ScheduleReads is null||ScheduleReads.Count>8||ScheduleReads.Any(r=>r is null||r.Events is null||r.Events.Count>80)||Facts is null||SelectedPlaces is null||Candidates is null||Routes is null||Draft is null||Missing is null||Facts.Count>80||SelectedPlaces.Count>30||Candidates.Count>16||Routes.Count>32||Draft.Count>15||Facts.Any(x=>x.Value is null||x.Key.Length>100||x.Value.Value is null||x.Value.Value.Length>2000)||SelectedPlaces.Any(x=>x.Value is null)||Candidates.Any(x=>x is null||x.Places is null||x.Places.Count>8||x.Places.Any(p=>p is null)))throw new ArgumentException("行程记忆格式无效。");
 }
}
