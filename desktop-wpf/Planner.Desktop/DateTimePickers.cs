using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Planner.Core;
namespace Planner.Desktop;

public sealed class StyledDateEntry:DatePicker
{
    Popup? picker;
    public StyledDateEntry()
    {
        var button=new FrameworkElementFactory(typeof(Button));button.SetValue(Button.HorizontalContentAlignmentProperty,HorizontalAlignment.Left);
        button.SetBinding(Button.ContentProperty,new System.Windows.Data.Binding(nameof(SelectedDate)){Source=this,StringFormat="{0:yyyy/MM/dd}",TargetNullValue="选择日期"});
        button.AddHandler(Button.ClickEvent,new RoutedEventHandler((_,e)=>{OpenPicker();e.Handled=true;}));Template=new ControlTemplate(typeof(DatePicker)){VisualTree=button};
        Unloaded+=(_,_)=>{if(picker is not null)picker.IsOpen=false;};
    }
    public void OpenPicker()
    {
        if(picker?.IsOpen==true){picker.IsOpen=false;return;}
        var calendar=new System.Windows.Controls.Calendar{SelectedDate=SelectedDate??Clock.Today.ToDateTime(TimeOnly.MinValue),DisplayDate=SelectedDate??Clock.Now,Background=Ui.Brush("#FFFDF9"),BorderThickness=new(0),Foreground=Ui.Brush("#443A32"),FirstDayOfWeek=DayOfWeek.Monday};
        var dayStyle=new Style(typeof(CalendarDayButton));dayStyle.Setters.Add(new Setter(BackgroundProperty,Brushes.Transparent));dayStyle.Setters.Add(new Setter(ForegroundProperty,Ui.Brush("#443A32")));dayStyle.Setters.Add(new Setter(FontSizeProperty,14d));
        var border=new FrameworkElementFactory(typeof(Border));border.SetValue(Border.CornerRadiusProperty,new CornerRadius(9));border.SetBinding(Border.BackgroundProperty,new System.Windows.Data.Binding("Background"){RelativeSource=new(System.Windows.Data.RelativeSourceMode.TemplatedParent)});var text=new FrameworkElementFactory(typeof(ContentPresenter));text.SetValue(HorizontalAlignmentProperty,HorizontalAlignment.Center);text.SetValue(VerticalAlignmentProperty,VerticalAlignment.Center);text.SetValue(MarginProperty,new Thickness(5));border.AppendChild(text);dayStyle.Setters.Add(new Setter(TemplateProperty,new ControlTemplate(typeof(CalendarDayButton)){VisualTree=border}));
        var selected=new Trigger{Property=CalendarDayButton.IsSelectedProperty,Value=true};selected.Setters.Add(new Setter(BackgroundProperty,Ui.Brush("#AC5A4C")));selected.Setters.Add(new Setter(ForegroundProperty,Brushes.White));dayStyle.Triggers.Add(selected);var inactive=new Trigger{Property=CalendarDayButton.IsInactiveProperty,Value=true};inactive.Setters.Add(new Setter(OpacityProperty,.45));dayStyle.Triggers.Add(inactive);calendar.CalendarDayButtonStyle=dayStyle;
        var panel=new StackPanel();panel.Children.Add(Ui.Text("选择日期",15,true));panel.Children.Add(calendar);var actions=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right};panel.Children.Add(actions);
        actions.Children.Add(Ui.Button("今天",()=>{calendar.DisplayDate=Clock.Now;calendar.SelectedDate=Clock.Today.ToDateTime(TimeOnly.MinValue);}));actions.Children.Add(Ui.Button("取消",()=>picker!.IsOpen=false));actions.Children.Add(Ui.Button("确定日期",()=>{SelectedDate=calendar.SelectedDate;picker!.IsOpen=false;},true));
        picker=MakePopup(this,panel);picker.IsOpen=true;
    }
    internal static Popup MakePopup(FrameworkElement target,UIElement child)=>new(){PlacementTarget=target,Placement=PlacementMode.Bottom,StaysOpen=false,AllowsTransparency=true,Child=new Border{Background=Ui.Brush("#FFFDF9"),BorderBrush=Ui.Brush("#E5DBCF"),BorderThickness=new(1),CornerRadius=new(17),Padding=new(14),Child=child}};
}

public sealed class TimeWheel:Border
{
    readonly bool hours;readonly Func<DateTime> current;readonly Action<DateTime> changed;readonly StackPanel rows=new();Point anchor;double residual;bool dragging,moved;TouchDevice? touch;
    public TimeWheel(bool hours,Func<DateTime> current,Action<DateTime> changed)
    {
        this.hours=hours;this.current=current;this.changed=changed;Width=100;Height=220;Child=rows;Background=Brushes.Transparent;Focusable=true;Refresh();
        PreviewMouseWheel+=(_,e)=>{RollBy(e.Delta>0?-1:1);e.Handled=true;};
        PreviewMouseLeftButtonDown+=(_,e)=>{if(e.StylusDevice is not null)return;anchor=e.GetPosition(this);residual=0;moved=false;dragging=true;CaptureMouse();Focus();e.Handled=true;};
        PreviewMouseMove+=(_,e)=>{if(!dragging||e.LeftButton!=MouseButtonState.Pressed)return;Drag(e.GetPosition(this));e.Handled=true;};
        PreviewMouseLeftButtonUp+=(_,e)=>{if(!dragging)return;dragging=false;if(!moved)RollBy(Math.Clamp((int)(e.GetPosition(this).Y/44)-2,-2,2));ReleaseMouseCapture();e.Handled=true;};LostMouseCapture+=(_,_)=>dragging=false;
        PreviewTouchDown+=(_,e)=>{touch=e.TouchDevice;anchor=e.GetTouchPoint(this).Position;residual=0;moved=false;CaptureTouch(touch);e.Handled=true;};
        PreviewTouchMove+=(_,e)=>{if(touch!=e.TouchDevice)return;Drag(e.GetTouchPoint(this).Position);e.Handled=true;};
        PreviewTouchUp+=(_,e)=>{if(touch!=e.TouchDevice)return;if(!moved)RollBy(Math.Clamp((int)(e.GetTouchPoint(this).Position.Y/44)-2,-2,2));ReleaseTouchCapture(touch);touch=null;e.Handled=true;};LostTouchCapture+=(_,_)=>touch=null;
        PreviewKeyDown+=(_,e)=>{if(e.Key is Key.Up or Key.Down){RollBy(e.Key==Key.Up?-1:1);e.Handled=true;}};
    }
    void Drag(Point point){double delta=point.Y-anchor.Y;if(Math.Abs(delta)>5)moved=true;residual-=delta;anchor=point;int steps=(int)(residual/44);if(steps!=0){residual-=steps*44;RollBy(steps);}}
    public void RollBy(int steps){if(steps!=0)Ui.Safe(()=>changed(DateTimeSelection.Roll(current(),hours,steps)));}
    public void Refresh()
    {
        rows.Children.Clear();var value=current();for(int i=-2;i<=2;i++){int delta=i;DateTime candidate;try{candidate=DateTimeSelection.Roll(value,hours,i);}catch(ArgumentException){candidate=value;}var b=Ui.Button((hours?candidate.Hour:candidate.Minute).ToString("00"),()=>RollBy(delta));b.Height=44;b.MinHeight=44;b.Margin=new(0);b.Padding=new(0);b.Background=Ui.Brush(i==0?"#F0E3D5":"#FFFDF9");b.Foreground=Ui.Brush(i==0?"#8E5339":"#8C7F73");Typography.Set(b,i==0?28:21);b.Opacity=Math.Abs(i)==2?.35:i==0?1:.7;rows.Children.Add(b);}
    }
}

public sealed class TimeWheelPicker:StackPanel
{
    DateTime value;readonly TextBlock day=Ui.Text("",14,true);readonly TimeWheel hour,minute;readonly TextBox manual;readonly TextBlock status=Ui.Text("",11);
    public DateTime Value=>value;
    public event Action<DateTime>? Confirmed;public event Action? Cancelled;
    public TimeWheelPicker(DateTime initial)
    {
        value=initial;Width=290;Children.Add(Ui.Text("选择时间 · 24小时制",16,true));Children.Add(day);var labels=new Grid();labels.ColumnDefinitions.Add(new());labels.ColumnDefinitions.Add(new());var h=Ui.Text("时",12);h.HorizontalAlignment=HorizontalAlignment.Center;labels.Children.Add(h);var m=Ui.Text("分",12);m.HorizontalAlignment=HorizontalAlignment.Center;Grid.SetColumn(m,1);labels.Children.Add(m);Children.Add(labels);
        var wheels=new Grid();wheels.ColumnDefinitions.Add(new());wheels.ColumnDefinitions.Add(new(){Width=new(24)});wheels.ColumnDefinitions.Add(new());hour=new(true,()=>value,SetValue);minute=new(false,()=>value,SetValue);wheels.Children.Add(hour);Grid.SetColumn(minute,2);wheels.Children.Add(minute);var colon=Ui.Text(":",26,true);colon.VerticalAlignment=VerticalAlignment.Center;colon.HorizontalAlignment=HorizontalAlignment.Center;Grid.SetColumn(colon,1);wheels.Children.Add(colon);Children.Add(wheels);
        manual=Ui.Input(value.ToString("HH:mm"));manual.Tag="PickerTimeInput";Ui.Field(this,"直接输入（HH:mm）",manual);Children.Add(status);var actions=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right};actions.Children.Add(Ui.Button("取消",()=>Cancelled?.Invoke()));actions.Children.Add(Ui.Button("确定时间",Confirm,true));Children.Add(actions);SetValue(initial);
        manual.KeyDown+=(_,e)=>{if(e.Key==Key.Enter){Confirm();e.Handled=true;}if(e.Key==Key.Escape){Cancelled?.Invoke();e.Handled=true;}};
    }
    void SetValue(DateTime next){value=next;day.Text=value.ToString("yyyy/MM/dd dddd",CultureInfo.GetCultureInfo("zh-CN"));manual.Text=value.ToString("HH:mm");status.Text="滚动或拖动数字，跨过边界会同步调整日期。";hour.Refresh();minute.Refresh();}
    void Confirm(){if(!TimeOnly.TryParseExact(manual.Text.Trim(),"HH:mm",out var t)){status.Text="请输入00:00—23:59，例如10:05。";return;}Confirmed?.Invoke(value.Date.Add(t.ToTimeSpan()));}
}
