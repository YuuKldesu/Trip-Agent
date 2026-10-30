using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
namespace Planner.Desktop;

public static class AppChrome
{
    static readonly DependencyProperty AttachedProperty=DependencyProperty.RegisterAttached("Attached",typeof(bool),typeof(AppChrome),new PropertyMetadata(false));
    public static void Attach(Window window)
    {
        if((bool)window.GetValue(AttachedProperty)||window.Content is not UIElement content)return;window.SetValue(AttachedProperty,true);if(window.ReadLocalValue(Control.BackgroundProperty)==DependencyProperty.UnsetValue)window.Background=Ui.Brush("#FBF7F0");if(window.ReadLocalValue(Control.ForegroundProperty)==DependencyProperty.UnsetValue)window.Foreground=Ui.Brush("#443A32");window.FontFamily=new("Microsoft YaHei UI");Typography.Set(window,13);bool resizable=window.ResizeMode is ResizeMode.CanResize or ResizeMode.CanResizeWithGrip;
        window.UseLayoutRounding=true;window.SnapsToDevicePixels=true;window.WindowStyle=WindowStyle.None;WindowChrome.SetWindowChrome(window,new(){CaptionHeight=38,ResizeBorderThickness=new(resizable?6:0),CornerRadius=new(12),GlassFrameThickness=new(0),UseAeroCaptionButtons=false});
        window.Content=null;var layout=new Grid{Background=window.Background};layout.RowDefinitions.Add(new(){Height=new(38)});layout.RowDefinitions.Add(new());var caption=new DockPanel{Tag="AppCaption",Background=Ui.Brush("#EEDFD0"),LastChildFill=true};layout.Children.Add(caption);var actions=new StackPanel{Orientation=Orientation.Horizontal};DockPanel.SetDock(actions,Dock.Right);caption.Children.Add(actions);
        Button Action(string glyph,string name,string tag,Action run){var b=Ui.Button("",run);b.Content=ShellIcons.Glyph(glyph,12);b.Width=44;b.Height=36;b.MinHeight=36;b.Padding=new(0);b.Margin=new(0);b.Background=Brushes.Transparent;b.HorizontalContentAlignment=HorizontalAlignment.Center;b.ToolTip=name;b.Tag=tag;System.Windows.Automation.AutomationProperties.SetName(b,name);WindowChrome.SetIsHitTestVisibleInChrome(b,true);actions.Children.Add(b);return b;}
        Action("\uE921","最小化","WindowMinimize",()=>window.WindowState=WindowState.Minimized);var maximize=Action("\uE922","最大化或还原","WindowMaximize",()=>window.WindowState=window.WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized);maximize.IsEnabled=resizable;Action("\uE8BB","关闭","WindowClose",window.Close);
        var title=new TextBlock{FontSize=12,Margin=new(15,0,8,0),VerticalAlignment=VerticalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis};title.SetBinding(TextBlock.TextProperty,new Binding(nameof(Window.Title)){Source=window});caption.Children.Add(title);Grid.SetRow(content,1);layout.Children.Add(content);var frame=new Border{Tag="AppWindowFrame",Background=window.Background,BorderBrush=Ui.Brush("#A98164"),BorderThickness=new(1.5),CornerRadius=new(12),ClipToBounds=true,Child=layout};window.Content=frame;
        window.StateChanged+=(_,_)=>{maximize.Content=ShellIcons.Glyph(window.WindowState==WindowState.Maximized?"\uE923":"\uE922",12);frame.CornerRadius=new(window.WindowState==WindowState.Maximized?0:12);};
        var handle=new WindowInteropHelper(window).Handle;if(HwndSource.FromHwnd(handle) is {} source)source.AddHook((IntPtr hwnd,int message,IntPtr wParam,IntPtr lParam,ref bool handled)=>ConstrainMaximize(window,hwnd,message,wParam,lParam,ref handled));
    }
    static IntPtr ConstrainMaximize(Window window,IntPtr hwnd,int message,IntPtr wParam,IntPtr lParam,ref bool handled)
    {
        if(message!=0x24)return IntPtr.Zero;var monitor=MonitorFromWindow(hwnd,2);var info=new MonitorInfo{Size=Marshal.SizeOf<MonitorInfo>()};if(monitor==IntPtr.Zero||!GetMonitorInfo(monitor,ref info))return IntPtr.Zero;var bounds=Marshal.PtrToStructure<MinMaxInfo>(lParam);
        // WM_GETMINMAXINFO uses physical screen pixels; WPF MinWidth/MinHeight use DIPs.
        // Handling this message suppresses WPF's default limit handling, so preserve those limits here.
        var transform=HwndSource.FromHwnd(hwnd)?.CompositionTarget?.TransformToDevice??Matrix.Identity;
        bounds.MinTrackSize.X=Math.Max(bounds.MinTrackSize.X,(int)Math.Ceiling(window.MinWidth*transform.M11));bounds.MinTrackSize.Y=Math.Max(bounds.MinTrackSize.Y,(int)Math.Ceiling(window.MinHeight*transform.M22));
        if(double.IsFinite(window.MaxWidth))bounds.MaxTrackSize.X=Math.Max(bounds.MinTrackSize.X,(int)Math.Floor(window.MaxWidth*transform.M11));
        if(double.IsFinite(window.MaxHeight))bounds.MaxTrackSize.Y=Math.Max(bounds.MinTrackSize.Y,(int)Math.Floor(window.MaxHeight*transform.M22));
        bounds.MaxPosition=new(){X=info.Work.Left-info.Monitor.Left,Y=info.Work.Top-info.Monitor.Top};bounds.MaxSize=new(){X=info.Work.Right-info.Work.Left,Y=info.Work.Bottom-info.Work.Top};Marshal.StructureToPtr(bounds,lParam,false);handled=true;return IntPtr.Zero;
    }
    [StructLayout(LayoutKind.Sequential)]struct NativePoint{public int X,Y;}
    [StructLayout(LayoutKind.Sequential)]struct NativeRect{public int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential)]struct MinMaxInfo{public NativePoint Reserved,MaxSize,MaxPosition,MinTrackSize,MaxTrackSize;}
    [StructLayout(LayoutKind.Sequential)]struct MonitorInfo{public int Size;public NativeRect Monitor,Work;public uint Flags;}
    [DllImport("user32.dll")]static extern IntPtr MonitorFromWindow(IntPtr hwnd,uint flags);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)][return:MarshalAs(UnmanagedType.Bool)]static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo info);
}
