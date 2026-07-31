using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using Mono.Data.Sqlite;

namespace MiniOrm
{
    public class MiniOrmContext : IDisposable
    {
        readonly IDbConnection _conn;

        // A single connection is shared by the render thread, the GPIO thread, HTTP handlers
        // and every module's Update task. Sqlite connections are not thread-safe, so every
        // statement goes through this gate. Monitor is reentrant, which lets Transaction()
        // hold it across the whole body without deadlocking nested queries on the same thread.
        internal readonly object Gate = new object();

        public MiniOrmContext(string connectionString)
        {
            _conn = CreateConnection(connectionString);
            _conn.Open();
        }

        public void Dispose()
        {
            lock (Gate) _conn.Dispose();
        }

        public TableQuery Table(string name) => new TableQuery(_conn, name, Gate);

        public TableQuery Table<T>()
        {
            string name = typeof(T).GetCustomAttribute<TableAttribute>()?.Name ?? typeof(T).Name;
            return new TableQuery(_conn, name, Gate);
        }

        public void Execute(string sql)
        {
            lock (Gate)
                using (var cmd = _conn.CreateCommand())
                {
                    cmd.CommandText = sql;
                    cmd.ExecuteNonQuery();
                }
        }

        /// <summary>
        /// Runs a PRAGMA that returns a value (e.g. journal_mode) and gives back the result as text.
        /// </summary>
        public string Pragma(string sql)
        {
            lock (Gate)
                using (var cmd = _conn.CreateCommand())
                {
                    cmd.CommandText = sql;
                    return cmd.ExecuteScalar()?.ToString();
                }
        }

        public void CreateTable<T>()
        {
            var type = typeof(T);
            string table = type.GetCustomAttribute<TableAttribute>()?.Name ?? type.Name;

            var pkCols = new List<string>();
            foreach (var prop in type.GetProperties())
                if (prop.GetCustomAttribute<PrimaryKeyAttribute>() != null)
                    pkCols.Add(prop.GetCustomAttribute<ColumnAttribute>()?.Name ?? prop.Name);

            var colDefs     = new List<string>();
            var constraints = new List<string>();

            foreach (var prop in type.GetProperties())
            {
                string col     = prop.GetCustomAttribute<ColumnAttribute>()?.Name ?? prop.Name;
                string sqlType = ToSqlType(prop.PropertyType);
                bool isPk      = prop.GetCustomAttribute<PrimaryKeyAttribute>()    != null;
                bool isAuto    = prop.GetCustomAttribute<AutoIncrementAttribute>() != null;
                bool notNull   = prop.GetCustomAttribute<NotNullAttribute>()       != null;
                string defVal  = prop.GetCustomAttribute<DefaultAttribute>()?.Value;

                string def = $"{col} {sqlType}";
                if (isPk && pkCols.Count == 1) def += " PRIMARY KEY";
                if (isAuto)  def += " AUTOINCREMENT";
                if (notNull) def += " NOT NULL";
                if (defVal != null) def += $" DEFAULT {defVal}";
                colDefs.Add(def);
            }

            if (pkCols.Count > 1)
                constraints.Add($"PRIMARY KEY ({string.Join(", ", pkCols)})");

            foreach (var fk in type.GetCustomAttributes<ForeignKeyAttribute>())
            {
                string onDelete = fk.OnDelete != null ? $" ON DELETE {fk.OnDelete}" : "";
                constraints.Add($"FOREIGN KEY ({fk.Column}) REFERENCES {fk.RefTable}({fk.RefColumn}){onDelete}");
            }

            string body = string.Join(", ", colDefs.Concat(constraints));
            Execute($"CREATE TABLE IF NOT EXISTS {table} ({body})");
        }

        public void Transaction(Action body)
        {
            // Held for the whole body so no other thread can slip statements into this
            // transaction, and so two threads can never open one concurrently.
            lock (Gate)
                using (var tx = _conn.BeginTransaction())
                {
                    try
                    {
                        body();
                        tx.Commit();
                    }
                    catch
                    {
                        try { tx.Rollback(); } catch { }
                        throw;
                    }
                }
        }

        // ── Private ──────────────────────────────────────────────────────

        static IDbConnection CreateConnection(string connectionString)
        {
            int sep = connectionString.IndexOf("://");
            if (sep < 0) throw new ArgumentException("Missing protocol prefix (e.g. sqlite://)");
            string protocol = connectionString.Substring(0, sep);
            string cs       = connectionString.Substring(sep + 3);

            switch (protocol)
            {
                case "sqlite": return new SqliteConnection(cs);
                default: throw new NotSupportedException($"Unsupported protocol: {protocol}");
            }
        }

        static string ToSqlType(Type t)
        {
            if (t == typeof(string))                        return "TEXT";
            if (t == typeof(float) || t == typeof(double))  return "REAL";
            if (t == typeof(int)   || t == typeof(long)   ||
                t == typeof(bool)  || t == typeof(byte))    return "INTEGER";
            return "TEXT";
        }
    }
}
