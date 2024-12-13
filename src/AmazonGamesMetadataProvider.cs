using Playnite.SDK;
using Playnite.SDK.Models;
using System;
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

            if (game.Name.EndsWith(" - CE"))
            {
                game.Name = game.Name.TrimEndString("- CE") + "Collector's Edition";
            }
            else if (game.Name.EndsWith(" CE"))
            {
                game.Name = game.Name.TrimEndString("CE") + "Collector's Edition";
            }

            gameInfo.Links.Add(new Link("PCGamingWiki", @"http://pcgamingwiki.com/w/index.php?search=" + game.Name));
            return gameInfo;
        }
    }
}
