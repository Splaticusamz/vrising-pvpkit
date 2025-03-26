using BepInEx.Configuration;
using PvPKit.Database;
using System.IO;

namespace PvPKit.Configs
{
    public class MainConfig
    {
        private static ConfigFile Conf;
        private static readonly string FileName = "PvPKit.cfg";
        private static readonly string ConfigPath = Path.Combine("BepInEx", "config");

        public static bool EnabledKitCommand { get; private set; }

        public static void Initialize()
        {
            Conf = new ConfigFile(Path.Combine(ConfigPath, FileName), true);
            var configEntry = Conf.Bind("PvPKit", "EnableKitCommand", true, "Enable kit command that gives Dracula Set.");
            EnabledKitCommand = configEntry.Value;
            DB.EnabledKitCommand = EnabledKitCommand;
        }
    }
}
