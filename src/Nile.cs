﻿using CliWrap;
using CliWrap.Buffered;
using CommonPlugin;
using NileLibraryNS.Models;
using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace NileLibraryNS
{
    public class Nile
    {
        public static string UserAgent => @"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36";

        public static string ClientExecPath
        {
            get
            {
                var path = InstallationPath;
                return string.IsNullOrEmpty(path) ? string.Empty : path;
            }
        }

        public static bool IsInstalled
        {
            get
            {
                if (string.IsNullOrEmpty(InstallationPath) || !File.Exists(InstallationPath))
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
        }

        public static string InstallationPath
        {
            get
            {
                var launcherPath = "";
                var nileDefaultBinaryExe = "nile_windows_x86_64.exe";
                var nileShortBinaryExe = "nile.exe";

                var envPath = Environment.GetEnvironmentVariable("PATH")
                                         .Split(';')
                                         .Select(x => Path.Combine(x))
                                         .FirstOrDefault(x => File.Exists(Path.Combine(x, nileDefaultBinaryExe)));
                if (string.IsNullOrWhiteSpace(envPath))
                {
                    envPath = Environment.GetEnvironmentVariable("PATH")
                                         .Split(';')
                                         .Select(x => Path.Combine(x))
                                         .FirstOrDefault(x => File.Exists(Path.Combine(x, nileShortBinaryExe)));
                }

                var heroicNileBinary = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                           @"Programs\heroic\resources\app.asar.unpacked\build\bin\x64\win32\nile.exe");
                if (string.IsNullOrWhiteSpace(envPath) == false)
                {
                    launcherPath = envPath;
                }
                else if (File.Exists(heroicNileBinary))
                {
                    launcherPath = heroicNileBinary;
                }
                else
                {
                    var pf64 = Environment.GetEnvironmentVariable("ProgramW6432");
                    if (string.IsNullOrEmpty(pf64))
                    {
                        pf64 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                    }
                    launcherPath = Path.Combine(pf64, "Nile", "nile_windows_x86_64.exe");
                    if (!File.Exists(launcherPath))
                    {
                        var playniteAPI = API.Instance;
                        if (playniteAPI.ApplicationInfo.IsPortable)
                        {
                            launcherPath = Path.Combine(playniteAPI.Paths.ApplicationPath, "Nile", "nile_windows_x86_64.exe");
                        }
                    }
                }
                var savedSettings = NileLibrary.GetSettings();
                if (savedSettings != null)
                {
                    var savedLauncherPath = savedSettings.SelectedNilePath;
                    var playniteDirectoryVariable = ExpandableVariables.PlayniteDirectory.ToString();
                    if (savedLauncherPath != "")
                    {
                        if (savedLauncherPath.Contains(playniteDirectoryVariable))
                        {
                            var playniteAPI = API.Instance;
                            savedLauncherPath = savedLauncherPath.Replace(playniteDirectoryVariable, playniteAPI.Paths.ApplicationPath);
                        }
                        launcherPath = savedLauncherPath;
                    }
                }
                if (!File.Exists(launcherPath))
                {
                    launcherPath = "";
                }
                return launcherPath;
            }
        }

        public static string Icon => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Resources", @"icon.png");

        public static void StartClient()
        {
            ProcessStarter.StartProcess("cmd", $"/K {ClientExecPath} -h", Path.GetDirectoryName(ClientExecPath));
        }

        public static GameConfiguration GetGameConfiguration(string gameDir)
        {
            var configFile = Path.Combine(gameDir, GameConfiguration.ConfigFileName);
            if (File.Exists(configFile))
            {
                return Serialization.FromJsonFile<GameConfiguration>(configFile);
            }

            return null;
        }

        public static bool GetGameRequiresClient(GameConfiguration config)
        {
            return !config.Main.ClientId.IsNullOrEmpty() &&
                    config.Main.AuthScopes.HasItems();
        }

        public static string TokensPath
        {
            get
            {
                return Path.Combine(ConfigPath, "user.json");
            }
        }

        public static string ConfigPath
        {
            get
            {
                var nileConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "nile");
                var heroicNileConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "heroic", "nile_config", "nile");
                var originalNileInstallListPath = Path.Combine(nileConfigPath, "installed.json");
                var heroicNileInstallListPath = Path.Combine(heroicNileConfigPath, "installed.json");
                if (File.Exists(heroicNileInstallListPath))
                {
                    if (File.Exists(originalNileInstallListPath))
                    {
                        if (File.GetLastWriteTime(heroicNileInstallListPath) > File.GetLastWriteTime(originalNileInstallListPath))
                        {
                            nileConfigPath = heroicNileConfigPath;
                        }
                    }
                    else
                    {
                        nileConfigPath = heroicNileConfigPath;
                    }
                }
                var envNileConfigPath = Environment.GetEnvironmentVariable("NILE_CONFIG_PATH");
                if (!envNileConfigPath.IsNullOrWhiteSpace() && Directory.Exists(envNileConfigPath))
                {
                    nileConfigPath = envNileConfigPath;
                }
                return nileConfigPath;
            }
        }

        public static string GamesInstallationPath
        {
            get
            {
                var installPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Games");
                var playniteAPI = API.Instance;
                if (playniteAPI.ApplicationInfo.IsPortable)
                {
                    var playniteDirectoryVariable = ExpandableVariables.PlayniteDirectory.ToString();
                    installPath = Path.Combine(playniteDirectoryVariable, "Games");
                }
                var savedSettings = NileLibrary.GetSettings();
                if (savedSettings != null)
                {
                    var savedGamesInstallationPath = savedSettings.GamesInstallationPath;
                    if (savedGamesInstallationPath != "")
                    {
                        installPath = savedGamesInstallationPath;
                    }
                }
                return installPath;
            }
        }

        public static async Task<string> GetLauncherVersion()
        {
            var version = "0";
            if (IsInstalled)
            {
                var versionCmd = await Cli.Wrap(ClientExecPath)
                                          .WithArguments(new[] { "--version" })
                                          .AddCommandToLog()
                                          .WithValidation(CommandResultValidation.None)
                                          .ExecuteBufferedAsync();
                if (!versionCmd.StandardOutput.IsNullOrEmpty())
                {
                    version = Regex.Match(versionCmd.StandardOutput, @"\d+(\.\d+)+").Value;
                }
            }
            return version;
        }

        public static async Task<LauncherVersion> GetVersionInfoContent()
        {
            var newVersionInfoContent = new LauncherVersion();
            var logger = LogManager.GetLogger();
            if (!IsInstalled)
            {
                ShowNotInstalledError();
                return newVersionInfoContent;
            }
            var cacheVersionPath = NileLibrary.Instance.GetCachePath("infocache");
            if (!Directory.Exists(cacheVersionPath))
            {
                Directory.CreateDirectory(cacheVersionPath);
            }
            var cacheVersionFile = Path.Combine(cacheVersionPath, "nileVersion.json");
            string content = null;
            if (File.Exists(cacheVersionFile))
            {
                if (File.GetLastWriteTime(cacheVersionFile) < DateTime.Now.AddDays(-7))
                {
                    File.Delete(cacheVersionFile);
                }
            }
            if (!File.Exists(cacheVersionFile))
            {
                var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("User-Agent", UserAgent);
                var response = await httpClient.GetAsync("https://api.github.com/repos/imLinguin/nile/releases/latest");
                if (response.IsSuccessStatusCode)
                {
                    content = await response.Content.ReadAsStringAsync();
                    if (!Directory.Exists(cacheVersionPath))
                    {
                        Directory.CreateDirectory(cacheVersionPath);
                    }
                    File.WriteAllText(cacheVersionFile, content);
                }
                httpClient.Dispose();
            }
            else
            {
                content = FileSystem.ReadFileAsStringSafe(cacheVersionFile);
            }
            if (content.IsNullOrWhiteSpace())
            {
                logger.Error("An error occurred while downloading Nile's version info.");
            }
            else if (Serialization.TryFromJson(content, out LauncherVersion versionInfoContent))
            {
                newVersionInfoContent = versionInfoContent;
            }
            return newVersionInfoContent;
        }

        internal static void ClearCache()
        {
            var dataDir = NileLibrary.Instance.GetPluginUserDataPath();
            var cacheDir = Path.Combine(dataDir, "cache");
            if (Directory.Exists(cacheDir))
            {
                Directory.Delete(cacheDir, true);
            }
        }

        public static Dictionary<string, string> DefaultEnvironmentVariables
        {
            get
            {
                var envDict = new Dictionary<string, string>();
                var heroicNileConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "heroic", "nile_config", "nile");
                if (ConfigPath == heroicNileConfigPath)
                {
                    envDict.Add("NILE_CONFIG_PATH", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "heroic", "nile_config"));
                }
                return envDict;
            }
        }

        public static async Task<string> SyncLibIfNeeded(Game game)
        {
            string gameName = game.Name;
            var logger = LogManager.GetLogger();
            var playniteAPI = API.Instance;
            bool correctSyncJson = false;
            var nileLibSyncJsonPath = Path.Combine(ConfigPath, "library.json");
            var nileLibSyncJson = new List<NileLibraryFile.NileGames>();
            if (File.Exists(nileLibSyncJsonPath))
            {
                var nileLibyncJsonContent = FileSystem.ReadFileAsStringSafe(nileLibSyncJsonPath);
                if (!nileLibyncJsonContent.IsNullOrWhiteSpace() && Serialization.TryFromJson(nileLibyncJsonContent, out nileLibSyncJson))
                {
                    var wantedItem = nileLibSyncJson.FirstOrDefault(i => i.product.id == game.GameId);
                    if (wantedItem != null)
                    {
                        gameName = wantedItem.product.title.RemoveTrademarks();
                        correctSyncJson = true;
                    }
                    else
                    {
                        File.Delete(nileLibSyncJsonPath);
                    }
                }
            }
            if (!correctSyncJson)
            {
                BufferedCommandResult syncLibResult = await Cli.Wrap(ClientExecPath)
                                                               .WithArguments(new[] { "library", "sync" })
                                                               .WithEnvironmentVariables(DefaultEnvironmentVariables)
                                                               .AddCommandToLog()
                                                               .WithValidation(CommandResultValidation.None)
                                                               .ExecuteBufferedAsync();
                var syncErrorMessage = syncLibResult.StandardError;
                if (syncLibResult.ExitCode != 0 || syncErrorMessage.Contains("Error"))
                {
                    if (syncErrorMessage.Contains("not logged in"))
                    {
                        playniteAPI.Dialogs.ShowErrorMessage(ResourceProvider.GetString(LOC.Nile3P_PlayniteMetadataDownloadError).Format(ResourceProvider.GetString(LOC.Nile3P_PlayniteLoginRequired)), gameName);
                    }
                    else
                    {
                        playniteAPI.Dialogs.ShowErrorMessage(ResourceProvider.GetString(LOC.Nile3P_PlayniteMetadataDownloadError).Format(ResourceProvider.GetString(LOC.NileCheckLog)), gameName);
                    }
                    logger.Error(syncErrorMessage);
                    return gameName;
                }
                else
                {
                    var nileLibyncJsonContent = FileSystem.ReadFileAsStringSafe(nileLibSyncJsonPath);
                    var nileLibyncJson = Serialization.FromJson<List<NileLibraryFile.NileGames>>(nileLibyncJsonContent);
                    var wantedItem = nileLibSyncJson.FirstOrDefault(i => i.product.id == game.GameId);
                    if (wantedItem != null)
                    {
                        gameName = wantedItem.product.title.RemoveTrademarks();
                    }
                }
            }
            return gameName;
        }

        public static async Task<GameDownloadInfo> GetGameInfo(DownloadManagerData.Download gameData, bool skipRefreshing = false, bool silently = false, bool forceRefreshCache = false)
        {
            var gameID = gameData.gameID;
            var manifest = new GameDownloadInfo();
            var playniteAPI = API.Instance;
            var logger = LogManager.GetLogger();
            var cacheInfoPath = NileLibrary.Instance.GetCachePath("infocache");
            var cacheInfoFile = Path.Combine(cacheInfoPath, gameID + ".json");
            if (!Directory.Exists(cacheInfoPath))
            {
                Directory.CreateDirectory(cacheInfoPath);
            }
            bool correctJson = false;
            if (File.Exists(cacheInfoFile))
            {
                if (!skipRefreshing)
                {
                    if (File.GetLastWriteTime(cacheInfoFile) < DateTime.Now.AddDays(-7) || forceRefreshCache)
                    {
                        var metadataFile = Path.Combine(ConfigPath, "metadata", gameID + ".json");
                        if (File.Exists(metadataFile))
                        {
                            File.Delete(metadataFile);
                        }
                        File.Delete(cacheInfoFile);
                    }
                }
            }
            if (File.Exists(cacheInfoFile))
            {
                var content = FileSystem.ReadFileAsStringSafe(cacheInfoFile);
                if (!content.IsNullOrWhiteSpace() && Serialization.TryFromJson(content, out manifest))
                {
                    if (manifest != null && manifest.download_size != 0)
                    {
                        correctJson = true;
                    }
                }
            }
            if (!correctJson)
            {
                var game = new Game
                {
                    GameId = gameData.gameID,
                    Name = gameData.name
                };
                manifest.title = await SyncLibIfNeeded(game);
                BufferedCommandResult result = await Cli.Wrap(ClientExecPath)
                                      .WithArguments(new[] { "install", gameID, "--info", "--json" })
                                      .WithEnvironmentVariables(DefaultEnvironmentVariables)
                                      .AddCommandToLog()
                                      .WithValidation(CommandResultValidation.None)
                                      .ExecuteBufferedAsync();
                var errorMessage = result.StandardError;
                if (result.ExitCode != 0 || errorMessage.Contains("ERROR") || errorMessage.Contains("CRITICAL") || errorMessage.Contains("Error"))
                {
                    logger.Error(result.StandardError);
                    if (!silently)
                    {
                        if (result.StandardError.Contains("not logged in"))
                        {
                            playniteAPI.Dialogs.ShowErrorMessage(ResourceProvider.GetString(LOC.Nile3P_PlayniteMetadataDownloadError).Format(ResourceProvider.GetString(LOC.Nile3P_PlayniteLoginRequired)), gameData.name);
                        }
                        else
                        {
                            playniteAPI.Dialogs.ShowErrorMessage(ResourceProvider.GetString(LOC.Nile3P_PlayniteMetadataDownloadError).Format(ResourceProvider.GetString(LOC.NileCheckLog)), gameData.name);
                        }
                    }
                    manifest.errorDisplayed = true;
                }
                else
                {
                    var newManifest = Serialization.FromJson<GameDownloadInfo>(result.StandardOutput);
                    manifest.download_size = newManifest.download_size;
                    File.WriteAllText(cacheInfoFile, Serialization.ToJson(manifest));
                }
            }
            return manifest;
        }

        public static void MigrateAmazonManifest(string installDirectory, string gameId)
        {
            var parentDirectory = Directory.GetParent(installDirectory).FullName;
            var folderName = new DirectoryInfo(installDirectory).Name;
            var installDataDir = Path.Combine(parentDirectory, "__InstallData__", folderName);
            var manifestsDir = Path.Combine(installDataDir, "Manifests");
            var nileManifestsPath = Path.Combine(ConfigPath, "manifests");
            if (!File.Exists(Path.Combine(nileManifestsPath, $"{gameId}.raw")))
            {
                if (Directory.Exists(manifestsDir))
                {
                    string[] filePaths = Directory.GetFiles(manifestsDir, "*.manifest");
                    var manifestPath = filePaths[0];
                    if (!manifestPath.IsNullOrEmpty())
                    {
                        if (!Directory.Exists(nileManifestsPath))
                        {
                            Directory.CreateDirectory(nileManifestsPath);
                        }
                        File.Copy(manifestPath, Path.Combine(nileManifestsPath, $"{gameId}.raw"));
                    }
                }
            }
        }


        public static async Task AddGameToInstalledList(Game game)
        {
            await SyncLibIfNeeded(game);
            var installListPath = Path.Combine(ConfigPath, "installed.json");
            var installedList = new List<InstalledGames.Installed>();
            if (File.Exists(installListPath))
            {
                var installListContent = FileSystem.ReadFileAsStringSafe(installListPath);
                if (!installListContent.IsNullOrWhiteSpace())
                {
                    installedList = Serialization.FromJson<List<InstalledGames.Installed>>(installListContent);
                }
            }
            var folderName = new DirectoryInfo(game.InstallDirectory).Name;
            var parentDirectory = Directory.GetParent(game.InstallDirectory).FullName;
            var installDataDir = Path.Combine(parentDirectory, "__InstallData__", folderName);
            var installDataFile = Path.Combine(installDataDir, "product_data.json");

            MigrateAmazonManifest(game.InstallDirectory, game.GameId);

            if (File.Exists(installDataFile))
            {
                var installDataFileContent = FileSystem.ReadFileAsStringSafe(installDataFile);
                if (!installDataFileContent.IsNullOrWhiteSpace())
                {
                    var installDataJson = Serialization.FromJson<AmazonProductData>(installDataFileContent);
                    game.Version = installDataJson.InstalledVersion;
                }
            }
            if (installedList.FirstOrDefault(i => i.id == game.GameId) == null)
            {
                double gameSize = 0;
                if (game.InstallSize != null)
                {
                    gameSize = (double)game.InstallSize;
                }
                else
                {
                    gameSize = FileSystem.GetDirectorySize(game.InstallDirectory, false);
                }
                var installedInfo = new InstalledGames.Installed
                {
                    id = game.GameId,
                    path = game.InstallDirectory,
                    size = gameSize,
                    version = game.Version
                };
                installedList.Add(installedInfo);
            }
            var commonHelpers = NileLibrary.Instance.commonHelpers;
            commonHelpers.SaveJsonSettingsToFile(installedList, ConfigPath, "installed");
        }

        public static List<InstalledGames.Installed> GetInstalledAppList()
        {
            var installListPath = Path.Combine(ConfigPath, "installed.json");
            var list = new List<InstalledGames.Installed>();
            if (File.Exists(installListPath))
            {
                var content = FileSystem.ReadFileAsStringSafe(installListPath);
                if (!content.IsNullOrWhiteSpace() && Serialization.TryFromJson(content, out List<InstalledGames.Installed> nonEmptyList))
                {
                    list = nonEmptyList;
                }
            }
            return list;
        }

        public static void ShowNotInstalledError()
        {
            var playniteAPI = API.Instance;
            var options = new List<MessageBoxOption>
            {
                new MessageBoxOption(ResourceProvider.GetString(LOC.Nile3P_PlayniteInstallGame)),
                new MessageBoxOption(ResourceProvider.GetString(LOC.Nile3P_PlayniteOKLabel)),
            };
            var result = playniteAPI.Dialogs.ShowMessage(ResourceProvider.GetString(LOC.NileLauncherNotInstalled), "Nile (Amazon Games) library integration", MessageBoxImage.Information, options);
            if (result == options[0])
            {
                Playnite.Commands.GlobalCommands.NavigateUrl("https://github.com/hawkeye116477/playnite-nile-plugin/wiki/Troubleshooting#nile-is-not-installed");
            }
        }
    }
}
