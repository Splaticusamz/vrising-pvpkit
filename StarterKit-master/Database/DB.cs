using System.Collections.Generic;
using System.IO;

namespace StarterKit.Database
{
    internal struct RecordKit
    {
        public string Name { get; set; }
        public int Amount { get; set; }
        public RecordKit(string name, int amount) { Name = name; Amount = amount; }
    }

    internal class DB
    {
        private static readonly string FileDirectory = Path.Combine("BepInEx", "config", MyPluginInfo.PLUGIN_NAME);
        
        public static bool EnabledKitCommand = true;

        internal static void LoadData()
        {
            if (!Directory.Exists(FileDirectory)) Directory.CreateDirectory(FileDirectory);
            Plugin.Logger.LogWarning("DraculaKit DB initialized.");
        }
    }
}