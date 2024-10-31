using NileLibraryNS.Enums;
using NileLibraryNS.Models;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace NileLibraryNS
{
    /// <summary>
    /// Interaction logic for NileDownloadProperties.xaml
    /// </summary>
    public partial class NileDownloadProperties : UserControl
    {
        private DownloadManagerData.Download SelectedDownload => (DownloadManagerData.Download)DataContext;
        public DownloadManagerData downloadManagerData;
        private IPlayniteAPI playniteAPI = API.Instance;
        public List<string> requiredThings;

        public NileDownloadProperties()
        {
            InitializeComponent();
            LoadSavedData();
        }

        private DownloadManagerData LoadSavedData()
        {
            var downloadManager = NileLibrary.GetNileDownloadManager();
            downloadManagerData = downloadManager.downloadManagerData;
            return downloadManagerData;
        }

        private void NileDownloadPropertiesUC_Loaded(object sender, RoutedEventArgs e)
        {
            MaxWorkersNI.MaxValue = Helpers.CpuThreadsNumber;
            var wantedItem = SelectedDownload;
            if (wantedItem.downloadProperties != null)
            {
                SelectedGamePathTxt.Text = wantedItem.downloadProperties.installPath;
                MaxWorkersNI.Value = wantedItem.downloadProperties.maxWorkers.ToString();
                TaskCBo.SelectedValue = wantedItem.downloadProperties.downloadAction;
            }
            var downloadActionOptions = new Dictionary<DownloadAction, string>
            {
                { DownloadAction.Install, ResourceProvider.GetString(LOC.Nile3P_PlayniteInstallGame) },
                { DownloadAction.Repair, ResourceProvider.GetString(LOC.NileRepair) },
                { DownloadAction.Update, ResourceProvider.GetString(LOC.Nile3P_PlayniteUpdaterInstallUpdate) }
            };
            TaskCBo.ItemsSource = downloadActionOptions;
        }

        private void ChooseGamePathBtn_Click(object sender, RoutedEventArgs e)
        {
            var path = playniteAPI.Dialogs.SelectFolder();
            if (path != "")
            {
                SelectedGamePathTxt.Text = path;
            }
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            var wantedItem = downloadManagerData.downloads.FirstOrDefault(item => item.gameID == SelectedDownload.gameID);
            var installPath = SelectedGamePathTxt.Text;
            var playniteDirectoryVariable = ExpandableVariables.PlayniteDirectory.ToString();
            if (installPath.Contains(playniteDirectoryVariable))
            {
                installPath = installPath.Replace(playniteDirectoryVariable, playniteAPI.Paths.ApplicationPath);
            }
            if (!Helpers.IsDirectoryWritable(installPath))
            {
                return;
            }
            wantedItem.downloadProperties.installPath = installPath;
            wantedItem.downloadProperties.downloadAction = (DownloadAction)TaskCBo.SelectedValue;
            wantedItem.downloadProperties.maxWorkers = int.Parse(MaxWorkersNI.Value);
            var downloadManager = NileLibrary.GetNileDownloadManager();
            var previouslySelected = downloadManager.DownloadsDG.SelectedIndex;
            for (int i = 0; i < downloadManager.downloadManagerData.downloads.Count; i++)
            {
                if (downloadManager.downloadManagerData.downloads[i].gameID == SelectedDownload.gameID)
                {
                    downloadManager.downloadManagerData.downloads[i] = wantedItem;
                    break;
                }
            }
            downloadManager.DownloadsDG.SelectedIndex = previouslySelected;
            Window.GetWindow(this).Close();
        }

        private void UpdateSpaceInfo(string path, double installSizeNumber)
        {
            DriveInfo dDrive = new DriveInfo(path);
            if (dDrive.IsReady)
            {
                long availableFreeSpace = dDrive.AvailableFreeSpace;
                SpaceTB.Text = Helpers.FormatSize(availableFreeSpace);
                UpdateAfterInstallingSize(availableFreeSpace, installSizeNumber);
            }
        }

        private void UpdateAfterInstallingSize(long availableFreeSpace, double installSizeNumber)
        {
            double afterInstallSizeNumber = (double)(availableFreeSpace - installSizeNumber);
            if (afterInstallSizeNumber < 0)
            {
                afterInstallSizeNumber = 0;
            }
            AfterInstallingTB.Text = Helpers.FormatSize(afterInstallSizeNumber);
        }
    }
}
