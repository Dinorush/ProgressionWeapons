using BepInEx;
using BepInEx.Configuration;
using GTFO.API.Utilities;
using ProgressionGear.ProgressionLock;
using System.IO;

namespace ProgressionGear
{
    internal static class Configuration
    {
        private readonly static ConfigEntry<bool> _disableProgression;
        public static bool DisableProgression => _disableProgression.Value;
        private readonly static ConfigEntry<bool> _toggleBlink;
        public static bool ToggleBlink => _toggleBlink.Value;

        private static readonly ConfigFile _configFile;

        static Configuration()
        {
            _configFile = new ConfigFile(Path.Combine(Paths.ConfigPath, EntryPoint.MODNAME + ".cfg"), saveOnInit: true);
            string section = "Override";
            _disableProgression = _configFile.Bind(section, "Disable Progression Locks", false, "Disables progression-locking for weapons.");

            section = "General Settings";
            _toggleBlink = _configFile.Bind(section, "Toggle Blink", true, "Enables the blinking effect on gear toggle buttons.");
        }

        public static void Init()
        {
            LiveEdit.CreateListener(Paths.ConfigPath, EntryPoint.MODNAME + ".cfg", false).FileChanged += OnFileChanged;
        }

        private static void OnFileChanged(LiveEditEventArgs _)
        {
            bool disabled = DisableProgression;
            _configFile.Reload();

            if (disabled != DisableProgression)
                GearLockManager.Current.SetupAllowedGearsForActiveRundown();
        }
    }
}
