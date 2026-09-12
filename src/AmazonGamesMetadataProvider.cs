using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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

            gameInfo.Links.Add(new Link("PCGamingWiki", $"http://pcgamingwiki.com/w/index.php?search=" + Uri.EscapeDataString(game.Name)));

            // Load icon from exe
            if (game.IsInstalled && string.IsNullOrEmpty(game.Icon))
            {
                var installedAppList = Nile.GetInstalledAppList();
                var installedInfo = installedAppList.FirstOrDefault(i => i.id == game.GameId);
                if (installedInfo != null)
                {
                    var gameConfig = Nile.GetGameConfiguration(installedInfo.path);
                    if (!gameConfig.Main.Command.IsNullOrEmpty())
                    {
                        var exePath = Path.Combine(installedInfo.path, gameConfig.Main.Command);
                        if (File.Exists(exePath))
                        {
                            gameInfo.Icon = new MetadataFile(exePath);
                        }
                    }
                }
            }
            return gameInfo;
        }
    }
}
