using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Planner.Core;
namespace Planner.Desktop;
public static class ShellIcons
{
    public const string Menu="\uE700",Board="\uE787",Tables="\uE8F1",Map="\uE707",Assistant="\uE99A",Profile="\uE77B",Settings="\uE713";
    public static TextBlock Glyph(string value,double size=20)=>new(){Text=value,FontFamily=new("Segoe MDL2 Assets"),FontSize=size,VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center};
    public static Button Button(string glyph,string label,Action click,string tag,bool expanded=false)
    {
        var row=new StackPanel{Orientation=Orientation.Horizontal};row.Children.Add(Glyph(glyph));if(expanded)row.Children.Add(new TextBlock{Text=label,Margin=new(12,0,0,0),VerticalAlignment=VerticalAlignment.Center,TextWrapping=TextWrapping.Wrap,MaxWidth=126});var b=Ui.Button("",click);b.Content=row;b.Tag=tag;b.ToolTip=label;b.MinHeight=44;b.Padding=new(12,10,12,10);b.HorizontalContentAlignment=HorizontalAlignment.Left;AutomationProperties.SetName(b,label);return b;
    }
}
public sealed class TableColorPicker:WrapPanel
{
    public string? SelectedColor {get;private set;}
    public event Action? SelectionChanged;
    public TableColorPicker(string? selected=null){SelectedColor=selected;Render();}
    void Render()
    {
        Children.Clear();var colors=TablePalette.Colors.ToList();if(SelectedColor is {} custom&&!colors.Any(c=>c.Hex.Equals(custom,StringComparison.OrdinalIgnoreCase)))colors.Add(new("原有自定义颜色",custom));
        foreach(var c in colors){var circle=new Ellipse{Width=24,Height=24,Fill=Ui.Brush(c.Hex)};var grid=new Grid();grid.Children.Add(circle);bool active=string.Equals(c.Hex,SelectedColor,StringComparison.OrdinalIgnoreCase);if(active)grid.Children.Add(new TextBlock{Text="✓",Foreground=Brushes.White,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center});var b=Ui.Button("",()=>{SelectedColor=c.Hex;Render();SelectionChanged?.Invoke();});b.Content=grid;b.Tag="TableColor:"+c.Hex;b.ToolTip=c.Name;b.Width=44;b.Height=42;b.Margin=new(2);b.Padding=new(6);b.Background=active?Ui.Brush("#E7D7C7"):Brushes.Transparent;AutomationProperties.SetName(b,c.Name);Children.Add(b);}
    }
}
