using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Planner.Desktop;
public static class Ui
{
    public static Brush Brush(string hex){try{return (Brush)new BrushConverter().ConvertFromString(hex)!;}catch{return Brushes.BurlyWood;}}
    public static TextBlock Text(string text,double size=13,bool bold=false)=>new(){Text=text,FontSize=size,FontWeight=bold?FontWeights.SemiBold:FontWeights.Normal,TextWrapping=TextWrapping.Wrap,Margin=new(0,3,0,7)};
    public static Button Button(string text,Action action,bool primary=false){var b=new Button{Content=text};if(primary){b.Background=Brush("#B76F49");b.Foreground=Brushes.White;}b.Click+=(_,_)=>Safe(action);return b;}
    public static void Safe(Action action){try{action();}catch(Exception ex){MessageBox.Show(ex.Message,"操作未完成",MessageBoxButton.OK,MessageBoxImage.Warning);}}
    public static bool Confirm(string text)=>MessageBox.Show(text,"请确认",MessageBoxButton.OKCancel,MessageBoxImage.Question)==MessageBoxResult.OK;
    public static bool ConfirmReview(string text)
    {
        var w=new Window{Title="核对变更与实际影响",Width=720,Height=680,WindowStartupLocation=WindowStartupLocation.CenterScreen};var p=new DockPanel{Margin=new(20)};bool accepted=false;
        var buttons=Row(Button("取消",w.Close),Button("确认写入",()=>{accepted=true;w.Close();},true));buttons.HorizontalAlignment=HorizontalAlignment.Right;DockPanel.SetDock(buttons,Dock.Bottom);p.Children.Add(buttons);p.Children.Add(new ScrollViewer{Content=Text(text,13),VerticalScrollBarVisibility=ScrollBarVisibility.Auto});w.Content=p;w.ShowDialog();return accepted;
    }
    public static TextBox Input(string value="")=>new(){Text=value};
    public static ComboBox Choices(IEnumerable<string> values,int selected=0){var c=new ComboBox();foreach(var s in values)c.Items.Add(s);c.SelectedIndex=selected;return c;}
    public static void Field(Panel panel,string title,Control input){panel.Children.Add(Text(title,12,true));panel.Children.Add(input);}
    public static StackPanel Row(params UIElement[] items){var p=new StackPanel{Orientation=Orientation.Horizontal};foreach(var i in items)p.Children.Add(i);return p;}
    public static Border Card(UIElement child,string color="#FFFDF9")=>new(){Background=Brush(color),CornerRadius=new(12),BorderBrush=Brush("#E5D8CA"),BorderThickness=new(1),Padding=new(14),Margin=new(0,0,0,12),Child=child};
}
