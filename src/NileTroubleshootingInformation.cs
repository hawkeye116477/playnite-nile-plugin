using System.Diagnostics;
using System.Reflection;
using Playnite.SDK;

namespace NileLibraryNS
{
    public class NileTroubleshootingInformation
    {
        public string PlayniteVersion
        {
            get
            {
                var playniteAPI = API.Instance;
                return playniteAPI.ApplicationInfo.ApplicationVersion.ToString();
            }
        }

        public string PluginVersion
        {
            get
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
                return fvi.FileVersion;
            }
        }

        public string NileVersion { get; set; } = "";
        public string NileBinary { get; set; } = Nile.ClientExecPath;
        public string GamesInstallationPath => Nile.GamesInstallationPath;
    }
}