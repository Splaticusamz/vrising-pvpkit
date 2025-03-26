using BepInEx.Configuration;
using StarterKit.Database;
using System.IO;

namespace StarterKit.Configs
{
    internal class MainConfig
    {
        private static readonly string FileDirectory = Path.Combine("BepInEx", "config");
        private static readonly string FileName = "DraculaKit.cfg";
        private static readonly string fullPath = Path.Combine(FileDirectory, FileName);
        private static readonly ConfigFile Conf = new ConfigFile(fullPath, true);

        public static ConfigEntry<bool> EnabledKitCommand;

        public static void ConfigInit()
        {
            EnabledKitCommand = Conf.Bind("DraculaKit", "EnableKitCommand", true, "Enable kit command that gives Dracula Set.");

            ConfigBind();
        }
        public static void ConfigBind()
        {
            DB.EnabledKitCommand = EnabledKitCommand.Value;
        }
    }
}
