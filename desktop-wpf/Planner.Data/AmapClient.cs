using System.Net.Http;
using System.Globalization;
using System.Text.Json;
using Planner.Core;
namespace Planner.Data;
public sealed class AmapClient(string key,HttpClient? http=null):IMapProvider
{
    readonly HttpClient client=http??new(){Timeout=TimeSpan.FromSeconds(30)};
    IReadOnlyList<IReadOnlyList<GeoPoint>>? boundaries;
    async Task<string> Get(string path,Dictionary<string,string> args,CancellationToken token)
    {
        if(string.IsNullOrWhiteSpace(key))throw new ArgumentException("请在服务设置中填写高德Web服务Key。");
        args["key"]=key;
        var url="https://restapi.amap.com/"+path+"?"+string.Join("&",args.Select(x=>Uri.EscapeDataString(x.Key)+"="+Uri.EscapeDataString(x.Value)));
        try
        {
            using var response=await client.GetAsync(url,token);
            if(!response.IsSuccessStatusCode)throw new InvalidOperationException($"地图服务返回HTTP {(int)response.StatusCode}，请检查服务配置。");
            var text=await response.Content.ReadAsStringAsync(token);if(text.Length>8_000_000)throw new InvalidOperationException("地图响应超出大小限制。");
            using var doc=JsonDocument.Parse(text);var root=doc.RootElement;
            if(S(root,"status")!="1")throw new InvalidOperationException("地图服务拒绝请求（代码 "+S(root,"infocode")+"），请检查Key权限、配额和服务配置。");
            return text;
        }
        catch(HttpRequestException){throw new InvalidOperationException("无法连接地图服务，请检查网络和服务设置。");}
    }
    async Task<IReadOnlyList<IReadOnlyList<GeoPoint>>> Boundary(CancellationToken token)
    {
        if(boundaries is not null)return boundaries;
        var json=await Get("v3/config/district",new(){["keywords"]="320100",["subdistrict"]="0",["extensions"]="all"},token);
        using var d=JsonDocument.Parse(json);var district=A(d.RootElement,"districts").FirstOrDefault();
        var polygons=S(district,"polyline").Split('|',StringSplitOptions.RemoveEmptyEntries).Select(x=>(IReadOnlyList<GeoPoint>)Points(x)).Where(x=>x.Count>=3).ToList();
        if(polygons.Count==0)throw new InvalidOperationException("未取得南京市行政边界，无法验证市内路线。请检查行政区域查询权限。");
        return boundaries=polygons;
    }
    public async Task<List<Place>> SearchPlacesAsync(string text,CancellationToken token)
    {
        if(string.IsNullOrWhiteSpace(text))throw new ArgumentException("请输入地点名称。");
        var bounds=await Boundary(token);
        var json=await Get("v3/place/text",new(){["keywords"]=text,["city"]="320100",["citylimit"]="true",["offset"]="15",["extensions"]="base"},token);
        using var doc=JsonDocument.Parse(json);var result=new List<Place>();
        foreach(var p in A(doc.RootElement,"pois"))
        {
            var point=Points(S(p,"location")).FirstOrDefault();
            if(!S(p,"adcode").StartsWith("3201",StringComparison.Ordinal)||!bounds.Any(x=>GeoBoundary.Contains(x,point)))continue;
            result.Add(new(){InputName=text,Name=S(p,"name"),Address=S(p,"address"),Lng=point.Lng,Lat=point.Lat,CoordType="GCJ02",ProviderId=S(p,"id"),Adcode=S(p,"adcode"),Status="VERIFIED",Source="高德地点搜索"});
        }
        return result;
    }
    public async Task<List<RouteOption>> RoutesAsync(Place from,Place to,string mode,DateTime depart,CancellationToken token)
    {
        var bounds=await Boundary(token);
        if(from.Lng is null||from.Lat is null||to.Lng is null||to.Lat is null)throw new ArgumentException("地点尚未确认坐标。");
        if(!bounds.Any(x=>GeoBoundary.Contains(x,new(from.Lng.Value,from.Lat.Value)))||!bounds.Any(x=>GeoBoundary.Contains(x,new(to.Lng.Value,to.Lat.Value))))throw new ArgumentException("路线起终点必须在南京市内。");
        string Loc(Place p)=>p.Lng!.Value.ToString("0.######",CultureInfo.InvariantCulture)+","+p.Lat!.Value.ToString("0.######",CultureInfo.InvariantCulture);
        var args=new Dictionary<string,string>{["origin"]=Loc(from),["destination"]=Loc(to),["show_fields"]="cost,polyline"};
        var path=mode switch{"CAR" or "TAXI"=>"driving","BIKE"=>"bicycling","TRANSIT" or "BUS" or "METRO"=>"transit/integrated",_=>"walking"};
        if(path=="driving")args["show_fields"]="cost,polyline,tmcs";
        if(path=="transit/integrated"){args["city1"]="025";args["city2"]="025";args["ad1"]="320100";args["ad2"]="320100";args["strategy"]=mode=="METRO"?"7":"0";args["date"]=depart.ToString("yyyy-MM-dd");args["time"]=depart.ToString("HH-mm");}
        var json=await Get("v5/direction/"+path,args,token).ConfigureAwait(false);
        return ParseRoutes(json,mode,bounds);
    }
    public static List<RouteOption> ParseRoutes(string json,string mode,IReadOnlyList<IReadOnlyList<GeoPoint>> boundaries)
    {
        using var doc=JsonDocument.Parse(json);var root=doc.RootElement;if(S(root,"status")!="1")return[];
        var route=P(root,"route");var results=new List<RouteOption>();
        bool ValidGeometry(string geometry)
        {
            var pts=Points(geometry);if(pts.Count<2)return false;
            for(int i=1;i<pts.Count;i++)if(!GeoBoundary.CoversSegment(boundaries,pts[i-1],pts[i]))return false;
            return true;
        }
        foreach(var path in A(route,mode is "TRANSIT" or "BUS" or "METRO"?"transits":"paths"))
        {
            var cost=P(path,"cost");int duration=N(cost,"duration")??N(path,"duration")??0;if(duration<=0)continue;
            var option=new RouteOption{DurationSec=duration,DistanceM=N(path,"distance")??0};
            if(mode is "TRANSIT" or "BUS" or "METRO")
            {
                bool rejected=false;
                foreach(var part in A(path,"segments"))
                {
                    var train=P(part,"railway");if(train.ValueKind==JsonValueKind.Object&&train.EnumerateObject().Any()){rejected=true;break;}
                    var walking=P(part,"walking");var walkSteps=A(walking,"steps").ToList();
                    if(walkSteps.Count>0)option.Segments.Add(new(){Mode="WALK",Instruction=string.Join("；",walkSteps.Select(x=>S(x,"instruction"))),Geometry=string.Join(";",walkSteps.Select(x=>S(x,"polyline"))),DistanceM=N(walking,"distance"),DurationSec=N(P(walking,"cost"),"duration")??N(walking,"duration")??0,KnownCostFen=0});
                    var bus=A(P(part,"bus"),"buslines").FirstOrDefault();
                    if(bus.ValueKind==JsonValueKind.Object)
                    {
                        var type=S(bus,"type");var name=S(bus,"name");
                        if(new[]{"火车","铁路","城际","长途","航空"}.Any(x=>(type+name).Contains(x))){rejected=true;break;}
                        var transport=type.Contains("地铁")||type.Contains("轻轨")||name.Contains("地铁")?"METRO":"BUS";
                        option.Segments.Add(new(){Mode=transport,Instruction=name+"："+S(P(bus,"departure_stop"),"name")+" → "+S(P(bus,"arrival_stop"),"name"),Geometry=S(bus,"polyline"),DurationSec=N(P(bus,"cost"),"duration")??N(bus,"duration")??0,DistanceM=N(bus,"distance")});
                    }
                    var taxi=P(part,"taxi");if(taxi.ValueKind==JsonValueKind.Object&&taxi.EnumerateObject().Any())rejected=true;
                }
                if(rejected||option.Segments.Count==0)continue;
                var fare=Money(cost,"transit_fee")??Money(path,"cost");option.KnownCostFen=fare;option.CostComplete=fare.HasValue;option.CostNote=fare.HasValue?"公共交通票价参考":"票价暂未返回";
                option.Label=string.Join(" + ",option.Segments.Select(x=>Name(x.Mode)).Distinct());
            }
            else
            {
                var steps=A(path,"steps").ToList();var geometry=string.Join(";",steps.Select(x=>S(x,"polyline")));
                string? congestion=null;
                if(mode is "CAR" or "TAXI")
                {
                    var states=steps.SelectMany(x=>A(x,"tmcs")).Select(x=>S(x,"status")).Where(x=>x.Length>0&&x!="未知").Distinct().ToArray();if(states.Length>0)congestion=string.Join(" / ",states);
                }
                int? fee=mode switch{"WALK"=>0,"CAR"=>Money(cost,"tolls"),"TAXI"=>Money(cost,"taxi")??Money(P(route,"cost"),"taxi_fee")??Money(route,"taxi_cost"),_=>null};
                option.KnownCostFen=fee;option.CostComplete=mode=="WALK";
                option.CostNote=mode switch{"CAR"=>"仅含已返回过路费；油费、停车费未知","TAXI"=>"车费参考；等候及其他附加费未知","BIKE"=>"共享骑行费用及可用车辆未知",_=>"步行交通费为零"};
                option.Label=Name(mode);
                option.Segments.Add(new(){Mode=mode,Instruction=string.Join("；",steps.Select(x=>S(x,"instruction"))),Geometry=geometry,DurationSec=duration,DistanceM=option.DistanceM,KnownCostFen=fee,Congestion=congestion,CostDetailJson=System.Text.Json.JsonSerializer.Serialize(new{note=option.CostNote},JsonKit.Options)});
            }
            if(option.Segments.Any(x=>!ValidGeometry(x.Geometry??"")))continue;
            if(mode=="METRO"&&option.Uses("BUS")||mode=="BUS"&&option.Uses("METRO"))continue;
            option.Label+=$" · 方案{results.Count+1}";results.Add(option);
        }
        return results;
    }
    public static string Name(string mode)=>mode switch{"WALK"=>"步行","BIKE"=>"骑行","BUS"=>"公交","METRO"=>"地铁","CAR"=>"自驾","TAXI"=>"打车",_=>mode};
    static JsonElement P(JsonElement e,string key)=>e.ValueKind==JsonValueKind.Object&&e.TryGetProperty(key,out var value)?value:default;
    static string S(JsonElement e,string key){var p=P(e,key);return p.ValueKind is JsonValueKind.String or JsonValueKind.Number?p.ToString():"";}
    static IEnumerable<JsonElement> A(JsonElement e,string key){var p=P(e,key);return p.ValueKind==JsonValueKind.Array?p.EnumerateArray():[];}
    static int? N(JsonElement e,string key)=>double.TryParse(S(e,key),NumberStyles.Float,CultureInfo.InvariantCulture,out var x)&&double.IsFinite(x)&&x>=0&&x<int.MaxValue?(int)Math.Ceiling(x):null;
    static int? Money(JsonElement e,string key)=>decimal.TryParse(S(e,key),NumberStyles.Number,CultureInfo.InvariantCulture,out var x)&&x>=0&&x<int.MaxValue/100?(int)Math.Round(x*100):null;
    static List<GeoPoint> Points(string text)
    {
        var result=new List<GeoPoint>();foreach(var raw in text.Split(';',StringSplitOptions.RemoveEmptyEntries)){var bits=raw.Split(',');if(bits.Length==2&&double.TryParse(bits[0],NumberStyles.Float,CultureInfo.InvariantCulture,out var lng)&&double.TryParse(bits[1],NumberStyles.Float,CultureInfo.InvariantCulture,out var lat)&&double.IsFinite(lng)&&double.IsFinite(lat))result.Add(new(lng,lat));}return result;
    }
}
