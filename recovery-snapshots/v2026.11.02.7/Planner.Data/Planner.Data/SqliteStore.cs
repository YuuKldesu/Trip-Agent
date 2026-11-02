using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Planner.Core;

namespace Planner.Data;

public sealed class SqliteStore
{
	private sealed record Binding(string Table, Type Type, Func<PlannerState, IList> Items);

	private readonly string connectionString;

	private static readonly Binding[] Bindings = new Binding[11]
	{
		new Binding("schedule_table", typeof(ScheduleTable), (PlannerState s) => s.Tables),
		new Binding("place", typeof(Place), (PlannerState s) => s.Places),
		new Binding("activity", typeof(Activity), (PlannerState s) => s.Activities),
		new Binding("recurrence_rule", typeof(RecurrenceRule), (PlannerState s) => s.Rules),
		new Binding("activity_exception", typeof(ActivityException), (PlannerState s) => s.Exceptions),
		new Binding("plan", typeof(Plan), (PlannerState s) => s.Plans),
		new Binding("plan_scope", typeof(PlanScope), (PlannerState s) => s.Scopes),
		new Binding("plan_leg", typeof(PlanLeg), (PlannerState s) => s.Legs),
		new Binding("route_segment", typeof(RouteSegment), (PlannerState s) => s.Segments),
		new Binding("reminder_setting", typeof(ReminderSetting), (PlannerState s) => s.Reminders),
		new Binding("reminder_delivery", typeof(ReminderDelivery), (PlannerState s) => s.Deliveries)
	};

	public SqliteStore(string path)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
		connectionString = new SqliteConnectionStringBuilder
		{
			DataSource = path,
			ForeignKeys = true,
			Pooling = false,
			DefaultTimeout = 5
		}.ToString();
		using SqliteConnection sqliteConnection = Open();
		using SqliteTransaction sqliteTransaction = sqliteConnection.BeginTransaction();
		using SqliteCommand sqliteCommand = sqliteConnection.CreateCommand();
		sqliteCommand.Transaction = sqliteTransaction;
		sqliteCommand.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table' AND name='meta'";
		long num = 0L;
		if (Convert.ToInt64(sqliteCommand.ExecuteScalar()) > 0)
		{
			sqliteCommand.CommandText = "SELECT value FROM meta WHERE key='schema_version'";
			num = Convert.ToInt64(sqliteCommand.ExecuteScalar());
			if ((num < 1 || num > 2) ? true : false)
			{
				throw new InvalidOperationException("数据库版本不兼容，当前软件不会覆盖该文件。");
			}
		}
		using Stream stream = typeof(SqliteStore).Assembly.GetManifestResourceStream("Planner.Data.schema.sql");
		using StreamReader streamReader = new StreamReader(stream);
		sqliteCommand.CommandText = streamReader.ReadToEnd();
		sqliteCommand.ExecuteNonQuery();
		if (num == 1)
		{
			sqliteCommand.CommandText = "ALTER TABLE plan_leg ADD COLUMN known_cost_fen INTEGER CHECK(known_cost_fen>=0); ALTER TABLE plan_leg ADD COLUMN cost_complete INTEGER NOT NULL DEFAULT 0 CHECK(cost_complete IN(0,1)); ALTER TABLE plan_leg ADD COLUMN cost_note TEXT NOT NULL DEFAULT '旧方案未保存费用快照'; UPDATE meta SET value=2 WHERE key='schema_version';";
			sqliteCommand.ExecuteNonQuery();
		}
		sqliteTransaction.Commit();
	}

	private SqliteConnection Open()
	{
		SqliteConnection sqliteConnection = new SqliteConnection(connectionString);
		sqliteConnection.Open();
		return sqliteConnection;
	}

	public PlannerState Load()
	{
		using SqliteConnection sqliteConnection = Open();
		using SqliteTransaction sqliteTransaction = sqliteConnection.BeginTransaction();
		PlannerState plannerState = new PlannerState();
		using (SqliteCommand sqliteCommand = sqliteConnection.CreateCommand())
		{
			sqliteCommand.Transaction = sqliteTransaction;
			sqliteCommand.CommandText = "SELECT value FROM meta WHERE key='revision'";
			plannerState.Revision = Convert.ToInt64(sqliteCommand.ExecuteScalar());
		}
		Binding[] bindings = Bindings;
		foreach (Binding binding in bindings)
		{
			Dictionary<string, PropertyInfo> dictionary = binding.Type.GetProperties().ToDictionary((PropertyInfo p) => JsonNamingPolicy.SnakeCaseLower.ConvertName(p.Name));
			using SqliteCommand sqliteCommand2 = sqliteConnection.CreateCommand();
			sqliteCommand2.Transaction = sqliteTransaction;
			sqliteCommand2.CommandText = "SELECT * FROM " + binding.Table;
			using SqliteDataReader sqliteDataReader = sqliteCommand2.ExecuteReader();
			while (sqliteDataReader.Read())
			{
				using MemoryStream memoryStream = new MemoryStream();
				using (Utf8JsonWriter utf8JsonWriter = new Utf8JsonWriter(memoryStream))
				{
					utf8JsonWriter.WriteStartObject();
					for (int num = 0; num < sqliteDataReader.FieldCount; num++)
					{
						string name = sqliteDataReader.GetName(num);
						utf8JsonWriter.WritePropertyName(name);
						if (sqliteDataReader.IsDBNull(num))
						{
							utf8JsonWriter.WriteNullValue();
						}
						else if (dictionary[name].PropertyType == typeof(bool))
						{
							utf8JsonWriter.WriteBooleanValue(sqliteDataReader.GetInt64(num) != 0);
						}
						else
						{
							JsonSerializer.Serialize(utf8JsonWriter, sqliteDataReader.GetValue(num));
						}
					}
					utf8JsonWriter.WriteEndObject();
				}
				binding.Items(plannerState).Add(JsonSerializer.Deserialize(memoryStream.ToArray(), binding.Type, JsonKit.Options));
			}
		}
		sqliteTransaction.Commit();
		return plannerState;
	}

	public void Save(PlannerState state, long expectedRevision)
	{
		if (state.Revision != expectedRevision + 1)
		{
			throw new InvalidOperationException("保存版本必须递增。");
		}
		using SqliteConnection sqliteConnection = Open();
		using SqliteTransaction sqliteTransaction = sqliteConnection.BeginTransaction();
		using (SqliteCommand sqliteCommand = sqliteConnection.CreateCommand())
		{
			sqliteCommand.Transaction = sqliteTransaction;
			sqliteCommand.CommandText = "UPDATE meta SET value=$next WHERE key='revision' AND value=$expected";
			sqliteCommand.Parameters.AddWithValue("$next", state.Revision);
			sqliteCommand.Parameters.AddWithValue("$expected", expectedRevision);
			if (sqliteCommand.ExecuteNonQuery() != 1)
			{
				throw new InvalidOperationException("本地数据已被其他窗口修改，请重新加载后再保存。");
			}
		}
		foreach (Binding item in Bindings.Reverse())
		{
			using SqliteCommand sqliteCommand2 = sqliteConnection.CreateCommand();
			sqliteCommand2.Transaction = sqliteTransaction;
			sqliteCommand2.CommandText = "DELETE FROM " + item.Table;
			sqliteCommand2.ExecuteNonQuery();
		}
		Binding[] bindings = Bindings;
		foreach (Binding binding in bindings)
		{
			foreach (object item2 in binding.Items(state))
			{
				List<JsonProperty> list = JsonSerializer.SerializeToElement(item2, binding.Type, JsonKit.Options).EnumerateObject().ToList();
				using SqliteCommand sqliteCommand3 = sqliteConnection.CreateCommand();
				sqliteCommand3.Transaction = sqliteTransaction;
				sqliteCommand3.CommandText = $"INSERT INTO {binding.Table} ({string.Join(',', list.Select((JsonProperty f) => f.Name))}) VALUES ({string.Join(',', list.Select((JsonProperty _, int num2) => "$p" + num2))})";
				for (int num = 0; num < list.Count; num++)
				{
					JsonElement value = list[num].Value;
					object value2 = value.ValueKind switch
					{
						JsonValueKind.Null => (object)DBNull.Value, 
						JsonValueKind.True => 1, 
						JsonValueKind.False => 0, 
						JsonValueKind.Number => value.TryGetInt64(out var value3) ? ((double)value3) : value.GetDouble(), 
						_ => value.GetString(), 
					};
					sqliteCommand3.Parameters.AddWithValue("$p" + num, value2);
				}
				sqliteCommand3.ExecuteNonQuery();
			}
		}
		sqliteTransaction.Commit();
	}
}
