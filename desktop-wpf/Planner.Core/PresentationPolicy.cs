namespace Planner.Core;
public static class PresentationPolicy
{
    public static bool Compact(string mode,double width,double height,bool current,double fontSize)
    {
        if(mode=="FULL")return false;if(mode=="COMPACT")return true;
        if(width<=0||height<=0)return current;double scale=Math.Max(1,fontSize/14),ratio=width/height;
        return current?width<=1200*scale||ratio<=1.1:width<1080*scale||ratio<.95;
    }
}
public enum SwipeOutcome { None,Tap,Complete,Collect }
public sealed class SwipeTracker
{
    double x,y;bool active,moved,vertical,horizontal;
    public double DeltaX {get;private set;}
    public bool Horizontal=>horizontal&&!vertical;
    public void Begin(double startX,double startY){x=startX;y=startY;active=true;moved=vertical=horizontal=false;DeltaX=0;}
    public void Move(double currentX,double currentY)
    {
        if(!active)return;DeltaX=currentX-x;double dy=currentY-y;
        if(Math.Abs(DeltaX)>4||Math.Abs(dy)>4)moved=true;
        if(!horizontal&&Math.Abs(dy)>8&&Math.Abs(dy)>Math.Abs(DeltaX))vertical=true;
        if(!vertical&&Math.Abs(DeltaX)>8&&Math.Abs(DeltaX)>Math.Abs(dy)*1.25)horizontal=true;
    }
    public static double Threshold(double width)=>Math.Clamp(width*.3,30,80);
    public SwipeOutcome End(double width)
    {
        var result=!active?SwipeOutcome.None:horizontal&&!vertical&&Math.Abs(DeltaX)>=Threshold(width)?DeltaX<0?SwipeOutcome.Complete:SwipeOutcome.Collect:!moved?SwipeOutcome.Tap:SwipeOutcome.None;
        Cancel();return result;
    }
    public void Cancel(){active=moved=vertical=horizontal=false;DeltaX=0;}
}
