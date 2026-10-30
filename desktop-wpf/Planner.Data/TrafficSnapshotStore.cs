using Microsoft.Data.Sqlite;
using Planner.Core;
using System.Text.Json;
namespace Planner.Data;
// Query snapshots are separate from itinerary reservations. A failed check never fabricates a route leg.
public sealed class TrafficSnapshotStore
{
    readonly string connectionString;
    public TrafficSnapshotStore(string directory){connectionString=new SqliteConnectionStringBuilder{DataSource=Path.Combine(directory,"planner.db"),Pooling=false,DefaultTimeout=5}.ToString();using var c=Open();using var cmd=c.CreateCommand();cmd.CommandText="CREATE TABLE IF NOT EXISTS traffic_check_snapshot (fingerprint TEXT PRIMARY KEY, payload TEXT NOT NULL)";cmd.ExecuteNonQuery();}
    SqliteConnection Open(){var c=new SqliteConnection(connectionString);c.Open();return c;}
    public Dictionary<string,TrafficSnapshot> Load(){using var c=Open();using var cmd=c.CreateCommand();cmd.CommandText="SELECT payload FROM traffic_check_snapshot";using var reader=cmd.ExecuteReader();var result=new Dictionary<string,TrafficSnapshot>();while(reader.Read()){var item=JsonSerializer.Deserialize<TrafficSnapshot>(reader.GetString(0),JsonKit.Options);if(item is not null&&item.Fresh(Clock.Now))result[item.Fingerprint]=item;}return result;}
    public void Save(IEnumerable<TrafficSnapshot> snapshots){using var c=Open();using var tx=c.BeginTransaction();foreach(var item in snapshots){if(item.Minutes<0)throw new ArgumentException("交通时间不能为负数。");using var cmd=c.CreateCommand();cmd.Transaction=tx;cmd.CommandText="INSERT INTO traffic_check_snapshot(fingerprint,payload) VALUES($id,$json) ON CONFLICT(fingerprint) DO UPDATE SET payload=excluded.payload";cmd.Parameters.AddWithValue("$id",item.Fingerprint);cmd.Parameters.AddWithValue("$json",JsonSerializer.Serialize(item,JsonKit.Options));cmd.ExecuteNonQuery();}tx.Commit();}
}
