using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Planner.Core;

public static class TabClosing
{
	public static List<string> Targets(IReadOnlyList<string> open, string anchor, string mode)
	{
		bool flag;
		switch (mode)
		{
		case "CURRENT":
		case "OTHERS":
		case "RIGHT":
		case "ALL":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (!flag)
		{
			throw new ArgumentException("未知标签关闭方式。");
		}
		if (mode == "ALL")
		{
			return open.ToList();
		}
		int num = open.ToList().IndexOf(anchor);
		if (num < 0)
		{
			throw new ArgumentException("标签已关闭。");
		}
		if (!(mode == "CURRENT"))
		{
			if (mode == "OTHERS")
			{
				return open.Where((string id) => id != anchor).ToList();
			}
			return open.Skip(num + 1).ToList();
		}
		int num2 = 1;
		List<string> list = new List<string>(num2);
		CollectionsMarshal.SetCount(list, num2);
		CollectionsMarshal.AsSpan(list)[0] = anchor;
		return list;
	}
}
