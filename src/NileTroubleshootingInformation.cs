using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
                System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
                System.Diagnostics.FileVersionInfo fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location);
                return fvi.FileVersion;
            }
        }

        public string NileVersion { get; set; } = "";
        public string NileBinary { get; set; } = Nile.ClientExecPath;
        public string GamesInstallationPath => Nile.GamesInstallationPath;
    }
}
