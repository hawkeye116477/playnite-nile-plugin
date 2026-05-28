using CommonPlugin;
using CommonPlugin.Enums;
using Linguini.Shared.Types.Bundle;
using NileLibraryNS.Models;
using NileLibraryNS.Services;
using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Events;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using UnifiedDownloadManagerApiNS;

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
        public string installPath;
        private IInputElement lastMenuElement;

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

        public async Task StartTask(DownloadAction downloadAction, bool silently = false)
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

            var downloadTasks = new List<DownloadManagerData.Download>();

            foreach (var installData in MultiInstallData)
            {
                var gameId = installData.gameID;
                var wantedItem = NileLibrary.Instance.pluginDownloadData.downloads.FirstOrDefault(item => item.gameID == gameId);
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
            if (downloadTasks.Count > 0)
            {
                var nileDownloadLogic = new NileDownloadLogic();
                await nileDownloadLogic.AddTasks(downloadTasks, silently);
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
            if (!Nile.IsInstalled)
            {
                Nile.ShowNotInstalledError();
                return;
            }
            var isUdmInstalled = NileDownloadLogic.CheckIfUdmInstalled();
            if (!isUdmInstalled)
            {
                return;
            }
            CommonHelpers.SetControlBackground(this);
            if (MultiInstallData.First().downloadProperties.downloadAction == DownloadAction.Repair)
            {
                FolderDP.Visibility = Visibility.Collapsed;
                InstallBtn.Visibility = Visibility.Collapsed;
                RepairBtn.Visibility = Visibility.Visible;
                AfterInstallingSP.Visibility = Visibility.Collapsed;
            }
            var settings = NileLibrary.GetSettings();
            installPath = Nile.GamesInstallationPath;
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

            await RefreshAll();
            var games = MultiInstallData;

            if (settings.UnattendedInstall && (games.First().downloadProperties.downloadAction == DownloadAction.Install))
            {
                await StartTask(DownloadAction.Install, true);
            }
            else if (playniteAPI.ApplicationInfo.Mode == ApplicationMode.Fullscreen)
            {
                var firstEnabledBtn = LogicalTreeHelper.GetChildren(TopButtonsSP).OfType<Button>().FirstOrDefault(b => b.IsEnabled && b.IsVisible);
                if (firstEnabledBtn != null)
                {
                    firstEnabledBtn.Focus();
                }
                SelectedGamePathTxt.Focusable = false;
                ChooseGamePathBtn.Focusable = false;
            }
        }

        public async Task RefreshAll()
        {
            UnifiedDownloadManagerApi unifiedDownloadManagerApi = new UnifiedDownloadManagerApi();
            InstallBtn.IsEnabled = false;
            ReloadBtn.IsEnabled = false;
            UpdateSpaceInfo(installPath);

            downloadSizeNumber = 0;


            bool gamesListShouldBeDisplayed = false;

            var installedAppList = Nile.GetInstalledAppList();

            var pluginDownloadData = NileLibrary.Instance.pluginDownloadData;
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
                var wantedItem = pluginDownloadData.downloads.FirstOrDefault(item => item.gameID == installData.gameID);
                var wantedUnifiedTask = unifiedDownloadManagerApi.GetTask(installData.gameID, NileLibrary.Instance.Id.ToString());
            }

            CalculateTotalSize();


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
                    var loginErrorMessage = LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteGameInstallError, new Dictionary<string, IFluentType>
                    {
                        ["var0"] = (FluentString)LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteLoginRequired)
                    });
                    MessageCheckBoxDialog.ShowMessage("", loginErrorMessage, null, MessageBoxButton.OK, MessageBoxImage.Error);
                }
                if (games.Count <= 0)
                {
                    InstallerWindow.Close();
                }
                return;
            }
            if (downloadSizeNumber != 0)
            {
                InstallBtn.IsEnabled = true;
            }
            ReloadBtn.IsEnabled = true;
        }

        private async void ReloadBtn_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageCheckBoxDialog.ShowMessage(LocalizationManager.Instance.GetString(LOC.CommonReload), LocalizationManager.Instance.GetString(LOC.CommonReloadConfirm), null, MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result.Result)
            {
                InstallBtn.IsEnabled = false;
                DownloadSizeTB.Text = LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteLoadingLabel);
                InstallSizeTB.Text = LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteLoadingLabel);
                Nile.ClearCache();
                await RefreshAll();
            }
        }

        public static void HandleControllerInput(ControllerInput button, Window window)
        {
            var thisUserControl = window.Content as NileGameInstallerView;
            var focusedElement = Keyboard.FocusedElement as FrameworkElement;
            if (!(focusedElement is TextBox))
            {
                thisUserControl.lastMenuElement = Keyboard.FocusedElement;
            }
            switch (button)
            {
                case ControllerInput.A:
                    if (focusedElement is Button btn)
                    {
                        var peer = new ButtonAutomationPeer(btn);

                        if (peer.GetPattern(PatternInterface.Invoke) is IInvokeProvider provider)
                        {
                            provider.Invoke();
                        }
                        return;
                    }
                    if (focusedElement?.TemplatedParent is Expander expander)
                    {
                        expander.IsExpanded = !expander.IsExpanded;
                        return;
                    }
                    break;
                case ControllerInput.B:
                    if (focusedElement is TextBox)
                    {
                        thisUserControl.lastMenuElement?.Focus();
                        return;
                    }
                    window.Close();
                    break;
                case ControllerInput.DPadLeft:
                case ControllerInput.LeftStickLeft:
                    if (focusedElement is TextBox)
                    {
                        thisUserControl.lastMenuElement?.Focus();
                        return;
                    }
                    break;
                default:
                    break;
            }
        }
    }
}
