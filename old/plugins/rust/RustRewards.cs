using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Linq;
using System.Reflection;
using Oxide.Core.Plugins;
using Oxide.Core.CSharp;
using Oxide.Core;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries.Covalence;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
[Info("Rust Rewards", "MSpeedie", "2.2.6")]
[Description("Rewards players for activities using Economic or ServerRewards")]
// Big Thank you to Tarek the original author of this plugin!
// redBDGR, for maintaining the Barrel Points plugin
// Scriptzyy, the original author of the Barrel Points plugin
// Mr. Bubbles, the original author of the Gather Rewards plugin
// CanopySheep and Wulf, for maintaining the Gather Rewards plugin

public class RustRewards : RustPlugin
{
	[PluginReference] Plugin Economics;
	[PluginReference] Plugin ServerRewards;
	[PluginReference] Plugin Clans;
	[PluginReference] Plugin Friends;

    public Oxide.Core.VersionNumber Version { get; set; }
	readonly private CultureInfo CurrencyCulture = CultureInfo.CreateSpecificCulture("en-US");  // change this to change the currency symbol
	readonly DynamicConfigFile dataFile = Interface.Oxide.DataFileSystem.GetFile("RustRewards");
	private Timer Cleanertimer; // used to clean up tracking of folks damaging entities

	private struct TrackPlayer
	{
		public IPlayer iplayer;
		public float   time;  // using TOD_Sky.Instance.Cycle.Hour
	}
	readonly private float shotclock = 0.9f;  // how long after an action is done is it ignored in "hours" (to function that process this has some interesting decimal math)

	private Dictionary<uint, TrackPlayer> EntityCollectionCache = new Dictionary<uint, TrackPlayer>(); // float has TOD_Sky.Instance.Cycle.Hour")
	private Dictionary<string, string> playerPrefs = new Dictionary<string, string>();
	private Dictionary<string, double> groupmultiplier = new Dictionary<string, double>();

	const string permVIP = "rustrewards.vip";

	// to indicate I need to update the json file
	bool _didConfigChange;

	private Oxide.Core.VersionNumber ConfigVersion;

	private bool serverrewardsloaded = false;
	private bool economicsloaded = false;
	private bool clansloaded = false;
	private bool friendsloaded = false;
	private bool happyhouractive = false;
	private bool happyhourcross24 = false;
	private bool NPCReward_Enabled = true;
	private bool VIPMultiplier_Enabled = false;
	private bool ActivityReward_Enabled = false;
	private bool OpenReward_Enabled = false;
	private bool KillReward_Enabled = false;
	private bool HarvestReward_Enabled = false;
	private bool WelcomeMoney_Enabled = false;
	private bool WeaponMultiplier_Enabled = false;
	private bool DistanceMultiplier_Enabled = false;
	private bool UseEconomicsPlugin = false;
	private bool UseServerRewardsPlugin = true;
	private bool UseFriendsPlugin = true;
	private bool UseClansPlugin = true;
	private bool TakeMoneyfromVictim = false;
	private bool PrintInConsole = false;
	private bool DoLogging = true;
	private bool DoAdvancedVIP = false;
	private bool ShowcurrencySymbol = true;
	private bool HappyHour_Enabled = true;

	private int ActivityReward_Minutes = 15;
	private int HappyHour_BeginHour = 18;
	private int HappyHour_EndHour = 21;

	private Timer timecheck;

	private double mult_rocket = 1.0;
	private double mult_flamethrower = 1.0;
	private double mult_hammer_salvaged = 1.0;
	private double mult_icepick_salvaged = 1.0;
	private double mult_axe_salvaged = 1.0;
	private double mult_stone_pickaxe = 1.0;
	private double mult_pickaxe = 1.0;
	private double mult_stonehatchet = 1.0;
	private double mult_hatchet = 1.0;
	private double mult_butcherknife = 1.0;
	private double mult_pitchfork = 1.0;
	private double mult_sickle = 1.0;
	private double mult_torch = 1.0;
	private double mult_flashlight = 1.0;
	private double mult_chainsaw = 1.0;
	private double mult_jackhammer = 1.0;
	private double mult_assaultrifle = 1.0;
	private double mult_beancangrenade = 1.0;
	private double mult_boltactionrifle = 1.0;
	private double mult_boneclub = 1.0;
	private double mult_boneknife = 1.0;
	private double mult_candycaneclub = 1.0;
	private double mult_compoundbow = 1.0;
	private double mult_crossbow = 1.0;
	private double mult_customsmg = 1.0;
	private double mult_doublebarrelshotgun = 1.0;
	private double mult_eokapistol = 1.0;
	private double mult_f1grenade = 1.0;
	private double mult_handmadefishingrod = 1.0;
	private double mult_huntingbow = 1.0;
	private double mult_l96rifle = 1.0;
	private double mult_lr300 = 1.0;
	private double mult_longsword = 1.0;
	private double mult_m249 = 1.0;
	private double mult_m39 = 1.0;
	private double mult_m92pistol = 1.0;
	private double mult_mp5a4 = 1.0;
	private double mult_mace = 1.0;
	private double mult_machete = 1.0;
	private double mult_nailgun = 1.0;
	private double mult_pumpshotgun = 1.0;
	private double mult_pythonrevolver = 1.0;
	private double mult_revolver = 1.0;
	private double mult_rocketlauncher = 1.0;
	private double mult_salvagedcleaver = 1.0;
	private double mult_salvagedsword = 1.0;
	private double mult_satchelcharge = 1.0;
	private double mult_semiautomaticpistol = 1.0;
	private double mult_semiautomaticrifle = 1.0;
	private double mult_snowball = 1.0;
	private double mult_spas12shotgun = 1.0;
	private double mult_stonespear = 1.0;
	private double mult_thompson = 1.0;
	private double mult_timedexplosivecharge = 1.0;
	private double mult_waterpipeshotgun = 1.0;
	private double mult_woodenspear = 1.0;
	private double mult_VIPMultiplier = 1.0;
	private double mult_HappyHourMultiplier = 1.0;
	private double mult_distance_100 = 1.0;
	private double mult_distance_200 = 1.0;
	private double mult_distance_300 = 1.0;
	private double mult_distance_400 = 1.0;
	private double mult_distance_50 = 1.0;
	private double rate_sam = 1.0;
	private double rate_trap = 1.0;
	private double rate_autoturret = 1.0;
	private double rate_barrel = 1.0;
	private double rate_bear = 1.0;
	private double rate_boar = 1.0;
	private double rate_bradley = 1.0;
	private double rate_cactus = 1.0;
	private double rate_chicken = 1.0;
	private double rate_chinook = 1.0;
	private double rate_corn = 1.0;
	private double rate_crate = 1.0;
	private double rate_foodbox = 1.0;
	private double rate_giftbox = 1.0;
	private double rate_helicopter = 1.0;
	private double rate_hemp = 1.0;
	private double rate_horse = 1.0;
	private double rate_player = 1.0;
	private double rate_minecart = 1.0;
	private double rate_mushrooms = 1.0;
	private double rate_ore = 1.0;
	private double rate_pumpkin = 1.0;
	private double rate_murderer = 1.0;
	private double rate_scientist = 1.0;
	private double rate_stag = 1.0;
	private double rate_stones = 1.0;
	private double rate_sulfur = 1.0;
	private double rate_supplycrate = 1.0;
	private double rate_wolf = 1.0;
	private double rate_wood = 1.0;
	private double rate_npckill = 1.0;
	private double rate_activityreward = 1.0;
	private double rate_welcomemoney = 1.0;

	private string prestring = "<color=#CCBB00>";
	private string midstring =  "</color><color=#FFFFFF>";
	private string poststring = "</color>";

	private Dictionary<IPlayer, int> Activity_Reward = new Dictionary<IPlayer, int>();

	protected override void LoadDefaultConfig() { }

	object GetConfigValue(string category, string setting, object defaultValue)
	{
		Dictionary<string, object> data = new Dictionary<string, object>();
		object value = defaultValue;
		if (category == null || category == String.Empty)
		{
			Puts("Tell MSpeedie No Category for config");
		}
		if (setting == null || setting == String.Empty)
		{
			Puts("Tell MSpeedie No Setting for config");
		}

		try
		{
			data = Config[category] as Dictionary<string, object>;
		}
		catch
		{
			Puts("Tell MSpeedie Error getting config");
		}

		if (data == null)
		{
			data = new Dictionary<string, object>();
			Config[category] = data;
			_didConfigChange = true;
		}

		try
		{
			if (data.TryGetValue(setting, out value)) return value;

		}
		catch
		{
			value = defaultValue;
		}

		value = defaultValue;
		data[setting] = value;
		_didConfigChange = true;
		return value;
	}

    private void CheckCfg<T>(string Key, ref T var)
    {
        if (Config[Key] is T)
            var = (T)Config[Key];
        else
            Config[Key] = var;
    }

	object SetConfigValue(string category, string setting, object defaultValue)
	{
		var data = Config[category] as Dictionary<string, object>;
		object value;

		if (data == null)
		{
			data = new Dictionary<string, object>();
			Config[category] = data;
			_didConfigChange = true;
		}

		value = defaultValue;
		data[setting] = value;
		_didConfigChange = true;
		return value;
	}

	private void CheckConfig()
	{
		if (VIPMultiplier_Enabled && DoAdvancedVIP)
		{
			Puts("Warning: you are running VIP Multiplier enabled and Do Advanced VIP which can lead to big multipliers!");
		}

		if (DoAdvancedVIP && groupmultiplier == null)
		{
			Puts("Error: You have selected Do Advanced VIP but did not specify and group with rates");
		}

		if (!UseEconomicsPlugin && !UseServerRewardsPlugin)
			PrintWarning("Error: You need to select Economics or ServerReward or this plugin is pointless.");

		if (UseEconomicsPlugin && UseServerRewardsPlugin)
		{
			PrintWarning("Error: You need to select Economics or ServerReward but not both!");
			if (Economics.IsLoaded == true)
			{
				UseServerRewardsPlugin = false;
				Puts("Warning: Switched to Economics as it is loaded");
			}
			else
			{
				UseEconomicsPlugin = false;
				Puts("Warning: Switched to Server Rewards as Economics is not loaded");
			}
		}


		try
		{
			economicsloaded = false;
			if (UseEconomicsPlugin)
			{
				if (Economics != null && Economics.IsLoaded == true)
					economicsloaded = true;
				else
				{
					PrintWarning("Error: Economics plugin was not found! Can't reward players using Economics.");
				}
			}
		}
		catch
		{
			economicsloaded = false;
		}

		try
		{
			serverrewardsloaded = false;
			if (UseServerRewardsPlugin)
			{
				if (ServerRewards != null && ServerRewards.IsLoaded == true)
					serverrewardsloaded = true;
				else
				{
					PrintWarning("Error: ServerRewards plugin was not found! Can't reward players using ServerRewards.");
				}
			}
		}
		catch
		{
			serverrewardsloaded = false;
		}

		try
		{
			friendsloaded = false;
			if (UseFriendsPlugin)
			{
				if (Friends != null && Friends.IsLoaded == true)
					friendsloaded = true;
				else
				{
					PrintWarning("Warning: Friends plugin was not found! Can't check if victim is friend to killer.");
				}
			}
		}
		catch
		{
			friendsloaded = false;
		}

		try
		{
			clansloaded = false;
			if (UseClansPlugin)
			{
				if (Clans != null && Clans.IsLoaded == true)
					clansloaded = true;
				else
				{

					PrintWarning("Warning: Clans plugin was not found! Can't check if victim is in the same clan of killer.");
				}
			}
		}
		catch
		{
			clansloaded = false;
		}
	}

	protected override void LoadDefaultMessages()
	{
		lang.RegisterMessages(new Dictionary<string, string>
		{
			["activity"] = "You received {0} Reward for activity.",
			["autoturret"] = "You received {0} for destroying an autoturret",
			["barrel"] = "You received {0} Reward for looting a Barrel",
			["barrel"] = "You received {0} for destroying a barrel.",
			["bear"] = "You received {0} Reward for killing a bear",
			["boar"] = "You received {0} Reward for killing a boar",
			["bradley"] = "You received {0} Reward for killing a Bradley APC",
			["cactus"] = "You received {0} Reward for collecting Cactus",
			["chicken"] = "You received {0} Reward for killing a chicken",
			["chinook"] = "You received {0} Reward for killing a chinook CH47",
			["collect"] = "You received {0} Reward for collecting {1}.",
			["corn"] = "You received {0} Reward for collecting Corn.",
			["crate"] = "You received {0} Reward for looting a Crate",
			["foodbox"] = "You received {0} for looting a food box.",
			["giftbox"] = "You received {0} for looting a gift box.",
			["helicopter"] = "You received {0} Reward for killing a helicopter",
			["hemp"] = "You received {0} Reward for collecting Hemp",
			["horse"] = "You received {0} Reward for killing a horse",
			["kill"] = "You received {0} Reward for killing {1}.",
			["minecart"] = "You received {0} Reward for looting a Mine Cart",
			["minecart"] = "You received {0} for looting a minecart.",
			["murderer"] = "You received {0} Reward for killing a zombie/murderer",
			["mushrooms"] = "You received {0} Reward for collecting Mushrooms",
			["npc"] = "You received {0} Reward for killing a NPC",
			["ore"] = "You received {0} Reward for collecting Ore",
			["player"] = "You received {0} Reward for killing a player",
			["pumpkin"] = "You received {0} Reward for collecting Pumpkin",
			["sam"] = "You received {0} for destroying a SAM",
			["scientist"] = "You received {0} Reward for killing a scientist",
			["stag"] = "You received {0} Reward for killing a stag",
			["stones"] = "You received {0} Reward for collecting Stones",
			["sulfur"] = "You received {0} Reward for collecting Sulfur",
			["supplycrate"] = "You received {0} Reward for looting a supply crate",
			["trap"] = "You received {0} for destroying a trap",
			["welcomemoney"] = "Welcome to server! You received {0} as a welcome reward.",
			["wolf"] = "You received {0} Reward for killing a wolf",
			["wood"] = "You received {0} Reward for collecting Wood",
			["happyhourend"] = "Happy Hour(s) ended.",
			["happyhourstart"] = "Happy Hour(s) started.",
			["Prefix"] = "Rust Rewards",
			["rrm change"] = "Rewards Messages for {0} is now {1}",
			["rrm syntax"] = "/rrm syntax:  /rrm type state  Type is one of h,o or k (Havest, Open or Kill).  State is on or off.  for example /rrm h off",
			["rrm type"] = "type must be one of: h o or k only. (Havest, Open or Kill",
			["rrm state"] = "state need to be one of: on or off.",
			["VictimNoMoney"] = "{0} doesn't have enough money.",
			["rewardset"] = "Reward was set",
			["setrewards"] = "Variables you can set:"
		}, this);
	}

	void LoadConfigValues()
	{
		//ConfigVersion = GetConfigValue("version", "version", VersionNumber);
		NPCReward_Enabled = Convert.ToBoolean(GetConfigValue("settings", "NPCReward_Enabled", "true"));
		VIPMultiplier_Enabled = Convert.ToBoolean(GetConfigValue("settings", "VIPMultiplier_Enabled", "false"));
		DoAdvancedVIP = Convert.ToBoolean(GetConfigValue("settings", "Do_Advanced_VIP", "false"));
		ActivityReward_Enabled = Convert.ToBoolean(GetConfigValue("settings", "ActivityReward_Enabled", "true"));
		WelcomeMoney_Enabled = Convert.ToBoolean(GetConfigValue("settings", "WelcomeMoney_Enabled", "true"));
		OpenReward_Enabled = Convert.ToBoolean(GetConfigValue("settings", "OpenReward_Enabled", "true"));
		KillReward_Enabled = Convert.ToBoolean(GetConfigValue("settings", "KillReward_Enabled", "true"));
		HarvestReward_Enabled = Convert.ToBoolean(GetConfigValue("settings", "HarvestReward_Enabled", "true"));
		WeaponMultiplier_Enabled = Convert.ToBoolean(GetConfigValue("settings", "WeaponMultiplier_Enabled", "true"));
		DistanceMultiplier_Enabled = Convert.ToBoolean(GetConfigValue("settings", "DistanceMultiplier_Enabled", "true"));
		UseEconomicsPlugin = Convert.ToBoolean(GetConfigValue("settings", "UseEconomicsPlugin", "false"));
		UseServerRewardsPlugin = Convert.ToBoolean(GetConfigValue("settings", "UseServerRewardsPlugin", "false"));
		UseFriendsPlugin = Convert.ToBoolean(GetConfigValue("settings", "UseFriendsPlugin", "true"));
		UseClansPlugin = Convert.ToBoolean(GetConfigValue("settings", "UseClansPlugin", "true"));
		TakeMoneyfromVictim = Convert.ToBoolean(GetConfigValue("settings", "TakeMoneyfromVictim", "false"));
		PrintInConsole = Convert.ToBoolean(GetConfigValue("settings", "PrintInConsole", "false"));
		DoLogging = Convert.ToBoolean(GetConfigValue("settings", "DoLogging", "true"));
		ShowcurrencySymbol = Convert.ToBoolean(GetConfigValue("settings", "ShowcurrencySymbol", "true"));
		HappyHour_Enabled = Convert.ToBoolean(GetConfigValue("settings", "HappyHour_Enabled", "true"));

		ActivityReward_Minutes = Convert.ToInt32(GetConfigValue("settings", "ActivityReward_Minutes", 15));
		HappyHour_BeginHour = Convert.ToInt32(GetConfigValue("settings", "HappyHour_BeginHour", 17));
		HappyHour_EndHour = Convert.ToInt32(GetConfigValue("settings", "HappyHour_EndHour", 21));

		prestring = Convert.ToString(GetConfigValue("settings", "Pre String", "<color=#CCBB00>"));
		midstring = Convert.ToString(GetConfigValue("settings", "Mid String", "</color><color=#FFFFFF>"));
		poststring = Convert.ToString(GetConfigValue("settings", "Post String", "</color>"));

		mult_rocket = Convert.ToDouble(GetConfigValue("multipliers", "rocket", 1));
		mult_flamethrower = Convert.ToDouble(GetConfigValue("multipliers", "flamethrower", 1));
		mult_hammer_salvaged = Convert.ToDouble(GetConfigValue("multipliers", "hammer_salvaged", 1));
		mult_icepick_salvaged = Convert.ToDouble(GetConfigValue("multipliers", "icepick_salvaged", 1));
		mult_axe_salvaged = Convert.ToDouble(GetConfigValue("multipliers", "axe_salvaged", 1));
		mult_stone_pickaxe = Convert.ToDouble(GetConfigValue("multipliers", "stone_pickaxe", 1));
		mult_pickaxe = Convert.ToDouble(GetConfigValue("multipliers", "pickaxe", 1));
		mult_stonehatchet = Convert.ToDouble(GetConfigValue("multipliers", "stonehatchet", 1));
		mult_hatchet = Convert.ToDouble(GetConfigValue("multipliers", "hatchet", 1));
		mult_butcherknife = Convert.ToDouble(GetConfigValue("multipliers", "butcherknife", 1));
		mult_pitchfork = Convert.ToDouble(GetConfigValue("multipliers", "pitchfork", 1));
		mult_sickle = Convert.ToDouble(GetConfigValue("multipliers", "sickle", 1));
		mult_torch = Convert.ToDouble(GetConfigValue("multipliers", "torch", 1));
		mult_flashlight = Convert.ToDouble(GetConfigValue("multipliers", "flashlight", 1));
		mult_chainsaw = Convert.ToDouble(GetConfigValue("multipliers", "chainsaw", 1));
		mult_jackhammer = Convert.ToDouble(GetConfigValue("multipliers", "jackhammer", 1));
		mult_assaultrifle = Convert.ToDouble(GetConfigValue("multipliers", "assaultrifle", 1));
		mult_beancangrenade = Convert.ToDouble(GetConfigValue("multipliers", "beancangrenade", 1));
		mult_boltactionrifle = Convert.ToDouble(GetConfigValue("multipliers", "boltactionrifle", 1));
		mult_boneclub = Convert.ToDouble(GetConfigValue("multipliers", "boneclub", 1.5));
		mult_boneknife = Convert.ToDouble(GetConfigValue("multipliers", "boneknife", 1.5));
		mult_candycaneclub = Convert.ToDouble(GetConfigValue("multipliers", "candycaneclub", 1.5));
		mult_compoundbow = Convert.ToDouble(GetConfigValue("multipliers", "compoundbow", 1.25));
		mult_crossbow = Convert.ToDouble(GetConfigValue("multipliers", "crossbow", 1.25));
		mult_customsmg = Convert.ToDouble(GetConfigValue("multipliers", "customsmg", 1));
		mult_doublebarrelshotgun = Convert.ToDouble(GetConfigValue("multipliers", "doublebarrelshotgun", 1));
		mult_eokapistol = Convert.ToDouble(GetConfigValue("multipliers", "eokapistol", 1.25));
		mult_f1grenade = Convert.ToDouble(GetConfigValue("multipliers", "f1grenade", 1));
		mult_handmadefishingrod = Convert.ToDouble(GetConfigValue("multipliers", "handmadefishingrod", 2));
		mult_huntingbow = Convert.ToDouble(GetConfigValue("multipliers", "huntingbow", 1.5));
		mult_l96rifle = Convert.ToDouble(GetConfigValue("multipliers", "l96rifle", 1));
		mult_lr300 = Convert.ToDouble(GetConfigValue("multipliers", "lr300", 1));
		mult_longsword = Convert.ToDouble(GetConfigValue("multipliers", "longsword", 1.5));
		mult_m249 = Convert.ToDouble(GetConfigValue("multipliers", "m249", 1));
		mult_m39 = Convert.ToDouble(GetConfigValue("multipliers", "m39", 1));
		mult_m92pistol = Convert.ToDouble(GetConfigValue("multipliers", "m92pistol", 1));
		mult_mp5a4 = Convert.ToDouble(GetConfigValue("multipliers", "mp5a4", 1));
		mult_mace = Convert.ToDouble(GetConfigValue("multipliers", "mace", 1.5));
		mult_machete = Convert.ToDouble(GetConfigValue("multipliers", "machete", 1.5));
		mult_nailgun = Convert.ToDouble(GetConfigValue("multipliers", "nailgun", 1.25));
		mult_pumpshotgun = Convert.ToDouble(GetConfigValue("multipliers", "pumpshotgun", 1));
		mult_pythonrevolver = Convert.ToDouble(GetConfigValue("multipliers", "pythonrevolver", 1));
		mult_revolver = Convert.ToDouble(GetConfigValue("multipliers", "revolver", 1));
		mult_rocketlauncher = Convert.ToDouble(GetConfigValue("multipliers", "rocketlauncher", 1));
		mult_salvagedcleaver = Convert.ToDouble(GetConfigValue("multipliers", "salvagedcleaver", 1));
		mult_salvagedsword = Convert.ToDouble(GetConfigValue("multipliers", "salvagedsword", 1.5));
		mult_satchelcharge = Convert.ToDouble(GetConfigValue("multipliers", "satchelcharge", 1));
		mult_semiautomaticpistol = Convert.ToDouble(GetConfigValue("multipliers", "semiautomaticpistol", 1));
		mult_semiautomaticrifle = Convert.ToDouble(GetConfigValue("multipliers", "semiautomaticrifle", 1));
		mult_snowball = Convert.ToDouble(GetConfigValue("multipliers", "snowball", 2));
		mult_spas12shotgun = Convert.ToDouble(GetConfigValue("multipliers", "spas12shotgun", 1));
		mult_stonespear = Convert.ToDouble(GetConfigValue("multipliers", "stonespear", 1.25));
		mult_thompson = Convert.ToDouble(GetConfigValue("multipliers", "thompson", 1));
		mult_timedexplosivecharge = Convert.ToDouble(GetConfigValue("multipliers", "timedexplosivecharge", 1));
		mult_waterpipeshotgun = Convert.ToDouble(GetConfigValue("multipliers", "waterpipeshotgun", 1));
		mult_woodenspear = Convert.ToDouble(GetConfigValue("multipliers", "woodenspear", 1.75));
		mult_VIPMultiplier = Convert.ToDouble(GetConfigValue("multipliers", "vipmultiplier", 2));
		mult_HappyHourMultiplier = Convert.ToDouble(GetConfigValue("multipliers", "happyhourmultiplier", 2));
		mult_distance_50 = Convert.ToDouble(GetConfigValue("multipliers", "distance_50", 1.5));
		mult_distance_100 = Convert.ToDouble(GetConfigValue("multipliers", "distance_100", 2));
		mult_distance_200 = Convert.ToDouble(GetConfigValue("multipliers", "distance_200", 2.5));
		mult_distance_300 = Convert.ToDouble(GetConfigValue("multipliers", "distance_300", 3));
		mult_distance_400 = Convert.ToDouble(GetConfigValue("multipliers", "distance_400", 3.5));
		rate_autoturret = Convert.ToDouble(GetConfigValue("rates", "autoturret", 10));
		rate_barrel = Convert.ToDouble(GetConfigValue("rates", "barrel", 2));
		rate_bear = Convert.ToDouble(GetConfigValue("rates", "bear", 7));
		rate_boar = Convert.ToDouble(GetConfigValue("rates", "boar", 3));
		rate_bradley = Convert.ToDouble(GetConfigValue("rates", "bradley", 50));
		rate_cactus = Convert.ToDouble(GetConfigValue("rates", "cactus", 1));
		rate_chicken = Convert.ToDouble(GetConfigValue("rates", "chicken", 1));
		rate_chinook = Convert.ToDouble(GetConfigValue("rates", "chinook", 50));
		rate_corn = Convert.ToDouble(GetConfigValue("rates", "corn", 1));
		rate_crate = Convert.ToDouble(GetConfigValue("rates", "crate", 2));
		rate_foodbox = Convert.ToDouble(GetConfigValue("rates", "foodbox", 1));
		rate_giftbox = Convert.ToDouble(GetConfigValue("rates", "giftbox", 1));
		rate_helicopter = Convert.ToDouble(GetConfigValue("rates", "helicopter", 75));
		rate_hemp = Convert.ToDouble(GetConfigValue("rates", "hemp", 1));
		rate_horse = Convert.ToDouble(GetConfigValue("rates", "horse", 2));
		rate_player = Convert.ToDouble(GetConfigValue("rates", "player", 10));
		rate_minecart = Convert.ToDouble(GetConfigValue("rates", "minecart", 2));
		rate_mushrooms = Convert.ToDouble(GetConfigValue("rates", "mushrooms", 2));
		rate_ore = Convert.ToDouble(GetConfigValue("rates", "ore", 2));
		rate_pumpkin = Convert.ToDouble(GetConfigValue("rates", "pumpkin", 1));
		rate_murderer = Convert.ToDouble(GetConfigValue("rates", "murderer", 6));
		rate_sam = Convert.ToDouble(GetConfigValue("rates", "sam", 5));
		rate_scientist = Convert.ToDouble(GetConfigValue("rates", "scientist", 8));
		rate_stag = Convert.ToDouble(GetConfigValue("rates", "stag", 2));
		rate_stones = Convert.ToDouble(GetConfigValue("rates", "stones", 1));
		rate_sulfur = Convert.ToDouble(GetConfigValue("rates", "sulfur", 1));
		rate_supplycrate = Convert.ToDouble(GetConfigValue("rates", "supplycrate", 5));
		rate_trap = Convert.ToDouble(GetConfigValue("rates", "trap", 2));
		rate_wolf = Convert.ToDouble(GetConfigValue("rates", "wolf", 8));
		rate_wood = Convert.ToDouble(GetConfigValue("rates", "wood", 1));
		rate_npckill = Convert.ToDouble(GetConfigValue("rates", "npckill", 8));
		rate_activityreward = Convert.ToDouble(GetConfigValue("rates", "activityreward", 15));
		rate_welcomemoney = Convert.ToDouble(GetConfigValue("rates", "welcomemoney", 50));

		//sample group
		Dictionary<string, double> samplegroup = new Dictionary<string, double>();
		samplegroup.Add("vip", 1.5);
		samplegroup.Add("default", 1.0);
		//samplegroup.Add("admin", 2.0);
		//samplegroup.Add("vip", 1.5);
		//samplegroup.Add("mentor", 1.2);
		//samplegroup.Add("esteemed", 1.1);
		//samplegroup.Add("regular", 1.1);
		//samplegroup.Add("default", 1.0);
		
		var json = JsonConvert.SerializeObject(GetConfigValue("groupsettings", "groupmultipliers", samplegroup));
		groupmultiplier = JsonConvert.DeserializeObject<Dictionary<string, double>>(json);
		
		//if (groupmultiplier == null)
		//	Puts("MT GM loaded :(");
		//else Puts("gm count " + groupmultiplier.Count.ToString());

		if (HappyHour_BeginHour > HappyHour_EndHour)
			happyhourcross24 = true;
		else
			happyhourcross24 = false;

		CheckConfig();

		//if (ConfigVersion != Version || _didConfigChange)
		if (_didConfigChange)
		{
			Puts("Configuration file updated.");
			SaveConfig();
		}

		Cleanertimer = timer.Once(600, CleanerTimerProcess);

	}

	void OnServerInitialized()
	{
		permission.RegisterPermission(permVIP, this);
		LoadDefaultMessages();

		playerPrefs = dataFile.ReadObject<Dictionary<string, string>>();

		LoadConfigValues();

		if (ActivityReward_Enabled || (HappyHour_Enabled && (OpenReward_Enabled || KillReward_Enabled || HarvestReward_Enabled)))
		{
			timecheck = timer.Once(60, CheckCurrentTime);
		}
	}

	private void CleanerTimerProcess()
	{
		// look through cache of entities and remove ones that are too old
		foreach(KeyValuePair<uint, TrackPlayer> x in EntityCollectionCache.ToList())
		{
			if (x.Value.time > shotclock * 3)
				try
				{
					EntityCollectionCache.Remove(x.Key);
				}
				catch {} // probably deleted in the code while this was running
		}
		Cleanertimer = timer.Once(600, CleanerTimerProcess);
	}

	static string TrimPunctuation(string value)
	{
		return Regex.Replace(value, "[^A-Za-z0-9]", "");
	}

	private void CheckCurrentTime()
	{
		var gtime = TOD_Sky.Instance.Cycle.Hour;
		IPlayer ip = null;


		if (ActivityReward_Enabled)
		{
			foreach (var p in BasePlayer.activePlayerList.ToArray()) //players.Connected)
			{
				ip = p.IPlayer;
				if (ip != null && (Convert.ToDouble(p.secondsConnected) / 60 > ActivityReward_Minutes))
				{
					try
					{

						if (Activity_Reward.ContainsKey(ip))
						{
							if (Convert.ToDouble(p.secondsConnected - Activity_Reward[ip]) / 60 > ActivityReward_Minutes)
							{
								GiveReward(ip, "activity", null, null, 1);
								try
								{
									Activity_Reward[ip] = p.secondsConnected;
								}
								catch
								{
									Puts("Tell MSpeedie bug with adding to Activity_Reward");
								}
							}
						}
						else
						{
							try
							{
								Activity_Reward.Add(ip, p.secondsConnected);
							}
							catch
							{ }
						}
					}
					catch
					{
						try
						{
							Activity_Reward.Add(ip, p.secondsConnected);
						}
						catch
						{ }
					}
				}
			}
		}
		if (HappyHour_Enabled)
		{
			if (!happyhouractive)
			{
				if ((happyhourcross24 == false && gtime >= HappyHour_BeginHour && gtime < HappyHour_EndHour) ||
					(happyhourcross24 == true && ((gtime >= HappyHour_BeginHour && gtime < 24) || gtime < HappyHour_EndHour))
					)
				{
					happyhouractive = true;
					if (PrintInConsole)
						Puts("Happy hour(s) started.  Ending at " + HappyHour_EndHour);
					BroadcastMessage(Lang("happyhourstart"), Lang("Prefix"));
				}
			}
			else
			{
				if ((happyhourcross24 == false && gtime >= HappyHour_EndHour) ||
					(happyhourcross24 == true && (gtime < HappyHour_BeginHour && gtime >= HappyHour_EndHour))
					)
				{
					happyhouractive = false;
					if (PrintInConsole)
						Puts("Happy Hour(s) ended.  Next Happy Hour(s) starts at " + HappyHour_BeginHour);
					BroadcastMessage(Lang("happyhourend"), Lang("Prefix"));
				}
			}
		}
		timecheck = timer.Once(60, CheckCurrentTime);
	}

	private void OnPlayerInit(BasePlayer player)
	{
		if (!Economics && !ServerRewards) return;
		IPlayer iplayer = player.IPlayer;

		if (iplayer == null || iplayer.Id == null) return;
		if (playerPrefs.ContainsKey(iplayer.Id))return;
		else
		{
			playerPrefs.Add(iplayer.Id, "hko");
			dataFile.WriteObject(playerPrefs);
			if (PrintInConsole)
				Puts("New Player: " + iplayer.Name);
			if (WelcomeMoney_Enabled)
				GiveReward(iplayer, "welcomemoney", null, null, 1);
		}
	}


	private void Unload()
	{
		if (timecheck != null)
			timecheck.Destroy();
	}

	string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

	bool HasPerm(IPlayer p, string pe) => p.HasPermission(pe);


	private void BroadcastMessage(string msg, string prefix = null, object uid = null)
	{
		rust.BroadcastChat(prefix == null ? msg : "<color=#CCBB00>" + prefix + "</color>: ", msg);
	}

	private void MessagePlayer(IPlayer player, string msg, string prefix, string ptype)
	{
		// we check ptype (prefence type) to see if the player wants to see these
		string pref = "hko";

		if (player == null || string.IsNullOrEmpty(msg) || player.Id == null) return;
		else
		{
			try
			{
				playerPrefs.TryGetValue(player.Id, out pref);
				// catch and correct any corrupted preferences
				if (pref.Length > 3)
					pref = "hko";
			}
			catch
			{
				pref = "hko";
			}
		}

		if (!string.IsNullOrEmpty(pref) && (string.IsNullOrEmpty(ptype) || pref.Contains(ptype)))
		{
			if (prefix == null)
				player.Message(msg);
			else
				player.Message(String.Concat(prestring + prefix + ": "+ midstring , msg, poststring ));
		}
	}

	private double GetDistance(float distance)
	{
		if (distance < 50) return 1;
		else if (distance < 100) return mult_distance_50;
		else if (distance < 200) return mult_distance_100;
		else if (distance < 300) return mult_distance_200;
		else if (distance < 400) return mult_distance_300;
		else if (distance >= 400) return mult_distance_400;
		else return 1;
	}

	private double GetWeapon(string weaponshortname)
	{
		string weaponname = null;

		if (string.IsNullOrEmpty(weaponshortname))
			return 1;
		else
			weaponname = weaponshortname.Replace('_', '.');

		if (weaponname.Contains("ak47u")) return mult_assaultrifle;
		else if (weaponname.Contains("axe.salvaged")) return mult_axe_salvaged;
		else if (weaponname.Contains("bolt.rifle")) return mult_boltactionrifle;
		else if (weaponname.Contains("bone.club")) return mult_boneclub;
		else if (weaponname.Contains("bow")) return mult_huntingbow;
		else if (weaponname.Contains("butcherknife")) return mult_butcherknife;
		else if (weaponname.Contains("candy.cane")) return mult_candycaneclub;
		else if (weaponname.Contains("candycaneclub")) return mult_candycaneclub;
		else if (weaponname.Contains("chainsaw")) return mult_chainsaw;
		else if (weaponname.Contains("cleaver")) return mult_salvagedcleaver;
		else if (weaponname.Contains("compound")) return mult_compoundbow;
		else if (weaponname.Contains("crossbow")) return mult_crossbow;
		else if (weaponname.Contains("double.shotgun")) return mult_doublebarrelshotgun;
		else if (weaponname.Contains("eoka")) return mult_eokapistol;
		else if (weaponname.Contains("explosive.satchel")) return mult_satchelcharge;
		else if (weaponname.Contains("explosive.timed")) return mult_timedexplosivecharge;
		else if (weaponname.Contains("fishingrod.handmade")) return mult_handmadefishingrod;
		else if (weaponname.Contains("flamethrower")) return mult_flamethrower;
		else if (weaponname.Contains("flashlight")) return mult_flashlight;
		else if (weaponname.Contains("grenade.beancan")) return mult_beancangrenade;
		else if (weaponname.Contains("grenade.f1")) return mult_f1grenade;
		else if (weaponname.Contains("hammer.salvaged")) return mult_hammer_salvaged;
		else if (weaponname.Contains("hatchet")) return mult_hatchet;
		else if (weaponname.Contains("icepick.salvaged")) return mult_icepick_salvaged;
		else if (weaponname.Contains("jackhammer")) return mult_jackhammer;
		else if (weaponname.Contains("knife.bone")) return mult_boneknife;
		else if (weaponname.Contains("l96")) return mult_l96rifle;
		else if (weaponname.Contains("longsword")) return mult_longsword;
		else if (weaponname.Contains("lr300")) return mult_lr300;
		else if (weaponname.Contains("m249")) return mult_m249;
		else if (weaponname.Contains("m39")) return mult_m39;
		else if (weaponname.Contains("m92")) return mult_m92pistol;
		else if (weaponname.Contains("mace")) return mult_mace;
		else if (weaponname.Contains("machete")) return mult_machete;
		else if (weaponname.Contains("mp5")) return mult_mp5a4;
		else if (weaponname.Contains("nailgun")) return mult_nailgun;
		else if (weaponname.Contains("pickaxe")) return mult_pickaxe;
		else if (weaponname.Contains("pistol.revolver")) return mult_revolver;
		else if (weaponname.Contains("pistol.semiauto")) return mult_semiautomaticpistol;
		else if (weaponname.Contains("pitchfork")) return mult_pitchfork;
		else if (weaponname.Contains("python")) return mult_pythonrevolver;
		else if (weaponname.Contains("rocket")) return mult_rocket;
		else if (weaponname.Contains("rocket.launcher")) return mult_rocketlauncher;
		else if (weaponname.Contains("semi.auto.rifle")) return mult_semiautomaticrifle;
		else if (weaponname.Contains("shotgun.pump")) return mult_pumpshotgun;
		else if (weaponname.Contains("shotgun.waterpipe")) return mult_waterpipeshotgun;
		else if (weaponname.Contains("sickle")) return mult_sickle;
		else if (weaponname.Contains("smg")) return mult_customsmg;
		else if (weaponname.Contains("snowball")) return mult_snowball;
		else if (weaponname.Contains("spas12")) return mult_spas12shotgun;
		else if (weaponname.Contains("spear.stone")) return mult_stonespear;
		else if (weaponname.Contains("spear.wood")) return mult_woodenspear;
		else if (weaponname.Contains("stone.pickaxe")) return mult_stone_pickaxe;
		else if (weaponname.Contains("stonehatchet")) return mult_stonehatchet;
		else if (weaponname.Contains("sword")) return mult_salvagedsword;
		else if (weaponname.Contains("thompson")) return mult_thompson;
		else if (weaponname.Contains("torch")) return mult_torch;
		else
		{
			Puts("Rust Rewards, Unknown weapon: " + weaponname);
			return 1;
		}
	}

	private void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
	{
		if (!HarvestReward_Enabled) return;
		if (string.IsNullOrEmpty(item?.info?.shortname) && !(dispenser.GetComponent<BaseEntity>() is TreeEntity)) return;
		if (entity == null) return;
		if (entity is BaseNpc || entity is NPCPlayerApex || entity is NPCPlayer || entity is NPCMurderer) return;
		if (entity.ToPlayer().IPlayer == null) return;

		if (dispenser?.gameObject?.ToBaseEntity() == null) return;

		uint beId = dispenser.gameObject.ToBaseEntity().net.ID;

		if (beId == null) return;  // no id to do tracking

		if (dispenser.GetComponent<BaseEntity>() is TreeEntity ||
		    item.info.shortname.Contains("stone") || item.info.shortname.Contains("sulfur") ||
		    item.info.shortname.Contains(".ore") || item.info.shortname.Contains("cactus") ||
			item.info.shortname.Contains("driftwood") ||
			item.info.shortname.Contains("douglas") ||
			item.info.shortname.Contains("fir") ||
			item.info.shortname.Contains("birch") ||
			item.info.shortname.Contains("oak") ||
			item.info.shortname.Contains("pine") ||
			item.info.shortname.Contains("juniper") ||
			item.info.shortname.Contains("deadtree") ||
			item.info.shortname.Contains("swamp_tree") ||
			item.info.shortname.Contains("palm") ||
			item.info.shortname.Contains("wood") ||
			item.info.shortname.Contains("log")
			)
		{
			TrackPlayer ECEData;
			ECEData.iplayer = entity.ToPlayer().IPlayer;
			ECEData.time    = TOD_Sky.Instance.Cycle.Hour;

			if (EntityCollectionCache.ContainsKey(beId))
				EntityCollectionCache[beId] = ECEData;
			else
				EntityCollectionCache.Add(beId, ECEData);
		}
	}

	private void OnCollectiblePickup(Item item, BasePlayer player)
	{

		if (!HarvestReward_Enabled) return;
		if (string.IsNullOrEmpty(item?.info?.shortname)) return;
		if (player == null) return;
		if (player is BaseNpc || player is NPCPlayerApex || player is NPCPlayer || player is NPCMurderer) return;
		IPlayer iplayer = player.IPlayer;
		if (iplayer == null) return;

		string shortName = item.info.shortname;
		string resource = null;


		if (shortName.Contains("stone"))
			resource = "stones";
		else if (shortName.Contains("sulfur"))
			resource = "sulfur";
		else if (shortName.Contains(".ore"))
			resource = "ore";
		else if (shortName.Contains("wood") || shortName.Contains("log"))
			resource = "wood";
		else if (shortName.Contains("mushroom"))
			resource = "mushrooms";
		else if (shortName.Contains("seed.corn"))
			resource = "corn";
		else if (shortName.Contains("seed.hemp"))
			resource = "hemp";
		else if (shortName.Contains("seed.pumpkin"))
			resource = "pumpkin";
		//else
		//	Puts("OEC shortName: " + shortName);

		if (resource != null)
		{
	        double totalmultiplier = 1;

			totalmultiplier = (happyhouractive ? mult_HappyHourMultiplier : 1) * ((VIPMultiplier_Enabled && HasPerm(iplayer, permVIP)) ? mult_VIPMultiplier : 1);
			GiveReward(iplayer, resource, "h", null, totalmultiplier);
		}
	}

	void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
	{

		if (!KillReward_Enabled) return;
		if (entity == null || info.Initiator == null) return;
		if (!(info.Initiator is BasePlayer) ||
		      info.Initiator is BaseNpc || info.Initiator is NPCPlayerApex || info.Initiator is NPCPlayer || info.Initiator is NPCMurderer) return;
		if (string.IsNullOrEmpty(entity?.ShortPrefabName)) return;

		// used to track who killed it. last hit wins ;-)
		// Puts(entity.ShortPrefabName);
		if (!(entity is BaseHelicopter || entity is CH47HelicopterAIController || entity is BradleyAPC ||
			  entity.ShortPrefabName.Contains("bradleyapc") ||
			  entity.ShortPrefabName.Contains("ch47") ||
			  entity.ShortPrefabName.Contains("patrolhelicopter")
			  ))
			return;

		BasePlayer bplayer = info?.Initiator?.ToPlayer();
		if (bplayer == null) return;
		IPlayer iplayer = bplayer.IPlayer;
		if (iplayer == null) return;
		TrackPlayer ECEData;

		ECEData.iplayer = iplayer;
		ECEData.time = TOD_Sky.Instance.Cycle.Hour;

		//Puts("OETD BaseCombatEntity: " + entity.ShortPrefabName);
		if (EntityCollectionCache.ContainsKey(entity.net.ID))
			EntityCollectionCache[entity.net.ID] = ECEData;
		else
			EntityCollectionCache.Add(entity.net.ID, ECEData);
	}

	private void OnLootEntity(BasePlayer player, BaseEntity entity)
	{
		if (!OpenReward_Enabled) return;
		if (entity == null || entity.net.ID == null || string.IsNullOrEmpty(entity?.ShortPrefabName)) return;
		if (player == null || player is BaseNpc || player is NPCPlayerApex || player is NPCPlayer || player is NPCMurderer) return;

		IPlayer iplayer = player.IPlayer;
		if (iplayer == null) return;

		if (!(entity.ShortPrefabName.Contains("crate") || entity.ShortPrefabName.Contains("foodbox") ||
			  entity.ShortPrefabName.Contains("trash") || entity.ShortPrefabName.Contains("minecart") ||
			  entity.ShortPrefabName.Contains("supply")
			  ))
			return;

		TrackPlayer ECEData;
		ECEData.iplayer = iplayer;
		ECEData.time    = TOD_Sky.Instance.Cycle.Hour;

		if (EntityCollectionCache.ContainsKey(entity.net.ID))
			EntityCollectionCache[entity.net.ID] = ECEData;
		else
			EntityCollectionCache.Add(entity.net.ID, ECEData);
	}

	private void OnEntityKill(BaseNetworkable entity)
	{
		if (!OpenReward_Enabled && !HarvestReward_Enabled) return;
		if (entity == null || entity.net.ID == null) return;

		TrackPlayer ECEData;
		ECEData.iplayer = null;
		ECEData.time = 0f;

		IPlayer player = null;
		float   ptime = 0f;

		try
		{
			if (EntityCollectionCache.TryGetValue(entity.net.ID, out ECEData))
			{
				player    = ECEData.iplayer;
				ptime     = ECEData.time;
				//EntityCollectionCache.Remove(entity.net.ID);
			}
			else return;
		}
		catch {}

		if (player == null || (TOD_Sky.Instance.Cycle.Hour - ptime) >= shotclock)
		{
			//Puts("Too old: " + (TOD_Sky.Instance.Cycle.Hour - ptime));
			return;  // no data or action too old
		}

		if (string.IsNullOrEmpty(entity?.ShortPrefabName)) return;
		//if (!(entity.ShortPrefabName.Contains("planner") ||
		//	 entity.ShortPrefabName.Contains("junkpile") ||
		//	 entity.ShortPrefabName.Contains("divesite") ||
		//	 entity.ShortPrefabName.Contains("barrel") ||
		//	 entity.ShortPrefabName.Contains("hammer") ||
		//	 entity.ShortPrefabName.Contains("guitar") ||
		//	 entity.ShortPrefabName.Contains("junkpile") ||
		//	 entity.ShortPrefabName.Contains("waterbottle") ||
		//	 entity.ShortPrefabName.Contains("jug") ||
		//	 entity.ShortPrefabName.Contains("salvage") ||
		//	 entity.ShortPrefabName.Contains("generic") ||
		//	 entity.ShortPrefabName.Contains("bow") ||
		//	 entity.ShortPrefabName.Contains("boat") ||
		//	 entity.ShortPrefabName.Contains("rhib") ||
		//	 entity.ShortPrefabName.Contains("fuel") ||
		//	 entity.ShortPrefabName.Contains("foodbox") ||
		//	 entity.ShortPrefabName.Contains("giftbox") ||
		//	 entity.ShortPrefabName.Contains("standingdriver") ||
		//	 entity.ShortPrefabName.Contains("crate") ||
		//	 entity.ShortPrefabName.Contains("supply") ||
		//	 entity.ShortPrefabName.Contains("oilfireball") ||
		//	 entity.ShortPrefabName.Contains("rocket_basic") ||
		//	 entity.ShortPrefabName.Contains("entity") ||
		//	 entity.ShortPrefabName.Contains("weapon")
		//    ))
		//{
		//	Puts("OEK:" + entity.ShortPrefabName);
		//}

		string resource = null;
		string ptype = null;

		if (HarvestReward_Enabled)
		{
			if (entity.ShortPrefabName.Contains("stone"))
			{
				resource = "stones";
				ptype = "h";
			}
			else if (entity.ShortPrefabName.Contains("sulfur"))
			{
				resource = "sulfur";
				ptype = "h";
			}
			else if (entity.ShortPrefabName.Contains("-ore") ||
					entity.ShortPrefabName.Contains("ore_") ||
					entity.ShortPrefabName.Contains(".ore"))
			{
				resource = "ore";
				ptype = "h";
			}
			else if (entity.ShortPrefabName.Contains("cactus"))
			{
				resource = "cactus";
				ptype = "h";
			}
			else if (entity.ShortPrefabName.Contains("driftwood") ||
					entity.ShortPrefabName.Contains("douglas_fir") ||
					entity.ShortPrefabName.Contains("beech") ||
					entity.ShortPrefabName.Contains("birch") ||
					entity.ShortPrefabName.Contains("oak") ||
					entity.ShortPrefabName.Contains("pine") ||
					entity.ShortPrefabName.Contains("juniper") ||
					entity.ShortPrefabName.Contains("deadtree") ||
					entity.ShortPrefabName.Contains("dead_log") ||
					entity.ShortPrefabName.Contains("wood") ||
					entity.ShortPrefabName.Contains("swamp_tree") ||
					entity.ShortPrefabName.Contains("palm"))
			{
				resource = "wood";
				ptype = "h";
			}
		}

		if (OpenReward_Enabled)
		{
			if (entity.ShortPrefabName.Contains("minecart"))
			{
				resource = "minecart";
				ptype = "o";
			}
			else if (entity.ShortPrefabName.Contains("supply"))
			{
				resource = "supplycrate";
				ptype = "o";
			}
			else if (entity.ShortPrefabName.Contains("foodbox") || entity.ShortPrefabName.Contains("trash-pile"))
			{
				resource = "foodbox";
				ptype = "o";
			}
			else if (entity.ShortPrefabName.Contains("giftbox"))
			{
				resource = "giftbox";
				ptype = "o";
			}
			else if (entity.ShortPrefabName.Contains("crate"))
			{
				resource = "crate";
				ptype = "o";
			}
		}

		if (EntityCollectionCache.ContainsKey(entity.net.ID))
		{
			EntityCollectionCache.Remove(entity.net.ID);
		}

		if (ptype != null && resource != null)
		{
				double totalmultiplier = 1;
				totalmultiplier = (happyhouractive ? mult_HappyHourMultiplier : 1) * ((VIPMultiplier_Enabled && HasPerm(player, permVIP)) ? mult_VIPMultiplier : 1);
				GiveReward(player, resource, ptype, null, totalmultiplier);
		}
	}

	private void OnEntityDeath(BaseCombatEntity victim, HitInfo info)
	{
		BasePlayer bplayer = null;
		IPlayer    iplayer = null;

		if (!OpenReward_Enabled && !KillReward_Enabled && !HarvestReward_Enabled) return;
		if (victim == null || string.IsNullOrEmpty(victim.name)) return;
		if ((victim.name.Contains("servergibs") || victim.name.Contains("corpse")) || victim.name.Contains("assets/prefabs/plants/")) return;  // no money for cleaning up the left over crash/corpse/plants
		//Puts("oed victim.name: " + victim.name);
		if (info != null && info.Initiator != null && !string.IsNullOrEmpty(info.Initiator.name))
		{
			if (!info.Initiator is BasePlayer) return;
			if (info.Initiator is BaseNpc || info.Initiator is NPCPlayerApex ||
				info.Initiator is NPCPlayer || info.Initiator is NPCMurderer ||
				info.Initiator.name.Contains("scarecrow.prefab") ||
				info.Initiator.name.Contains("/npc/scientist/htn")) return;
			try
			{
				bplayer = info.Initiator.ToPlayer();
				if (bplayer != null && bplayer.IPlayer != null)
				{
					iplayer = bplayer.IPlayer;
				}
			}
			catch {}
		}

		string       resource = null;
		string       ptype = null;

		if (bplayer == null)
		{
			float        ptime = 0f;
			TrackPlayer  ECEData;
			ECEData.iplayer = null as IPlayer;
			ECEData.time = 0f;

			if (victim is BaseHelicopter || victim.name.Contains("patrolhelicopter") ||
				victim is CH47HelicopterAIController || victim.name.Contains("ch47") ||
				victim is BradleyAPC || victim.name.Contains("bradleyapc"))
			{

				if (victim is BaseHelicopter || victim.name.Contains("patrolhelicopter"))
				{
					resource = "helicopter";
					ptype = "k";
				}

				else if (victim is CH47HelicopterAIController || victim.name.Contains("ch47"))
				{
					resource = "chinook";
					ptype = "k";
				}
				else if (victim is BradleyAPC || victim.name.Contains("bradleyapc"))
				{
					resource = "bradley";
					ptype = "k";
				}

				if (victim != null && victim.net.ID != null)
				{
					if (EntityCollectionCache.TryGetValue(victim.net.ID, out ECEData))
					{
						if (iplayer == null)
						{
							ptime = ECEData.time;
							iplayer = ECEData.iplayer;
							bplayer = iplayer.Object as BasePlayer;
						}
						EntityCollectionCache.Remove(victim.net.ID);
					}
					else
						return;  // they already got credit in kill

				}

				if (iplayer == null || bplayer == null) // could not find player from victim
				{
					// Puts("OED no player on heli/bradley/ch47");
					return;
				}
				else if (ptime != 0f && (TOD_Sky.Instance.Cycle.Hour - ptime) >=  shotclock)  // no data or action too old
				{
					// Puts ("OED ptime too old: " + ptime.ToString() + " : " + (TOD_Sky.Instance.Cycle.Hour - shotclock));
					return;
				}
			}
		}

		if (iplayer == null || iplayer.Id == null) return; // if we did not find the player no one to give the reward to, we can exit

		BasePlayer victimplayer = null as BasePlayer;

		if (ptype == null || resource == null)
		{
			if (victim.name.Contains("loot-barrel") || victim.name.Contains("loot_barrel") || victim.name.Contains("oil_barrel"))
			{
				resource = "barrel";
				ptype = "o";
			}
			else if (victim.name.Contains("foodbox") || victim.name.Contains("trash-pile"))
			{
				resource = "foodbox";
				ptype = "o";
			}
			else if (victim.name.Contains("giftbox"))
			{
				resource = "giftbox";
				ptype = "o";
			}
			else if (victim is BaseHelicopter || victim.name.Contains("patrolhelicopter"))
			{
				resource = "helicopter";
				ptype = "k";
			}
			else if (victim is BradleyAPC || victim.name.Contains("bradleyapc"))
			{
				resource = "bradley";
				ptype = "k";
			}
			else if (victim is CH47HelicopterAIController || victim.name.Contains("ch47"))
			{
				resource = "chinook";
				ptype = "k";
			}
			else if (victim.name.Contains("assets/rust.ai/agents/") && !victim.name.Contains("corpse"))
			{
				ptype = "k";
				if (victim.name.Contains("stag"))
				{
					resource = "stag";
				}
				else if (victim.name.Contains("boar"))
				{
					resource = "boar";
				}
				else if (victim.name.Contains("horse"))
				{
					resource = "horse";
				}
				else if (victim.name.Contains("bear"))
				{
					resource = "bear";
				}
				else if (victim.name.Contains("wolf"))
				{
					resource = "wolf";
				}
				else if (victim.name.Contains("chicken"))
				{
					resource = "chicken";
				}
				else if (victim.name.Contains("zombie")) // lumped these in with Murderers
				{
					if (!NPCReward_Enabled) return;
					resource = "murderer";
				}
				else
				{
					Puts("tell mspeedie: OED missing animal: " + victim.name);
				}
			}
			else if (victim is BaseNpc || victim is NPCPlayerApex || victim is NPCPlayer || victim is Scientist || victim is NPCMurderer ||
					victim.name.Contains("scarecrow.prefab") || victim.name.Contains("scientist/htn") ||
					victim.name.Contains("scientistgunner") || victim.name.Contains("scientistastar") || victim.name.Contains("scientistturret"))
			{
				ptype = "k";
				if (!NPCReward_Enabled) return;
				else if (victim is Scientist || victim.name.Contains("scientist/htn") ||
						 victim.name.Contains("scientistgunner") || victim.name.Contains("scientistastar") || victim.name.Contains("scientistturret"))
				{
					resource = "scientist";
				}
				else if (victim is NPCMurderer || victim.name.Contains("scarecrow.prefab"))
				{
					resource = "murderer";
				}
				else
				{
					resource = "npc";
				}
			}
			else if (victim is BasePlayer)
			{
				bool isFriend = false;
				victimplayer = victim.ToPlayer();

				if (victimplayer == null || victimplayer.userID == null)
				{
					resource = "player";
					ptype = "k";
					// there appears to be an error here as data is missing
					// it probably is an npc
					// but it also could be a player so give the reward for player
					Puts("tell mspeedie to warning PVP kill on: Victim / Killer / prefab name : " + victim.name + " (" + victimplayer.displayName + ") / " + bplayer.displayName + " / " + TrimPunctuation(victim.ShortPrefabName.ToLower()));

				}
				else if (iplayer.Id == victimplayer.userID.ToString())
					return;  // killed themselves
				else
				{
					if (friendsloaded)
						try
						{
							isFriend = (bool)Friends?.CallHook("HasFriend", iplayer.Id, victimplayer.userID);
						}
						catch
						{
							isFriend = false;
						}

					if (isFriend) return;  // killing friends is not a profitable strategy
					else if (clansloaded)
					{
						try
						{
							string pclan = (string)Clans?.CallHook("GetClanOf", bplayer);
							string vclan = (string)Clans?.CallHook("GetClanOf", victimplayer);
							if (!string.IsNullOrEmpty(pclan) && !string.IsNullOrEmpty(vclan) && pclan == vclan)
								isFriend = true;
						}
						catch
						{
							isFriend = false;
						}
					}

					if (isFriend) return;  // killing friends is not a profitable strategy
					else
					{
						resource = "player";
						ptype = "k";
					}
				}
			}
			else if (victim.name == "assets/prefabs/npc/autoturret/autoturret_deployed.prefab")
			{
				resource = "autoturret";
				ptype = "k";
			}
			else if (victim.name == "assets/prefabs/npc/sam_site_turret/sam_site_turret_deployed.prefab")
			{
				resource = "sam";
				ptype = "k";
			}
			else if (victim.name == "assets/prefabs/deployable/bear trap/beartrap.prefab" ||
					victim.name == "assets/prefabs/deployable/landmine/landmine.prefab" ||
					victim.name == "assets/prefabs/deployable/floor spikes/spikes.floor.prefab" ||
					victim.name == "assets/prefabs/npc/flame turret/flameturret.deployed.prefab" ||
					victim.name == "assets/prefabs/deployable/single shot trap/guntrap.deployed.prefab" ||
					victim.name == "assets/bundled/prefabs/static/spikes_static.prefab")
			{
				resource = "trap";
				ptype = "k";
			}
			else if (victim.name.Contains("log"))
			{
				resource = "wood";
				ptype = "h";
			}
		}

		// nothing to process
		if (string.IsNullOrEmpty(resource) || string.IsNullOrEmpty(ptype)) return;  // did not find one to process

		double     totalmultiplier = 1;
		// compute applicable multipliers
		if (ptype == "k" && (DistanceMultiplier_Enabled || WeaponMultiplier_Enabled) &&
			info != null && info.Initiator != null && info.Initiator.ToPlayer() != null)
		{

			if (info.WeaponPrefab != null  && !string.IsNullOrEmpty(info.WeaponPrefab.ShortPrefabName))
			{
				string weaponname = info?.WeaponPrefab?.ShortPrefabName;
				//Puts(weaponname + " GetWeapon: " + GetWeapon(weaponname) + " Distance: " + GetDistance(victim.Distance2D(info?.Initiator?.ToPlayer())));
				totalmultiplier = (DistanceMultiplier_Enabled ? GetDistance(victim.Distance2D(info?.Initiator?.ToPlayer())) : 1) *
								  (WeaponMultiplier_Enabled ? GetWeapon(weaponname) : 1) *
								  (happyhouractive ? mult_HappyHourMultiplier : 1) * ((VIPMultiplier_Enabled && HasPerm(iplayer, permVIP)) ? mult_VIPMultiplier : 1);
			}
			else
				totalmultiplier = (DistanceMultiplier_Enabled ? GetDistance(victim.Distance2D(info?.Initiator?.ToPlayer())) : 1) *
								  (happyhouractive ? mult_HappyHourMultiplier : 1) * ((VIPMultiplier_Enabled && HasPerm(iplayer, permVIP)) ? mult_VIPMultiplier : 1);
		}
		else
		 totalmultiplier = (happyhouractive ? mult_HappyHourMultiplier : 1) * ((VIPMultiplier_Enabled && HasPerm(iplayer, permVIP)) ? mult_VIPMultiplier : 1);

		 // Give Reward
		 GiveReward(iplayer, resource, ptype, victimplayer, totalmultiplier);
	}

	private void GiveReward(IPlayer player, string reason, string ptype, BasePlayer victim, double multiplier = 1)
	{
		if (!Economics && !ServerRewards) return;

		// safety checks
		if (player == null || string.IsNullOrEmpty(reason) || multiplier < 0.001 ||
			player is BaseNpc || player is NPCPlayerApex || player is NPCPlayer || player is NPCMurderer)
			return;

		if (ptype == "O" && !OpenReward_Enabled)
			return;
		else if (ptype == "K" && !KillReward_Enabled)
			return;
		else if (ptype == "H" && !HarvestReward_Enabled)
			return;

		double amount = 0;

		if (reason.Contains("barrel"))
			amount = rate_barrel;
		else if (reason.Contains("supplycrate"))
			amount = rate_supplycrate;
		else if (reason.Contains("foodbox"))
			amount = rate_foodbox;
		else if (reason.Contains("giftbox"))
			amount = rate_giftbox;
		else if (reason.Contains("minecart"))
			amount = rate_minecart;
		else if (reason.Contains("crate"))
			amount = rate_crate;
		else if (reason == "player")
			amount = rate_player;
		else if (reason == "bear")
			amount = rate_bear;
		else if (reason == "wolf")
			amount = rate_wolf;
		else if (reason == "chicken")
			amount = rate_chicken;
		else if (reason == "horse")
			amount = rate_horse;
		else if (reason == "boar")
			amount = rate_boar;
		else if (reason == "stag")
			amount = rate_stag;
		else if (reason.Contains("cactus"))
			amount = rate_cactus;
		else if (reason == "wood")
			amount = rate_wood;
		else if (reason == "stones")
			amount = rate_stones;
		else if (reason == "sulfur")
			amount = rate_sulfur;
		else if (reason == "ore")
			amount = rate_ore;
		else if (reason == "corn")
			amount = rate_corn;
		else if (reason == "hemp")
			amount = rate_hemp;
		else if (reason == "mushrooms")
			amount = rate_mushrooms;
		else if (reason == "pumpkin")
			amount = rate_pumpkin;
		else if (reason == "helicopter")
			amount = rate_helicopter;
		else if (reason == "chinook")
			amount = rate_chinook;
		else if (reason == "murderer")
			amount = rate_murderer;
		else if (reason == "scientist")
			amount = rate_scientist;
		else if (reason == "bradley")
			amount = rate_bradley;
		else if (reason == "trap")
			amount = rate_trap;
		else if (reason == "autoturret")
			amount = rate_autoturret;
		else if (reason == "sam")
			amount = rate_sam;
		else if (reason == "activity")
			amount = rate_activityreward;
		else if (reason == "welcomemoney")
			amount = rate_welcomemoney;
		else if (reason == "npc")
			amount = rate_npckill;
		else
		{
			amount = 0;
			Puts("Rust Rewards Unknown reason:" + reason);
		}

		// no reward nothing to process
		if (amount <= 0)
			return;

		//Puts("1: reason : ptype : " + reason + " : " + ptype);

		else if (DoAdvancedVIP && groupmultiplier.Count != 0)
		{
			double temp_mult = 1.0;
			// loop through groupmultiplier till there is a hit on the table or none left
			//Puts("count gm: " + groupmultiplier.Count);
			foreach(KeyValuePair<string, double> gm in groupmultiplier)
			{
				if (string.IsNullOrEmpty(gm.Key))
				{
					Puts("Empty GM name please check your json");
				}
				//else
				//	Puts(gm.Key + " : " + gm.Value.ToString());

				if (!string.IsNullOrEmpty(gm.Key) &&  player.BelongsToGroup(gm.Key))
				{
					if (gm.Value > temp_mult) temp_mult = gm.Value;
				}				
			}
			//Puts("multiplier: " + multiplier.ToString());
			//Puts("temp_mult: " + temp_mult.ToString());
			multiplier = multiplier * temp_mult;
		}

		// make sure multipler is not zero
		if (multiplier <= 0)
		{
			Puts("Rust Rewards Multipler should be greater than zero. reason:" + reason);
			return;
		}

		amount = amount * multiplier;
		// make sure net amount is not zero or too small
		if (amount <= 0)
		{
			Puts("Net amount (amount * multipler) should be greater zero. reason:" + reason);
			return;
		}
		else if ((amount < 0.01 && UseEconomicsPlugin) || (amount < 1.0 && UseServerRewardsPlugin))
			Puts("Net amount is too small: " + amount);

		//  these use to be both if but it seems odd to me to pay in two currencies at the same rate
		if (UseServerRewardsPlugin)
		{
			amount = Math.Round(amount, 0);
			if (reason == "player" && TakeMoneyfromVictim && string.IsNullOrEmpty(victim.UserIDString))
			{
				ServerRewards?.Call("AddPoints", player.Id, (int)(amount));
				ServerRewards?.Call("TakePoints", victim.userID, (int)(amount));
			}
			else
				ServerRewards?.Call("AddPoints", player.Id, (int)(amount));
		}
		else if (UseEconomicsPlugin)
		{
			if (reason == "player" && TakeMoneyfromVictim && string.IsNullOrEmpty(victim.UserIDString))
			{
				if (!(bool)Economics?.Call("Transfer", victim.UserIDString, player.Id, amount))
				{
					MessagePlayer(player, Lang("VictimNoMoney", player.Id, victim.displayName), Lang("Prefix"), "k");
				}
				else
					Economics?.Call("Deposit", player.Id, amount);
			}
			else
				Economics?.Call("Deposit", player.Id, amount);
		}
		if (ShowcurrencySymbol)
			MessagePlayer(player, Lang(reason, player.Id, amount.ToString("C", CurrencyCulture)), Lang("Prefix"), ptype);
		else
			MessagePlayer(player, Lang(reason, player.Id, amount), Lang("Prefix"), ptype);

		if (DoLogging)
		{
			if (ShowcurrencySymbol)
				LogToFile(Name, $"[{DateTime.Now}] " + player.Name + " got " + amount.ToString("C", CurrencyCulture) + " for " + reason, this);
			else
				LogToFile(Name, $"[{DateTime.Now}] " + player.Name + " got " + amount + " for " + reason, this);
		}
		if (PrintInConsole)
			Puts(player.Name + " got " + amount + " for " + reason);
	}

	#region Commands
	[ChatCommand("rrm")]
	void ChatCommandRRM(BasePlayer player, string command, string[] args)
	{
		IPlayer iplayer = player.IPlayer;

		bool pstate = true;
		string pref = "hko";   // Havest, Kill, and Open
		string pstateString = null;
		string ptype = null;
		string ptypeString = null;

		if (args.Length < 2)
		{
			MessagePlayer(iplayer, Lang("rrm syntax", iplayer.Id), Lang("Prefix"), null);
			return;
		}
		else
		{
			if (string.IsNullOrEmpty(args[0]) || args[0].Length > 1)
			{
				MessagePlayer(iplayer, Lang("rrm type", iplayer.Id), Lang("Prefix"), null);
				return;
			}

			if (string.IsNullOrEmpty(args[1]) || args[1].Length < 2)
			{
				MessagePlayer(iplayer, Lang("rrm state", iplayer.Id), Lang("Prefix"), null);
				return;
			}

			ptype = args[0].ToLower().Substring(0, 1);
			if (ptype != "h" && ptype != "k" && ptype != "o")
			{
				MessagePlayer(iplayer, Lang("rrm type", iplayer.Id), Lang("Prefix"), null);
				return;
			}

			pstateString = args[1].ToLower().Substring(0, 2);
			if (pstateString != "on" && pstateString != "of" && pstateString != "tr" && pstateString != "fa" && pstateString != "ye" && pstateString != "no")
			{
				MessagePlayer(iplayer, Lang("rrm state", iplayer.Id), Lang("Prefix"), null);
				return;
			}
			else
			{
				if (pstateString == "of" || pstateString == "fa" || pstateString == "no")
					pstate = false;
				else
					pstate = true;
			}
		}

		try
		{
			if (playerPrefs.ContainsKey(iplayer.Id))
				playerPrefs.TryGetValue(iplayer.Id, out pref);
			else
			{
				pref = "hko";
				playerPrefs.Add(iplayer.Id, pref);
			}
		}
		catch
		{
			try
			{
				pref = "hko";
				playerPrefs.Add(iplayer.Id, pref);
			}
			catch { }
		}

		if (pstate == true && !pref.Contains(ptype))
		{
			pref = ptype + pref;
		}
		if (pstate == false && pref.Contains(ptype))
		{
			pref = pref.Replace(ptype, "");
		}

		playerPrefs[iplayer.Id] = pref;
		dataFile.WriteObject(playerPrefs);
		if (pstate)
			pstateString = "on";
		else
			pstateString = "off";

		if (ptype == "h")
			ptypeString = "harvesting";
		else if (ptype == "k")
			ptypeString = "killing";
		else if (ptype == "o")
			ptypeString = "opening";


		MessagePlayer(iplayer, Lang("rrm change", iplayer.Id, ptypeString, pstateString), Lang("Prefix"), null);
		// Puts (pref + " : " + pstateString + " : " + ptype);
	}
	#endregion
}
}