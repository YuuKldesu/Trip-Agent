using System.Globalization;
using System.Windows;
using System.Windows.Controls;
namespace Planner.Desktop;
public static class Typography
{
    static readonly HashSet<double> sizes=[];static double current=14;
    static string Key(double nominal)=>"Planner.Font."+nominal.ToString(CultureInfo.InvariantCulture);
    public static void Set(FrameworkElement target,double nominal)
    {
        sizes.Add(nominal);if(Application.Current is {} app)app.Resources[Key(nominal)]=nominal*current/13;
        target.SetResourceReference(target is TextBlock?TextBlock.FontSizeProperty:Control.FontSizeProperty,Key(nominal));
    }
    public static void Apply(double value)
    {
        current=value;sizes.Add(13);if(Application.Current is {} app)foreach(var size in sizes)app.Resources[Key(size)]=size*current/13;
    }
}
