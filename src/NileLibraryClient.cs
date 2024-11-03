using Playnite.SDK;

namespace NileLibraryNS
{
    public class NileLibraryClient : LibraryClient
    {
        public override string Icon => Nile.Icon;

        public override bool IsInstalled => Nile.IsInstalled;

        public override void Open()
        {
            Nile.StartClient();
        }

    }
}