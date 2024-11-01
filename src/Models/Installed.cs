using System.Collections.Generic;

public class InstalledGames
{
    public List<Installed> installedGames { get; set; } = new List<Installed>();

    public class Installed
    {
        public string id { get; set; }
        public string version { get; set; }
        public string path { get; set; }
        public double size { get; set; }
    }
}
