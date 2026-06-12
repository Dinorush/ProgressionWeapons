using System.Collections.Generic;

namespace ProgressionGear.GearToggle
{
    public sealed class GearToggleData
    {
        private static readonly List<uint> DefaultIDs = new();
        private static readonly Localization.LocalizedText DefaultText = new() { Id = 0, UntranslatedText = "Switch Gear" };
        private static readonly Localization.LocalizedText[] DefaultTextArr = new Localization.LocalizedText[] { DefaultText };

        public static readonly GearToggleData[] Template = new GearToggleData[]
        {
            new(),
            new()
            {
                ButtonText = new Localization.LocalizedText[]
                {
                    new() {Id = 0, UntranslatedText = "Previous"},
                    new() {Id = 0, UntranslatedText = "Next"}
                }
            }
        };

        public List<uint> OfflineIDs { get; set; } = DefaultIDs;
        public Localization.LocalizedText[] ButtonText { get; set; } = DefaultTextArr;
        public bool ReverseOrder { get; set; } = false;
        public string Name { get; set; } = string.Empty;
    }
}
