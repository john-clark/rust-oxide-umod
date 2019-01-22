using System;
using System.Linq;
using System.Collections.Generic;

using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("AngryPromotion", "Tori1157", "1.1.1", ResourceId = 2686)]
    [Description("Automatically add users to a group if they have a specific word/phrase in their steam name.")]

    class AngryPromotion : CovalencePlugin
    {
        #region Fields

        private bool Changed;
        private bool printToConsole;
        private bool informPlayer;
        private bool groupAdding;
        private bool useGroupInfo;
        private bool removeReward;
        private bool useThanksMessage;

        private string messagePrefix;
        private string messageColor;
        private string promotionKey;
        private string groupKey;
        private string groupInformation;
        private string thanksMessage;

        private float messageDelay;

        private const string AdminPermission = "angrypromotion.admin";

        #endregion

        #region Loaded

        private void Init()
        {
            permission.RegisterPermission(AdminPermission, this);

            LoadVariables();
            LoadData(ref promoters);
            SaveData(promoters);

            if (!permission.GroupExists(groupKey))
            {
                PrintWarning(lang.GetMessage("Group NonExistent", this).Replace("{group}", groupKey));
            }
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new config file");
            Config.Clear();
            LoadVariables();
        }

        private void LoadVariables()
        {
            /// -- MESSAGING -- ///

            informPlayer = Convert.ToBoolean(GetConfig("Messaging Options", "Inform Player", true));
            messageColor = Convert.ToString(GetConfig("Messaging Options", "Message Color", "#ffa500"));
            messagePrefix = Convert.ToString(GetConfig("Messaging Options", "Message Prefix", "Angry Promotion"));
            printToConsole = Convert.ToBoolean(GetConfig("Messaging Options", "Print To Console", true));
            messageDelay = Convert.ToSingle(GetConfig("Messaging Options", "Chat Message Delay", 10));
            useThanksMessage = Convert.ToBoolean(GetConfig("Messaging Options", "Use Thanks Message", true));
            groupInformation = Convert.ToString(GetConfig("Messages", "Group Information", new List<string>
            {
                "As long as you have [#00ffff]{promotionkey}[/#] word/phrase in your name you will gain access to the items listed below, if you remove the phrase from your name you will lose access.\n",
                "//----------------------------//",
                "- [#ffa500]Remover Tool[/#]",
                "- [#ffa500]1.5% more SRP[/#]",
                "- [#008000]Colored Name[/#]",
                "//----------------------------//",
                "[#ff0000][i][+12]For more information contact an Administrator[/+][/i][/#]"
            }));
            thanksMessage = Convert.ToString(GetConfig("Messages", "Thanks Message", new List<string>
            {
                "[+18][#ffa500]Thank you {player}![/#][/+]\n",
                "As a thanks for having [#00ffff]{promotionkey}[/#] in your name, we're giving you access to [#00ffff]{groupkey}[/#] group.",
                "\nTo learn more about what this group gives, type [#ffa500]/promotion group.[/#]"
            }));


            /// -- OPTIONS -- ///

            groupKey = Convert.ToString(GetConfig("Options", "Group", "promoter"));
            groupAdding = Convert.ToBoolean(GetConfig("Options", "Group Adding", true));
            promotionKey = Convert.ToString(GetConfig("Options", "PromotionKey", "yourwebsite.com"));
            useGroupInfo = Convert.ToBoolean(GetConfig("Options", "Use Group Info", true));
            removeReward = Convert.ToBoolean(GetConfig("Options", "Toggle Phrase Check For Removal", true));

            if (Changed)
            {
                SaveConfig();
                Changed = false;
            }
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                /// -- ERROR -- ///

                ["No Permission"] = "[#add8e6]{player}[/#] you do not have permission to use the [#00ffff]{command}[/#] command.",

                ["SteamID Not Found Chat"] = "SteamID [#00ffff]{id}[/#] could not be found.",
                ["Player Not Found Chat"] = "Username [#00ffff]{player}[/#] could not be found.",
                ["Multiple Players Found"] = "Multiple users found!\n\n{matches}",

                ["Invalid Parameter Chat"] = "Parameter [#00ffff]{parameter}[/#] is invalid or written wrong.",
                ["Invalid Syntax Check"] = "Invalid Syntax!  |  /promotion check \"User Name\"",
                ["Invalid Syntax Remove"] ="Invalid Syntax!  |  /promotion remove \"User Name\"",

                ["Group NonExistent"] = "Group '{group}' does not exist! Therefor users will not get put into groups!",

                /// -- CONFIRM -- ///
                
                ["Player Added Group"] = "You have been added to the [#00ffff]{group}[/#] group.",
                ["Player Removed Group"] = "You have been removed from the [#00ffff]{group}[/#] group, since you removed [#00ffff]{promotionkey}[/#] from your name.",

                ["Admin Removed User"] = "You have successfully removed [#00ffff]{target}[/#] from [#00ffff]{group}[/#] group.",

                /// -- INFO -- ///

                ["Default Message Group"] = "By having [#00ffff]{promotionkey}[/#] in your name you will gain access to the [#00ffff]{group}[/#] group automatically when you join.\n\nTo learn more about what [#00ffff]{group}[/#] group gives access to, type the following into chat: [#ffa500]/promotion group[/#].",
                ["Default Message"] = "By having [#00ffff]{promotionkey}[/#] in your name you will gain access to the [#00ffff]{group}[/#] group automatically when you join.",

                ["Player Is Promoting"] = "[#00ffff]{player}[/#] is currently promoting your server.",
                ["Player Not Promoting"] = "[#00ffff]{player}[/#] is currently [#ff0000]not[/#] promoting your server.",

                ["Admin Help1"] = "- [#ffa500]/promotion check[/#] [i](Checks to see if user is promoting)[/i]\n- [#ffa500]/promotion help[/#] [i](Displays this message)[/i]\n- [#ffa500]/promotion group[/#] [i](Displays the information regarding the group)[/i]\n- [#ffa500]/promotion[/#] [i](Displays the information regarding the plugin)[/i]\n- [#ffa500]/promotion remove \"User Name\"[/#] [i](Removes player from promotion group)[/i]",
                ["Player Help"] = "- [#ffa500]/promotion help[/#] [i](Displays this message)[/i]\n- [#ffa500]/promotion group[/#] [i](Displays the information regarding the group)[/i]\n- [#ffa500]/promotion[/#] [i](Displays the information regarding the plugin)[/i]",

            }, this);
        }

        #endregion

        #region Commands

        [Command("promotion")]
        private void PromotionCommand(IPlayer player, string command, string[] args)
        {
            var HasPerm = (player.HasPermission(AdminPermission));

            #region Default

            if (args.Length == 0)
            {
                if (useGroupInfo == true)
                {
                    SendInfoMessage(player, lang.GetMessage("Default Message Group", this, player.Id).Replace("{promotionkey}", promotionKey).Replace("{group}", groupKey));
                    return;
                }

                SendInfoMessage(player, lang.GetMessage("Default Message", this, player.Id).Replace("{promotionkey}", promotionKey).Replace("{group}", groupKey));
                return;
            }

            #endregion

            var CommandArg = args[0].ToLower();
            var CommandInfo = (command + " " + args[0]);
            var CaseArgs = (new List<object>
            {
                "check", "help", "group", "remove"
            });

            if (!CaseArgs.Contains(CommandArg))
            {
                SendChatMessage(player, lang.GetMessage("Invalid Parameter Chat", this, player.Id).Replace("{parameter}", CommandArg));
                return;
            }

            switch (CommandArg)
            {
                #region Group
                case "group":

                    string message = "";
                    foreach (var messageText in Config["Messages", "Group Information"] as List<object>)
                        message = message + messageText + "\n";

                    SendInfoMessage(player, message.Replace("{promotionkey}", promotionKey));

                return;
                #endregion

                #region Check
                case "check":

                    if (!HasPerm && !player.IsServer)
                    {
                        SendChatMessage(player, lang.GetMessage("No Permission", this, player.Id).Replace("{player}", player.Name).Replace("{command}", CommandInfo));
                        return;
                    }

                    if (args.Length == 1)
                    {
                        SendChatMessage(player, lang.GetMessage("Invalid Syntax Check", this, player.Id));
                        return;
                    }

                    IPlayer target;
                    target = GetPlayer(args[1], player);

                    if (target == null) return;

                    if (PromotingInfo.IsPromoting(target))
                    {
                        SendChatMessage(player, lang.GetMessage("Player Is Promoting", this, player.Id).Replace("{player}", target.Name));
                        return;
                    }

                    SendChatMessage(player, lang.GetMessage("Player Not Promoting", this, player.Id).Replace("{player}", target.Name));
                return;
                #endregion

                #region Help
                case "help":

                    if (HasPerm)
                    {
                        SendInfoMessage(player, lang.GetMessage("Admin Help1", this, player.Id));
                        return;
                    }

                    SendInfoMessage(player, lang.GetMessage("Player Help", this, player.Id));

                return;

                #endregion

                #region Remove
                case "remove":

                    if (!HasPerm && !player.IsServer)
                    {
                        SendChatMessage(player, lang.GetMessage("No Permission", this, player.Id).Replace("{player}", player.Name).Replace("{command}", CommandInfo));
                        return;
                    }

                    if (args.Length == 1)
                    {
                        SendChatMessage(player, lang.GetMessage("Invalid Syntax Remove", this, player.Id));
                        return;
                    }

                    IPlayer Target;
                    Target = GetPlayer(args[1], player);

                    if (Target == null) return;

                    if (!PromotingInfo.IsPromoting(Target))
                    {
                        SendChatMessage(player, lang.GetMessage("Player Not Promoting", this, player.Id).Replace("{player}", Target.Name));
                        return;
                    }

                    permission.RemoveUserGroup(Target.Id, groupKey);
                    promoters.Remove(Target.Id);
                    SaveData(promoters);

                    SendChatMessage(player, lang.GetMessage("Admin Removed User", this, player.Id).Replace("{target}", Target.Name).Replace("{group}", groupKey));
                return;
                #endregion
            }
        }

        #endregion

        #region Functions

        private void OnUserConnected(IPlayer player)
        {
            if (player == null) return;
            if (!permission.GroupExists(groupKey)) return;

            var PlayerName = player.Name.ToLower();
            var PlayerNameChecker = PlayerName.Contains(promotionKey.ToLower());
            var PlayerIsPromoting = PromotingInfo.IsPromoting(player);

            if (groupAdding == true)
            {
                // Checks to see if player has already been given permissions
                if (!PlayerIsPromoting)
                {
                    if (PlayerNameChecker && !PlayerIsPromoting)
                    {
                        /// ADDING THEM SINCE THEY HAVE KEY
                        
                        permission.AddUserGroup(player.Id, groupKey);
                        promoters[player.Id] = new PromotingInfo();
                        SaveData(promoters);

                        if (informPlayer == true)
                        {
                            string message = "";
                            foreach (var thanksText in Config["Messages", "Thanks Message"] as List<object>) // TODO: Clean this mess up
                                message = message + thanksText + "\n";

                            timer.Once(messageDelay, () =>
                            {
                                if (useThanksMessage == true)
                                {
                                    SendThanksMessage(player, message.Replace("{promotionkey}", promotionKey).Replace("{player}", player.Name).Replace("{groupkey}", groupKey));
                                    return;
                                }

                                SendChatMessage(player, lang.GetMessage("Player Added Group", this, player.Id).Replace("{group}", groupKey));
                            });
                        }

                        if (printToConsole == true)
                        {
                            Puts(lang.GetMessage("Player Added Group", this, player.Id).Replace("{player}", player.Name).Replace("{group}", groupKey));
                        }

                        return;
                    }
                }

                if (removeReward == true)
                {
                    if (!PlayerNameChecker && PlayerIsPromoting)
                    {
                        /// REMOVING SINCE THEY DO NOT HAVE KEY

                        permission.RemoveUserGroup(player.Id, groupKey);
                        promoters.Remove(player.Id);
                        SaveData(promoters);

                        if (informPlayer == true)
                        {
                            timer.Once(messageDelay, () =>
                            {
                                SendChatMessage(player, lang.GetMessage("Player Removed Group", this, player.Id).Replace("{group}", groupKey).Replace("{promotionkey}", promotionKey));
                            });
                        }

                        if (printToConsole == true)
                        {
                            Puts(lang.GetMessage("Player Removed Group", this, player.Id).Replace("{group}", groupKey).Replace("{promotionkey}", promotionKey));
                        }

                        return;
                    }
                }
            }
        }

        #endregion

        #region Helpers / Usefull Functions

        #region Config
        private object GetConfig(string menu, string datavalue, object defaultValue)
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
        #endregion

        #region Player Finding
        private IPlayer GetPlayer(string nameOrID, IPlayer player)
        {
            if (nameOrID.IsSteamId() == true)
            {
                IPlayer result = players.All.ToList().Find((p) => p.Id == nameOrID);

                if (result == null)
                    SendChatMessage(player, lang.GetMessage("SteamID Not Found Chat", this, player.Id).Replace("{id}", nameOrID));

                return result;
            }

            List<IPlayer> foundPlayers = new List<IPlayer>();

            foreach (IPlayer current in players.Connected)
            {
                if (current.Name.ToLower() == nameOrID.ToLower())
                    return current;

                if (current.Name.ToLower().Contains(nameOrID.ToLower()))
                    foundPlayers.Add(current);
            }

            switch (foundPlayers.Count)
            {
                case 0:
                        SendChatMessage(player, lang.GetMessage("Player Not Found Chat", this, player.Id).Replace("{player}", nameOrID));
                    break;

                case 1:
                    return foundPlayers[0];

                default:
                    string[] names = (from current in foundPlayers select current.Name).ToArray();
                    SendChatMessage(player, lang.GetMessage("Multiple Players Found Chat", this, player.Id).Replace("{matches}", string.Join(", ", names)));
                    break;
            }
            return null;
        }

        private bool IsParseableTo<T>(object s)
        {
            try
            {
                var parsed = (T)Convert.ChangeType(s, typeof(T));
                return true;
            }
            catch
            {
                return false;
            }
        }
        #endregion

        #region Data
        private static Dictionary<string, PromotingInfo> promoters = new Dictionary<string, PromotingInfo>();

        public class PromotingInfo
        {
            public static bool IsPromoting(IPlayer player) => promoters.ContainsKey(player.Id);

            public PromotingInfo() { }
        }

        private void LoadData<T>(ref T data, string filename = null) => data = Core.Interface.Oxide.DataFileSystem.ReadObject<T>(filename ?? Name);
        private void SaveData<T>(T data, string filename = null) => Core.Interface.Oxide.DataFileSystem.WriteObject(filename ?? Name, data);

        #endregion

        #endregion

        #region Messaging

        private void SendChatMessage(IPlayer player, string message)
        {
            player.Reply(message, covalence.FormatText("[" + messageColor + "]" + messagePrefix + "[/#]:"));
        }

        private void SendInfoMessage(IPlayer player, string message)
        {
            player.Reply(message, covalence.FormatText("[+18][" + messageColor + "]" + messagePrefix + "[/#][/+]\n\n"));
        }

        private void SendThanksMessage(IPlayer player, string message)
        {
            player.Reply(message);
        }

        #endregion
    }
}