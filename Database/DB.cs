using System.Collections.Generic;
using System.IO;

namespace PvPKit.Database
{
    internal struct RecordKit
    {
        public string Name { get; set; }
        public int Amount { get; set; }
        public RecordKit(string name, int amount) { Name = name; Amount = amount; }
    }

    public class DB
    {
        private static readonly string FileDirectory = Path.Combine("BepInEx", "config", MyPluginInfo.PLUGIN_NAME);
        
        public static bool EnabledKitCommand { get; set; }

        public static void Initialize()
        {
            EnabledKitCommand = true;
            Plugin.Logger.LogWarning("PvPKit DB initialized.");
        }

        internal static void LoadData()
        {
            if (!Directory.Exists(FileDirectory)) Directory.CreateDirectory(FileDirectory);
            Plugin.Logger.LogWarning("PvPKit DB initialized.");
        }
    }
}