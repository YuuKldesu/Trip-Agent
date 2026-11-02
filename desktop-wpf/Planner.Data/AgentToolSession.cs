using System.Text.Json;
using System.Globalization;
using Planner.Core;
namespace Planner.Data;

public sealed class AgentToolRequest
{
 public string Name {get;set;}="";
 public JsonElement Arguments {get;set;}
}
// Read-only capability boundary: no database handle, no model-supplied coordinates or URLs.
public sealed class AgentToolSession(AgentMemory memory,PlannerState state,RepositoryMap map,IProgress<string>? progress=null)
{
 int count;readonly Dictionary<string,object> cache=[];
 public async Task<object> ExecuteAsync(AgentToolRequest request,CancellationToken token)
 {
  token.ThrowIfCancellationRequested();if(++count>6)return new{error="本轮工具查询已达6次上限，请先询问用户后继续。"};
  try
  {
   if(request.Arguments.ValueKind!=JsonValueKind.Object)throw new ArgumentException("工具参数必须是对象。");
   string Text(string key){if(!request.Arguments.TryGetProperty(key,out var value)||value.ValueKind!=JsonValueKind.String||string.IsNullOrWhiteSpace(value.GetString())||value.GetString()!.Length>200)throw new ArgumentException("缺少有效工具参数："+key);return value.GetString()!;}
   var cacheKey=request.Name+request.Arguments.GetRawText();if(cache.TryGetValue(cacheKey,out var saved))return saved;
   object result;
   switch(request.Name)
   {
    case "search_places":
    {
     var query=Text("query");progress?.Report("正在查找地点："+query);
     var places=(await map.SearchAsync(query,token)).Take(8).Select(JsonKit.Clone).ToList();token.ThrowIfCancellationRequested();
     memory.Candidates.RemoveAll(c=>c.Query==query);if(memory.Candidates.Count>=16)memory.Candidates.RemoveAt(0);memory.Candidates.Add(new(){Query=query,Places=places});
     result=new{query,query_at=Clock.Now,requires_user_selection=true,candidates=places.Select(p=>new{place_ref=p.Id,p.Name,p.Address,p.Source,p.Status}),note="候选不是已确认地点，须由用户点击选择。"};break;
    }
    case "query_route":
    {
     var fromRef=Text("from_ref");var toRef=Text("to_ref");var mode=Text("mode");
     if(!memory.SelectedPlaces.TryGetValue(fromRef,out var from)||!memory.SelectedPlaces.TryGetValue(toRef,out var to))throw new ArgumentException("路线端点必须来自用户已选择的地点引用，先搜索并请用户确认。");
     if(mode is not("公交" or "步行" or "骑行" or "驾车" or "打车"))throw new ArgumentException("仅支持市内公交、步行、骑行、驾车或打车。");
     if(!DateTimeOffset.TryParse(Text("depart_at"),CultureInfo.InvariantCulture,DateTimeStyles.None,out var date))throw new ArgumentException("请提供带日期的出发时间。");var depart=date.ToOffset(TimeSpan.FromHours(8)).DateTime;
     progress?.Report("正在计算路线："+from.Name+" → "+to.Name);
     var estimate=await map.TravelAsync(from,to,mode=="打车"?"驾车":mode,depart,token);token.ThrowIfCancellationRequested();
     var route=new AgentRoute{FromRef=fromRef,ToRef=toRef,Mode=mode,DepartAt=depart,Minutes=estimate.Minutes,Source=estimate.Source,KnownCostFen=estimate.Option?.KnownCostFen,CostComplete=estimate.Option?.CostComplete??false,Option=estimate.Option is null?null:JsonKit.Clone(estimate.Option)};
     memory.Routes.RemoveAll(r=>r.FromRef==fromRef&&r.ToRef==toRef&&r.Mode==mode&&r.DepartAt==depart);if(memory.Routes.Count>=32)memory.Routes.RemoveAt(0);memory.Routes.Add(route);
     result=new{from_ref=fromRef,to_ref=toRef,from=from.Name,to=to.Name,mode,depart_at=depart,arrival_at=depart.AddMinutes(route.Minutes),minutes=route.Minutes,source=route.Source,query_at=route.QueryAt,known_cost_fen=mode=="打车"?null:route.KnownCostFen,cost_complete=mode!="打车"&&route.CostComplete,crowding="未知",note=mode=="打车"?"使用驾车路线，不能将道路费用视为打车报价。":"路线为查询时快照，未知费用不补猜。"};break;
    }
    case "read_schedule":
    {
     if(!DateOnly.TryParseExact(Text("from_day"),"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var first)||!DateOnly.TryParseExact(Text("to_day"),"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var last)||last<first||last.DayNumber-first.DayNumber>=7||last==DateOnly.MaxValue)throw new ArgumentException("日程读取需提供1—7天的有效日期范围。");
     progress?.Report("正在核对已有行程："+first.ToString("MM-dd")+"—"+last.ToString("MM-dd"));
     var occurrences=CalendarEngine.Expand(state,first.ToDateTime(TimeOnly.MinValue),last.AddDays(1).ToDateTime(TimeOnly.MinValue));
     var snapshot=new ScheduleRead{FromDay=first,ToDay=last,Revision=state.Revision,Truncated=occurrences.Count>80,Events=occurrences.Take(80).Select(o=>new ScheduleReadItem{Title=o.Title,Start=o.Start,End=o.End,Table=state.Tables.FirstOrDefault(t=>t.Id==o.Activity.TableId)?.Name,Place=state.Places.FirstOrDefault(p=>p.Id==o.PlaceId)?.Name}).ToList()};
     memory.ScheduleReads.RemoveAll(r=>r.FromDay==first&&r.ToDay==last);if(memory.ScheduleReads.Count>=8)memory.ScheduleReads.RemoveAt(0);memory.ScheduleReads.Add(snapshot);
     result=new{schedule=snapshot,conflicts=CalendarEngine.Conflicts(occurrences).Take(30).Select(c=>c.Message)};break;
    }
    default:throw new ArgumentException("未知工具，仅允许search_places、query_route、read_schedule。");
   }
   if(JsonSerializer.Serialize(result,JsonKit.Options).Length>24000)throw new ArgumentException("结果过多，请缩小查询范围。");cache[cacheKey]=result;return result;
  }
  catch(OperationCanceledException){throw;}
  catch(Exception ex)when(ex is ArgumentException or InvalidOperationException or HttpRequestException or JsonException or FormatException)
  {return new{error=ex is HttpRequestException?"工具网络查询失败，请稍后重试。":ex is JsonException or FormatException?"工具返回格式异常，请重新查询。":ex.Message,query_at=Clock.Now};}
 }
}
