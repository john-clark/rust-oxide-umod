using Oxide.Game.Rust.Cui;
using UnityEngine;  //cui textanchor
using System.Collections.Generic;   //dict
using Oxide.Core.Plugins;
using System.Linq;
using Convert = System.Convert;

namespace Oxide.Plugins
{
	[Info("Zone PVx Info", "BuzZ[PHOQUE]", "0.0.3")]
	[Description("HUD on PVx name defined Zones")]

/*======================================================================================================================= 
*
*   
*   15th november 2018
*   
*   0.0.3   get in dict player/zonein   for multiple zones go thru wich was buggy   + added config for hud/text/position
*
*    THANKS TO THE OXIDE/UMOD TEAM for coding quality, ideas, and time spent for the community
*
*
*
*=======================================================================================================================*/


	public class ZonePVxInfo : RustPlugin
	{

        [PluginReference] Plugin ZoneManager;

        /*string Prefix = "[ZPI] ";                  // CHAT PLUGIN PREFIX
        string PrefixColor = "#bf0000";                 // CHAT PLUGIN PREFIX COLOR
        string ChatColor = "#dd8e8e";                   // CHAT MESSAGE COLOR
        ulong SteamIDIcon = 76561198079320022;  
*/
        bool ConfigChanged;
        bool pvpall;
        bool debug = false;
        private static string PVXHUD;
        string pvpcolor = "0.6 0.2 0.2";
        string pvecolor = "0.5 1.0 0.0";
        string pvpcoloropacity = "0.5";
        string pvecoloropacity = "0.4";
        string pvptextcolor = "1.0 1.0 1.0";
        string pvetextcolor = "1.0 1.0 1.0";
        int textsize = 12;
        double HUDleft = 0.65;
        double HUDbottom = 0.04;

        Dictionary<BasePlayer, string> zoneyouare = new Dictionary<BasePlayer, string>();

        void Init()
        {
            LoadVariables();
        }
        
        void Unload()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList.ToList())
            {
                LetsDestroyUIz(player);
            }
        }

        void Loaded()
        {
            if (ZoneManager == false)
                {
                    PrintError("ZoneManager.cs is needed and not present.");
                }
        }

/*#region MESSAGES

    void LoadDefaultMessages()
    {

        lang.RegisterMessages(new Dictionary<string, string>
        {
            {"PVEinMsg", "You entered a PVE zone."},
            {"PVPinMsg", "You entered a PVP zone."},
            {"PVEoutMsg", "You leaved a PVE zone."},
            {"PVPoutMsg", "You leaved a PVP zone."},

        }, this, "en");

        lang.RegisterMessages(new Dictionary<string, string>
        {
            {"PVEinMsg", "Vous entrez en zone PVE."},
            {"PVPinMsg", "Vous entrez en zone PVP."},
            {"PVEoutMsg", "Vous quittez une zone PVP."},
            {"PVPoutMsg", "Vous quittez une zone PVP."},

        }, this, "fr");
    }

#endregion*/

#region CONFIG

    protected override void LoadDefaultConfig()
        {
            LoadVariables();
        }

        private void LoadVariables()
        {
            /*Prefix = Convert.ToString(GetConfig("Chat Settings", "Prefix", "[NightPVP] "));                       // CHAT PLUGIN PREFIX
            PrefixColor = Convert.ToString(GetConfig("Chat Settings", "PrefixColor", "#bf0000"));                // CHAT PLUGIN PREFIX COLOR
            ChatColor = Convert.ToString(GetConfig("Chat Settings", "ChatColor", "#dd8e8e"));                    // CHAT MESSAGE COLOR
            SteamIDIcon = Convert.ToUInt64(GetConfig("Chat Settings", "SteamIDIcon", "76561198079320022")); */       // SteamID FOR PLUGIN ICON - STEAM PROFILE CREATED FOR THIS PLUGIN / NONE YET /
            pvpcolor = Convert.ToString(GetConfig("HUD COLOR", "for PVP", "0.6 0.2 0.2"));
            pvecolor = Convert.ToString(GetConfig("HUD COLOR", "for PVE", "0.5 1.0 0.0"));
            pvpcoloropacity = Convert.ToString(GetConfig("HUD OPACITY", "for PVP", "0.5"));
            pvecoloropacity = Convert.ToString(GetConfig("HUD OPACITY", "for PVE", "0.4"));
            textsize = Convert.ToInt32(GetConfig("HUD TEXT", "size", "12"));
            pvptextcolor = Convert.ToString(GetConfig("HUD TEXT", "color for PVP", "1.0 1.0 1.0"));
            pvetextcolor = Convert.ToString(GetConfig("HUD TEXT", "color for PVE", "1.0 1.0 1.0"));
            HUDleft = Convert.ToDouble(GetConfig("HUD POSITION", "left", "0.65"));
            HUDbottom = Convert.ToDouble(GetConfig("HUD COLOR", "bottom", "0.04"));

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

//////////////////////////////

        void LetsDestroyUIz(BasePlayer player)
        {
            zoneyouare.Remove(player);
            CuiHelper.DestroyUi(player, PVXHUD);
        }

        string GetZoneName(string zoneID) => (string)ZoneManager?.Call("GetZoneName", zoneID);

        void OnEnterZone(string ZoneID, BasePlayer player)
        {
            if (pvpall) return;
            LetsDestroyUIz(player);
            if (ZoneID == null)
            {
                if (debug == true){Puts($"-> NULL ZONEID. IGNORED.");}
                return;
            }
            bool check = CheckThisID(player);
            if (check == false)
            {
                if (debug == true){Puts($"-> ENTER ZONE IGNORED.");}
                return;
            }
            string zonename = GetZoneName(ZoneID); 
            if (zonename == null)
            {
                if (debug == true){Puts($"-> NULL ZONE NAME. IGNORED.");}
                return;
            }
            zonename = zonename.ToLower();            
            string playername = player.displayName;
            if (zonename.Contains("pve") == true)
            {
                if (debug == true){Puts($"-> ENTERED IN PVE {ZoneID} - {zonename} : HUMAN PLAYER {playername}");}
                zoneyouare.Remove(player);
                zoneyouare.Add(player, "pve");
                DisplayPVXHUD (player);
                return;
            }
            if (zonename.Contains("pvp") == true)
            {
                if (debug == true){Puts($"-> ENTERED IN PVP {ZoneID} - {zonename} : HUMAN PLAYER {playername}");}
                zoneyouare.Remove(player);
                zoneyouare.Add(player, "pvp");
                DisplayPVXHUD (player);
            }
        }

        void OnExitZone(string ZoneID, BasePlayer player)
        {
            if (pvpall == true){return;}
            if (ZoneID == null)
            {
                if (debug == true){Puts($"-> NULL ZONEID. IGNORED.");}
                return;
            }            
            bool check = CheckThisID(player);
            if (check == false)
            {
                if (debug == true){Puts($"-> EXIT ZONE IGNORED.");}
                return;
            }
            string zonename = GetZoneName(ZoneID); 
            if (zonename == null)
            {
                if (debug == true){Puts($"-> NULL ZONE NAME. IGNORED.");}
                return;
            }
            zonename = zonename.ToLower();
            string playername = player.displayName;
            if (zonename.Contains("pve") || zonename.Contains("pvp"))
            {
                if (debug == true){Puts($"-> EXIT ZONE {ZoneID} - {zonename} : HUMAN PLAYER {playername}");}
                LetsDestroyUIz(player);
            }
        }

        private bool CheckThisID(BasePlayer player)
        {
            if (BasePlayer.activePlayerList.ToList().Contains(player) == true) return true;
            else return false;
        }

        [ChatCommand("pvpall_on")]         
        private void AllDaMapIsPVP(BasePlayer admin, string command, string[] args)
        {
            pvpall = true;
            foreach (BasePlayer player in BasePlayer.activePlayerList.ToList())
            {
                LetsDestroyUIz(player);
                zoneyouare.Add(player, "pvp");
                DisplayPVXHUD (player);
            }
        }

        [ChatCommand("pvpall_off")]         
        private void AllDaMapZones(BasePlayer admin, string command, string[] args)
        {
            pvpall = false;
            foreach (BasePlayer player in BasePlayer.activePlayerList.ToList())
            {
                LetsDestroyUIz(player);
            }
        }

        void DisplayPVXHUD (BasePlayer player)
        {
            string whatisdazone;
            string zonetype = "PVP\nZONE";
            string zonecolor = pvpcolor;
            string zonecoloropac = pvpcoloropacity;
            string textcolor = pvptextcolor;
            zoneyouare.TryGetValue(player, out whatisdazone);
            if (whatisdazone == null) return;
            if (whatisdazone == "pve")
            {
                zonetype = "PVE\nZONE";
                zonecolor = pvecolor;
                textcolor = pvetextcolor;
                zonecoloropac = pvecoloropacity;
            }

            var CuiElement = new CuiElementContainer();
            PVXHUD = CuiElement.Add(new CuiPanel{Image ={Color = $"{zonecolor} {zonecoloropac}"},RectTransform ={AnchorMin = $"{HUDleft} {HUDbottom}",AnchorMax = $"{HUDleft+0.04} {HUDbottom+0.04}"},CursorEnabled = false
                }, new CuiElement().Parent = "Overlay", PVXHUD);
                    
            CuiElement.Add(new CuiLabel{Text ={Text = zonetype,FontSize = textsize,Align = TextAnchor.MiddleCenter,Color = $"{textcolor} 1.0"},RectTransform ={AnchorMin = "0.10 0.10",   AnchorMax = "0.90 0.90"}
                }, PVXHUD);
            CuiHelper.AddUi(player, CuiElement);
        }

        void OnPlayerSleepEnded(BasePlayer player)
        {
            if (pvpall == true)
            {
                zoneyouare.Remove(player);
                zoneyouare.Add(player, "pvp");
                DisplayPVXHUD (player);
            }

// NEED TO ADD if wake up in a zone -> isplayer in zone + get name etc.
//  isOnPlayer

        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            LetsDestroyUIz(player);
        }
    }
}
