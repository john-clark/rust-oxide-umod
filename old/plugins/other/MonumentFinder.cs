using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Oxide.Core;
using System.Text.RegularExpressions;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("MonumentFinder", "PsychoTea", "1.0.1")]
    [Description("Allows admins to teleport to monuments.")]

    class MonumentFinder : CovalencePlugin
    {
        const string permUse = "monumentfinder.use";
        SortedDictionary<string, Vector3> monPos = new SortedDictionary<string, Vector3>();

        void Init()
        {
            permission.RegisterPermission(permUse, this);

            lang.RegisterMessages(new Dictionary<string, string>()
            {
                { "NoPermission", "You don't have permission to use this command." },
                { "IncorrectUsage", "Incorrect usage! /mon {list/name}" },
                { "DoesntExist", "The monument '{0}' doesn't exist. Use '/mon list' for a list of monuments." },
                { "MonumentList", "<color=lime>Monuments:</color>\n{0}" },
                { "TeleportedTo", "Teleported to: {0}" }
            }, this);
        }

        void OnServerInitialized() => FindMonuments();

        [Command("mon")]
        void monCommand(IPlayer player, string command, string[] args)
        {
            if (!HasPerm(player))
            {
                player.Reply(lang.GetMessage("NoPermission", this, player.Id));
                return;
            }

            if (args.Length == 0)
            {
                player.Reply(lang.GetMessage("IncorrectUsage", this, player.Id));
                return;
            }

            if (args[0].ToLower() == "list")
            {
                string monuments = string.Join("\n", monPos.Keys.ToArray());
                player.Reply(lang.GetMessage("MonumentList", this, player.Id).Replace("{0}", monuments));
                return;
            }

            string monName = string.Join(" ", args).ToLower().Titleize();

            if (!monPos.ContainsKey(monName))
            {
                player.Reply(lang.GetMessage("DoesntExist", this, player.Id).Replace("{0}", monName));
                return;
            }

            var pos = ToGeneric(monPos[monName]);
            player.Teleport(pos.X, pos.Y, pos.Z);
            player.Reply(lang.GetMessage("TeleportedTo", this, player.Id).Replace("{0}", monName));
        }
        
        void FindMonuments()
        {
            foreach (MonumentInfo monument in UnityEngine.Object.FindObjectsOfType<MonumentInfo>())
            {
                if (monument.name.Contains("power_sub") || monument.name.Contains("cave")) continue;
                string name = Regex.Match(monument.name, @"\w{6}\/(.+\/)(.+)\.(.+)").Groups[2].Value
                                   .Replace("_", " ").Replace(" 1", "").Titleize();
                if (monPos.ContainsKey(name)) continue;
                monPos.Add(name, monument.transform.position);
            }
            monPos.OrderBy(x => x.Key);
        }

        bool HasPerm(IPlayer player) => (player.IsAdmin || permission.UserHasPermission(player.Id, permUse));

        GenericPosition ToGeneric(Vector3 vec) => new GenericPosition(vec.x, vec.y, vec.z);
    }
}