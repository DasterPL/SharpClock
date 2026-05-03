using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace MiniOrm
{
    public class TableQuery
    {
        readonly IDbConnection _conn;
        readonly string _table;
        readonly List<(string col, object val)> _where = new List<(string, object)>();
        string[] _columns;
        string _orderBy;

        public TableQuery(IDbConnection conn, string table)
        {
            _conn  = conn;
            _table = table;
        }

        public TableQuery Columns(params string[] columns) { _columns = columns; return this; }
        public TableQuery Where(string col, object val)    { _where.Add((col, val)); return this; }
        public TableQuery OrderBy(string col)              { _orderBy = col; return this; }

        // ── Terminal operations ──────────────────────────────────────────

        public List<T> Select<T>(Func<IDataReader, T> map)
        {
            string cols = _columns != null ? string.Join(", ", _columns) : "*";
            string sql  = $"SELECT {cols} FROM {_table}{WhereClause()}{OrderClause()}";
            var list = new List<T>();
            using (var cmd = Build(sql, WhereParams()))
            using (var reader = cmd.ExecuteReader())
                while (reader.Read())
                    list.Add(map(reader));
            return list;
        }

        public object Scalar(string expression)
        {
            string sql = $"SELECT {expression} FROM {_table}{WhereClause()}";
            using (var cmd = Build(sql, WhereParams()))
                return cmd.ExecuteScalar();
        }

        public int Update(params (string col, object val)[] set)
        {
            string setClauses = string.Join(", ", set.Select(s => $"{s.col} = @s_{s.col}"));
            var parms = set.Select(s => ($"@s_{s.col}", s.val)).Concat(WhereParams());
            using (var cmd = Build($"UPDATE {_table} SET {setClauses}{WhereClause()}", parms))
                return cmd.ExecuteNonQuery();
        }

        public int Upsert(params (string col, object val)[] values) =>
            InsertWith("OR REPLACE", values);

        public int InsertOrIgnore(params (string col, object val)[] values) =>
            InsertWith("OR IGNORE", values);

        public int Delete()
        {
            using (var cmd = Build($"DELETE FROM {_table}{WhereClause()}", WhereParams()))
                return cmd.ExecuteNonQuery();
        }

        // ── Internals ────────────────────────────────────────────────────

        int InsertWith(string conflict, (string col, object val)[] values)
        {
            string cols  = string.Join(", ", values.Select(v => v.col));
            string parms = string.Join(", ", values.Select(v => $"@{v.col}"));
            using (var cmd = Build(
                $"INSERT {conflict} INTO {_table} ({cols}) VALUES ({parms})",
                values.Select(v => ($"@{v.col}", v.val))))
                return cmd.ExecuteNonQuery();
        }

        string WhereClause() => _where.Count == 0 ? "" :
            " WHERE " + string.Join(" AND ", _where.Select(w => $"{w.col} = @{w.col}"));

        string OrderClause() => _orderBy != null ? $" ORDER BY {_orderBy}" : "";

        IEnumerable<(string, object)> WhereParams() =>
            _where.Select(w => ($"@{w.col}", w.val));

        IDbCommand Build(string sql, IEnumerable<(string name, object val)> parms)
        {
            var cmd = _conn.CreateCommand();
            cmd.CommandText = sql;
            foreach (var (name, val) in parms)
            {
                var p = cmd.CreateParameter();
                p.ParameterName = name;
                p.Value = val ?? DBNull.Value;
                cmd.Parameters.Add(p);
            }
            return cmd;
        }
    }
}
