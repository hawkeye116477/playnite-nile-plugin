using NileLibraryNS.Models;
using System.Linq;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Playnite.SDK.Data;
using Playnite.Common;
using Playnite.SDK.Models;
using NileLibraryNS.Enums;
using CommonPlugin;

namespace NileLibraryNS
{
    /// <summary>
    /// Interaction logic for NileGameSettingsView.xaml
    /// </summary>
    public partial class NileGameSettingsView : UserControl
    {
        private Game Game => DataContext as Game;
        public string GameID => Game.GameId;
        public GameSettings gameSettings;

        public NileGameSettingsView()
        {
            InitializeComponent();
        }

        public static GameSettings LoadGameSettings(string gameID)
        {
            var gameSettings = new GameSettings();
            var gameSettingsFile = Path.Combine(NileLibrary.Instance.GetPluginUserDataPath(), "GamesSettings", $"{gameID}.json");
            if (File.Exists(gameSettingsFile))
            {
                if (Serialization.TryFromJson(FileSystem.ReadFileAsStringSafe(gameSettingsFile), out GameSettings savedGameSettings))
                {
                    if (savedGameSettings != null)
                    {
                        gameSettings = savedGameSettings;
                    }
                }
            }
            return gameSettings;
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            var globalSettings = NileLibrary.GetSettings();
            var newGameSettings = new GameSettings();
            bool globalDisableUpdates = false;
            if (globalSettings.GamesUpdatePolicy == UpdatePolicy.Never)
            {
                globalDisableUpdates = true;
            }
            if (DisableGameUpdateCheckingChk.IsChecked != globalDisableUpdates)
            {
                newGameSettings.DisableGameVersionCheck = DisableGameUpdateCheckingChk.IsChecked;
            }
            if (StartupArgumentsTxt.Text != "")
            {
                newGameSettings.StartupArguments = StartupArgumentsTxt.Text.SplitOutsideQuotes(' ').ToList();
            }
            if (globalSettings.StartGamesWithoutLauncher != LaunchGameDirectlyChk.IsChecked)
            {
                newGameSettings.LaunchDirectly = LaunchGameDirectlyChk.IsChecked;
            }
            var gameSettingsFile = Path.Combine(NileLibrary.Instance.GetPluginUserDataPath(), "GamesSettings", $"{GameID}.json");
            if (newGameSettings.GetType().GetProperties().Any(p => p.GetValue(newGameSettings) != null) || File.Exists(gameSettingsFile))
            {
                if (File.Exists(gameSettingsFile))
                {
                    var oldGameSettings = LoadGameSettings(GameID);
                }
                var commonHelpers = NileLibrary.Instance.commonHelpers;
                commonHelpers.SaveJsonSettingsToFile(newGameSettings,  "GamesSettings", GameID, true);
            }
            Window.GetWindow(this).Close();
        }

        private void NileGameSettingsViewUC_Loaded(object sender, RoutedEventArgs e)
        {
            CommonHelpers.SetControlBackground(this);
            var globalSettings = NileLibrary.GetSettings();
            if (globalSettings.GamesUpdatePolicy == UpdatePolicy.Never)
            {
                DisableGameUpdateCheckingChk.IsChecked = true;
            }
            gameSettings = LoadGameSettings(GameID);
            if (gameSettings.DisableGameVersionCheck != null)
            {
                DisableGameUpdateCheckingChk.IsChecked = gameSettings.DisableGameVersionCheck;
            }
            if (gameSettings.StartupArguments != null)
            {
                StartupArgumentsTxt.Text = string.Join(" ", gameSettings.StartupArguments);
            }
            if (gameSettings.LaunchDirectly != null)
            {
                LaunchGameDirectlyChk.IsChecked = gameSettings.LaunchDirectly;
            }
            else
            {
                LaunchGameDirectlyChk.IsChecked = globalSettings.StartGamesWithoutLauncher;
            }
        }

    }
}
