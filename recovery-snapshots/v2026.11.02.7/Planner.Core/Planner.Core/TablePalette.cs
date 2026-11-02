using System.Collections.Generic;

namespace Planner.Core;

public static class TablePalette
{
	public static IReadOnlyList<TableColor> Colors { get; } = new TableColor[12]
	{
		new TableColor("陶土红", "#B66A58"),
		new TableColor("杏橙", "#BF8651"),
		new TableColor("麦芽金", "#9C853D"),
		new TableColor("橄榄绿", "#7F8C58"),
		new TableColor("鼠尾草", "#66856F"),
		new TableColor("松石绿", "#4D8C84"),
		new TableColor("湖蓝", "#587F9F"),
		new TableColor("雾蓝", "#747DA7"),
		new TableColor("淡紫", "#8B74A0"),
		new TableColor("玫瑰粉", "#AA738B"),
		new TableColor("可可棕", "#957765"),
		new TableColor("石灰", "#7C817F")
	};
}
