using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Planner.Core;

namespace Planner.Data;

public sealed class RepositoryMap(ServiceSettings settings, HttpClient? http = null, IMapProvider? provider = null)
{
	private readonly IMapProvider? online = provider ?? (string.IsNullOrEmpty(settings.MapServiceKeyProtected) ? null : new AmapClient(SettingsStore.Unprotect(settings.MapServiceKeyProtected), http));

	public static readonly IReadOnlyDictionary<string, (double Lng, double Lat)> Presets = new Dictionary<string, (double, double)>
	{
		["南京南站"] = (118.8022, 31.9745),
		["南京站"] = (118.7978, 32.0871),
		["新街口"] = (118.7857, 32.0417),
		["夫子庙"] = (118.7886, 32.0215),
		["南京博物院"] = (118.8484, 32.0448),
		["玄武湖"] = (118.803, 32.0733),
		["中山陵"] = (118.8529, 32.0617),
		["南京大学"] = (118.7792, 32.0572),
		["总统府"] = (118.8036, 32.0444),
		["南京图书馆"] = (118.797, 32.042)
	};

	public bool IsOnline => online != null;

	public async Task<List<Place>> SearchAsync(string query, CancellationToken token)
	{
		if (string.IsNullOrWhiteSpace(query))
		{
			throw new ArgumentException("请填写地点名称。");
		}
		if (online != null)
		{
			return await online.SearchPlacesAsync(query, token);
		}
		List<Place> list = (from p in Presets
			where p.Key.Contains(query) || query.Contains(p.Key)
			select new Place
			{
				InputName = query,
				Name = p.Key,
				Lng = p.Value.Lng,
				Lat = p.Value.Lat,
				Status = "UNVERIFIED",
				Source = "本地示例坐标，未核实入口"
			}).ToList();
		if (list.Count == 0)
		{
			throw new ArgumentException("未配置高德Key，本地模式仅支持预置南京地点；可先手动填写不含交通的活动，或配置高德后搜索。");
		}
		return list;
	}

	public async Task<TravelEstimate> TravelAsync(Place from, Place to, string mode, DateTime depart, CancellationToken token)
	{
		if (from.Id == to.Id || (from.Lng.HasValue && from.Lng == to.Lng && from.Lat == to.Lat))
		{
			return new TravelEstimate(0, "同一地点");
		}
		if (!from.Lng.HasValue || !from.Lat.HasValue || !to.Lng.HasValue || !to.Lat.HasValue)
		{
			throw new ArgumentException("相邻活动缺少坐标，请先搜索并选择地点。");
		}
		if (online != null)
		{
			IMapProvider mapProvider = online;
			List<RouteOption> list = await mapProvider.RoutesAsync(from, to, mode switch
			{
				"公交" => "TRANSIT", 
				"骑行" => "BIKE", 
				"驾车" => "CAR", 
				_ => "WALK", 
			}, depart, token);
			if (list.Count == 0)
			{
				throw new InvalidOperationException("地图未找到可用路线。");
			}
			return new TravelEstimate((int)Math.Ceiling((double)list[0].DurationSec / 60.0), mode + " · 高德查询快照")
			{
				Option = JsonKit.Clone(list[0])
			};
		}
		double d = (from.Lat.Value + to.Lat.Value) / 2.0 * Math.PI / 180.0;
		double num = Math.Sqrt(Math.Pow((from.Lng.Value - to.Lng.Value) * 111.0 * Math.Cos(d), 2.0) + Math.Pow((from.Lat.Value - to.Lat.Value) * 111.0, 2.0));
		double num2 = mode switch
		{
			"驾车" => 25, 
			"公交" => 18, 
			"骑行" => 12, 
			_ => 4, 
		};
		return new TravelEstimate(Math.Max(1, (int)Math.Ceiling(num * 1.5 / num2 * 60.0) + ((mode == "公交") ? 10 : 0)), mode + " · 直线距离粗估（未联网核验）");
	}
}
