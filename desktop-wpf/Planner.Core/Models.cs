using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;

namespace Planner.Core;

public static class Clock
{
    public static DateTime Now => DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)).DateTime;
    public static DateOnly Today => DateOnly.FromDateTime(Now);
    public static DateOnly Monday(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
}
public static class JsonKit
{
    public static readonly JsonSerializerOptions Options = Create();
    static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, PropertyNameCaseInsensitive = true, WriteIndented = true };
        options.Converters.Add(new ChinaDateConverter());
        return options;
    }
    public static T Clone<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, Options), Options)!;
    sealed class ChinaDateConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            var text = reader.GetString()!;
            if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                return d.ToOffset(TimeSpan.FromHours(8)).DateTime;
            throw new JsonException("日期时间格式错误。");
        }
        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture) + "+08:00");
    }
}
public abstract class Entity { public string Id { get; set; } = Guid.NewGuid().ToString("N"); }
public sealed class ScheduleTable : Entity
{
    public string Name { get; set; } = "我的行程";
    public string Color { get; set; } = "#D5A078";
    public bool Enabled { get; set; } = true;
    public DateOnly? ValidFrom { get; set; }
    public DateOnly? ValidUntil { get; set; }
    public int Version { get; set; } = 1;
    public DateTime UpdatedAt { get; set; } = Clock.Now;
    public bool Active(DateOnly date) => Enabled && (ValidFrom is null || date >= ValidFrom) && (ValidUntil is null || date <= ValidUntil);
}
public sealed class Place : Entity
{
    public string InputName { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Address { get; set; }
    public double? Lng { get; set; }
    public double? Lat { get; set; }
    public string? CoordType { get; set; }
    public string? ProviderId { get; set; }
    public string? Adcode { get; set; }
    public string Status { get; set; } = "UNVERIFIED";
    public string? Source { get; set; }
    public override string ToString() => Name + (string.IsNullOrWhiteSpace(Address) ? "" : " · " + Address);
}
public sealed class Activity : Entity
{
    public string TableId { get; set; } = "";
    public string Title { get; set; } = "";
    public string? PlaceId { get; set; }
    public string Kind { get; set; } = "ONCE";
    public string ConstraintType { get; set; } = "FIXED";
    public DateTime? StartAt { get; set; }
    public DateTime? EndAt { get; set; }
    public int DurationMin { get; set; } = 60;
    public DateTime? WindowStart { get; set; }
    public DateTime? WindowEnd { get; set; }
    public string? Note { get; set; }
    public int Version { get; set; } = 1;
    public DateTime UpdatedAt { get; set; } = Clock.Now;
}
public sealed class RecurrenceRule
{
    public string ActivityId { get; set; } = "";
    public DateOnly ValidFrom { get; set; } = Clock.Today;
    public DateOnly? ValidUntil { get; set; }
    public int WeekMask { get; set; } = 31;
    public string StartTime { get; set; } = "09:00";
    public string? WindowFrom { get; set; }
    public int? WindowSpanMin { get; set; }
    public bool Matches(DateOnly date) => date >= ValidFrom && (ValidUntil is null || date <= ValidUntil) && (WeekMask & (1 << (((int)date.DayOfWeek + 6) % 7))) != 0;
}
public sealed class ActivityException : Entity
{
    public string ActivityId { get; set; } = "";
    public DateOnly OriginDate { get; set; }
    public string Kind { get; set; } = "OVERRIDE";
    public DateTime? StartAt { get; set; }
    public DateTime? EndAt { get; set; }
    public string? Title { get; set; }
    public string? PlaceId { get; set; }
    public bool PlaceOverride { get; set; }
    public int Version { get; set; } = 1;
    public DateTime UpdatedAt { get; set; } = Clock.Now;
}
public sealed record Occurrence(Activity Activity, DateOnly? OriginDate, string Title, string? PlaceId, DateTime Start, DateTime End, int ExceptionVersion = 0)
{
    public string Key => Activity.Id + ":" + (OriginDate?.ToString("yyyy-MM-dd") ?? "once");
    public string VersionKey => Activity.Version + ":" + ExceptionVersion;
}
public sealed record Conflict(string Kind, string FirstKey, string? SecondKey, string Message);
public sealed class Plan : Entity
{
    public DateOnly PlanDate { get; set; }
    public int Version { get; set; } = 1;
    public string Status { get; set; } = "CONFIRMED";
    public string? StartPlaceId { get; set; }
    public DateTime? EarliestDepart { get; set; }
    public string? ResourceJson { get; set; }
    public DateTime ConfirmedAt { get; set; } = Clock.Now;
}
public sealed class PlanScope
{
    public string PlanId { get; set; } = "";
    public string TableId { get; set; } = "";
    public string Role { get; set; } = "CONSTRAINT";
    public int TableVersion { get; set; }
}
public sealed class PlanLeg : Entity
{
    public int? KnownCostFen {get;set;}
    public bool CostComplete {get;set;}
    public string CostNote {get;set;}="费用快照暂无数据";
    public string PlanId { get; set; } = "";
    public int Seq { get; set; }
    public string? FromActivityId { get; set; }
    public DateOnly? FromOriginDate { get; set; }
    public string ToActivityId { get; set; } = "";
    public DateOnly? ToOriginDate { get; set; }
    public DateTime DepartAt { get; set; }
    public DateTime ArriveAt { get; set; }
    public int BufferSec { get; set; }
    public DateTime QueryAt { get; set; }
    public DateTime? RequestedDepartAt { get; set; }
    public string ValidationStatus { get; set; } = "UNVERIFIED";
}
public sealed class RouteSegment : Entity
{
    public string LegId { get; set; } = "";
    public int Seq { get; set; }
    public string Mode { get; set; } = "WALK";
    public string? ResourceType { get; set; }
    public string? Instruction { get; set; }
    public int DurationSec { get; set; }
    public int? DistanceM { get; set; }
    public int? KnownCostFen { get; set; }
    public string? CostDetailJson { get; set; }
    public string? Congestion { get; set; }
    public string? Geometry { get; set; }
    public string Provider { get; set; } = "高德";
}
public sealed class ReminderSetting
{
    public string ActivityId { get; set; } = "";
    public bool Enabled { get; set; }
    public int LeadMin { get; set; } = 15;
    public string Basis { get; set; } = "DEPARTURE";
}
public sealed class ReminderDelivery : Entity
{
    public string ActivityId { get; set; } = "";
    public string InstanceKey { get; set; } = "";
    public DateTime DueAt { get; set; }
    public string Status { get; set; } = "NOTIFIED";
    public DateTime RecordedAt { get; set; } = Clock.Now;
}
public sealed class PlannerState
{
    public long Revision { get; set; }
    public List<ScheduleTable> Tables { get; set; } = [];
    public List<Place> Places { get; set; } = [];
    public List<Activity> Activities { get; set; } = [];
    public List<RecurrenceRule> Rules { get; set; } = [];
    public List<ActivityException> Exceptions { get; set; } = [];
    public List<Plan> Plans { get; set; } = [];
    public List<PlanScope> Scopes { get; set; } = [];
    public List<PlanLeg> Legs { get; set; } = [];
    public List<RouteSegment> Segments { get; set; } = [];
    public List<ReminderSetting> Reminders { get; set; } = [];
    public List<ReminderDelivery> Deliveries { get; set; } = [];
    public PlannerState Copy() => JsonKit.Clone(this);
    public string PlaceName(string? id) => Places.FirstOrDefault(p => p.Id == id)?.Name ?? "未填地点";
}
