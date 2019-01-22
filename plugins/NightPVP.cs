using Oxide.Game.Rust.Cui;
using UnityEngine;  //cui textanchor
using System.Collections.Generic;   //dict
using Convert = System.Convert;
using System.Linq;
using System;   //Math
using Oxide.Core;

namespace Oxide.Plugins
{
	[Info("Night PVP", "BuzZ[PHOQUE]", "0.1.7")]
	[Description("PVP only during night and PVE no damage during day")]

/*======================================================================================================================= 
*
*   
*   15th november 2018
*
*	THANKS TO THE OXIDE/UMOD TEAM for coding quality, ideas, and time spent for the community
*
*   0.1.6   20181123    null pve damage when not owner of entity    message timer/playerlist to avoid spamming
*
*=======================================================================================================================*/

	public class NightPVP : RustPlugin
	{
        string Prefix = "[NightPVP] ";                  // CHAT PLUGIN PREFIX
        string PrefixColor = "#bf0000";                 // CHAT PLUGIN PREFIX COLOR
        string ChatColor = "#dd8e8e";                   // CHAT MESSAGE COLOR
        ulong SteamIDIcon = 76561198079320022;  
        
        float starthour = 21;
        float stophour = 6;
        float rate = 10;

        private static string ONpvpHUD;
        private static string ONpveHUD;
        bool debug = false;
        private bool ConfigChanged;
        double leftmin = 0.95;
        double bottom = 0.86;
        int HUDtxtsize = 10;
        double HUDwidth = 0.05;
        double HUDheigth = 0.04;
        string HUDpvecolor = "0.5 1.0 0.0";
        string HUDpveopacity = "0.0";
        string HUDpvpcolor = "0.85 0.2 0.5";
        string HUDpvpopacity = "0.0";

        public List<BasePlayer> hasreceived = new List<BasePlayer>();

        void Init()
        {
            LoadVariables();
        }

        void OnServerInitialized()
        {
            if (storedData.NightPvpOn == false)NightIsOff();
            else NightIsOn();
            timer.Every(rate, () =>
            {   
                insidetimer();
            });
        }

        void insidetimer()
        {
            float gamehour = TOD_Sky.Instance.Cycle.Hour;
            int hournow = Convert.ToInt32(Math.Round(gamehour-0.5));
                if (hournow >= starthour || hournow < stophour)
                {
                    if (storedData.NightPvpOn == false){NightIsOn();}
                    storedData.NightPvpOn = true;
                }
                else
                {
                    if (storedData.NightPvpOn == true){NightIsOff();}
                    storedData.NightPvpOn = false;
                }
                if (debug == true){Puts($"{hournow} PVP is ON {storedData.NightPvpOn}");}
        }

        void Loaded()
        {
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
        }

        void Unload()
        {

            foreach (BasePlayer player in BasePlayer.activePlayerList.ToList())
            {
                KillHUD(player);
            }
            Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);
        }

        class StoredData
        {
            public bool NightPvpOn;

            public StoredData()
            {
            }
        }
        private StoredData storedData;

#region MESSAGES

        protected override void LoadDefaultMessages()
        {

            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"PVEMsg", "Night PVP turned"},
                {"PVPMsg", "Night PVP turned"},
                {"nulledMsg", "PVP Damage nulled, this is PVE time"},
                {"daysafeMsg", "<size=11><color=green>DAY SAFE</color></size>\nPVE"},
                {"nightpvpMsg", "<size=11><color=red>NIGHT</color></size>\nPVP"},

            }, this, "en");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"PVEMsg", "La nuit PVP est terminée"},
                {"PVPMsg", "La nuit PVP a commencée"},
                {"nulledMsg", "Dommages PVP ignorés, vous êtes dans le créneau PVE"},
                {"daysafeMsg", "<size=11><color=green>PROTECTION</color></size>\nPVE"},
                {"nightpvpMsg", "<size=11><color=red>NUIT</color></size>\nPVP"},

            }, this, "fr");
        }

#endregion

#region CONFIG

    protected override void LoadDefaultConfig()
        {
            LoadVariables();
        }

        private void LoadVariables()
        {
            Prefix = Convert.ToString(GetConfig("Chat Settings", "Prefix", "[NightPVP] "));                       // CHAT PLUGIN PREFIX
            PrefixColor = Convert.ToString(GetConfig("Chat Settings", "PrefixColor", "#bf0000"));                // CHAT PLUGIN PREFIX COLOR
            ChatColor = Convert.ToString(GetConfig("Chat Settings", "ChatColor", "#dd8e8e"));                    // CHAT MESSAGE COLOR
            SteamIDIcon = Convert.ToUInt64(GetConfig("Chat Settings", "SteamIDIcon", "76561198079320022"));        // SteamID FOR PLUGIN ICON - STEAM PROFILE CREATED FOR THIS PLUGIN / NONE YET /
            starthour = Convert.ToSingle(GetConfig("Night Time Zone", "Start at", "21"));
            stophour = Convert.ToSingle(GetConfig("Night Time Zone", "Stop at", "6"));
            leftmin = Convert.ToDouble(GetConfig("HUD position", "left (0.95 by default)", "0.95"));
            bottom = Convert.ToDouble(GetConfig("HUD position", "bottom (0.86 by default)", "0.86"));
            HUDtxtsize = Convert.ToInt32(GetConfig("HUD text size", "(10 by default)", "10"));
            HUDwidth = Convert.ToDouble(GetConfig("HUD size", "width (0.05 by default)", "0.05"));
            HUDheigth = Convert.ToDouble(GetConfig("HUD size", "heigth (0.04 by default)", "0.04"));
            HUDpvecolor = Convert.ToString(GetConfig("HUD color", "for PVE", "0.5 1.0 0.0"));                    // CHAT MESSAGE COLOR
            HUDpveopacity = Convert.ToString(GetConfig("HUD opacity", "for PVE", "0.0"));                    // CHAT MESSAGE COLOR
            HUDpvpcolor = Convert.ToString(GetConfig("HUD color", "for PVP", "0.85 0.2 0.5"));                    // CHAT MESSAGE COLOR
            HUDpvpopacity = Convert.ToString(GetConfig("HUD opacity", "for PVP", "0.0"));                    // CHAT MESSAGE COLOR

            //rate = Convert.ToSingle(GetConfig("", "", "300"));

            if (!ConfigChanged) return;
            SaveConfig();
            ConfigChanged = false;
        }

        private object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                ConfigChanged = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                ConfigChanged = true;
            }
            return value;
        }

#endregion

        private void NightIsOn()
        {
            ChatPlayerOnline("pvp");
            if (debug == true){Puts($"Night PVP turned ON");}
            foreach (BasePlayer player in BasePlayer.activePlayerList.ToList())
            {
                CuiHelper.DestroyUi(player, ONpvpHUD);
                CuiHelper.DestroyUi(player, ONpveHUD);
                DisplayPVP(player);
            }
		}

        private void NightIsOff()
        {
            ChatPlayerOnline("pve");
            if (debug == true){Puts($"Night PVP turned OFF");}
            foreach (BasePlayer player in BasePlayer.activePlayerList.ToList())
            {
                CuiHelper.DestroyUi(player, ONpvpHUD);
                CuiHelper.DestroyUi(player, ONpveHUD);
                DisplayPVE(player);
            }
        }

        private void DisplayPVE(BasePlayer player)
        {
			    var CuiElement = new CuiElementContainer();
			    ONpveHUD = CuiElement.Add(new CuiPanel{Image ={Color = $"{HUDpvecolor} {HUDpveopacity}"},RectTransform ={AnchorMin = $"{leftmin} {bottom}",AnchorMax = $"{leftmin+HUDwidth} {bottom +HUDheigth}"},CursorEnabled = false
                }, new CuiElement().Parent = "Overlay", ONpveHUD);
          		
			    CuiElement.Add(new CuiLabel{Text ={Text = $"{lang.GetMessage("daysafeMsg", this, player.UserIDString)}",FontSize = HUDtxtsize,Align = TextAnchor.MiddleCenter,
                Color = "1 1 1 1"},RectTransform ={AnchorMin = "0.10 0.10",   AnchorMax = "0.90 0.79"}
				}, ONpveHUD);

			    CuiHelper.AddUi(player, CuiElement);
        }

        private void DisplayPVP(BasePlayer player)
        {
			    var CuiElement = new CuiElementContainer();
			    ONpvpHUD = CuiElement.Add(new CuiPanel{Image ={Color = $"{HUDpvpcolor} {HUDpvpopacity}"},RectTransform ={AnchorMin = $"{leftmin} {bottom}",AnchorMax = $"{leftmin+HUDwidth} {bottom +HUDheigth}"},CursorEnabled = false
                }, new CuiElement().Parent = "Overlay", ONpvpHUD);
          		
			    CuiElement.Add(new CuiLabel{Text ={Text = $"{lang.GetMessage("nightpvpMsg", this, player.UserIDString)}",FontSize = HUDtxtsize,Align = TextAnchor.MiddleCenter,
                Color = "1 1 1 1"},RectTransform ={AnchorMin = "0.10 0.10",   AnchorMax = "0.90 0.79"}
				}, ONpvpHUD);

			    CuiHelper.AddUi(player, CuiElement);
        }

        private void KillHUD(BasePlayer player)
        {
                CuiHelper.DestroyUi(player, ONpvpHUD);
                CuiHelper.DestroyUi(player, ONpveHUD);
        }

#region PVE FRIENDLY STYLE but can kill NPC and be killed by NPC

        void OnEntityTakeDamage(BaseEntity entity, HitInfo info)
        {
            if (storedData.NightPvpOn == true){return;}
            if (entity == null || info == null) return;
            BasePlayer goodguy = entity as BasePlayer;
            if (info.Initiator == null) return;
            BasePlayer badguy = info.Initiator as BasePlayer;
            if (badguy == null) return;
            ////// RAIDING
            bool raiding = IsRaid(entity, badguy, info);
            if (raiding)return;
            ////// PLAYER vs PLAYER
            if (goodguy == null || badguy == null) return;
            //if (debug == true){Puts($"{goodguy.UserIDString.IsSteamID()}");}
            bool goodonline = IsReal(goodguy);
            bool badonline = IsReal(badguy);
            if (debug == true){Puts($"BLESSE {goodonline} - TIREUR {badonline}");}
            if (goodonline == false || badonline == false) {return;}    // REAL PLAYER vs PLAYER FILTER
            info.damageTypes.ScaleAll(0);
            AntiSpamage(badguy);
            AntiSpamage(goodguy);
            if (debug == true){Puts($"damage nulled");}
        }

        public bool IsReal(BasePlayer check)
        {
            if (debug == true){Puts($"bool IsReal");}
            if (BasePlayer.activePlayerList.ToList().Contains(check) == true)
            {
                return true;
            }
            if (BasePlayer.sleepingPlayerList.ToList().Contains(check) == true)
            {
                return true;  
            }
            else
            {
                return false;
            }
        }   

        public bool IsRaid(BaseEntity entity, BasePlayer badguy, HitInfo info)
        {
            if (debug == true)Puts($"VOID RAID");
            if (entity is BuildingBlock || entity.name.Contains("deploy") || entity.name.Contains("building"))
            {
                if (entity.OwnerID != badguy.userID)
                {
                    if (debug == true)Puts($"RAID !!!");
                    info.damageTypes.ScaleAll(0);
                    AntiSpamage(badguy);
                    return true;
                }
                else
                {
                    if (debug == true)Puts($"OWNER DAMAGE OWN ENTITY {entity.OwnerID}");
                    return true;
                }
            }
            else
            {
                return false;
            }
        }

#endregion

        private void AntiSpamage(BasePlayer player)
        {
            if (hasreceived.Contains(player))return;
            else
            {
                Player.Message(player, $"<color={ChatColor}>{lang.GetMessage("nulledMsg", this, player.UserIDString)}</color>",$"<color={PrefixColor}> {Prefix} </color>", SteamIDIcon);
                hasreceived.Add(player);
                timer.Once(20f, () =>
                {
                    hasreceived.Remove(player);
                });
            }
        }

#region CHAT MESSAGE TO ONLINE PLAYER

        private void ChatPlayerOnline(string status)
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList.ToList())
            {
                if (status == "pve")
                {
                    Player.Message(player, $"<color={ChatColor}>{lang.GetMessage("PVEMsg", this, player.UserIDString)}</color> <color=red>OFF</color>",$"<color={PrefixColor}> {Prefix} </color>", SteamIDIcon);
                }
                else
                {
                    Player.Message(player, $"<color={ChatColor}>{lang.GetMessage("PVPMsg", this, player.UserIDString)}</color> <color=green>ON</color>",$"<color={PrefixColor}> {Prefix} </color>", SteamIDIcon);
                }
            }
        }

#endregion

        void OnPlayerSleepEnded(BasePlayer player)
        {
            KillHUD(player);
            if (storedData.NightPvpOn == false){DisplayPVE(player);}
            else{DisplayPVP(player);}
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
                KillHUD(player);
        }


    }
}
