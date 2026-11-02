using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Planner.Core;

namespace Planner.Data;

public sealed class TrafficSnapshotStore
{
	private readonly string connectionString;

	public TrafficSnapshotStore(string directory)
	{
		connectionString = new SqliteConnectionStringBuilder
		{
			DataSource = Path.Combine(directory, "planner.db"),
			Pooling = false,
			DefaultTimeout = 5
		}.ToString();
		using SqliteConnection sqliteConnection = Open();
		using SqliteCommand sqliteCommand = sqliteConnection.CreateCommand();
		sqliteCommand.CommandText = "CREATE TABLE IF NOT EXISTS traffic_check_snapshot (fingerprint TEXT PRIMARY KEY, payload TEXT NOT NULL)";
		sqliteCommand.ExecuteNonQuery();
	}

	private SqliteConnection Open()
	{
		SqliteConnection sqliteConnection = new SqliteConnection(connectionString);
		sqliteConnection.Open();
		return sqliteConnection;
	}

	public Dictionary<string, TrafficSnapshot> Load()
	{
		using SqliteConnection sqliteConnection = Open();
		using SqliteCommand sqliteCommand = sqliteConnection.CreateCommand();
		sqliteCommand.CommandText = "SELECT payload FROM traffic_check_snapshot";
		using SqliteDataReader sqliteDataReader = sqliteCommand.ExecuteReader();
		Dictionary<string, TrafficSnapshot> dictionary = new Dictionary<string, TrafficSnapshot>();
		while (sqliteDataReader.Read())
		{
			TrafficSnapshot trafficSnapshot = JsonSerializer.Deserialize<TrafficSnapshot>(sqliteDataReader.GetString(0), JsonKit.Options);
			if (trafficSnapshot != null && trafficSnapshot.Fresh(Clock.Now))
			{
				dictionary[trafficSnapshot.Fingerprint] = trafficSnapshot;
			}
		}
		return dictionary;
	}

	public void Save(IEnumerable<TrafficSnapshot> snapshots)
	{
		using SqliteConnection sqliteConnection = Open();
		using SqliteTransaction sqliteTransaction = sqliteConnection.BeginTransaction();
		foreach (TrafficSnapshot snapshot in snapshots)
		{
			if (snapshot.Minutes < 0)
			{
				throw new ArgumentException("交通时间不能为负数。");
			}
			using SqliteCommand sqliteCommand = sqliteConnection.CreateCommand();
			sqliteCommand.Transaction = sqliteTransaction;
			sqliteCommand.CommandText = "INSERT INTO traffic_check_snapshot(fingerprint,payload) VALUES($id,$json) ON CONFLICT(fingerprint) DO UPDATE SET payload=excluded.payload";
			sqliteCommand.Parameters.AddWithValue("$id", snapshot.Fingerprint);
			sqliteCommand.Parameters.AddWithValue("$json", JsonSerializer.Serialize(snapshot, JsonKit.Options));
			sqliteCommand.ExecuteNonQuery();
		}
		sqliteTransaction.Commit();
	}
}
