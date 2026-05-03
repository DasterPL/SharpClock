using System;

namespace MiniOrm
{
    [AttributeUsage(AttributeTargets.Class)]
    public class TableAttribute : Attribute
    {
        public string Name { get; }
        public TableAttribute(string name) { Name = name; }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class ColumnAttribute : Attribute
    {
        public string Name { get; }
        public ColumnAttribute(string name) { Name = name; }
    }

    [AttributeUsage(AttributeTargets.Property)] public class PrimaryKeyAttribute    : Attribute { }
    [AttributeUsage(AttributeTargets.Property)] public class AutoIncrementAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Property)] public class NotNullAttribute       : Attribute { }

    [AttributeUsage(AttributeTargets.Property)]
    public class DefaultAttribute : Attribute
    {
        public string Value { get; }
        public DefaultAttribute(string value) { Value = value; }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class ForeignKeyAttribute : Attribute
    {
        public string Column    { get; }
        public string RefTable  { get; }
        public string RefColumn { get; }
        public string OnDelete  { get; }
        public ForeignKeyAttribute(string column, string refTable, string refColumn, string onDelete = null)
        {
            Column    = column;
            RefTable  = refTable;
            RefColumn = refColumn;
            OnDelete  = onDelete;
        }
    }
}
