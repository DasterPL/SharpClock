using MiniOrm;

namespace SharpClock
{
    [Table("properties")]
    class PropertyModel
    {
        [Column("key")]   [PrimaryKey]          public string Key   { get; set; }
        [Column("value")] [NotNull]              public string Value { get; set; }
    }

    [Table("modules")]
    class ModuleModel
    {
        [Column("name")]       [PrimaryKey]              public string Name      { get; set; }
        [Column("start")]      [NotNull] [Default("1")]  public int    Start     { get; set; }
        [Column("sort_order")] [NotNull] [Default("0")]  public int    SortOrder { get; set; }
    }

    [Table("module_params")]
    [ForeignKey("module_name", "modules", "name", "CASCADE")]
    class ModuleParamModel
    {
        [Column("module_name")] [PrimaryKey] [NotNull] public string ModuleName { get; set; }
        [Column("key")]         [PrimaryKey] [NotNull] public string Key        { get; set; }
        [Column("value")]                   [NotNull] public string Value      { get; set; }
    }

    [Table("dll_params")]
    class DllParamModel
    {
        [Column("dll_name")] [PrimaryKey] [NotNull] public string DllName { get; set; }
        [Column("key")]      [PrimaryKey] [NotNull] public string Key     { get; set; }
        [Column("value")]                [NotNull] public string Value   { get; set; }
    }

    [Table("module_storage")]
    class ModuleStorageModel
    {
        [Column("module_name")] [PrimaryKey] [NotNull] public string ModuleName { get; set; }
        [Column("key")]         [PrimaryKey] [NotNull] public string Key        { get; set; }
        [Column("value")]                   [NotNull] public string Value      { get; set; }
    }
}
