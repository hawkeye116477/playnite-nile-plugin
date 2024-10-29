using Playnite.Common;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NileLibraryNS
{
    public class NileLibraryClient : LibraryClient
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        public override string Icon => Nile.Icon;

        public override bool IsInstalled => Nile.IsInstalled;

        public override void Open()
        {
            Nile.StartClient();
        }

    }
}