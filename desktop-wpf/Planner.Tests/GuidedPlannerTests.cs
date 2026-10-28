using Planner.Core;
using System.Reflection;
static class GuidedPlannerTests
{
 static void Equal<T>(T expected,T actual){if(!Equals(expected,actual))throw new Exception($"expected {expected}, got {actual}");}
 static Type Selection()=>typeof(PlannerState).Assembly.GetType("Planner.Core.DateTimeSelection")??throw new Exception("datetime carry behavior missing");
 static DateTime Roll(DateTime value,bool hours,int steps)=>(DateTime)Selection().GetMethod("Roll")!.Invoke(null,[value,hours,steps])!;
 public static void Register(Action<string,Action> test)
 {
  test("minute wheel carry moves forward over leap midnight",()=>Equal(new DateTime(2028,2,29,0,0,0),Roll(new(2028,2,28,23,59,0),false,1)));
  test("minute wheel borrows previous year rather than wrapping clock alone",()=>Equal(new DateTime(2025,12,31,23,59,0),Roll(new(2026,1,1,0,0,0),false,-1)));
  test("hour wheel carries date and retains minute precision",()=>Equal(new DateTime(2026,11,1,0,17,0),Roll(new(2026,10,31,23,17,0),true,1)));
  test("hour wheel borrows leap date",()=>Equal(new DateTime(2028,2,29,23,42,0),Roll(new(2028,3,1,0,42,0),true,-1)));
  test("moving start keeps true elapsed duration across midnight",()=>{var result=((DateTime Start,DateTime End))Selection().GetMethod("MoveStart")!.Invoke(null,[new DateTime(2026,10,28,23,30,0),new DateTime(2026,10,29,1,0,0),new DateTime(2026,10,29,0,15,0)])!;Equal(new DateTime(2026,10,29,1,45,0),result.End);});
  test("cross-day fragments expose daily and whole durations without midnight phantom",()=>{var s=new PlannerState();s.Tables.Add(new(){Id="t"});s.Activities.Add(new(){TableId="t",Title="夜间活动",StartAt=new(2026,10,28,23,30,0),EndAt=new(2026,10,29,1,0,0),DurationMin=90});var days=BoardProjection.Range(s,new(2026,10,28),new(2026,10,30));var first=days[0].Entries.Single();Equal("23:30—24:00",first.TimeText);Equal(30,(int)(typeof(BoardEntry).GetProperty("DayMinutes")?.GetValue(first)??throw new Exception("day duration missing")));Equal(90,(int)typeof(BoardEntry).GetProperty("TotalMinutes")!.GetValue(first)!);Equal(60,(int)typeof(BoardEntry).GetProperty("DayMinutes")!.GetValue(days[1].Entries.Single())!);s.Activities[0].EndAt=new(2026,10,29,0,0,0);s.Activities[0].DurationMin=30;Equal(0,BoardProjection.Range(s,new(2026,10,29),new(2026,10,30)).Single().Entries.Count);});
 }
}
