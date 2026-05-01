using Mono.Data.Sqlite;
using System.Collections.Generic;

namespace SharpClock
{
    class ModuleStorage : Storage
    {
        readonly string _moduleName;

        public ModuleStorage(string moduleName) => _moduleName = moduleName;

        public override string Get(string key, string defaultValue = null)
        {
            using (var conn = Config.Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT value FROM module_storage WHERE module_name = @mod AND key = @key";
                cmd.Parameters.AddWithValue("@mod", _moduleName);
                cmd.Parameters.AddWithValue("@key", key);
                return cmd.ExecuteScalar()?.ToString() ?? defaultValue;
            }
        }

        public override void Set(string key, string value)
        {
            using (var conn = Config.Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "INSERT OR REPLACE INTO module_storage (module_name, key, value) VALUES (@mod, @key, @val)";
                cmd.Parameters.AddWithValue("@mod", _moduleName);
                cmd.Parameters.AddWithValue("@key", key);
                cmd.Parameters.AddWithValue("@val", value);
                cmd.ExecuteNonQuery();
            }
        }

        public override void Delete(string key)
        {
            using (var conn = Config.Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "DELETE FROM module_storage WHERE module_name = @mod AND key = @key";
                cmd.Parameters.AddWithValue("@mod", _moduleName);
                cmd.Parameters.AddWithValue("@key", key);
                cmd.ExecuteNonQuery();
            }
        }

        public override IReadOnlyDictionary<string, string> GetAll()
        {
            var dict = new Dictionary<string, string>();
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
