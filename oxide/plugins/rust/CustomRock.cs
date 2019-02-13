using System.Collections.Generic;
using Oxide.Core;
using System;
using System.Text;


namespace Oxide.Plugins
{
    [Info("Custom Rock", "birthdates", "0.5", ResourceId = 0)]
    [Description("Custom rock when you spawn.")]
    public class CustomRock : RustPlugin
    {
        private class StoredData
        {
            public readonly Dictionary<string, ulong> delayedRocks = new Dictionary<string, ulong>();
            public readonly Dictionary<string, ulong> rocks = new Dictionary<string, ulong>();
        }
        private StoredData sd;

        private ulong DefaultRockSkin;

        protected override void LoadDefaultConfig() {
            Config["DefaultRockSkin"] = 1;
        }        

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"NoPermission", "You don't have any permission."},
                {
                    "RespawnMessage",
                    "Thank you for supporting the server! For this, we have granted you a custom rock skin"
                },
                {"ValidArgs", "Please specify a valid skin id"},
                {
                    "ResettingSkinAtSave",
                    "Considering you already have a skin, to keep the server lag free(and spam free): We are going to reset your rock skin when the server saves"
                },
                {"SuccessMessage", "Success! We have set your rock skin to {0}!"},
                {"SaveSuccessMessage", "Success! The server has saved and your rock skin has been updated."}
            }, this);
        }

        private void Init()
        {
            permission.RegisterPermission("customrock.use", this);
            permission.RegisterPermission("customrock.spawnwith", this);
            sd = Interface.Oxide.DataFileSystem.ReadObject<StoredData>("CustomRock");
            DefaultRockSkin = ulong.Parse(Config["DefaultRockSkin"].ToString());
        }

        private void OnPlayerRespawned(BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, "customrock.spawnwith")) GiveKit(player);
            
        }

        private void OnServerSave()
        {
            if (sd.delayedRocks.Count > 0) {
                foreach (var p in sd.delayedRocks.Keys)
                {
                    ulong skin;
                    if (sd.delayedRocks.TryGetValue(p, out skin)) {
                        sd.rocks[p] = skin;
                        SendReply(Player.Find(p), lang.GetMessage("SaveSuccessMessage",this,p));
                    }                      
                }
            }
            Interface.Oxide.DataFileSystem.WriteObject("CustomRock", sd);
            sd.delayedRocks.Clear();
            
        }


        private void GiveKit(BasePlayer player)
        {
            

            ulong skin = DefaultRockSkin;
            
            sd.rocks.TryGetValue(player.UserIDString, out skin);
            foreach (var i in player.inventory.AllItems())
                if (i.info.shortname == "rock" && skin != null)
                {
                    var b = ItemManager.CreateByName("rock", 1, Convert.ToUInt32(Config["DefaultRockSkin"].ToString()));

                    if (i != null) i.Remove();

                    if (b != null) player.GiveItem(b);
                }
            if(skin != 0) {
                SendReply(player, lang.GetMessage("RespawnMessage", this, player.UserIDString));
            }
        }

        [ChatCommand("rskin")]
        private void rSkinCommand(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 1)
            {
                if (!permission.UserHasPermission(player.UserIDString, "customrock.use")) SendReply(player, lang.GetMessage("NoPermission", this, player.UserIDString));
                    
                else
                {
                    ulong s;
                    if (!ulong.TryParse(args[0], out s)) SendReply(player, lang.GetMessage("ValidArgs", this, player.UserIDString));
                    
                    else if(args[0].Length < 8 || args[0].Length > 10) SendReply(player, lang.GetMessage("ValidArgs", this, player.UserIDString));
                    
                    else
                    {
                        if (sd.rocks.ContainsKey(player.UserIDString))
                        {
                            sd.delayedRocks[player.UserIDString] = s;
                            SendReply(player, lang.GetMessage("ResettingSkinAtSave", this, player.UserIDString));
                        }
                        else
                        {
                            sd.rocks[player.UserIDString] = s;
                            Interface.Oxide.DataFileSystem.WriteObject("CustomRock", sd);
                            SendReply(player, lang.GetMessage("SuccessMessage", this, player.UserIDString),s);
                        }
                    }
                }
            }
            else SendReply(player, lang.GetMessage("ValidArgs", this, player.UserIDString));
                
        }


    }
    
}