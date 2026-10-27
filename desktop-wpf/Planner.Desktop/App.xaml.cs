using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Planner.Core;

namespace Planner.Desktop;
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var index=Array.IndexOf(e.Args,"--data-dir");
        var data=index>=0&&index+1<e.Args.Length?Path.GetFullPath(e.Args[index+1]):Path.Combine(AppContext.BaseDirectory,"Data");
        DispatcherUnhandledException+=(s,x)=>{MessageBox.Show(x.Exception.Message,"操作未完成",MessageBoxButton.OK,MessageBoxImage.Warning);x.Handled=true;};
        try
        {
            Directory.CreateDirectory(data);
            var vm=new PlannerViewModel(data);var window=new MainWindow(vm);MainWindow=window;window.Show();
            if(e.Args.Contains("--smoke"))
            {
                if(vm.State.Activities.Count==0)vm.LoadSample();
                window.Dispatcher.BeginInvoke(async()=>{
                    await Task.Delay(800);window.UpdateLayout();
                    void Capture(Window target,string name){target.UpdateLayout();var visual=(FrameworkElement)target.Content;var image=new RenderTargetBitmap((int)Math.Ceiling(visual.ActualWidth),(int)Math.Ceiling(visual.ActualHeight),96,96,PixelFormats.Pbgra32);image.Render(visual);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using var stream=File.Create(Path.Combine(data,name));encoder.Save(stream);}
                    Capture(window,"ui-smoke.png");
                    var editor=new EditorWindow(vm,vm.WeekItems().First()){Owner=window};editor.Show();await Task.Delay(200);Capture(editor,"editor-smoke.png");editor.Close();
                    var route=new RouteWindow(vm){Owner=window};route.Show();await Task.Delay(200);Capture(route,"route-smoke.png");route.Close();
                    File.WriteAllText(Path.Combine(data,"smoke-result.txt"),$"WPF rendered main/editor/routes; main={window.ActualWidth}x{window.ActualHeight}; activities={vm.State.Activities.Count}; revision={vm.State.Revision}");
                    await Task.Delay(300);Shutdown();
                },DispatcherPriority.ApplicationIdle);
            }
        }
        catch(Exception ex){MessageBox.Show("无法打开本地数据："+ex.Message,"启动失败",MessageBoxButton.OK,MessageBoxImage.Error);Shutdown(1);}
    }
}
