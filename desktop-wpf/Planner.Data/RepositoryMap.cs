using Planner.Core;
using System.Net.Http;
namespace Planner.Data;
public sealed class RepositoryMap(ServiceSettings settings,HttpClient? http=null)
{
    readonly string key=SettingsStore.Unprotect(settings.MapServiceKeyProtected);
    public static readonly IReadOnlyDictionary<string,(double Lng,double Lat)> Presets=new Dictionary<string,(double,double)>{["南京南站"]=(118.8022,31.9745),["南京站"]=(118.7978,32.0871),["新街口"]=(118.7857,32.0417),["夫子庙"]=(118.7886,32.0215),["南京博物院"]=(118.8484,32.0448),["玄武湖"]=(118.803,32.0733),["中山陵"]=(118.8529,32.0617),["南京大学"]=(118.7792,32.0572),["总统府"]=(118.8036,32.0444),["南京图书馆"]=(118.797,32.042)};
    public async Task<List<Place>> SearchAsync(string query,CancellationToken token)
    {
        if(string.IsNullOrWhiteSpace(query))throw new ArgumentException("请填写地点名称。");
        if(key.Length>0)return await new AmapClient(key,http).SearchPlacesAsync(query,token);
        var result=Presets.Where(p=>p.Key.Contains(query)||query.Contains(p.Key)).Select(p=>new Place{InputName=query,Name=p.Key,Lng=p.Value.Lng,Lat=p.Value.Lat,Status="UNVERIFIED",Source="本地示例坐标，未核实入口"}).ToList();
        if(result.Count==0)throw new ArgumentException("未配置高德Key，本地模式仅支持预置南京地点；可先手动填写不含交通的活动，或配置高德后搜索。");return result;
    }
    public async Task<TravelEstimate> TravelAsync(Place from,Place to,string mode,DateTime depart,CancellationToken token)
    {
        if(from.Id==to.Id||(from.Lng is not null&&from.Lng==to.Lng&&from.Lat==to.Lat))return new(0,"同一地点");
        if(from.Lng is null||from.Lat is null||to.Lng is null||to.Lat is null)throw new ArgumentException("相邻活动缺少坐标，请先搜索并选择地点。");
        if(key.Length>0){var options=await new AmapClient(key,http).RoutesAsync(from,to,mode switch{"公交"=>"TRANSIT","骑行"=>"BIKE","驾车"=>"CAR",_=>"WALK"},depart,token);if(options.Count==0)throw new InvalidOperationException("地图未找到可用路线。");return new((int)Math.Ceiling(options[0].DurationSec/60d),mode+" · 高德查询快照"){Option=JsonKit.Clone(options[0])};}
        double lat=(from.Lat.Value+to.Lat.Value)/2*Math.PI/180,km=Math.Sqrt(Math.Pow((from.Lng.Value-to.Lng.Value)*111*Math.Cos(lat),2)+Math.Pow((from.Lat.Value-to.Lat.Value)*111,2));double speed=mode switch{"驾车"=>25,"公交"=>18,"骑行"=>12,_=>4};
        return new(Math.Max(1,(int)Math.Ceiling(km*1.5/speed*60)+(mode=="公交"?10:0)),mode+" · 直线距离粗估（未联网核验）");
    }
}
