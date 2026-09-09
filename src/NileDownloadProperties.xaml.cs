using CommonPlugin;
using CommonPlugin.Enums;
using NileLibraryNS.Models;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using UnifiedDownloadManagerApiNS;
using UnifiedDownloadManagerApiNS.Models;

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
        private long availableFreeSpace;

        public NileDownloadProperties()
        {
            InitializeComponent();
        }

        private void NileDownloadPropertiesUC_Loaded(object sender, RoutedEventArgs e)
        {
            CommonHelpers.SetControlBackground(this);
            MaxWorkersNI.MaxValue = CommonHelpers.CpuThreadsNumber;
            if (SelectedDownload.downloadProperties != null)
            {
                SelectedGamePathTxt.Text = SelectedDownload.downloadProperties.installPath;
                MaxWorkersNI.Value = SelectedDownload.downloadProperties.maxWorkers.ToString();
                TaskCBo.SelectedValue = SelectedDownload.downloadProperties.downloadAction;
            }
            var downloadActionOptions = new Dictionary<DownloadAction, string>
            {
                { DownloadAction.Install, LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteInstallGame) },
                { DownloadAction.Repair, LocalizationManager.Instance.GetString(LOC.CommonRepair) },
                { DownloadAction.Update, LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteUpdaterInstallUpdate) }
            };
            TaskCBo.ItemsSource = downloadActionOptions;
            UpdateSpaceInfo(SelectedDownload.downloadProperties.installPath);
            SizeGrd.Visibility = Visibility.Visible;
            UnifiedDownloadManagerApi unifiedDownloadManagerApi = new UnifiedDownloadManagerApi();
            var wantedItem = unifiedDownloadManagerApi.GetTask(SelectedDownload.gameID, NileLibrary.Instance.Id.ToString());
            if (wantedItem?.status is UnifiedDownloadStatus.Completed or UnifiedDownloadStatus.Running)
            {
                SaveBtn.IsEnabled = false;
            }
            if (playniteAPI.ApplicationInfo.Mode == ApplicationMode.Fullscreen)
            {
                GeneralTab.Focus();
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

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            var wantedItem = downloadManagerData.downloads.FirstOrDefault(item => item.gameID == SelectedDownload.gameID);
            var installPath = SelectedGamePathTxt.Text;
            var playniteDirectoryVariable = ExpandableVariables.PlayniteDirectory.ToString();
            if (installPath.Contains(playniteDirectoryVariable))
            {
                installPath = installPath.Replace(playniteDirectoryVariable, playniteAPI.Paths.ApplicationPath);
            }
            if (!CommonHelpers.IsDirectoryWritable(installPath, LOC.CommonPermissionError))
            {
                return;
            }
            wantedItem.downloadProperties.installPath = installPath;
            wantedItem.downloadProperties.downloadAction = (DownloadAction)TaskCBo.SelectedValue;
            wantedItem.downloadProperties.maxWorkers = int.Parse(MaxWorkersNI.Value);
            NileLibrary.Instance.SaveDownloadData();
            Window.GetWindow(this).Close();
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
            double afterInstallSizeNumber = availableFreeSpace - SelectedDownload.downloadSizeNumber;
            if (afterInstallSizeNumber < 0)
            {
                afterInstallSizeNumber = 0;
            }
            AfterInstallingTB.Text = CommonHelpers.FormatSize(afterInstallSizeNumber);
        }

        private void NileDownloadPropertiesUC_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            NileLibrary.Instance.UC_PreviewKeyDown(sender, e);
        }
    }
}
