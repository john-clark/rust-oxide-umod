using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Item Renamer", "birthdates", "1.1.1")]
    [Description("Rename items with style")]
    public class ItemRenamer : RustPlugin
    {

        public const string Permission = "itemrenamer.use";

        private void Init()
        {
            LoadConfig();
            permission.RegisterPermission(Permission, this);
            cmd.AddChatCommand("itemrename", this, ItemRenameCommand);
            if(_config.items == null) PrintError("Blacklisted items is not setup correctly, please set it up correctly or reset the config");
            if (_config.bw == null) PrintError("Blacklisted words is not setup correctly, please set it up correctly or reset the config");
            
        }

        private void ItemRenameCommand(BasePlayer player, string arg2, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, Permission) && !player.IsAdmin)
            {
                SendReply(player, lang.GetMessage("NoPermission", this, player.UserIDString));
            }
            else
            {
                if (args.Length < 1)
                {
                    SendReply(player, lang.GetMessage("InvalidArgs", this, player.UserIDString));
                }
                else
                {
                    if (player.GetActiveItem() == null)
                    {
                         SendReply(player, lang.GetMessage("PleaseHoldAnItemThatCanBeRenamed", this,player.UserIDString));
                    }
                    else
                    {
                        if (_config.items == null)
                        {
                            SendReply(player, lang.GetMessage("CannotDoThisRightNow", this, player.UserIDString));
                            return;
                        }
                        if (_config.bw == null)
                        {
                            SendReply(player, lang.GetMessage("CannotDoThisRightNow", this, player.UserIDString));
                            return;
                        }
                        foreach (var f in _config.items)
                        {
                            if (!player.GetActiveItem().info.shortname.Equals(f.ToLower())) continue;
                            SendReply(player, lang.GetMessage("PleaseHoldAnItemThatCanBeRenamed", this, player.UserIDString));
                            return;
                        }
                       
                        var name = args[0];
                        if (name.Length > _config.chars)
                        {
                            SendReply(player, lang.GetMessage("ItemNameTooLong", this,player.UserIDString));
                            return;
                        }
                        if (!_config.color && name.ToLower().Contains("color="))
                        {
                            SendReply(player, lang.GetMessage("YouCannotUseColor", this, player.UserIDString));
                            return;
                        }

                        foreach (var block in _config.bw)
                        {
                            if (!name.ToLower().Contains(block.ToLower())) continue;
                            SendReply(player, lang.GetMessage("BlacklistedWordsUsed", this, player.UserIDString));
                            return;
                        }
                        SendReply(player, string.Format(lang.GetMessage("ItemRenameSuccess", this, player.UserIDString), name));
                        var item = player.GetActiveItem();
                        player.inventory.containerBelt.Remove(item);
                        item.name = name;
                        player.inventory.GiveItem(item);
                    }

                }
            }
        }


        private ConfigFile _config;


        public class ConfigFile
        {
            [JsonProperty(PropertyName = "Max characters in a rename including color")]
            public int chars;

            [JsonProperty(PropertyName = "Blacklisted words")]
            public List<string> bw;

            [JsonProperty(PropertyName = "Blacklisted items")]
            public List<string> items;
            [JsonProperty(PropertyName = "Ability to use color")]
            public bool color;

            public static ConfigFile DefaultConfig()
            {
                return new ConfigFile()
                {
                    color = true,
                    chars = 25,
                    bw = new List<string>
                    {
                        "fuck",
                        "bitch"
                    },
                    items = new List<string>
                    {
                        "rifle.ak",
                        "rifle.bolt"
                    }
                };
            }

        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"NoPermission", "You don't have permission to do this!"},
                {"BlacklistedWordsUsed", "You cannot use those words in your item name"},
                {"ItemNameTooLong", "Your item name is too long. (max is {0} characters)"},
                {"PleaseHoldAnItemThatCanBeRenamed", "You cannot rename the item you're holding."},
                {"InvalidArgs", "Invalid Usage! Usage: /itemrename <name>"},
                {"ItemRenameSuccess", "We have renamed your item to {0}"},
                {"YouCannotUseColor", "You may not use color!"},
                {"CannotDoThisRightNow", "You cannot perform this command as of now, please contact an administrator about this."}
            }, this);
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            _config = Config.ReadObject<ConfigFile>();
            if (_config == null)
            {
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig()
        {
            _config = ConfigFile.DefaultConfig();
            PrintWarning("Default configuration has been loaded.");
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }

    }
}