using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Planner.Core;

public static class JsonKit
{
	private sealed class ChinaDateConverter : JsonConverter<DateTime>
	{
		public override DateTime Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
		{
			if (DateTimeOffset.TryParse(reader.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
			{
				return result.ToOffset(TimeSpan.FromHours(8)).DateTime;
			}
			throw new JsonException("日期时间格式错误。");
		}

		public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
		{
			writer.WriteStringValue(value.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture) + "+08:00");
		}
	}

	public static readonly JsonSerializerOptions Options = Create();

	private static JsonSerializerOptions Create()
	{
		return new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
			PropertyNameCaseInsensitive = true,
			WriteIndented = true,
			Converters = { (JsonConverter)new ChinaDateConverter() }
		};
	}

	public static T Clone<T>(T value)
	{
		return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, Options), Options);
	}
}
