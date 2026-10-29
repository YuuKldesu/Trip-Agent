using Planner.Core;
using Planner.Data;
static class SoftUiTests
{
 static void Equal<T>(T a,T b){if(!Equals(a,b))throw new Exception($"expected {a}, got {b}");}
 static string Temp()=>Path.Combine(Path.GetTempPath(),"soft-ui-"+Guid.NewGuid());
 public static void Register(Action<string,Action> test)
 {
  test("legacy preferences retain completed keys and receive safe soft defaults",()=>{var d=Temp();Directory.CreateDirectory(d);try{File.WriteAllText(Path.Combine(d,"board.json"),"{\"view\":\"LIST\",\"completed\":[\"a:once\"]}");var p=new BoardPreferenceStore(d).Load();Equal("LIST",p.View);Equal(true,p.Completed.Contains("a:once"));Equal(14d,p.FontSize);Equal("AUTO",p.LayoutMode);Equal(0,p.Important.Count);}finally{Directory.Delete(d,true);}});
  test("independent preference updates read fresh file rather than overwrite old completion",()=>{var d=Temp();try{var a=new BoardPreferenceStore(d);var b=new BoardPreferenceStore(d);a.Update(p=>p.Completed.Add("a:once"));b.Update(p=>{p.FontSize=18;p.Important.Add("b:once");p.LayoutMode="COMPACT";});var read=a.Load();Equal(true,read.Completed.Contains("a:once"));Equal(false,read.Completed.Contains("b:once"));Equal(true,read.Important.Contains("b:once"));Equal(18d,read.FontSize);Equal("COMPACT",read.LayoutMode);}finally{Directory.Delete(d,true);}});
  test("invalid soft values normalize without discarding completed schedule keys",()=>{var d=Temp();Directory.CreateDirectory(d);try{File.WriteAllText(Path.Combine(d,"board.json"),"{\"font_size\":99,\"layout_mode\":\"x\",\"completed\":[\"a:once\"]}");var p=new BoardPreferenceStore(d).Load();Equal(14d,p.FontSize);Equal("AUTO",p.LayoutMode);Equal(true,p.Completed.Contains("a:once"));}finally{Directory.Delete(d,true);}});
  test("right swipe selects collection independently of completion",()=>{var swipe=new SwipeTracker();for(int i=0;i<2;i++){swipe.Begin(0,0);swipe.Move(90,2);Equal("Collect",swipe.End(200).ToString());}});
  test("short vertical and cancelled gestures cannot toggle or open editor",()=>{var s=new SwipeTracker();foreach(var delta in new[]{(10d,0d),(5d,100d)}){s.Begin(0,0);s.Move(delta.Item1,delta.Item2);Equal(SwipeOutcome.None,s.End(200));}s.Begin(0,0);s.Move(100,0);s.Cancel();Equal(SwipeOutcome.None,s.End(200));});
  test("left swipe selects completion in both directions of existing state",()=>{var s=new SwipeTracker();for(int i=0;i<2;i++){s.Begin(100,0);s.Move(0,0);Equal("Complete",s.End(200).ToString());}});
  test("stationary pointer is a tap and threshold adapts to narrow cards",()=>{var s=new SwipeTracker();s.Begin(1,1);Equal(SwipeOutcome.Tap,s.End(200));s.Begin(0,0);s.Move(31,0);Equal("Collect",s.End(90).ToString());});
  test("local profile and panel choices survive restart with old completion keys",()=>{var d=Temp();try{var store=new BoardPreferenceStore(d);store.Update(p=>{p.Completed.Add("a:once");(p.GetType().GetProperty("Nickname")??throw new Exception("local profile missing")).SetValue(p,"小白的行程");p.GetType().GetProperty("SidebarExpanded")!.SetValue(p,true);});var restored=store.Load();Equal("小白的行程",(string)restored.GetType().GetProperty("Nickname")!.GetValue(restored)!);Equal(true,(bool)restored.GetType().GetProperty("SidebarExpanded")!.GetValue(restored)!);Equal(true,restored.Completed.Contains("a:once"));}finally{if(Directory.Exists(d))Directory.Delete(d,true);}});
  test("responsive policy honors manual overrides",()=>{Equal(false,PresentationPolicy.Compact("FULL",400,800,true,14));Equal(true,PresentationPolicy.Compact("COMPACT",1480,920,false,14));});
  test("responsive auto uses hysteresis and large font requirements",()=>{Equal(true,PresentationPolicy.Compact("AUTO",900,900,false,14));Equal(false,PresentationPolicy.Compact("AUTO",1140,800,false,14));Equal(true,PresentationPolicy.Compact("AUTO",1140,800,true,14));Equal(false,PresentationPolicy.Compact("AUTO",1300,800,true,14));Equal(true,PresentationPolicy.Compact("AUTO",1480,920,false,22));});
 }
}
