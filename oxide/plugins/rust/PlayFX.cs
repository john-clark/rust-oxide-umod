using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("PlayFX", "Vliek", "1.0.3", ResourceId = 2810)]
    [Description("Play any Rust fx/effect on any specified player.")]

    class PlayFX : RustPlugin
    {
        string usagePerm = "playfx.use";
        bool Changed;
        bool localEffect;

        string GetLang(string msg, string userID) => lang.GetMessage(msg, this, userID);

        protected override void LoadDefaultConfig()
        {
            Puts("Creating a new config file.");
            Config.Clear();
            LoadVariables();
        }
        private void LoadVariables()
        {
            localEffect = Convert.ToBoolean(GetConfig("Settings", "Play effect locally", true));

            if (!Changed) return;
            SaveConfig();
            Changed = false;
        }

        object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                Changed = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                Changed = true;
            }
            return value;
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["usageExample"] = "Usage example: /playfx 76561198238497190 assets/bundled/prefabs/fx/gestures/drink_vomit.prefab",
                ["noPlayerFound"] = "No player matching this SteamID",
                ["noPermission"] = "You don't have the required permission to play effects on players."
            }, this);
        }

        void Init()
        {
            permission.RegisterPermission(usagePerm, this);
        }


        [ChatCommand("playfx")]
        void CmdPlayFX(BasePlayer player, string command, string[] args)
        {
            if (haspermission(player))
            {
                playeffect(player, args);
                return;
            }
            Player.Reply(player, GetLang("noPermission", player.UserIDString));
        }

        [ConsoleCommand("playfx")]
        void CCmdPlayFX(ConsoleSystem.Arg args)
        {
            if(args.Player() == null)
            {
                if (args.Args == null)
                {
                    Puts(GetLang("usageExample", null));
                    return;
                }
                playeffect(null, args.Args);
            }
            else
            {
                if (args.Args == null)
                {
                    Player.Reply(args.Player(), GetLang("usageExample", args.Player().UserIDString));
                    return;
                }
                playeffect(args.Player(), args.Args);
            }
        }

        private void playeffect(BasePlayer player, string[] args)
        {

            if(player == null)
            {
                if (args.Length != 2)
                {
                    Puts(GetLang("usageExample", null));
                    return;
                }
                string argTarget = args[0];
                string effect = args[1];
                if (converttoulong(argTarget))
                {
                    ulong effectTarget = Convert.ToUInt64(argTarget);
                    BasePlayer finaltarget = BasePlayer.FindByID(effectTarget);
                    if (finaltarget == null)
                    {
                        Puts(GetLang("noPlayerFound", null));
                        return;
                    }
                    var finaleffect = new Effect(effect, finaltarget, 0, Vector3.zero, Vector3.forward);
                    if(localEffect)
                    {
                        EffectNetwork.Send(finaleffect, finaltarget.net.connection);
                    }
                    else
                    {
                        EffectNetwork.Send(finaleffect);
                    }                  
                    Puts("Effect ran on " + finaltarget.displayName);
                    return;
                }
            }
            else
            {
                if (args.Length != 2)
                {
                    Player.Reply(player, GetLang("usageExample", player.UserIDString));
                    return;
                }
                string argTarget = args[0];
                string effect = args[1];
                if(converttoulong(argTarget))
                {
                    ulong effectTarget = Convert.ToUInt64(argTarget);
                    BasePlayer finaltarget = BasePlayer.FindByID(effectTarget);
                    if (finaltarget == null)
                    {
                        Player.Reply(player, GetLang("noPlayerFound", player.UserIDString));
                        return;
                    }
                    var finaleffect = new Effect(effect, finaltarget, 0, Vector3.zero, Vector3.forward);
                    if (localEffect)
                    {
                        EffectNetwork.Send(finaleffect, finaltarget.net.connection);
                    }
                    else
                    {
                        EffectNetwork.Send(finaleffect);
                    }
                    Player.Reply(player, "Effect ran on " + finaltarget.displayName);
                    return;
                }
            }

        }

        private bool haspermission(BasePlayer player)
        {
            if (player == null)
                return true;
            if (permission.UserHasPermission(player.UserIDString, usagePerm))
                return true;
            return false;
        }

        private bool converttoulong(string argTarget)
        {
            try
            {
                ulong converttest = Convert.ToUInt64(argTarget);
            }
            catch (FormatException e)
            {
                Puts(e.Message);
                return false;
            }
            return true;
        }
    }
}