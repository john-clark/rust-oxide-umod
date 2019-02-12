using Oxide.Core;
using System;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using System.Collections;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace Oxide.Plugins
{
    [Info("Clan Tags", "GreenArrow", "0.8", ResourceId = 2450)]
    [Description("Adding support for Clan tags in Better Chat. ")]

    class ClanTags : CovalencePlugin
    {
        [PluginReference]
        private Plugin BetterChat,Clans,HWClans;
		
		object defaultClanCol,before,after;
		Dictionary<string,string> perClanColor;
		
        protected override void LoadDefaultConfig() {
			Config.Clear();
            Config["defaultClanCol"] = "FFA500";
            Config["BeforeTag"] = "[";
            Config["AfterTag"] = "]";
			Config["PerClanColor"] = new Dictionary<string, string> {{"ThisIsClanTag > That is color (dont use #) >", "808000"}, {"example", "00FF00"}};
		    SaveConfig();
		}
	    
	
        private string GetUsersClan(IPlayer player) {
			
    		string clan = (string)Clans?.Call("GetClanOf",player.Object);
			
		if (clan == null)
		    return null;
		
		return clan;
		}
		
	
        private string GetClanOwner(string lol) {

    		JObject claninfo = (JObject)Clans?.Call("GetClan",lol);	
		    
			string clanowner = (string)claninfo["owner"];

    			return clanowner;
		 
		    return null;
		}
		
        private string GetClanTagFormatted(IPlayer player)
        {	

			string clantag = GetUsersClan(player);
            string togetherstring = covalence.FormatText($"[#{defaultClanCol}]{before}{clantag}{after}[/#]");

   		
            if (clantag != null && !string.IsNullOrEmpty(clantag))
			{

	            foreach (KeyValuePair<string,string> pair in perClanColor)
                {
			    	string custcol = pair.Value;
			            if (clantag == pair.Key)
			    		togetherstring = covalence.FormatText($"[#{custcol}]{before}{clantag}{after}[/#]");
			    }	
			    
			    return togetherstring;
			}
			return null;

        }
		
		private void OnPluginLoaded(Plugin plugin)
		{
			if (plugin.Title != "Better Chat")
				return;

    		Interface.CallHook("API_RegisterThirdPartyTitle", this, new Func<IPlayer, string>(GetClanTagFormatted));

		}
		
        [Command("clancol")]
        void ChangeClanCol(IPlayer player, string command, string[] args)
        {
		    
			if (permission.UserHasPermission(player.Id, "clantags.admin") && args.Length == 2)
            {
			
			if (perClanColor.ContainsKey(args[0]))
			perClanColor.Remove(args[0]);
			
			perClanColor.Add(args[0],args[1]);
			Config.Set("PerClanColor", perClanColor);
			SaveConfig();
			player.Reply($"<size=15><color=#31e231>[ClanTags]</color></size> <color=#{args[1]}> {args[0]}</color> was added to clan tags colors.");		    

			
			} else
			if (!permission.UserHasPermission(player.Id, "clantags.admin"))
			player.Reply($"<size=15><color=#31e231>[ClanTags]</color></size> You don't have permission!");
            
            else if (args.Length <= 1)
		    player.Reply($"<size=15><color=#31e231>[ClanTags]</color></size> Not enough infomation given! <color=#c4c103>Example: /clancol test 800000</color>");


        }
	
        [Command("clantag")]
        void ChangeClanTagOwner(IPlayer player, string command, string[] args)
        {
		    string clantag = GetUsersClan(player);
				
			if (permission.UserHasPermission(player.Id, "clantags.default") && args.Length == 1 && (clantag != null) && (GetClanOwner(clantag) == (string)player.Id))
            {
			if (perClanColor.ContainsKey(clantag))
			perClanColor.Remove(clantag);
			
			perClanColor.Add(clantag,args[0].Substring(1,6));
			Config.Set("PerClanColor", perClanColor);
			SaveConfig();
			player.Reply($"<size=15><color=#31e231>[ClanTags]</color></size> <color=#{args[0].Substring(1,6)}> {clantag}</color> your clan tag has been updated.");		    

			} else

			if (!permission.UserHasPermission(player.Id, "clantags.default"))
			player.Reply($"<size=15><color=#31e231>[ClanTags]</color></size> You don't have permission!");
            
            if (args.Length <= 0)
		    player.Reply($"<size=15><color=#31e231>[ClanTags]</color></size> Not enough infomation given! <color=#c4c103>Example: /clantag 800000</color>");
				
			if ((clantag != null) && (GetClanOwner(clantag) != (string)player.Id) )
		    player.Reply($"<size=15><color=#31e231>[ClanTags]</color></size> Only the clan owner is allowed to change the clans tag color.");		    
				
			if (clantag == null)
			player.Reply($"<size=15><color=#31e231>[ClanTags]</color></size> You're not in a clan.");		    

        }

		
        void OnServerInitialized()
		{

    		defaultClanCol = Config["defaultClanCol"];
            before = Config["BeforeTag"];
		    after = Config["AfterTag"];
			perClanColor = JsonConvert.DeserializeObject<Dictionary<string, string>>(JsonConvert.SerializeObject(Config["PerClanColor"]));
            permission.RegisterPermission("clantags.admin", this);
			permission.RegisterPermission("clantags.default", this);
						
    		BetterChat?.CallHook("API_RegisterThirdPartyTitle", this, new Func<IPlayer, string>(GetClanTagFormatted));

		}
		
	}
}