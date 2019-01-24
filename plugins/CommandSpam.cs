using Rust;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("CommandSpam", "WeaselSauce", "1.0.0")]
    [Description("Rate limit player command attempts to combat malicious attempts to lag servers.")]
    class CommandSpam : RustPlugin
    {
        
        // configure command tolerances (threshold) within an interval of time (cooldown)
        // example:  no more than 15 commands issued within any 8 second period
	    public float cooldown = 8.0f; 
        public int threshold = 15;

        public Dictionary<string, DateTime> cooldowns = new Dictionary<string, DateTime>();
        public Dictionary<string, int> thresholds = new Dictionary<string, int>();


        private object OnUserCommand(IPlayer player, string command, string[] args)
        {
            if (IsFlooding(player))
            {
                player.Kick(string.Concat("Kicked: ", "Command spamming detected by server."));
			Puts($"COMMAND SPAM DETECTION : {player.Name} auto-kicked");
                return true;
            }
	    	return null;
        }

        private double GetNextCommandTime(IPlayer player)
        {
            if (cooldowns[player.Id].AddSeconds(cooldown) > DateTime.Now)
                return Math.Ceiling((cooldowns[player.Id].AddSeconds(cooldown) - DateTime.Now).TotalSeconds);
            return 0;
        }

        private bool IsFlooding(IPlayer player, string action = "G") // hacky but needed
        {
            if (cooldowns.ContainsKey(player.Id))
            {
                var hasCooldown = GetNextCommandTime(player) > 0;
                if (hasCooldown)
                {
                    if (thresholds.ContainsKey(player.Id))
                    {
                        if (thresholds[player.Id] > threshold)
                        {
                            return true;
                        }

                        if (action != null)
                        {
                            thresholds[player.Id] = ++thresholds[player.Id];
                            cooldowns.Remove(player.Id);
                            cooldowns.Add(player.Id, DateTime.Now);
                        }
                        return false;
                    }

                    if (!thresholds.ContainsKey(player.Id))
                        thresholds.Add(player.Id, 1);
                    return false;
                }

                if (!hasCooldown && cooldowns.ContainsKey(player.Id))
                {
                    cooldowns.Remove(player.Id);
                    if (thresholds.ContainsKey(player.Id))
                        thresholds.Remove(player.Id);
                }
            }

            if (!cooldowns.ContainsKey(player.Id))
            {
                cooldowns.Add(player.Id, DateTime.Now);
            }
            return false;
        }
    }
}
