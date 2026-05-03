using System;
using System.IO;
using MiniOrm;

namespace SharpClock
{
    class Database : IDisposable
    {
        static readonly string ConnStr =
            $"sqlite://Data Source={Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.db")};Version=3;";

        static Database _instance;
        static readonly object _lock = new object();
        public static Database Instance
        {
            get
            {
                if (_instance == null)
                    lock (_lock)
                        if (_instance == null)
                            _instance = new Database();
                return _instance;
            }
        }

        public MiniOrmContext Orm { get; }

        Database()
        {
            Orm = new MiniOrmContext(ConnStr);
            Orm.Execute("PRAGMA foreign_keys = ON");
            Orm.CreateTable<PropertyModel>();
            Orm.CreateTable<ModuleModel>();
            Orm.CreateTable<ModuleParamModel>();
            Orm.CreateTable<DllParamModel>();
            Orm.CreateTable<ModuleStorageModel>();
            Orm.Execute("INSERT OR IGNORE INTO properties VALUES ('brightness', '2')");
            Orm.Execute("INSERT OR IGNORE INTO properties VALUES ('animated_switching', 'True')");
        }

        public void Dispose() => Orm.Dispose();
    }
}
