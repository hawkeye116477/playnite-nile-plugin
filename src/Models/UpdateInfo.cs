namespace NileLibraryNS.Models
{
    public class UpdateInfo
    {
        public string Title { get; set; }
        public double Download_size { get; set; } = 0;
        public bool Success { get; set; } = true;
        public string Install_path { get; set; } = "";
    }
}
