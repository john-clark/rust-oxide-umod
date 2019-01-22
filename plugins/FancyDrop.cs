using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Net;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using IEnumerator = System.Collections.IEnumerator;
using UnityEngine;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
	[Info("FancyDrop", "FuJiCuRa", "2.7.8", ResourceId = 1934)]
	[Description("The Next Level of a fancy airdrop-toolset")]
	class FancyDrop : RustPlugin
	{
		[PluginReference]
		Plugin GUIAnnouncements;

		static FancyDrop fd = null;
		bool Changed = false;
		bool initialized = false;
		Vector3 lastDropPos;
		float lastDropRadius = 0;
		Vector3 lastLootPos;
		int lastMinute;
		double lastHour;

		string msgConsoleDropSpawn;
		static string msgConsoleDropLanded;

		List<CargoPlane> CargoPlanes = new List<CargoPlane>();
		List<SupplyDrop> SupplyDrops = new List<SupplyDrop>();
		List<SupplyDrop> LootedDrops = new List<SupplyDrop>();
		List<BaseEntity> activeSignals = new List<BaseEntity>();
		Dictionary<BasePlayer, Timer> timers = new Dictionary<BasePlayer, Timer>();

		static Dictionary<string, object> defaultRealTimers()
		{
			var dp = new Dictionary<string, object>();
			dp.Add("16:00","massdrop 3");
			dp.Add("18:00","toplayer *");
			return dp;
		}

		static Dictionary<string, object> defaultServerTimers()
		{
			var dp = new Dictionary<string, object>();
			dp.Add("6","massdrop 3");
			dp.Add("18","massdropto 0 0 5 100");
			return dp;
		}

		static Dictionary<string,object> defaultDrop()
		{
			var dp = new Dictionary<string, object>();
			dp.Add("minItems", 8);
			dp.Add("maxItems", 8);
			dp.Add("minCrates", 1);
			dp.Add("maxCrates", 1);
			dp.Add("cratesGap", 50);
			dp.Add("useCustomLootTable", false);
			dp.Add("additionalheight", 0);
			dp.Add("despawnMinutes", 15);
			dp.Add("crateAirResistance", 2.0);
			dp.Add("includeStaticItemList", false);
			dp.Add("includeStaticItemListName", "regular");
			dp.Add("includeStaticItemListOnly", false);
			dp.Add("planeSpeed", 75);
			dp.Add("itemDivider", 2);
			return dp;
		}

		static Dictionary<string, object> defaultDropTypes()
		{
			var dp = new Dictionary<string, object>();

			var dp0 = new Dictionary<string, object>();
			dp0.Add("minItems", 8);
			dp0.Add("maxItems", 8);
			dp0.Add("minCrates", 1);
			dp0.Add("maxCrates", 1);
			dp0.Add("cratesGap", 50);
			dp0.Add("useCustomLootTable", false);
			dp0.Add("additionalheight", 0);
			dp0.Add("despawnMinutes", 15);
			dp0.Add("crateAirResistance", 2.0);
			dp0.Add("includeStaticItemList", false);
			dp0.Add("includeStaticItemListName", "regular");
			dp0.Add("includeStaticItemListOnly", false);
			dp0.Add("planeSpeed", 75);
			dp0.Add("itemDivider", 2);
			dp.Add("regular", dp0);

			var dp1 = new Dictionary<string, object>();
			dp1.Add("minItems", 8);
			dp1.Add("maxItems", 8);
			dp1.Add("minCrates", 1);
			dp1.Add("maxCrates", 1);
			dp1.Add("cratesGap", 50);
			dp1.Add("useCustomLootTable", false);
			dp1.Add("additionalheight", 0);
			dp1.Add("despawnMinutes", 15);
			dp1.Add("crateAirResistance", 2.0);
			dp1.Add("includeStaticItemList", false);
			dp1.Add("includeStaticItemListName", "supplysignal");
			dp1.Add("includeStaticItemListOnly", false);
			dp1.Add("planeSpeed", 75);
			dp1.Add("itemDivider", 2);
			dp.Add("supplysignal", dp1);

			var dp2 = new Dictionary<string, object>();
			dp2.Add("minItems", 8);
			dp2.Add("maxItems", 8);
			dp2.Add("minCrates", 1);
			dp2.Add("maxCrates", 1);
			dp2.Add("cratesGap", 50);
			dp2.Add("useCustomLootTable", false);
			dp2.Add("additionalheight", 0);
			dp2.Add("despawnMinutes", 15);
			dp2.Add("crateAirResistance", 2.0);
			dp2.Add("includeStaticItemList", false);
			dp2.Add("includeStaticItemListName", "massdrop");
			dp2.Add("includeStaticItemListOnly", false);
			dp2.Add("planeSpeed", 75);
			dp2.Add("itemDivider", 2);
			dp.Add("massdrop", dp2);

			var dp3 = new Dictionary<string, object>();
			dp3.Add("minItems", 8);
			dp3.Add("maxItems", 8);
			dp3.Add("minCrates", 1);
			dp3.Add("maxCrates", 1);
			dp3.Add("cratesGap", 50);
			dp3.Add("useCustomLootTable", false);
			dp3.Add("additionalheight", 0);
			dp3.Add("despawnMinutes", 15);
			dp3.Add("crateAirResistance", 2.0);
			dp3.Add("includeStaticItemList", false);
			dp3.Add("includeStaticItemListName", "dropdirect");
			dp3.Add("includeStaticItemListOnly", false);
			dp3.Add("planeSpeed", 75);
			dp3.Add("itemDivider", 2);
			dp.Add("dropdirect", dp3);

			var dp4 = new Dictionary<string, object>();
			dp4.Add("minItems", 8);
			dp4.Add("maxItems", 8);
			dp4.Add("minCrates", 1);
			dp4.Add("maxCrates", 1);
			dp4.Add("cratesGap", 50);
			dp4.Add("useCustomLootTable", false);
			dp4.Add("additionalheight", 0);
			dp4.Add("despawnMinutes", 15);
			dp4.Add("crateAirResistance", 2.0);
			dp4.Add("includeStaticItemList", false);
			dp4.Add("includeStaticItemListName", "custom_event");
			dp4.Add("includeStaticItemListOnly", false);
			dp4.Add("planeSpeed", 75);
			dp4.Add("notificationInfo", "Custom Stuff");
			dp4.Add("itemDivider", 2);
			dp.Add("custom_event", dp4);

			return dp;
		}

		static Dictionary<string, object> defaultItemList()
		{
			var dp0_0 = new Dictionary<string, object>();
			dp0_0.Add("targeting.computer", 2);
			dp0_0.Add("cctv.camera", 2);
			var dp0 = new Dictionary<string, object>();
			dp0.Add("itemList", dp0_0);
			dp0.Add("itemDivider", 2);

			var dp1_0 = new Dictionary<string, object>();
			dp1_0.Add("explosive.timed", 4);
			dp1_0.Add("metal.refined", 100);
			var dp1 = new Dictionary<string, object>();
			dp1.Add("itemList", dp1_0);
			dp1.Add("itemDivider", 2);

			var dp2_0 = new Dictionary<string, object>();
			dp2_0.Add("explosive.timed", 4);
			dp2_0.Add("grenade.f1", 10);
			var dp2 = new Dictionary<string, object>();
			dp2.Add("itemList", dp2_0);
			dp2.Add("itemDivider", 2);

			var dp3_0 = new Dictionary<string, object>();
			dp3_0.Add("explosive.timed", 4);
			dp3_0.Add("surveycharge", 10);
			var dp3 = new Dictionary<string, object>();
			dp3.Add("itemList", dp3_0);
			dp3.Add("itemDivider", 2);

			var dp4_0 = new Dictionary<string, object>();
			dp4_0.Add("explosive.timed", 10);
			dp4_0.Add("grenade.f1", 10);
			var dp4 = new Dictionary<string, object>();
			dp4.Add("itemList", dp4_0);
			dp4.Add("itemDivider", 2);

			var dp = new Dictionary<string, object>();
			dp.Add("regular", dp0);
			dp.Add("supplysignal", dp1);
			dp.Add("massdrop", dp2);
			dp.Add("dropdirect", dp3);
			dp.Add("custom_event", dp4);

			return dp;
		}

		FieldInfo dropPlanestartPos = typeof(CargoPlane).GetField("startPos", (BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public));
		FieldInfo dropPlaneendPos = typeof(CargoPlane).GetField("endPos", (BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public));
		FieldInfo dropPlanesecondsToTake = typeof(CargoPlane).GetField("secondsToTake", (BindingFlags.Instance | BindingFlags.NonPublic |  BindingFlags.Public));
		FieldInfo dropPlanesecondsTaken = typeof(CargoPlane).GetField("secondsTaken", (BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public));
		FieldInfo dropPlanedropped = typeof(CargoPlane).GetField("dropped", (BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public));
		FieldInfo _isSpawned = typeof(BaseNetworkable).GetField("isSpawned", (BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public));
		FieldInfo _creationFrame = typeof(BaseNetworkable).GetField("creationFrame", (BindingFlags.Instance | BindingFlags.NonPublic |  BindingFlags.Public));
		static FieldInfo _parachute = typeof(SupplyDrop).GetField("parachute", (BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public));

		CargoPlane referencePlane = GameManager.server.FindPrefab("assets/prefabs/npc/cargo plane/cargo_plane.prefab").GetComponent<CargoPlane>();
		SpawnFilter spawnFilter = GameManager.server.FindPrefab("assets/prefabs/npc/cargo plane/cargo_plane.prefab").GetComponent<CargoPlane>().filter;
		
		List<Regex> regexTags = new List<Regex>
		{
			new Regex(@"<color=.+?>", RegexOptions.Compiled),
			new Regex(@"<size=.+?>", RegexOptions.Compiled)
		};

		List<string> tags = new List<string>
		{
			"</color>",
			"</size>",
			"<i>",
			"</i>",
			"<b>",
			"</b>"
		};

		#region Config

		Dictionary<string,object> setupDropTypes;
		Dictionary<string,object> setupDropDefault;
		Dictionary<string, object> setupItemList;

		float planeOffSetXMultiply;
		float planeOffSetYMultiply;

		bool airdropTimerEnabled;
		string airdropTimerCmd;
		bool airdropRemoveInBuilt;
		bool airdropTimerResetAfterRandom;
		bool airdropCleanupAtStart;
		int airdropTimerMinPlayers;
		int airdropTimerWaitMinutesMin;
		int airdropTimerWaitMinutesMax;
		int airdropMassdropDefault;
		float airdropMassdropRadiusDefault;
		float airdropMassdropDelay;
		Timer _aidropTimer;
		Timer _massDropTimer;

		static bool useSupplyDropEffectLanded;
		static string supplyDropEffect;
		bool disableRandomSupplyPos;
		bool shootDownDrops;
		int shootDownCount;

		bool supplyDropLight;
		float dropLightFrequency;
		float dropLightOffTime;
		bool removeDropLightOnLanded;
		float signalRocketSpeed;
		float signalRocketExplosionTime;

		string Prefix;
		string Color;
		string colorAdmMsg;
		string colorTextMsg;
		string Format;
		string guiCommand;
		float supplySignalSmokeTime;
		int neededAuthLvl;
		bool lockDirectDrop;
		bool lockSignalDrop;
		bool unlockDropAfterLoot;
		string version;

		bool useRealtimeTimers;
		bool useGametimeTimers;
		bool logTimersToConsole;
		int timersMinPlayers;
		Dictionary<string, object> realTimers = new Dictionary<string, object>();
		Dictionary<string, object> serverTimers = new Dictionary<string, object>();

		bool notifyByChatAdminCalls;
		bool notifyDropGUI;
		bool notifyDropServerSignal;
		bool notifyDropServerSignalCoords;
		bool notifyDropSignalByPlayer;
		bool notifyDropSignalByPlayerCoords;
		bool notifyDropAdminSignal;
		bool notifyDropConsoleSignal;

		bool notifyDropServerRegular;
		bool notifyDropServerRegularCoords;
		bool notifyDropConsoleRegular;

		bool notifyDropServerCustom;
		bool notifyDropServerCustomCoords;

		bool notifyDropServerMass;

		bool notifyDropPlayer;
		bool notifyDropDirect;

		static bool notifyDropPlayersOnLanded;
		float supplyDropNotifyDistance;
		float supplyLootNotifyDistance;

		bool notifyDropServerLooted;
		bool notifyDropServerLootedCoords;
		bool notifyDropConsoleLooted;

		static bool notifyDropConsoleOnLanded;
		bool notifyDropConsoleSpawned;
		bool notifyDropConsoleFirstOnly;

		bool SimpleUI_Enable;
		int SimpleUI_FontSize;
		float SimpleUI_Top;
		float SimpleUI_Left;
		float SimpleUI_MaxWidth;
		float SimpleUI_MaxHeight;
		float SimpleUI_HideTimer;
		string SimpleUI_NoticeColor;
		string SimpleUI_ShadowColor;

		object GetConfig(string menu, string datavalue, object defaultValue)
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

		protected override void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
			                      {
									{"msgDropSignal", "Someone ordered an Airdrop"},
									{"msgDropSignalCoords", "Someone ordered an Airdrop to position <color=yellow>X:{0} Z:{1}</color>"},
									{"msgDropSignalAdmin", "Signal thrown by '{0}' at: {1}"},
									{"msgDropSignalByPlayer", "Signal thrown by <color=yellow>{0}</color>"},
									{"msgDropSignalByPlayerCoords", "Signal thrown by <color=yellow>{0}</color> at position <color=yellow>X:{1} Z:{2}</color>"},
									{"msgDropRegular", "Cargoplane will deliver the daily AirDrop in a few moments"},
									{"msgDropRegularCoords", "Cargoplane will deliver the daily AirDrop at <color=yellow>X:{0} | Z:{1}</color> in a few moments"},
									{"msgDropMass", "Massdrop incoming"},
									{"msgDropCustom", "Eventdrop <color=orange>{0}</color> is on his way"},
									{"msgDropCustomCoords", "Eventdrop <color=orange>{0}</color> is on his way to <color=yellow>X:{1} | Z:{2}</color>"},
									{"msgDropPlayer", "<color=yellow>Incoming Drop</color> to your current location"},
									{"msgDropDirect", "<color=yellow>Drop</color> spawned above your <color=yellow>current</color> location"},
									{"msgDropLanded", "Supplydrop has landed <color=yellow>{0:F0}m</color> away from you at direction <color=yellow>{1}</color>"},
									{"msgDropLootet", "<color=yellow>{0}</color> was looting the AirDrop"},
									{"msgDropLootetCoords", "<color=yellow>{0}</color> was looting the AirDrop at (<color=yellow>X:{1} | Z:{2}</color>)"},
									{"msgNoAccess", "You are not allowed to use this command"},
									{"msgConsoleDropSpawn", "SupplyDrop spawned at (X:{0} Y:{1} Z:{2})"},
									{"msgConsoleDropLanded", "SupplyDrop landed at (X:{0} Y:{1} Z:{2})"},
									{"msgCrateLocked", "This crate is locked until being looted by the owner"},
									{"msgNorth", "North"},
									{"msgNorthEast", "NorthEast"},
									{"msgEast", "East"},
									{"msgSouthEast", "SouthEast"},
									{"msgSouth", "South"},
									{"msgSouthWest", "SouthWest"},
									{"msgWest", "West"},
									{"msgNorthWest", "NorthWest"},
								  },this);
		}

		void LoadVariables()
		{
			setupDropTypes = (Dictionary<string, object>)GetConfig("DropSettings", "DropTypes", defaultDropTypes());
			setupDropDefault = (Dictionary<string, object>)GetConfig("DropSettings", "DropDefault", defaultDrop());
			setupItemList = (Dictionary<string, object>)GetConfig("StaticItems", "DropTypes", defaultItemList());

			useSupplyDropEffectLanded = Convert.ToBoolean(GetConfig("Airdrop", "Use special effect at reaching ground position", true));
			supplyDropEffect = Convert.ToString(GetConfig("Airdrop", "Prefab effect to use", "assets/bundled/prefabs/fx/survey_explosion.prefab"));
			airdropMassdropDefault = Convert.ToInt32(GetConfig("Airdrop", "Massdrop default plane amount", 5));
			airdropMassdropDelay = Convert.ToSingle(GetConfig("Airdrop", "Delay between Massdrop plane spawns", 0.66));
			airdropMassdropRadiusDefault = Convert.ToSingle(GetConfig("Airdrop", "Default radius for location based massdrop", 100));
			signalRocketSpeed = Convert.ToSingle(GetConfig("Airdrop", "signal rocket speed", 15));
			signalRocketExplosionTime = Convert.ToSingle(GetConfig("Airdrop", "signal rocket explosion timer", 15));
			shootDownDrops = Convert.ToBoolean(GetConfig("Airdrop", "Players can shoot down the drop", false));
			shootDownCount = Convert.ToInt32(GetConfig("Airdrop", "Players can shoot down the drop - needed hits", 5));

			airdropTimerEnabled = Convert.ToBoolean(GetConfig("Timer", "Use Airdrop timer", true));
			airdropTimerCmd = Convert.ToString(GetConfig("Timer", "Used Airdrop timer command", "random"));
			airdropRemoveInBuilt = Convert.ToBoolean(GetConfig("Timer", "Remove builtIn Airdrop", true));
			airdropCleanupAtStart = Convert.ToBoolean(GetConfig("Timer", "Cleanup old Drops at serverstart", true));
			airdropTimerMinPlayers = Convert.ToInt32(GetConfig("Timer", "Minimum players for timed Drop", 2));
			airdropTimerWaitMinutesMin = Convert.ToInt32(GetConfig("Timer", "Minimum minutes for random timer delay ", 30));
			airdropTimerWaitMinutesMax = Convert.ToInt32(GetConfig("Timer", "Maximum minutes for random timer delay ", 50));
			airdropTimerResetAfterRandom = Convert.ToBoolean(GetConfig("Timer", "Reset Timer after manual random drop", false));

			planeOffSetXMultiply = Convert.ToSingle(GetConfig("Airdrop", "Multiplier for overall flight distance; lower means faster at map", 1.25));
			planeOffSetYMultiply = Convert.ToSingle(GetConfig("Airdrop", "Multiplier for (plane height * highest point on Map); Default 1.0", 1.0));
			disableRandomSupplyPos = Convert.ToBoolean(GetConfig("Airdrop", "Disable SupplySignal randomization", false));

			Prefix = Convert.ToString(GetConfig("Generic", "Chat/Message prefix", "Air Drop"));
			Color = Convert.ToString(GetConfig("Generic", "Prefix color", "cyan"));
			Format = Convert.ToString(GetConfig("Generic", "Prefix format", "<color={0}>{1}</color>: "));
			guiCommand = Convert.ToString(GetConfig("Generic", "GUI Announce command", "announce.announce"));
			neededAuthLvl = Convert.ToInt32(GetConfig("Generic", "AuthLevel needed for console commands", 1));
			supplySignalSmokeTime = Convert.ToSingle(GetConfig("Generic", "Time for active smoke of SupplySignal", 210.0));
			colorAdmMsg = Convert.ToString(GetConfig("Generic", "Admin messages color", "silver"));
			notifyByChatAdminCalls = Convert.ToBoolean(GetConfig("Generic", "Show message to admin after command usage", true));
			colorTextMsg = Convert.ToString(GetConfig("Generic", "Broadcast messages color", "white"));
			lockDirectDrop = Convert.ToBoolean(GetConfig("Generic", "Lock DirectDrop to be looted only by target player", true));
			lockSignalDrop = Convert.ToBoolean(GetConfig("Generic", "Lock SignalDrop to be looted only by target player", false));
			unlockDropAfterLoot = Convert.ToBoolean(GetConfig("Generic", "Unlock crates only after player stopped looting", false));

			version = Convert.ToString(GetConfig("Generic", "version", this.Version.ToString()));

			if (version != this.Version.ToString())
			{
				Config["Generic","version"] =  this.Version.ToString();
				Changed = true;
			}

			useRealtimeTimers = Convert.ToBoolean(GetConfig("Timers", "use RealTime", false));
			useGametimeTimers = Convert.ToBoolean(GetConfig("Timers", "use ServerTime", false));
			logTimersToConsole = Convert.ToBoolean(GetConfig("Timers", "log to console", true));
			realTimers = (Dictionary<string, object>)GetConfig("Timers", "RealTime", defaultRealTimers());
			serverTimers = (Dictionary<string, object>)GetConfig("Timers", "ServerTime", defaultServerTimers());
			timersMinPlayers = Convert.ToInt32(GetConfig("Timers", "Minimum players for running Timers", 0));

			supplyDropLight = Convert.ToBoolean(GetConfig("DropLight", "use SupplyDrop Light", true));
			dropLightFrequency = Convert.ToSingle(GetConfig("DropLight", "Frequency for blinking", 0.25));
			dropLightOffTime = Convert.ToSingle(GetConfig("DropLight", "Time for pause between blinking", 3.0));
			removeDropLightOnLanded = Convert.ToBoolean(GetConfig("DropLight", "remove Light once landed", false));

			notifyDropGUI = Convert.ToBoolean(GetConfig("Notification", "use GUI Announcements for any Drop notification", false));

			notifyDropServerSignal = Convert.ToBoolean(GetConfig("Notification", "Notify players at Drop by SupplySignal", true));
			notifyDropServerSignalCoords = Convert.ToBoolean(GetConfig("Notification", "Notify players at Drop by SupplySignal including Coords ", false));
			notifyDropConsoleSignal = Convert.ToBoolean(GetConfig("Notification", "Notify console at Drop by SupplySignal", true));

			notifyDropServerRegular = Convert.ToBoolean(GetConfig("Notification", "Notify players at Random/Timed Drop", true));
			notifyDropServerRegularCoords = Convert.ToBoolean(GetConfig("Notification", "Notify players at Random/Timed Drop including Coords", false));
			notifyDropConsoleRegular = Convert.ToBoolean(GetConfig("Notification", "Notify console at timed-regular Drop", true));

			notifyDropServerCustom = Convert.ToBoolean(GetConfig("Notification", "Notify players at custom/event Drop", true));
			notifyDropServerCustomCoords = Convert.ToBoolean(GetConfig("Notification", "Notify players at custom/event Drop including Coords", false));

			notifyDropServerMass = Convert.ToBoolean(GetConfig("Notification", "Notify players at Massdrop", true));

			notifyDropPlayer = Convert.ToBoolean(GetConfig("Notification", "Notify a player about incoming Drop to his location", true));
			notifyDropDirect = Convert.ToBoolean(GetConfig("Notification", "Notify a player about spawned Drop at his location", true));

			notifyDropPlayersOnLanded = Convert.ToBoolean(GetConfig("Notification", "Notify players when Drop is landed about distance", true));
			notifyDropConsoleOnLanded = Convert.ToBoolean(GetConfig("Notification", "Notify console when Drop is landed", false));

			notifyDropConsoleSpawned = Convert.ToBoolean(GetConfig("Notification", "Notify console when Drop is spawned", false));
			notifyDropConsoleFirstOnly = Convert.ToBoolean(GetConfig("Notification", "Notify console when Drop landed/spawned only at the first", true));

			supplyDropNotifyDistance = Convert.ToSingle(GetConfig("Notification", "Maximum distance in meters to get notified about landed Drop", 1000));
			supplyLootNotifyDistance = Convert.ToSingle(GetConfig("Notification", "Maximum distance in meters to get notified about looted Drop", 1000));
			notifyDropServerLooted = Convert.ToBoolean(GetConfig("Notification", "Notify players when a Drop is being looted", true));
			notifyDropServerLootedCoords = Convert.ToBoolean(GetConfig("Notification", "Notify players when a Drop is being looted including coords", false));
			notifyDropConsoleLooted = Convert.ToBoolean(GetConfig("Notification", "Notify console when a Drop is being looted", true));

			notifyDropSignalByPlayer = Convert.ToBoolean(GetConfig("Notification", "Notify Players who has thrown a SupplySignal", false));
			notifyDropSignalByPlayerCoords = Convert.ToBoolean(GetConfig("Notification", "Notify Players who has thrown a SupplySignal including coords", false));
			notifyDropAdminSignal = Convert.ToBoolean(GetConfig("Notification", "Notify admins per chat about player who has thrown SupplySignal ", false));

			SimpleUI_Enable = Convert.ToBoolean(GetConfig("SimpleUI", "SimpleUI_Enable", false));
			SimpleUI_FontSize = Convert.ToInt32(GetConfig("SimpleUI", "SimpleUI_FontSize", 25));
			SimpleUI_Top = Convert.ToSingle(GetConfig("SimpleUI", "SimpleUI_Top", 0.05));
			SimpleUI_Left = Convert.ToSingle(GetConfig("SimpleUI", "SimpleUI_Left", 0.1));
			SimpleUI_MaxWidth = Convert.ToSingle(GetConfig("SimpleUI", "SimpleUI_MaxWidth", 0.8));
			SimpleUI_MaxHeight = Convert.ToSingle(GetConfig("SimpleUI", "SimpleUI_MaxHeight", 0.1));
			SimpleUI_HideTimer = Convert.ToSingle(GetConfig("SimpleUI", "SimpleUI_HideTimer", 10));
			SimpleUI_NoticeColor = Convert.ToString(GetConfig("SimpleUI", "SimpleUI_NoticeColor", "1 1 1 0.9"));
			SimpleUI_ShadowColor = Convert.ToString(GetConfig("SimpleUI", "SimpleUI_ShadowColor", "0.1 0.1 0.1 0.5"));

			if (!Changed) return;
			SaveConfig();
			Changed = false;
		}

		protected override void LoadDefaultConfig()
		{
			Config.Clear();
			LoadVariables();
		}

		#endregion Config

		#region ColliderCheck

		void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
		{
			if (!shootDownDrops || info == null)
				return;
			if (entity is SupplyDrop)
			{
				var drop = entity as SupplyDrop;
				if (drop == null || drop.IsDestroyed) return;

				BaseEntity parachute = _parachute.GetValue(drop) as BaseEntity;
				if (parachute == null || parachute.IsDestroyed) return;

				var col = drop.GetComponent<ColliderCheck>();
				if (col == null) return;
				if (col.hitCounter < shootDownCount)
				{
					col.hitCounter++;
					return;
				}
				parachute.Kill();
				parachute = null;
				drop.GetComponent<Rigidbody>().drag = 0.3f;
				drop.GetComponent<Rigidbody>().collisionDetectionMode = CollisionDetectionMode.Continuous;
				col.wasHit = true;
			}
		}

		sealed class ColliderCheck : FacepunchBehaviour
		{
			public bool notifyEnabled = true;
			public bool notifyConsole;
			public Dictionary<string,object> cratesettings;
			public bool landed = false;
			public int hitCounter = 0;
			public bool wasHit;
			public DropLight dropLight;

			void Awake()
			{
				fd.NextTick(() => {
					if (cratesettings.ContainsKey("maxItems"))
						fd.SetupContainer(GetComponent<StorageContainer>(), cratesettings);
					else
						Awake();
				});
			}

			void PlayerStoppedLooting(BasePlayer player)
			{
				if (!fd.unlockDropAfterLoot || GetComponent<BaseEntity>().OwnerID == 0uL || player.userID != GetComponent<BaseEntity>().OwnerID) return;
				GetComponent<BaseEntity>().OwnerID = 0uL;
				fd.LootedDrops.Add(GetComponent<BaseEntity>() as SupplyDrop);
			}

			void OnCollisionEnter(Collision col)
			{
				if (!landed)
				{
					landed = true;
					if (wasHit)
					{
						if (useSupplyDropEffectLanded)
							Effect.server.Run(supplyDropEffect, GetComponent<BaseEntity>().transform.position);
						StartCoroutine( HitRemove() );
						return;
					}
					if (cratesettings.ContainsKey("fireSignalRocket") && (bool)cratesettings["fireSignalRocket"])
						fd.CreateRocket(GetComponent<BaseEntity>().transform.position);
					if (notifyEnabled && notifyDropPlayersOnLanded && (string)cratesettings["droptype"] != "dropdirect" && (((fd.lastDropRadius*2)*1.2) - Vector3.Distance(fd.lastDropPos, GetComponent<BaseEntity>().transform.position) <= 0 && !(UnityEngine.CollisionEx.GetEntity(col) is SupplyDrop)))
						fd.NotifyOnDropLanded(GetComponent<BaseEntity>());
					fd.lastDropPos = GetComponent<BaseEntity>().transform.position;
					StartCoroutine( DeSpawn() );
					if (useSupplyDropEffectLanded)
							Effect.server.Run(supplyDropEffect, GetComponent<BaseEntity>().transform.position);
					if(notifyConsole && notifyDropConsoleOnLanded)
						fd.Puts(string.Format(fd.lang.GetMessage(msgConsoleDropLanded, fd), GetComponent<BaseEntity>().transform.position.x.ToString("0"),GetComponent<BaseEntity>().transform.position.y.ToString("0"),GetComponent<BaseEntity>().transform.position.z.ToString("0")));
				}
			}

			IEnumerator HitRemove()
			{
				yield return new WaitForEndOfFrame();
				(GetComponent<BaseEntity>() as StorageContainer).DropItems();
				fd.SupplyDrops.Remove(GetComponent<SupplyDrop>());
				Destroy(dropLight);
				cratesettings.Clear();
				GetComponent<BaseEntity>().Kill();
			}

			IEnumerator DeSpawn()
			{
				yield return new WaitForSeconds( (int)cratesettings["despawnMinutes"] * 60 );
				yield return new WaitWhile(() => GetComponent<BaseEntity>().IsOpen());
				Destroy(dropLight);
				cratesettings.Clear();
				GetComponent<BaseEntity>().Kill();
			}

			void OnDestroy()
			{
				Destroy(dropLight);
				cratesettings.Clear();
				fd.SupplyDrops.Remove(GetComponent<SupplyDrop>());
				fd.LootedDrops.Remove(GetComponent<SupplyDrop>());
			}

			public ColliderCheck(){}
		}

		#endregion ColliderCheck

		#region DropTiming

		sealed class DropTiming : FacepunchBehaviour
		{
			private int dropCount;
			private float updatedTime;
			public Vector3 startPos;
			public Vector3 endPos;
			public bool notify = true;
			private bool notifyConsole = true;
			private int cratesToDrop;
			public Dictionary<string,object> dropsettings;

			private float gapTimeToTake = 0f;
			private float halfTimeToTake = 0f;
			private float offsetTimeToTake = 0f;

			void Awake()
			{
				dropCount = 0;
				notifyConsole = true;
			}

			public void GetSettings(Dictionary<string,object> drop, Vector3 start, Vector3 end, float seconds)
			{
				dropsettings = new Dictionary<string,object>(drop);
				startPos = start;
				endPos = end;
				gapTimeToTake = Convert.ToSingle(dropsettings["cratesGap"]) / Convert.ToSingle(dropsettings["planeSpeed"]);
				halfTimeToTake = seconds / 2;
				offsetTimeToTake = gapTimeToTake / 2 ;
				cratesToDrop = Mathf.RoundToInt(UnityEngine.Random.Range(Convert.ToSingle(dropsettings["minCrates"])*100f, Convert.ToSingle(dropsettings["maxCrates"])*100f) / 100f);
				if ((cratesToDrop % 2) == 0)
					updatedTime = halfTimeToTake - offsetTimeToTake - ( (cratesToDrop-1) / 2 * gapTimeToTake);
				else
					updatedTime = halfTimeToTake - ( (cratesToDrop-1) / 2 * gapTimeToTake);
			}
            public void TimeOverride(float seconds)
            {
                halfTimeToTake = seconds / 2;
                offsetTimeToTake = gapTimeToTake / 2;
                cratesToDrop = Mathf.RoundToInt(UnityEngine.Random.Range(Convert.ToSingle(dropsettings["minCrates"]) * 100f, Convert.ToSingle(dropsettings["maxCrates"]) * 100f) / 100f);
                if ((cratesToDrop % 2) == 0)
                    updatedTime = halfTimeToTake - offsetTimeToTake - ((cratesToDrop - 1) / 2 * gapTimeToTake);
                else
                    updatedTime = halfTimeToTake - ((cratesToDrop - 1) / 2 * gapTimeToTake);
            }
			void Update()
			{
				if ((float)fd.dropPlanesecondsTaken.GetValue(GetComponent<CargoPlane>()) > updatedTime && dropCount < cratesToDrop)
				{
					updatedTime += gapTimeToTake;
					dropCount++;
					fd.createSupplyDrop(GetComponent<CargoPlane>().transform.position, new Dictionary<string,object>(dropsettings), notify, notifyConsole, startPos, endPos);
					if(fd.notifyDropConsoleSpawned && notifyConsole)
						fd.Puts(string.Format(fd.lang.GetMessage(fd.msgConsoleDropSpawn, fd), GetComponent<BaseEntity>().transform.position.x.ToString("0"),GetComponent<BaseEntity>().transform.position.y.ToString("0"),GetComponent<BaseEntity>().transform.position.z.ToString("0")));
				}
				if (dropCount == 1 && notify) notify = false;
				if (dropCount == 1 && fd.notifyDropConsoleFirstOnly) notifyConsole = false;
			}

			void OnDestroy()
			{
				dropsettings.Clear();
				fd.CargoPlanes.Remove(GetComponent<CargoPlane>());
				Destroy(this);
			}

			public DropTiming(){}
		}

		#endregion DropTiming

		#region DropLight

		sealed class DropLight : FacepunchBehaviour
		{
			bool isRunning = false;
			BaseEntity light;

			public void Init()
			{
				light = GetComponent<BaseEntity>();
				if (TOD_Sky.Instance.IsNight)
				{
					if(!(bool)fd._isSpawned.GetValue(light))
						light.Spawn();
					isRunning = true;
					StartCoroutine( Light() );
				}
			}

			void Update()
			{
				if (TOD_Sky.Instance.IsNight && !isRunning)
				{
					if(!(bool)fd._isSpawned.GetValue(light))
						light.Spawn();
					isRunning = true;
					StartCoroutine( Light() );
				}
				if((fd.removeDropLightOnLanded && light.GetParentEntity().GetComponent<ColliderCheck>().landed) || light.ParentHasFlag(BaseEntity.Flags.Open))
					Destroy(this);
			}

			IEnumerator Light()
			{
				WaitForSeconds wait = new WaitForSeconds(fd.dropLightFrequency);
				light.limitNetworking = false;
				if (light.GetParentEntity() != null && light.GetParentEntity().GetComponent<ColliderCheck>().landed)
					yield return new WaitForSeconds(fd.dropLightOffTime);			
				else
				{
					yield return wait;				
					for (int i = 0; i < 4; i++)
					{
						light.limitNetworking = true;
						yield return wait;
						light.limitNetworking = false;
						yield return wait;
					}
					light.limitNetworking = true;
					yield return new WaitForSeconds(fd.dropLightOffTime);
				}				
				if(TOD_Sky.Instance.IsNight)
					StartCoroutine( Light() );
				else
				{
					isRunning = false;
					Destroy(this);
				}
			}

			void OnDestroy()
			{
				if (!light.IsDestroyed)
					light.Kill();
			}

			public DropLight(){}
		}

		#endregion DropLight

		#region ObjectsCreation

		CargoPlane createCargoPlane(Vector3 pos = new Vector3())
		{
			var newPlane = GameManager.server.CreateEntity("assets/prefabs/npc/cargo plane/cargo_plane.prefab", pos, new Quaternion(), true) as CargoPlane;
			return newPlane;
		}

		void createSupplyDrop(Vector3 pos, Dictionary<string,object> cratesettings, bool notify = true, bool notifyConsole = true, Vector3 start = new Vector3(), Vector3 end = new Vector3())
		{
			SupplyDrop newDrop;
			object value;
			if(cratesettings.TryGetValue("userID", out value))
			{
				newDrop = GameManager.server.CreateEntity("assets/prefabs/misc/supply drop/supply_drop.prefab", pos, new Quaternion(), true) as SupplyDrop;
				(newDrop as BaseEntity).OwnerID = (ulong)value;
			}
			else
				newDrop = GameManager.server.CreateEntity("assets/prefabs/misc/supply drop/supply_drop.prefab", pos, Quaternion.LookRotation(end - start), true) as SupplyDrop;
			(newDrop as BaseNetworkable).gameObject.AddComponent<ColliderCheck>();
			newDrop.GetComponent<ColliderCheck>().cratesettings = cratesettings;
			newDrop.GetComponent<ColliderCheck>().notifyEnabled = notify;
			newDrop.GetComponent<ColliderCheck>().notifyConsole = notifyConsole;
			newDrop.GetComponent<Rigidbody>().drag = Convert.ToSingle(cratesettings["crateAirResistance"]);
			if ((bool)cratesettings["betterloot"])
				newDrop.GetComponent<LootContainer>().initialLootSpawn = false;
			object obj = Interface.CallHook("OnFancyDropCrate", cratesettings );
			if (obj != null)
			{
				newDrop.GetComponent<ColliderCheck>().cratesettings["AlCustom"] = true;
				newDrop.GetComponent<ColliderCheck>().cratesettings["AlCustomList"] = (List<Item>)obj;
				SpawnNetworkable(newDrop as BaseNetworkable);
			}
			else
				newDrop.Spawn();
			Interface.CallHook("OnLootSpawn", new object[]{ newDrop.GetComponent<LootContainer>()});
			SupplyDrops.Add(newDrop);
			if (supplyDropLight)
				newDrop.GetComponent<ColliderCheck>().dropLight = createLantern(newDrop as BaseEntity);
		}

		void SpawnNetworkable(BaseNetworkable ent)
		{
			if (ent.GetComponent<UnityEngine.Component>().transform.root != ent.GetComponent<UnityEngine.Component>().transform)
				ent.GetComponent<UnityEngine.Component>().transform.parent = null;
			Rust.Registry.Entity.Register(ent.GetComponent<UnityEngine.Component>().gameObject, ent);
			if (ent.net == null)
				ent.net = Network.Net.sv.CreateNetworkable();
			ent.net.handler = ent;
			_creationFrame.SetValue(ent, Time.frameCount);
			ent.PreInitShared();
			ent.InitShared();
			ent.ServerInit();
			ent.PostInitShared();
			ent.UpdateNetworkGroup();
			_isSpawned.SetValue(ent, true);
			Interface.CallHook("OnEntitySpawned", ent );
			ent.SendNetworkUpdateImmediate(true);
		}
		
		DropLight createLantern(BaseEntity entity)
		{
			var lantern = (TimedExplosive)GameManager.server.CreateEntity("assets/prefabs/tools/flareold/flare.deployed.prefab", default(Vector3), default(Quaternion), true);
			lantern.gameObject.AddComponent<DropLight>();
			lantern.gameObject.Identity();
			lantern.SetParent(entity as LootContainer, "parachute_attach");
			lantern.SetMotionEnabled(false);
			lantern.SetCollisionEnabled(false);
			lantern.timerAmountMax *= 10f;
			lantern.timerAmountMin *= 10f;
			lantern.transform.localPosition = new Vector3(0f, 0.25f, 0f);
			lantern.transform.localRotation = Quaternion.Euler(270f,0f,0f);
			lantern.GetComponent<DropLight>().Init();
			return lantern.GetComponent<DropLight>();
		}

		void CreateRocket(Vector3 startPoint)
		{
			BaseEntity entity = null;
			if (TOD_Sky.Instance.IsNight)
				entity = GameManager.server.CreateEntity("assets/prefabs/npc/patrol helicopter/rocket_heli_airburst.prefab", startPoint + new Vector3(0,10,0), new Quaternion(), true);
			else
				entity = GameManager.server.CreateEntity("assets/prefabs/ammo/rocket/rocket_smoke.prefab", startPoint + new Vector3(0,10,0), new Quaternion(), true);
			entity.GetComponent<TimedExplosive>().timerAmountMin = signalRocketExplosionTime;
			entity.GetComponent<TimedExplosive>().timerAmountMax = signalRocketExplosionTime;
			entity.GetComponent<ServerProjectile>().gravityModifier = 0f;
			entity.GetComponent<ServerProjectile>().speed = signalRocketSpeed;
			for (int i = 0; i < entity.GetComponent<TimedExplosive>().damageTypes.Count; i++)
			{
				entity.GetComponent<TimedExplosive>().damageTypes[i].amount *= 0f;
			}
			entity.SendMessage("InitializeVelocity", Vector3.up * 2f);
			entity.Spawn();
		}

		void startCargoPlane(Vector3 dropToPos = new Vector3(), bool randomDrop = true, CargoPlane plane = null, string dropType = "regular", string staticList = "", bool showinfo = true, ulong userID = 0uL)
		{
			Dictionary<string,object> dropsettings;
			if (setupDropTypes.ContainsKey(dropType))
			{
				dropsettings = new Dictionary<string,object>((Dictionary<string,object>)setupDropTypes[dropType]);
				object value;
				foreach( var pair in setupDropDefault)
					if (!dropsettings.TryGetValue(pair.Key, out value))
						dropsettings.Add(pair.Key, setupDropDefault[pair.Key]);
			}
			else
				dropsettings = new Dictionary<string,object>((Dictionary<string,object>)setupDropDefault);
			if (userID != 0uL)
				dropsettings.Add("userID", userID);
			dropsettings.Add("droptype", dropType);
			if(staticList != "")
				dropsettings["includeStaticItemListName"] = staticList;
			if (Convert.ToInt32(dropsettings["planeSpeed"]) < 20)
				dropsettings["planeSpeed"] = 20;
			if (Convert.ToSingle(dropsettings["crateAirResistance"]) < 0.6)
				dropsettings["crateAirResistance"] = 0.6;
			if (Convert.ToInt32(dropsettings["despawnMinutes"]) < 1)
				dropsettings["despawnMinutes"] = 1;
			if (Convert.ToInt32(dropsettings["cratesGap"]) < 5)
				dropsettings["cratesGap"] = 5;
			if (Convert.ToInt32(dropsettings["minItems"]) < 1)
				dropsettings["minItems"] = 1;
			if (Convert.ToInt32(dropsettings["maxItems"]) < 1)
				dropsettings["maxItems"] = 1;
			if (Convert.ToInt32(dropsettings["minCrates"]) < 1)
				dropsettings["minCrates"] = 1;
			if (Convert.ToInt32(dropsettings["maxCrates"]) < 1)
				dropsettings["maxCrates"] = 1;
			object isSupplyDropActive = Interface.Oxide.CallHook("isSupplyDropActive");
			dropsettings["betterloot"] = isSupplyDropActive != null && (bool)isSupplyDropActive ? true : false;
			string notificationInfo = "";
			if(dropsettings.ContainsKey("notificationInfo"))
				notificationInfo = (string)dropsettings["notificationInfo"];
			if (plane == null)
				plane = createCargoPlane();
			if (randomDrop)
				dropToPos = plane.RandomDropPosition();
			float x = TerrainMeta.Size.x;
			float y;
			y = (TerrainMeta.HighestPoint.y * planeOffSetYMultiply) + (int)dropsettings["additionalheight"];
			Vector3 startPos = Vector3Ex.Range(-1f, 1f);
			startPos.y = 0f;
			startPos.Normalize();
			startPos *= x * planeOffSetXMultiply;
			startPos.y = y;
			Vector3 endPos = startPos * -1f;
			endPos.y = startPos.y;
			startPos += dropToPos;
			endPos += dropToPos;
			float secondsToTake = Vector3.Distance(startPos, endPos) / (int)dropsettings["planeSpeed"];
			plane.gameObject.AddComponent<DropTiming>();
			plane.GetComponent<DropTiming>().GetSettings(new Dictionary<string,object>(dropsettings), startPos, endPos, secondsToTake);
			dropsettings.Clear();
			dropPlanedropped.SetValue(plane, true);
			plane.InitDropPosition(dropToPos);
			if(!CargoPlanes.Contains(plane))
			{
				if ((plane as BaseNetworkable).net == null)
					(plane as BaseNetworkable).net = Network.Net.sv.CreateNetworkable();
				CargoPlanes.Add(plane);
			}
			(plane as BaseNetworkable).limitNetworking = true;
			if((int)_creationFrame.GetValue(plane) == 0)
				plane.Spawn();	
			plane.transform.position = startPos;
			plane.transform.rotation = Quaternion.LookRotation(endPos - startPos);
			dropPlanestartPos.SetValue(plane, startPos);
			dropPlaneendPos.SetValue(plane, endPos);
			dropPlanesecondsToTake.SetValue(plane, secondsToTake);			
			(plane as BaseNetworkable).limitNetworking = false;
			if (showinfo)
				DropNotifier(dropToPos, dropType, staticList, notificationInfo);
		}

		void DropNotifier(Vector3 dropToPos, string dropType, string staticList, string notificationInfo)
		{
			if (dropType == "dropdirect")
				return;
			else if (dropType == "supplysignal")
			{
				if(notifyDropServerSignal)
					if(notifyDropGUI && SimpleUI_Enable)
						if (notifyDropServerSignalCoords)
							foreach(var player in BasePlayer.activePlayerList)
								MessageToPlayerUI(player, string.Format(lang.GetMessage("msgDropSignalCoords", this, player.UserIDString), dropToPos.x.ToString("0"), dropToPos.z.ToString("0")));
						else
							foreach(var player in BasePlayer.activePlayerList)
								MessageToPlayerUI(player, string.Format(lang.GetMessage("msgDropSignal", this, player.UserIDString)));
					else if(notifyDropGUI && GUIAnnouncements)
						if (notifyDropServerSignalCoords)
							MessageToAllGui(string.Format(lang.GetMessage("msgDropSignalCoords", this), dropToPos.x.ToString("0"), dropToPos.z.ToString("0")));
						else
							MessageToAllGui(lang.GetMessage("msgDropSignal", this));
					else
						if (notifyDropServerSignalCoords)
							foreach(var player in BasePlayer.activePlayerList)
								MessageToPlayer(player, string.Format(lang.GetMessage("msgDropSignalCoords", this, player.UserIDString), dropToPos.x.ToString("0"), dropToPos.z.ToString("0")));
						else
							foreach(var player in BasePlayer.activePlayerList)
								MessageToPlayer(player, lang.GetMessage("msgDropSignal", this, player.UserIDString));
				return;
			}
			else if(dropType == "regular")
			{
				if(notifyDropServerRegular)
					if(notifyDropGUI && SimpleUI_Enable)
						if (notifyDropServerRegularCoords)
							foreach(var player in BasePlayer.activePlayerList)
								MessageToPlayerUI(player, string.Format(lang.GetMessage("msgDropRegularCoords", this, player.UserIDString), dropToPos.x.ToString("0"), dropToPos.z.ToString("0")));
						else
							foreach(var player in BasePlayer.activePlayerList)
								MessageToPlayerUI(player, lang.GetMessage("msgDropRegular", this, player.UserIDString));
					else if(notifyDropGUI && GUIAnnouncements)
						if (notifyDropServerRegularCoords)
							MessageToAllGui(string.Format(lang.GetMessage("msgDropRegularCoords", this), dropToPos.x.ToString("0"), dropToPos.z.ToString("0")));
						else
							MessageToAllGui(lang.GetMessage("msgDropRegular", this));
					else
						if (notifyDropServerRegularCoords)
							foreach(var player in BasePlayer.activePlayerList)
								MessageToPlayer(player, string.Format(lang.GetMessage("msgDropRegularCoords", this, player.UserIDString), dropToPos.x.ToString("0"), dropToPos.z.ToString("0")));
						else
							foreach(var player in BasePlayer.activePlayerList)
								MessageToPlayer(player, lang.GetMessage("msgDropRegular", this, player.UserIDString));
				return;
			}
			else if(dropType == "massdrop")
			{
				if(notifyDropServerMass)
					if(notifyDropGUI && SimpleUI_Enable)
						foreach(var player in BasePlayer.activePlayerList)
							MessageToPlayerUI(player, lang.GetMessage("msgDropMass", this, player.UserIDString));
					else if(notifyDropGUI && GUIAnnouncements)
						MessageToAllGui(lang.GetMessage("msgDropMass", this));
					else
						foreach(var player in BasePlayer.activePlayerList)
							MessageToPlayer(player, lang.GetMessage("msgDropMass", this, player.UserIDString));
				return;
			}
			else
			{
				if(notifyDropServerCustom)
					if(notifyDropGUI && SimpleUI_Enable)
						if (notifyDropServerCustomCoords && _massDropTimer != null && _massDropTimer.Repetitions == 0)
							foreach(var player in BasePlayer.activePlayerList)
								MessageToPlayerUI(player, string.Format(lang.GetMessage("msgDropCustomCoords", this, player.UserIDString), notificationInfo, dropToPos.x.ToString("0"), dropToPos.z.ToString("0")));
						else
							foreach(var player in BasePlayer.activePlayerList)
								MessageToPlayerUI(player, string.Format(lang.GetMessage("msgDropCustom", this, player.UserIDString), notificationInfo));
					else if(notifyDropGUI && GUIAnnouncements)
						if (notifyDropServerCustomCoords && _massDropTimer != null && _massDropTimer.Repetitions == 0)
							MessageToAllGui(string.Format(lang.GetMessage("msgDropCustomCoords", this), notificationInfo, dropToPos.x.ToString("0"), dropToPos.z.ToString("0")));
						else
							MessageToAllGui(string.Format(lang.GetMessage("msgDropCustom", this), notificationInfo));
					else
						if (notifyDropServerCustomCoords && _massDropTimer != null && _massDropTimer.Repetitions == 0)
							foreach(var player in BasePlayer.activePlayerList)
								MessageToPlayer(player, string.Format(lang.GetMessage("msgDropCustomCoords", this, player.UserIDString), notificationInfo, dropToPos.x.ToString("0"), dropToPos.z.ToString("0")));
						else
							foreach(var player in BasePlayer.activePlayerList)
								MessageToPlayer(player, string.Format(lang.GetMessage("msgDropCustom", this, player.UserIDString), notificationInfo));
			}
		}

		#endregion ObjectsCreation

		#region ServerHooks

		void OnExplosiveThrown(BasePlayer player, BaseEntity entity)
		{
			if (!initialized || entity == null || !(entity is SupplySignal)) return;
			if (entity.net == null)
				entity.net = Network.Net.sv.CreateNetworkable();
			if (activeSignals.Contains(entity))
				return;
			activeSignals.Add(entity);
			SupplyThrown(player, entity);
		}

		void OnExplosiveDropped(BasePlayer player, BaseEntity entity)
		{
			if (!initialized || entity == null || !(entity is SupplySignal)) return;
			if (activeSignals.Contains(entity))
				return;
			activeSignals.Add(entity);
			SupplyThrown(player, entity);
		}

		void SupplyThrown(BasePlayer player, BaseEntity entity)
		{
			Vector3 playerposition = player.transform.position;
			timer.Once(3.0f, () => {
				if (entity == null)
				{
					activeSignals.Remove(entity);
					return;
				}
				InvokeHandler.CancelInvoke(entity.GetComponent<MonoBehaviour>(), new Action((entity as SupplySignal).Explode));
			});
			timer.Once(3.3f, () => {
				if (entity == null) return;
				activeSignals.Remove(entity);
				Vector3 position = new Vector3();
				if (!disableRandomSupplyPos)
					position = entity.transform.position + new Vector3(UnityEngine.Random.Range(-20f, 20f), 0f, UnityEngine.Random.Range(-20f, 20f));
				else
					position = entity.transform.position;
				InvokeHandler.Invoke(entity.GetComponent<MonoBehaviour>(), new Action((entity as SupplySignal).FinishUp), supplySignalSmokeTime);
				entity.SetFlag(BaseEntity.Flags.On, true, false);
				entity.SendNetworkUpdateImmediate(false);

				if (lockSignalDrop)
					startCargoPlane(position, false, null, "supplysignal", "", true, player.userID);
				else
					startCargoPlane(position, false, null, "supplysignal");

				if (notifyDropConsoleSignal)
					Puts($"SupplySignal thrown by '{player.displayName}' at: {playerposition}");

				if (notifyDropAdminSignal)
				{
					foreach(var admin in BasePlayer.activePlayerList.Where(p => p.IsAdmin).ToList())
					SendReply(admin, $"<color={colorAdmMsg}>"+ string.Format(lang.GetMessage("msgDropSignalAdmin", this, player.UserIDString), player.displayName, playerposition) + "</color>");
				}

				if (notifyDropSignalByPlayer)
					if (notifyDropSignalByPlayerCoords)
						PrintToChat(string.Format(Format,Color, Prefix) + $"<color={colorTextMsg}>" + string.Format(lang.GetMessage("msgDropSignalByPlayerCoords", this, player.UserIDString), player.displayName, position.x.ToString("0"), position.z.ToString("0")) +"</color>");
					else
						PrintToChat(string.Format(Format,Color, Prefix) + $"<color={colorTextMsg}>" + string.Format(lang.GetMessage("msgDropSignalByPlayer", this, player.UserIDString), player.displayName) +"</color>");
			});
		}

		void OnLootEntity(BasePlayer player, BaseEntity entity)
		{
			if (!initialized || entity == null || !(entity is SupplyDrop) || LootedDrops.Contains(entity as SupplyDrop)) return;
			if ((lockSignalDrop || lockDirectDrop) && entity.OwnerID != 0uL && entity.OwnerID != player.userID)
			{
				NextTick(() => player.EndLooting());
				MessageToPlayer(player, lang.GetMessage("msgCrateLocked", this, player.UserIDString));
				return;
			}
			if (entity.OwnerID == player.userID)
			{
				if (notifyDropConsoleLooted)
					Puts($"{player.displayName} ({player.UserIDString}) looted his Drop at: {entity.transform.position.ToString("0")}");
				if (!unlockDropAfterLoot)
				{
					entity.OwnerID = 0uL;
					LootedDrops.Add(entity as SupplyDrop);
				}
				return;
			}
			if (Vector3.Distance(lastLootPos, entity.transform.position) > ((lastDropRadius*2)*1.2))
			{
				bool direct = false;
				if (entity.GetComponent<ColliderCheck>() != null && entity.GetComponent<ColliderCheck>().cratesettings != null && Convert.ToString(entity.GetComponent<ColliderCheck>().cratesettings["droptype"]) == "dropdirect")
					direct = true;
				if (notifyDropServerLooted && !direct) NotifyOnDropLooted(entity,player);
				if (notifyDropConsoleLooted) Puts($"{player.displayName} ({player.UserIDString}) looted the Drop at: {entity.transform.position.ToString("0")}");
				LootedDrops.Add(entity as SupplyDrop);
				lastLootPos = entity.transform.position;
				return;
			}
		}

		void Init()
		{
			fd = this;
			initialized = false;
			LoadVariables();
			LoadDefaultMessages();
			msgConsoleDropSpawn = lang.GetMessage("msgConsoleDropSpawn", this);
			msgConsoleDropLanded = lang.GetMessage("msgConsoleDropLanded", this);

			bool saveNeeded = false;
			foreach ( var defaults in setupItemList)
			{
				Dictionary<string, object> check1 = defaults.Value as Dictionary<string, object>;
				if (!check1.ContainsKey("itemDivider"))
				{
					saveNeeded = true;
					check1.Add("itemDivider", 2);
				}
				else
					continue;
				Dictionary<string, object> newitems = new Dictionary<string, object>();
				Dictionary<string, object> check2 = check1["itemList"] as Dictionary<string, object>;
				foreach ( var items in check2)
				{
					if (!(items.Value as Dictionary<string, object>).ContainsKey("min")) continue;
					Dictionary<string, object> itemlist = items.Value as Dictionary<string, object>;
					if (itemlist.ContainsKey("min") && itemlist.ContainsKey("max"))
					{
						saveNeeded = true;
						newitems.Add(items.Key.ToString(), (int)itemlist["max"]);
					}
				}
				check1["itemList"] = newitems;
			}
			if (saveNeeded)
			{
				saveNeeded = false;
				Config["StaticItems", "DropTypes"] = setupItemList;
				Config.Save();
			}

			if(!setupDropDefault.ContainsKey("itemDivider"))
			{
				setupDropDefault.Add("itemDivider", 2);
				if (setupDropDefault.ContainsKey("randomAmountSingleItem"))
					setupDropDefault.Remove("randomAmountSingleItem");
				if (setupDropDefault.ContainsKey("randomAmountGroupedItem"))
					setupDropDefault.Remove("randomAmountGroupedItem");
				Config["DropSettings", "DropDefault"] = setupDropDefault;
				Config.Save();
			}

			foreach ( var defaults in setupDropTypes)
			{
				Dictionary<string, object> check = defaults.Value as Dictionary<string, object>;
				if(!check.ContainsKey("fireSignalRocket"))
				{
					check.Add("fireSignalRocket", false);
					saveNeeded = true;
				}
				if(!check.ContainsKey("itemDivider"))
				{
					check.Add("itemDivider", 2);
					if (check.ContainsKey("randomAmountSingleItem"))
						check.Remove("randomAmountSingleItem");
					if (check.ContainsKey("randomAmountGroupedItem"))
						check.Remove("randomAmountGroupedItem");
					saveNeeded = true;
				}
			}
			if (saveNeeded)
			{
				saveNeeded = false;
				Config["DropSettings", "DropTypes"] = setupDropTypes;
				Config.Save();
			}
			Interface.CallHook("OnFancyDropTypes", setupDropTypes);
		}

		object getFancyDropTypes()
		{
			if (setupDropTypes != null)
				return setupDropTypes;
			else
				return null;
		}

		void Unload()
		{
			airdropTimerStop();
			foreach (var obj in UnityEngine.Object.FindObjectsOfType<ColliderCheck>().ToList())
				GameObject.Destroy(obj);
			foreach (var obj in UnityEngine.Object.FindObjectsOfType<DropLight>().ToList())
				GameObject.Destroy(obj);
			foreach (var obj in UnityEngine.Object.FindObjectsOfType<DropTiming>().ToList())
				GameObject.Destroy(obj);
		}

		void OnServerInitialized()
		{
			Puts($"Map Highest Point: ({TerrainMeta.HighestPoint.y}m) | Plane flying height: (~{TerrainMeta.HighestPoint.y * planeOffSetYMultiply}m)");
			if(airdropTimerEnabled)
				Puts($"Timed Airdrop activated with '{airdropTimerMinPlayers}' players between '{airdropTimerWaitMinutesMin}' and '{airdropTimerWaitMinutesMax}' minutes");
			if((airdropCleanupAtStart && UnityEngine.Time.realtimeSinceStartup < 60) || BasePlayer.activePlayerList.Count == 1 ) airdropCleanUp();
			if (airdropRemoveInBuilt) removeBuiltInAirdrop();
			if (airdropTimerEnabled) airdropTimerNext();
			NextTick(() => SetupLoot());
			object value;
			var checkdefaults = defaultDrop();
			foreach( var pair in checkdefaults)
				if (!setupDropDefault.TryGetValue(pair.Key, out value))
					setupDropDefault.Add(pair.Key, checkdefaults[pair.Key]);
			initialized = true;
		}

		void OnTick()
		{
			if (useRealtimeTimers) OnTickReal();
			if (useGametimeTimers) OnTickServer();
		}

		void OnTickReal()
		{
			if (lastMinute == DateTime.UtcNow.Minute) return;
			lastMinute = DateTime.UtcNow.Minute;
			if (BasePlayer.activePlayerList.Count >= timersMinPlayers && realTimers.ContainsKey(DateTime.Now.ToString("HH:mm")))
			{
				string runCmd = (string)realTimers[DateTime.Now.ToString("HH:mm")];
				if (logTimersToConsole)
					Puts($"Run real timer: ({DateTime.Now.ToString("HH:mm")}) {runCmd}");
				ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), "ad." + runCmd);
			}
		}

		void OnTickServer()
		{
			if (lastHour == Math.Floor(TOD_Sky.Instance.Cycle.Hour)) return;
			lastHour = Math.Floor(TOD_Sky.Instance.Cycle.Hour);
			if (BasePlayer.activePlayerList.Count >= timersMinPlayers && serverTimers.ContainsKey(lastHour.ToString()))
			{
				string runCmd = (string)serverTimers[lastHour.ToString()];
				if (logTimersToConsole)
					Puts($"Run server timer: ({lastHour}) {runCmd}");
				ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), "ad." + runCmd);
			}
		}

        #endregion ServerHooks

        #region Plugin Hooks
        bool IsFancyDrop(CargoPlane plane) => plane == null ? false : plane.GetComponent<DropTiming>();

		object IsFancyDropType(CargoPlane plane)
		{
			if ( plane == null || !plane.GetComponent<DropTiming>()) return false;
			return (string)plane.GetComponent<DropTiming>().dropsettings["droptype"];
		}

		void OverrideDropTime(CargoPlane plane, float seconds)
        {
            var dropTiming = plane.GetComponent<DropTiming>();
            if (dropTiming != null)
            {
                dropTiming.TimeOverride(seconds);
            }
        }
        #endregion

        #region Commands

        [ConsoleCommand("ad.random")]
		void dropRandom(ConsoleSystem.Arg arg)
		{
			if(arg.Connection != null && arg.Connection.authLevel < neededAuthLvl) return;
			var plane = createCargoPlane();
			Vector3 newpos = plane.RandomDropPosition();
			newpos.y += TerrainMeta.HeightMap.GetHeight(newpos);
			string type = "regular";
			if(arg.Args != null && arg.Args.Length > 0)
				if(setupDropTypes.ContainsKey(arg.Args[0]))
					type = arg.Args[0];
				else
				{
					SendReply(arg, "Droptype not found");
					return;
				}
			string list = "";
			if(arg.Args != null && arg.Args.Length > 1)
				if(setupItemList.ContainsKey(arg.Args[1]))
					list = arg.Args[1];
				else
				{
					SendReply(arg, "Static itemlist not found");
					return;
				}
			startCargoPlane(newpos, false, plane, type, list);
			if (list == "")
				SendReply(arg, $"Random Airdrop of type '{type}' incoming at: {newpos.ToString("0")}");
			else
				SendReply(arg, $"Random Airdrop of type '{type}|{list}' incoming at: {newpos.ToString("0")}");
			if (airdropTimerEnabled && airdropTimerResetAfterRandom)
				airdropTimerNext();
		}

		[ConsoleCommand("ad.topos")]
		void dropToPos(ConsoleSystem.Arg arg)
		{
				if(arg.Connection != null && arg.Connection.authLevel < neededAuthLvl) return;
				if(arg.Args == null || arg.Args.Length < 2)
				{
					SendReply(arg, "Please specity location with X and Z coordinates as integer");
					return;
				}
				string type = "regular";
				if(arg.Args != null && arg.Args.Length > 2)
					if(setupDropTypes.ContainsKey(arg.Args[2]))
						type = arg.Args[2];
					else
					{
						SendReply(arg, "Droptype not found");
						return;
					}
				string list = "";
				if(arg.Args != null && arg.Args.Length > 3)
					if(setupItemList.ContainsKey(arg.Args[3]))
						list = arg.Args[3];
					else
					{
						SendReply(arg, "Static itemlist not found");
						return;
					}
				Vector3 newpos = new Vector3(Convert.ToInt32(arg.Args[0]), 0, Convert.ToInt32(arg.Args[1]));
				newpos.y += TerrainMeta.HeightMap.GetHeight(newpos);
				startCargoPlane(newpos, false, null, type, list);
				if (list == "")
					SendReply(arg, $"Airdrop of type '{type}' started to: {newpos.ToString("0")}");
				else
					SendReply(arg, $"Airdrop of type '{type}|{list}' started to: {newpos.ToString("0")}");
		}

		[ConsoleCommand("ad.massdrop")]
		void dropMass(ConsoleSystem.Arg arg)
		{
			int drops = 0;
			if(arg.Connection != null && arg.Connection.authLevel < neededAuthLvl) return;
			if(arg.Args == null)
				drops = airdropMassdropDefault;
			else if (arg.Args != null && arg.Args.Length >= 1)
			{
				int.TryParse(arg.Args[0], out drops);
				if (drops == 0)
				{
					SendReply(arg, "Massdrop value has to be an integer number");
					return;
				}
			}
			string type = "massdrop";
			if(arg.Args != null && arg.Args.Length > 1)
				if(setupDropTypes.ContainsKey(arg.Args[1]))
					type = arg.Args[1];
				else
				{
					SendReply(arg, string.Format("Droptype not found"));
					return;
				}
			string list = "";
			if(arg.Args != null && arg.Args.Length > 2)
				if(setupItemList.ContainsKey(arg.Args[2]))
					list = arg.Args[2];
				else
				{
					SendReply(arg, string.Format("Static itemlist not found"));
					return;
				}
			if (list == "")
				SendReply(arg, $"Massdrop started with {drops.ToString()} Drops of type '{type}'");
			else
				SendReply(arg, $"Massdrop started with {drops.ToString()} Drops of type '{type}|{list}'");
			if (_massDropTimer != null && !_massDropTimer.Destroyed)
				_massDropTimer.Destroy();
			bool showinfo = true;
			_massDropTimer = timer.Repeat(airdropMassdropDelay,drops+1, () => {
				if (_massDropTimer == null || _massDropTimer.Destroyed) return;
				startCargoPlane(Vector3.zero, true, null, type, list, showinfo);
				if (_massDropTimer.Repetitions == drops) showinfo = false;
				});
		}
		
		[ConsoleCommand("ad.massdropto")]
		void dropMassTo(ConsoleSystem.Arg arg)
		{
			int drops = airdropMassdropDefault;
			float x = -99999;
			float z = -99999;
			float radius = airdropMassdropRadiusDefault;
			if((arg.Connection != null && arg.Connection.authLevel < neededAuthLvl) || arg.Args == null) return;
			if(arg.Args.Length < 2)
			{
				SendReply(arg, "Specify at minumum (X) and (Z)");
				return;
			}
			if(arg.Args.Length >= 3) int.TryParse(arg.Args[2],out drops);

			if(!float.TryParse(arg.Args[0], out x) || !float.TryParse(arg.Args[1],out z))
			{
				SendReply(arg, "Specify at minumum (X) (Z) or with '0 0' for random position | and opt:drop-count and opt:radius )");
				return;
			}
			Vector3 newpos = new Vector3();
			if(x == 0 || z == 0)
			{
				newpos = referencePlane.RandomDropPosition();
				x = newpos.x;
				z = newpos.z;
			}
			if(arg.Args.Length > 3) float.TryParse(arg.Args[3],out radius);
			lastDropRadius = radius;
			string type = "massdrop";
			if(arg.Args != null && arg.Args.Length > 4)
				if(setupDropTypes.ContainsKey(arg.Args[4]))
					type = arg.Args[4];
				else
				{
					SendReply(arg, string.Format("Droptype not found"));
					return;
				}
			string list = "";
			if(arg.Args != null && arg.Args.Length > 5)
				if(setupItemList.ContainsKey(arg.Args[5]))
					list = arg.Args[5];
				else
				{
					SendReply(arg, string.Format("Static itemlist not found"));
					return;
				}
			if (list == "")
				SendReply(arg, string.Format($"Massdrop  of type '{type}' to (X:{x.ToString("0")} Z:{z.ToString("0")}) started with {drops.ToString()} Drops( {radius}m Radius)"));
			else
				SendReply(arg, string.Format($"Massdrop  of type '{type}|{list}' to (X:{x.ToString("0")} Z:{z.ToString("0")}) started with {drops.ToString()} Drops( {radius}m Radius)"));
			if (_massDropTimer != null && !_massDropTimer.Destroyed)
				_massDropTimer.Destroy();
			bool showinfo = true;
			_massDropTimer = timer.Repeat(airdropMassdropDelay,drops+1, () =>{
					if (_massDropTimer == null || _massDropTimer.Destroyed) return;
					newpos.x = UnityEngine.Random.Range(x - radius, x + radius);
					newpos.z = UnityEngine.Random.Range(z - radius, z + radius);
					//newpos.y -= TerrainMeta.HeightMap.GetHeight(newpos);
					startCargoPlane(newpos, false, null, type, list, showinfo);
					if (_massDropTimer.Repetitions == drops) showinfo = false;
					});
		}

		[ConsoleCommand("ad.toplayer")]
		void dropToPlayer(ConsoleSystem.Arg arg)
		{
			if(arg.Connection != null && arg.Connection.authLevel < neededAuthLvl) return;
			if (arg.Args == null)
			{
				SendReply(arg, string.Format("Please specify a target playername"));
				return;
			}
			if (arg.Args[0] == "*")
			{
				foreach( BasePlayer target in BasePlayer.activePlayerList)
				{
					if (target.IsAdmin) continue;
					NextTick(() => {
						var newpos = new Vector3();
						newpos = target.transform.position;
						startCargoPlane(newpos, false, null, "dropdirect");
						if (notifyDropPlayer)
							MessageToPlayer(target, lang.GetMessage("msgDropPlayer", this, target.UserIDString));
					});
				}
				SendReply(arg, string.Format($"Started Airdrop to each active player"));
			}
			else
			{
				BasePlayer target = FindPlayerByName(arg.Args[0]);
				if (target == null)
				{
					SendReply(arg, string.Format($"Player '{arg.Args[0]}' not found"));
					return;
				}
				var newpos = new Vector3();
				newpos = target.transform.position;
				startCargoPlane(newpos, false, null, "dropdirect");
				SendReply(arg, string.Format($"Starting Airdrop to Player '{target.displayName}' at: {newpos.ToString("0")}"));
				if (notifyDropPlayer)
					MessageToPlayer(target, lang.GetMessage("msgDropPlayer", this, target.UserIDString));
			}
		}

		[ConsoleCommand("ad.dropplayer")]
		void dropDropOnly(ConsoleSystem.Arg arg)
		{
			if(arg.Connection != null && arg.Connection.authLevel < neededAuthLvl) return;
			if (arg.Args == null)
			{
				SendReply(arg, string.Format("Please specify a target playername"));
				return;
			}
			BasePlayer target = FindPlayerByName(arg.Args[0]);
			if (target == null)
			{
				SendReply(arg, string.Format($"Player '{arg.Args[0]}' not found"));
				return;
			}
			var newpos = new Vector3();
			newpos = target.transform.position;
			newpos.y += 100;

			Dictionary<string,object> setting;
			if (setupDropTypes.ContainsKey("dropdirect"))
			{
				setting = new Dictionary<string,object>((Dictionary<string,object>)setupDropTypes["dropdirect"]);
				object value;
				foreach( var pair in setupDropDefault)
					if (!setting.TryGetValue(pair.Key, out value))
						setting.Add(pair.Key, setupDropDefault[pair.Key]);
			}
			else
				setting = new Dictionary<string,object>((Dictionary<string,object>)setupDropDefault);
			setting.Add("userID", target.userID);
			object isSupplyDropActive = Interface.Oxide.CallHook("isSupplyDropActive");
			setting.Add("betterloot", isSupplyDropActive != null && (bool)isSupplyDropActive ? true : false);
			setting["droptype"] = "dropdirect";
			createSupplyDrop(newpos, new Dictionary<string,object>(setting), false, false);
			setting.Clear();
			SendReply(arg, string.Format($"Direct Drop to Player '{target.displayName}' at: {target.transform.position.ToString("0")}"));
			if (notifyDropDirect)
				MessageToPlayer(target, lang.GetMessage("msgDropDirect", this, target.UserIDString));

		}

		[ConsoleCommand("ad.timer")]
		void dropReloadTimer(ConsoleSystem.Arg arg)
		{
			if(arg.Connection != null && arg.Connection.authLevel < neededAuthLvl) return;
				if(arg.Args != null && arg.Args.Length > 0)
				{
					try	{
						airdropTimerNext(Convert.ToInt32(arg.Args[0]));
						return;
						}
					catch { SendReply(arg, string.Format("Custom Timervalue has to be an integer number."));
							return;}
				}
				airdropTimerNext();
		}

		[ConsoleCommand("ad.cleanup")]
		void dropCleanUp(ConsoleSystem.Arg arg)
		{
			if(arg.Connection != null && arg.Connection.authLevel < neededAuthLvl) return;
			if (_massDropTimer != null && !_massDropTimer.Destroyed)
				_massDropTimer.Destroy();
			var planes = UnityEngine.Object.FindObjectsOfType<CargoPlane>().ToList();
			SendReply(arg, $"...killing {planes.Count} Planes");
			foreach(var plane in planes)
				plane.Kill();
			var drops = UnityEngine.Object.FindObjectsOfType<SupplyDrop>().ToList();
			SendReply(arg,$"...killing {drops.Count} SupplyDrops");
			foreach(var drop in drops)
				drop.Kill();
			CargoPlanes.Clear();
			SupplyDrops.Clear();
			LootedDrops.Clear();
			ItemManager.DoRemoves();
		}

		private void airdropCleanUp()
		{
			var drops = UnityEngine.Object.FindObjectsOfType<SupplyDrop>().ToList();
			Puts($"...killing {drops.Count} SupplyDrops");
			foreach(var drop in drops)
				drop.KillMessage();
		}

		[ConsoleCommand("ad.lootreload")]
		void dropLootReload(ConsoleSystem.Arg arg)
		{
			if (arg.Connection != null && arg.Connection.authLevel < neededAuthLvl) return;
			SendReply(arg, "Custom loot reloading...");
			SetupLoot();
		}

		[ChatCommand("droprandom")]
		void cdropRandom(BasePlayer player, string command)
		{
			if(player.net.connection.authLevel < neededAuthLvl)
			{
				SendReply(player, string.Format(Format, Color, Prefix) + "You are not allowed to use this command");
				return;
			}
			var plane = createCargoPlane();
			Vector3 newpos = plane.RandomDropPosition();
			newpos.y += TerrainMeta.HeightMap.GetHeight(newpos);
			startCargoPlane(newpos, false, plane, "regular");
			SendReply(player, $"<color={colorAdmMsg}>Random Airdrop incoming at: {newpos.ToString("0")}</color>");
			if (airdropTimerEnabled && airdropTimerResetAfterRandom)
				airdropTimerNext();
		}

		[ChatCommand("droptopos")]
		void cdropToPos(BasePlayer player, string command, string[] args)
		{
			if(player.net.connection.authLevel < neededAuthLvl)
			{
				SendReply(player, string.Format(Format, Color, Prefix) + "You are not allowed to use this command");
				return;
			}
			if(args == null || args.Length != 2)
			{
				SendReply(player, $"<color={colorAdmMsg}>Please specity location with X and Z coordinates only</color>");
				return;
			}
			Vector3 newpos = new Vector3(Convert.ToInt32(args[0]), 0, Convert.ToInt32(args[1]));
			newpos.y += TerrainMeta.HeightMap.GetHeight(newpos);
			startCargoPlane(newpos, false, null, "regular");
			if (notifyByChatAdminCalls)
				SendReply(player, $"<color={colorAdmMsg}>Airdrop called to position: {newpos.ToString("0")}</color>");
		}

		[ChatCommand("droptoplayer")]
		void cdropToPlayer(BasePlayer player, string command, string[] args)
		{
			if(player.net.connection.authLevel < neededAuthLvl)
			{
				SendReply(player, string.Format(Format, Color, Prefix) + lang.GetMessage("msgNoAccess", this));
				return;
			}
			if(args.Length < 1)
			{
				SendReply(player, $"<color={colorAdmMsg}>Please specify a target playername</color>");
				return;
			}
			if (args[0] == "*")
			{
				foreach( BasePlayer target in BasePlayer.activePlayerList)
				{
					if (target.IsAdmin) continue;
					NextTick(() => {
						var newpos = new Vector3();
						newpos = target.transform.position;
						startCargoPlane(newpos, false, null, "dropdirect");
						if (notifyDropPlayer)
							MessageToPlayer(target, lang.GetMessage("msgDropPlayer", this, target.UserIDString));
					});
				}
				if (notifyByChatAdminCalls)
					SendReply(player, $"<color={colorAdmMsg}>Started Airdrop to each active player</color>");
			}
			else
			{
				BasePlayer target = FindPlayerByName(args[0]);
				if (target == null)
				{
					SendReply(player, $"<color={colorAdmMsg}>Player '{args[0]}' not found</color>");
					return;
				}
				var newpos = new Vector3();
				newpos = target.transform.position;
				startCargoPlane(newpos, false, null, "dropdirect");
				if (notifyByChatAdminCalls)
					SendReply(player, $"<color={colorAdmMsg}>Airdrop called to player '{target.displayName}' at: {newpos.ToString("0")}</color>");
				if (notifyDropPlayer)
					MessageToPlayer(target, lang.GetMessage("msgDropPlayer", this, target.UserIDString));
			}
		}

		[ChatCommand("dropdirect")]
		 void cdropDirect(BasePlayer player, string command, string[] args)
		{
			if(player.net.connection.authLevel < neededAuthLvl)
			{
				SendReply(player, string.Format(Format, Color, Prefix) + lang.GetMessage("msgNoAccess", this));
				return;
			}
			if(args.Length < 1)
			{
				SendReply(player, $"<color={colorAdmMsg}>Please specify a target playername</color>");
				return;
			}
			BasePlayer target = FindPlayerByName(args[0]);
			if (target == null)
			{
				SendReply(player, $"<color={colorAdmMsg}>Player '{args[0]}' not found</color>");
				return;
			}
			var newpos = new Vector3();
			newpos = target.transform.position;
			newpos.y += 100;

			Dictionary<string,object> setting;
			if (setupDropTypes.ContainsKey("dropdirect"))
			{
				setting = new Dictionary<string,object>((Dictionary<string,object>)setupDropTypes["dropdirect"]);
				object value;
				foreach( var pair in setupDropDefault)
					if (!setting.TryGetValue(pair.Key, out value))
						setting.Add(pair.Key, setupDropDefault[pair.Key]);
			}
			else
				setting = new Dictionary<string,object>((Dictionary<string,object>)setupDropDefault);
			setting.Add("userID", target.userID);
			object isSupplyDropActive = Interface.Oxide.CallHook("isSupplyDropActive");
			setting.Add("betterloot", isSupplyDropActive != null && (bool)isSupplyDropActive ? true : false);
			setting["droptype"] = "dropdirect";
			createSupplyDrop(newpos, new Dictionary<string,object>(setting), false, false);
			setting.Clear();
			if (notifyByChatAdminCalls)
				SendReply(player, $"<color={colorAdmMsg}>Direct Drop to Player '{target.displayName}' at: {target.transform.position.ToString("0")}</color>");
			if (notifyDropDirect)
				MessageToPlayer(target, lang.GetMessage("msgDropDirect", this, target.UserIDString));
		}

		[ChatCommand("dropmass")]
		void cdropMass(BasePlayer player, string command, string[] args)
		{
			int drops = 0;
			if(player.net.connection.authLevel < neededAuthLvl)
			{
				SendReply(player, string.Format(Format, Color, Prefix) + lang.GetMessage("msgNoAccess", this));
				return;
			}
			if(args.Length < 1)
				drops = airdropMassdropDefault;
			else
				try{ drops = Convert.ToInt32(args[0]); }
				catch { SendReply(player, $"<color={colorAdmMsg}>Massdrop value has to be an integer number</color>");
						return;
						}
			if (notifyByChatAdminCalls)
				SendReply(player, $"<color={colorAdmMsg}>Massdrop started with {drops.ToString()} Drops</color>");

			if (_massDropTimer != null && !_massDropTimer.Destroyed)
				_massDropTimer.Destroy();

			bool showinfo = true;
			_massDropTimer = timer.Repeat(airdropMassdropDelay,drops+1, () => {
				if (_massDropTimer == null || _massDropTimer.Destroyed) return;
				startCargoPlane(Vector3.zero, true, null, "massdrop", "", showinfo);
				if (_massDropTimer.Repetitions == drops) showinfo = false;
				});
		}

		[ChatCommand("droptomass")]
		void cdropToMass(BasePlayer player, string command, string[] args)
		{
			int drops = airdropMassdropDefault;
			float x = -99999;
			float z = -99999;
			float radius = airdropMassdropRadiusDefault;
			if(player.net.connection.authLevel < neededAuthLvl)
			{
				SendReply(player, string.Format(Format, Color, Prefix) + lang.GetMessage("msgNoAccess", this));
				return;
			}
			if(args.Length < 2)
			{
				SendReply(player, $"<color={colorAdmMsg}>Specify at minumum (X) and (Z)</color>");
				return;
			}
			if(args.Length >= 3) int.TryParse(args[2],out drops);

			if(!float.TryParse(args[0], out x) || !float.TryParse(args[1],out z))
			{
				SendReply(player, $"<color={colorAdmMsg}>Specify at minumum (X) (Z) or with '0 0' for random position | and opt:drop-count and opt:radius )</color>");
				return;
			}
			Vector3 newpos = new Vector3();
			if(x == 0 || z == 0)
			{
				var plane = createCargoPlane();
				newpos = plane.RandomDropPosition();
				x = newpos.x;
				z = newpos.z;
				plane.Kill();
			}

			if(args.Length > 3) float.TryParse(args[3],out radius);
			lastDropRadius = radius;
			if (notifyByChatAdminCalls)
				SendReply(player, $"<color={colorAdmMsg}>Massdrop to (X:{x.ToString("0")} Z:{z.ToString("0")}) started with {drops.ToString()} Drops( {radius}m Radius)</color>");

			if (_massDropTimer != null && !_massDropTimer.Destroyed)
				_massDropTimer.Destroy();

			bool showinfo = true;
			_massDropTimer = timer.Repeat(airdropMassdropDelay,drops+1, () =>{
					if (_massDropTimer == null || _massDropTimer.Destroyed) return;
					newpos.x = UnityEngine.Random.Range(x - radius, x + radius);
					newpos.z = UnityEngine.Random.Range(z - radius, z + radius);
					newpos.y += TerrainMeta.HeightMap.GetHeight(newpos);
					startCargoPlane(newpos, false, null, "massdrop", "", showinfo);
					if (_massDropTimer.Repetitions == drops) showinfo = false;
					});
		}

		#endregion Commands

		#region airdropTimer

		void airdropTimerNext(int custom = 0)
		{
			if(airdropTimerEnabled)
			{
				int delay;
				airdropTimerStop();
				if (custom == 0)
					delay = UnityEngine.Random.Range(airdropTimerWaitMinutesMin ,airdropTimerWaitMinutesMax);
				else
					delay = custom;
				_aidropTimer = timer.Once(delay * 60, airdropTimerRun);
				if(notifyDropConsoleRegular)
					Puts($"Next timed Airdrop in {delay.ToString()} minutes");
			}
		}

		void airdropTimerRun()
		{
			var playerCount = BasePlayer.activePlayerList.Count;
			if (playerCount >= airdropTimerMinPlayers)
			{
				//var plane = createCargoPlane();
				//var newpos = new Vector3();
				//newpos = plane.RandomDropPosition();
				//startCargoPlane(newpos, false, plane, "massdrop");
				ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), "ad." + airdropTimerCmd.Replace("ad.",""));
				if(notifyDropConsoleRegular)
					Puts($"Timed Airdrop initiated with command '{"ad." + airdropTimerCmd.Replace("ad.","")}'");
			}
			else
			{
				if(notifyDropConsoleRegular)
					Puts("Timed Airdrop skipped, not enough Players");
			}
			airdropTimerNext();
		}

		void airdropTimerStop()
		{
			if (_aidropTimer == null || _aidropTimer.Destroyed)
				return;
			_aidropTimer.Destroy();
			_aidropTimer = null;
		}

		void removeBuiltInAirdrop()
		{
			var triggeredEvents = UnityEngine.Object.FindObjectsOfType<TriggeredEventPrefab>();
			var planePrefab = triggeredEvents.Where(e => e.targetPrefab != null && e.targetPrefab.guid.Equals("8429b072581d64747bfe17eab7852b42")).ToList();
			foreach (var prefab in planePrefab)
			{
				Puts("Builtin Airdrop removed");
				UnityEngine.Object.Destroy(prefab);
			}
		}

		#endregion airdropTimer

		#region FindPlayer

		static BasePlayer FindPlayerByName(string name)
		{
			BasePlayer result = null;
			foreach (BasePlayer current in BasePlayer.activePlayerList)
			{
				if (current.displayName.Equals(name, StringComparison.OrdinalIgnoreCase))
				{
					BasePlayer result2 = current;
					return result2;
				}
				if (current.UserIDString.Contains(name, CompareOptions.OrdinalIgnoreCase))
				{
					BasePlayer result2 = current;
					return result2;
				}
				if (current.displayName.Contains(name, CompareOptions.OrdinalIgnoreCase))
				{
					result = current;
				}
			}
			return result;
		}

		#endregion FindPlayer

		#region Messaging

		void MessageToAllGui(string message)
		{
			var msg = string.Format(Format, Color, Prefix) + message;
			rust.RunServerCommand(guiCommand+" "+msg.Quote());
		}

		void MessageToPlayerUI(BasePlayer player, string message)
		{
			UIMessage(player, string.Format(Format, Color, Prefix) + message);
		}

		void MessageToPlayer(BasePlayer player, string message)
		{
			PrintToChat(player, string.Format(Format,Color, Prefix) + $"<color={colorTextMsg}>"+ message +"</color>");
		}

		string GetDirectionAngle(float angle, string UserIDString)
		{
			if (angle > 337.5 || angle < 22.5)
				return lang.GetMessage("msgNorth", this, UserIDString);
			else if (angle > 22.5 && angle < 67.5)
				return lang.GetMessage("msgNorthEast", this, UserIDString);
			else if (angle > 67.5 && angle < 112.5)
				return lang.GetMessage("msgEast", this, UserIDString);
			else if (angle > 112.5 && angle < 157.5)
				return lang.GetMessage("msgSouthEast", this, UserIDString);
			else if (angle > 157.5 && angle < 202.5)
				return lang.GetMessage("msgSouth", this, UserIDString);
			else if (angle > 202.5 && angle < 247.5)
				return lang.GetMessage("msgSouthWest", this, UserIDString);
			else if (angle > 247.5 && angle < 292.5)
				return lang.GetMessage("msgWest", this, UserIDString);
			else if (angle > 292.5 && angle < 337.5)
				return lang.GetMessage("msgNorthWest", this, UserIDString);
			return "";
		}

		void NotifyOnDropLanded(BaseEntity drop)
		{
			foreach (var player in BasePlayer.activePlayerList.Where(p => Vector3.Distance(p.transform.position, drop.transform.position) < supplyDropNotifyDistance).ToList())
			{
				var msg = string.Format(lang.GetMessage("msgDropLanded", this, player.UserIDString), Vector3.Distance(player.transform.position, drop.transform.position), GetDirectionAngle(Quaternion.LookRotation((drop.transform.position - player.eyes.position).normalized).eulerAngles.y, player.UserIDString));
				MessageToPlayer(player, msg);
			}
		}

		void NotifyOnDropLooted(BaseEntity drop, BasePlayer looter)
		{
			foreach (var player in BasePlayer.activePlayerList.Where(p => Vector3.Distance(p.transform.position, drop.transform.position) < supplyLootNotifyDistance).ToList())
				if (notifyDropServerLootedCoords)
					MessageToPlayer(player, string.Format(lang.GetMessage("msgDropLootetCoords", this, player.UserIDString), looter.displayName, drop.transform.position.x.ToString("0"), drop.transform.position.z.ToString("0")  ));
				else
					MessageToPlayer(player, string.Format(lang.GetMessage("msgDropLootet", this, player.UserIDString), looter.displayName));
		}

		#endregion Messaging

		#region SetupLoot

		class ExportData
		{
			public string Name;
			public int NumberToSpawn;
			public float Probability;
			public Dictionary<int,Dictionary<string,Dictionary<string,int>>> Categories = new Dictionary<int,Dictionary<string,Dictionary<string,int>>>();
			public Dictionary<string,Dictionary<string,int>> Items = new Dictionary<string,Dictionary<string,int>>();
			[JsonIgnore]
			public Dictionary<string,Dictionary<ItemDefinition,int>> ItemDefs = new Dictionary<string,Dictionary<ItemDefinition,int>>();
		}

		void SpawnIntoContainer(ref int itemCount, ItemContainer container, int itemDivider, int level, ExportData expData, string category = "")
		{
			if (level == 1)
			{
				category = expData.Categories[level].First().Key.ToString();
			}
			if (expData.Categories.ContainsKey(level))
			{
				if (expData.Categories[level].ContainsKey(category) && expData.Categories[level][category].Count > 0)
				{
					SubCategoryIntoContainer(ref itemCount, container, itemDivider, level, expData, category);
					return;
				}
			}
			if (expData.ItemDefs.ContainsKey(category))
			{
				foreach(var itemdef in expData.ItemDefs[category])
				{
					if (itemCount >= container.capacity || container.IsFull())
					{
						return;
					}
					int count = 0;
					 count = UnityEngine.Random.Range(Mathf.CeilToInt((int)itemdef.Value / itemDivider), (int)itemdef.Value);
					if (count <= 0)
						count = itemdef.Value;
					Item item = ItemManager.Create(itemdef.Key, count, 0);
					if (item != null)
					{
						if(item.MoveToContainer(container, -1, true))
						{
							itemCount++;
						}
						else
							item.Remove(0f);
					}
				}
			}
		}

		
		void SubCategoryIntoContainer(ref int itemCount, ItemContainer container, int itemDivider, int level, ExportData expData, string category = "")
		{
			int num = 0;
			foreach (var cat in expData.Categories[level][category])
				num += cat.Value;
			int num2 = UnityEngine.Random.Range(0, num);
			foreach (var cat in expData.Categories[level][category])
			{
				if (!(expData.Categories[level][category][cat.Key] == 0))
				{
					num -= cat.Value;
					if (num2 >= num)
					{
						SpawnIntoContainer(ref itemCount, container, itemDivider, level+1, expData, cat.Key);
						return;
					}
				}
			}
		}

	   void SetupContainer(StorageContainer drop, Dictionary<string,object> setup)
	   {
			int slots = Mathf.RoundToInt(UnityEngine.Random.Range(Convert.ToSingle(setup["minItems"])*100f, Convert.ToSingle(setup["maxItems"])*100f) / 100f);
			bool ALCustom = false;
			if (setup.ContainsKey("AlCustom"))
			{
				slots = (setup["AlCustomList"] as List<Item>).Count;
				setup["betterloot"] = false;
				ALCustom = true;
			}
			if (slots > 36) slots = 36;
			if (!(bool)setup["betterloot"] || ALCustom)
			{
				drop.inventory?.Clear();
				ItemManager.DoRemoves();
				drop.inventory = new ItemContainer();
				drop.inventory.ServerInitialize(null, slots);
			}
			else
			{
				drop.inventory.capacity = slots;
				drop.inventorySlots = slots;				
			}
			int filled = 0;
			if ((bool)setup["includeStaticItemList"])
			{
				object value;
				if (setupItemList.TryGetValue(Convert.ToString(setup["includeStaticItemListName"]), out value))
				{
					Dictionary<string,object> prelist = (Dictionary<string,object>)value;
					int divider = 1;
					if (prelist.ContainsKey("itemDivider"))
						if ((int)prelist["itemDivider"] > 0)
							divider = (int)prelist["itemDivider"];
					object value2;

					if(prelist.TryGetValue("itemList", out value2))
					{
						Dictionary<string,object> itemlist = (Dictionary<string,object>)value2;
						System.Random r = new System.Random();
						var randomItemlist = itemlist.OrderBy(x => r.Next()).ToDictionary(item => item.Key, item => item.Value);
						for (int i = 0; i < randomItemlist.Count(); ++i)
						{
							Item slot = drop.inventory.GetSlot(i);
							if (slot != null)
							{
								drop.inventory.itemList.Remove(slot);
								slot.Remove();
							}
						}
						foreach ( var item in randomItemlist)
						{
							if (filled == slots) return;
							int amount;
							try { amount = UnityEngine.Random.Range(Mathf.CeilToInt((int)item.Value / divider), (int)item.Value); }
							catch { amount = 1; }
							var itemDef = ItemManager.FindItemDefinition(item.Key);
							if (itemDef == null) continue;
							if (amount == 0) amount++;
							drop.inventory.AddItem(itemDef, amount);
							filled++;
						}
					}
					if ((bool)setup["includeStaticItemListOnly"])
					{
						drop.panelName = "generic";
						drop.inventory.capacity = drop.inventory.itemList.Count;
						drop.inventory.MarkDirty();
						return;
					}
				}
			}

			if (ALCustom)
			{
				(drop as LootContainer).initialLootSpawn = false;
				foreach (var custom in (setup["AlCustomList"] as List<Item>))
				{
					if (!drop.inventory.IsFull())
						custom.MoveToContainer(drop.inventory, -1, false);
					else
						custom.Remove();
				}
				drop.panelName = "generic";
				drop.inventory.capacity = drop.inventory.itemList.Count;
				drop.inventory.MarkDirty();
				ItemManager.DoRemoves();
				return;
			}
			
			if(!(bool)setup["betterloot"] && (bool)setup["useCustomLootTable"] && (dropLoot.First().ItemDefs.Count > 0 || ALCustom))
			{
				int itemCount = 0;
				for (int i = 0; i < dropLoot.Count; i++)
				{
					var dLoot = dropLoot[i];
					for (int j = 0; j < dLoot.NumberToSpawn; j++)
					{
						float num = UnityEngine.Random.Range(0f, 1f);
						if (num <= dLoot.Probability)
						{
							SpawnIntoContainer(ref itemCount, drop.inventory, (int)setup["itemDivider"], 1, dLoot);
							if (drop.inventory.IsFull() || itemCount >= slots)
								break;
						}
					}
					if (drop.inventory.IsFull() || itemCount >= slots)
						break;
				}
				if (!drop.inventory.IsFull() && itemCount < slots)
				{
					do
					{
						SpawnIntoContainer(ref itemCount, drop.inventory, (int)setup["itemDivider"], 1, dropLoot.Last());
						if (itemCount >= slots || drop.inventory.IsFull())
							break;
					} while(true);
				}
				drop.panelName = "generic";
				drop.inventory.capacity = drop.inventory.itemList.Count;
				drop.inventory.MarkDirty();
			}
			if(!(bool)setup["betterloot"] && !(bool)setup["useCustomLootTable"])
				drop.GetComponent<LootContainer>().PopulateLoot();
			drop.panelName = "generic";
			drop.inventory.capacity = drop.inventory.itemList.Count;
			drop.inventory.MarkDirty();
	   }

		List<ExportData> dropLoot = null;
		
		void SetupLoot()
		{
			dropLoot = Interface.GetMod().DataFileSystem.ReadObject<List<ExportData>>(this.Title) ?? new List<ExportData>();
			
			if (dropLoot == null || dropLoot.Count == 0)
			{
				var loot = GameManager.server.FindPrefab("assets/prefabs/misc/supply drop/supply_drop.prefab").GetComponent<LootContainer>();
				for (int i = 0; i < loot.LootSpawnSlots.Length; i++)
				{
					var lootSlot = loot.LootSpawnSlots[i];
					var exportData = new ExportData();
					exportData.Name = lootSlot.definition.name.ToString();
					exportData.NumberToSpawn = lootSlot.numberToSpawn;
					exportData.Probability = lootSlot.probability;			
					ExportLootSpawn(exportData, lootSlot.definition, 1);
					dropLoot.Add(exportData);
				}
				Interface.GetMod().DataFileSystem.WriteObject(this.Title, dropLoot);
			}
			
			int items = 0;
			foreach (var dLoot in dropLoot.ToList())
			{
				if (dLoot.Items.Count > 0)
				{
					foreach (var amount in dLoot.Items)
					{
						foreach(var item in amount.Value)
						{
							var def = ItemManager.FindItemDefinition(item.Key);
							if (def == null)
								continue;
							var chk = ItemManager.Create(def);
							if (chk == null)
							{
								try { chk.Remove(); } catch {}
								continue;
							}
							if(!dLoot.ItemDefs.ContainsKey(amount.Key))
								dLoot.ItemDefs.Add(amount.Key, new Dictionary<ItemDefinition,int>() );
							dLoot.ItemDefs[amount.Key].Add(def , item.Value);
							items++;
							chk.Remove();
						}
					}
				}
			}
			ItemManager.DoRemoves();
			Puts($"'{dropLoot.Count}' custom loot tables loaded with overall '{items}' items");
		}
		
		void ExportLootSpawn(ExportData expData, LootSpawn lootSpawn, int level)
		{
			if (lootSpawn.subSpawn != null && lootSpawn.subSpawn.Length > 0)
			{
				if(!expData.Categories.ContainsKey(level))
					expData.Categories.Add(level, new Dictionary<string,Dictionary<string,int>>());
				foreach (var entry in lootSpawn.subSpawn)
				{
					string cat = entry.category.ToString().Replace(" (LootSpawn)", "");
					if(!expData.Categories[level].ContainsKey(lootSpawn.name))
						expData.Categories[level].Add(lootSpawn.name, new Dictionary<string,int>());
					expData.Categories[level][lootSpawn.name].Add(cat , entry.weight);
					ExportLootSpawn(expData, entry.category, level+1);
				}
				return;
			}
			if (lootSpawn.items != null && lootSpawn.items.Length > 0)
			{
				foreach (var amount in lootSpawn.items)
				{
					if(!expData.Items.ContainsKey(lootSpawn.name))
						expData.Items.Add(lootSpawn.name, new Dictionary<string,int>() );
					expData.Items[lootSpawn.name].Add(amount.itemDef.shortname , (int)amount.amount);
				}
			}
		}
		
		
		#endregion SetupLoot

		#region SimpleUI

		class UIColor
		{
			string color;

			public UIColor(double red, double green, double blue, double alpha)
			{
				color = $"{red} {green} {blue} {alpha}";
			}

			public override string ToString() => color;
		}

		class UIObject
		{
			List<object> ui = new List<object>();
			List<string> objectList = new List<string>();

			public UIObject()
			{
			}

			public void Draw(BasePlayer player)
			{
				CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo() { connection = player.net.connection }, null, "AddUI", JsonConvert.SerializeObject(ui).Replace("{NEWLINE}", Environment.NewLine));
			}

			public void Destroy(BasePlayer player)
			{
				foreach (string uiName in objectList)
					CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo() { connection = player.net.connection }, null, "DestroyUI", uiName);
			}

			public string AddText(string name, double left, double top, double width, double height, string color, string text, int textsize = 15, string parent = "Hud", int alignmode = 0, float fadeIn = 0f, float fadeOut = 0f)
			{
				//name = name + RandomString();
				text = text.Replace("\n", "{NEWLINE}");
				string align = "";

				switch (alignmode)
				{
					case 0: { align = "LowerCenter"; break; };
					case 1: { align = "LowerLeft"; break; };
					case 2: { align = "LowerRight"; break; };
					case 3: { align = "MiddleCenter"; break; };
					case 4: { align = "MiddleLeft"; break; };
					case 5: { align = "MiddleRight"; break; };
					case 6: { align = "UpperCenter"; break; };
					case 7: { align = "UpperLeft"; break; };
					case 8: { align = "UpperRight"; break; };
				}

				ui.Add(new Dictionary<string, object> {
					{"name", name},
					{"parent", parent},
					{"fadeOut", fadeOut.ToString()},
					{"components",
						new List<object> {
							new Dictionary<string, string> {
								{"type", "UnityEngine.UI.Text"},
								{"text", text},
								{"fontSize", textsize.ToString()},
								{"color", color.ToString()},
								{"align", align},
								{"fadeIn", fadeIn.ToString()}
							},
							new Dictionary<string, string> {
								{"type", "RectTransform"},
								{"anchormin", $"{left} {((1 - top) - height)}"},
								{"anchormax", $"{(left + width)} {(1 - top)}"}
							}
						}
					}
				});

				objectList.Add(name);
				return name;
			}
		}

		void UIMessage(BasePlayer player, string message)
		{
			bool replaced = false;
			float fadeIn = 0.2f;
			Timer playerTimer;

			timers.TryGetValue(player, out playerTimer);
			if (playerTimer != null && !playerTimer.Destroyed)
			{
				playerTimer.Destroy();
				fadeIn = 0.1f;
				replaced = true;
			}

			UIObject ui = new UIObject();

			ui.AddText("Notice_DropShadow", SimpleUI_Left + 0.002, SimpleUI_Top + 0.002, SimpleUI_MaxWidth, SimpleUI_MaxHeight, SimpleUI_ShadowColor, StripTags(message), SimpleUI_FontSize, "Hud", 3, fadeIn, 0.2f);
			ui.AddText("Notice", SimpleUI_Left, SimpleUI_Top, SimpleUI_MaxWidth, SimpleUI_MaxHeight, SimpleUI_NoticeColor, message, SimpleUI_FontSize, "Hud", 3, fadeIn, 0.2f);

			ui.Destroy(player);

			if(replaced)
			{
				timer.Once(0.1f, () =>
				{
					ui.Draw(player);
					timers[player] = timer.Once(SimpleUI_HideTimer, () => ui.Destroy(player));
				});
			}
			else
			{
				ui.Draw(player);
				timers[player] = timer.Once(SimpleUI_HideTimer, () => ui.Destroy(player));
			}
		}

		string StripTags(string original)
		{
			foreach (string tag in tags)
				original = original.Replace(tag, "");

			foreach (Regex regexTag in regexTags)
				original = regexTag.Replace(original, "");

			return original;
		}

		#endregion SimpleUI

	}

}