using CommonPlugin.Enums;
using NileLibraryNS.Enums;
using NileLibraryNS.Services;
using Playnite.SDK;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace NileLibraryNS
{
    public class NileLibrarySettings
    {
        public int Version { get; set; }
        public bool ImportInstalledGames { get; set; } = true;
        public bool ConnectAccount { get; set; } = false;
        public bool ImportUninstalledGames { get; set; } = false;
        public bool StartGamesWithoutLauncher { get; set; } = false;
        public string GamesInstallationPath { get; set; } = "";
        public string SelectedNilePath { get; set; } = "";
        public int MaxWorkers { get; set; } = 0;
        public bool UnattendedInstall { get; set; } = false;
        public DownloadCompleteAction DoActionAfterDownloadComplete { get; set; } = DownloadCompleteAction.Nothing;
        public bool DisplayDownloadSpeedInBits { get; set; } = false;
        public bool DisplayDownloadTaskFinishedNotifications { get; set; } = true;
        public ClearCacheTime AutoClearCache { get; set; } = ClearCacheTime.Never;
        public UpdatePolicy GamesUpdatePolicy { get; set; } = UpdatePolicy.Month;
    }

    public class NileLibrarySettingsViewModel : PluginSettingsViewModel<NileLibrarySettings, NileLibrary>
    {
        public bool IsFirstRunUse { get; set; }

        public bool IsUserLoggedIn
        {
            get
            {
                try
                {
                    var client = new AmazonAccountClient(Plugin);
                    return client.GetIsUserLoggedIn().GetAwaiter().GetResult();
                }
                catch (Exception e)
                {
                    Logger.Error(e, "");
                    return false;
                }
            }
        }

        public RelayCommand<object> LoginCommand
        {
            get => new RelayCommand<object>(async (a) =>
            {
                await Login();
            });
        }

        public NileLibrarySettingsViewModel(NileLibrary library, IPlayniteAPI api) : base(library, api)
        {
            var savedSettings = LoadSavedSettings();
            if (savedSettings != null)
            {
                if (savedSettings.Version == 0)
                {
                    Logger.Debug("Updating Amazon settings from version 0.");
                    if (savedSettings.ImportUninstalledGames)
                    {
                        savedSettings.ConnectAccount = true;
                    }
                }

                savedSettings.Version = 1;
                Settings = savedSettings;
            }
            else
            {
                Settings = new NileLibrarySettings() { Version = 1 };
            }
        }

        private async Task Login()
        {
            try
            {
                var client = new AmazonAccountClient(Plugin);
                await client.Login();
                OnPropertyChanged(nameof(IsUserLoggedIn));
            }
            catch (Exception e) when (!Debugger.IsAttached)
            {
                Logger.Error(e, "Failed to authenticate Amazon user.");
            }
        }
    }
}