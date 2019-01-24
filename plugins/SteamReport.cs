using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

using System.Linq;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("SteamReport", "Spicy", "2.0.1", ResourceId = 2047)]
    [Description("Sends in-game reports to admins via Steam.")]
    class SteamReport : CovalencePlugin
    {
        #region Config

        List<string> admins;
        string requestUrl;
        string reportCommand;

        protected override void LoadDefaultConfig()
        {
            Config["Admins"] = new List<string>
            {
                "76561198103592543"
            };
            Config["RequestUrl"] = "http://dev.spicee.xyz/reportingSystem/one";
            Config["ReportCommand"] = "report";
        }

        #endregion

        #region Lang

        void InitLang()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Syntax"] = "Syntax: /report [name|id] [message]",
                ["PlayersNone"] = "No players were found.",
                ["PlayersMultiple"] = "Multiple players were found.",
                ["Fail"] = "Report failed to send.",
                ["Sent"] = "Report sent.",
            }, this);
        }

        string _(string key, string userId) => lang.GetMessage(key, this, userId);

        #endregion

        #region Hooks

        void Init()
        {
            InitLang();

            admins = Config.Get<List<string>>("Admins");
            requestUrl = Config.Get<string>("RequestUrl");
            reportCommand = Config.Get<string>("ReportCommand");

            foreach (var id in admins)
                if (!id.IsSteamId())
                    Puts($"{id} is not a valid SteamID64.");

            AddCovalenceCommand(reportCommand, "SendReport", "steamreport.use");
        }

        #endregion

        #region Command

        void SendReport(IPlayer player, string command, string[] args)
        {
            if (args == null || args.Length <= 1)
            {
                player.Reply(_("Syntax", player.Id));
                return;
            }

            var found = players.FindPlayers(args[0]).Where(p => p.IsConnected);

            if (!found.Any())
            {
                player.Reply(_("PlayersNone", player.Id));
                return;
            }

            if (found.Count() > 1)
            {
                player.Reply(_("PlayersMultiple", player.Id));
                return;
            }

            var target = found.First();
            var message = string.Empty;

            for (var i = 1; i < args.Length; i++)
                message += args[i] + (i == args.Length ? string.Empty : " ");

            var request = string.Format("{0}?adminList={1}&reporterName={2}&reporterId={3}&reporterPos={4}&reporteeName={5}&reporteeId={6}&reporteePos={7}&reportMessage={8}",
                requestUrl, string.Join("|", admins.ToArray()), player.Name, player.Id, player.Position().ToString(), target.Name, target.Id, target.Position().ToString(), message);

            webrequest.EnqueueGet(request, (code, response) =>
            {
                if (code != 200 || response == null)
                {
                    player.Reply(_("Fail", player.Id));
                    return;
                }

                player.Reply(_("Sent", player.Id));
            }, this);
        }

        #endregion
    }
}
