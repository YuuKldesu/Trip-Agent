namespace Planner.Core;
public static class EditWindows
{
    public static void Apply(Activity a,DateOnly date,string startText,string endText,bool singleOverride)
    {
        if(singleOverride)return;
        if(a.ConstraintType!="FLEXIBLE"){a.WindowStart=null;a.WindowEnd=null;return;}
        if(!TimeOnly.TryParseExact(startText.Trim(),"HH:mm",out var start)||!TimeOnly.TryParseExact(endText.Trim(),"HH:mm",out var end))throw new ArgumentException("灵活时间范围请使用HH:mm格式。");
        a.WindowStart=date.ToDateTime(start);a.WindowEnd=date.ToDateTime(end);if(a.WindowEnd<=a.WindowStart)a.WindowEnd=a.WindowEnd.Value.AddDays(1);
        if(a.StartAt<a.WindowStart||a.EndAt>a.WindowEnd)throw new ArgumentException("当前起止时间应落在灵活范围内。");
    }
}
