using NileLibraryNS.Enums;
using NileLibraryNS.Models;
using Playnite;
using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace NileLibraryNS
{
    public class NileInstallController : InstallController
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private readonly NileLibrary library;

        public NileInstallController(Game game, NileLibrary library) : base(game)
        {
            Name = "Install using Nile client";
            this.library = library;
        }

        public override void Install(InstallActionArgs args)
        {
            var installProperties = new DownloadProperties { downloadAction = DownloadAction.Install };
            var installData = new List<DownloadManagerData.Download>
            {
                new DownloadManagerData.Download { gameID = Game.GameId, name = Game.Name, downloadProperties = installProperties }
            };
            LaunchInstaller(installData);
            Game.IsInstalling = false;
        }

        public static void LaunchInstaller(List<DownloadManagerData.Download> installData)
        {
            if (!Nile.IsInstalled)
            {
                throw new Exception(ResourceProvider.GetString(LOC.NileNotInstalled));
            }
            var playniteAPI = API.Instance;
            Window window = null;
            if (playniteAPI.ApplicationInfo.Mode == ApplicationMode.Desktop)
            {
                window = playniteAPI.Dialogs.CreateWindow(new WindowCreationOptions
                {
                    ShowMaximizeButton = false,
                });
            }
            else
            {
                window = new Window
                {
                    Background = System.Windows.Media.Brushes.DodgerBlue
                };
            }
            window.DataContext = installData;
            window.Content = new NileGameInstallerView();
            window.Owner = playniteAPI.Dialogs.GetCurrentAppWindow();
            window.SizeToContent = SizeToContent.WidthAndHeight;
            window.MinWidth = 600;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var title = ResourceProvider.GetString(LOC.Nile3P_PlayniteInstallGame);
            if (installData[0].downloadProperties.downloadAction == DownloadAction.Repair)
            {
                title = ResourceProvider.GetString(LOC.NileRepair);
            }
            if (installData.Count == 1)
            {
                title = installData[0].name;
            }
            window.Title = title;
            window.ShowDialog();
        }
    }

    public class NileUninstallController : UninstallController
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private readonly NileLibrary library;
        private CancellationTokenSource watcherToken;

        public NileUninstallController(Game game, NileLibrary library) : base(game)
        {
            Name = "Uninstall using Amazon client";
            this.library = library;
        }

        public override void Dispose()
        {
            watcherToken?.Cancel();
        }

        public override void Uninstall(UninstallActionArgs args)
        {
            if (Nile.IsInstalled)
            {
                Nile.StartClient();
                StartUninstallWatcher();
            }
            else
            {
                throw new Exception("Can't uninstall game. Amazon Games client not found.");
            }
        }

        public async void StartUninstallWatcher()
        {
            watcherToken = new CancellationTokenSource();

            while (true)
            {
                if (watcherToken.IsCancellationRequested)
                {
                    return;
                }

                Dictionary<string, GameMetadata> installedGames = null;
                try
                {
                    installedGames = library.GetInstalledGames();
                }
                catch (Exception e)
                {
                    logger.Error(e, "Failed to get info about installed Amazon games.");
                }

                if (installedGames != null)
                {
                    if (!installedGames.TryGetValue(Game.GameId, out var installData))
                    {
                        InvokeOnUninstalled(new GameUninstalledEventArgs());
                        return;
                    }
                }

                await Task.Delay(2000);
            }
        }
    }
}
