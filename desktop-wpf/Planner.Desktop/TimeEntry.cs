using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
namespace Planner.Desktop;
public sealed class TimeEntry:UserControl
{
    readonly TextBox input;readonly Popup popup;
    public string Text {get=>input.Text;set=>input.Text=value;}
    public event Action? Changed;
    public TimeEntry(string value)
    {
        var grid=new Grid();grid.ColumnDefinitions.Add(new());grid.ColumnDefinitions.Add(new(){Width=GridLength.Auto});Content=grid;
        input=Ui.Input(value);grid.Children.Add(input);input.TextChanged+=(_,_)=>Changed?.Invoke();
        var open=Ui.Button("⌄",()=>{});open.Padding=new(8,4,8,4);Grid.SetColumn(open,1);grid.Children.Add(open);
        var items=new WrapPanel{Width=252};for(int m=0;m<24*60;m+=30){int minute=m;var pick=Ui.Button($"{m/60:00}:{m%60:00}",()=>Text=$"{minute/60:00}:{minute%60:00}");pick.Width=76;pick.Margin=new(2);items.Children.Add(pick);}
        popup=new(){PlacementTarget=this,Placement=PlacementMode.Bottom,StaysOpen=false,AllowsTransparency=true,Child=new Border{Background=Ui.Brush("#FFFDF9"),BorderBrush=Ui.Brush("#E5D8CA"),BorderThickness=new(1),Padding=new(6),Child=new ScrollViewer{Height=230,Content=items,VerticalScrollBarVisibility=ScrollBarVisibility.Auto}}};
        foreach(Button b in items.Children)b.Click+=(_,_)=>popup.IsOpen=false;
        open.Click+=(_,_)=>popup.IsOpen=!popup.IsOpen;PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape&&popup.IsOpen){popup.IsOpen=false;e.Handled=true;}};Unloaded+=(_,_)=>popup.IsOpen=false;
    }
}
