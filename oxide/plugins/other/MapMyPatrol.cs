using System.Collections.Generic;	//dict
using Oxide.Game.Rust.Cui;
using UnityEngine;	//Vector3
using Oxide.Core.Plugins;
using Convert = System.Convert;

namespace Oxide.Plugins
{
	[Info("Map My Patrol", "BuzZ[PHOQUE]", "0.0.4")]
	[Description("Adds red marker(s) on ingame map for Heli Patrol(s) position, HUD with details, and refresh on timer.")]

/*======================================================================================================================= 
*
*   
*   20th august 2018
* 
*	0.0.4	configfile with sound/banner/bannercolor/HUDcolor/HUDfontsize
*
*	THANKS TO THE OXIDE/UMOD TEAM for coding quality, ideas, and time spent for the community
*	https://umod.org/plugins/image-library to get little animation on HUD
*
*=======================================================================================================================*/


	public class MapMyPatrol : RustPlugin
	{
        [PluginReference]
        Plugin ImageLibrary;

		bool debug = false;
		int anim;
		string icondapatrol;
        private bool ConfigChanged;
        //const string MapMyPatrolHUD = "mapmypatrol.hud"; 
		bool sound = false;
		bool bannerdisplay = false;
		string bannercolor = "0.5 0.5 0.5 0.30";
		string HUDcolor = "0.5 0.5 0.5 0.30";
		int HUDfontsize = 10;
		
        public Dictionary<BaseHelicopter, MapMarkerGenericRadius> baseradius = new Dictionary<BaseHelicopter, MapMarkerGenericRadius>();
        public Dictionary<BaseHelicopter, Vector3> baseposition = new Dictionary<BaseHelicopter, Vector3>();
        public Dictionary<BaseHelicopter, string> basemessage = new Dictionary<BaseHelicopter, string>();
        public Dictionary<BaseHelicopter, PatrolHelicopterAI> baseai = new Dictionary<BaseHelicopter, PatrolHelicopterAI>();
        public Dictionary<BaseHelicopter, BaseCombatEntity> basecombat = new Dictionary<BaseHelicopter, BaseCombatEntity>();
        public Dictionary<BaseHelicopter, BaseEntity> baseent = new Dictionary<BaseHelicopter, BaseEntity>();
		
		private Timer patrolrefresh;

/*	FOR FUTURE VERSIONS
https://image.ibb.co/eCiHqK/Map_My_Patrol_icone_HUD_blue.png
https://image.ibb.co/i2RsPe/Map_My_Patrol_icone_HUD_fullgreen.png
https://image.ibb.co/dJYTxz/Map_My_Patrol_icone_HUD_fullred.png
https://image.ibb.co/npaK4e/Map_My_Patrol_icone_HUD_green.png
https://image.ibb.co/mJVmje/Map_My_Patrol_icone_HUD_purple.png
https://image.ibb.co/bYrsPe/Map_My_Patrol_icone_HUD_red.png
*/
        void Init()
        {
            LoadVariables();
            //permission.RegisterPermission(MapMyPatrolHUD, this);
        }

        void OnServerInitialized()
        {
			ulong imageId = 666999666999;
			if (ImageLibrary == true)
			{
				bool exists = (bool)ImageLibrary?.Call ("HasImage", "mapmypatrolblue", imageId);
				if (exists == false)
				{
//					ImageLibrary?.Call ("AddImage","https://upload.wikimedia.org/wikipedia/commons/thumb/7/7c/Maki-heliport-15.svg/240px-Maki-heliport-15.svg.png", "mapmypatrolblack", imageId);
					ImageLibrary?.Call ("AddImage","https://image.ibb.co/eCiHqK/Map_My_Patrol_icone_HUD_blue.png", "mapmypatrolblue", imageId);

					if (debug == true) {Puts($"ADDING ICON TO ImageLibrary");}

				}
				if (debug == true) {Puts($"LOADING ICON from ImageLibrary");}
				icondapatrol = (string)ImageLibrary?.Call ("GetImage","mapmypatrolblue", imageId);
			}

			if (ImageLibrary == false)
			{
				PrintWarning($"You are missing plugin reference : ImageLibrary");
			}
		}

		void Unload()
		{
			
			foreach (var paire in baseradius)
			{

				paire.Value.Kill();
            	paire.Value.SendUpdate();
				if (debug == true) {Puts($"PATROL MAPMARKER KILLED");}
				/*baseradius.Clear();
				basemessage.Clear();
				baseposition.Clear();
				baseai.Clear();
				basecombat.Clear();
				baseent.Clear();*/

			}
			
			if (patrolrefresh != null){patrolrefresh.Destroy();}
		}


#region MESSAGES

    void LoadDefaultMessages()
    {

        lang.RegisterMessages(new Dictionary<string, string>
        {
            {"SpawnMsg", "A Patrol has spawned and will fly above Island in a few. Check your MAP (G) for a red marker."},
            {"KilledMsg", "A Patrol has gone away from Island."},

			
        }, this, "en");

        lang.RegisterMessages(new Dictionary<string, string>
        {
            {"SpawnMsg", "Une patrouille est en route pour l'île. Un marqueur rouge est rajouté sur la carte (G)"},
            {"KilledMsg", "Une patrouille a quitté l'île."},
        }, this, "fr");
    }


#endregion


#region CONFIG

    protected override void LoadDefaultConfig()
        {
            LoadVariables();
        }

        private void LoadVariables()
        {
            /*Prefix = Convert.ToString(GetConfig("Chat Settings", "Prefix", "[My CH47] "));                       // CHAT PLUGIN PREFIX
            PrefixColor = Convert.ToString(GetConfig("Chat Settings", "PrefixColor", "#149800"));                // CHAT PLUGIN PREFIX COLOR
            ChatColor = Convert.ToString(GetConfig("Chat Settings", "ChatColor", "#bbffb1"));                    // CHAT MESSAGE COLOR
            SteamIDIcon = Convert.ToUInt64(GetConfig("Chat Settings", "SteamIDIcon", "76561198332562475"));*/       
            sound = Convert.ToBoolean(GetConfig("Sound Settings", "play soundfx", "false"));
            bannerdisplay = Convert.ToBoolean(GetConfig("Banner Settings", "display banner", "false"));
            HUDfontsize = Convert.ToInt32(GetConfig("HUD Settings", "font size", "10"));
            HUDcolor = Convert.ToString(GetConfig("HUD Settings", "color on 3 firsts numbers - opacity on last number", "0.5 0.5 0.5 0.30"));      
            bannercolor = Convert.ToString(GetConfig("Banner Settings", "color on 3 firsts numbers - opacity on last number", "0.5 0.5 0.5 0.30"));      

            if (!ConfigChanged) return;
            SaveConfig();
            ConfigChanged = false;
        }

        private object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                ConfigChanged = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                ConfigChanged = true;
            }
            return value;
        }

#endregion


        void MarkerDisplayingDelete(BaseHelicopter delpatrol)
        {
			if (baseradius.ContainsKey(delpatrol) == true)
			{
				MapMarkerGenericRadius delmarker;
				baseradius.TryGetValue(delpatrol, out delmarker);
				delmarker.Kill();
            	delmarker.SendUpdate();
				if (debug == true) {Puts($"PATROL MAPMARKER KILLED");}
			}
		}

        void GenerateMarkers()
		{
			if (baseradius != null)
			{
				foreach (var paire in baseradius)
				{
					MapMarkerGenericRadius MapMarkerDel = paire.Value;
						if (MapMarkerDel != null)
						{
							MapMarkerDel.Kill();
							MapMarkerDel.SendUpdate();
						}
				}
			}


			foreach (var nelico in baseposition)
			{

				Vector3 position = nelico.Value;
				MapMarkerGenericRadius MapMarker = GameManager.server.CreateEntity("assets/prefabs/tools/map/genericradiusmarker.prefab", position) as MapMarkerGenericRadius;
				MapMarker.alpha = 0.6f;
				MapMarker.color1 = Color.red;
				MapMarker.color2 = Color.black;
				MapMarker.radius = 8;

				baseradius.Remove(nelico.Key);
				baseradius.Add(nelico.Key, MapMarker);

				if (debug == true) {Puts($"PATROL MARKER SPAWNED ON MAP");}
			}

			foreach (var paire in baseradius)
			{
				paire.Value.Spawn();
            	paire.Value.SendUpdate();
			}
		}

#region HUD

		private void PatrolHUD()
		{

			int patrolnum = basemessage.Count;
			anim = anim + 1;
			double colonnegauche = 0.00;
			colonnegauche = colonnegauche + (anim * 0.16);

			if (colonnegauche > 0.89)
			{
				anim = 0;
				colonnegauche = 0.00;
			}

			double colonnedroite = colonnegauche + 0.15;
			double lignehaut = 0.80;
			double lignebas = lignehaut - 0.10;

            int Round = 1;
            int round = -1;

			List<string> messagelist = new List<string>();
			List<BaseHelicopter> helilist = new List<BaseHelicopter>();
			List<PatrolHelicopterAI> ailist = new List<PatrolHelicopterAI>();


			string[] messagearray;			
			BaseHelicopter[] heliarray;
			PatrolHelicopterAI[] aiarray;


			foreach (var paire in basemessage)
			{
				messagelist.Add(paire.Value);
				helilist.Add(paire.Key);
				if (baseai.ContainsKey(paire.Key) == true)
				{
					PatrolHelicopterAI ai;
					baseai.TryGetValue(paire.Key, out ai);
					ailist.Add(ai);

				}

			}
			messagearray = messagelist.ToArray();
			heliarray = helilist.ToArray();
			aiarray = ailist.ToArray();
			
			if (debug == true) {Puts($"BaseMessage COUNT : {messagelist.Count}");}

            for (Round = 1; Round <= patrolnum ; Round++)     
            	{
                    round = round + 1;
					double lignedecalage = 0.11 * round;
					var CuiElement = new CuiElementContainer();
					var PatrolHUDBanner = CuiElement.Add(new CuiPanel{Image ={Color = HUDcolor},RectTransform ={AnchorMin = $"0.0 {lignebas - lignedecalage}",AnchorMax = $"0.10 {lignehaut - lignedecalage}"},CursorEnabled = false});
					/*var closeButton = new CuiButton{Button ={Close = PatrolHUDBanner,Color = "0.0 0.0 0.0 0.6"},RectTransform ={AnchorMin = "0.90 0.00",AnchorMax = "0.99 1.00"},Text ={Text = "X",FontSize = 20,Align = TextAnchor.MiddleCenter}};
					CuiElement.Add(closeButton, PatrolHUDBanner);       */       		

					foreach (var player in BasePlayer.activePlayerList)
					{

						Vector3 basepos;
						baseposition.TryGetValue(heliarray[round], out basepos);
						int dist = (int)Vector3.Distance(player.transform.position, basepos);

						string finalmessage = $": <color=orange>{dist}m.</color> away.{messagearray[round]}";

						PatrolHelicopterAI myAI = aiarray[round];

						if (myAI.PlayerVisible(player) == true)
							{
								if (debug == true) {Puts($"PLAYER {player.displayName} IS VISIBLE !");}
								finalmessage = $"{finalmessage}\n<color=red>YOU ARE VISIBLE</color>";
							}


						CuiElement.Add(new CuiLabel{Text ={Text = $"<color=white>#{round+1}</color> {finalmessage}",FontSize = HUDfontsize,Align = TextAnchor.MiddleLeft,Color = "0.0 0.0 0.0 1"},RectTransform ={AnchorMin = "0.10 0.10",   AnchorMax = "0.90 0.79"}
						}, PatrolHUDBanner);

						if (ImageLibrary == true)
						{
							CuiElement.Add(new CuiElement
								{
									Name = CuiHelper.GetGuid(),
									Parent = PatrolHUDBanner,
									Components =
										{
											new CuiRawImageComponent {Png = icondapatrol},
											new CuiRectTransformComponent {AnchorMin = $"{colonnegauche} 0.80", AnchorMax = $"{colonnedroite} 0.99"}
										}
								});
						}

						CuiHelper.AddUi(player, CuiElement);

						timer.Once(5, () =>
						{
							CuiHelper.DestroyUi(player, PatrolHUDBanner);
						});

					}
				}
		}

#endregion

#region SPAWN DETECTION

        private void OnEntitySpawned(BaseEntity Entity)
        {

            if (Entity == null) return;
            if (Entity is BaseHelicopter)
            {

				anim = 0;
				BaseHelicopter helicopter = Entity as BaseHelicopter;  
				baseent.Add(helicopter, Entity);

				var position = Entity.ServerPosition;
					baseposition.Remove(helicopter);
					baseposition.Add(helicopter, position);

				PatrolHelicopterAI AI = Entity.GetComponent<PatrolHelicopterAI>();
				baseai.Add(helicopter, AI);

				BaseCombatEntity pat = Entity as BaseCombatEntity;
				basecombat.Add(helicopter, pat);

				//GenerateMarkers(helicopter);
				DisplayBannerToAll("spawn");

				if (debug == true) {Puts($"BEFORE TIMER - IN BasePosition : {baseposition.Count}");}	// bonus info

					AfterSpawn();
				
		    }
		}
#endregion


		void AfterSpawn()
		{

			if (patrolrefresh == null)
			{
				if (debug == true) {Puts($"TIMER IS NULL");}

				patrolrefresh = timer.Repeat(5, 0, () =>
				{
					AfterSpawn();
				});

			}

			if (debug == true) {Puts($"VOID AFTERSPAWN");}	// bonus info
			


			foreach (var paire in baseent)
			{
				if (debug == true) {Puts($"AI COUNT : {baseent.Count}");}	// bonus info

				BaseEntity Entity2;
				Entity2 = paire.Value;

				BaseHelicopter helicopter = paire.Key;	

				PatrolHelicopterAI myAI;
				baseai.TryGetValue(helicopter, out myAI);
				
				BaseCombatEntity patrol;
				basecombat.TryGetValue(helicopter, out patrol);

				var position2 = Entity2.ServerPosition;
				if (debug == true) {Puts($"ENTITY NEW POSITION : {position2} - SPEED {myAI.moveSpeed}");}

				baseposition.Remove(helicopter);
				baseposition.Add(helicopter, position2);

				if (debug == true) {Puts($"Left Rockets : {myAI.numRocketsLeft}");}
				if (debug == true) {Puts($"DESTINATION : {myAI.destination}");}
				if (debug == true) {Puts($"SPAWNTIME : {myAI.spawnTime}");}

				string names = "";
				string message = "";

				foreach (BasePlayer player in BasePlayer.activePlayerList)
				{
					List<int> distlist = new List<int>();
					message = $"\nSpeed : <size=11><color=cyan>{myAI.moveSpeed}</color></size>\nLeft Rockets : <size=11><color=cyan>{myAI.numRocketsLeft}</color></size>";

					basemessage.Remove(helicopter);
					basemessage.Add(helicopter, message);
				}

			}				
					GenerateMarkers();
					PatrolHUD();

		}

#region ON PATROL KILL

		void OnEntityKill(BaseCombatEntity Entity, HitInfo info)
		{
			if (Entity == null) return;
			if (Entity is BaseHelicopter)
			{	
				BaseHelicopter helicopterkilled = Entity as BaseHelicopter;  

				if (baseent.ContainsKey(helicopterkilled) == true)
					{

						DisplayBannerToAll("killed");
						MarkerDisplayingDelete(helicopterkilled);

						baseradius.Remove(helicopterkilled);
						basemessage.Remove(helicopterkilled);
						baseposition.Remove(helicopterkilled);
						baseai.Remove(helicopterkilled);
						basecombat.Remove(helicopterkilled);
						baseent.Remove(helicopterkilled);
						
						if (baseent.Count == 0)
						{
							patrolrefresh.Destroy();
							patrolrefresh = null;
							if (debug == true) {Puts($"PATROL IS NO MORE. TIMER DESTROYED");}


							baseradius.Clear();
							basemessage.Clear();
							baseposition.Clear();
							baseai.Clear();
							basecombat.Clear();
							baseent.Clear();


						}
					}
			}
		}

#endregion

#region BANNER

		void DisplayBannerToAll(string reason)
		{

			foreach (var player in BasePlayer.activePlayerList)
			{

				if (bannerdisplay == true)
				{
					string message = string.Empty;
					if (reason == "spawn")
					{
						message = $"{lang.GetMessage("SpawnMsg", this, player.UserIDString)}";
					}
					if (reason == "killed")
					{
						message = $"{lang.GetMessage("KilledMsg", this, player.UserIDString)}";
					}
					var CuiElement = new CuiElementContainer();
					var PatrolBanner = CuiElement.Add(new CuiPanel{Image ={Color = bannercolor},RectTransform ={AnchorMin = "0.20 0.85",AnchorMax = "0.80 0.90"},CursorEnabled = false});                   
					var closeButton = new CuiButton{
						Button ={Close = PatrolBanner,Color = "0.0 0.0 0.0 0.6"},RectTransform ={AnchorMin = "0.90 0.01",AnchorMax = "0.99 0.99"},Text ={Text = "X",FontSize = 20,Align = TextAnchor.MiddleCenter}};
					CuiElement.Add(closeButton, PatrolBanner);         
					CuiElement.Add(new CuiLabel{
						Text ={Text = $"{message}",FontSize = 20,Align = TextAnchor.MiddleCenter,Color = "1.0 1.0 1.0 1"},RectTransform ={AnchorMin = "0.10 0.10",   AnchorMax = "0.90 0.90"}
						}, PatrolBanner);

					CuiHelper.AddUi(player, CuiElement);

					timer.Once(12, () =>
						{
							CuiHelper.DestroyUi(player, PatrolBanner);
						});
					if (debug == true) {Puts($"BANNER DISPLAYING FOR PLAYER {player.displayName}");}

				}

				if (sound == true)
				{
					var sound = new Effect("assets/bundled/prefabs/fx/player/howl.prefab", player, 0, Vector3.zero, Vector3.forward);
					EffectNetwork.Send(sound, player.net.connection);
					if (debug == true) {Puts($"PLAYING SOUND FOR PLAYER {player.displayName}");}

				}

		


			}
		}

#endregion

	}
}


/*

FOR FUTURE VERSIONS
change icon color on behaviour (red on attack/blue on visible/etc.)

	public float lastSeenTime = float.PositiveInfinity;
	public float visibleFor;
	public Vector3 destination;
	public float moveSpeed;
	public float throttleSpeed;
	public aiState _currentState;
	private Vector3 _aimTarget;
	private bool movementLockingAiming;
	private bool hasAimTarget;
	private bool aimDoorSide;
	private Vector3 _lastPos;
	private Vector3 _lastMoveDir;
	public bool isDead;
	private bool isRetiring;
	public float spawnTime;
	public float lastDamageTime;
	public List<targetinfo> _targetList = new List<targetinfo>();
	public List<MonumentInfo> _visitedMonuments;
	public float arrivalTime;
	public float lastRocketTime;

 */