using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Planner.Core;
namespace Planner.Desktop;

public sealed partial class AgentReviewWindow
{
 readonly Button calculate=Ui.Button("计算时间与路程",()=>{});
 readonly DispatcherTimer previewTimer=new(){Interval=TimeSpan.FromMilliseconds(350)};
 CancellationTokenSource? previewCancel;
 readonly Dictionary<string,TravelEstimate> routeCache=new();
 long draftVersion;bool applyingPreview,saving,timerWired;
 void WireScheduling(Card c,Func<bool> syncing)
 {
  void Changed(){if(!syncing())DraftChanged();}
  c.Begin.Changed+=()=>{if(applyingPreview||syncing())return;c.AutoStart=string.IsNullOrWhiteSpace(c.Begin.Text);DraftChanged();};
  c.End.Changed+=()=>{if(applyingPreview||syncing())return;if(!string.IsNullOrWhiteSpace(c.Begin.Text))c.AutoStart=false;else{applyingPreview=true;try{c.Deadline.Text=c.End.Text;}finally{applyingPreview=false;}}DraftChanged(c);};
  c.Duration.TextChanged+=(_,_)=>Changed();c.Day.SelectedDateChanged+=(_,_)=>Changed();c.EndDay.SelectedDateChanged+=(_,_)=>{if(!applyingPreview&&!syncing()&&!string.IsNullOrWhiteSpace(c.Begin.Text))c.AutoStart=false;Changed();};c.Mode.SelectionChanged+=(_,_)=>Changed();c.Search.Changed+=Changed;c.Title.TextChanged+=(_,_)=>Changed();c.Note.TextChanged+=(_,_)=>Changed();c.Deadline.TextChanged+=(_,_)=>Changed();c.Lead.TextChanged+=(_,_)=>Changed();c.Reminder.Checked+=(_,_)=>Changed();c.Reminder.Unchecked+=(_,_)=>Changed();
  if(!timerWired){timerWired=true;previewTimer.Tick+=async(_,_)=>{previewTimer.Stop();try{await CalculateAsync(false);}catch(OperationCanceledException){}catch(Exception ex){if(!closing)status.Text=ex.Message;}};}
 }
 void DraftChanged(Card? editingEnd=null)
 {
  if(applyingPreview||closing)return;draftVersion++;previewCancel?.Cancel();previewTimer.Stop();applyingPreview=true;
  try{foreach(var c in cards){if(c.Traffic is not null)c.Traffic.Visibility=Visibility.Collapsed;if(c.OutgoingTraffic is not null)c.OutgoingTraffic.Visibility=Visibility.Collapsed;if(c.AutoStart){c.Begin.Text="";if(!ReferenceEquals(c,editingEnd))c.End.Text="";}}}finally{applyingPreview=false;}
  if(IsLoaded&&!saving){status.Text="安排有变动，正在重新核对时间与路程…";previewTimer.Start();}
 }
 List<PreparedTrip> ReadDraft()
 {
  var prepared=new List<PreparedTrip>();
  foreach(var c in cards)
  {
   var r=JsonKit.Clone(c.Original);r.Title=c.Title.Text.Trim();r.Day=c.Day.SelectedDate is {} d?DateOnly.FromDateTime(d):null;r.Start=c.AutoStart?null:Minute(c.Begin.Text);r.End=c.AutoStart?null:Minute(c.End.Text);r.LatestEnd=Minute(c.Deadline.Text) is {} limit?limit+c.DeadlineDayOffset*1440:null;r.Location=c.Query.Text.Trim();r.Notes=c.Note.Text.Trim();r.Duration=int.TryParse(c.Duration.Text,out var duration)?duration:null;r.ModeIn=c.Mode.SelectedIndex>0?c.Mode.Text:null;r.EndDay=null;
   if(r.Start is {} begin&&r.End is {} end&&r.Day is {} first&&c.EndDay.SelectedDate is {} last){r.EndDay=DateOnly.FromDateTime(last);int span=(r.EndDay.Value.DayNumber-first.DayNumber)*1440+end-begin;if(span<(r.IsReturn?0:1))throw new ArgumentException("结束日期时间必须晚于开始。");r.Duration=span;}
   r.ReminderEnabled=c.Reminder.IsChecked==true;r.ReminderLeadMin=int.TryParse(c.Lead.Text,out var lead)?lead:throw new ArgumentException("提醒提前分钟应为0—10080。");RepositoryPlanner.Validate(r);var place=c.Chosen??throw new ArgumentException($"请为“{r.Title}”搜索并选择准确地点。");r.Location=place.Name;prepared.Add(new(r,place));
  }
  return prepared;
 }
 async Task<RepositoryPlan?> CalculateAsync(bool chooseRoute)
 {
  previewTimer.Stop();previewCancel?.Cancel();previewCancel?.Dispose();previewCancel=CancellationTokenSource.CreateLinkedTokenSource(cancel.Token);var token=previewCancel.Token;long own=draftVersion;
  var prepared=ReadDraft();status.Text="正在计算时间与路程…";
  var plan=await RepositoryPlanner.PlanAsync(vm.State.Copy(),prepared,async(from,to,mode,departure,ct)=>
  {

      string CacheKey(string? chosen)=>$"{from.Id}/{to.Id}/{chosen}/{departure:O}";
   if(routeCache.TryGetValue(CacheKey(mode),out var cached))return cached;
   if(string.IsNullOrWhiteSpace(mode)&&!chooseRoute)throw new ArgumentException("请为后续活动选择交通方式，或点击“计算时间与路程”比较方案。");
   var estimate=await RepositoryDialogs.Travel(this,map,from,to,mode,departure,ct);ct.ThrowIfCancellationRequested();
   string selected=mode??estimate.Source.Split(" · ")[0];routeCache[CacheKey(selected)]=estimate;routeCache[CacheKey(mode)]=estimate;
   if(string.IsNullOrWhiteSpace(mode)){var target=prepared.FindIndex(p=>p.Place.Id==to.Id);if(target>=0){prepared[target].Request.ModeIn=selected;applyingPreview=true;try{cards[target].Mode.SelectedItem=selected;}finally{applyingPreview=false;}}}
   return estimate;
  },token);
  token.ThrowIfCancellationRequested();if(closing||own!=draftVersion)return null;
  applyingPreview=true;
  try{for(int i=0;i<cards.Count;i++)
  {
   var c=cards[i];var row=plan.Rows[i];var begin=row.Trip.Request.Day!.Value.ToDateTime(TimeOnly.MinValue).AddMinutes(row.Start);var end=begin.AddMinutes(row.Trip.Request.Duration!.Value);c.Begin.DateContext=begin.Date;c.Day.SelectedDate=begin.Date;c.Begin.Text=begin.ToString("HH:mm");c.End.DateContext=end.Date;c.EndDay.SelectedDate=end.Date;c.End.Text=end.ToString("HH:mm");
   if(row.Before.Minutes>0&&row.TravelDepartAt is {} depart){var body=new StackPanel();body.Children.Add(Ui.Text("交通｜"+(prepared.Select(x=>x.Place).Concat(vm.State.Places).FirstOrDefault(p=>p.Id==row.PreviousPlaceId)?.Name??row.PreviousTitle)+" → "+c.Chosen?.Name,15,true));body.Children.Add(Ui.Text($"{depart:MM-dd HH:mm}—{depart.AddMinutes(row.Before.Minutes):MM-dd HH:mm} · {row.Before.Minutes}分钟",13,true));body.Children.Add(Ui.Text(row.Before.Source,12));c.Traffic!.Child=body;c.Traffic.Visibility=Visibility.Visible;}else c.Traffic!.Visibility=Visibility.Collapsed;
   c.OutgoingTraffic!.Visibility=Visibility.Collapsed;if(row.After.Minutes>0&&row.NextActivityId is not null&&!plan.Rows.Any(r=>r.ActivityId==row.NextActivityId)){string nextName=prepared.Select(x=>x.Place).Concat(vm.State.Places).FirstOrDefault(p=>p.Id==row.NextPlaceId)?.Name??row.NextTitle??"后续活动";var body=new StackPanel();body.Children.Add(Ui.Text("交通｜"+c.Chosen?.Name+" → "+nextName,15,true));body.Children.Add(Ui.Text($"{end:MM-dd HH:mm}—{end.AddMinutes(row.After.Minutes):MM-dd HH:mm} · {row.After.Minutes}分钟",13,true));body.Children.Add(Ui.Text(row.After.Source,12));c.OutgoingTraffic.Child=body;c.OutgoingTraffic.Visibility=Visibility.Visible;}
  }}finally{applyingPreview=false;}
  status.Text="已填入实际时间；交通将作为独立卡片保存。请核对后生成。";return plan;
 }
}
