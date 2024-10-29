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

namespace NileLibraryNS
{
    public class NileInstallController : InstallController
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private readonly NileLibrary library;
        private CancellationTokenSource watcherToken;

        public NileInstallController(Game game, NileLibrary library) : base(game)
        {
            Name = "Install using Nile client";
            this.library = library;
        }

        public override void Dispose()
        {
            watcherToken?.Cancel();
        }

        public override void Install(InstallActionArgs args)
        {
            StartInstallWatcher();
        }

        public async void StartInstallWatcher()
        {
            watcherToken = new CancellationTokenSource();
            await Task.Run(async () =>
            {
                while (true)
                {
                    if (watcherToken.IsCancellationRequested)
                    {
                        return;
                    }

                    var isServicesInitialized = Process.GetProcessesByName("Amazon Games Services").Length > 0;
                    var isUiInitialized = Process.GetProcessesByName("Amazon Games UI").Length == 4;
                    if (isServicesInitialized && isUiInitialized)
                    {
                        // The install URI only works when this service is running and
                        // all the UI processes have been initialized, otherwise it will
                        // just start the launcher without any further action
                        ProcessStarter.StartUrl($"amazon-games://install/{Game.GameId}");
                        break;
                    }

                    await Task.Delay(1000);
                }

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
                        if (installedGames.TryGetValue(Game.GameId, out var installData))
                        {
                            var installInfo = new GameInstallationData()
                            {
                                InstallDirectory = installData.InstallDirectory
                            };

                            InvokeOnInstalled(new GameInstalledEventArgs(installInfo));
                            return;
                        }
                    }

                    await Task.Delay(10000);
                }
            });
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
