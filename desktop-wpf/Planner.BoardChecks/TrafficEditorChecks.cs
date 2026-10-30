using System.IO;
using System.Windows;
using System.Windows.Controls;
using Planner.Core;
using Planner.Data;
using Planner.Desktop;
static class TrafficEditorChecks
{
    public static async Task Run(string directory,Action<bool,string> check)
    {
        var vm=new PlannerViewModel(Path.Combine(directory,"traffic-editor"),false);var day=Clock.Today.ToDateTime(TimeOnly.MinValue);string table=vm.SelectedTableId;
        vm.Mutate(s=>{s.Places.Add(new(){Id="a",Name="南京图书馆"});s.Places.Add(new(){Id="b",Name="南京博物院"});s.Activities.Add(new(){Id="a",TableId=table,Title="阅读",PlaceId="a",StartAt=day.AddHours(9),EndAt=day.AddHours(10),DurationMin=60});s.Activities.Add(new(){Id="b",TableId=table,Title="参观",PlaceId="b",StartAt=day.AddHours(10.5),EndAt=day.AddHours(11.5),DurationMin=60});});
        var occurrence=CalendarEngine.ExpandForDisplay(vm.State,day,day.AddDays(1)).Single(o=>o.Activity.Id=="b");var editor=new EditorWindow(vm,occurrence);editor.Show();
        try
        {
            var start=Walk(editor).OfType<TimeEntry>().Single(e=>Equals(e.Tag,"StartTime"));start.Text="10:20";
            check(!Walk(editor).OfType<CheckBox>().Any(c=>Equals(c.Content,"保存前核验相邻交通"))&&!Walk(start).OfType<Button>().Any(),"编辑器移除常驻交通核验和独立时钟按钮");
            long revision=vm.State.Revision;var save=Walk(editor).OfType<Button>().Single(b=>Equals(b.Content,"保存"));save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(50);
            var warning=Application.Current.Windows.OfType<Window>().Single(w=>w.Title=="检查前后行程");check(Walk(warning).OfType<TextBlock>().Any(t=>t.Text.Contains("交通未核验")),"缺少Key时明确警告而非假装核验成功");
            Walk(warning).OfType<Button>().Single(b=>Equals(b.Content,"返回修改")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(50);check(vm.State.Revision==revision&&editor.IsVisible&&save.IsEnabled,"取消检查保留编辑内容且不写入日程");
            save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(50);warning=Application.Current.Windows.OfType<Window>().Single(w=>w.Title=="检查前后行程");Walk(warning).OfType<Button>().Single(b=>Equals(b.Content,"仍然保存")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(80);
            check(vm.State.Revision==revision+1&&!editor.IsVisible&&new TrafficSnapshotStore(vm.DataDirectory).Load().Values.Single().Minutes is null,"用户可在交通未知时明确确认保存，不生成虚假路线");
        }
        finally{if(editor.IsVisible)editor.Close();}
    }
    static IEnumerable<DependencyObject> Walk(DependencyObject root){yield return root;foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())foreach(var item in Walk(child))yield return item;}
}
