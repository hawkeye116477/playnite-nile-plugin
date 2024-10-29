using CliWrap;
using CliWrap.Buffered;
using NileLibraryNS.Models;
using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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
                var heroicNileBinary = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                           @"Programs\heroic\resources\app.asar.unpacked\build\bin\x64\win32\nile.exe");
                if (File.Exists(heroicNileBinary))
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
                        if (Directory.Exists(savedLauncherPath))
                        {
                            launcherPath = savedLauncherPath;
                        }
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
                throw new Exception(ResourceProvider.GetString(LOC.NileNotInstalled));
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
                httpClient.DefaultRequestHeaders.Add("User-Agent", Nile.UserAgent);
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
    }
}
