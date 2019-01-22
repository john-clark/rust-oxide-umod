using UnityEngine;
using System.Collections.Generic;
using System;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Signal Cooldown", "Vliek", "1.0.3", ResourceId = 2805)]
    [Description("Add a cooldown to supply signals to avoid that players are going to leave from rage.")]
    class SignalCooldown : RustPlugin
    {
        private bool Changed;
        private bool refundSignal;
        private int timeCooldown;
        private string cooldownPerm;
        private ulong messageIcon;
        private int authLevel;

        public List<ulong> signalCooldown = new List<ulong>();
        public Dictionary<ulong, float> lastRun = new Dictionary<ulong, float>();

        string GetLang(string msg, string userID) => lang.GetMessage(msg, this, userID);

        protected override void LoadDefaultConfig()
        {
            Puts("Creating a new config file.");
            Config.Clear();
            LoadVariables();
        }
        private void LoadVariables()
        {
            refundSignal = Convert.ToBoolean(GetConfig("Settings", "Refund signal", true));
            timeCooldown = Convert.ToInt32(GetConfig("Settings", "Cooldown in seconds", 300));
            cooldownPerm = Convert.ToString(GetConfig("Settings", "Ignore cooldown permission", "signalcooldown.ignore"));
            messageIcon = Convert.ToUInt64(GetConfig("Settings", "Icon for message", 0));
            authLevel = Convert.ToInt32(GetConfig("Settings", "Auth level admin commands", 1));

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
                ["cooldownActive"] = "<color=#b7b7b7>Supplysignal cooldown active, wait {0} seconds till you can throw a signal again.</color>",
                ["cooldownOver"] = "<color=#b7b7b7>You are able to throw a supplysignal again.</color>",
                ["noPermission"] = "<color=#b7b7b7>You don't have the required auth level to run this command.</color>",
                ["manualReset"] = "<color=#b7b7b7>All supplysignal cooldowns have been manually reset.</color>"
            }, this);
        }

        private void Init()
        {
            LoadVariables();
            permission.RegisterPermission(cooldownPerm, this);
        }

        private void OnExplosiveDropped(BasePlayer player, BaseEntity entity) => OnExplosiveThrown(player, entity);

        private void OnExplosiveThrown(BasePlayer player, BaseEntity entity)
        {
            if (entity.ShortPrefabName != "grenade.supplysignal.deployed")
                return;
            if (!permission.UserHasPermission(player.UserIDString, cooldownPerm))
            {
                if (signalCooldown.Contains(player.userID))
                {
                    float difference = (lastRun[player.userID] + timeCooldown) - UnityEngine.Time.time;
                    float finaldifference = Mathf.Round(difference);
                    entity.Kill();
                    Player.Message(player, GetLang("cooldownActive", player.UserIDString), null, messageIcon, finaldifference);
                    if (refundSignal)
                    {
                        Item item = ItemManager.CreateByName("supply.signal", 1);
                        if (item != null)
                        {
                            player.inventory.GiveItem(item, player.inventory.containerBelt);
                        }
                    }
                    return;
                }

                signalCooldown.Add(player.userID);
                lastRun.Add(player.userID, UnityEngine.Time.time);
                timer.Once(timeCooldown, () =>
                {
                    signalCooldown.Remove(player.userID);
                    lastRun.Remove(player.userID);
                    Player.Message(player, GetLang("cooldownOver", player.UserIDString), null, messageIcon);
                });
                Puts("Signal cooldown started on " + player.displayName);
                return;
            }
            return;
        }

        [ChatCommand("signalreset")]
        private void ResetTimeCMD(BasePlayer player, string command, string[] args)
        {
            if (player.Connection.authLevel < authLevel)
            {
                Player.Message(player, GetLang("noPermission", player.UserIDString), null, messageIcon);
                return;
            }
            Player.Message(player, GetLang("manualReset", player.UserIDString), null, messageIcon);
            Reset();
        }

        void Reset()
        {
            signalCooldown.Clear();
            lastRun.Clear();
            Puts("All cooldowns have been manually reset.");
        }
    }
}