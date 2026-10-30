using Planner.Core;
using Planner.Data;
using Activity=Planner.Core.Activity;
static class TrafficTests
{
    static void Check(bool value){if(!value)throw new Exception("traffic regression");}
    public static void Register(Action<string,Action> test)
    {
        PlannerState State(){var s=new PlannerState();s.Tables.Add(new(){Id="t",Name="工作"});s.Places.Add(new(){Id="p",Name="南大",Lng=118.78,Lat=32.05,Status="VERIFIED"});return s;}
        Occurrence Item(string id,DateTime start,DateTime end)=>new(new Activity{Id=id,TableId="t",PlaceId="p",Title=id,StartAt=start,EndAt=end},null,id,"p",start,end);
        var from=Item("a",new(2026,10,30,22,0,0),new(2026,10,30,23,50,0));var to=Item("b",new(2026,10,31,0,20,0),new(2026,10,31,1,0,0));
        test("short gap never claims traffic was checked without a result",()=>{var pair=new TrafficPair(from,to);Check(pair.GapMinutes==30);Check(AdjacentTraffic.Label(pair,null).Contains("交通未核验"));Check(AdjacentTraffic.Label(pair,new(){Minutes=20}).Contains("余量 10 分钟"));Check(AdjacentTraffic.Label(pair,new(){Minutes=45}).Contains("缺 15 分钟"));});
        test("adjacent checks include previous evening and following morning",()=>{var s=State();s.Activities.Add(from.Activity);s.Activities.Add(to.Activity);var pairs=AdjacentTraffic.Around(s,to);Check(pairs.Count==1&&pairs[0].From.Key==from.Key&&pairs[0].GapMinutes==30);});
        test("traffic fingerprint expires on times and coordinates but not notes",()=>{var s=State();var p=new TrafficPair(from,to);string key=p.Fingerprint(s);s.Activities.Add(from.Activity);from.Activity.Note="备注变化";Check(key==p.Fingerprint(s));Check(key!=new TrafficPair(from,to with{Start=to.Start.AddMinutes(1)}).Fingerprint(s));s.Places[0].Lng+=.001;Check(key!=p.Fingerprint(s));});
        test("traffic snapshots persist without reserving itinerary time",()=>{var dir=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString());try{Directory.CreateDirectory(dir);var db=new SqliteStore(Path.Combine(dir,"planner.db"));var state=db.Load();var store=new TrafficSnapshotStore(dir);store.Save([new(){Fingerprint="good",Minutes=20,Mode="公交"},new(){Fingerprint="failed",Source="网络错误"},new(){Fingerprint="expired",Minutes=5,CheckedAt=Clock.Now.AddDays(-2)}]);var found=store.Load();Check(found["good"].Minutes==20&&found["failed"].Minutes is null&&!found.ContainsKey("expired"));Check(db.Load().Revision==state.Revision&&db.Load().Legs.Count==0);state.Revision++;db.Save(state,state.Revision-1);Check(store.Load().ContainsKey("good"));}finally{Directory.Delete(dir,true);}});
        test("note-only edits retain existing traffic checks",()=>{var s=State();Check(AdjacentTraffic.SameTimingAndPlace(from,from.Start,from.End,s.Places[0],s));Check(!AdjacentTraffic.SameTimingAndPlace(from,from.Start.AddMinutes(1),from.End,s.Places[0],s));});
    }
}
