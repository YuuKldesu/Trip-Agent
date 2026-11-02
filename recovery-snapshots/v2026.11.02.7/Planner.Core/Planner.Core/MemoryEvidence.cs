using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Planner.Core;

internal static class MemoryEvidence
{
	public static bool Matches(MemoryUpdate update, ConversationMessage source)
	{
		string text = update.Evidence.Trim();
		string text2 = update.Value.Trim();
		string text3 = update.Key.ToLowerInvariant();
		if (text2 == text)
		{
			return true;
		}
		if (text3.Contains("duration") || text3.Contains("耗时"))
		{
			Match match = Regex.Match(text, "^(\\d+(?:\\.\\d+)?)\\s*(分钟|小时|个小时)$");
			if (match.Success && decimal.TryParse(match.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result))
			{
				return text2 == (result * (decimal)((match.Groups[2].Value == "分钟") ? 1 : 60)).ToString("0.################", CultureInfo.InvariantCulture);
			}
			if ((text == "半小时" || text == "半个小时") ? true : false)
			{
				return text2 == "30";
			}
		}
		if (text3.Contains("date") || text3.EndsWith("day") || text3.Contains("日期"))
		{
			DateOnly value = DateOnly.FromDateTime(source.CreatedAt);
			DateOnly? dateOnly = text switch
			{
				"今天" => (DateOnly?)value, 
				"明天" => value.AddDays(1), 
				"后天" => value.AddDays(2), 
				_ => null, 
			};
			string[] array = new string[3] { "yyyy-MM-dd", "yyyy/M/d", "yyyy年M月d日" };
			foreach (string format in array)
			{
				if (DateOnly.TryParseExact(text, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result2))
				{
					dateOnly = result2;
				}
			}
			if (dateOnly.HasValue)
			{
				return text2 == dateOnly.GetValueOrDefault().ToString("yyyy-MM-dd");
			}
			return false;
		}
		if ((text3.EndsWith("start") || text3.EndsWith("end") || text3.Contains("earliest")) && TimeOnly.TryParseExact(text, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result3))
		{
			return text2 == (result3.Hour * 60 + result3.Minute).ToString(CultureInfo.InvariantCulture);
		}
		return false;
	}
}
