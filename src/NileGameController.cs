using CliWrap;
using CliWrap.Buffered;
using NileLibraryNS.Enums;
using NileLibraryNS.Models;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
}
