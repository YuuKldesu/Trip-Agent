using Planner.Core;
using Microsoft.Data.Sqlite;
using System.Collections;
using System.Text.Json;
namespace Planner.Data;
public sealed class SqliteStore
{
    readonly string connectionString;
    sealed record Binding(string Table,Type Type,Func<PlannerState,IList> Items);
    static readonly Binding[] Bindings = [
        new("schedule_table",typeof(ScheduleTable),s=>s.Tables),new("place",typeof(Place),s=>s.Places),new("activity",typeof(Activity),s=>s.Activities),
        new("recurrence_rule",typeof(RecurrenceRule),s=>s.Rules),new("activity_exception",typeof(ActivityException),s=>s.Exceptions),
        new("plan",typeof(Plan),s=>s.Plans),new("plan_scope",typeof(PlanScope),s=>s.Scopes),new("plan_leg",typeof(PlanLeg),s=>s.Legs),
        new("route_segment",typeof(RouteSegment),s=>s.Segments),new("reminder_setting",typeof(ReminderSetting),s=>s.Reminders),new("reminder_delivery",typeof(ReminderDelivery),s=>s.Deliveries) ];
    public SqliteStore(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        connectionString=new SqliteConnectionStringBuilder{DataSource=path,ForeignKeys=true,Pooling=false,DefaultTimeout=5}.ToString();
        using var connection=Open();using var transaction=connection.BeginTransaction();using var command=connection.CreateCommand();command.Transaction=transaction;
        command.CommandText="SELECT count(*) FROM sqlite_master WHERE type='table' AND name='meta'";
        long prior=0;if(Convert.ToInt64(command.ExecuteScalar())>0){command.CommandText="SELECT value FROM meta WHERE key='schema_version'";prior=Convert.ToInt64(command.ExecuteScalar());if(prior is <1 or >2)throw new InvalidOperationException("数据库版本不兼容，当前软件不会覆盖该文件。");}
        using var resource=typeof(SqliteStore).Assembly.GetManifestResourceStream("Planner.Data.schema.sql")!;
        using var reader=new StreamReader(resource);command.CommandText=reader.ReadToEnd();command.ExecuteNonQuery();
        if(prior==1)
        {
            command.CommandText="ALTER TABLE plan_leg ADD COLUMN known_cost_fen INTEGER CHECK(known_cost_fen>=0); ALTER TABLE plan_leg ADD COLUMN cost_complete INTEGER NOT NULL DEFAULT 0 CHECK(cost_complete IN(0,1)); ALTER TABLE plan_leg ADD COLUMN cost_note TEXT NOT NULL DEFAULT '旧方案未保存费用快照'; UPDATE meta SET value=2 WHERE key='schema_version';";command.ExecuteNonQuery();
        }
        transaction.Commit();
    }
    SqliteConnection Open(){var c=new SqliteConnection(connectionString);c.Open();return c;}
    public PlannerState Load()
    {
        using var connection=Open();using var tx=connection.BeginTransaction();var state=new PlannerState();
        using(var cmd=connection.CreateCommand()){cmd.Transaction=tx;cmd.CommandText="SELECT value FROM meta WHERE key='revision'";state.Revision=Convert.ToInt64(cmd.ExecuteScalar());}
        foreach(var binding in Bindings)
        {
            var props=binding.Type.GetProperties().ToDictionary(p=>JsonNamingPolicy.SnakeCaseLower.ConvertName(p.Name));
            using var cmd=connection.CreateCommand();cmd.Transaction=tx;cmd.CommandText="SELECT * FROM "+binding.Table;
            using var reader=cmd.ExecuteReader();
            while(reader.Read())
            {
                using var stream=new MemoryStream();
                using(var writer=new Utf8JsonWriter(stream))
                {
                    writer.WriteStartObject();
                    for(var i=0;i<reader.FieldCount;i++)
                    {
                        var name=reader.GetName(i);writer.WritePropertyName(name);
                        if(reader.IsDBNull(i)) writer.WriteNullValue();
                        else if(props[name].PropertyType==typeof(bool)) writer.WriteBooleanValue(reader.GetInt64(i)!=0);
                        else JsonSerializer.Serialize(writer,reader.GetValue(i));
                    }
                    writer.WriteEndObject();
                }
                binding.Items(state).Add(JsonSerializer.Deserialize(stream.ToArray(),binding.Type,JsonKit.Options)!);
            }
        }
        tx.Commit();return state;
    }
    public void Save(PlannerState state,long expectedRevision)
    {
        if(state.Revision!=expectedRevision+1) throw new InvalidOperationException("保存版本必须递增。");
        using var connection=Open();using var tx=connection.BeginTransaction();
        using(var guard=connection.CreateCommand())
        {
            guard.Transaction=tx;guard.CommandText="UPDATE meta SET value=$next WHERE key='revision' AND value=$expected";
            guard.Parameters.AddWithValue("$next",state.Revision);guard.Parameters.AddWithValue("$expected",expectedRevision);
            if(guard.ExecuteNonQuery()!=1) throw new InvalidOperationException("本地数据已被其他窗口修改，请重新加载后再保存。");
        }
        foreach(var binding in Bindings.Reverse())
        {using var cmd=connection.CreateCommand();cmd.Transaction=tx;cmd.CommandText="DELETE FROM "+binding.Table;cmd.ExecuteNonQuery();}
        foreach(var binding in Bindings)
            foreach(var item in binding.Items(state))
            {
                var json=JsonSerializer.SerializeToElement(item,binding.Type,JsonKit.Options);
                var fields=json.EnumerateObject().ToList();
                using var cmd=connection.CreateCommand();cmd.Transaction=tx;
                cmd.CommandText=$"INSERT INTO {binding.Table} ({string.Join(',',fields.Select(f=>f.Name))}) VALUES ({string.Join(',',fields.Select((_,i)=>"$p"+i))})";
                for(var i=0;i<fields.Count;i++)
                {
                    var value=fields[i].Value;
                    object parameter=value.ValueKind switch {JsonValueKind.Null=>DBNull.Value,JsonValueKind.True=>1,JsonValueKind.False=>0,JsonValueKind.Number=>value.TryGetInt64(out var l)?l:value.GetDouble(),_=>value.GetString()!};
                    cmd.Parameters.AddWithValue("$p"+i,parameter);
                }
                cmd.ExecuteNonQuery();
            }
        tx.Commit();
    }
}
