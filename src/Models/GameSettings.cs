using System.Collections.Generic;

namespace NileLibraryNS.Models
{
    public class GameSettings
    {
        public bool? DisableGameVersionCheck { get; set; }
        public List<string> StartupArguments { get; set; } = new List<string>();
        public bool? LaunchDirectly { get; set; }
    }
}
