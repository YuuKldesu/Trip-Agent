using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Planner.Data;

public static class PlaceQueries
{
	public static List<string> FromMessage(string text)
	{
		List<string> list = new List<string>();
		foreach (Match item in Regex.Matches(text, "(?:在|去|到|搜索|查找|查询)\\s*[“\"‘']?(?<place>[\\p{IsCJKUnifiedIdeographs}A-Za-z0-9·（）()· -]{2,50}?)(?=参观|游玩|开会|学习|吃饭|待[一二两三四五六七八九十0-9]|停留|约|[，。！？、,;；“”‘’\"']|\\d+(?:小时|分钟)|$)", RegexOptions.CultureInvariant))
		{
			string text2 = item.Groups["place"].Value.Trim();
			int length = text2.Length;
			bool flag = length >= 2 && length <= 50;
			if (flag)
			{
				bool flag2;
				switch (text2)
				{
				case "明天":
				case "这里":
				case "那里":
				case "今天":
				case "南京":
				case "哪儿":
				case "哪里":
					flag2 = true;
					break;
				default:
					flag2 = false;
					break;
				}
				flag = !flag2;
			}
			if (flag && !text2.Contains("怎么") && !text2.Contains("多久"))
			{
				list.Add(text2);
			}
		}
		foreach (string name in RepositoryMap.Presets.Keys.OrderByDescending((string x) => x.Length))
		{
			if (text.Contains(name, StringComparison.Ordinal) && !list.Any((string n) => n.Contains(name, StringComparison.Ordinal)))
			{
				list.Add(name);
			}
		}
		return list.Distinct(StringComparer.Ordinal).Take(2).ToList();
	}
}
