using System;
using System.Collections.Generic;
using System.Linq;

using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("EnchantTools", "MalkoR", "1.0.3")]
    [Description("Adds enchanted tools to the game that mining melted resources.")]
    class EnchantTools : RustPlugin
    {
        private List<int> EnchantedTools;

        #region Oxide hooks
        private void Init()
        {
            LoadVariables();
            EnchantedTools = Interface.Oxide?.DataFileSystem?.ReadObject<List<int>>("EnchantTools") ?? new List<int>();
            cmd.AddChatCommand(configData.Command, this, "CmdEnchant");
            cmd.AddConsoleCommand(configData.Command, this, "CcmdEnchant");
            permission.RegisterPermission("enchanttools.admin", this);
            permission.RegisterPermission("enchanttools.use", this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["PermissionAdmin"] = "You don't have permission to use this command.",
                ["MultiplePlayer"] = "Multiple players found: {0}",
                ["PlayerIsNotFound"] = "The player with the name {0} was not found.",
                ["UsageSyntax"] = "Usage command syntax: \n<color=#FF99CC>{0} <tool_name> <playerName or Id></color>\nAvailable tools names:\n{1}",
                ["ToolGiven"] = "{0} received enchanted tool: {1}.",
                ["CantRepeair"] = "You can't repair an enchanted tools.",
                ["ConsoleNotAvailable"] = "This command available only from server console or rcon.",
                ["ConsoleNoPlayerFound"] = "No player with the specified SteamID was found.",
                ["ConsoleNoPlayerAlive"] = "The player with the specified ID was not found among active or sleeping players.",
                ["ConsoleToolGiven"] = "{0} received enchanted tool {1}.",
                ["ConsoleUsageSyntax"] = "Usage command syntax: \n<color=#FF99CC>{0} <tool_name> <steamId></color>\nAvailable tools names:\n{1}"

            }, this);
        }

        void OnNewSave(string filename) {
            EnchantedTools = new List<int>();
            SaveEnchantedTools();
        }

        void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            BasePlayer player = entity.ToPlayer();

            if (player == null) return;

            if (!configData.CanUseByAllPlayers && !permission.UserHasPermission(player.UserIDString, "enchanttool.use")) return;

            if (EnchantedTools.Contains(player.GetActiveItem().GetHashCode()))
            {
                switch (item.info.shortname)
                {
                    case "sulfur.ore":
                        ReplaceContents(-891243783, ref item);
                        break;
                    case "hq.metal.ore":
                        ReplaceContents(374890416, ref item);
                        break;
                    case "metal.ore":
                        ReplaceContents(688032252, ref item);
                        break;
                    case "wolfmeat.raw":
                        ReplaceContents(-1691991080, ref item);
                        break;
                    case "meat.boar":
                        ReplaceContents(991728250, ref item);
                        break;
                    case "fish.raw":
                        ReplaceContents(-2078972355, ref item);
                        break;
                    case "chicken.raw":
                        ReplaceContents(1734319168, ref item);
                        break;
                    case "bearmeat":
                        ReplaceContents(-2043730634, ref item);
                        break;
                    case "wood":
                        ReplaceContents(1436001773, ref item);
                        break;
                }
            }
        }

        object OnItemRepair(BasePlayer player, Item item)
        {
            if(EnchantedTools.Contains(item.GetHashCode()))
            {
                SendReply(player, lang.GetMessage("CantRepeair", this, player.UserIDString));
                return false;
            }
            return null;
        }        

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (EnchantedTools.Contains(entity.GetHashCode()))
                EnchantedTools.Remove(entity.GetHashCode());
        }
        #endregion

        #region Chat command
        private void CmdEnchant(BasePlayer player, string command, string[] args)
        {
            if(!player.IsAdmin && !permission.UserHasPermission(player.UserIDString, "enchanttools.admin"))
            {
                SendReply(player, lang.GetMessage("PermissionAdmin", this, player.UserIDString));
                return;
            }

            List<string> shortnames = new List<string>();
            foreach (Tool tool in configData.Tools)
                shortnames.Add(tool.shortname);

            if(args.Length == 2)
            {
                if(shortnames.Contains(args.ElementAtOrDefault(0)))
                {
                    List<BasePlayer> founded_players = new List<BasePlayer>();

                    string searchNameOrId = args.ElementAtOrDefault(1);

                    foreach (BasePlayer p in BasePlayer.activePlayerList)
                    {
                        if (p.displayName.Contains(searchNameOrId))
                            founded_players.Add(p);
                        else if (p.UserIDString == searchNameOrId)
                            founded_players.Add(p);
                    }
                    if(founded_players.Count > 1)
                    {
                        List<string> multiple_names = new List<string>();
                        foreach (BasePlayer p in founded_players)
                            multiple_names.Add(p.displayName);
                        SendReply(player, lang.GetMessage("MultiplePlayer", this, player.UserIDString), String.Join(", ", multiple_names.ToArray()));
                        return;
                    }
                    else if(founded_players.Count == 0)
                    {
                        SendReply(player, lang.GetMessage("PlayerIsNotFound", this, player.UserIDString), args.ElementAtOrDefault(1));
                        return;
                    }
                    else
                    {
                        Tool tool = configData.Tools.Find(t => t.shortname == args.ElementAtOrDefault(0));
                        BasePlayer reciver = founded_players.First();
                        if(reciver.IsValid())
                        {
                            GiveEnchantTool(reciver, tool);
                            SendReply(player, lang.GetMessage("ToolGiven", this, player.UserIDString), reciver.displayName, args.ElementAtOrDefault(0));
                            return;
                        }                        
                    }                    
                }
            }
            SendReply(player, lang.GetMessage("UsageSyntax", this, player.UserIDString), configData.Command, String.Join(", ", shortnames.ToArray()));
        }
        #endregion

        #region Console command
        void CcmdEnchant(ConsoleSystem.Arg arg)
        {
            if(!arg.IsServerside && !arg.IsRcon)
            {
                BasePlayer player = arg.Player();
                if(player != null)
                    player.ConsoleMessage(lang.GetMessage("ConsoleNotAvailable", this, player.UserIDString));
            }

            string[] args = arg.Args;            

            List<string> shortnames = new List<string>();
            foreach (Tool tool in configData.Tools)
                shortnames.Add(tool.shortname);

            if (args.Length == 2)
            {
                if (shortnames.Contains(args.ElementAtOrDefault(0)))
                {
                    List<BasePlayer> founded_players = new List<BasePlayer>();

                    string steamId = args.ElementAtOrDefault(1);

                    IPlayer iplayer = covalence.Players.FindPlayerById(steamId);

                    if(iplayer == null)
                    {
                        SendReply(arg, lang.GetMessage("ConsoleNoPlayerFound", this));
                    }

                    BasePlayer reciver;
                    reciver = BasePlayer.activePlayerList.Find(p => p.UserIDString == steamId);
                    if(reciver == null)
                    {
                        reciver = BasePlayer.sleepingPlayerList.Find(p => p.UserIDString == steamId);                        
                    }

                    if (reciver == null)
                    {
                        SendReply(arg, lang.GetMessage("ConsoleNoPlayerAlive", this));
                    }
                    else
                    {
                        Tool tool = configData.Tools.Find(t => t.shortname == args.ElementAtOrDefault(0));
                        GiveEnchantTool(reciver, tool);
                        SendReply(arg, lang.GetMessage("ConsoleToolRecived", this), reciver.displayName, args.ElementAtOrDefault(0));
                    }
                    return;
                }
            }
            SendReply(arg, lang.GetMessage("ConsoleUsageSyntax", this), configData.Command, String.Join(", ", shortnames.ToArray()));
        }
        #endregion

        #region Helpers
        private void ReplaceContents(int ItemId, ref Item item)
        {
            Item _item = ItemManager.CreateByItemID(ItemId, item.amount);
            item.info = _item.info;
            item.contents = _item.contents;
        }

        private void GiveEnchantTool(BasePlayer player, Tool tool)
        {
            Item item = ItemManager.CreateByName(tool.shortname, 1, tool.skinId);
            player.GiveItem(item);
            EnchantedTools.Add(item.GetHashCode());
            SaveEnchantedTools();
        }

        private void SaveEnchantedTools()
        {
            Interface.Oxide.DataFileSystem.WriteObject("EnchantTools", EnchantedTools);
        }
        #endregion

        #region Config        
        private ConfigData configData;
        class ConfigData
        {
            public string Command;
            public bool CanUseByAllPlayers;
            public List<Tool> Tools;
        }
        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }
        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                Command = "enchanttool",
                CanUseByAllPlayers = true,
                
                Tools = new List<Tool>()
                {
                    new Tool()
                    {
                        shortname = "hatchet",
                        skinId = 0,
                        canRepair = true
                    },
                    new Tool()
                    {
                        shortname = "axe.salvaged",
                        skinId = 0,
                        canRepair = true
                    },
                    new Tool()
                    {
                        shortname = "pickaxe",
                        skinId = 0,
                        canRepair = true
                    },
                    new Tool()
                    {
                        shortname = "icepick.salvaged",
                        skinId = 0,
                        canRepair = true
                    },
                }
            };
            SaveConfig(config);
        }
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion

        #region Nested Classes
        class Tool
        {
            public string shortname;
            public uint skinId;
            public bool canRepair;
        }
        #endregion
    }
}