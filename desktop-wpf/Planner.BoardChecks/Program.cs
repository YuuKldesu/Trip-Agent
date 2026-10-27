using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Planner.Core;
using Planner.Data;
using Planner.Desktop;
using Activity=Planner.Core.Activity;

static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if(args.Length!=1)return 2;string directory=Path.GetFullPath(args[0]);Directory.CreateDirectory(directory);if(File.Exists(Path.Combine(directory,"planner.db")))throw new InvalidOperationException("需要新的隔离检查目录。");
        var log=new List<string>();var app=new App();app.InitializeComponent();app.ShutdownMode=ShutdownMode.OnExplicitShutdown;var vm=new PlannerViewModel(directory);vm.FocusDate(new(2026,10,27));Seed(vm);
        var main=new MainWindow(vm);app.MainWindow=main;main.Show();int code=1;var frame=new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(async()=>
        {
            try
            {
                await Task.Delay(250);var board=Walk(main).OfType<ScheduleBoard>().Single();Check(board.ViewMode=="MONTH","默认打开整月看板");
                Check(Texts(board).Contains("行程看板")&&Texts(board).Contains("周一")&&Texts(board).Contains("周日"),"月历标题和七天表头可见");
                Check(Texts(board).Any(t=>t.Contains("09:00—18:00")),"长时工作以紧凑卡片显示时间");Capture(main,Path.Combine(directory,"month.png"));
                var denseScroll=Walk(board).OfType<ScrollViewer>().First(s=>s.Content is Grid);Check(denseScroll.ExtentHeight>denseScroll.ViewportHeight,"密集日期自动增高并可滚动");
                var before=JsonSerializer.Serialize(vm.State,JsonKit.Options);FindButton(board,"列表").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(120);Check(board.ViewMode=="LIST","列表按钮切换为分组列表");
                var mark=Walk(board).OfType<CheckBox>().First(c=>c.Tag is string);var key=(string)mark.Tag;mark.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(60);Check(new BoardPreferenceStore(directory).Load().Completed.Contains(key),"完成标记实际持久化");Check(JsonSerializer.Serialize(vm.State,JsonKit.Options)==before,"完成标记不修改活动或排程约束");Capture(main,Path.Combine(directory,"list.png"));
                FindButton(board,"周课表").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(100);Check(board.ViewMode=="WEEK"&&Walk(board).OfType<CalendarView>().Any(),"原周课表仍可进入");Capture(main,Path.Combine(directory,"week.png"));
                FindButton(board,"月历").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));var work=vm.State.Tables.Single(t=>t.Name=="工作安排").Id;vm.HiddenTables.Add(work);vm.Notify();await Task.Delay(100);
                Check(!Texts(board).Contains("工作（示例）"),"显示过滤隐藏工作表卡片");Check(Texts(board).Contains("! 时间冲突"),"隐藏但生效的工作表仍参与冲突检查");vm.HiddenTables.Remove(work);vm.Notify();
                board.NavigateTo(new(2020,8,1));await Task.Delay(100);Check(Walk(board).OfType<Grid>().Any(g=>g.RowDefinitions.Count==6&&g.ColumnDefinitions.Count==7),"六行月份保留全部日期");Capture(main,Path.Combine(directory,"six-row-month.png"));
                board.NavigateTo(new(2026,10,27));board.SetView("LIST");await Task.Delay(100);var span=Walk(board).OfType<TextBlock>().First(t=>t.Text.Contains("按日期查看")).Text;FindButton(board,"加载下月").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(100);Check(Walk(board).OfType<TextBlock>().Any(t=>t.Text.Contains("2026年11月")&&t.Text.Contains("按日期查看")),"列表可追加相邻月份和跨日续段");
                board.SetView("MONTH");board.NavigateTo(new(2026,10,27));main.Width=1120;main.Height=680;await Task.Delay(150);Capture(main,Path.Combine(directory,"compact-window.png"));Check(board.ActualWidth>400,"最小窗口仍有可用月历宽度");var compactScroll=Walk(board).OfType<ScrollViewer>().First(s=>s.Content is Grid);Check(compactScroll.ScrollableWidth>0&&((Grid)compactScroll.Content).ActualWidth>=840,"窄窗口月历保持可读列宽并支持横向滚动");
                bool regressions=true;void Regression(bool ok,string label){log.Add((ok?"PASS ":"FAIL ")+label);regressions&=ok;}
                var columns=((Grid)compactScroll.Content).ColumnDefinitions.Select(c=>c.ActualWidth).ToArray();Regression(columns.Max()-columns.Min()<1,"长备注下月历七列等宽且表头宽度同步");
                board.SetView("LIST");board.NavigateTo(new(2020,8,1));await Task.Delay(60);FindButton(board,"›").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(60);FindButton(board,"›").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(60);Regression(board.DisplayMonth==new DateOnly(2020,10,1),"空月份列表仍可连续切换月份");
                board.SetView("MONTH");board.NavigateTo(new(2026,10,27));await Task.Delay(60);FindButton(board,"15").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(60);Regression(vm.SelectedDate==new DateOnly(2026,10,15)&&vm.Week==new DateOnly(2026,10,12),"月历选日同步周课表及检查所在周");
                Check(regressions,"审阅回归检查全部通过");
                var prefs=new BoardPreferenceStore(directory).Load();Check(prefs.View=="MONTH"&&prefs.Completed.Contains(key),"视图切换后完成状态仍保留");code=0;
            }
            catch(Exception ex){log.Add("FAIL "+ex);}
            finally{File.WriteAllLines(Path.Combine(directory,"board-checks.txt"),log);main.Close();frame.Continue=false;app.Shutdown(code);}
        });Dispatcher.PushFrame(frame);return code;
        void Check(bool result,string label){if(!result)throw new Exception(label);log.Add("PASS "+label);}
    }
    static void Seed(PlannerViewModel vm)
    {
        var work=vm.State.Tables.Single(t=>t.Name=="工作安排").Id;var personal=vm.State.Tables.Single(t=>t.Name=="个人安排").Id;
        var commands=new List<EditCommand>{new(){Activity=new(){TableId=work,Title="工作（示例）",Kind="WEEKLY",DurationMin=540},Rule=new(){ValidFrom=new(2026,10,1),WeekMask=21,StartTime="09:00"}},new(){Activity=new(){TableId=personal,Title="图书馆阅读（示例）",StartAt=new(2026,10,27,14,0,0),EndAt=new(2026,10,27,16,0,0),DurationMin=120,Note="归还书籍，整理本周阅读笔记。"},Place=new(){Name="南京图书馆",InputName="南京图书馆"}},new(){Activity=new(){TableId=personal,Title="与工作重叠的预约（示例）",StartAt=new(2026,10,16,13,0,0),EndAt=new(2026,10,16,14,0,0),DurationMin=60}},new(){Activity=new(){TableId=personal,Title="跨日整理（示例）",StartAt=new(2026,10,31,23,0,0),EndAt=new(2026,11,1,2,0,0),DurationMin=180}}};
        for(int i=0;i<5;i++)commands.Add(new(){Activity=new(){TableId=personal,Title="设计评审与资料整理（示例）"+i,StartAt=new(2026,10,15,8+i*2,0,0),EndAt=new(2026,10,15,9+i*2,0,0),DurationMin=60,Note=i==0?string.Concat(Enumerable.Repeat("长备注内容应完整换行展示，不应遮挡下一项活动。",4)):"核对资料后归档。"},Place=new(){Name="新街口",InputName="新街口"}});
        vm.Edit(commands);
    }
    static IEnumerable<DependencyObject> Walk(DependencyObject root){yield return root;foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())foreach(var item in Walk(child))yield return item;}
    static List<string> Texts(DependencyObject root)=>Walk(root).OfType<TextBlock>().Select(t=>t.Text).ToList();
    static Button FindButton(DependencyObject root,string text)=>Walk(root).OfType<Button>().First(b=>b.Content is string s&&s==text);
    static void Capture(Window window,string path)
    {
        window.UpdateLayout();var element=(FrameworkElement)window.Content;int width=(int)Math.Ceiling(element.ActualWidth),height=(int)Math.Ceiling(element.ActualHeight);var drawing=new DrawingVisual();using(var dc=drawing.RenderOpen()){dc.DrawRectangle(window.Background,null,new Rect(0,0,width,height));dc.DrawRectangle(new VisualBrush(element),null,new Rect(0,0,width,height));}var image=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);image.Render(drawing);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));using var stream=File.Create(path);png.Save(stream);
    }
}
