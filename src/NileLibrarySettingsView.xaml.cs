using CliWrap;
using CliWrap.Buffered;
using CommonPlugin;
using CommonPlugin.Enums;
using Linguini.Shared.Types.Bundle;
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
                AuthStatusTB.Text = LocalizationManager.Instance.GetString(LOC.ThirdPartyAmazonLoginChecking);
                var clientApi = new AmazonAccountClient(NileLibrary.Instance);
                var userLoggedIn = await clientApi.GetIsUserLoggedIn();
                if (userLoggedIn)
                {
                    AuthStatusTB.Text = LocalizationManager.Instance.GetString(LOC.CommonSignedInAs, new Dictionary<string, IFluentType> { ["userName"] = (FluentString)clientApi.GetUsername() });
                    LoginBtn.Content = LocalizationManager.Instance.GetString(LOC.CommonSignOut);
                    LoginBtn.IsChecked = true;
                }
                else
                {
                    AuthStatusTB.Text = LocalizationManager.Instance.GetString(LOC.ThirdPartyAmazonNotLoggedIn);
                    LoginBtn.Content = LocalizationManager.Instance.GetString(LOC.ThirdPartyAmazonAuthenticateLabel);
                    LoginBtn.IsChecked = false;
                }
                LoginBtn.IsEnabled = true;
            }
            else
            {
                AuthStatusTB.Text = LocalizationManager.Instance.GetString(LOC.ThirdPartyAmazonNotLoggedIn);
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
                    playniteAPI.Dialogs.ShowErrorMessage(LocalizationManager.Instance.GetString(LOC.ThirdPartyAmazonNotLoggedInError), "");
                    logger.Error(ex, "Failed to authenticate user.");
                }
                UpdateAuthStatus();
            }
            else
            {
                var answer = playniteAPI.Dialogs.ShowMessage(LocalizationManager.Instance.GetString(LOC.CommonSignOutConfirm), LocalizationManager.Instance.GetString(LOC.CommonSignOut), MessageBoxButton.YesNo);
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
                { DownloadCompleteAction.Nothing, LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteDoNothing) },
                { DownloadCompleteAction.ShutDown, LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteMenuShutdownSystem) },
                { DownloadCompleteAction.Reboot, LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteMenuRestartSystem) },
                { DownloadCompleteAction.Hibernate, LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteMenuHibernateSystem) },
                { DownloadCompleteAction.Sleep, LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteMenuSuspendSystem) },
            };
            AfterDownloadCompleteCBo.ItemsSource = downloadCompleteActions;

            var autoClearOptions = new Dictionary<ClearCacheTime, string>
            {
                { ClearCacheTime.Day, LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteOptionOnceADay) },
                { ClearCacheTime.Week, LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteOptionOnceAWeek) },
                { ClearCacheTime.Month, LocalizationManager.Instance.GetString(LOC.CommonOnceAMonth) },
                { ClearCacheTime.ThreeMonths, LocalizationManager.Instance.GetString(LOC.CommonOnceEvery3Months) },
                { ClearCacheTime.SixMonths, LocalizationManager.Instance.GetString(LOC.CommonOnceEvery6Months) },
                { ClearCacheTime.Never, LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteSettingsPlaytimeImportModeNever) }
            };
            AutoClearCacheCBo.ItemsSource = autoClearOptions;
            AutoRemoveCompletedDownloadsCBo.ItemsSource = autoClearOptions;

            var updatePolicyOptions = new Dictionary<UpdatePolicy, string>
            {
                { UpdatePolicy.PlayniteLaunch, LocalizationManager.Instance.GetString(LOC.CommonCheckUpdatesEveryPlayniteStartup) },
                { UpdatePolicy.Day, LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteOptionOnceADay) },
                { UpdatePolicy.Week, LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteOptionOnceAWeek) },
                { UpdatePolicy.Month, LocalizationManager.Instance.GetString(LOC.CommonOnceAMonth) },
                { UpdatePolicy.ThreeMonths, LocalizationManager.Instance.GetString(LOC.CommonOnceEvery3Months) },
                { UpdatePolicy.SixMonths, LocalizationManager.Instance.GetString(LOC.CommonOnceEvery6Months) },
                { UpdatePolicy.Never, LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteOptionOnlyManually) }
            };
            GamesUpdatesCBo.ItemsSource = updatePolicyOptions;

            var launcherUpdatePolicyOptions = new Dictionary<UpdatePolicy, string>
            {
                { UpdatePolicy.PlayniteLaunch, LocalizationManager.Instance.GetString(LOC.CommonCheckUpdatesEveryPlayniteStartup) },
                { UpdatePolicy.Day, LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteOptionOnceADay) },
                { UpdatePolicy.Week, LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteOptionOnceAWeek) },
                { UpdatePolicy.Month, LocalizationManager.Instance.GetString(LOC.CommonOnceAMonth) },
                { UpdatePolicy.ThreeMonths, LocalizationManager.Instance.GetString(LOC.CommonOnceEvery3Months) },
                { UpdatePolicy.SixMonths, LocalizationManager.Instance.GetString(LOC.CommonOnceEvery6Months) },
                { UpdatePolicy.Never, LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteOptionOnlyManually) }
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
                LauncherVersionTxt.Text = LocalizationManager.Instance.GetString(LOC.CommonLauncherNotInstalled);
                NileBinaryTxt.Text = LocalizationManager.Instance.GetString(LOC.CommonLauncherNotInstalled);
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
                playniteAPI.Dialogs.ShowErrorMessage(LOC.CommonPathNotExistsError);
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
                        new MessageBoxOption(LocalizationManager.Instance.GetString(LOC.CommonViewChangelog)),
                        new MessageBoxOption(LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteOkLabel)),
                    };
                    var result = playniteAPI.Dialogs.ShowMessage(LocalizationManager.Instance.GetString(LOC.CommonNewVersionAvailable, new Dictionary<string, IFluentType> { ["appName"] = (FluentString)"Nile", ["appVersion"] = (FluentString)newVersion }), LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteUpdaterWindowTitle), MessageBoxImage.Information, options);
                    if (result == options[0])
                    {
                        var changelogURL = $"https://github.com/imLinguin/nile/releases/tag/v{newVersion}";
                        Playnite.Commands.GlobalCommands.NavigateUrl(changelogURL);
                    }
                }
                else
                {
                    playniteAPI.Dialogs.ShowMessage(LocalizationManager.Instance.GetString(LOC.CommonNoUpdatesAvailable));
                }
            }
            else
            {
                playniteAPI.Dialogs.ShowErrorMessage(LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteUpdateCheckFailMessage), "Nile");
            }
        }

        private void ClearCacheBtn_Click(object sender, RoutedEventArgs e)
        {
            var result = playniteAPI.Dialogs.ShowMessage(LocalizationManager.Instance.GetString(LOC.CommonClearCacheConfirm), LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteSettingsClearCacheTitle), MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                Nile.ClearCache();
            }
        }

        private void ChooseLauncherBtn_Click(object sender, RoutedEventArgs e)
        {
            var file = playniteAPI.Dialogs.SelectFile($"{LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteExecutableTitle)}|*.exe");
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
            var result = playniteAPI.Dialogs.ShowMessage(LocalizationManager.Instance.GetString(LOC.CommonMigrationConfirm), LocalizationManager.Instance.GetString(LOC.CommonMigrateGamesOriginal), MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.No)
            {
                return;
            }
            GlobalProgressOptions globalProgressOptions = new GlobalProgressOptions(LocalizationManager.Instance.GetString(LOC.CommonMigratingGamesOriginal), false) { IsIndeterminate = false };
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
                            playniteAPI.Dialogs.ShowMessage(LocalizationManager.Instance.GetString(LOC.CommonMigrationCompleted), LocalizationManager.Instance.GetString(LOC.CommonMigrateGamesOriginal), MessageBoxButton.OK, MessageBoxImage.Information);
                            logger.Info("Successfully migrated " + migratedGames.Count + " game(s) from Amazon Games to Nile.");
                        }
                        if (notImportedGames.Count > 0)
                        {
                            logger.Info(notImportedGames.Count + " game(s) probably needs to be imported or installed again.");
                        }
                        if (migratedGames.Count == 0 && notImportedGames.Count == 0)
                        {
                            playniteAPI.Dialogs.ShowErrorMessage(LocalizationManager.Instance.GetString(LOC.CommonMigrationNoGames));
                        }
                    }
                    else
                    {
                        a.ProgressMaxValue = 1;
                        a.CurrentProgressValue = 1;
                        playniteAPI.Dialogs.ShowErrorMessage(LocalizationManager.Instance.GetString(LOC.CommonMigrationNoGames));
                    }
                }
            }, globalProgressOptions);
        }
    }
}