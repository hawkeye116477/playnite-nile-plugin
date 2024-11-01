using Playnite.SDK;
using Playnite.SDK.Models;
using System.Collections.Generic;

namespace NileLibraryNS
{
    public class AmazonGamesMetadataProvider : LibraryMetadataProvider
    {
        public override GameMetadata GetMetadata(Game game)
        {
            var gameInfo = new GameMetadata
            {
                Links = new List<Link>()
            };

            gameInfo.Links.Add(new Link("PCGamingWiki", @"http://pcgamingwiki.com/w/index.php?search=" + game.Name));
            return gameInfo;
        }
    }
}
