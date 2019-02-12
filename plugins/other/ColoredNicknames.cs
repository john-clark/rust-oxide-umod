using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Oxide.Core.Configuration;
using CodeHatch.Common;
using CodeHatch.Engine.Networking;

namespace Oxide.Plugins
{
    [Info("Nickname Color Changer", "Ruby", "1.1.1")]
    [Description("Makes nicknames coloured using ROK registry system.")]
    public class ColoredNicknames : ReignOfKingsPlugin
    { 
		#region Configs
        T GetConfig<T>(string name, T defaultValue)
        {
            if (Config[name] == null) return defaultValue;
            return (T)Convert.ChangeType(Config[name], typeof(T));
        }
	
		private bool AdminOnly => GetConfig("AdminOnly", false);
		
		protected override void LoadDefaultConfig()
        {
            Config["AdminOnly"] = AdminOnly;
            SaveConfig();
        }
		
		private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
				["Success"] = "Color of your nickname has been changed successfully!",
				["Removed"] = "Color of your nickname has been removed.",
                ["Example"] = "Usage: /color ff0000 /color green",
                ["Invalid"] = "Invalid arguments. Example: /color blue",
                ["Permission"] = "You don't have permission to use this command.",
                ["TargetSuccess"] = "Command to change nickname of target player has been sent!",
                ["TargetExample"] = "Usage: /setcolor NickName ff0000",
                ["TargetInvalid"] = "Invalid arguments. Example: /setcolor NickName ff0000"
            }, this);
        }
		#endregion
		
		[ChatCommand("color")]
        private void ColorChange(Player player, string cmd, string[] args)
		{			
			#region Player Data
			string playerId = player.Id.ToString();
			string nickname = player.Name.ToString();
			string new_nickname = nickname.Remove(nickname.Length - 3);
            #endregion

            #region Player Checks
            if (AdminOnly && !player.HasPermission("admin"))
            {
                PrintToChat(player, lang.GetMessage("Permission", this, playerId));
                return;
            }
            if (args == null || args.Length != 1)
            {
                PrintToChat(player, lang.GetMessage("Example", this, playerId));
                return;
            }

            Regex reg = new Regex("[^a-z0-9 ]");
            args[0] = reg.Replace(args[0], "");

            #region Player Text to HEX
            if (args[0].Contains("white") || args[0].Contains("reset"))
            {
				new_nickname = new_nickname.Remove(0, 8);
                covalence.Server.Command($"registry rename '{nickname}' '{new_nickname}'");
				PrintToChat(player, lang.GetMessage("Removed", this, playerId));
				return;
            }
            else if (args[0].Contains("red"))
            {
                args[0] = "ff0000";
            }
            else if (args[0].Contains("pink"))
            {
                args[0] = "ff69b4";
            }
            else if (args[0].Contains("green"))
            {
                args[0] = "00cc00";
            }
            else if (args[0].Contains("blue"))
            {
                args[0] = "0000ff";
            }
            else if (args[0].Contains("orange"))
            {
                args[0] = "ffa500";
            }
            else if (args[0].Contains("yellow"))
            {
                args[0] = "ffff00";
            }
            else if (args[0].Contains("gray"))
            {
                args[0] = "808080";
            }
            else if (args[0].Contains("silver"))
            {
                args[0] = "c0c0c0";
            }
            else if (args[0].Contains("black"))
            {
                args[0] = "000000";
            }
            else if (args[0].Contains("purple"))
            {
                args[0] = "800080";
            }
            else if (args[0].Contains("maroon"))
            {
                args[0] = "800000";
            }
            else if (args[0].Contains("olive"))
            {
                args[0] = "808000";
            }
            else if (args[0].Contains("lime"))
            {
                args[0] = "00ff00";
            }
            else if (args[0].Contains("teal"))
            {
                args[0] = "008080";
            }
            else if (args[0].Contains("aqua"))
            {
                args[0] = "00ffff";
            }
            else if (args[0].Contains("navy"))
            {
                args[0] = "000080";
            }
            else if (args[0].Contains("fuchsia"))
            {
                args[0] = "ff00ff";
            }
            #endregion

            if (args[0].Length != 6)
            {
                PrintToChat(player, lang.GetMessage("Invalid", this, playerId));
                return;
            }
            #endregion

            #region Player Main
            if (nickname.Contains("[") && nickname.Contains("]"))
            {
				new_nickname = new_nickname.Remove(0, 8);
                covalence.Server.Command($"registry rename '{nickname}' '[{args[0]}]{new_nickname}[-]'");
                PrintToChat(player, lang.GetMessage("Success", this, playerId));
				return;
            }
            else
            {
                covalence.Server.Command($"registry rename '{nickname}' '[{args[0]}]{nickname}[-]'");
                PrintToChat(player, lang.GetMessage("Success", this, playerId));
				return;
            }
            #endregion
        }

        [ChatCommand("setcolor")]
        private void TargetColorChange(Player player, string cmd, string[] args)
		{
            #region Admin Data/Checks
            string playerId = player.Id.ToString();
            if (args == null || args.Length != 2)
            {
                PrintToChat(player, lang.GetMessage("TargetExample", this, playerId));
                return;
            }
            if (args[1].Length != 6 && !args[1].Contains("reset"))
            {
                PrintToChat(player, lang.GetMessage("TargetInvalid", this, playerId));
                return;
            }
            if (player.HasPermission("admin"))
            {
                #region Admin Main
                if (args[0].Contains("[") && args[0].Contains("]"))
                {
                    string target = args[0].Remove(args[0].Length - 3);
                    target = target.Remove(0, 8);
                    if (args[1].Contains("reset"))
                    {
                        covalence.Server.Command($"registry rename '{args[0]}' '{target}'");
                    }
                    else
                    {
                        covalence.Server.Command($"registry rename '{args[0]}' '[{args[1]}]{target}[-]'");
                    }
                    PrintToChat(player, lang.GetMessage("TargetSuccess", this, playerId));
                    return;
                }
                else
                {
                    covalence.Server.Command($"registry rename '{args[0]}' '[{args[1]}]{args[0]}[-]'");
                    PrintToChat(player, lang.GetMessage("TargetSuccess", this, playerId));
                    return;
                }
                #endregion
            }
            else
            {
                PrintToChat(player, lang.GetMessage("Permission", this, playerId));
                return;
            }
            #endregion
        }
    }
}