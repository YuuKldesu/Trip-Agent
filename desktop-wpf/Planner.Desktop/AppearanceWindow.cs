using System.Windows;
using System.Windows.Controls;
namespace Planner.Desktop;
public sealed class AppearanceWindow:Window
{
    public AppearanceWindow(PlannerViewModel vm)
    {
        Title="外观与字号";Width=480;Height=470;MinWidth=360;MinHeight=360;WindowStartupLocation=WindowStartupLocation.CenterOwner;Background=Ui.Brush("#FBF7F0");
        var root=new DockPanel{Margin=new(22)};Content=root;var close=Ui.Button("完成",Close,true);DockPanel.SetDock(close,Dock.Bottom);root.Children.Add(close);var panel=new StackPanel();root.Children.Add(new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
        panel.Children.Add(Ui.Text("外观与字号",21,true));var value=Ui.Text("",14,true);panel.Children.Add(value);
        var slider=new Slider{Minimum=12,Maximum=22,TickFrequency=1,IsSnapToTickEnabled=true,Value=vm.Appearance.FontSize,SmallChange=1,LargeChange=2,Margin=new(0,8,0,18)};panel.Children.Add(slider);
        var presets=new WrapPanel();foreach(int size in new[]{12,14,18,22}){int selected=size;presets.Children.Add(Ui.Button(size==14?"14 · 标准":size.ToString(),()=>slider.Value=selected));}panel.Children.Add(presets);
        void FontLabel()=>value.Text=$"正文字号：{vm.Appearance.FontSize:0}（即时预览）";FontLabel();slider.ValueChanged+=(_,e)=>{double font=Math.Round(e.NewValue);if(font==vm.Appearance.FontSize)return;Ui.Safe(()=>vm.UpdateAppearance(p=>p.FontSize=font));FontLabel();};
        panel.Children.Add(Ui.Text("界面模式",15,true));string[] modes=["AUTO","FULL","COMPACT"];var mode=Ui.Choices(["自动切换","完整界面","简洁侧栏"],Array.IndexOf(modes,vm.Appearance.LayoutMode));panel.Children.Add(mode);mode.SelectionChanged+=(_,_)=>Ui.Safe(()=>{if(Owner is MainWindow main)main.SetLayoutMode(modes[mode.SelectedIndex]);else vm.UpdateAppearance(p=>p.LayoutMode=modes[mode.SelectedIndex]);});
        panel.Children.Add(Ui.Text("自动模式会结合窗口宽度、纵横比例和字号调整；简洁模式展示当天及随后6天。",12));
        var pin=new CheckBox{Content="简洁模式保持置顶",IsChecked=vm.Appearance.AlwaysOnTop};pin.Click+=(_,_)=>Ui.Safe(()=>vm.UpdateAppearance(p=>p.AlwaysOnTop=pin.IsChecked==true));panel.Children.Add(pin);
        panel.Children.Add(Ui.Text("星星标记重要；统一右滑切换完成，右键同样可以操作。字号和显示状态保存在本机。",12));
    }
}
