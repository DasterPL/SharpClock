using System.Collections.Generic;
using System.Linq;

namespace SharpClock
{
    class Storage : IStorage
    {
        readonly string _moduleName;
        static Database Db => Database.Instance;

        public Storage(string moduleName) => _moduleName = moduleName;

        public T Get<T>(string key, T defaultValue = default(T))
        {
            string result = Db.Orm.Table("module_storage")
                .Where("module_name", _moduleName)
                .Where("key", key)
                .Scalar("value")?.ToString();
            if (result == null) return defaultValue;
            try { return Converter.To<T>(result); }
            catch { return defaultValue; }
        }

        public bool Set<T>(string key, T value)
        {
            Db.Orm.Table("module_storage").Upsert(
                ("module_name", _moduleName),
                ("key",         key),
                ("value",       value));
            return true;
        }

        public bool Delete(string key)
        {
            Db.Orm.Table("module_storage")
                .Where("module_name", _moduleName)
                .Where("key", key)
                .Delete();
            return true;
        }

        public IReadOnlyDictionary<string, object> GetAll() =>
            Db.Orm.Table("module_storage")
              .Columns("key", "value")
              .Where("module_name", _moduleName)
              .Select(r => (key: r.GetString(0), val: r.GetString(1)))
              .ToDictionary(t => t.key, t => (object)t.val);
    }
}
