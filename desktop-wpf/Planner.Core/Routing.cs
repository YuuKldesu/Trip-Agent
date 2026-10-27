namespace Planner.Core;
public readonly record struct GeoPoint(double Lng,double Lat);
public sealed class RouteOption
{
    public string Label {get;set;}="";
    public int DurationSec {get;set;}
    public int DistanceM {get;set;}
    public int? KnownCostFen {get;set;}
    public bool CostComplete {get;set;}
    public string CostNote {get;set;}="";
    public DateTime QueryAt {get;set;}=Clock.Now;
    public List<RouteSegment> Segments {get;set;}=[];
    public int WalkingM=>Segments.Where(s=>s.Mode=="WALK").Sum(s=>s.DistanceM??0);
    public bool Uses(string mode)=>Segments.Any(s=>s.Mode==mode);
    public string CostText=>KnownCostFen is {} c?$"已知 ¥{c/100.0:0.##}"+(CostComplete?"":" + 未知项"):"费用暂无数据";
    public override string ToString()=>$"{Label} · {Math.Ceiling(DurationSec/60.0)}分钟 · {CostText}";
}
public interface IMapProvider
{
    Task<List<Place>> SearchPlacesAsync(string text,CancellationToken token);
    Task<List<RouteOption>> RoutesAsync(Place from,Place to,string mode,DateTime depart,CancellationToken token);
}
public static class TransportPolicy
{
    public static bool Allows(RouteOption route,IReadOnlyCollection<string> modes)
    {
        bool transit=route.Uses("BUS")||route.Uses("METRO");
        return route.Segments.All(s=>modes.Contains(s.Mode)||s.Mode=="WALK"&&transit);
    }
    public static List<string> QueryModes(IReadOnlyCollection<string> modes)
    {
        var result=modes.Where(m=>m is not("BUS" or "METRO")).ToList();
        if(modes.Contains("BUS")&&modes.Contains("METRO"))result.Add("TRANSIT");else if(modes.Contains("BUS"))result.Add("BUS");else if(modes.Contains("METRO"))result.Add("METRO");return result;
    }
}
public static class GeoBoundary
{
    public static bool CoversSegment(IReadOnlyList<IReadOnlyList<GeoPoint>> boundaries,GeoPoint a,GeoPoint b)
    {
        bool Inside(GeoPoint p)=>boundaries.Any(x=>Contains(x,p));
        if(!Inside(a)||!Inside(b))return false;
        var cuts=new List<double>{0,1};double dx=b.Lng-a.Lng,dy=b.Lat-a.Lat;
        foreach(var polygon in boundaries)for(int i=0,j=polygon.Count-1;i<polygon.Count;j=i++)
        {
            var c=polygon[j];var d=polygon[i];double ex=d.Lng-c.Lng,ey=d.Lat-c.Lat,den=dx*ey-dy*ex;
            if(Math.Abs(den)<1e-16)continue;
            double t=((c.Lng-a.Lng)*ey-(c.Lat-a.Lat)*ex)/den,u=((c.Lng-a.Lng)*dy-(c.Lat-a.Lat)*dx)/den;
            if(t>0&&t<1&&u>=0&&u<=1)cuts.Add(t);
        }
        cuts.Sort();for(int i=1;i<cuts.Count;i++){double t=(cuts[i-1]+cuts[i])/2;if(!Inside(new(a.Lng+t*dx,a.Lat+t*dy)))return false;}
        return true;
    }
    public static bool Contains(IReadOnlyList<GeoPoint> polygon,GeoPoint point)
    {
        if(polygon.Count<3||!double.IsFinite(point.Lng)||!double.IsFinite(point.Lat))return false;
        bool inside=false;
        for(int i=0,j=polygon.Count-1;i<polygon.Count;j=i++)
        {
            var a=polygon[j];var b=polygon[i];
            double cross=(point.Lat-a.Lat)*(b.Lng-a.Lng)-(point.Lng-a.Lng)*(b.Lat-a.Lat);
            if(Math.Abs(cross)<1e-10&&point.Lng>=Math.Min(a.Lng,b.Lng)-1e-10&&point.Lng<=Math.Max(a.Lng,b.Lng)+1e-10&&point.Lat>=Math.Min(a.Lat,b.Lat)-1e-10&&point.Lat<=Math.Max(a.Lat,b.Lat)+1e-10)return true;
            if((a.Lat>point.Lat)!=(b.Lat>point.Lat)&&point.Lng<(b.Lng-a.Lng)*(point.Lat-a.Lat)/(b.Lat-a.Lat)+a.Lng)inside=!inside;
        }
        return inside;
    }
}
public sealed class PlanningRequest
{
    public DateOnly Date {get;set;}
    public Place Origin {get;set;}=new();
    public DateTime Earliest {get;set;}
    public List<string> Modes {get;set;}=["WALK","BUS"];
    public bool ReplanFlexible {get;set;}
    public HashSet<string> EditableTables {get;set;}=[];
    public string BikeResource {get;set;}="SHARED_BIKE";
    public int ParkingMinutes {get;set;}=5;
    public List<string> Warnings {get;set;}=[];
}
public sealed record PlannedStop(Occurrence Original,DateTime Start,DateTime End,RouteOption Route,DateTime Depart,DateTime Arrive,int BufferSec);
public sealed class ItineraryCandidate
{
    public string Title {get;set;}="";
    public List<PlannedStop> Stops {get;set;}=[];
    public int TravelSeconds=>Stops.Sum(s=>s.Route.DurationSec+s.BufferSec);
    public int? KnownCostFen=>Stops.Any(s=>s.Route.KnownCostFen.HasValue)?Stops.Sum(s=>s.Route.KnownCostFen??0):null;
    public bool CostComplete=>Stops.All(s=>s.Route.CostComplete);
    public int WalkingM=>Stops.Sum(s=>s.Route.WalkingM);
    public string Summary=>$"交通约 {Math.Ceiling(TravelSeconds/60.0)} 分钟 · "+(KnownCostFen is {} c?$"已知费用 ¥{c/100.0:0.##}":"费用暂无数据")+(CostComplete?"":"，另有未知项")+$" · 步行 {WalkingM} 米";
}
public sealed class ItineraryPlanner(IMapProvider provider)
{
    sealed record Node(List<PlannedStop> Stops,List<Occurrence> Left,Place At,DateTime Time,Place Car,Place Bike);
    public async Task<List<ItineraryCandidate>> GenerateAsync(PlannerState state,PlanningRequest request,CancellationToken token)
    {
        if(request.Modes.Count==0||request.Modes.Any(x=>!new[]{"WALK","BIKE","BUS","METRO","CAR","TAXI"}.Contains(x)))throw new ArgumentException("请选择市内交通方式。");
        if(request.Origin.Status!="VERIFIED"||request.Origin.Lng is null||request.Origin.Lat is null)throw new ArgumentException("请先从地点搜索中确认出发点。");
        var day=request.Date.ToDateTime(TimeOnly.MinValue);
        var all=CalendarEngine.Expand(state,day,day.AddDays(1)).OrderBy(x=>x.Start).ToList();
        if(all.Count==0)throw new ArgumentException("当天没有生效事项。");
        if(all.Count>12)throw new ArgumentException("一次自动规划最多12项，请拆分事项或缩小生效范围。");
        if(all.Any(x=>x.Start<day||x.End>day.AddDays(1)))throw new ArgumentException("当天含跨日事项，请先手动安排跨日部分。");
        if(request.Earliest<day||request.Earliest>=day.AddDays(1))throw new ArgumentException("最早出发时间须在当天。");
        var places=state.Places.ToDictionary(x=>x.Id);
        foreach(var x in all)if(x.PlaceId is null||!places.TryGetValue(x.PlaceId,out var p)||p.Status!="VERIFIED"||p.Lng is null||p.Lat is null)throw new ArgumentException($"请先确认“{x.Title}”的市内地点。");
        bool Flexible(Occurrence x)=>request.ReplanFlexible&&request.EditableTables.Contains(x.Activity.TableId)&&x.Activity.ConstraintType=="FLEXIBLE"&&x.ExceptionVersion==0;
        (DateTime,DateTime) Window(Occurrence x)
        {
            if(x.OriginDate is {} date)
            {var r=state.Rules.Single(q=>q.ActivityId==x.Activity.Id);var start=date.ToDateTime(TimeOnly.Parse(r.WindowFrom??r.StartTime));return(start,start.AddMinutes(r.WindowSpanMin??x.Activity.DurationMin));}
            return(x.Activity.WindowStart??x.Start,x.Activity.WindowEnd??x.End);
        }
        var cache=new Dictionary<string,List<RouteOption>>();int queries=0;
        async Task<List<RouteOption>> Options(Place a,Place b,DateTime at)
        {
            if(Same(a,b))return[new(){Label="同一地点",CostComplete=true,KnownCostFen=0,Segments=[]}];
            var options=new List<RouteOption>();var modes=TransportPolicy.QueryModes(request.Modes);
            foreach(var mode in modes)
            {
                token.ThrowIfCancellationRequested();var key=$"{a.Id}|{b.Id}|{mode}|{at:yyyyMMddHHmm}";
                if(!cache.TryGetValue(key,out var routes))
                {
                    if(++queries>100)continue;
                    try{routes=await provider.RoutesAsync(a,b,mode,at,token);}
                    catch(InvalidOperationException ex){routes=[];var warning=$"{mode} 查询不可用：{ex.Message}";if(!request.Warnings.Contains(warning))request.Warnings.Add(warning);}
                    cache[key]=routes;
                }
                foreach(var route in routes)
                {
                    if(!TransportPolicy.Allows(route,request.Modes))continue;
                    var copy=JsonKit.Clone(route);
                    foreach(var segment in copy.Segments.Where(s=>s.Mode=="BIKE"))segment.ResourceType=request.BikeResource;
                    if(copy.Uses("BIKE")&&request.BikeResource=="OWN_BIKE"){copy.CostComplete=true;copy.KnownCostFen=0;copy.CostNote="自有自行车，不计购置维护费用";}
                    options.Add(copy);
                }
            }
            return options;
        }
        double Score(Node n,int policy)=>policy switch{1=>n.Stops.Sum(s=>(s.Route.KnownCostFen??0)+(s.Route.CostComplete?0:50000))+n.Stops.Sum(s=>s.Route.DurationSec)/60.0,2=>n.Stops.Sum(s=>s.Route.WalkingM)+n.Stops.Sum(s=>s.Route.DurationSec)/100.0,_=>n.Stops.Sum(s=>s.Route.DurationSec+s.BufferSec)};
        var completed=new List<ItineraryCandidate>();string[] labels=["交通用时较少","已知费用优先（未知项另计）","步行较少"];
        for(int policy=0;policy<3;policy++)
        {
            List<Node> beam=[new([],all,request.Origin,request.Earliest,request.Origin,request.Origin)];
            for(int depth=0;depth<all.Count&&beam.Count>0;depth++)
            {
                var next=new List<Node>();
                foreach(var node in beam)
                {
                    var fixedNext=node.Left.Where(x=>!Flexible(x)).OrderBy(x=>x.Start).FirstOrDefault();
                    var choices=node.Left.Where(Flexible).OrderBy(x=>Window(x).Item2).Take(3).ToList();if(fixedNext is not null)choices.Add(fixedNext);
                    foreach(var item in choices)
                    {
                        var destination=places[item.PlaceId!];
                        foreach(var route in await Options(node.At,destination,node.Time))
                        {
                            if(route.Uses("CAR")&&!Same(node.At,node.Car)||route.Uses("BIKE")&&request.BikeResource=="OWN_BIKE"&&!Same(node.At,node.Bike))continue;
                            int buffer=route.Uses("CAR")?Math.Clamp(request.ParkingMinutes,0,60)*60:0;
                            var arrive=node.Time.AddSeconds(route.DurationSec+buffer);DateTime start,end;
                            if(Flexible(item)){var (low,high)=Window(item);start=arrive>low?arrive:low;end=start+(item.End-item.Start);if(end>high||end>day.AddDays(1)||fixedNext is not null&&end>fixedNext.Start)continue;}
                            else {start=item.Start;end=item.End;if(arrive>start)continue;}
                            var stop=new PlannedStop(item,start,end,route,node.Time,arrive,buffer);
                            next.Add(new([..node.Stops,stop],node.Left.Where(x=>x.Key!=item.Key).ToList(),destination,end,route.Uses("CAR")?destination:node.Car,route.Uses("BIKE")&&request.BikeResource=="OWN_BIKE"?destination:node.Bike));
                        }
                    }
                }
                beam=next.OrderBy(n=>Score(n,policy)).Take(6).ToList();
            }
            if(beam.FirstOrDefault(x=>x.Left.Count==0) is {} best)completed.Add(new(){Title=labels[policy],Stops=best.Stops});
        }
        return completed.DistinctBy(x=>string.Join("|",x.Stops.Select(s=>$"{s.Original.Key}:{s.Start:O}:{s.Route.Label}"))).ToList();
    }
    static bool Same(Place a,Place b)=>a.Id==b.Id||a.Lng.HasValue&&b.Lng.HasValue&&a.Lat.HasValue&&b.Lat.HasValue&&Math.Abs(a.Lng.Value-b.Lng.Value)<0.00001&&Math.Abs(a.Lat.Value-b.Lat.Value)<0.00001;
}
