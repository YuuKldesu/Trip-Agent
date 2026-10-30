using System.Windows;
using Planner.Core;
using Planner.Data;
namespace Planner.Desktop;
public sealed partial class EditorWindow
{
    async Task<(PlannerState,List<TrafficSnapshot>)> CheckAdjacentAsync(PlannerState proposed,Occurrence target,EditCommand command)
    {
        var pairs=AdjacentTraffic.Around(proposed,target);var results=new List<TrafficSnapshot>();var warnings=new List<string>();
        if(pairs.Count==0)return(proposed,results);
        var stored=new TrafficSnapshotStore(vm.DataDirectory).Load();var settings=new SettingsStore(vm.DataDirectory).Load();
        bool online=false;RepositoryMap? map=null;string setupError="尚未配置高德 Web 服务 Key，无法查询实际交通时间。";try{online=SettingsStore.Unprotect(settings.MapServiceKeyProtected).Length>0;if(online)map=new RepositoryMap(settings);}catch{setupError="地图服务配置无法读取，请在设置中重新保存密钥。";}var resolved=new Dictionary<string,Place>();
        async Task<Place> Resolve(Occurrence o)
        {
            if(resolved.TryGetValue(o.Key,out var known))return known;
            var p=proposed.Places.FirstOrDefault(p=>p.Id==o.PlaceId);
            if(p?.Lng is not null&&p.Lat is not null&&p.Status!="UNVERIFIED")return resolved[o.Key]=p;
            string query=p?.Name??"";
            if(query.Length==0)query=await TrafficDialogs.Location(this,o.Title,cancel.Token);
            status.Text="正在查询地点："+query;
            var selected=await RepositoryDialogs.ChoosePlace(this,await map!.SearchAsync(query,cancel.Token),cancel.Token);
            resolved[o.Key]=selected;return selected;
        }
        bool appliedTarget=false;
        for(int index=0;index<pairs.Count;index++)
        {
            cancel.Token.ThrowIfCancellationRequested();var pair=pairs[index];string fingerprint=pair.Fingerprint(proposed);
            TrafficSnapshot? snapshot=stored.GetValueOrDefault(fingerprint);
            if(snapshot?.Minutes is null)
            {
                try
                {
                    var from=proposed.Places.FirstOrDefault(p=>p.Id==pair.From.PlaceId);var to=proposed.Places.FirstOrDefault(p=>p.Id==pair.To.PlaceId);
                    bool same=from is not null&&to is not null&&from.Id==to.Id&&from.Lng is not null&&from.Lat is not null&&from.Status!="UNVERIFIED";
                    TravelEstimate estimate;string mode="同一地点";
                    if(same)estimate=new(0,"同一地点");
                    else
                    {
                        if(!online)throw new InvalidOperationException(setupError);
                        from=await Resolve(pair.From);to=await Resolve(pair.To);
                        var targetPlace=resolved.GetValueOrDefault(target.Key);
                        if(targetPlace is not null&&!appliedTarget&&targetPlace.Id!=target.PlaceId)
                        {
                            // Only the edited activity adopts its explicitly selected location. Neighbor selections are query snapshots.
                            command.Place=targetPlace;command.Activity.PlaceId=targetPlace.Id;
                            proposed=ChangeService.Apply(vm.State,[command],openedRevision);
                            var updated=CalendarEngine.ExpandForDisplay(proposed,target.Start.Date,target.End.Date.AddDays(1)).First(o=>o.Start==target.Start&&o.End==target.End&&o.Title==target.Title&&o.Activity.TableId==target.Activity.TableId);
                            for(int j=0;j<pairs.Count;j++){var edge=pairs[j];pairs[j]=edge.From.Key==target.Key?new(updated,edge.To):new(edge.From,updated);}
                            pair=pairs[index];appliedTarget=true;
                            // Future-rule edits can create a new ID each projection; do not rebuild that projection again.
                            target=updated;resolved[target.Key]=targetPlace;fingerprint=pair.Fingerprint(proposed);
                        }
                        mode=await TrafficDialogs.Mode(this,pair,stored.Values.FirstOrDefault(x=>x.FromKey==pair.From.Key&&x.ToKey==pair.To.Key)?.Mode,cancel.Token);
                        status.Text="正在检查："+pair.From.Title+" → "+pair.To.Title;
                        estimate=await map!.TravelAsync(from,to,mode,pair.From.End,cancel.Token);
                        estimate=estimate with{Source=from.Name+" → "+to.Name+" · "+estimate.Source};
                    }
                    snapshot=new(){Fingerprint=fingerprint,FromKey=pair.From.Key,ToKey=pair.To.Key,Minutes=estimate.Minutes,Mode=mode,Source=estimate.Source};
                }
                catch(OperationCanceledException){throw;}
                catch(Exception ex){snapshot=new(){Fingerprint=fingerprint,FromKey=pair.From.Key,ToKey=pair.To.Key,Source=ex.Message};warnings.Add(pair.From.Title+" → "+pair.To.Title+"：交通未核验。"+ex.Message);}
            }
            results.Add(snapshot);
            if(snapshot.Minutes is {} minutes&&minutes>pair.GapMinutes)warnings.Add(pair.From.Title+" → "+pair.To.Title+"："+AdjacentTraffic.Label(pair,snapshot));
        }
        if(command.Scope=="FUTURE")warnings.Add("交通结果仅针对本次日期；后续重复日期需按当天安排重新检查。");
        if(warnings.Count>0&&!await TrafficDialogs.SaveWarning(this,string.Join("\n\n",warnings),cancel.Token))throw new OperationCanceledException();
        return(proposed,results);
    }
}
