namespace Planner.Core;
public sealed record TableColor(string Name,string Hex);
public static class TablePalette
{
    public static IReadOnlyList<TableColor> Colors {get;}=new TableColor[]{new("陶土红","#B66A58"),new("杏橙","#BF8651"),new("麦芽金","#9C853D"),new("橄榄绿","#7F8C58"),new("鼠尾草","#66856F"),new("松石绿","#4D8C84"),new("湖蓝","#587F9F"),new("雾蓝","#747DA7"),new("淡紫","#8B74A0"),new("玫瑰粉","#AA738B"),new("可可棕","#957765"),new("石灰","#7C817F")};
}
