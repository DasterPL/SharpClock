using System;
using System.Collections.Generic;
using System.Drawing;
using Mono.Data.Sqlite;

namespace SharpClock
{
    class Config
    {
        static readonly string DbPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.db");
        static string ConnStr => $"Data Source={DbPath};Version=3;";

        static Config _instance;
        static readonly object _lock = new object();
        public static Config Instance
        {
            get
            {
                if (_instance == null)
                    lock (_lock)
                        if (_instance == null)
                            _instance = new Config();
                return _instance;
            }
        }

        Config()
        {
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS properties (
                        key   TEXT PRIMARY KEY,
                        value TEXT NOT NULL
                    );
                    CREATE TABLE IF NOT EXISTS modules (
                        name       TEXT PRIMARY KEY,
                        start      INTEGER NOT NULL DEFAULT 1,
                        sort_order INTEGER NOT NULL DEFAULT 0
                    );
                    CREATE TABLE IF NOT EXISTS module_params (
                        module_name TEXT NOT NULL,
                        key         TEXT NOT NULL,
                        value       TEXT NOT NULL,
                        PRIMARY KEY (module_name, key),
                        FOREIGN KEY (module_name) REFERENCES modules(name) ON DELETE CASCADE
                    );
                    INSERT OR IGNORE INTO properties VALUES ('brightness', '10');
                    INSERT OR IGNORE INTO properties VALUES ('animated_switching', 'False');
                ";
                cmd.ExecuteNonQuery();
            }
        }

        static SqliteConnection Open()
        {
            var conn = new SqliteConnection(ConnStr);
            conn.Open();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "PRAGMA foreign_keys = ON";
                cmd.ExecuteNonQuery();
            }
            return conn;
        }

        public class Module
        {
            public string Class { get; private set; }
            public bool Start { get; private set; }
            public Dictionary<string, string> Params { get; private set; }

            public Module(string cls, bool start, Dictionary<string, string> parms)
            {
                Class = cls;
                Start = start;
                Params = parms;
            }

            public override string ToString() => Class;
        }

        Dictionary<string, string> GetParams(SqliteConnection conn, string moduleName)
        {
            var dict = new Dictionary<string, string>();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT key, value FROM module_params WHERE module_name = @name";
                cmd.Parameters.AddWithValue("@name", moduleName);
                using (var reader = cmd.ExecuteReader())
                    while (reader.Read())
                        dict[reader.GetString(0)] = reader.GetString(1);
            }
            return dict;
        }

        public Module[] Modules
        {
            get
            {
                var list = new List<Module>();
                using (var conn = Open())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT name, start FROM modules ORDER BY sort_order";
                    using (var reader = cmd.ExecuteReader())
                        while (reader.Read())
                            list.Add(new Module(reader.GetString(0), reader.GetInt32(1) != 0, GetParams(conn, reader.GetString(0))));
                }
                return list.ToArray();
            }
        }

        public Module GetModule(string name)
        {
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT name, start FROM modules WHERE name = @name";
                cmd.Parameters.AddWithValue("@name", name);
                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.Read()) return null;
                    bool start = reader.GetInt32(1) != 0;
                    return new Module(name, start, GetParams(conn, name));
                }
            }
        }

        public string[] ModuleOrder
        {
            get
            {
                var names = new List<string>();
                using (var conn = Open())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT name FROM modules ORDER BY sort_order";
                    using (var reader = cmd.ExecuteReader())
                        while (reader.Read())
                            names.Add(reader.GetString(0));
                }
                return names.ToArray();
            }
        }

        public void EditModules(PixelModule[] modules)
        {
            using (var conn = Open())
            using (var tx = conn.BeginTransaction())
            {
                foreach (var module in modules)
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "UPDATE modules SET start = @start WHERE name = @name";
                        cmd.Parameters.AddWithValue("@start", module.IsRunning ? 1 : 0);
                        cmd.Parameters.AddWithValue("@name", module.Name);
                        if (cmd.ExecuteNonQuery() == 0)
                        {
                            Logger.Log(ConsoleColor.Red, $"Module {module.Name} not found in config");
                            continue;
                        }
                    }
                    UpsertParams(conn, module);
                }
                tx.Commit();
            }
        }

        public void CreateModule(PixelModule module)
        {
            Logger.Log(ConsoleColor.Yellow, $"Module Name: {module.Name} Start: True");
            using (var conn = Open())
            using (var tx = conn.BeginTransaction())
            {
                int sortOrder;
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT COALESCE(MAX(sort_order) + 1, 0) FROM modules";
                    sortOrder = Convert.ToInt32(cmd.ExecuteScalar());
                }
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "INSERT OR IGNORE INTO modules (name, start, sort_order) VALUES (@name, 1, @order)";
                    cmd.Parameters.AddWithValue("@name", module.Name);
                    cmd.Parameters.AddWithValue("@order", sortOrder);
                    cmd.ExecuteNonQuery();
                }
                UpsertParams(conn, module);
                tx.Commit();
            }
        }

        void UpsertParams(SqliteConnection conn, PixelModule module)
        {
            foreach (var entry in module.Settings.All)
            {
                try
                {
                    string value = SettingsBuilder.Serialize(entry.ValueType, entry.Get());
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "INSERT OR REPLACE INTO module_params (module_name, key, value) VALUES (@mod, @key, @val)";
                        cmd.Parameters.AddWithValue("@mod", module.Name);
                        cmd.Parameters.AddWithValue("@key", entry.Key);
                        cmd.Parameters.AddWithValue("@val", value);
                        cmd.ExecuteNonQuery();
                    }
                }
                catch (Exception)
                {
                    Logger.Log(ConsoleColor.Yellow, $"Prop: {entry.Key} Value: ", ConsoleColor.Red, "NULL");
                }
            }
        }

        public void RemoveModule(string name)
        {
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "DELETE FROM modules WHERE name = @name";
                cmd.Parameters.AddWithValue("@name", name);
                cmd.ExecuteNonQuery();
            }
        }

        public void SortModules(string[] names)
        {
            using (var conn = Open())
            using (var tx = conn.BeginTransaction())
            {
                for (int i = 0; i < names.Length; i++)
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "UPDATE modules SET sort_order = @order WHERE name = @name";
                        cmd.Parameters.AddWithValue("@order", i);
                        cmd.Parameters.AddWithValue("@name", names[i]);
                        cmd.ExecuteNonQuery();
                    }
                }
                tx.Commit();
            }
        }

        string GetProperty(string key)
        {
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT value FROM properties WHERE key = @key";
                cmd.Parameters.AddWithValue("@key", key);
                return cmd.ExecuteScalar()?.ToString();
            }
        }

        void SetProperty(string key, string value)
        {
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "INSERT OR REPLACE INTO properties (key, value) VALUES (@key, @value)";
                cmd.Parameters.AddWithValue("@key", key);
                cmd.Parameters.AddWithValue("@value", value);
                cmd.ExecuteNonQuery();
            }
        }

        public byte Brightness
        {
            get => byte.Parse(GetProperty("brightness") ?? "10");
            set => SetProperty("brightness", value.ToString());
        }

        public bool AnimatedSwitching
        {
            get => bool.Parse(GetProperty("animated_switching") ?? "False");
            set => SetProperty("animated_switching", value.ToString());
        }
    }
}
