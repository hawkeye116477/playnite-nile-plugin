﻿using CliWrap;
using CliWrap.Buffered;
using CliWrap.EventStream;
using CommonPlugin;
using CommonPlugin.Enums;
using NileLibraryNS.Models;
using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Data;
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

        public NileInstallController(Game game, NileLibrary library) : base(game)
        {
            Name = "Install using Nile client";
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
                Nile.ShowNotInstalledError();
                return;
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

        public NileUninstallController(Game game) : base(game)
        {
            Name = "Uninstall using Nile";
        }

        public override void Uninstall(UninstallActionArgs args)
        {
            Dispose();
            var games = new List<Game>
            {
                Game
            };
            LaunchUninstaller(games);
            Game.IsUninstalling = false;
        }

        public static void LaunchUninstaller(List<Game> games)
        {
            if (!Nile.IsInstalled)
            {
                Nile.ShowNotInstalledError();
                return;
            }
            var playniteAPI = API.Instance;
            string gamesCombined = string.Join(", ", games.Select(item => item.Name));
            var result = MessageCheckBoxDialog.ShowMessage(ResourceProvider.GetString(LOC.Nile3P_PlayniteUninstallGame), ResourceProvider.GetString(LOC.NileUninstallGameConfirm).Format(gamesCombined), LOC.NileRemoveGameLaunchSettings, MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result.Result)
            {
                var canContinue = NileLibrary.Instance.StopDownloadManager(true);
                if (!canContinue)
                {
                    return;
                }
                var notUninstalledGames = new List<Game>();
                var uninstalledGames = new List<Game>();
                GlobalProgressOptions globalProgressOptions = new GlobalProgressOptions($"{ResourceProvider.GetString(LOC.Nile3P_PlayniteUninstalling)}... ", false);
                playniteAPI.Dialogs.ActivateGlobalProgress(async (a) =>
                {
                    a.IsIndeterminate = false;
                    a.ProgressMaxValue = games.Count;
                    using (playniteAPI.Database.BufferedUpdate())
                    {
                        var counter = 0;
                        foreach (var game in games)
                        {
                            a.Text = $"{ResourceProvider.GetString(LOC.Nile3P_PlayniteUninstalling)} {game.Name}... ";
                            var cmd = await Cli.Wrap(Nile.ClientExecPath)
                                               .WithArguments(new[] { "uninstall", game.GameId })
                                               .WithEnvironmentVariables(Nile.DefaultEnvironmentVariables)
                                               .AddCommandToLog()
                                               .WithValidation(CommandResultValidation.None)
                                               .ExecuteBufferedAsync();
                            if (!cmd.StandardError.Contains("removed successfully"))
                            {
                                logger.Debug("[Nile] " + cmd.StandardError);
                                logger.Error("[Nile] exit code: " + cmd.ExitCode);
                            }
                            try
                            {
                                if (Directory.Exists(game.InstallDirectory))
                                {
                                    Directory.Delete(game.InstallDirectory, true);
                                }
                            }
                            catch (Exception ex)
                            {
                                notUninstalledGames.Add(game);
                                logger.Error(ex.Message);
                                counter += 1;
                                a.CurrentProgressValue = counter;
                                continue;
                            }
                            if (result.CheckboxChecked)
                            {
                                var gameSettingsFile = Path.Combine(Path.Combine(NileLibrary.Instance.GetPluginUserDataPath(), "GamesSettings", $"{game.GameId}.json"));
                                if (File.Exists(gameSettingsFile))
                                {
                                    File.Delete(gameSettingsFile);
                                }
                            }
                            game.IsInstalled = false;
                            game.InstallDirectory = "";
                            game.Version = "";
                            playniteAPI.Database.Games.Update(game);
                            uninstalledGames.Add(game);
                        }
                        counter += 1;
                        a.CurrentProgressValue = counter;
                    }
                }, globalProgressOptions);
                if (uninstalledGames.Count > 0)
                {
                    if (uninstalledGames.Count == 1)
                    {
                        playniteAPI.Dialogs.ShowMessage(ResourceProvider.GetString(LOC.NileUninstallSuccess).Format(uninstalledGames[0].Name));
                    }
                    else
                    {
                        string uninstalledGamesCombined = string.Join(", ", uninstalledGames.Select(item => item.Name));
                        playniteAPI.Dialogs.ShowMessage(ResourceProvider.GetString(LOC.NileUninstallSuccessOther).Format(uninstalledGamesCombined));
                    }
                }
                if (notUninstalledGames.Count > 0)
                {
                    if (notUninstalledGames.Count == 1)
                    {
                        playniteAPI.Dialogs.ShowErrorMessage(ResourceProvider.GetString(LOC.Nile3P_PlayniteGameUninstallError).Format(ResourceProvider.GetString(LOC.NileCheckLog)), notUninstalledGames[0].Name);
                    }
                    else
                    {
                        string notUninstalledGamesCombined = string.Join(", ", notUninstalledGames.Select(item => item.Name));
                        playniteAPI.Dialogs.ShowMessage($"{ResourceProvider.GetString(LOC.NileUninstallErrorOther).Format(notUninstalledGamesCombined)} {ResourceProvider.GetString(LOC.NileCheckLog)}");
                    }
                }

            }
        }
    }

    public class NilePlayController : PlayController
    {
        private IPlayniteAPI playniteAPI = API.Instance;
        private static ILogger logger = LogManager.GetLogger();
        private CancellationTokenSource watcherToken;

        public NilePlayController(Game game) : base(game)
        {
            Name = string.Format(ResourceProvider.GetString(LOC.Nile3P_AmazonStartUsingClient), "Nile");
        }

        public override void Dispose()
        {
            watcherToken?.Dispose();
            watcherToken = null;
        }

        public override async void Play(PlayActionArgs args)
        {
            Dispose();
            if (Directory.Exists(Game.InstallDirectory))
            {
                BeforeGameStarting();
                await LaunchGame();
            }
            else
            {
                InvokeOnStopped(new GameStoppedEventArgs());
            }
        }

        public void BeforeGameStarting()
        {
            var gameSettings = NileGameSettingsView.LoadGameSettings(Game.GameId);
            if (!gameSettings.IsFullyInstalled)
            {
                var playniteAPI = API.Instance;
                GlobalProgressOptions installProgressOptions = new GlobalProgressOptions(ResourceProvider.GetString(LOC.NileFinishingInstallation), false);
                playniteAPI.Dialogs.ActivateGlobalProgress((a) =>
                {
                    var gameConfig = Nile.GetGameConfiguration(Game.InstallDirectory);
                    if (gameConfig.PostInstall.Count > 0)
                    {
                        foreach (var depend in gameConfig.PostInstall)
                        {
                            var dependExe = Path.GetFullPath(Path.Combine(Game.InstallDirectory, depend.Command));
                            if (File.Exists(dependExe))
                            {
                                var process = ProcessStarter.StartProcess(dependExe, string.Join(" ", depend.Args));
                                process.WaitForExit();
                            }
                        }
                    }
                    gameSettings.IsFullyInstalled = true;
                    var commonHelpers = NileLibrary.Instance.commonHelpers;
                    commonHelpers.SaveJsonSettingsToFile(gameSettings, "GamesSettings", Game.GameId, true);
                }, installProgressOptions);
            }
        }

        public async Task LaunchGame(bool noLauncher = false)
        {
            Dispose();
            var playArgs = new List<string>();
            var globalSettings = NileLibrary.GetSettings();
            var gameSettings = NileGameSettingsView.LoadGameSettings(Game.GameId);

            bool canLaunchWithoutLauncher = false;
            bool noLauncherModeEnabled = globalSettings.StartGamesWithoutLauncher;
            if (gameSettings?.LaunchDirectly != null)
            {
                noLauncherModeEnabled = (bool)gameSettings.LaunchDirectly;
            }

            var workingDirectory = Path.Combine(Game.InstallDirectory);
            var mainBinaryPath = Nile.ClientExecPath;
            if (noLauncher || noLauncherModeEnabled)
            {
                var gameConfig = Nile.GetGameConfiguration(Game.InstallDirectory);
                if (!Nile.GetGameRequiresClient(gameConfig))
                {
                    canLaunchWithoutLauncher = true;
                    mainBinaryPath = Path.Combine(Game.InstallDirectory, gameConfig.Main.Command);
                    if (gameConfig.Main.Args.HasNonEmptyItems())
                    {
                        playArgs.AddRange(gameConfig.Main.Args);
                    }
                    if (!gameConfig.Main.WorkingSubdirOverride.IsNullOrEmpty())
                    {
                        workingDirectory = Path.Combine(Game.InstallDirectory, gameConfig.Main.WorkingSubdirOverride);
                    }
                }
            }
            if (!canLaunchWithoutLauncher && !Nile.IsInstalled)
            {
                Nile.ShowNotInstalledError();
                InvokeOnStopped(new GameStoppedEventArgs());
                return;
            }

            if (!canLaunchWithoutLauncher)
            {
                playArgs.AddRange(new[] { "launch", Game.GameId });
            }

            if (gameSettings.StartupArguments?.Any() == true)
            {
                playArgs.AddRange(gameSettings.StartupArguments);
            }
            var stdOutBuffer = new StringBuilder();
            var cmd = Cli.Wrap(mainBinaryPath)
                         .WithArguments(playArgs)
                         .WithEnvironmentVariables(Nile.DefaultEnvironmentVariables)
                         .AddCommandToLog()
                         .WithValidation(CommandResultValidation.None)
                         .WithWorkingDirectory(workingDirectory);
            await foreach (var cmdEvent in cmd.ListenAsync())
            {
                switch (cmdEvent)
                {
                    case StartedCommandEvent started:
                        var monitor = new MonitorDirectory(Game.InstallDirectory);
                        if (monitor.IsTrackable())
                        {

                            StartTracking(() => monitor.IsProcessRunning() > 0,
                                          startupCheck: () => monitor.IsProcessRunning());
                        }
                        break;
                    case StandardErrorCommandEvent stdErr:
                        if (mainBinaryPath == Nile.ClientExecPath)
                        {
                            stdOutBuffer.AppendLine(stdErr.Text);
                        }
                        break;
                    case ExitedCommandEvent exited:
                        if (exited.ExitCode != 0 && exited.ExitCode != 1 && mainBinaryPath == Nile.ClientExecPath)
                        {
                            var errorMessage = stdOutBuffer.ToString();
                            logger.Debug("[Nile] " + errorMessage);
                            logger.Error("[Nile] exit code: " + exited.ExitCode);
                            if (errorMessage.Contains("not logged in") && canLaunchWithoutLauncher)
                            {
                                var tryOfflineResponse = new MessageBoxOption(LOC.NileLaunchGameDirectly);
                                var okResponse = new MessageBoxOption(LOC.Nile3P_PlayniteOKLabel, true, true);
                                var offlineConfirm = playniteAPI.Dialogs.ShowMessage(string.Format(ResourceProvider.GetString(LOC.Nile3P_PlayniteGameStartError), ResourceProvider.GetString(LOC.Nile3P_PlayniteLoginRequired)), "", MessageBoxImage.Error,
                                    new List<MessageBoxOption> { tryOfflineResponse, okResponse });
                                if (offlineConfirm == tryOfflineResponse)
                                {
                                    watcherToken.Cancel();
                                    await LaunchGame(true);
                                    return;
                                }
                                else
                                {
                                    InvokeOnStopped(new GameStoppedEventArgs());
                                }
                            }
                            else
                            {
                                InvokeOnStopped(new GameStoppedEventArgs());
                                playniteAPI.Dialogs.ShowErrorMessage(string.Format(ResourceProvider.GetString(LOC.Nile3P_PlayniteGameStartError), ResourceProvider.GetString(LOC.NileCheckLog)));
                            }
                        }
                        else
                        {
                            stdOutBuffer = null;
                        }
                        break;
                    default:
                        break;
                }
            }
        }

        public void StartTracking(Func<bool> trackingAction,
                                  Func<int> startupCheck = null,
                                  int trackingFrequency = 2000,
                                  int trackingStartDelay = 0)
        {
            if (watcherToken != null)
            {
                throw new Exception("Game is already being tracked.");
            }

            watcherToken = new CancellationTokenSource();
            Task.Run(async () =>
            {
                ulong playTimeMs = 0;
                var trackingWatch = new Stopwatch();
                var maxFailCount = 5;
                var failCount = 0;

                if (trackingStartDelay > 0)
                {
                    await Task.Delay(trackingStartDelay, watcherToken.Token).ContinueWith(task => { });
                }

                if (startupCheck != null)
                {
                    while (true)
                    {
                        if (watcherToken.IsCancellationRequested)
                        {
                            return;
                        }

                        if (failCount >= maxFailCount)
                        {
                            InvokeOnStopped(new GameStoppedEventArgs(0));
                            return;
                        }

                        try
                        {
                            var id = startupCheck();
                            if (id > 0)
                            {
                                InvokeOnStarted(new GameStartedEventArgs { StartedProcessId = id });
                                break;
                            }
                        }
                        catch (Exception e)
                        {
                            failCount++;
                            logger.Error(e, "Game startup tracking iteration failed.");
                        }

                        await Task.Delay(trackingFrequency, watcherToken.Token).ContinueWith(task => { });
                    }
                }

                while (true)
                {
                    failCount = 0;
                    if (watcherToken.IsCancellationRequested)
                    {
                        return;
                    }

                    if (failCount >= maxFailCount)
                    {
                        var playTimeS = playTimeMs / 1000;
                        InvokeOnStopped(new GameStoppedEventArgs(playTimeS));
                        return;
                    }

                    try
                    {
                        trackingWatch.Restart();
                        if (!trackingAction())
                        {
                            var playTimeS = playTimeMs / 1000;
                            InvokeOnStopped(new GameStoppedEventArgs(playTimeS));
                            return;
                        }
                    }
                    catch (Exception e)
                    {
                        failCount++;
                        logger.Error(e, "Game tracking iteration failed.");
                    }

                    await Task.Delay(trackingFrequency, watcherToken.Token).ContinueWith(task => { });
                    trackingWatch.Stop();
                    if (trackingWatch.ElapsedMilliseconds > (trackingFrequency + 30_000))
                    {
                        // This is for cases where system is put into sleep or hibernation.
                        // Realistically speaking, one tracking interation should never take 30+ seconds,
                        // but lets use that as safe value in case this runs super slowly on some weird PCs.
                        continue;
                    }

                    playTimeMs += (ulong)trackingWatch.ElapsedMilliseconds;
                }
            });
        }
    }

    public class NileUpdateController
    {
        private IPlayniteAPI playniteAPI = API.Instance;
        private static ILogger logger = LogManager.GetLogger();
        public async Task<Dictionary<string, UpdateInfo>> CheckGameUpdates(string gameId, bool forceRefreshCache = false)
        {
            var gameToUpdate = new Dictionary<string, UpdateInfo>();
            var gamesToUpdate = await CheckAllGamesUpdates(false, forceRefreshCache);
            if (gamesToUpdate.Count > 0)
            {
                var wantedItem = gamesToUpdate.FirstOrDefault(g => g.Key == gameId && g.Value.Success);
                if (wantedItem.Key != null)
                {
                    gameToUpdate.Add(wantedItem.Key, wantedItem.Value);
                }
            }
            return gameToUpdate;
        }

        public async Task<Dictionary<string, UpdateInfo>> CheckAllGamesUpdates(bool silently = false, bool forceRefreshCache = false)
        {
            var gamesToUpdate = new Dictionary<string, UpdateInfo>();
            var appList = Nile.GetInstalledAppList();
            if (appList.Count == 0)
            {
                return gamesToUpdate;
            }
            var cmd = await Cli.Wrap(Nile.ClientExecPath)
                               .WithArguments(new[] { "list-updates", "--json" })
                               .WithEnvironmentVariables(Nile.DefaultEnvironmentVariables)
                               .AddCommandToLog()
                               .WithValidation(CommandResultValidation.None)
                               .ExecuteBufferedAsync();
            var errorMessage = cmd.StandardError;
            if (cmd.ExitCode != 0)
            {
                logger.Error(errorMessage);
                if (!silently)
                {
                    if (errorMessage.Contains("not logged in"))
                    {
                        playniteAPI.Dialogs.ShowErrorMessage(NileLibrary.Instance.Name, $"{ResourceProvider.GetString(LOC.Nile3P_PlayniteUpdateCheckFailMessage)} {ResourceProvider.GetString(LOC.Nile3P_PlayniteLoginRequired)}");
                    }
                    else
                    {
                        playniteAPI.Dialogs.ShowErrorMessage(NileLibrary.Instance.Name, $"{ResourceProvider.GetString(LOC.Nile3P_PlayniteUpdateCheckFailMessage)} {ResourceProvider.GetString(LOC.NileCheckLog)}");
                    }
                }
            }
            else
            {
                var gamesToUpdateManifest = Serialization.FromJson<List<string>>(cmd.StandardOutput);
                if (gamesToUpdateManifest.Count > 0)
                {
                    foreach (var gameToUpdate in gamesToUpdateManifest)
                    {
                        var gameSettings = NileGameSettingsView.LoadGameSettings(gameToUpdate);
                        if (gameSettings.DisableGameVersionCheck != true)
                        {
                            var updateInfo = new UpdateInfo();
                            var gameData = new DownloadManagerData.Download
                            {
                                gameID = gameToUpdate
                            };
                            var gameInfo = await Nile.GetGameInfo(gameData, silently);
                            if (gameInfo.errorDisplayed)
                            {
                                updateInfo.Success = false;
                                logger.Error($"An error occured during checking {gameToUpdate} updates.");
                            }
                            else
                            {
                                updateInfo.Download_size = gameInfo.download_size;
                                updateInfo.Title = gameInfo.title;
                                if (appList.FirstOrDefault(i => i.id == gameToUpdate) != null)
                                {
                                    updateInfo.Install_path = appList.FirstOrDefault(i => i.id == gameToUpdate).path;
                                }
                            }
                            gamesToUpdate.Add(gameToUpdate, updateInfo);
                        }
                    }
                }
            }
            return gamesToUpdate;
        }

        public async Task UpdateGame(Dictionary<string, UpdateInfo> gamesToUpdate, string gameTitle = "", bool silently = false, DownloadProperties downloadProperties = null)
        {
            var updateTasks = new List<DownloadManagerData.Download>();
            if (gamesToUpdate.Count > 0)
            {
                bool canUpdate = true;
                if (canUpdate)
                {
                    if (silently)
                    {
                        var playniteApi = API.Instance;
                        playniteApi.Notifications.Add(new NotificationMessage("NileGamesUpdates", ResourceProvider.GetString(LOC.NileGamesUpdatesUnderway), NotificationType.Info));
                    }
                    NileDownloadManagerView downloadManager = NileLibrary.GetNileDownloadManager();
                    foreach (var gameToUpdate in gamesToUpdate)
                    {
                        var downloadData = new DownloadManagerData.Download { gameID = gameToUpdate.Key, downloadProperties = downloadProperties };
                        var wantedItem = downloadManager.downloadManagerData.downloads.FirstOrDefault(item => item.gameID == gameToUpdate.Key);
                        if (wantedItem != null)
                        {
                            if (wantedItem.status == DownloadStatus.Completed)
                            {
                                downloadManager.downloadManagerData.downloads.Remove(wantedItem);
                                downloadManager.downloadsChanged = true;
                                wantedItem = null;
                            }
                        }
                        if (wantedItem != null)
                        {
                            if (!silently)
                            {
                                playniteAPI.Dialogs.ShowMessage(string.Format(ResourceProvider.GetString(LOC.NileDownloadAlreadyExists), wantedItem.name), "", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }
                        else
                        {
                            if (downloadProperties == null)
                            {
                                var settings = NileLibrary.GetSettings();
                                downloadProperties = new DownloadProperties()
                                {
                                    downloadAction = DownloadAction.Update,
                                    maxWorkers = settings.MaxWorkers,
                                };
                            }
                            if (!gameToUpdate.Value.Install_path.IsNullOrEmpty())
                            {
                                downloadProperties.installPath = gameToUpdate.Value.Install_path;
                            }
                            var updateTask = new DownloadManagerData.Download
                            {
                                gameID = gameToUpdate.Key,
                                name = gameToUpdate.Value.Title,
                                downloadSizeNumber = gameToUpdate.Value.Download_size,
                                downloadProperties = downloadProperties,
                                fullInstallPath = downloadProperties.installPath
                            };
                            updateTasks.Add(updateTask);
                        }
                    }
                    if (updateTasks.Count > 0)
                    {
                        await downloadManager.EnqueueMultipleJobs(updateTasks, silently);
                    }
                }
            }
            else if (!silently)
            {
                playniteAPI.Dialogs.ShowMessage(ResourceProvider.GetString(LOC.NileNoUpdatesAvailable), gameTitle);
            }
        }
    }

}
