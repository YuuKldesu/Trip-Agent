CREATE TABLE IF NOT EXISTS meta (key TEXT PRIMARY KEY, value INTEGER NOT NULL);
INSERT OR IGNORE INTO meta VALUES ('schema_version',2),('revision',0);
CREATE TABLE IF NOT EXISTS schedule_table (
 id TEXT PRIMARY KEY, name TEXT NOT NULL CHECK(length(trim(name)) BETWEEN 1 AND 60), color TEXT NOT NULL,
 enabled INTEGER NOT NULL CHECK(enabled IN(0,1)), valid_from TEXT, valid_until TEXT,
 version INTEGER NOT NULL CHECK(version>0), updated_at TEXT NOT NULL,
 CHECK(valid_from IS NULL OR valid_until IS NULL OR valid_until>=valid_from));
CREATE TABLE IF NOT EXISTS place (
 id TEXT PRIMARY KEY,input_name TEXT NOT NULL,name TEXT NOT NULL,address TEXT,lng REAL,lat REAL,
 coord_type TEXT,provider_id TEXT,adcode TEXT,status TEXT NOT NULL CHECK(status IN('UNVERIFIED','VERIFIED')),source TEXT,
 CHECK((lng IS NULL AND lat IS NULL) OR (lng BETWEEN -180 AND 180 AND lat BETWEEN -90 AND 90)),
 CHECK(status!='VERIFIED' OR (lng IS NOT NULL AND lat IS NOT NULL AND source IS NOT NULL)));
CREATE TABLE IF NOT EXISTS activity (
 id TEXT PRIMARY KEY,table_id TEXT NOT NULL REFERENCES schedule_table(id), title TEXT NOT NULL CHECK(length(trim(title)) BETWEEN 1 AND 100),
 place_id TEXT REFERENCES place(id),kind TEXT NOT NULL CHECK(kind IN('ONCE','WEEKLY')),constraint_type TEXT NOT NULL CHECK(constraint_type IN('FIXED','FLEXIBLE')),
 start_at TEXT,end_at TEXT,duration_min INTEGER NOT NULL CHECK(duration_min>0 AND duration_min<=10080),window_start TEXT,window_end TEXT,note TEXT,
 version INTEGER NOT NULL CHECK(version>0),updated_at TEXT NOT NULL,
 CHECK(kind='WEEKLY' OR (start_at IS NOT NULL AND end_at>start_at)));
CREATE TABLE IF NOT EXISTS recurrence_rule (
 activity_id TEXT PRIMARY KEY REFERENCES activity(id), valid_from TEXT NOT NULL,valid_until TEXT,
 week_mask INTEGER NOT NULL CHECK(week_mask BETWEEN 1 AND 127),start_time TEXT NOT NULL,window_from TEXT,window_span_min INTEGER,
 CHECK(valid_until IS NULL OR valid_until>=valid_from));
CREATE TABLE IF NOT EXISTS activity_exception (
 id TEXT PRIMARY KEY,activity_id TEXT NOT NULL REFERENCES activity(id), origin_date TEXT NOT NULL,
 kind TEXT NOT NULL CHECK(kind IN('OVERRIDE','CANCEL')),start_at TEXT,end_at TEXT,title TEXT,place_id TEXT REFERENCES place(id),
 place_override INTEGER NOT NULL CHECK(place_override IN(0,1)),version INTEGER NOT NULL CHECK(version>0),updated_at TEXT NOT NULL,
 UNIQUE(activity_id,origin_date),CHECK(kind='CANCEL' OR (start_at IS NOT NULL AND end_at>start_at)));
CREATE TABLE IF NOT EXISTS plan (
 id TEXT PRIMARY KEY,plan_date TEXT NOT NULL UNIQUE,version INTEGER NOT NULL,status TEXT NOT NULL,start_place_id TEXT REFERENCES place(id),
 earliest_depart TEXT,resource_json TEXT,confirmed_at TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS plan_scope (
 plan_id TEXT NOT NULL REFERENCES plan(id),table_id TEXT NOT NULL REFERENCES schedule_table(id),role TEXT NOT NULL,table_version INTEGER NOT NULL,
 PRIMARY KEY(plan_id,table_id));
CREATE TABLE IF NOT EXISTS plan_leg (
 id TEXT PRIMARY KEY,plan_id TEXT NOT NULL REFERENCES plan(id),seq INTEGER NOT NULL,
 from_activity_id TEXT REFERENCES activity(id),from_origin_date TEXT,to_activity_id TEXT NOT NULL REFERENCES activity(id),to_origin_date TEXT,
 depart_at TEXT NOT NULL,arrive_at TEXT NOT NULL,buffer_sec INTEGER NOT NULL CHECK(buffer_sec>=0),query_at TEXT NOT NULL,requested_depart_at TEXT,
 validation_status TEXT NOT NULL CHECK(validation_status IN('VALID','STALE','UNVERIFIED')),known_cost_fen INTEGER CHECK(known_cost_fen>=0),cost_complete INTEGER NOT NULL DEFAULT 0 CHECK(cost_complete IN(0,1)),cost_note TEXT NOT NULL DEFAULT '费用快照暂无数据',UNIQUE(plan_id,seq),CHECK(arrive_at>=depart_at));
CREATE TABLE IF NOT EXISTS route_segment (
 id TEXT PRIMARY KEY,leg_id TEXT NOT NULL REFERENCES plan_leg(id),seq INTEGER NOT NULL,mode TEXT NOT NULL CHECK(mode IN('WALK','BIKE','BUS','METRO','CAR','TAXI')),
 resource_type TEXT,instruction TEXT,duration_sec INTEGER NOT NULL CHECK(duration_sec>=0),distance_m INTEGER CHECK(distance_m>=0),
 known_cost_fen INTEGER CHECK(known_cost_fen>=0),cost_detail_json TEXT,congestion TEXT,geometry TEXT,provider TEXT NOT NULL,UNIQUE(leg_id,seq));
CREATE TABLE IF NOT EXISTS reminder_setting (
 activity_id TEXT PRIMARY KEY REFERENCES activity(id),enabled INTEGER NOT NULL CHECK(enabled IN(0,1)),lead_min INTEGER NOT NULL CHECK(lead_min>=0),basis TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS reminder_delivery (
 id TEXT PRIMARY KEY,activity_id TEXT NOT NULL REFERENCES activity(id),instance_key TEXT NOT NULL,due_at TEXT NOT NULL,status TEXT NOT NULL,recorded_at TEXT NOT NULL,
 UNIQUE(instance_key,due_at));
CREATE INDEX IF NOT EXISTS idx_activity_range ON activity(table_id,kind,start_at);
CREATE INDEX IF NOT EXISTS idx_exception_moved ON activity_exception(start_at,end_at);
