using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using UnityEngine;

// TODO: Fix only grabbing fx based on prefab name: not if they are playable on the client

namespace Oxide.Plugins
{
    [Info("Prefab Sniffer", "Wulf/lukespragg", "1.2.2", ResourceId = 1938)]
    [Description("Sniffs the game files for prefab file locations")]
    public class PrefabSniffer : CovalencePlugin
    {
        private const string commandUsage = "Usage: prefabs <build, find, fx, or all> [keyword]";

        private Dictionary<string, Object> files;
        private GameManifest.PooledString[] manifest;

        private void OnServerInitialized()
        {
            files = FileSystemBackend.cache;
            manifest = GameManifest.Current.pooledStrings;
        }

        [Command("prefab", "prefabs")]
        private void PrefabsCommand(IPlayer player, string command, string[] args)
        {
            if (args.Length == 0)
            {
                player.Reply(commandUsage);
                return;
            }

            List<string> resourcesList = new List<string>();
            string argName = "";

            switch (args[0].ToLower())
            {
                case "find":
                    if (args.Length > 2) PrintWarning("Usage: prefabs find <keyword>");
                    foreach (GameManifest.PooledString asset in manifest)
                    {
                        if (!asset.str.Contains(args[1]) || !asset.str.EndsWith(".prefab")) continue;

                        resourcesList.Add(asset.str);
                    }
                    argName = "find";
                    break;

                case "build":
                    foreach (string asset in files.Keys)
                    {
                        if ((!asset.StartsWith("assets/content/")
                            && !asset.StartsWith("assets/bundled/")
                            && !asset.StartsWith("assets/prefabs/"))
                            || !asset.EndsWith(".prefab")) continue;

                        if (asset.Contains(".worldmodel.")
                            || asset.Contains("/fx/")
                            || asset.Contains("/effects/")
                            || asset.Contains("/build/skins/")
                            || asset.Contains("/_unimplemented/")
                            || asset.Contains("/ui/")
                            || asset.Contains("/sound/")
                            || asset.Contains("/world/")
                            || asset.Contains("/env/")
                            || asset.Contains("/clothing/")
                            || asset.Contains("/skins/")
                            || asset.Contains("/decor/")
                            || asset.Contains("/monument/")
                            || asset.Contains("/crystals/")
                            || asset.Contains("/projectiles/")
                            || asset.Contains("/meat_")
                            || asset.EndsWith(".skin.prefab")
                            || asset.EndsWith(".viewmodel.prefab")
                            || asset.EndsWith("_test.prefab")
                            || asset.EndsWith("_collision.prefab")
                            || asset.EndsWith("_ragdoll.prefab")
                            || asset.EndsWith("_skin.prefab")
                            || asset.Contains("/clutter/")) continue;

                        GameObject go = GameManager.server.FindPrefab(asset);
                        if (go?.GetComponent<BaseEntity>() != null) resourcesList.Add(asset);
                    }
                    argName = "build";
                    break;

                case "fx":
                    foreach (GameManifest.PooledString asset in manifest)
                    {
                        if ((!asset.str.StartsWith("assets/content/")
                            && !asset.str.StartsWith("assets/bundled/")
                            && !asset.str.StartsWith("assets/prefabs/"))
                            || !asset.str.EndsWith(".prefab")) continue;

                        if (asset.str.Contains("/fx/")) resourcesList.Add(asset.str);
                    }
                    argName = "fx";
                    break;

                case "all":
                    foreach (GameManifest.PooledString asset in manifest) resourcesList.Add(asset.str);
                    argName = "all";
                    break;

                default:
                    player.Reply(commandUsage);
                    break;
            }

            if (!string.IsNullOrEmpty(argName))
            {
                for (int i = 0; i < resourcesList.Count - 1; i++)
                {
                    player.Reply($"{i} - {resourcesList[i]}");
                    LogToFile(argName, $"{i} - {resourcesList[i]}", this);
                }

                player.Reply("Prefab results saved to the oxide/logs folder");
            }
        }
    }
}