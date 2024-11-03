using CommonPlugin;
using Playnite.Common;
using Playnite.SDK.Data;
using System;
using System.IO;

namespace NileLibraryNS
{
    public class NileMessagesSettingsModel
    {
        public bool DontShowDownloadManagerWhatsUpMsg { get; set; } = false;
    }

    public class NileMessagesSettings
    {
        public static NileMessagesSettingsModel LoadSettings()
        {
            NileMessagesSettingsModel messagesSettings = null;
            var dataDir = NileLibrary.Instance.GetPluginUserDataPath();
            var dataFile = Path.Combine(dataDir, "messages.json");
            bool correctJson = false;
            if (File.Exists(dataFile))
            {
                var content = FileSystem.ReadFileAsStringSafe(dataFile);
                if (!content.IsNullOrWhiteSpace() && Serialization.TryFromJson(content, out messagesSettings))
                {
                    correctJson = true;
                }
            }
            if (!correctJson)
            {
                messagesSettings = new NileMessagesSettingsModel { };
            }
            return messagesSettings;
        }

        public static void SaveSettings(NileMessagesSettingsModel messagesSettings)
        {
            var commonHelpers = NileLibrary.Instance.commonHelpers;
            commonHelpers.SaveJsonSettingsToFile(messagesSettings, "", "messages", true);
        }
    }
}
