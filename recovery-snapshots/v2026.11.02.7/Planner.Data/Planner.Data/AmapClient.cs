using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Planner.Core;

namespace Planner.Data;

public sealed class AmapClient(string key, HttpClient? http = null) : IMapProvider
{
	private readonly HttpClient client = http ?? new HttpClient
	{
		Timeout = TimeSpan.FromSeconds(30L)
	};

	private IReadOnlyList<IReadOnlyList<GeoPoint>>? boundaries;

	private async Task<string> Get(string path, Dictionary<string, string> args, CancellationToken token)
	{
		if (string.IsNullOrWhiteSpace(key))
		{
			throw new ArgumentException("请在服务设置中填写高德Web服务Key。");
		}
		args["key"] = key;
		string requestUri = "https://restapi.amap.com/" + path + "?" + string.Join("&", args.Select((KeyValuePair<string, string> x) => Uri.EscapeDataString(x.Key) + "=" + Uri.EscapeDataString(x.Value)));
		try
		{
			using HttpResponseMessage response = await client.GetAsync(requestUri, token);
			if (!response.IsSuccessStatusCode)
			{
				throw new MapServiceException($"高德服务请求失败：HTTP {(int)response.StatusCode}。");
			}
			string text = await response.Content.ReadAsStringAsync(token);
			if (text.Length > 8000000)
			{
				throw new InvalidOperationException("地图响应超出大小限制。");
			}
			using JsonDocument jsonDocument = JsonDocument.Parse(text);
			JsonElement rootElement = jsonDocument.RootElement;
			if (S(rootElement, "status") != "1")
			{
				throw MapServiceException.Rejected(S(rootElement, "infocode"));
			}
			return text;
		}
		catch (OperationCanceledException) when (!token.IsCancellationRequested)
		{
			throw MapServiceException.Timeout();
		}
		catch (HttpRequestException error)
		{
			throw MapServiceException.Network(error);
		}
		catch (JsonException)
		{
			throw new MapServiceException("高德响应异常：数据格式无效。");
		}
	}

	private async Task<IReadOnlyList<IReadOnlyList<GeoPoint>>> Boundary(CancellationToken token)
	{
		if (boundaries != null)
		{
			return boundaries;
		}
		using JsonDocument jsonDocument = JsonDocument.Parse(await Get("v3/config/district", new Dictionary<string, string>
		{
			["keywords"] = "320100",
			["subdistrict"] = "0",
			["extensions"] = "all"
		}, token));
		List<IReadOnlyList<GeoPoint>> list = (from x in ((IEnumerable<string>)S(A(jsonDocument.RootElement, "districts").FirstOrDefault(), "polyline").Split('|', StringSplitOptions.RemoveEmptyEntries)).Select((Func<string, IReadOnlyList<GeoPoint>>)((string x) => Points(x)))
			where x.Count >= 3
			select x).ToList();
		if (list.Count == 0)
		{
			throw new MapServiceException("高德行政边界查询失败：未返回南京边界。");
		}
		return boundaries = list;
	}

	public async Task<List<Place>> SearchPlacesAsync(string text, CancellationToken token)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			throw new ArgumentException("请输入地点名称。");
		}
		token.ThrowIfCancellationRequested();
		using JsonDocument jsonDocument = JsonDocument.Parse(await Get("v3/place/text", new Dictionary<string, string>
		{
			["keywords"] = text,
			["city"] = "南京",
			["citylimit"] = "true",
			["offset"] = "25",
			["page"] = "1",
			["extensions"] = "base"
		}, token));
		List<Place> list = new List<Place>();
		if (P(jsonDocument.RootElement, "pois").ValueKind != JsonValueKind.Array)
		{
			throw new MapServiceException("高德响应异常：缺少地点列表。");
		}
		List<JsonElement> list2 = A(jsonDocument.RootElement, "pois").ToList();
		foreach (JsonElement item in list2)
		{
			List<GeoPoint> list3 = Points(S(item, "location"));
			string text2 = S(item, "adcode");
			if (((text2.Length > 0) ? text2.StartsWith("3201", StringComparison.Ordinal) : (S(item, "cityname") == "南京市")) && list3.Count == 1 && !string.IsNullOrWhiteSpace(S(item, "name")))
			{
				GeoPoint geoPoint = list3[0];
				double lng = geoPoint.Lng;
				bool flag = ((lng < -180.0 || lng > 180.0) ? true : false);
				bool flag2 = flag;
				if (!flag2)
				{
					double lat = geoPoint.Lat;
					bool flag3 = ((lat < -90.0 || lat > 90.0) ? true : false);
					flag2 = flag3;
				}
				if (!flag2 && !(geoPoint == default(GeoPoint)))
				{
					list.Add(new Place
					{
						InputName = text,
						Name = S(item, "name"),
						Address = S(item, "address"),
						Lng = geoPoint.Lng,
						Lat = geoPoint.Lat,
						CoordType = "GCJ02",
						ProviderId = S(item, "id"),
						Adcode = S(item, "adcode"),
						Status = "VERIFIED",
						Source = "高德地点搜索"
					});
				}
			}
		}
		token.ThrowIfCancellationRequested();
		if (list2.Count > 0 && list.Count == 0)
		{
			throw new MapServiceException("高德地点结果不可用：地点不在南京市内或缺少有效坐标。");
		}
		return list;
	}

	public async Task<List<RouteOption>> RoutesAsync(Place from, Place to, string mode, DateTime depart, CancellationToken token)
	{
		IReadOnlyList<IReadOnlyList<GeoPoint>> bounds = await Boundary(token);
		if (!from.Lng.HasValue || !from.Lat.HasValue || !to.Lng.HasValue || !to.Lat.HasValue)
		{
			throw new ArgumentException("地点尚未确认坐标。");
		}
		if (!bounds.Any((IReadOnlyList<GeoPoint> x) => GeoBoundary.Contains(x, new GeoPoint(from.Lng.Value, from.Lat.Value))) || !bounds.Any((IReadOnlyList<GeoPoint> x) => GeoBoundary.Contains(x, new GeoPoint(to.Lng.Value, to.Lat.Value))))
		{
			throw new ArgumentException("路线起终点必须在南京市内。");
		}
		Dictionary<string, string> dictionary = new Dictionary<string, string>
		{
			["origin"] = Loc(from),
			["destination"] = Loc(to),
			["show_fields"] = "cost,polyline"
		};
		string text;
		switch (mode)
		{
		case "CAR":
		case "TAXI":
			text = "driving";
			break;
		case "BIKE":
			text = "bicycling";
			break;
		case "TRANSIT":
		case "BUS":
		case "METRO":
			text = "transit/integrated";
			break;
		default:
			text = "walking";
			break;
		}
		string text2 = text;
		if (text2 == "driving")
		{
			dictionary["show_fields"] = "cost,polyline,tmcs";
		}
		if (text2 == "transit/integrated")
		{
			dictionary["city1"] = "025";
			dictionary["city2"] = "025";
			dictionary["ad1"] = "320100";
			dictionary["ad2"] = "320100";
			dictionary["strategy"] = ((mode == "METRO") ? "7" : "0");
			dictionary["date"] = depart.ToString("yyyy-MM-dd");
			dictionary["time"] = depart.ToString("HH-mm");
		}
		return ParseRoutes(await Get("v5/direction/" + text2, dictionary, token).ConfigureAwait(continueOnCapturedContext: false), mode, bounds);
		static string Loc(Place p)
		{
			return p.Lng.Value.ToString("0.######", CultureInfo.InvariantCulture) + "," + p.Lat.Value.ToString("0.######", CultureInfo.InvariantCulture);
		}
	}

	public static List<RouteOption> ParseRoutes(string json, string mode, IReadOnlyList<IReadOnlyList<GeoPoint>> boundaries)
	{
		using (JsonDocument jsonDocument = JsonDocument.Parse(json))
		{
			JsonElement rootElement = jsonDocument.RootElement;
			if (S(rootElement, "status") != "1")
			{
				return new List<RouteOption>();
			}
			JsonElement jsonElement = P(rootElement, "route");
			List<RouteOption> list = new List<RouteOption>();
			JsonElement e = jsonElement;
			bool flag;
			switch (mode)
			{
			case "TRANSIT":
			case "BUS":
			case "METRO":
				flag = true;
				break;
			default:
				flag = false;
				break;
			}
			foreach (JsonElement item in A(e, flag ? "transits" : "paths"))
			{
				JsonElement e2 = P(item, "cost");
				int num = N(e2, "duration") ?? N(item, "duration").GetValueOrDefault();
				if (num <= 0)
				{
					continue;
				}
				RouteOption routeOption = new RouteOption
				{
					DurationSec = num,
					DistanceM = N(item, "distance").GetValueOrDefault()
				};
				switch (mode)
				{
				case "TRANSIT":
				case "BUS":
				case "METRO":
					flag = true;
					break;
				default:
					flag = false;
					break;
				}
				if (flag)
				{
					bool flag2 = false;
					foreach (JsonElement item2 in A(item, "segments"))
					{
						JsonElement jsonElement2 = P(item2, "railway");
						if (jsonElement2.ValueKind == JsonValueKind.Object && jsonElement2.EnumerateObject().Any())
						{
							flag2 = true;
							break;
						}
						JsonElement e3 = P(item2, "walking");
						List<JsonElement> list2 = A(e3, "steps").ToList();
						if (list2.Count > 0)
						{
							routeOption.Segments.Add(new RouteSegment
							{
								Mode = "WALK",
								Instruction = string.Join("；", list2.Select((JsonElement x) => S(x, "instruction"))),
								Geometry = string.Join(";", list2.Select((JsonElement x) => S(x, "polyline"))),
								DistanceM = N(e3, "distance"),
								DurationSec = (N(P(e3, "cost"), "duration") ?? N(e3, "duration").GetValueOrDefault()),
								KnownCostFen = 0
							});
						}
						JsonElement e4 = A(P(item2, "bus"), "buslines").FirstOrDefault();
						if (e4.ValueKind == JsonValueKind.Object)
						{
							string type = S(e4, "type");
							string name = S(e4, "name");
							if (new string[5] { "火车", "铁路", "城际", "长途", "航空" }.Any((string x) => (type + name).Contains(x)))
							{
								flag2 = true;
								break;
							}
							string mode2 = ((type.Contains("地铁") || type.Contains("轻轨") || name.Contains("地铁")) ? "METRO" : "BUS");
							routeOption.Segments.Add(new RouteSegment
							{
								Mode = mode2,
								Instruction = name + "：" + S(P(e4, "departure_stop"), "name") + " → " + S(P(e4, "arrival_stop"), "name"),
								Geometry = S(e4, "polyline"),
								DurationSec = (N(P(e4, "cost"), "duration") ?? N(e4, "duration").GetValueOrDefault()),
								DistanceM = N(e4, "distance")
							});
						}
						JsonElement jsonElement3 = P(item2, "taxi");
						if (jsonElement3.ValueKind == JsonValueKind.Object && jsonElement3.EnumerateObject().Any())
						{
							flag2 = true;
						}
					}
					if (flag2 || routeOption.Segments.Count == 0)
					{
						continue;
					}
					int? num2 = (routeOption.KnownCostFen = Money(e2, "transit_fee") ?? Money(item, "cost"));
					routeOption.CostComplete = num2.HasValue;
					routeOption.CostNote = (num2.HasValue ? "公共交通票价参考" : "票价暂未返回");
					routeOption.Label = string.Join(" + ", routeOption.Segments.Select((RouteSegment x) => Name(x.Mode)).Distinct());
				}
				else
				{
					List<JsonElement> source = A(item, "steps").ToList();
					string geometry = string.Join(";", source.Select((JsonElement x) => S(x, "polyline")));
					string congestion = null;
					if ((mode == "CAR" || mode == "TAXI") ? true : false)
					{
						string[] array = (from x in source.SelectMany((JsonElement x) => A(x, "tmcs"))
							select S(x, "status") into x
							where x.Length > 0 && x != "未知"
							select x).Distinct().ToArray();
						if (array.Length != 0)
						{
							congestion = string.Join(" / ", array);
						}
					}
					int? knownCostFen = (routeOption.KnownCostFen = mode switch
					{
						"WALK" => 0, 
						"CAR" => Money(e2, "tolls"), 
						"TAXI" => Money(e2, "taxi") ?? Money(P(jsonElement, "cost"), "taxi_fee") ?? Money(jsonElement, "taxi_cost"), 
						_ => null, 
					});
					routeOption.CostComplete = mode == "WALK";
					RouteOption routeOption2 = routeOption;
					routeOption2.CostNote = mode switch
					{
						"CAR" => "仅含已返回过路费；油费、停车费未知", 
						"TAXI" => "车费参考；等候及其他附加费未知", 
						"BIKE" => "共享骑行费用及可用车辆未知", 
						_ => "步行交通费为零", 
					};
					routeOption.Label = Name(mode);
					routeOption.Segments.Add(new RouteSegment
					{
						Mode = mode,
						Instruction = string.Join("；", source.Select((JsonElement x) => S(x, "instruction"))),
						Geometry = geometry,
						DurationSec = num,
						DistanceM = routeOption.DistanceM,
						KnownCostFen = knownCostFen,
						Congestion = congestion,
						CostDetailJson = JsonSerializer.Serialize(new
						{
							note = routeOption.CostNote
						}, JsonKit.Options)
					});
				}
				if (!routeOption.Segments.Any((RouteSegment x) => !ValidGeometry(x.Geometry ?? "")) && (!(mode == "METRO") || !routeOption.Uses("BUS")) && (!(mode == "BUS") || !routeOption.Uses("METRO")))
				{
					routeOption.Label += $" · 方案{list.Count + 1}";
					list.Add(routeOption);
				}
			}
			return list;
		}
		bool ValidGeometry(string text)
		{
			List<GeoPoint> list3 = Points(text);
			if (list3.Count < 2)
			{
				return false;
			}
			for (int i = 1; i < list3.Count; i++)
			{
				if (!GeoBoundary.CoversSegment(boundaries, list3[i - 1], list3[i]))
				{
					return false;
				}
			}
			return true;
		}
	}

	public static string Name(string mode)
	{
		return mode switch
		{
			"WALK" => "步行", 
			"BIKE" => "骑行", 
			"BUS" => "公交", 
			"METRO" => "地铁", 
			"CAR" => "自驾", 
			"TAXI" => "打车", 
			_ => mode, 
		};
	}

	private static JsonElement P(JsonElement e, string key)
	{
		if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(key, out var value))
		{
			return default;
		}
		return value;
	}

	private static string S(JsonElement e, string key)
	{
		JsonElement jsonElement = P(e, key);
		JsonValueKind valueKind = jsonElement.ValueKind;
		if (valueKind - 3 > JsonValueKind.Object)
		{
			return "";
		}
		return jsonElement.ToString();
	}

	private static IEnumerable<JsonElement> A(JsonElement e, string key)
	{
		JsonElement jsonElement = P(e, key);
		return (jsonElement.ValueKind == JsonValueKind.Array) ? jsonElement.EnumerateArray() : default(JsonElement.ArrayEnumerator);
	}

	private static int? N(JsonElement e, string key)
	{
		if (!double.TryParse(S(e, key), NumberStyles.Float, CultureInfo.InvariantCulture, out var result) || !double.IsFinite(result) || !(result >= 0.0) || !(result < 2147483647.0))
		{
			return null;
		}
		return (int)Math.Ceiling(result);
	}

	private static int? Money(JsonElement e, string key)
	{
		if (!decimal.TryParse(S(e, key), NumberStyles.Number, CultureInfo.InvariantCulture, out var result) || !(result >= 0m) || !(result < 21474836m))
		{
			return null;
		}
		return (int)Math.Round(result * 100m);
	}

	private static List<GeoPoint> Points(string text)
	{
		List<GeoPoint> list = new List<GeoPoint>();
		string[] array = text.Split(';', StringSplitOptions.RemoveEmptyEntries);
		for (int i = 0; i < array.Length; i++)
		{
			string[] array2 = array[i].Split(',');
			if (array2.Length == 2 && double.TryParse(array2[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var result) && double.TryParse(array2[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var result2) && double.IsFinite(result) && double.IsFinite(result2))
			{
				list.Add(new GeoPoint(result, result2));
			}
		}
		return list;
	}
}
