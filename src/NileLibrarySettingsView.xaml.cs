using CliWrap;
using CliWrap.Buffered;
using CommonPlugin;
using CommonPlugin.Enums;
using NileLibraryNS.Enums;
using NileLibraryNS.Services;
using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;


namespace NileLibraryNS
{
    public partial class NileLibrarySettingsView : UserControl
    {
        private IPlayniteAPI playniteAPI = API.Instance;
        private ILogger logger = LogManager.GetLogger();
        public NileTroubleshootingInformation troubleshootingInformation;

        public NileLibrarySettingsView()
        {
            InitializeComponent();
            UpdateAuthStatus();
            MaxWorkersNI.MaxValue = CommonHelpers.CpuThreadsNumber;
        }

        private async void UpdateAuthStatus()
        {
            if (NileLibrary.GetSettings().ConnectAccount)
            {
                LoginBtn.IsEnabled = false;
                AuthStatusTB.Text = ResourceProvider.GetString(LOC.Nile3P_AmazonLoginChecking);
                var clientApi = new AmazonAccountClient(NileLibrary.Instance);
                var userLoggedIn = await clientApi.GetIsUserLoggedIn();
                if (userLoggedIn)
                {
                    AuthStatusTB.Text = ResourceProvider.GetString(LOC.NileSignedInAs).Format(clientApi.GetUsername());
                    LoginBtn.Content = ResourceProvider.GetString(LOC.NileSignOut);
                    LoginBtn.IsChecked = true;
                }
                else
                {
                    AuthStatusTB.Text = ResourceProvider.GetString(LOC.Nile3P_AmazonNotLoggedIn);
                    LoginBtn.Content = ResourceProvider.GetString(LOC.Nile3P_AmazonAuthenticateLabel);
                    LoginBtn.IsChecked = false;
                }
                LoginBtn.IsEnabled = true;
            }
            else
            {
                AuthStatusTB.Text = ResourceProvider.GetString(LOC.Nile3P_AmazonNotLoggedIn);
                LoginBtn.IsEnabled = true;
            }
        }

        private async void LoginBtn_Click(object sender, RoutedEventArgs e)
        {
            var userLoggedIn = LoginBtn.IsChecked;
            var clientApi = new AmazonAccountClient(NileLibrary.Instance);
            if (!userLoggedIn == false)
            {
                try
                {
                    await clientApi.Login();
                }
                catch (Exception ex)
                {
                    playniteAPI.Dialogs.ShowErrorMessage(playniteAPI.Resources.GetString(LOC.Nile3P_AmazonNotLoggedInError), "");
                    logger.Error(ex, "Failed to authenticate user.");
                }
                UpdateAuthStatus();
            }
            else
            {
                var answer = playniteAPI.Dialogs.ShowMessage(ResourceProvider.GetString(LOC.NileSignOutConfirm), LOC.NileSignOut, MessageBoxButton.YesNo);
                if (answer == MessageBoxResult.Yes)
                {
                    clientApi.LogOut();
                    UpdateAuthStatus();
                }
                else
                {
                    LoginBtn.IsChecked = true;
                }
            }
        }

        private void AmazonConnectAccountChk_Checked(object sender, RoutedEventArgs e)
        {
            UpdateAuthStatus();
        }

        private async void NileSettingsUC_Loaded(object sender, RoutedEventArgs e)
        {
            var installedAddons = playniteAPI.Addons.Addons;
            if (installedAddons.Contains("AmazonLibrary_Builtin"))
            {
                MigrateAmazonBtn.IsEnabled = true;
            }

            var downloadCompleteActions = new Dictionary<DownloadCompleteAction, string>
            {
                { DownloadCompleteAction.Nothing, ResourceProvider.GetString(LOC.Nile3P_PlayniteDoNothing) },
                { DownloadCompleteAction.ShutDown, ResourceProvider.GetString(LOC.Nile3P_PlayniteMenuShutdownSystem) },
                { DownloadCompleteAction.Reboot, ResourceProvider.GetString(LOC.Nile3P_PlayniteMenuRestartSystem) },
                { DownloadCompleteAction.Hibernate, ResourceProvider.GetString(LOC.Nile3P_PlayniteMenuHibernateSystem) },
                { DownloadCompleteAction.Sleep, ResourceProvider.GetString(LOC.Nile3P_PlayniteMenuSuspendSystem) },
            };
            AfterDownloadCompleteCBo.ItemsSource = downloadCompleteActions;

            var autoClearOptions = new Dictionary<ClearCacheTime, string>
            {
                { ClearCacheTime.Day, ResourceProvider.GetString(LOC.Nile3P_PlayniteOptionOnceADay) },
                { ClearCacheTime.Week, ResourceProvider.GetString(LOC.Nile3P_PlayniteOptionOnceAWeek) },
                { ClearCacheTime.Month, ResourceProvider.GetString(LOC.NileOnceAMonth) },
                { ClearCacheTime.ThreeMonths, ResourceProvider.GetString(LOC.NileOnceEvery3Months) },
                { ClearCacheTime.SixMonths, ResourceProvider.GetString(LOC.NileOnceEvery6Months) },
                { ClearCacheTime.Never, ResourceProvider.GetString(LOC.Nile3P_PlayniteSettingsPlaytimeImportModeNever) }
            };
            AutoClearCacheCBo.ItemsSource = autoClearOptions;

            var updatePolicyOptions = new Dictionary<UpdatePolicy, string>
            {
                { UpdatePolicy.PlayniteLaunch, ResourceProvider.GetString(LOC.NileCheckUpdatesEveryPlayniteStartup) },
                { UpdatePolicy.Day, ResourceProvider.GetString(LOC.Nile3P_PlayniteOptionOnceADay) },
                { UpdatePolicy.Week, ResourceProvider.GetString(LOC.Nile3P_PlayniteOptionOnceAWeek) },
                { UpdatePolicy.Month, ResourceProvider.GetString(LOC.NileOnceAMonth) },
                { UpdatePolicy.ThreeMonths, ResourceProvider.GetString(LOC.NileOnceEvery3Months) },
                { UpdatePolicy.SixMonths, ResourceProvider.GetString(LOC.NileOnceEvery6Months) },
                { UpdatePolicy.Never, ResourceProvider.GetString(LOC.Nile3P_PlayniteOptionOnlyManually) }
            };
            GamesUpdatesCBo.ItemsSource = updatePolicyOptions;

            var launcherUpdatePolicyOptions = new Dictionary<UpdatePolicy, string>
            {
                { UpdatePolicy.PlayniteLaunch, ResourceProvider.GetString(LOC.NileCheckUpdatesEveryPlayniteStartup) },
                { UpdatePolicy.Day, ResourceProvider.GetString(LOC.Nile3P_PlayniteOptionOnceADay) },
                { UpdatePolicy.Week, ResourceProvider.GetString(LOC.Nile3P_PlayniteOptionOnceAWeek) },
                { UpdatePolicy.Month, ResourceProvider.GetString(LOC.NileOnceAMonth) },
                { UpdatePolicy.ThreeMonths, ResourceProvider.GetString(LOC.NileOnceEvery3Months) },
                { UpdatePolicy.SixMonths, ResourceProvider.GetString(LOC.NileOnceEvery6Months) },
                { UpdatePolicy.Never, ResourceProvider.GetString(LOC.Nile3P_PlayniteOptionOnlyManually) }
            };
            LauncherUpdatesCBo.ItemsSource = launcherUpdatePolicyOptions;

            troubleshootingInformation = new NileTroubleshootingInformation();
            PlayniteVersionTxt.Text = troubleshootingInformation.PlayniteVersion;
            PluginVersionTxt.Text = troubleshootingInformation.PluginVersion;
            GamesInstallationPathTxt.Text = troubleshootingInformation.GamesInstallationPath;
            LogFilesPathTxt.Text = playniteAPI.Paths.ConfigurationPath;
            if (Nile.IsInstalled)
            {
                var nileVersion = await Nile.GetLauncherVersion();
                troubleshootingInformation.NileVersion = nileVersion;
                LauncherVersionTxt.Text = nileVersion;
                NileBinaryTxt.Text = troubleshootingInformation.NileBinary;
            }
            else
            {
                troubleshootingInformation.NileVersion = "Not%20installed";
                LauncherVersionTxt.Text = ResourceProvider.GetString(LOC.NileLauncherNotInstalled);
                NileBinaryTxt.Text = ResourceProvider.GetString(LOC.NileLauncherNotInstalled);
                CheckForNileUpdatesBtn.IsEnabled = false;
                OpenNileBinaryBtn.IsEnabled = false;
            }
            ReportBugHyp.NavigateUri = new Uri($"https://github.com/hawkeye116477/playnite-nile-plugin/issues/new?assignees=&labels=bug&projects=&template=bugs.yml&pluginV={troubleshootingInformation.PluginVersion}&playniteV={troubleshootingInformation.PlayniteVersion}&launcherV={troubleshootingInformation.NileVersion}");
        }

        private void OpenLogFilesPathBtn_Click(object sender, RoutedEventArgs e)
        {
            ProcessStarter.StartProcess("explorer.exe", playniteAPI.Paths.ConfigurationPath);
        }

        private void OpenGamesInstallationPathBtn_Click(object sender, RoutedEventArgs e)
        {
            if (Directory.Exists(troubleshootingInformation.GamesInstallationPath))
            {
                ProcessStarter.StartProcess("explorer.exe", troubleshootingInformation.GamesInstallationPath);
            }
            else
            {
                playniteAPI.Dialogs.ShowErrorMessage(LOC.NilePathNotExistsError);
            }
        }

        private void OpenNileBinaryBtn_Click(object sender, RoutedEventArgs e)
        {
            Nile.StartClient();
        }

        private void CopyRawDataBtn_Click(object sender, RoutedEventArgs e)
        {
            var troubleshootingJSON = Serialization.ToJson(troubleshootingInformation, true);
            Clipboard.SetText(troubleshootingJSON);
        }

        private async void CheckForNileUpdatesBtn_Click(object sender, RoutedEventArgs e)
        {
            var versionInfoContent = await Nile.GetVersionInfoContent();
            if (versionInfoContent.Tag_name != null)
            {
                var newVersion = versionInfoContent.Tag_name.Replace("v", "");
                if (troubleshootingInformation.NileVersion != newVersion)
                {
                    var options = new List<MessageBoxOption>
                    {
                        new MessageBoxOption(ResourceProvider.GetString(LOC.NileViewChangelog)),
                        new MessageBoxOption(ResourceProvider.GetString(LOC.Nile3P_PlayniteOKLabel)),
                    };
                    var result = playniteAPI.Dialogs.ShowMessage(string.Format(ResourceProvider.GetString(LOC.NileNewVersionAvailable), "Nile", newVersion), ResourceProvider.GetString(LOC.Nile3P_PlayniteUpdaterWindowTitle), MessageBoxImage.Information, options);
                    if (result == options[0])
                    {
                        var changelogURL = $"https://github.com/imLinguin/nile/releases/tag/v{newVersion}";
                        Playnite.Commands.GlobalCommands.NavigateUrl(changelogURL);
                    }
                }
                else
                {
                    playniteAPI.Dialogs.ShowMessage(ResourceProvider.GetString(LOC.NileNoUpdatesAvailable));
                }
            }
            else
            {
                playniteAPI.Dialogs.ShowErrorMessage(ResourceProvider.GetString(LOC.Nile3P_PlayniteUpdateCheckFailMessage), "Nile");
            }
        }

        private void ClearCacheBtn_Click(object sender, RoutedEventArgs e)
        {
            var result = playniteAPI.Dialogs.ShowMessage(ResourceProvider.GetString(LOC.NileClearCacheConfirm), ResourceProvider.GetString(LOC.Nile3P_PlayniteSettingsClearCacheTitle), MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                Nile.ClearCache();
            }
        }

        private void ChooseLauncherBtn_Click(object sender, RoutedEventArgs e)
        {
            var file = playniteAPI.Dialogs.SelectFile($"{ResourceProvider.GetString(LOC.Nile3P_PlayniteExecutableTitle)}|*.exe");
            if (file != "")
            {
                SelectedNilePathTxt.Text = file;
            }
        }

        private void ChooseGamePathBtn_Click(object sender, RoutedEventArgs e)
        {
            var path = playniteAPI.Dialogs.SelectFolder();
            if (path != "")
            {
                SelectedGamePathTxt.Text = path;
            }
        }

        private void GamesUpdatesCBo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedValue = (KeyValuePair<UpdatePolicy, string>)GamesUpdatesCBo.SelectedItem;
            if (selectedValue.Key == UpdatePolicy.Never)
            {
                AutoUpdateGamesChk.IsEnabled = false;
            }
            else
            {
                AutoUpdateGamesChk.IsEnabled = true;
            }
        }

        private void MigrateAmazonBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!Nile.IsInstalled)
            {
                Nile.ShowNotInstalledError();
                return;
            }
            var result = playniteAPI.Dialogs.ShowMessage(ResourceProvider.GetString(LOC.NileMigrationConfirm), ResourceProvider.GetString(LOC.NileMigrateGamesAmazon), MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.No)
            {
                return;
            }
            GlobalProgressOptions globalProgressOptions = new GlobalProgressOptions(ResourceProvider.GetString(LOC.NileMigratingGamesAmazon), false) { IsIndeterminate = false };
            playniteAPI.Dialogs.ActivateGlobalProgress(async (a) =>
            {
                using (playniteAPI.Database.BufferedUpdate())
                {
                    var gamesToMigrate = playniteAPI.Database.Games.Where(i => i.PluginId == Guid.Parse("402674cd-4af6-4886-b6ec-0e695bfa0688")).ToList();
                    var migratedGames = new List<string>();
                    var notImportedGames = new List<string>();
                    if (gamesToMigrate.Count > 0)
                    {
                        var iterator = 0;
                        a.ProgressMaxValue = gamesToMigrate.Count() + 1;
                        a.CurrentProgressValue = 0;
                        foreach (var game in gamesToMigrate.ToList())
                        {
                            iterator++;
                            var alreadyExists = playniteAPI.Database.Games.FirstOrDefault(i => i.GameId == game.GameId && i.PluginId == NileLibrary.Instance.Id);
                            if (alreadyExists == null)
                            {
                                game.PluginId = NileLibrary.Instance.Id;
                                if (game.IsInstalled)
                                {
                                    bool canContinue = NileLibrary.Instance.StopDownloadManager(true);
                                    if (canContinue)
                                    {
                                        if (game.Version.IsNullOrEmpty())
                                        {
                                            game.Version = "0";
                                        }
                                        await Nile.AddGameToInstalledList(game);
                                    }
                                    else
                                    {
                                        notImportedGames.Add(game.GameId);
                                        game.IsInstalled = false;
                                    }
                                }
                                playniteAPI.Database.Games.Update(game);
                                migratedGames.Add(game.GameId);
                                a.CurrentProgressValue = iterator;
                            }
                        }
                        a.CurrentProgressValue = gamesToMigrate.Count() + 1;
                        if (migratedGames.Count > 0)
                        {
                            playniteAPI.Dialogs.ShowMessage(LOC.NileMigrationCompleted, LOC.NileMigrateGamesAmazon, MessageBoxButton.OK, MessageBoxImage.Information);
                            logger.Info("Successfully migrated " + migratedGames.Count + " game(s) from Amazon Games to Nile.");
                        }
                        if (notImportedGames.Count > 0)
                        {
                            logger.Info(notImportedGames.Count + " game(s) probably needs to be imported or installed again.");
                        }
                        if (migratedGames.Count == 0 && notImportedGames.Count == 0)
                        {
                            playniteAPI.Dialogs.ShowErrorMessage(LOC.NileMigrationNoGames);
                        }
                    }
                    else
                    {
                        a.ProgressMaxValue = 1;
                        a.CurrentProgressValue = 1;
                        playniteAPI.Dialogs.ShowErrorMessage(LOC.NileMigrationNoGames);
                    }
                }
            }, globalProgressOptions);
        }
    }
}