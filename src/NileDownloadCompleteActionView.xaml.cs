using CommonPlugin;
using CommonPlugin.Enums;
using Playnite.SDK;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace NileLibraryNS
{
    /// <summary>
    /// Interaction logic for NileDownloadCompleteActionView.xaml
    /// </summary>
    public partial class NileDownloadCompleteActionView : UserControl
    {
        private DownloadCompleteAction downloadCompleteAction = NileLibrary.GetSettings().DoActionAfterDownloadComplete;
        private DispatcherTimer timer;
        private int time = 60;

        public NileDownloadCompleteActionView()
        {
            InitializeComponent();
        }

        private void NileDownloadCompleteActionUC_Loaded(object sender, RoutedEventArgs e)
        {
            CommonHelpers.SetControlBackground(this);
            switch (downloadCompleteAction)
            {
                case DownloadCompleteAction.ShutDown:
                    ActionBtn.Content = ResourceProvider.GetString(LOC.Nile3P_PlayniteMenuShutdownSystem);
                    CountdownTB.Text = LocalizationManager.Instance.GetString(LOC.CommonSystemShutdownCountdown);
                    break;
                case DownloadCompleteAction.Reboot:
                    ActionBtn.Content = ResourceProvider.GetString(LOC.Nile3P_PlayniteMenuRestartSystem);
                    CountdownTB.Text = LocalizationManager.Instance.GetString(LOC.CommonSystemRestartCountdown);
                    break;
                case DownloadCompleteAction.Hibernate:
                    ActionBtn.Content = ResourceProvider.GetString(LOC.Nile3P_PlayniteMenuHibernateSystem);
                    CountdownTB.Text = LocalizationManager.Instance.GetString(LOC.CommonSystemHibernateCountdown);
                    break;
                case DownloadCompleteAction.Sleep:
                    ActionBtn.Content = ResourceProvider.GetString(LOC.Nile3P_PlayniteMenuSuspendSystem);
                    CountdownTB.Text = LocalizationManager.Instance.GetString(LOC.CommonSystemSuspendCountdown);
                    break;
            }
            CountdownPB.Maximum = time;
            CountdownSecondsTB.Text = $"{time} s";
            timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (time > 0)
            {
                time--;
                CountdownPB.Value += 1;
                CountdownSecondsTB.Text = $"{time} s";
            }
            else
            {
                CountdownPB.Value = CountdownPB.Maximum;
                timer.Stop();
                StartDownloadCompleteAction();
            }
        }

        public void StartDownloadCompleteAction()
        {
            switch (downloadCompleteAction)
            {
                case DownloadCompleteAction.ShutDown:
                    Process.Start("shutdown", "/s /t 0");
                    break;
                case DownloadCompleteAction.Reboot:
                    Process.Start("shutdown", "/r /t 0");
                    break;
                case DownloadCompleteAction.Hibernate:
                    Playnite.Native.Powrprof.SetSuspendState(true, true, false);
                    break;
                case DownloadCompleteAction.Sleep:
                    Playnite.Native.Powrprof.SetSuspendState(false, true, false);
                    break;
                default:
                    break;
            }
        }

        private void ActionBtn_Click(object sender, RoutedEventArgs e)
        {
            Window.GetWindow(this).Close();
            timer.Stop();
            StartDownloadCompleteAction();
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            timer.Stop();
            Window.GetWindow(this).Close();
        }
    }
}
