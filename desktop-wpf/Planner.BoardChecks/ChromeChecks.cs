using System.Windows;
using System.Windows.Controls;
using Planner.Desktop;
static class ChromeChecks
{
 public static async Task Run(MainWindow main,Action<bool,string> check)
 {
  await Task.Delay(80);check(main.WindowStyle==WindowStyle.None,"主窗口移除系统边框与标题栏");var min=Find(main,"WindowMinimize");min.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(40);check(main.WindowState==WindowState.Minimized,"无边框窗口保留最小化");main.WindowState=WindowState.Normal;await Task.Delay(40);Find(main,"WindowMaximize").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(80);check(main.WindowState==WindowState.Maximized,"无边框窗口保留最大化");Find(main,"WindowMaximize").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(60);check(main.WindowState==WindowState.Normal&&main.ResizeMode==ResizeMode.CanResize,"无边框窗口保留还原和边缘缩放");
  var w=new Window{Title="子窗口边框检查",Width=400,Height=250,Owner=main,Content=new TextBlock{Text="子窗口"},WindowStartupLocation=WindowStartupLocation.CenterOwner};w.Show();await Task.Delay(50);check(w.WindowStyle==WindowStyle.None,"独立子窗口沿用同色无边框顶栏");Find(w,"WindowClose").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(!w.IsVisible,"自绘关闭按钮关闭对应子窗口");
 }
 static Button Find(DependencyObject root,string tag)=>Walk(root).OfType<Button>().First(b=>Equals(b.Tag,tag));
 static IEnumerable<DependencyObject> Walk(DependencyObject root){yield return root;foreach(var c in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())foreach(var d in Walk(c))yield return d;}
}
