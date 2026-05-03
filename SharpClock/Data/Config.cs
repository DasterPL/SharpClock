using System;
using System.Collections.Generic;
using System.Linq;

namespace SharpClock
{
    static class Config
    {
        static Database Db => Database.Instance;

        // ── Internal helpers ─────────────────────────────────────────────

        static Dictionary<string, string> GetModuleParams(string moduleName) =>
            Db.Orm.Table("module_params")
              .Columns("key", "value")
              .Where("module_name", moduleName)
              .Select(r => (r.GetString(0), r.GetString(1)))
              .ToDictionary(t => t.Item1, t => t.Item2);

        static void UpsertModuleParams(PixelModule module)
        {
            foreach (var entry in module.Settings.All)
            {
                try
                {
                    Db.Orm.Table("module_params").Upsert(
                        ("module_name", module.Name),
                        ("key",         entry.Key),
                        ("value",       Converter.Serialize(entry.ValueType, entry.Get())));
                }
                catch (Exception)
                {
                    Logger.Log(ConsoleColor.Yellow, $"Prop: {entry.Key} Value: ", ConsoleColor.Red, "NULL");
                }
            }
        }

        // ── Modules ──────────────────────────────────────────────────────

        public static ModuleConfig[] GetModules() =>
            Db.Orm.Table("modules")
              .OrderBy("sort_order")
              .Select(r => new ModuleConfig(r.GetString(0), r.GetInt32(1) != 0, GetModuleParams(r.GetString(0))))
              .ToArray();

        public static ModuleConfig GetModule(string name) =>
            Db.Orm.Table("modules")
              .Columns("start")
              .Where("name", name)
              .Select(r => new ModuleConfig(name, r.GetInt32(0) != 0, GetModuleParams(name)))
              .FirstOrDefault();

        public static string[] ModuleOrder =>
            Db.Orm.Table("modules")
              .Columns("name")
              .OrderBy("sort_order")
              .Select(r => r.GetString(0))
              .ToArray();

        public static void EditModule(PixelModule module)
        {
            Db.Orm.Transaction(() =>
            {
                int affected = Db.Orm.Table("modules")
                    .Where("name", module.Name)
                    .Update(("start", module.IsRunning ? 1 : 0));
                if (affected == 0)
                    Logger.Log(ConsoleColor.Red, $"Module {module.Name} not found in config");
                else
                    UpsertModuleParams(module);
            });
        }

        public static void EditModules(PixelModule[] modules)
        {
            foreach (var module in modules)
                EditModule(module);
        }

        public static void CreateModule(PixelModule module)
        {
            Logger.Log(ConsoleColor.Yellow, $"Module Name: {module.Name} Start: True");
            Db.Orm.Transaction(() =>
            {
                int sortOrder = Convert.ToInt32(Db.Orm.Table("modules").Scalar("COALESCE(MAX(sort_order) + 1, 0)"));
                Db.Orm.Table("modules").InsertOrIgnore(
                    ("name",       module.Name),
                    ("start",      1),
                    ("sort_order", sortOrder));
                UpsertModuleParams(module);
            });
        }

        public static void RemoveModule(string name) =>
            Db.Orm.Table("modules").Where("name", name).Delete();

        public static void SortModules(string[] names)
        {
            Db.Orm.Transaction(() =>
            {
                for (int i = 0; i < names.Length; i++)
                    Db.Orm.Table("modules").Where("name", names[i]).Update(("sort_order", i));
            });
        }

        // ── Global (DLL) settings ────────────────────────────────────────

        public static Dictionary<string, string> GetGlobalParams(string dllName) =>
            Db.Orm.Table("dll_params")
              .Columns("key", "value")
              .Where("dll_name", dllName)
              .Select(r => (r.GetString(0), r.GetString(1)))
              .ToDictionary(t => t.Item1, t => t.Item2);

        public static void EditGlobalSettings(PixelGlobalSettings gs)
        {
            foreach (var entry in gs.Settings.All)
            {
                try
                {
                    Db.Orm.Table("dll_params").Upsert(
                        ("dll_name", gs.Name),
                        ("key",      entry.Key),
                        ("value",    Converter.Serialize(entry.ValueType, entry.Get())));
                }
                catch (Exception)
                {
                    Logger.Log(ConsoleColor.Yellow, $"GlobalSetting: {entry.Key} Value: ", ConsoleColor.Red, "NULL");
                }
            }
        }

        // ── Properties ───────────────────────────────────────────────────

        public static byte Brightness
        {
            get => byte.Parse(Db.Orm.Table("properties").Where("key", "brightness").Scalar("value")?.ToString() ?? "10");
            set => Db.Orm.Table("properties").Upsert(("key", "brightness"), ("value", value.ToString()));
        }

        public static bool AnimatedSwitching
        {
            get => bool.Parse(Db.Orm.Table("properties").Where("key", "animated_switching").Scalar("value")?.ToString() ?? "False");
            set => Db.Orm.Table("properties").Upsert(("key", "animated_switching"), ("value", value.ToString()));
        }
    }
}
