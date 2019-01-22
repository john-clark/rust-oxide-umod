using System.Collections.Generic;
using System;
using System.Text.RegularExpressions;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Oxide.Plugins
{
    [Info ("VPN Block", "Calytic", "0.0.53")]
    class VPNBlock : CovalencePlugin
    {
        List<string> allowedISPs = new List<string> ();
        string IPHUB_API_KEY;
        string IPSTACK_API_KEY;
        Dictionary<string, string> headers = new Dictionary<string, string> ();
        string unauthorizedMessage;
        bool debug = false;

        void Loaded ()
        {
            LoadData ();
            LoadMessages ();
            permission.RegisterPermission ("vpnblock.canvpn", this);
            IPHUB_API_KEY = GetConfig ("IPHub", "apiKey", string.Empty);
            IPSTACK_API_KEY = GetConfig ("IPStack", "apiKey", string.Empty);
            debug = GetConfig ("Debug", false);
            if (string.IsNullOrEmpty (IPHUB_API_KEY) && string.IsNullOrEmpty (IPSTACK_API_KEY)) {
                Unsubscribe ("OnUserConnected");
            }

            if (!string.IsNullOrEmpty (IPHUB_API_KEY)) {
                headers.Add ("X-Key", IPHUB_API_KEY);
            }

            unauthorizedMessage = GetMsg ("Unauthorized");
        }

        protected override void LoadDefaultConfig ()
        {
            Config ["IPHub", "apiKey"] = "";
            Config ["IPStack", "apiKey"] = "";
            Config ["Debug"] = false;
        }

        void LoadData ()
        {
            allowedISPs = Interface.Oxide.DataFileSystem.ReadObject<List<string>> ("vpnblock_allowedisp");
        }

        void SaveData ()
        {
            Interface.Oxide.DataFileSystem.WriteObject ("vpnblock_allowedisp", allowedISPs);
        }

        [Command ("wisp")]
        void WhiteListISP (IPlayer player, string command, string [] args)
        {
            if (!IsAllowed (player)) return;

            if (args.Length == 0) {
                player.Reply (GetMsg ("WISP Invalid", player.Id));
                return;
            }

            allowedISPs.Add (string.Join (" ", args));

            player.Reply (GetMsg ("ISP Whitelisted", player.Id));
            SaveData ();
        }

        void LoadMessages ()
        {
            lang.RegisterMessages (new Dictionary<string, string>
            {
                {"Unauthorized", "Unauthorized.  ISP/VPN not permitted"},
                {"Is Banned", "{0} is trying to connect from proxy VPN/ISP {1}"},
                {"ISP Whitelisted", "ISP Whitelisted"},
                {"WISP Invalid", "Syntax Invalid. /wisp [ISP NAME]"},
            }, this);
        }

        bool IsAllowed (IPlayer player)
        {
            if (player.IsAdmin) return true;
            return false;
        }

        bool hasAccess (IPlayer player, string permissionname)
        {
            if (player.IsAdmin) return true;
            return permission.UserHasPermission (player.Id, permissionname);
        }

        void OnUserConnected (IPlayer player)
        {
            if (hasAccess (player, "vpnblock.canvpn")) {
                return;
            }

            string ip = player.Address;
            string url = string.Empty;
            if (!string.IsNullOrEmpty (IPHUB_API_KEY)) {
                url = string.Format ("http://v2.api.iphub.info/ip/{0}", ip);
                webrequest.Enqueue (url, string.Empty, (code, response) => HandleIPHubResponse (player, url, ip, code, response), this, RequestMethod.GET, headers);
            }

            if (!string.IsNullOrEmpty (IPSTACK_API_KEY)) {
                url = string.Format ("http://api.ipstack.com/{0}?access_key={1}", ip, IPSTACK_API_KEY);
                webrequest.Enqueue (url, string.Empty, (code, response) => HandleIPStackResponse (player, url, ip, code, response), this);
            }
        }

        void HandleIPHubResponse (IPlayer player, string url, string ip, int code, string response)
        {
            if (code != 200 || string.IsNullOrEmpty (response)) {
                PrintError ("Service temporarily offline");
            } else {
                Dictionary<string, object> jsonresponse;
                try {
                    jsonresponse = JsonConvert.DeserializeObject<Dictionary<string, object>> (response);
                } catch (JsonReaderException e) {
                    PrintWarning ("Error parsing url response: {0}", url);
                    return;
                }

                if (debug) {
                    Log (response);
                }

                if (jsonresponse ["block"] != null) {
                    var playerVpn = (jsonresponse ["block"].ToString ());

                    if (jsonresponse ["asn"] == null) {
                        LogWarning ("IPHub response does not include asn information");
                        return;
                    }

                    var playerIsp = (jsonresponse ["asn"].ToString ());

                    if (IsWhitelisted (playerIsp)) return;

                    if (playerVpn == "1") {
                        player.Kick (unauthorizedMessage);
                        LogWarning (GetMsg ("Is Banned"), $"{player.Name} ({player.Id}/{ip})", playerIsp);
                    }
                } else {
                    LogWarning ("IPHub response does not include block information");
                }
            }
        }

        void HandleIPStackResponse (IPlayer player, string url, string ip, int code, string response)
        {
            if (code != 200 || string.IsNullOrEmpty (response)) {
                LogError ("Service temporarily offline");
            } else {
                JObject json;
                try {
                    json = JObject.Parse (response);
                } catch (JsonReaderException e) {
                    LogWarning ("Error parsing URL response: {0}", url);
                    return;
                }

                if (debug) {
                    Log (response);
                }

                if (json ["error"] != null) {
                    LogWarning (json ["error"] ["info"].ToString ().Split (new char [] { '.' }, 2) [1]);
                    return;
                }

                if (json ["security"] != null) {
                    if (json ["security"] ["is_proxy"] == null) {
                        LogWarning ("IPStack response does not include proxy information");
                        return;
                    }

                    if (json ["connection"] == null) {
                        LogWarning ("IPStack response does not include connection information");
                        return;
                    }

                    if (json ["connection"] ["asn"] == null) {
                        LogWarning ("IPStack response does not include asn information");
                        return;
                    }

                    string playerVpn = json ["security"] ["is_proxy"].ToString ();
                    string playerIsp = json ["connection"] ["asn"].ToString ();

                    if (IsWhitelisted (playerIsp)) return;

                    if (playerVpn == "1") {
                        player.Kick (unauthorizedMessage);
                        LogWarning (GetMsg ("Is Banned"), $"{player.Name} ({player.Id}/{ip})", playerIsp);
                    }
                } else {
                    LogWarning ("IPStack response does not include security information");
                }
            }
        }

        bool IsWhitelisted (string playerIsp)
        {
            foreach (string isp in allowedISPs) {
                if (playerIsp.Contains (isp)) {
                    return true;
                }
            }

            return false;
        }

        T GetConfig<T> (string name, string name2, T defaultValue)
        {
            if (Config [name, name2] == null) {
                return defaultValue;
            }

            return (T)Convert.ChangeType (Config [name, name2], typeof (T));
        }

        T GetConfig<T> (string name, T defaultValue)
        {
            if (Config [name] == null)
                return defaultValue;

            return (T)Convert.ChangeType (Config [name], typeof (T));
        }

        string GetMsg (string key, object userID = null)
        {
            return lang.GetMessage (key, this, userID == null ? null : userID.ToString ());
        }
    }
}
