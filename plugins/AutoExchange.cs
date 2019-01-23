using Oxide.Core.Plugins;
using Oxide.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Oxide.Plugins
{
    [Info("AutoExchange", "Alissonerdx and TheRealDoge", "1.0.0")]
    [Description("AutoExchange Economics in RP")]
    public class AutoExchange : RustPlugin
    {
        [PluginReference]
        Plugin Economics;

        [PluginReference]
        Plugin ServerRewards;

        private bool economicsOk;

        private bool serverRewardsOk;

        private Timer timeRun = null;

        private Dictionary<string, string> messages = new Dictionary<string, string>()
        {
            { "MSG_1", "AutoExchange converted {0} Economics into {1} RP" },
            { "MSG_2", "Incorrect Parameters, ex: ae on 250 or ae off" },
            { "MSG_3", "Incorrect Parameter: rate! rate must be an integer value greater than 0" },
            { "MSG_4", "AutoExchange Running" },
            { "MSG_5", "AutoExchange Stoped" },
            { "MSG_6", "You are not allowed to use AutoExchange" }
        };


        void Loaded()
        {
            permission.RegisterPermission("autoexchange.run", this);

            if (Economics == null)
            {
                PrintWarning("Plugin 'Economics' was not found!");
                economicsOk = false;
            }
            else
            {
                economicsOk = true;
            }

            if (ServerRewards == null)
            {
                PrintWarning("Plugin 'ServerRewards' was not found!");
                serverRewardsOk = false;
            }
            else
            {
                serverRewardsOk = true;
            }

            lang.RegisterMessages(messages, this);
        }
        bool HasPermission(BasePlayer player, string perm) => permission.UserHasPermission(player.UserIDString, perm);

        [ChatCommand("ae")]
        void aeCommand(BasePlayer player, string command, string[] args)
        {
            if (this.HasPermission(player, "autoexchange.run"))
            {
                if (args.Count() >= 1)
                {
                    var param = args[0].ToLower();

                    int rateExchange = 0;

                    if (param.Equals("on") && args.Count() == 2)
                    {
                        int.TryParse(args[1].ToLower(), out rateExchange);

                        if (rateExchange != 0)
                        {
                            rust.SendChatMessage(player, string.Format(lang.GetMessage("MSG_4", this, player.UserIDString)));
                            timeRun = timer.Repeat(600f, 0, () =>
                            {
                                var playersOnline = BasePlayer.activePlayerList as List<BasePlayer>;
                                foreach (var playerOn in playersOnline)
                                {
                                    var amount = (double)Economics?.Call("GetPlayerMoney", playerOn.userID);
                                    var rpCount = (int)amount / rateExchange;
                                    var economicsDiscount = rpCount * rateExchange;
                                    amount -= economicsDiscount;
                                    if (rpCount != 0 && economicsDiscount != 0)
                                    {
                                        rust.SendChatMessage(playerOn, string.Format(lang.GetMessage("MSG_1", this, playerOn.UserIDString), economicsDiscount, rpCount));
                                        Economics?.Call("Set", playerOn.userID, amount);
                                        ServerRewards?.Call("AddPoints", playerOn.userID, rpCount);
                                    }
                                }
                            });
                        }
                        else
                        {
                            Puts(lang.GetMessage("MSG_3", this));
                        }
                    }
                    else
                        if (param.Equals("off"))
                    {
                        timeRun.Destroy();
                        rust.SendChatMessage(player, string.Format(lang.GetMessage("MSG_5", this, player.UserIDString)));
                    }
                    else
                    {
                        Puts(lang.GetMessage("MSG_2", this));
                    }

                }
                else
                {
                    Puts(lang.GetMessage("MSG_2", this));
                }
            }
            else
            {
                rust.SendChatMessage(player, string.Format(lang.GetMessage("MSG_6", this, player.UserIDString)));
            }
        }
    }
}
