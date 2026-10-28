using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Planner.Core;
namespace Planner.Desktop;

public sealed class SwipeActivityCard:Border
{
    readonly SwipeTracker tracker=new();readonly TranslateTransform translation=new();bool tracking;TouchDevice? touch;
    public BoardEntry Entry {get;}
    public Button Star {get;}
    public bool IsCompleted {get;private set;}
    public bool IsImportant {get;private set;}
    public event Action? ToggleRequested;public event Action? ImportantRequested;public event Action? EditRequested;
    public SwipeActivityCard(BoardEntry entry)
    {
        Entry=entry;Tag=entry;CornerRadius=new(16);Background=Ui.Brush("#FFFDF9");BorderBrush=Ui.Brush("#E6E1DB");BorderThickness=new(1);Padding=new(10);Margin=new(0,0,0,8);RenderTransform=translation;
        Star=Ui.Button("☆",()=>ImportantRequested?.Invoke());Star.Background=Brushes.Transparent;Star.Padding=new(3,0,3,0);Star.Margin=new(2,0,0,0);Star.MinWidth=28;Star.FontFamily=new("Segoe UI Symbol");Typography.Set(Star,18);
        AutomationProperties.SetName(this,entry.Occurrence.Title);Star.ToolTip="标记重要";
        Unloaded+=(_,_)=>CancelSwipe();
    }
    public void SetState(bool completed,bool important,bool animate=false)
    {
        bool changed=IsCompleted!=completed;IsCompleted=completed;IsImportant=important;
        Star.Content=important?"★":"☆";Star.Foreground=Ui.Brush(important?"#A47820":"#998979");Star.ToolTip=important?"取消重要标记":"标记重要";
        AutomationProperties.SetName(Star,(important?"取消重要：":"标记重要：")+Entry.Occurrence.Title);
        ToolTip=$"{Entry.Occurrence.Title}\n{Entry.Occurrence.Start:yyyy-MM-dd HH:mm} — {Entry.Occurrence.End:MM-dd HH:mm}\n"+(completed?"已完成":"待进行")+" · 右滑切换完成 · 点击编辑 · 右键更多";
        if(animate&&changed)AnimateOpacity();else{BeginAnimation(OpacityProperty,null);Opacity=completed? .55:1;}
    }
    public Point GesturePosition(Point local)=>Parent is UIElement parent?TranslatePoint(local,parent):local;
    public void BeginSwipe(Point point){tracking=true;tracker.Begin(point.X,point.Y);translation.BeginAnimation(TranslateTransform.XProperty,null);translation.X=0;BeginAnimation(OpacityProperty,null);Opacity=IsCompleted? .55:1;}
    public void ContinueSwipe(Point point)
    {
        if(!tracking)return;tracker.Move(point.X,point.Y);if(!tracker.Horizontal)return;
        double threshold=SwipeTracker.Threshold(ActualWidth);double progress=Math.Min(1,Math.Max(0,tracker.DeltaX)/threshold);
        translation.X=Math.Clamp(tracker.DeltaX*.75,-threshold*.6,threshold*1.15);
        Opacity=IsCompleted? .55+progress*.45:1-progress*.45;
    }
    public void EndSwipe(bool cancelled=false)
    {
        if(!tracking)return;tracking=false;if(cancelled)tracker.Cancel();var outcome=tracker.End(ActualWidth);
        if(IsMouseCaptured)ReleaseMouseCapture();if(touch is {} device){touch=null;ReleaseTouchCapture(device);}
        if(outcome==SwipeOutcome.Toggle)ToggleRequested?.Invoke();else if(outcome==SwipeOutcome.Tap)EditRequested?.Invoke();
        translation.BeginAnimation(TranslateTransform.XProperty,new DoubleAnimation(0,TimeSpan.FromMilliseconds(220)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}});AnimateOpacity();
    }
    void AnimateOpacity()=>BeginAnimation(OpacityProperty,new DoubleAnimation(IsCompleted? .55:1,TimeSpan.FromMilliseconds(220)));
    void CancelSwipe(){if(!tracking)return;EndSwipe(true);}
    bool Interactive(DependencyObject? target)
    {
        while(target is not null&&target!=this){if(target is ButtonBase or TextBoxBase or Selector)return true;target=target is Visual?VisualTreeHelper.GetParent(target):LogicalTreeHelper.GetParent(target);}return false;
    }
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e){base.OnMouseLeftButtonDown(e);if(e.StylusDevice is not null||Interactive(e.OriginalSource as DependencyObject))return;BeginSwipe(GesturePosition(e.GetPosition(this)));CaptureMouse();e.Handled=true;}
    protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(tracking&&touch is null){if(e.LeftButton!=MouseButtonState.Pressed){CancelSwipe();return;}ContinueSwipe(GesturePosition(e.GetPosition(this)));e.Handled=true;}}
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e){base.OnMouseLeftButtonUp(e);if(tracking&&touch is null){EndSwipe();e.Handled=true;}}
    protected override void OnLostMouseCapture(MouseEventArgs e){base.OnLostMouseCapture(e);if(touch is null)CancelSwipe();}
    protected override void OnTouchDown(TouchEventArgs e){base.OnTouchDown(e);if(Interactive(e.OriginalSource as DependencyObject)||tracking)return;touch=e.TouchDevice;BeginSwipe(GesturePosition(e.GetTouchPoint(this).Position));}
    protected override void OnTouchMove(TouchEventArgs e){base.OnTouchMove(e);if(touch!=e.TouchDevice)return;ContinueSwipe(GesturePosition(e.GetTouchPoint(this).Position));if(tracker.Horizontal){CaptureTouch(e.TouchDevice);e.Handled=true;}}
    protected override void OnTouchUp(TouchEventArgs e){base.OnTouchUp(e);if(touch==e.TouchDevice){EndSwipe();e.Handled=true;}}
    protected override void OnLostTouchCapture(TouchEventArgs e){base.OnLostTouchCapture(e);if(touch==e.TouchDevice)CancelSwipe();}
    protected override void OnPreviewKeyDown(KeyEventArgs e){if(e.Key==Key.Enter&&(Keyboard.Modifiers&ModifierKeys.Shift)!=0){ToggleRequested?.Invoke();e.Handled=true;}base.OnPreviewKeyDown(e);}
}
