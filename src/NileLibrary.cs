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
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using UnifiedDownloadManagerApiNS;
using UnifiedDownloadManagerApiNS.Interfaces;
using UnifiedDownloadManagerApiNS.Models;

namespace NileLibraryNS
{
    [LoadPlugin]
    public class NileLibrary : LibraryPluginBase<NileLibrarySettingsViewModel>, IUnifiedDownloadProvider
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        public static NileLibrary Instance { get; set; }
        public CommonHelpers commonHelpers { get; set; }
        public IUnifiedDownloadLogic UnifiedDownloadLogic { get; set; }
        public DownloadManagerData pluginDownloadData { get; set; }

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
            UnifiedDownloadLogic = new NileDownloadLogic();
            pluginDownloadData = LoadSavedDownloadData();
        }

        public DownloadManagerData LoadSavedDownloadData()
        {
            var dataDir = Instance.GetPluginUserDataPath();
            var dataFile = Path.Combine(dataDir, "downloads.json");
            bool correctJson = false;
            if (File.Exists(dataFile))
            {
                var content = FileSystem.ReadFileAsStringSafe(dataFile);
                if (!content.IsNullOrWhiteSpace() && Serialization.TryFromJson(content, out DownloadManagerData newPluginDownloadData))
                {
                    if (newPluginDownloadData != null && newPluginDownloadData.downloads != null)
                    {
                        correctJson = true;
                        pluginDownloadData = newPluginDownloadData;
                    }
                }
            }
            if (!correctJson)
            {
                pluginDownloadData = new DownloadManagerData
                {
                    downloads = new ObservableCollection<DownloadManagerData.Download>()
                };
            }
            return pluginDownloadData;
        }

        public void SaveDownloadData()
        {
            commonHelpers.SaveJsonSettingsToFile(pluginDownloadData, "", "downloads", true);
        }

        public void MigrateOldDownloadData()
        {
            var oldPluginDownloadDataForMigration = new OldDownloadManagerData();
            var dataDir = Instance.GetPluginUserDataPath();
            var oldDataFile = Path.Combine(dataDir, "downloadManager.json");
            var oldDataBackupFile = Path.Combine(dataDir, "downloadManager.json.migrated");

            bool udmInstalled = PlayniteApi.Addons.Plugins.Any(plugin => plugin.Id.Equals(UnifiedDownloadManagerSharedProperties.Id));
            if (File.Exists(oldDataFile) && udmInstalled)
            {
                GlobalProgressOptions globalProgressOptions = new GlobalProgressOptions(LocalizationManager.Instance.GetString(LOC.CommonMigratingData), false) { IsIndeterminate = true };
                PlayniteApi.Dialogs.ActivateGlobalProgress(async (a) =>
                {
                    await PlayniteApi.MainView.UIDispatcher.InvokeAsync(async () =>
                    {
                        logger.Debug("Migrating old downloads data...");
                        var content = FileSystem.ReadFileAsStringSafe(oldDataFile);
                        if (!content.IsNullOrWhiteSpace() && Serialization.TryFromJson(content, out OldDownloadManagerData oldPluginDownloadData))
                        {
                            if (oldPluginDownloadData != null && oldPluginDownloadData.downloads != null)
                            {
                                oldPluginDownloadDataForMigration = oldPluginDownloadData;
                            }
                        }
                        var nileDownloadLogic = (NileDownloadLogic)Instance.UnifiedDownloadLogic;
                        var oldData = oldPluginDownloadDataForMigration.downloads;
                        var unifiedTasks = new List<UnifiedDownload>();
                        foreach (var oldDownload in oldData)
                        {
                            if (oldDownload.status == DownloadStatus.Running || oldDownload.status == DownloadStatus.Queued)
                            {
                                oldDownload.status = DownloadStatus.Paused;
                            }
                            var newPluginTask = new DownloadManagerData.Download
                            {
                                addedTime = oldDownload.addedTime,
                                completedTime = oldDownload.completedTime,
                                downloadedNumber = oldDownload.downloadedNumber,
                                downloadProperties = Serialization.GetClone(oldDownload.downloadProperties),
                                downloadSizeNumber = oldDownload.downloadSizeNumber,
                                fullInstallPath = oldDownload.fullInstallPath,
                                gameID = oldDownload.gameID,
                                name = oldDownload.name,
                                progress = oldDownload.progress,
                                status = oldDownload.status
                            };
                            Instance.pluginDownloadData.downloads.Add(newPluginTask);
                            var unifiedTask = new UnifiedDownload
                            {
                                gameID = oldDownload.gameID,
                                name = oldDownload.name,
                                downloadSizeBytes = oldDownload.downloadSizeNumber,
                                installSizeBytes = oldDownload.downloadSizeNumber,
                                fullInstallPath = oldDownload.fullInstallPath,
                                pluginId = Instance.Id.ToString(),
                                sourceName = "Amazon",
                                addedTime = oldDownload.addedTime,
                            };
                            unifiedTask.status = (UnifiedDownloadStatus)oldDownload.status;
                            unifiedTask.progress = oldDownload.progress;
                            unifiedTask.downloadedBytes = oldDownload.downloadedNumber;
                            unifiedTask.completedTime = oldDownload.completedTime;
                            unifiedTasks.Add(unifiedTask);
                        }
                        UnifiedDownloadManagerApi unifiedDownloadManagerApi = new UnifiedDownloadManagerApi();
                        await unifiedDownloadManagerApi.AddTasks(unifiedTasks);
                        Instance.SaveDownloadData();
                        File.Move(oldDataFile, oldDataBackupFile);
                        logger.Debug("Migration done.");
                    });
                }, globalProgressOptions);
            }
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

        public bool StopDownloadManager(bool displayConfirm = false)
        {
            var unifiedDownloadManagerApi = new UnifiedDownloadManagerApi();
            var allDownloads = unifiedDownloadManagerApi.GetAllDownloads();
            var runningAndQueuedDownloads = allDownloads.Where(i => i.status == UnifiedDownloadStatus.Running || i.status == UnifiedDownloadStatus.Queued).ToList();
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
                unifiedDownloadManagerApi.PauseAllTasks(Instance.Id.ToString());
            }
            return true;
        }

        public override async void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            MigrateOldDownloadData();
            var globalSettings = GetSettings();
            if (globalSettings != null)
            {
                if (globalSettings.GamesUpdatePolicy != UpdatePolicy.Never)
                {
                    var nextGamesUpdateTime = globalSettings.NextGamesUpdateTime;
                    bool udmInstalled = PlayniteApi.Addons.Plugins.Any(plugin => plugin.Id.Equals(UnifiedDownloadManagerSharedProperties.Id));
                    if (nextGamesUpdateTime != 0 && udmInstalled)
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
                            await Nile.CheckForLauncherUpdates(false);
                        }
                    }

                }
            }
        }

        public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
        {
            var settings = GetSettings();
            if (settings != null)
            {
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
                SaveDownloadData();
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
        }
    }
}