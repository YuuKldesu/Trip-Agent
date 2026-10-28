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
        var grid=new Grid();grid.ColumnDefinitions.Add(new());grid.ColumnDefinitions.Add(new(){Width=GridLength.Auto});Content=grid;
        input=Ui.Input(value);grid.Children.Add(input);input.TextChanged+=(_,_)=>Changed?.Invoke();
        var open=Ui.Button("◷",OpenPicker);open.ToolTip="滚轮选择时间";open.Padding=new(9,6,9,6);Grid.SetColumn(open,1);grid.Children.Add(open);input.PreviewMouseDoubleClick+=(_,e)=>{OpenPicker();e.Handled=true;};
        PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape&&popup?.IsOpen==true){popup.IsOpen=false;e.Handled=true;}else if(e.Key==Key.Down&&(Keyboard.Modifiers&ModifierKeys.Alt)!=0){OpenPicker();e.Handled=true;}};Unloaded+=(_,_)=>{if(popup is not null)popup.IsOpen=false;};
    }
    public void OpenPicker()
    {
        if(popup?.IsOpen==true){popup.IsOpen=false;return;}var time=TimeOnly.TryParseExact(Text,"HH:mm",out var t)?t:new TimeOnly(9,0);var picker=new TimeWheelPicker(DateContext.Date.Add(time.ToTimeSpan()));popup=StyledDateEntry.MakePopup(this,picker);
        picker.Confirmed+=value=>{DateContext=value.Date;Text=value.ToString("HH:mm");DateTimeChanged?.Invoke(value);popup.IsOpen=false;};picker.Cancelled+=()=>popup.IsOpen=false;popup.IsOpen=true;
    }
}
