using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("StickyChat", "recon", "0.0.8", ResourceId = 1443)]
    public class StickyChat : CovalencePlugin
    {
        public enum ChatType
        {
            GENERAL = 0,
            CLAN = 1,
            PRIVATE = 2,
            REPLY = 3
        }

        public class StickyInfo
        {
            public string details = "";
            public ChatType type = ChatType.GENERAL;
        }

        private void Loaded()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "Chatmode Switch", "You are currently chatting in {0} chat. Type /{1} to switch to {2}."},
                { "Private Chatmode", "You are currently chatting in {0} chat with {3}. Type /{1} to switch to {2}."},
                { "Current Chatmode", "You are currently chatting in {0} chat."},
                { "PM Error", "You need to specify player name you want to sticky message to. /pt [name]"},
                { "Commands", "You can use following sticky chat commands:"
                + "\n<color=green>/gt</color> - Stick to general chat."
                + "\n<color=green>/ct</color> - Stick to clan chat."
                + "\n<color=green>/pt [name]</color> - Stick to [name]'s chat."
                + "\n<color=green>/rt</color> - Stick to reply chat."}
            }, this);
        }

        private Dictionary<IPlayer, StickyInfo> stickies = new Dictionary<IPlayer, StickyInfo>();

        private void OnUserDisconnected(IPlayer player)
        {
            stickies.Remove(player);
        }

        [PluginReference("Clans")]
        private Plugin clanLib;

        [PluginReference("PrivateMessage")]
        private Plugin pmLib;

        [PluginReference("BetterChat")]
        private Plugin bcLib;

        [Command("ct")]
        private void clanChat(IPlayer player, string command, string[] args)
        {
            if (!stickies.Any(x => x.Key.Id == player.Id))
            {
                stickies.Add(player, new StickyInfo() { type = ChatType.CLAN });
                player.Reply(Lang("Chatmode Switch", player.Id, "CLAN", "gt", "GENERAL"));
            }
        }

        [Command("gt")]
        private void generalChat(IPlayer player, string command, string[] args)
        {
            if (stickies.Any(x => x.Key.Id == player.Id))
            {
                stickies.Remove(player);
                player.Reply(Lang("Current Chatmode", player.Id, "GENERAL"));
            }
        }

        [Command("rt")]
        private void replyChat(IPlayer player, string command, string[] args)
        {
            if (!stickies.Any(x => x.Key.Id == player.Id))
            {
                stickies.Add(player, new StickyInfo() { type = ChatType.REPLY });
                player.Reply(Lang("Chatmode Switch", player.Id, "REPLY", "gt", "GENERAL"));
            }
        }

        [Command("pt")]
        private void privateChat(IPlayer player, string command, string[] args)
        {
            if (args.Length != 1)
            {
                player.Reply(Lang("PM Error"), player.Id);
            }
            if (!stickies.Any(x => x.Key.Id == player.Id))
            {
                stickies.Add(player, new StickyInfo() { type = ChatType.PRIVATE, details = args[0] });
                player.Reply(Lang("Private Chatmode", player.Id, "PRIVATE", "gt", "GENERAL", args[0]));
            }
        }

        private object OnUserChat(IPlayer player, string message)
        {
            if (stickies.Any(x => x.Key.Id == player.Id && x.Value.type == ChatType.CLAN))
            {
                if (!bcLib)
                {
                    clanLib.Call("cmdChatClanchat", players.FindPlayerById(player.Id)?.Object as BasePlayer, "", new string[] { message });
                    clanLib.Call("cmdClanChat", player.Id, "", new string[] { message });
                }
                return false;
            }
            else if (stickies.Any(x => x.Key.Id == player.Id && x.Value.type == ChatType.REPLY))
            {
                if (!bcLib)
                {
                    pmLib.Call("cmdPmReply", players.FindPlayerById(player.Id)?.Object as BasePlayer, "", new string[] { message });
                }
                return false;
            }
            else if (stickies.Any(x => x.Key.Id == player.Id && x.Value.type == ChatType.PRIVATE))
            {
                if (!bcLib)
                {
                    StickyInfo result = (from el in stickies
                                         where el.Key.Id == player.Id && el.Value.type == ChatType.PRIVATE
                                         select el.Value).First();
                    pmLib.Call("cmdPm", players.FindPlayerById(player.Id)?.Object as BasePlayer, "", new string[] { result.details, message });
                }
                return false;
            }
            return null;
        }

        private object OnBetterChat(Dictionary<string, object> messageData)
        {
            IPlayer player = (IPlayer)messageData["Player"];
            string message = (string)messageData["Text"];
            if (stickies.Any(x => x.Key.Id == player.Id && x.Value.type == ChatType.CLAN))
            {
                clanLib.Call("cmdChatClanchat", players.FindPlayerById(player.Id)?.Object as BasePlayer, "", new string[] { message });
                clanLib.Call("cmdClanChat", player, "", new string[] { message });
                return false;
            }
            else if (stickies.Any(x => x.Key.Id == player.Id && x.Value.type == ChatType.REPLY))
            {
                pmLib.Call("cmdPmReply", players.FindPlayerById(player.Id)?.Object as BasePlayer, "", new string[] { message });
                return false;
            }
            else if (stickies.Any(x => x.Key.Id == player.Id && x.Value.type == ChatType.PRIVATE))
            {
                StickyInfo result = (from el in stickies
                                     where el.Key.Id == player.Id && el.Value.type == ChatType.PRIVATE
                                     select el.Value).First();
                pmLib.Call("cmdPm", players.FindPlayerById(player.Id)?.Object as BasePlayer, "", new string[] { result.details, message });
                return false;
            }
            return null;
        }

        private void SendHelpText(IPlayer player)
        {
            player.Reply(Lang("Commands", player.Id));
        }

        private int PlayerStickyState(IPlayer player)
        {
            if (stickies.Any(x => x.Key.Id == player.Id))
            {
                StickyInfo result = (from el in stickies
                                     where el.Key.Id == player.Id
                                     select el.Value).First();
                return Convert.ToInt32(result.type);
            }
            return 0;
        }

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
    }
}