using System.IO;
using System.Windows;
using System.Windows.Controls;
using Planner.Core;
using Planner.Desktop;

static class RolloverLayoutChecks
{
    public static async Task Run(string directory,Action<bool,string> check)
    {
        var vm=new PlannerViewModel(Path.Combine(directory,"rollover-layout"),false);
        vm.UpdateAppearance(p=>{p.LayoutMode="FULL";p.SidebarExpanded=false;p.FontSize=22;});
        var main=new MainWindow(vm);main.Show();
        try
        {
            await Task.Delay(50);main.UpdateLayout();
            var profile=Walk(main).OfType<Button>().Single(b=>Equals(b.Tag,"NavProfile"));
            var avatar=Walk(profile).OfType<Border>().Single(b=>b.Width==24);
            var point=avatar.TranslatePoint(new Point(0,0),profile);
            check(point.X>=0&&point.X+avatar.ActualWidth<=profile.ActualWidth,"折叠侧栏22号字体头像完整显示");
            var board=Walk(main).OfType<ScheduleBoard>().Single();var previous=new DateOnly(2026,10,31);var next=previous.AddDays(1);
            board.CheckDateChange(previous);board.NavigateTo(previous);long revision=vm.State.Revision;
            board.CheckDateChange(next);await Task.Delay(30);
            check(vm.SelectedDate==next&&board.DisplayMonth==new DateOnly(2026,11,1),"跨午夜和跨月时跟随今天");
            check(Walk(board).OfType<Button>().Count(b=>Equals(b.Content,"1 · 今天"))==1&&!Walk(board).OfType<Button>().Any(b=>Equals(b.Content,"31 · 今天")),"日期缓存失效且今天标记唯一");
            board.NavigateTo(new(2025,4,15));board.CheckDateChange(next.AddDays(1));
            check(board.DisplayMonth==new DateOnly(2025,4,1)&&vm.SelectedDate==new DateOnly(2025,4,15),"午夜刷新保留正在查看的历史月份");
            board.SetView("LIST");board.CheckDateChange(next.AddDays(2));
            check(Walk(board).OfType<Button>().Any(b=>Equals(b.Content,"＋ 添加今天的行程")),"列表日期刷新保留今天空组");
            check(vm.State.Revision==revision,"日期刷新不会写入或修改日程");
            var editor=new EditorWindow(vm){Owner=main};editor.Show();
            try
            {
                Walk(editor).OfType<Expander>().Single().IsExpanded=true;editor.Width=440;editor.UpdateLayout();
                var row=Walk(editor).OfType<WrapPanel>().Single(p=>Equals(p.Tag,"ReminderRow"));var lead=Walk(row).OfType<TextBox>().Single();
                var label=Walk(row).OfType<TextBlock>().Single(t=>t.Text=="提前");
                double labelMid=label.TranslatePoint(new Point(0,label.ActualHeight/2),row).Y,leadMid=lead.TranslatePoint(new Point(0,lead.ActualHeight/2),row).Y;
                check(Math.Abs(labelMid-leadMid)<2,"窄窗口大字号提醒标签与输入框垂直居中");
            }
            finally{editor.Close();}
        }
        finally{main.Close();}
    }
    static IEnumerable<DependencyObject> Walk(DependencyObject root){yield return root;foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())foreach(var item in Walk(child))yield return item;}
}
