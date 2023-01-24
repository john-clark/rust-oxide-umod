using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("Discord F7 Report", "Skipcast", "1.0.0")]
	[Description("Sends a message through Discord when a player sends a report using the F7 menu.")]
	public class DiscordF7Report : RustPlugin
	{
		[PluginReference] Plugin Discord;

		void Loaded()
		{
			if (!Discord)
			{
				PrintError("Discord plugin not detected. Make sure it's installed.");
				return;
			}
		}

		void OnServerCommand(ConsoleSystem.Arg arg)
		{
			if (!Discord || arg.Connection == null || !arg.HasArgs(2))
				return;
			
			if (arg.cmd.FullName == "server.cheatreport")
			{
				ulong steamId = arg.GetULong(0);
				string text = arg.GetString(1);
				BasePlayer reporter = arg.Player();
				BasePlayer reportTarget = BasePlayer.Find(steamId.ToString());

				if (reportTarget == null || string.IsNullOrEmpty(text))
				{
					return;
				}

				string message = $"Reporter: {reporter.displayName} [{reporter.UserIDString}]\n Reported: {reportTarget.displayName} [{reportTarget.userID}].\nReason: {text}";

				Discord.Call("SendMessage", message);
			}
		}
	}
}