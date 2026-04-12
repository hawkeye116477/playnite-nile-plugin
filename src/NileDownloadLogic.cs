using CliWrap;
using CliWrap.EventStream;
using CommonPlugin;
using CommonPlugin.Enums;
using Linguini.Shared.Types.Bundle;
using NileLibraryNS.Models;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using UnifiedDownloadManagerApiNS;
using UnifiedDownloadManagerApiNS.Interfaces;
using UnifiedDownloadManagerApiNS.Models;

namespace NileLibraryNS
{
    public class NileDownloadLogic : IUnifiedDownloadLogic
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private IPlayniteAPI playniteAPI = API.Instance;
        public bool downloadsChanged = false;

        public async Task AddTasks(List<DownloadManagerData.Download> downloadTasks, bool migrate = false)
        {
            var unifiedTasks = new List<UnifiedDownload>();
            foreach (var downloadTask in downloadTasks)
            {
                NileLibrary.Instance.pluginDownloadData.downloads.Add(downloadTask);
                var unifiedTask = new UnifiedDownload
                {
                    gameID = downloadTask.gameID,
                    name = downloadTask.name,
                    downloadSizeBytes = downloadTask.downloadSizeNumber,
                    installSizeBytes = downloadTask.downloadSizeNumber,
                    pluginId = NileLibrary.Instance.Id.ToString(),
                    fullInstallPath = downloadTask.fullInstallPath,
                    sourceName = "Amazon",
                };
                if (migrate)
                {
                    unifiedTask.addedTime = downloadTask.addedTime;
                    unifiedTask.status = (UnifiedDownloadStatus)downloadTask.status;
                    unifiedTask.progress = downloadTask.progress;
                    unifiedTask.downloadedBytes = downloadTask.downloadedNumber;
                    unifiedTask.completedTime = downloadTask.completedTime;
                }
                unifiedTasks.Add(unifiedTask);
            }
            UnifiedDownloadManagerApi unifiedDownloadManagerApi = new UnifiedDownloadManagerApi();
            await unifiedDownloadManagerApi.AddTasks(unifiedTasks);
            NileLibrary.Instance.SaveDownloadData();
        }

        public async Task OnCancelDownload(UnifiedDownload downloadTask)
        {
            var matchingPluginTask = NileLibrary.Instance.pluginDownloadData.downloads.FirstOrDefault(t => t.gameID == downloadTask.gameID);
            var resumeFile = Path.Combine(Nile.ConfigPath, "tmp", downloadTask.gameID + ".resume");
            var repairFile = Path.Combine(Nile.ConfigPath, "tmp", downloadTask.gameID + ".repair");

            const int maxRetries = 5;
            int delayMs = 100;
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    if (File.Exists(resumeFile))
                    {
                        File.Delete(resumeFile);
                    }

                    if (File.Exists(repairFile))
                    {
                        File.Delete(repairFile);
                    }
                    if (downloadTask.fullInstallPath != null && matchingPluginTask.downloadProperties.downloadAction == DownloadAction.Install)
                    {
                        if (Directory.Exists(matchingPluginTask.fullInstallPath))
                        {
                            Directory.Delete(matchingPluginTask.fullInstallPath, true);
                        }
                    }
                }
                catch (Exception rex)
                {
                    if (i < maxRetries - 1)
                    {
                        await Task.Delay(delayMs);
                        delayMs *= 2;
                    }
                    else
                    {
                        logger.Warn(rex, $"Can't cleanup after cancellation. Please try removing files manually.");
                        break;
                    }
                }
            }
        }

        public Task OnRemoveDownloadEntry(UnifiedDownload downloadTask)
        {
            var matchingPluginTask = NileLibrary.Instance.pluginDownloadData.downloads.FirstOrDefault(t => t.gameID == downloadTask.gameID);
            if (matchingPluginTask != null)
            {
                downloadsChanged = true;
                NileLibrary.Instance.pluginDownloadData.downloads.Remove(matchingPluginTask);
                NileLibrary.Instance.SaveDownloadData();
            }
            return Task.CompletedTask;
        }

        public async Task StartDownload(UnifiedDownload downloadTask)
        {
            UnifiedDownloadManagerApi unifiedDownloadManagerApi = new UnifiedDownloadManagerApi();
            var installCommand = new List<string>();
            var settings = NileLibrary.GetSettings();
            var gameID = downloadTask.gameID;
            var matchingPluginTask = NileLibrary.Instance.pluginDownloadData.downloads.FirstOrDefault(t => t.gameID == gameID);
            var wantedUnifiedTask = unifiedDownloadManagerApi.GetTask(gameID, NileLibrary.Instance.Id.ToString());
            var downloadProperties = matchingPluginTask.downloadProperties;
            var gameTitle = downloadTask.name;
            double cachedDownloadSizeNumber = wantedUnifiedTask.downloadSizeBytes;
            double downloadCache = 0;
            if (downloadProperties.downloadAction == DownloadAction.Install)
            {
                installCommand.Add("install");
            }
            if (downloadProperties.downloadAction == DownloadAction.Repair)
            {
                Nile.MigrateAmazonManifest(matchingPluginTask.fullInstallPath, matchingPluginTask.gameID);
                installCommand.Add("verify");
            }
            if (downloadProperties.downloadAction == DownloadAction.Update)
            {
                installCommand.Add("update");
            }
            installCommand.Add(gameID);

            if (!downloadTask.fullInstallPath.IsNullOrEmpty())
            {
                installCommand.AddRange(new[] { "--path", matchingPluginTask.fullInstallPath });
            }

            if (downloadProperties.maxWorkers != 0)
            {
                installCommand.AddRange(new[] { "--max-workers", downloadProperties.maxWorkers.ToString() });
            }

            // We changing tokens to workaround that Nile doesn't support cancelation, so we need to force it :-)
            var gracefulInstallerCTS = wantedUnifiedTask.forcefulCts;
            var forcefulInstallerCTS = wantedUnifiedTask.gracefulCts;
            try
            {
                bool errorDisplayed = false;
                bool successDisplayed = false;
                bool loginErrorDisplayed = false;
                string memoryErrorMessage = "";
                bool permissionErrorDisplayed = false;
                bool diskSpaceErrorDisplayed = false;
                var cmd = Cli.Wrap(Nile.ClientExecPath)
                             .WithEnvironmentVariables(await Nile.GetDefaultEnvironmentVariables())
                             .WithArguments(installCommand)
                             .AddCommandToLog()
                             .WithValidation(CommandResultValidation.None);
                await foreach (CommandEvent cmdEvent in cmd.ListenAsync(Console.OutputEncoding, Console.OutputEncoding, forcefulInstallerCTS.Token, gracefulInstallerCTS.Token))
                {
                    switch (cmdEvent)
                    {
                        case StartedCommandEvent started:
                            wantedUnifiedTask.status = UnifiedDownloadStatus.Running;
                            break;
                        case StandardErrorCommandEvent stdErr:
                            if (stdErr.Text.Contains("Verification") || stdErr.Text.Contains("Verifying"))
                            {
                                wantedUnifiedTask.activity = LocalizationManager.Instance.GetString(LOC.CommonVerifying);
                            }
                            var progressMatch = Regex.Match(stdErr.Text, @"Progress: (\d+\.\d+)");
                            if (progressMatch.Length >= 2)
                            {
                                if (downloadProperties.downloadAction != DownloadAction.Update)
                                {
                                    wantedUnifiedTask.activity = LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteDownloadingLabel);
                                }
                                else
                                {
                                    wantedUnifiedTask.activity = LocalizationManager.Instance.GetString(LOC.CommonDownloadingUpdate);
                                }
                                double progress = CommonHelpers.ToDouble(progressMatch.Groups[1].Value);
                                wantedUnifiedTask.progress = progress;
                            }
                            var elapsedMatch = Regex.Match(stdErr.Text, @"Running for: (\d\d:\d\d:\d\d)");
                            if (elapsedMatch.Length >= 2)
                            {
                                wantedUnifiedTask.elapsed = TimeSpan.Parse(elapsedMatch.Groups[1].Value);
                            }
                            var ETAMatch = Regex.Match(stdErr.Text, @"ETA: (\d\d:\d\d:\d\d)");
                            if (ETAMatch.Length >= 2)
                            {
                                wantedUnifiedTask.eta = TimeSpan.Parse(ETAMatch.Groups[1].Value);
                            }
                            var downloadedMatch = Regex.Match(stdErr.Text, @"Downloaded: (\S+) (\wiB)");
                            if (downloadedMatch.Length >= 2)
                            {
                                double downloadedNumber = CommonHelpers.ToBytes(CommonHelpers.ToDouble(downloadedMatch.Groups[1].Value), downloadedMatch.Groups[2].Value);
                                double totalDownloadedNumber = downloadedNumber + downloadCache;
                                wantedUnifiedTask.downloadedBytes = totalDownloadedNumber;
                                //double newProgress = totalDownloadedNumber / wantedItem.downloadSizeNumber * 100;
                                //wantedItem.progress = newProgress;
                                //NilePanel.ProgressValue = newProgress;

                                if (totalDownloadedNumber == wantedUnifiedTask.downloadSizeBytes)
                                {
                                    switch (downloadProperties.downloadAction)
                                    {
                                        case DownloadAction.Install:
                                            wantedUnifiedTask.activity = LocalizationManager.Instance.GetString(LOC.CommonFinishingInstallation);
                                            break;
                                        case DownloadAction.Update:
                                            wantedUnifiedTask.activity = LocalizationManager.Instance.GetString(LOC.CommonFinishingUpdate);
                                            break;
                                        case DownloadAction.Repair:
                                            wantedUnifiedTask.activity = LocalizationManager.Instance.GetString(LOC.CommonFinishingRepair);
                                            break;
                                        default:
                                            break;
                                    }
                                }
                            }
                            var downloadSpeedMatch = Regex.Match(stdErr.Text, @"Download\t- (\S+) (\wiB)");
                            if (downloadSpeedMatch.Length >= 2)
                            {
                                wantedUnifiedTask.downloadSpeedBytes = CommonHelpers.ToBytes(CommonHelpers.ToDouble(downloadSpeedMatch.Groups[1].Value), downloadSpeedMatch.Groups[2].Value);
                            }
                            var diskSpeedMatch = Regex.Match(stdErr.Text, @"Disk\t- (\S+) (\wiB)");
                            if (diskSpeedMatch.Length >= 2)
                            {
                                wantedUnifiedTask.diskWriteSpeedBytes = CommonHelpers.ToBytes(CommonHelpers.ToDouble(diskSpeedMatch.Groups[1].Value), diskSpeedMatch.Groups[2].Value);
                            }
                            var errorMessage = stdErr.Text;
                            if (errorMessage.Contains("finished") || errorMessage.Contains("Finished") || errorMessage.Contains("already up to date"))
                            {
                                successDisplayed = true;
                            }
                            else if (errorMessage.Contains("WARNING") && !errorMessage.Contains("exit requested") && !errorMessage.Contains("PermissionError"))
                            {
                                logger.Warn($"[Nile] {errorMessage}");
                            }
                            else if (errorMessage.Contains("ERROR") || errorMessage.Contains("CRITICAL") || errorMessage.Contains("Error") || errorMessage.Contains("Failure"))
                            {
                                logger.Error($"[Nile] {errorMessage}");
                                if (errorMessage.Contains("not logged in"))
                                {
                                    loginErrorDisplayed = true;
                                }
                                else if (errorMessage.Contains("MemoryError"))
                                {
                                    memoryErrorMessage = errorMessage;
                                }
                                else if (errorMessage.Contains("PermissionError"))
                                {
                                    permissionErrorDisplayed = true;
                                }
                                else if (errorMessage.Contains("Not enough available disk space"))
                                {
                                    diskSpaceErrorDisplayed = true;
                                }
                                if (!errorMessage.Contains("old manifest"))
                                {
                                    errorDisplayed = true;
                                }
                            }
                            break;
                        case ExitedCommandEvent exited:
                            if ((!successDisplayed && errorDisplayed) || exited.ExitCode != 0)
                            {
                                if (loginErrorDisplayed)
                                {
                                    playniteAPI.Dialogs.ShowErrorMessage(LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteGameInstallError, new Dictionary<string, IFluentType> { ["var0"] = (FluentString)LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteLoginRequired) }));
                                }
                                else if (permissionErrorDisplayed)
                                {
                                    playniteAPI.Dialogs.ShowErrorMessage(LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteGameInstallError, new Dictionary<string, IFluentType> { ["var0"] = (FluentString)LocalizationManager.Instance.GetString(LOC.CommonPermissionError) }));
                                }
                                else if (diskSpaceErrorDisplayed)
                                {
                                    playniteAPI.Dialogs.ShowErrorMessage(LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteGameInstallError, new Dictionary<string, IFluentType> { ["var0"] = (FluentString)LocalizationManager.Instance.GetString(LOC.CommonNotEnoughSpace) }));
                                }
                                else
                                {
                                    playniteAPI.Dialogs.ShowErrorMessage(LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteGameInstallError, new Dictionary<string, IFluentType> { ["var0"] = (FluentString)LocalizationManager.Instance.GetString(LOC.CommonCheckLog) }));
                                }
                                wantedUnifiedTask.status = UnifiedDownloadStatus.Error;
                            }
                            else
                            {
                                var installedAppList = Nile.GetInstalledAppList();
                                if (installedAppList != null)
                                {
                                    if (installedAppList.FirstOrDefault(i => i.id == gameID) != null)
                                    {
                                        var installedGameInfo = installedAppList.FirstOrDefault(i => i.id == gameID);
                                        Playnite.SDK.Models.Game game = new Playnite.SDK.Models.Game();
                                        game = playniteAPI.Database.Games.FirstOrDefault(item => item.PluginId == NileLibrary.Instance.Id && item.GameId == gameID);
                                        game.InstallDirectory = installedGameInfo.path;
                                        game.Version = installedGameInfo.version;
                                        game.InstallSize = (ulong?)installedGameInfo.size;
                                        game.IsInstalled = true;
                                        playniteAPI.Database.Games.Update(game);
                                    }
                                }
                                wantedUnifiedTask.status = UnifiedDownloadStatus.Completed;
                                wantedUnifiedTask.progress = 100;
                                DateTimeOffset now = DateTime.UtcNow;
                                wantedUnifiedTask.completedTime = now.ToUnixTimeSeconds();
                            }
                            gracefulInstallerCTS?.Dispose();
                            forcefulInstallerCTS?.Dispose();
                            break;
                        default:
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex is OperationCanceledException && (downloadTask.status == UnifiedDownloadStatus.Canceled || downloadTask.status == UnifiedDownloadStatus.Paused))
                {
                    if (downloadTask.status == UnifiedDownloadStatus.Canceled)
                    {
                        await OnCancelDownload(downloadTask);
                    }
                }
                else
                {
                    logger.Debug($"An error occured during downloading {downloadTask.name}: {ex.Message}");
                    downloadTask.status = UnifiedDownloadStatus.Error;
                }
            }
            finally
            {
                NileLibrary.Instance.SaveDownloadData();
            }
        }

        public void OpenDownloadPropertiesWindow(UnifiedDownload selectedEntry)
        {
            var window = playniteAPI.Dialogs.CreateWindow(new WindowCreationOptions
            {
                ShowMaximizeButton = false,
            });
            var matchingPluginTask = NileLibrary.Instance.pluginDownloadData.downloads.FirstOrDefault(t => t.gameID == selectedEntry.gameID);
            window.Title = selectedEntry.name + " — " + LocalizationManager.Instance.GetString(LOC.CommonDownloadProperties);
            window.DataContext = matchingPluginTask;
            window.Content = new NileDownloadProperties();
            window.Owner = playniteAPI.Dialogs.GetCurrentAppWindow();
            window.SizeToContent = SizeToContent.WidthAndHeight;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            window.ShowDialog();
        }

        public static bool CheckIfUdmInstalled()
        {
            var playniteAPI = API.Instance;
            bool installed = playniteAPI.Addons.Plugins.Any(plugin => plugin.Id.Equals(UnifiedDownloadManagerSharedProperties.Id));
            if (!installed)
            {
                var options = new List<MessageBoxOption>
                {
                    new MessageBoxOption(LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteInstallGame)),
                    new MessageBoxOption(LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteOkLabel)),
                };
                var result = playniteAPI.Dialogs.ShowMessage(LocalizationManager.Instance.GetString(LOC.CommonLauncherNotInstalled, new Dictionary<string, IFluentType> { ["launcherName"] = (FluentString)"Unified Download Manager" }), "Nile (Amazon Games) library integration", MessageBoxImage.Information, options);
                if (result == options[0])
                {
                    Playnite.Commands.GlobalCommands.NavigateUrl("playnite://playnite/installaddon/UnifiedDownloadManager");
                }
            }
            return installed;
        }
    }
}
