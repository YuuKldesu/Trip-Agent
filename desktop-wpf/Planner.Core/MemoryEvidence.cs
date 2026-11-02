using System.Globalization;
using System.Text.RegularExpressions;
namespace Planner.Core;
static class MemoryEvidence
{
 public static bool Matches(MemoryUpdate update,ConversationMessage source)
 {
  string evidence=update.Evidence.Trim(),value=update.Value.Trim(),key=update.Key.ToLowerInvariant();
  if(value==evidence)return true;
  if(key.Contains("duration")||key.Contains("耗时"))
  {
   var match=Regex.Match(evidence,@"^(\d+(?:\.\d+)?)\s*(分钟|小时|个小时)$");
   if(match.Success&&decimal.TryParse(match.Groups[1].Value,NumberStyles.Number,CultureInfo.InvariantCulture,out var n)){var minutes=n*(match.Groups[2].Value=="分钟"?1:60);return value==minutes.ToString("0.################",CultureInfo.InvariantCulture);}
   if(evidence is "半小时" or "半个小时")return value=="30";
  }
  if(key.Contains("date")||key.EndsWith("day")||key.Contains("日期"))
  {
   var day=DateOnly.FromDateTime(source.CreatedAt);
   DateOnly? resolved=evidence switch{"今天"=>day,"明天"=>day.AddDays(1),"后天"=>day.AddDays(2),_=>null};
   foreach(var format in new[]{"yyyy-MM-dd","yyyy/M/d","yyyy年M月d日"})if(DateOnly.TryParseExact(evidence,format,CultureInfo.InvariantCulture,DateTimeStyles.None,out var parsed))resolved=parsed;
   return resolved is {} date&&value==date.ToString("yyyy-MM-dd");
  }
  if((key.EndsWith("start")||key.EndsWith("end")||key.Contains("earliest"))&&TimeOnly.TryParseExact(evidence,"HH:mm",CultureInfo.InvariantCulture,DateTimeStyles.None,out var time))return value==(time.Hour*60+time.Minute).ToString(CultureInfo.InvariantCulture);
  return false;
 }
}
