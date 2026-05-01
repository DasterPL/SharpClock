using Mono.Data.Sqlite;
using System.Collections.Generic;

namespace SharpClock
{
    class Storage : IStorage
    {
        readonly string _moduleName;

        public Storage(string moduleName) => _moduleName = moduleName;

        public T Get<T>(string key, T defaultValue = default(T))
        {
            using (var conn = Config.Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT value FROM module_storage WHERE module_name = @mod AND key = @key";
                cmd.Parameters.AddWithValue("@mod", _moduleName);
                cmd.Parameters.AddWithValue("@key", key);
                var result = cmd.ExecuteScalar()?.ToString();
                if (result == null) return defaultValue;
                try { return SettingsBuilder.Convert<T>(result); }
                catch { return defaultValue; }
            }
        }

        public bool Set<T>(string key, T value)
        {
            using (var conn = Config.Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "INSERT OR REPLACE INTO module_storage (module_name, key, value) VALUES (@mod, @key, @val)";
                cmd.Parameters.AddWithValue("@mod", _moduleName);
                cmd.Parameters.AddWithValue("@key", key);
                cmd.Parameters.AddWithValue("@val", value);
                cmd.ExecuteNonQuery();
                return true;
            }
        }

        public bool Delete(string key)
        {
            using (var conn = Config.Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "DELETE FROM module_storage WHERE module_name = @mod AND key = @key";
                cmd.Parameters.AddWithValue("@mod", _moduleName);
                cmd.Parameters.AddWithValue("@key", key);
                cmd.ExecuteNonQuery();
                return true;
            }
        }

        public IReadOnlyDictionary<string, object> GetAll()
        {
            var dict = new Dictionary<string, object>();
            using (var conn = Config.Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT key, value FROM module_storage WHERE module_name = @mod";
                cmd.Parameters.AddWithValue("@mod", _moduleName);
                using (var reader = cmd.ExecuteReader())
                    while (reader.Read())
                        dict[reader.GetString(0)] = reader.GetString(1);
            }
            return dict;
        }
    }
}
