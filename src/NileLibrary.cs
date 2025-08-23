using CliWrap;
using CliWrap.Buffered;
using CommonPlugin;
using CommonPlugin.Enums;
using Linguini.Shared.Types.Bundle;
using NileLibraryNS.Enums;
using NileLibraryNS.Models;
using NileLibraryNS.Services;
using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;

namespace NileLibraryNS
{
    [LoadPlugin]
    public class NileLibrary : LibraryPluginBase<NileLibrarySettingsViewModel>
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        public static NileLibrary Instance { get; set; }
        private NileDownloadManagerView NileDownloadManagerView;
        private SidebarItem downloadManagerSidebarItem;
        public CommonHelpers commonHelpers { get; set; }

        public NileLibrary(IPlayniteAPI api) : base(
            "Nile (Amazon)",
            Guid.Parse("5901B4B4-774D-411A-9CCE-807C5CA49D88"),
            new LibraryPluginProperties { CanShutdownClient = false, HasSettings = true },
            new NileLibraryClient(),
            Nile.Icon,
            (_) => new NileLibrarySettingsView(),
            api)
        {
            Instance = this;
            commonHelpers = new CommonHelpers(Instance);
            SettingsViewModel = new NileLibrarySettingsViewModel(this, PlayniteApi);
            LoadExtraLocalization();
            commonHelpers.LoadNeededResources();
            NileDownloadManagerView = new NileDownloadManagerView();
        }

        public static NileLibrarySettings GetSettings()
        {
            return Instance.SettingsViewModel?.Settings ?? null;
        }

        public override LibraryMetadataProvider GetMetadataDownloader()
        {
            return new AmazonGamesMetadataProvider();
        }

        public override IEnumerable<InstallController> GetInstallActions(GetInstallActionsArgs args)
        {
            if (args.Game.PluginId != Id)
            {
                yield break;
            }

            yield return new NileInstallController(args.Game, this);
        }

        public override IEnumerable<UninstallController> GetUninstallActions(GetUninstallActionsArgs args)
        {
            if (args.Game.PluginId != Id)
            {
                yield break;
            }

            yield return new NileUninstallController(args.Game);
        }

        public override IEnumerable<PlayController> GetPlayActions(GetPlayActionsArgs args)
        {
            if (args.Game.PluginId != Id)
            {
                yield break;
            }
            yield return new NilePlayController(args.Game);
        }

        public static NileDownloadManagerView GetNileDownloadManager()
        {
            return Instance.NileDownloadManagerView;
        }

        internal Dictionary<string, GameMetadata> GetInstalledGames()
        {
            var games = new Dictionary<string, GameMetadata>();
            var appList = Nile.GetInstalledAppList();

            foreach (InstalledGames.Installed installedGame in appList)
            {
                var app = installedGame;
                var installLocation = app.path;
                if (installLocation.IsNullOrEmpty())
                {
                    continue;
                }

                installLocation = Paths.FixSeparators(installLocation);
                if (!Directory.Exists(installLocation))
                {
                    logger.Error($"Amazon game {app.id} installation directory {installLocation} not detected.");
                    continue;
                }
                var gameName = new DirectoryInfo(installLocation).Name;
                var nileLibSyncJsonPath = Path.Combine(Nile.ConfigPath, "library.json");
                if (File.Exists(nileLibSyncJsonPath))
                {
                    var nileLibSyncJson = new List<NileLibraryFile.NileGames>();
                    var nileLibyncJsonContent = FileSystem.ReadFileAsStringSafe(nileLibSyncJsonPath);
                    if (!nileLibyncJsonContent.IsNullOrWhiteSpace() && Serialization.TryFromJson(nileLibyncJsonContent, out nileLibSyncJson))
                    {
                        var wantedGame = nileLibSyncJson.FirstOrDefault(i => i.product.id == app.id);
                        if (wantedGame != null)
                        {
                            gameName = wantedGame.product.title.RemoveTrademarks();
                        }
                    }
                }

                var game = new GameMetadata()
                {
                    Source = new MetadataNameProperty("Amazon"),
                    Name = gameName,
                    GameId = app.id,
                    Version = app.version,
                    InstallSize = (ulong?)app.size,
                    InstallDirectory = installLocation,
                    IsInstalled = true,
                    Platforms = new HashSet<MetadataProperty> { new MetadataSpecProperty("pc_windows") }
                };

                games.Add(game.GameId, game);
            }

            // Import games installed using Amazon Games Launcher
            //var amazonInstallSqlPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Amazon Games\Data\Games\Sql\GameInstallInfo.sqlite");
            //if (File.Exists(amazonInstallSqlPath))
            //{
            //    bool canContinue = StopDownloadManager(true);
            //    if (canContinue)
            //    {
            //        using var sql = SQLite.OpenDatabase(amazonInstallSqlPath, SqliteOpenFlags.ReadOnly);
            //        foreach (var program in sql.Query<GameConfiguration.AmazonLauncherInstallGameInfo>(@"SELECT * FROM DbSet WHERE Installed = 1;"))
            //        {
            //            if (!Directory.Exists(program.InstallDirectory))
            //            {
            //                continue;
            //            }
            //            var game = new Game()
            //            {
            //                InstallDirectory = Paths.FixSeparators(program.InstallDirectory),
            //                GameId = program.Id,
            //                Name = program.ProductTitle.RemoveTrademarks(),
            //            };
            //            var gameMeta = new GameMetadata()
            //            {
            //                InstallDirectory = game.InstallDirectory,
            //                GameId = game.GameId,
            //                Source = new MetadataNameProperty("Amazon"),
            //                Name = game.Name,
            //                IsInstalled = true,
            //                Platforms = new HashSet<MetadataProperty> { new MetadataSpecProperty("pc_windows") },
            //            };
            //            if (!games.ContainsKey(game.GameId))
            //            {
            //                await Nile.AddGameToInstalledList(game);
            //                games.Add(game.GameId, gameMeta);
            //            }
            //        }
            //    }
            //}

            return games;
        }

        public List<GameMetadata> GetLibraryGames()
        {
            var games = new List<GameMetadata>();
            var client = new AmazonAccountClient(this);
            var entitlements = client.GetAccountEntitlements().GetAwaiter().GetResult();

            foreach (var item in entitlements)
            {
                if (item.product.productLine == "Twitch:FuelEntitlement")
                {
                    continue;
                }

                var game = new GameMetadata()
                {
                    Source = new MetadataNameProperty("Amazon"),
                    GameId = item.product.id,
                    Name = item.product.title.RemoveTrademarks(),
                    Platforms = new HashSet<MetadataProperty> { new MetadataSpecProperty("pc_windows") }
                };

                games.Add(game);
            }

            return games;
        }

        public override IEnumerable<GameMetadata> GetGames(LibraryGetGamesArgs args)
        {
            var allGames = new List<GameMetadata>();
            var installedGames = new Dictionary<string, GameMetadata>();
            Exception importError = null;

            if (SettingsViewModel.Settings.ImportInstalledGames)
            {
                try
                {
                    installedGames = GetInstalledGames();
                    Logger.Debug($"Found {installedGames.Count} installed Nile games.");
                    allGames.AddRange(installedGames.Values.ToList());
                }
                catch (Exception e) when (!PlayniteApi.ApplicationInfo.ThrowAllErrors)
                {
                    Logger.Error(e, "Failed to import installed Nile games.");
                    importError = e;
                }
            }

            if (SettingsViewModel.Settings.ConnectAccount)
            {
                try
                {
                    var libraryGames = GetLibraryGames();
                    Logger.Debug($"Found {libraryGames.Count} library Nile games.");

                    if (!SettingsViewModel.Settings.ImportUninstalledGames)
                    {
                        libraryGames = libraryGames.Where(lg => installedGames.ContainsKey(lg.GameId)).ToList();
                    }

                    foreach (var game in libraryGames)
                    {
                        if (installedGames.TryGetValue(game.GameId, out var installed))
                        {
                            installed.Playtime = game.Playtime;
                            installed.LastActivity = game.LastActivity;
                        }
                        else
                        {
                            allGames.Add(game);
                        }
                    }
                }
                catch (Exception e) when (!PlayniteApi.ApplicationInfo.ThrowAllErrors)
                {
                    Logger.Error(e, "Failed to import linked account Nile games details.");
                    importError = e;
                }
            }

            if (importError != null)
            {
                PlayniteApi.Notifications.Add(new NotificationMessage(
                    ImportErrorMessageId,
                    LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteLibraryImportError, new Dictionary<string, IFluentType> { ["var0"] = (FluentString)Name }) +
                    Environment.NewLine + importError.Message,
                    NotificationType.Error,
                    () => OpenSettingsView()));
            }
            else
            {
                PlayniteApi.Notifications.Remove(ImportErrorMessageId);
            }

            return allGames;
        }

        public void LoadExtraLocalization()
        {
            var currentLanguage = PlayniteApi.ApplicationSettings.Language;
            LocalizationManager.Instance.SetLanguage(currentLanguage);
            var commonFluentArgs = new Dictionary<string, IFluentType>
            {
                { "launcherName", (FluentString)"Nile" },
                { "pluginShortName", (FluentString)"Nile" },
                { "originalPluginShortName", (FluentString)"Amazon" },
                { "updatesSourceName", (FluentString)"Amazon" }
            };
            LocalizationManager.Instance.SetCommonArgs(commonFluentArgs);
        }

        public string GetCachePath(string dirName)
        {
            var cacheDir = Path.Combine(GetPluginUserDataPath(), "cache", dirName);
            if (!Directory.Exists(cacheDir))
            {
                Directory.CreateDirectory(cacheDir);
            }
            return cacheDir;
        }

        public static long GetNextClearingTime(ClearCacheTime frequency)
        {
            DateTimeOffset? clearingTime = null;
            DateTimeOffset now = DateTime.UtcNow;
            switch (frequency)
            {
                case ClearCacheTime.Day:
                    clearingTime = now.AddDays(1);
                    break;
                case ClearCacheTime.Week:
                    clearingTime = now.AddDays(7);
                    break;
                case ClearCacheTime.Month:
                    clearingTime = now.AddMonths(1);
                    break;
                case ClearCacheTime.ThreeMonths:
                    clearingTime = now.AddMonths(3);
                    break;
                case ClearCacheTime.SixMonths:
                    clearingTime = now.AddMonths(6);
                    break;
                default:
                    break;
            }
            return clearingTime?.ToUnixTimeSeconds() ?? 0;
        }

        public static long GetNextUpdateCheckTime(UpdatePolicy frequency)
        {
            DateTimeOffset? updateTime = null;
            DateTimeOffset now = DateTime.UtcNow;
            switch (frequency)
            {
                case UpdatePolicy.PlayniteLaunch:
                    updateTime = now;
                    break;
                case UpdatePolicy.Day:
                    updateTime = now.AddDays(1);
                    break;
                case UpdatePolicy.Week:
                    updateTime = now.AddDays(7);
                    break;
                case UpdatePolicy.Month:
                    updateTime = now.AddMonths(1);
                    break;
                case UpdatePolicy.ThreeMonths:
                    updateTime = now.AddMonths(3);
                    break;
                case UpdatePolicy.SixMonths:
                    updateTime = now.AddMonths(6);
                    break;
                default:
                    break;
            }
            return updateTime?.ToUnixTimeSeconds() ?? 0;
        }

        public static SidebarItem GetPanel()
        {
            if (Instance.downloadManagerSidebarItem == null)
            {
                Instance.downloadManagerSidebarItem = new SidebarItem
                {
                    Title = LocalizationManager.Instance.GetString(LOC.CommonPanel),
                    Icon = Nile.Icon,
                    Type = SiderbarItemType.View,
                    Opened = () => GetNileDownloadManager(),
                    ProgressValue = 0,
                    ProgressMaximum = 100,
                };
            }
            return Instance.downloadManagerSidebarItem;
        }

        public override IEnumerable<SidebarItem> GetSidebarItems()
        {
            yield return downloadManagerSidebarItem;
        }

        public bool StopDownloadManager(bool displayConfirm = false)
        {
            NileDownloadManagerView downloadManager = GetNileDownloadManager();
            var runningAndQueuedDownloads = downloadManager.downloadManagerData.downloads.Where(i => i.status == DownloadStatus.Running
                                                                                                     || i.status == DownloadStatus.Queued).ToList();
            if (runningAndQueuedDownloads.Count > 0)
            {
                if (displayConfirm)
                {
                    var stopConfirm = PlayniteApi.Dialogs.ShowMessage(LocalizationManager.Instance.GetString(LOC.CommonInstanceNotice), "", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (stopConfirm == MessageBoxResult.No)
                    {
                        return false;
                    }
                }
                foreach (var download in runningAndQueuedDownloads)
                {
                    if (download.status == DownloadStatus.Running)
                    {
                        downloadManager.gracefulInstallerCTS?.Cancel();
                        downloadManager.gracefulInstallerCTS?.Dispose();
                        downloadManager.forcefulInstallerCTS?.Dispose();
                    }
                    download.status = DownloadStatus.Paused;
                }
                downloadManager.SaveData();
            }
            return true;
        }

        public override async void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            var globalSettings = GetSettings();
            if (globalSettings != null)
            {
                if (globalSettings.GamesUpdatePolicy != UpdatePolicy.Never)
                {
                    var nextGamesUpdateTime = globalSettings.NextGamesUpdateTime;
                    if (nextGamesUpdateTime != 0)
                    {
                        DateTimeOffset now = DateTime.UtcNow;
                        if (now.ToUnixTimeSeconds() >= nextGamesUpdateTime)
                        {
                            globalSettings.NextGamesUpdateTime = GetNextUpdateCheckTime(globalSettings.GamesUpdatePolicy);
                            SavePluginSettings(globalSettings);
                            NileUpdateController NileUpdateController = new NileUpdateController();
                            var gamesUpdates = await NileUpdateController.CheckAllGamesUpdates(silently: true);
                            if (gamesUpdates.Count > 0)
                            {
                                var successUpdates = gamesUpdates.Where(i => i.Value.Success).ToDictionary(i => i.Key, i => i.Value);
                                if (successUpdates.Count > 0)
                                {
                                    if (globalSettings.AutoUpdateGames)
                                    {
                                        await NileUpdateController.UpdateGame(successUpdates, "", true);
                                    }
                                    else
                                    {
                                        Window window = null;
                                        if (PlayniteApi.ApplicationInfo.Mode == ApplicationMode.Fullscreen && PlayniteApi.ApplicationInfo.ApplicationVersion.Minor < 36)
                                        {
                                            window = new Window();
                                        }
                                        else
                                        {
                                            window = PlayniteApi.Dialogs.CreateWindow(new WindowCreationOptions
                                            {
                                                ShowMaximizeButton = false,
                                            });
                                        }
                                        window.DataContext = successUpdates;
                                        window.Title = $"{LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteExtensionsUpdates)}";
                                        window.Content = new NileUpdaterView();
                                        window.Owner = PlayniteApi.Dialogs.GetCurrentAppWindow();
                                        window.SizeToContent = SizeToContent.WidthAndHeight;
                                        window.MinWidth = 600;
                                        window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                                        window.ShowDialog();
                                    }
                                }
                                else
                                {
                                    PlayniteApi.Notifications.Add(new NotificationMessage("NileGamesUpdateCheckFail",
                                                                                          $"{Name} {Environment.NewLine}" +
                                                                                          $"{LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteUpdateCheckFailMessage)}",
                                                                                          NotificationType.Error));
                                }
                            }
                        }
                    }
                }
                if (globalSettings.LauncherUpdatePolicy != UpdatePolicy.Never && Nile.IsInstalled)
                {
                    var nextCometUpdateTime = globalSettings.NextLauncherUpdateTime;
                    if (nextCometUpdateTime != 0)
                    {
                        DateTimeOffset now = DateTime.UtcNow;
                        if (now.ToUnixTimeSeconds() >= nextCometUpdateTime)
                        {
                            globalSettings.NextLauncherUpdateTime = GetNextUpdateCheckTime(globalSettings.LauncherUpdatePolicy);
                            SavePluginSettings(globalSettings);
                            var nileVersionInfoContent = await Nile.GetVersionInfoContent();
                            if (nileVersionInfoContent.Tag_name != null)
                            {
                                var newVersion = new Version(nileVersionInfoContent.Tag_name.Replace("v", ""));
                                var oldVersion = new Version(await Nile.GetLauncherVersion());
                                if (oldVersion.CompareTo(newVersion) < 0)
                                {
                                    var options = new List<MessageBoxOption>
                                    {
                                        new MessageBoxOption(LocalizationManager.Instance.GetString(LOC.CommonViewChangelog), true),
                                        new MessageBoxOption(LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteOkLabel), false, true),
                                    };
                                    var result = PlayniteApi.Dialogs.ShowMessage(LocalizationManager.Instance.GetString(LOC.CommonNewVersionAvailable, new Dictionary<string, IFluentType> { ["appName"] = (FluentString)"Nile", ["appVersion"] = (FluentString)newVersion.ToString() }), LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteUpdaterWindowTitle), MessageBoxImage.Information, options);
                                    if (result == options[0])
                                    {
                                        var changelogURL = $"https://github.com/imLinguin/nile/releases/tag/v{newVersion}";
                                        Playnite.Commands.GlobalCommands.NavigateUrl(changelogURL);
                                    }
                                }
                            }
                            var gogdlVersionInfoContent = await Nile.GetVersionInfoContent();
                            if (gogdlVersionInfoContent.Tag_name != null)
                            {
                                var newVersion = new Version(gogdlVersionInfoContent.Tag_name.Replace("v", ""));
                                var oldVersion = new Version(await Nile.GetLauncherVersion());
                                if (oldVersion.CompareTo(newVersion) < 0)
                                {
                                    var options = new List<MessageBoxOption>
                                    {
                                        new MessageBoxOption(LocalizationManager.Instance.GetString(LOC.CommonViewChangelog), true),
                                        new MessageBoxOption(LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteOkLabel), false, true),
                                    };
                                    var result = PlayniteApi.Dialogs.ShowMessage(LocalizationManager.Instance.GetString(LOC.CommonNewVersionAvailable, new Dictionary<string, IFluentType> { ["appName"] = (FluentString)"Nile", ["appVersion"] = (FluentString)$"{newVersion}" }), LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteUpdaterWindowTitle), MessageBoxImage.Information, options);
                                    if (result == options[0])
                                    {
                                        var changelogURL = $"https://github.com/Heroic-Games-Launcher/heroic-gogdl/releases/tag/v{newVersion}";
                                        Playnite.Commands.GlobalCommands.NavigateUrl(changelogURL);
                                    }
                                }
                            }
                        }
                    }

                }
            }
        }

        public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
        {
            StopDownloadManager();
            NileDownloadManagerView downloadManager = GetNileDownloadManager();
            var settings = GetSettings();
            if (settings != null)
            {
                if (settings.AutoRemoveCompletedDownloads != ClearCacheTime.Never)
                {
                    var nextRemovingCompletedDownloadsTime = settings.NextRemovingCompletedDownloadsTime;
                    if (nextRemovingCompletedDownloadsTime != 0)
                    {
                        DateTimeOffset now = DateTime.UtcNow;
                        if (now.ToUnixTimeSeconds() >= nextRemovingCompletedDownloadsTime)
                        {
                            foreach (var downloadItem in downloadManager.downloadManagerData.downloads.ToList())
                            {
                                if (downloadItem.status == DownloadStatus.Completed)
                                {
                                    downloadManager.downloadManagerData.downloads.Remove(downloadItem);
                                    downloadManager.downloadsChanged = true;
                                }
                            }
                            settings.NextRemovingCompletedDownloadsTime = GetNextClearingTime(settings.AutoRemoveCompletedDownloads);
                            SavePluginSettings(settings);
                        }
                    }
                    else
                    {
                        settings.NextRemovingCompletedDownloadsTime = GetNextClearingTime(settings.AutoRemoveCompletedDownloads);
                        SavePluginSettings(settings);
                    }
                }
                if (settings.AutoClearCache != ClearCacheTime.Never)
                {
                    var nextClearingTime = settings.NextClearingTime;
                    if (nextClearingTime != 0)
                    {
                        DateTimeOffset now = DateTime.UtcNow;
                        if (now.ToUnixTimeSeconds() >= nextClearingTime)
                        {
                            Nile.ClearCache();
                            settings.NextClearingTime = GetNextClearingTime(settings.AutoClearCache);
                            SavePluginSettings(settings);
                        }
                    }
                    else
                    {
                        settings.NextClearingTime = GetNextClearingTime(settings.AutoClearCache);
                        SavePluginSettings(settings);
                    }
                }
                downloadManager.SaveData();
            }
        }

        public override IEnumerable<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args)
        {
            var NileGames = args.Games.Where(i => i.PluginId == Id).ToList();
            if (NileGames.Count > 0)
            {
                if (NileGames.Count == 1)
                {
                    Game game = NileGames.FirstOrDefault();
                    if (game.IsInstalled)
                    {
                        yield return new GameMenuItem
                        {
                            Description = LocalizationManager.Instance.GetString(LOC.CommonLauncherSettings),
                            Icon = "ModifyLaunchSettingsIcon",
                            Action = (args) =>
                            {
                                if (!Nile.IsInstalled)
                                {
                                    Nile.ShowNotInstalledError();
                                    return;
                                }
                                Window window = PlayniteApi.Dialogs.CreateWindow(new WindowCreationOptions
                                {
                                    ShowMaximizeButton = false
                                });
                                window.DataContext = game;
                                window.Title = $"{LocalizationManager.Instance.GetString(LOC.CommonLauncherSettings)} - {game.Name}";
                                window.Content = new NileGameSettingsView();
                                window.Owner = PlayniteApi.Dialogs.GetCurrentAppWindow();
                                window.SizeToContent = SizeToContent.WidthAndHeight;
                                window.MinWidth = 600;
                                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                                window.ShowDialog();
                            }
                        };
                        yield return new GameMenuItem
                        {
                            Description = LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteCheckForUpdates),
                            Icon = "UpdateDbIcon",
                            Action = (args) =>
                            {
                                if (!Nile.IsInstalled)
                                {
                                    Nile.ShowNotInstalledError();
                                    return;
                                }

                                NileUpdateController NileUpdateController = new NileUpdateController();
                                var gamesToUpdate = new Dictionary<string, UpdateInfo>();
                                GlobalProgressOptions updateCheckProgressOptions = new GlobalProgressOptions(LocalizationManager.Instance.GetString(LOC.CommonCheckingForUpdates), false) { IsIndeterminate = true };
                                PlayniteApi.Dialogs.ActivateGlobalProgress(async (a) =>
                                {
                                    gamesToUpdate = await NileUpdateController.CheckGameUpdates(game.GameId);
                                }, updateCheckProgressOptions);
                                if (gamesToUpdate.Count > 0)
                                {
                                    var successUpdates = gamesToUpdate.Where(i => i.Value.Success).ToDictionary(i => i.Key, i => i.Value);
                                    if (successUpdates.Count > 0)
                                    {
                                        Window window = PlayniteApi.Dialogs.CreateWindow(new WindowCreationOptions
                                        {
                                            ShowMaximizeButton = false,
                                        });
                                        window.DataContext = successUpdates;
                                        window.Title = $"{LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteExtensionsUpdates)}";
                                        window.Content = new NileUpdaterView();
                                        window.Owner = PlayniteApi.Dialogs.GetCurrentAppWindow();
                                        window.SizeToContent = SizeToContent.WidthAndHeight;
                                        window.MinWidth = 600;
                                        window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                                        window.ShowDialog();
                                    }
                                    else
                                    {
                                        PlayniteApi.Dialogs.ShowErrorMessage(LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteUpdateCheckFailMessage), game.Name);
                                    }
                                }
                                else
                                {
                                    PlayniteApi.Dialogs.ShowMessage(LocalizationManager.Instance.GetString(LOC.CommonNoUpdatesAvailable), game.Name);
                                }
                            }
                        };
                    }
                    else
                    {
                        yield return new GameMenuItem
                        {
                            Description = LocalizationManager.Instance.GetString(LOC.CommonImportInstalledGame),
                            Icon = "AddGameIcon",
                            Action = (args) =>
                            {
                                if (!Nile.IsInstalled)
                                {
                                    Nile.ShowNotInstalledError();
                                    return;
                                }

                                var path = PlayniteApi.Dialogs.SelectFolder();
                                if (path != "")
                                {
                                    game.InstallDirectory = path;
                                    game.IsInstalled = true;
                                    bool canContinue = StopDownloadManager(true);
                                    if (!canContinue)
                                    {
                                        return;
                                    }
                                    GlobalProgressOptions importProgressOptions = new GlobalProgressOptions(LocalizationManager.Instance.GetString(LOC.CommonImportingGame, new Dictionary<string, IFluentType> { ["gameTitle"] = (FluentString)game.Name }), false) { IsIndeterminate = true };
                                    PlayniteApi.Dialogs.ActivateGlobalProgress(async (a) =>
                                    {
                                        await Nile.AddGameToInstalledList(game);
                                        PlayniteApi.Database.Games.Update(game);
                                        PlayniteApi.Dialogs.ShowMessage(LocalizationManager.Instance.GetString(LOC.CommonImportFinished));
                                    }, importProgressOptions);
                                }
                            }
                        };
                    }
                    if (game.IsInstalled)
                    {
                        yield return new GameMenuItem
                        {
                            Description = LocalizationManager.Instance.GetString(LOC.CommonMove),
                            Icon = "MoveIcon",
                            Action = (args) =>
                            {
                                if (!Nile.IsInstalled)
                                {
                                    Nile.ShowNotInstalledError();
                                    return;
                                }

                                var newPath = PlayniteApi.Dialogs.SelectFolder();
                                if (newPath != "")
                                {
                                    var oldPath = game.InstallDirectory;
                                    if (Directory.Exists(oldPath) && Directory.Exists(newPath))
                                    {
                                        string sepChar = Path.DirectorySeparatorChar.ToString();
                                        string altChar = Path.AltDirectorySeparatorChar.ToString();
                                        if (!oldPath.EndsWith(sepChar) && !oldPath.EndsWith(altChar))
                                        {
                                            oldPath += sepChar;
                                        }
                                        var folderName = Path.GetFileName(Path.GetDirectoryName(oldPath));
                                        newPath = Path.Combine(newPath, folderName);
                                        var moveConfirm = PlayniteApi.Dialogs.ShowMessage(LocalizationManager.Instance.GetString(LOC.CommonMoveConfirm, new Dictionary<string, IFluentType> { ["appName"] = (FluentString)game.Name, ["path"] = (FluentString)newPath }), LocalizationManager.Instance.GetString(LOC.CommonMove), MessageBoxButton.YesNo, MessageBoxImage.Question);
                                        if (moveConfirm == MessageBoxResult.Yes)
                                        {
                                            GlobalProgressOptions globalProgressOptions = new GlobalProgressOptions(LocalizationManager.Instance.GetString(LOC.CommonMovingGame, new Dictionary<string, IFluentType> { ["appName"] = (FluentString)game.Name, ["path"] = (FluentString)newPath }), false);
                                            PlayniteApi.Dialogs.ActivateGlobalProgress((a) =>
                                            {
                                                a.ProgressMaxValue = 3;
                                                a.CurrentProgressValue = 0;
                                                _ = (Application.Current.Dispatcher?.BeginInvoke((Action)delegate
                                                {
                                                    try
                                                    {
                                                        bool canContinue = StopDownloadManager(true);
                                                        if (!canContinue)
                                                        {
                                                            return;
                                                        }
                                                        Directory.Move(oldPath, newPath);
                                                        a.CurrentProgressValue = 1;
                                                        var installListPath = Path.Combine(Nile.ConfigPath, "installed.json");
                                                        if (File.Exists(installListPath))
                                                        {
                                                            var content = FileSystem.ReadFileAsStringSafe(installListPath);
                                                            if (!content.IsNullOrWhiteSpace())
                                                            {
                                                                var installListJson = Serialization.FromJson<List<InstalledGames.Installed>>(content);
                                                                var wantedItem = installListJson.FirstOrDefault(g => g.id == game.GameId);
                                                                if (wantedItem != null)
                                                                {
                                                                    wantedItem.path = newPath;
                                                                }
                                                                var strConf = Serialization.ToJson(installListJson, true);
                                                                File.WriteAllText(installListPath, strConf);
                                                            }
                                                        }
                                                        game.InstallDirectory = newPath;
                                                        PlayniteApi.Database.Games.Update(game);
                                                        a.CurrentProgressValue = 3;
                                                        PlayniteApi.Dialogs.ShowMessage(LocalizationManager.Instance.GetString(LOC.CommonMoveGameSuccess, new Dictionary<string, IFluentType> { ["appName"] = (FluentString)game.Name, ["path"] = (FluentString)newPath }));
                                                    }
                                                    catch (Exception e)
                                                    {
                                                        a.CurrentProgressValue = 3;
                                                        PlayniteApi.Dialogs.ShowErrorMessage(LocalizationManager.Instance.GetString(LOC.CommonMoveGameError, new Dictionary<string, IFluentType> { ["appName"] = (FluentString)game.Name, ["path"] = (FluentString)newPath }));
                                                        logger.Error(e.Message);
                                                    }
                                                }));
                                            }, globalProgressOptions);
                                        }
                                    }
                                }
                            }
                        };
                    }
                }

                var notInstalledNileGames = NileGames.Where(i => i.IsInstalled == false).ToList();
                if (notInstalledNileGames.Count > 0)
                {
                    if (NileGames.Count > 1)
                    {
                        var installData = new List<DownloadManagerData.Download>();
                        foreach (var notInstalledNileGame in notInstalledNileGames)
                        {
                            var installProperties = new DownloadProperties { downloadAction = DownloadAction.Install };
                            installData.Add(new DownloadManagerData.Download { gameID = notInstalledNileGame.GameId, name = notInstalledNileGame.Name, downloadProperties = installProperties });
                        }
                        yield return new GameMenuItem
                        {
                            Description = LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteInstallGame),
                            Icon = "InstallIcon",
                            Action = (args) =>
                            {
                                NileInstallController.LaunchInstaller(installData);
                            }
                        };
                    }
                }
                var installedNileGames = NileGames.Where(i => i.IsInstalled).ToList();
                if (installedNileGames.Count > 0)
                {
                    yield return new GameMenuItem
                    {
                        Description = LocalizationManager.Instance.GetString(LOC.CommonRepair),
                        Icon = "RepairIcon",
                        Action = (args) =>
                        {
                            if (!Nile.IsInstalled)
                            {
                                Nile.ShowNotInstalledError();
                                return;
                            }

                            Window window = PlayniteApi.Dialogs.CreateWindow(new WindowCreationOptions
                            {
                                ShowMaximizeButton = false,
                            });

                            var installData = new List<DownloadManagerData.Download>();
                            foreach (var game in installedNileGames)
                            {
                                var installProperties = new DownloadProperties { downloadAction = DownloadAction.Repair, installPath = CommonHelpers.NormalizePath(game.InstallDirectory) };
                                installData.Add(new DownloadManagerData.Download { gameID = game.GameId, name = game.Name, downloadProperties = installProperties });
                            }
                            window.DataContext = installData;
                            window.Content = new NileGameInstallerView();
                            window.Owner = PlayniteApi.Dialogs.GetCurrentAppWindow();
                            window.SizeToContent = SizeToContent.WidthAndHeight;
                            window.MinWidth = 600;
                            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                            var title = LocalizationManager.Instance.GetString(LOC.CommonRepair);
                            if (installedNileGames.Count == 1)
                            {
                                title = installedNileGames[0].Name;
                            }
                            window.Title = title;
                            window.ShowDialog();
                        }
                    };
                    if (NileGames.Count > 1)
                    {
                        yield return new GameMenuItem
                        {
                            Description = LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteUninstallGame),
                            Icon = "UninstallIcon",
                            Action = (args) =>
                            {
                                NileUninstallController.LaunchUninstaller(installedNileGames);
                            }
                        };
                    }
                }
            }
        }

        public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
        {
            yield return new MainMenuItem
            {
                Description = LocalizationManager.Instance.GetString(LOC.CommonCheckForGamesUpdatesButton),
                MenuSection = $"@{Instance.Name}",
                Icon = "UpdateDbIcon",
                Action = (args) =>
                {
                    if (!Nile.IsInstalled)
                    {
                        Nile.ShowNotInstalledError();
                        return;
                    }

                    var gamesUpdates = new Dictionary<string, UpdateInfo>();
                    NileUpdateController NileUpdateController = new NileUpdateController();
                    GlobalProgressOptions updateCheckProgressOptions = new GlobalProgressOptions(LocalizationManager.Instance.GetString(LOC.CommonCheckingForUpdates), false) { IsIndeterminate = true };
                    PlayniteApi.Dialogs.ActivateGlobalProgress(async (a) =>
                    {
                        gamesUpdates = await NileUpdateController.CheckAllGamesUpdates();
                    }, updateCheckProgressOptions);
                    if (gamesUpdates.Count > 0)
                    {
                        var successUpdates = gamesUpdates.Where(i => i.Value.Success).ToDictionary(i => i.Key, i => i.Value);
                        if (successUpdates.Count > 0)
                        {
                            Window window = PlayniteApi.Dialogs.CreateWindow(new WindowCreationOptions
                            {
                                ShowMaximizeButton = false,
                            });
                            window.DataContext = successUpdates;
                            window.Title = $"{LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteExtensionsUpdates)}";
                            window.Content = new NileUpdaterView();
                            window.Owner = PlayniteApi.Dialogs.GetCurrentAppWindow();
                            window.SizeToContent = SizeToContent.WidthAndHeight;
                            window.MinWidth = 600;
                            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                            window.ShowDialog();
                        }
                        else
                        {
                            PlayniteApi.Dialogs.ShowErrorMessage(LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteUpdateCheckFailMessage));
                        }
                    }
                    else
                    {
                        PlayniteApi.Dialogs.ShowMessage(LocalizationManager.Instance.GetString(LOC.CommonNoUpdatesAvailable));
                    }
                }
            };
            if (PlayniteApi.ApplicationInfo.Mode == ApplicationMode.Fullscreen)
            {
                yield return new MainMenuItem
                {
                    Description = LocalizationManager.Instance.GetString(LOC.CommonDownloadManager),
                    MenuSection = $"@{Instance.Name}",
                    Icon = "InstallIcon",
                    Action = (args) =>
                    {
                        Window window = PlayniteApi.Dialogs.CreateWindow(new WindowCreationOptions
                        {
                            ShowMaximizeButton = true,
                        });
                        window.Title = $"{LocalizationManager.Instance.GetString(LOC.CommonPanel)}";
                        window.Content = GetNileDownloadManager();
                        window.Owner = PlayniteApi.Dialogs.GetCurrentAppWindow();
                        window.SizeToContent = SizeToContent.WidthAndHeight;
                        window.ShowDialog();
                    }
                };
            }
        }
    }
}