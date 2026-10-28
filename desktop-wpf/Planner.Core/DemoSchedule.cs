namespace Planner.Core;
public static class BoardLayout
{
    public static double RowMinimum(double viewport,int rows)=>Math.Max(88,viewport/Math.Max(1,rows));
}
public static class DemoSchedule
{
    public static PlannerState AddToEmpty(PlannerState state,DateOnly today)
    {
        if(state.Activities.Count>0||state.Tables.Count==0)return state;
        var commands=new List<EditCommand>();int[] counts=[1,3,0,5,2,1,4];
        string[] titles=["晨间阅读","资料整理","午后散步","课程讨论","晚间复盘"];
        for(int d=0;d<counts.Length;d++)for(int n=0;n<counts[d];n++)
        {
            var day=today.AddDays(d-2);var begin=day.ToDateTime(new TimeOnly(8+n*2,0));
            commands.Add(new(){Activity=new(){TableId=state.Tables[(d+n)%state.Tables.Count].Id,Title=titles[n]+"（示例）",StartAt=begin,EndAt=begin.AddMinutes(n%2==0?60:90),DurationMin=n%2==0?60:90,Note=d==3&&n==1?"示例长备注：带上笔记本与资料，整理最近的讨论内容，核对尚未完成的事项。日期格会随活动内容增长，其他日期的安排数量刻意不同，便于检查界面布局。":"可直接编辑或删除这项示例行程。"},Place=new(){Name=n%2==0?"南京图书馆":"新街口",InputName=n%2==0?"南京图书馆":"新街口",Lng=n%2==0?118.797:118.7857,Lat=n%2==0?32.042:32.0417,Status="UNVERIFIED",Source="示例坐标，非实地核验"}});
        }
        var night=today.AddDays(4).ToDateTime(new(23,0));commands.Add(new(){Activity=new(){TableId=state.Tables.Last().Id,Title="跨日整理（示例）",StartAt=night,EndAt=night.AddHours(2),DurationMin=120,Note="用于检查跨日续段。"}});
        return ChangeService.Apply(state,commands,state.Revision);
    }
}
