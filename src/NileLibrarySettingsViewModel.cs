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
        public ClearCacheTime AutoRemoveCompletedDownloads { get; set; } = ClearCacheTime.Never;
        public ClearCacheTime AutoClearCache { get; set; } = ClearCacheTime.Never;
        public UpdatePolicy GamesUpdatePolicy { get; set; } = UpdatePolicy.Month;
        public long NextClearingTime { get; set; } = 0;
        public long NextRemovingCompletedDownloadsTime { get; set; } = 0;
        public long NextGamesUpdateTime { get; set; } = 0;
        public UpdatePolicy LauncherUpdatePolicy { get; set; } = UpdatePolicy.Month;
        public long NextLauncherUpdateTime { get; set; } = 0;
        public bool AutoUpdateGames { get; set; } = false;
    }

    public class NileLibrarySettingsViewModel : PluginSettingsViewModel<NileLibrarySettings, NileLibrary>
    {
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
            Settings = LoadSavedSettings() ?? new NileLibrarySettings();
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

        public override void EndEdit()
        {
            if (EditingClone.AutoClearCache != Settings.AutoClearCache)
            {
                if (Settings.AutoClearCache != ClearCacheTime.Never)
                {
                    Settings.NextClearingTime = NileLibrary.GetNextClearingTime(Settings.AutoClearCache);
                }
                else
                {
                    Settings.NextClearingTime = 0;
                }
            }
            if (EditingClone.AutoRemoveCompletedDownloads != Settings.AutoRemoveCompletedDownloads)
            {
                if (Settings.AutoRemoveCompletedDownloads != ClearCacheTime.Never)
                {
                    Settings.NextRemovingCompletedDownloadsTime = NileLibrary.GetNextClearingTime(Settings.AutoRemoveCompletedDownloads);
                }
                else
                {
                    Settings.NextRemovingCompletedDownloadsTime = 0;
                }
            }
            if (EditingClone.GamesUpdatePolicy != Settings.GamesUpdatePolicy)
            {
                if (Settings.GamesUpdatePolicy != UpdatePolicy.Never)
                {
                    Settings.NextGamesUpdateTime = NileLibrary.GetNextUpdateCheckTime(Settings.GamesUpdatePolicy);
                }
                else
                {
                    Settings.NextGamesUpdateTime = 0;
                }
            }
            if (EditingClone.LauncherUpdatePolicy != Settings.LauncherUpdatePolicy)
            {
                if (Settings.LauncherUpdatePolicy != UpdatePolicy.Never)
                {
                    Settings.NextLauncherUpdateTime = NileLibrary.GetNextUpdateCheckTime(Settings.LauncherUpdatePolicy);
                }
                else
                {
                    Settings.NextLauncherUpdateTime = 0;
                }
            }
            base.EndEdit();
        }
    }
}