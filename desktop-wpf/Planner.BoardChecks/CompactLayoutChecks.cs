using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Planner.Desktop;
static class CompactLayoutChecks
{
    public static async Task Run(string directory,Action<bool,string> check)
    {
        var vm=new PlannerViewModel(Path.Combine(directory,"compact-regression"),false);var main=new MainWindow(vm);main.Show();
        try
        {
            foreach(double font in new[]{14d,22d})
            {
                vm.UpdateAppearance(p=>p.FontSize=font);
                var editor=new EditorWindow(vm){Owner=main};editor.Show();
                try
                {
                    await Task.Delay(40);var dpi=VisualTreeHelper.GetDpi(editor);var limits=NativeLimits(editor);
                    check(limits.MinTrackSize.X>=Math.Ceiling(editor.MinWidth*dpi.DpiScaleX)&&limits.MinTrackSize.Y>=Math.Ceiling(editor.MinHeight*dpi.DpiScaleY),$"字号{font}原生拖动最小尺寸包含当前DPI换算");
                    foreach(double width in new[]{820d,640d,520d,440d})
                    {
                        editor.Width=width;editor.Height=420;await Task.Delay(35);editor.UpdateLayout();
                        var viewport=Walk(editor).OfType<ScrollViewer>().First(s=>s.Content is StackPanel);
                        var form=(FrameworkElement)viewport.Content;
                        check(form.ActualWidth<=viewport.ViewportWidth+2,$"字号{font}窗口{width}表单宽度受视口约束");
                        var save=Walk(editor).OfType<Button>().Single(b=>Equals(b.Content,"保存"));var point=save.TranslatePoint(new Point(save.ActualWidth,save.ActualHeight),editor);
                        check(point.X<=editor.ActualWidth&&point.Y<=editor.ActualHeight&&point.X>0&&point.Y>0,$"字号{font}窗口{width}保存按钮始终可达");
                        viewport.ScrollToBottom();editor.UpdateLayout();var more=Walk(editor).OfType<Expander>().Single();more.IsExpanded=true;editor.UpdateLayout();viewport.ScrollToBottom();editor.UpdateLayout();
                        var lead=Walk(editor).OfType<TextBox>().Single(t=>Equals(t.Tag,"ReminderLead"));check(lead.ActualWidth<=84&&lead.ActualHeight<48,$"字号{font}窗口{width}提醒数字框紧凑");
                        check(Walk(editor).OfType<TextBox>().Single(t=>Equals(t.Tag,"ActivityTitle")).MaxLines==1,"标题保持单行，长标题可在框内横向编辑");
                    }
                    editor.WindowState=WindowState.Maximized;await Task.Delay(30);editor.WindowState=WindowState.Normal;await Task.Delay(30);check(editor.ActualWidth>=editor.MinWidth,"最大化还原后保留有效编辑宽度");
                }
                finally{editor.Close();}
            }
            vm.UpdateAppearance(p=>{p.FontSize=14;p.LayoutMode="FULL";});main.OpenSettingsCategory("appearance");await Task.Delay(40);main.UpdateLayout();var settings=Walk(main).OfType<SettingsPage>().Single();var search=Walk(settings).OfType<TextBox>().Single(t=>Equals(t.Tag,"SettingsSearch"));var placeholder=Walk(settings).OfType<TextBlock>().Single(t=>Equals(t.Tag,"SettingsSearchPlaceholder"));
            check(search.ActualWidth<=560&&placeholder.IsVisible&&placeholder.Text.Contains("搜索设置"),"设置搜索有宽度上限和可见用途引导");search.Text="找不到的设置";check(Walk(settings).OfType<TextBlock>().Any(t=>Equals(t.Tag,"SettingsSearchStatus")&&t.Text.Contains("未找到")),"搜索无结果有明确反馈");Walk(settings).OfType<Button>().Single(b=>Equals(b.Tag,"SettingsSearchClear")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(search.Text==""&&placeholder.IsVisible,"清除搜索后恢复引导");
            main.ReturnToBoard();vm.UpdateAppearance(p=>p.AssistantPanelOpen=true);await Task.Delay(30);check(!Walk(main).OfType<Button>().Any(b=>b.Content is string t&&t.Contains("复制")),"助手不再显示复制按钮");
        }
        finally{main.Close();}
    }
    static NativeMinMax NativeLimits(Window window)
    {
        IntPtr buffer=Marshal.AllocHGlobal(Marshal.SizeOf<NativeMinMax>());
        try{Marshal.StructureToPtr(new NativeMinMax(),buffer,false);SendMessage(new WindowInteropHelper(window).Handle,0x24,IntPtr.Zero,buffer);return Marshal.PtrToStructure<NativeMinMax>(buffer);}finally{Marshal.FreeHGlobal(buffer);}
    }
    [StructLayout(LayoutKind.Sequential)]struct NativePoint{public int X,Y;}
    [StructLayout(LayoutKind.Sequential)]struct NativeMinMax{public NativePoint Reserved,MaxSize,MaxPosition,MinTrackSize,MaxTrackSize;}
    [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr hwnd,int message,IntPtr wParam,IntPtr lParam);
    static IEnumerable<DependencyObject> Walk(DependencyObject root){yield return root;foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())foreach(var item in Walk(child))yield return item;}
}
