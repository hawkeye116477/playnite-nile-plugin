using System.Collections.Generic;

namespace NileLibraryNS.Models
{
    public class GameConfiguration
    {
        public const string ConfigFileName = "fuel.json";

        public class Config
        {
            public string Command;
            public string WorkingSubdirOverride;
            public List<string> Args;
            public List<string> AuthScopes;
            public string ClientId;
        }

        public string SchemaVersion;
        public Config Main;
        public List<PostInstallConfig> PostInstall = new List<PostInstallConfig>();

        public class PostInstallConfig
        {
            public string Command = "";
            public List<string> Args = new List<string>();
        }
    }
}
