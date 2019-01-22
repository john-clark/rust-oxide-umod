#region using-directives
using System;
using System.Collections.Generic;
using CodeHatch.Engine.Networking;
using CodeHatch.Common;
using Oxide.Core;
using CodeHatch.UserInterface.Dialogues;
using CodeHatch.Networking.Events.Players;
using CodeHatch.Engine.Modules.SocialSystem;
using CodeHatch.Thrones.SocialSystem;
using UnityEngine;

#endregion
#region Header
namespace Oxide.Plugins
{
    [Info("MessageBird", "juk3b0x", "1.0.0")]
    public class MessageBird : ReignOfKingsPlugin
    {
        #endregion
        #region LanguageAPI
        private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "BirdNotBack", "Your Bird is still on its way! you can send it again, when it is back!" },
                { "SendOnlyFromBase", "You can only send a MessageBird from your own base!" },
                { "Flying", "Your Bird is carrying your message to [FF0000]{0}[FFFFFF], it will be back in about [FFFF00]{1}[FFFFFF] Seconds. " },
                { "ToSendToTitle", "Send a Bird to [FFFF00]{0}[FFFFFF]" },
                { "ReceivedFromTitle", "Received a Bird from [FFFF00]{0}[FFFFFF]" },
                { "Cancelled", "The letter to [FFFF00]{0}[FFFFFF] was [FF0000]NOT[FFFFFF] sent!" },
                { "InvalidPlayer", "The Player [FF0000]{0}[FFFFFF] does not exist, or is not online (maybe Check the spelling?!)." },
                { "EmptyLetter", "If you want to send a letter, you should write something in it!!" },
                { "Dont", "Don't Send" },
                { "BoxText", "Type in the text you want to write in a letter and click OK" },
                { "Returned", "Your Message Bird has returned from [FFFF00]{0}[FFFFFF], you may send it again now!" },
                { "Prefix", "[0000FF]Dear{0}, \n\n[FFFFFF]" },
                { "Suffix", "\n\n[0000FF]Sincerely, \n {0}[FFFFFF]" },
                { "CheckLetter", "[FFFF00]This is how your Letter will look Like:[FFFFFF]\n\n {0} {1} {2} \n\n [FFFF00]Do you want to send this message?[FFFFFF]" },
                { "MessageSent", "Your bird is on its way to [FF0000]{0}[FFFFFF] with your message!" },
                { "Send", "Send" }		
			}, this);
        }
        string GetMessage(string key, string userId = null) => lang.GetMessage(key, this, userId);
        #endregion
        float SpeedMultiplier;

        void Init() => LoadDefaultConfig();

        protected override void LoadDefaultConfig()
        {
            Config["SpeedMultiplier"] = SpeedMultiplier = GetConfig("SpeedMultiplier", 0.25f);
            SaveConfig();
        }

        T GetConfig<T>(string name, T defaultValue) => Config[name] == null ? defaultValue : (T)System.Convert.ChangeType(Config[name], typeof(T));
        void Loaded()
        {

            LoadDefaultMessages();
        }
        #region Bird
        [ChatCommand("bird")]
        private void BirdFlyOrDie(Player player, string cmd, string[] inputarray)
        {
            
            string input = string.Concat(inputarray);
            var targetPlayer = Server.GetPlayerByName(input.ToString());
            if (BirdJustSent)
            {
                PrintToChat(player, string.Format(GetMessage("BirdNotBack", player.Id.ToString())));
                return;
            }
            if (!AtOwnBase(player))
            {
                PrintToChat(player, string.Format(GetMessage("SendOnlyFromBase", player.Id.ToString())));
                return;
            }
            if (input == null || input.ToString() == "" || targetPlayer == null)
            {
                PrintToChat(player, string.Format(GetMessage("InvalidPlayer", player.Id.ToString()), input.ToString()));
                return;
            }
            string title = string.Format(GetMessage("ToSendToTitle", player.Id.ToString()), targetPlayer.Name);
            string message = string.Format(GetMessage("BoxText", player.Id.ToString()));
            player.ShowInputPopup(title, message, "", "OK", "Cancel", (options, dialogue, data) => CheckLetter(player, targetPlayer, options, dialogue, data));

        }
        private void CheckLetter (Player player, Player targetPlayer, Options options, Dialogue dialogue, object data)
        {
            
            if (options == Options.Cancel)
            {
                PrintToChat(player, string.Format(GetMessage("Cancelled", player.Id.ToString()), targetPlayer.Name));
                return;
            }
            if (dialogue.ValueMessage == null || dialogue.ValueMessage=="")
            {
                PrintToChat(player, string.Format(GetMessage("EmptyLetter", player.Id.ToString()), targetPlayer.Name));
                return;
            }
            string message = dialogue.ValueMessage;
            string messagePrefix = string.Format(GetMessage("Prefix", player.Id.ToString()),targetPlayer.Name);
            string messageSuffix = string.Format(GetMessage("Suffix", player.Id.ToString()), player.Name);
            player.ShowConfirmPopup(string.Format(GetMessage("TextBoxTitle", player.Id.ToString()), targetPlayer.Name), string.Format(GetMessage("CheckLetter", player.Id.ToString()), messagePrefix, message, messageSuffix) , string.Format(GetMessage("Send", player.Id.ToString())), string.Format(GetMessage("Dont", player.Id.ToString())), (options1, dialogue1, data1) => SendBirdOrCancel(player, targetPlayer, options1, dialogue1, data1, messagePrefix, message, messageSuffix));
        }
        private void SendBirdOrCancel(Player player, Player targetPlayer, Options options1, Dialogue dialogue1, object data1,string messagePrefix, string message, string messageSuffix)
        {
            float BirdArriveTime = TimeFromCrestToTarget(player, targetPlayer);
            float BirdReturnTime = BirdArriveTime;
            if (options1 == Options.No)
            {
                PrintToChat(player, string.Format(GetMessage("Cancelled", player.Id.ToString()), targetPlayer.Name));
                return;
            }
            BirdJustSent = true;
            PrintToChat(player, string.Format(GetMessage("MessageSent", player.Id.ToString()), targetPlayer.Name));
            string title = string.Format(GetMessage("ReceivedFromTitle", targetPlayer.Id.ToString()), player.Name);
            timer.Repeat(BirdArriveTime, 1, () =>
            {
                targetPlayer.ShowPopup(title, messagePrefix + message + messageSuffix, "Fly", (options2, dialogue2,data2) => SendBirdBack(player, targetPlayer, options2, dialogue2, data2, BirdReturnTime));
            });
        }
        private void SendBirdBack (Player player, Player targetPlayer, Options options2, Dialogue dialogue2, object data2, float BirdReturnTime)
        {
            if (options2 == Options.OK)
            {
                timer.Repeat(BirdReturnTime, 1, () =>
                {
                    BirdJustSent = false;
                    PrintToChat(player, string.Format(GetMessage("Returned", player.Id.ToString()), targetPlayer.Name));
                });
            }
            return;
        }
        #endregion
        #region Checks, Values and bools
        private bool AtOwnBase (Player player)
        {
            var PlayerPosition = player.Entity.Position;
            var crestScheme = SocialAPI.Get<CrestScheme>();
            if (crestScheme.GetCrestAt(PlayerPosition) == null)
            {
                return false;
            }
            var crest = crestScheme.GetCrestAt(PlayerPosition);
            var playerGuild = PlayerExtensions.GetGuild(player).Name;
            var crestGuild = crest.GuildName;
            if (playerGuild == crestGuild)
            {
                return true;
            }
            return false;
        }
        private bool BirdJustSent = false;
        private float TimeFromCrestToTarget (Player player, Player targetPlayer)
        {
            LoadDefaultConfig();
            var crestScheme = SocialAPI.Get<CrestScheme>();
            var crest = crestScheme.GetCrest(player.Id);
            Vector3 CrestPosition = crest.Position;
            Puts(CrestPosition.ToString());
            Vector3 targetPlayerPosition = targetPlayer.Entity.Position;
            Puts(targetPlayerPosition.ToString());
            float distance = (Vector3.Distance(CrestPosition, targetPlayerPosition)*SpeedMultiplier);
            Puts((Vector3.Distance(CrestPosition, targetPlayerPosition).ToString()));
            Puts(SpeedMultiplier.ToString());

            Puts(distance.ToString());
            return distance;
            
        }
        #endregion
    }
}