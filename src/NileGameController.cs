using CliWrap;
using CliWrap.Buffered;
using CliWrap.EventStream;
using NileLibraryNS.Enums;
using NileLibraryNS.Models;
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
                throw new Exception(ResourceProvider.GetString(LOC.NileNotInstalled));
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
                var uninstalledGames = new List<Game>();
                var notUninstalledGames = new List<Game>();
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
                                               .WithArguments(new[] { "-y", "uninstall", game.GameId })
                                               .WithEnvironmentVariables(Nile.DefaultEnvironmentVariables)
                                               .AddCommandToLog()
                                               .WithValidation(CommandResultValidation.None)
                                               .ExecuteBufferedAsync();
                            if (cmd.StandardError.Contains("removed successfully") || cmd.StandardError.Contains("isn't installed"))
                            {
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
                            else
                            {
                                notUninstalledGames.Add(game);
                                logger.Debug("[Nile] " + cmd.StandardError);
                                logger.Error("[Nile] exit code: " + cmd.ExitCode);
                            }
                            counter += 1;
                            a.CurrentProgressValue = counter;
                        }
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
            if (Directory.Exists(Game.InstallDirectory) && Nile.IsInstalled)
            {
                BeforeGameStarting();
                await LaunchGame();
            }
            else
            {
                InvokeOnStopped(new GameStoppedEventArgs());
                if (!Nile.IsInstalled)
                {
                    playniteAPI.Dialogs.ShowErrorMessage(ResourceProvider.GetString(LOC.NileNotInstalled));
                }
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
                    Helpers.SaveJsonSettingsToFile(gameSettings, Game.GameId, "GamesSettings");
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
                        if (exited.ExitCode != 0 && mainBinaryPath == Nile.ClientExecPath)
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
                                playniteAPI.Dialogs.ShowErrorMessage(string.Format(ResourceProvider.GetString(LOC.Nile3P_PlayniteGameStartError), ResourceProvider.GetString(LOC.Nile3P_PlayniteLoginRequired)));
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

}
