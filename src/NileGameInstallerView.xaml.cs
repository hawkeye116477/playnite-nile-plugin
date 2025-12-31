using CommonPlugin;
using CommonPlugin.Enums;
using Linguini.Shared.Types.Bundle;
using NileLibraryNS.Models;
using NileLibraryNS.Services;
using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace NileLibraryNS
{
    /// <summary>
    /// Interaction logic for NileGameInstallerView.xaml
    /// </summary>
    public partial class NileGameInstallerView : UserControl
    {
        private ILogger logger = LogManager.GetLogger();
        private IPlayniteAPI playniteAPI = API.Instance;
        public string installCommand;
        public double downloadSizeNumber;
        public long availableFreeSpace;
        private GameDownloadInfo manifest;
        public DownloadManagerData.Download singleGameInstallData;

        public NileGameInstallerView()
        {
            InitializeComponent();
        }

        public Window InstallerWindow => Window.GetWindow(this);

        private List<DownloadManagerData.Download> MultiInstallData
        {
            get => (List<DownloadManagerData.Download>)DataContext;
            set { }
        }

        private void ChooseGamePathBtn_Click(object sender, RoutedEventArgs e)
        {
            var path = playniteAPI.Dialogs.SelectFolder();
            if (path != "")
            {
                SelectedGamePathTxt.Text = path;
                UpdateSpaceInfo(path);
            }
        }

        private void UpdateSpaceInfo(string path)
        {
            DriveInfo dDrive = new DriveInfo(path);
            if (dDrive.IsReady)
            {
                availableFreeSpace = dDrive.AvailableFreeSpace;
                SpaceTB.Text = CommonHelpers.FormatSize(availableFreeSpace);
            }
            UpdateAfterInstallingSize();
        }

        private void UpdateAfterInstallingSize()
        {
            double afterInstallSizeNumber = (double)(availableFreeSpace - downloadSizeNumber);
            if (afterInstallSizeNumber < 0)
            {
                afterInstallSizeNumber = 0;
            }
            AfterInstallingTB.Text = CommonHelpers.FormatSize(afterInstallSizeNumber);
        }

        public async Task StartTask(DownloadAction downloadAction)
        {
            var settings = NileLibrary.GetSettings();
            var installPath = SelectedGamePathTxt.Text;
            if (installPath == "")
            {
                installPath = Nile.GamesInstallationPath;
            }
            var playniteDirectoryVariable = ExpandableVariables.PlayniteDirectory.ToString();
            if (installPath.Contains(playniteDirectoryVariable))
            {
                installPath = installPath.Replace(playniteDirectoryVariable, playniteAPI.Paths.ApplicationPath);
            }
            InstallerWindow.Close();
            NileDownloadManagerView downloadManager = NileLibrary.GetNileDownloadManager();
            var downloadTasks = new List<DownloadManagerData.Download>();
            var downloadItemsAlreadyAdded = new List<string>();
            foreach (var installData in MultiInstallData)
            {
                var gameId = installData.gameID;
                var wantedItem = downloadManager.downloadManagerData.downloads.FirstOrDefault(item => item.gameID == gameId);
                if (wantedItem == null)
                {
                    if (installData.downloadProperties.downloadAction == DownloadAction.Install)
                    {
                        var folderName = installData.name;
                        string[] inappropriateDirChars = { ":", "/", "*", "?", "<", ">", "\\", "|", "™", "\"", "®" };
                        foreach (var inappropriateDirChar in inappropriateDirChars)
                        {
                            folderName = folderName.Replace(inappropriateDirChar, "");
                        }
                        installData.fullInstallPath = Path.Combine(installPath, folderName);
                    }
                    else if (!installData.downloadProperties.installPath.IsNullOrEmpty())
                    {
                        installPath = installData.downloadProperties.installPath;
                        installData.fullInstallPath = installPath;
                    }
                    if (!CommonHelpers.IsDirectoryWritable(installPath, LOC.CommonPermissionError))
                    {
                        continue;
                    }
                    var downloadProperties = GetDownloadProperties(installData, downloadAction, installPath);
                    installData.downloadProperties = downloadProperties;
                    downloadTasks.Add(installData);
                }
            }
            if (downloadItemsAlreadyAdded.Count > 0)
            {
                string downloadItemsAlreadyAddedCombined = downloadItemsAlreadyAdded[0];
                if (downloadItemsAlreadyAdded.Count > 1)
                {
                    downloadItemsAlreadyAddedCombined = string.Join(", ", downloadItemsAlreadyAdded.Select(item => item.ToString()));
                }
                playniteAPI.Dialogs.ShowMessage(LocalizationManager.Instance.GetString(LOC.CommonDownloadAlreadyExists, new Dictionary<string, IFluentType> { ["appName"] = (FluentString)downloadItemsAlreadyAddedCombined, ["count"] = (FluentNumber)downloadItemsAlreadyAdded.Count }), "", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            if (downloadTasks.Count > 0)
            {
                await downloadManager.EnqueueMultipleJobs(downloadTasks);
            }
        }

        private async void InstallBtn_Click(object sender, RoutedEventArgs e)
        {
            await StartTask(DownloadAction.Install);
        }

        private async void RepairBtn_Click(object sender, RoutedEventArgs e)
        {
            foreach (var installData in MultiInstallData)
            {
                installData.downloadSizeNumber = 0;
            }
            await StartTask(DownloadAction.Repair);
        }

        public DownloadProperties GetDownloadProperties(DownloadManagerData.Download installData, DownloadAction downloadAction, string installPath = "")
        {
            var settings = NileLibrary.GetSettings();
            int maxWorkers = settings.MaxWorkers;
            if (MaxWorkersNI.Value != "")
            {
                maxWorkers = int.Parse(MaxWorkersNI.Value);
            }
            var newDownloadProperties = new DownloadProperties();
            newDownloadProperties = Serialization.GetClone(installData.downloadProperties);
            newDownloadProperties.downloadAction = downloadAction;
            if (installPath != "")
            {
                newDownloadProperties.installPath = installPath;
            }
            newDownloadProperties.maxWorkers = maxWorkers;
            return newDownloadProperties;
        }

        private void CalculateTotalSize()
        {
            downloadSizeNumber = 0;
            foreach (var installData in MultiInstallData)
            {
                downloadSizeNumber += installData.downloadSizeNumber;
            }
            UpdateAfterInstallingSize();
            DownloadSizeTB.Text = CommonHelpers.FormatSize(downloadSizeNumber);
            InstallSizeTB.Text = CommonHelpers.FormatSize(downloadSizeNumber);
        }

        private async void NileGameInstallerUC_Loaded(object sender, RoutedEventArgs e)
        {
            CommonHelpers.SetControlBackground(this);
            if (MultiInstallData.First().downloadProperties.downloadAction == DownloadAction.Repair)
            {
                FolderDP.Visibility = Visibility.Collapsed;
                InstallBtn.Visibility = Visibility.Collapsed;
                RepairBtn.Visibility = Visibility.Visible;
                AfterInstallingSP.Visibility = Visibility.Collapsed;
            }
            var settings = NileLibrary.GetSettings();
            var installPath = Nile.GamesInstallationPath;
            var playniteDirectoryVariable = ExpandableVariables.PlayniteDirectory.ToString();
            if (installPath.Contains(playniteDirectoryVariable))
            {
                installPath = installPath.Replace(playniteDirectoryVariable, playniteAPI.Paths.ApplicationPath);
            }
            SelectedGamePathTxt.Text = installPath;
            UpdateSpaceInfo(installPath);
            var cacheInfoPath = NileLibrary.Instance.GetCachePath("infocache");
            if (!Directory.Exists(cacheInfoPath))
            {
                Directory.CreateDirectory(cacheInfoPath);
            }
            MaxWorkersNI.MaxValue = CommonHelpers.CpuThreadsNumber;
            MaxWorkersNI.Value = settings.MaxWorkers.ToString();
            var downloadItemsAlreadyAdded = new List<string>();
            downloadSizeNumber = 0;

            NileDownloadManagerView downloadManager = NileLibrary.GetNileDownloadManager();

            bool gamesListShouldBeDisplayed = false;


            var installedAppList = Nile.GetInstalledAppList();

            foreach (var installData in MultiInstallData.ToList())
            {
                manifest = await Nile.GetGameInfo(installData);
                if (manifest.errorDisplayed)
                {
                    gamesListShouldBeDisplayed = true;
                    MultiInstallData.Remove(installData);
                    continue;
                }
                installData.downloadSizeNumber = manifest.download_size;
                var wantedItem = downloadManager.downloadManagerData.downloads.FirstOrDefault(item => item.gameID == installData.gameID);
                if (wantedItem != null)
                {
                    if (wantedItem.status == DownloadStatus.Completed && installedAppList.FirstOrDefault(i => i.id == installData.gameID) == null)
                    {
                        downloadManager.downloadManagerData.downloads.Remove(wantedItem);
                    }
                    else
                    {
                        downloadItemsAlreadyAdded.Add(installData.name);
                        MultiInstallData.Remove(installData);
                    }
                }
            }

            CalculateTotalSize();
            if (downloadItemsAlreadyAdded.Count > 0)
            {
                string downloadItemsAlreadyAddedCombined = downloadItemsAlreadyAdded[0];
                if (downloadItemsAlreadyAdded.Count > 1)
                {
                    downloadItemsAlreadyAddedCombined = string.Join(", ", downloadItemsAlreadyAdded.Select(item => item.ToString()));
                }
                playniteAPI.Dialogs.ShowMessage(LocalizationManager.Instance.GetString(LOC.CommonDownloadAlreadyExists, new Dictionary<string, IFluentType> { ["appName"] = (FluentString)downloadItemsAlreadyAddedCombined, ["count"] = (FluentNumber)downloadItemsAlreadyAdded.Count }), "", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            var games = MultiInstallData;
            GamesLB.ItemsSource = games;
            if ((games.Count > 1 && singleGameInstallData == null) || gamesListShouldBeDisplayed)
            {
                GamesBrd.Visibility = Visibility.Visible;
            }

            var clientApi = new AmazonAccountClient(NileLibrary.Instance);
            var userLoggedIn = await clientApi.GetIsUserLoggedIn();
            if (games.Count <= 0 || !userLoggedIn)
            {
                if (!userLoggedIn)
                {
                    playniteAPI.Dialogs.ShowErrorMessage(LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteGameInstallError, new Dictionary<string, IFluentType> { ["var0"] = (FluentString)LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteLoginRequired) }));
                }
                InstallerWindow.Close();
                return;
            }
            if (downloadSizeNumber != 0)
            {
                InstallBtn.IsEnabled = true;
            }
            else if (games.First().downloadProperties.downloadAction != DownloadAction.Repair)
            {
                InstallerWindow.Close();
            }
            if (settings.UnattendedInstall && (games.First().downloadProperties.downloadAction == DownloadAction.Install))
            {
                await StartTask(DownloadAction.Install);
            }
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            Window.GetWindow(this).Close();
        }
    }
}
