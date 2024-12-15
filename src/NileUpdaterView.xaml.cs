using CommonPlugin;
using CommonPlugin.Enums;
using NileLibraryNS.Models;
using Playnite.SDK;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace NileLibraryNS
{
    /// <summary>
    /// Interaction logic for NileUpdaterView.xaml
    /// </summary>
    public partial class NileUpdaterView : UserControl
    {
        private IPlayniteAPI playniteAPI = API.Instance;
        public Dictionary<string, UpdateInfo> UpdatesList => (Dictionary<string, UpdateInfo>)DataContext;

        public NileUpdaterView()
        {
            InitializeComponent();
            SetControlStyles();
        }

        private void UpdatesLB_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateBtn.IsEnabled = UpdatesLB.SelectedIndex != -1;
            double initialDownloadSizeNumber = 0;
            double initialInstallSizeNumber = 0;
            foreach (var selectedOption in UpdatesLB.SelectedItems.Cast<KeyValuePair<string, UpdateInfo>>().ToList())
            {
                initialDownloadSizeNumber += selectedOption.Value.Download_size;
                initialInstallSizeNumber += selectedOption.Value.Download_size;
            }
            var downloadSize = CommonHelpers.FormatSize(initialDownloadSizeNumber);
            DownloadSizeTB.Text = downloadSize;
            var installSize = CommonHelpers.FormatSize(initialInstallSizeNumber);
            InstallSizeTB.Text = installSize;
        }

        private void SelectAllBtn_Click(object sender, RoutedEventArgs e)
        {
            if (UpdatesLB.Items.Count == UpdatesLB.SelectedItems.Count)
            {
                UpdatesLB.UnselectAll();
            }
            else
            {
                UpdatesLB.SelectAll();
            }
        }

        private void SetControlStyles()
        {
            var baseStyleName = "BaseTextBlockStyle";
            if (playniteAPI.ApplicationInfo.Mode == ApplicationMode.Fullscreen)
            {
                baseStyleName = "TextBlockBaseStyle";
                Resources.Add(typeof(Button), new Style(typeof(Button), null));
            }

            if (ResourceProvider.GetResource(baseStyleName) is Style baseStyle && baseStyle.TargetType == typeof(TextBlock))
            {
                var implicitStyle = new Style(typeof(TextBlock), baseStyle);
                Resources.Add(typeof(TextBlock), implicitStyle);
            }
        }

        private async void UpdateBtn_Click(object sender, RoutedEventArgs e)
        {
            if (UpdatesLB.SelectedItems.Count > 0)
            {
                var settings = NileLibrary.GetSettings();
                int maxWorkers = settings.MaxWorkers;
                if (MaxWorkersNI.Value != "")
                {
                    maxWorkers = int.Parse(MaxWorkersNI.Value);
                }
                NileUpdateController NileUpdateController = new NileUpdateController();
                DownloadProperties downloadProperties = new DownloadProperties
                {
                    downloadAction = DownloadAction.Update,
                    maxWorkers = maxWorkers,
                };
                Window.GetWindow(this).Close();
                var updatesList = new Dictionary<string, UpdateInfo>();
                foreach (var selectedOption in UpdatesLB.SelectedItems.Cast<KeyValuePair<string, UpdateInfo>>().ToList())
                {
                    updatesList.Add(selectedOption.Key, selectedOption.Value);
                }
                await NileUpdateController.UpdateGame(updatesList, "", false, downloadProperties);
            }
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            Window.GetWindow(this).Close();
        }

        private void NileUpdaterUC_Loaded(object sender, RoutedEventArgs e)
        {
            UpdatesLB.ItemsSource = UpdatesList;
            UpdatesLB.Visibility = Visibility.Visible;
            UpdatesLB.SelectAll();
            var settings = NileLibrary.GetSettings();
            MaxWorkersNI.MaxValue = CommonHelpers.CpuThreadsNumber;
            MaxWorkersNI.Value = settings.MaxWorkers.ToString();
        }
    }
}
