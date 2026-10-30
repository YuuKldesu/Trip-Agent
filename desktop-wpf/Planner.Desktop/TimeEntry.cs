using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Planner.Core;
namespace Planner.Desktop;
public sealed class TimeEntry:UserControl
{
    readonly TextBox input;Popup? popup;
    public string Text {get=>input.Text;set=>input.Text=value;}
    public event Action? Changed;
    public DateTime DateContext {get;set;}=Clock.Today.ToDateTime(TimeOnly.MinValue);
    public event Action<DateTime>? DateTimeChanged;
    public TimeEntry(string value)
    {
        Margin=new(0,3,0,8);MinHeight=36;
        var grid=new Grid();Content=new Border{CornerRadius=new(8),Background=AppTheme.Brush("Surface"),BorderBrush=AppTheme.Brush("Border"),BorderThickness=new(1),Child=grid};
        input=Ui.Input(value);input.Margin=new(0);input.Padding=new(10,6,36,6);input.BorderThickness=new(0);input.Background=System.Windows.Media.Brushes.Transparent;input.MinHeight=34;grid.Children.Add(input);input.TextChanged+=(_,_)=>Changed?.Invoke();
        var icon=ShellIcons.Glyph("\uE823",16);icon.HorizontalAlignment=HorizontalAlignment.Right;icon.Margin=new(0,0,10,0);icon.IsHitTestVisible=false;grid.Children.Add(icon);ToolTip="点击选择时间，也可输入 HH:mm";input.PreviewMouseLeftButtonUp+=(_,e)=>{OpenPicker();e.Handled=true;};
        PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape&&popup?.IsOpen==true){popup.IsOpen=false;e.Handled=true;}else if(e.Key==Key.Down&&(Keyboard.Modifiers&ModifierKeys.Alt)!=0){OpenPicker();e.Handled=true;}};Unloaded+=(_,_)=>{if(popup is not null)popup.IsOpen=false;};
    }
    public void OpenPicker()
    {
        if(popup?.IsOpen==true){popup.IsOpen=false;return;}var time=TimeOnly.TryParseExact(Text,"HH:mm",out var t)?t:new TimeOnly(9,0);var picker=new TimeWheelPicker(DateContext.Date.Add(time.ToTimeSpan()));popup=StyledDateEntry.MakePopup(this,picker);
        picker.Confirmed+=value=>{DateContext=value.Date;Text=value.ToString("HH:mm");DateTimeChanged?.Invoke(value);popup.IsOpen=false;};picker.Cancelled+=()=>popup.IsOpen=false;popup.IsOpen=true;
    }
}
