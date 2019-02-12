using System.Collections.Generic;	//dict
using Oxide.Game.Rust.Cui;
using UnityEngine;	//Vector3
using Oxide.Core.Plugins;
using System.Linq;	//List
using System;   //String.

namespace Oxide.Plugins
{
	[Info("Map My AirDrop", "BuzZ[PHOQUE]", "0.0.6")]
	[Description("Display a popup on Cargo Plane spawn, and a marker on ingame map at drop position.")]

/*======================================================================================================================= 
*
*   
*   6th september 2018
*
*	Marker will show magenta on spawn, and cyan on loot.
*	0.0.5				CUI destroy on unload + permissions for HUD and banner + config file marker radius + changed how HUD refreshes
*	0.0.6				DestoyAllUi + refresh with dic
*	THANKS TO THE OXIDE/UMOD TEAM for coding quality, ideas, and time spent for the community
*	filter SantaSligh drop with true/flase in config
*=======================================================================================================================*/

	public class MapMyAirDrop : RustPlugin
	{
		bool debug = false;
        public Dictionary<BaseEntity, MapMarkerGenericRadius> dropradius = new Dictionary<BaseEntity, MapMarkerGenericRadius>();
        public Dictionary<BaseEntity, bool> lootedornot = new Dictionary<BaseEntity, bool>();
        public Dictionary<BaseEntity, SupplyDrop> entsupply = new Dictionary<BaseEntity, SupplyDrop>();
		public Dictionary<BaseEntity, Vector3> dropposition = new Dictionary<BaseEntity,Vector3>();
		//List<string> HUDlist = new List<string>();
		//List<string> bannerlist = new List<string>();
		private Timer cargorefresh;
        //private string CargoHUDBanner;
        //private string CargoBanner;
        private bool ConfigChanged;
        const string MapMyAirdropHUD = "mapmyairdrop.hud";
		const string MapMyAirdropBanner = "mapmyairdrop.banner"; 
		//public Dictionary<string, BasePlayer> HUDlist = new Dictionary<string,BasePlayer>();
		//public Dictionary<string, BasePlayer> bannerlist = new Dictionary<string,BasePlayer>();
		public Dictionary<BasePlayer, List<string>> HUDlist = new Dictionary<BasePlayer, List<string>>();
		public Dictionary<BasePlayer, List<string>> bannerlist = new Dictionary<BasePlayer, List<string>>();

		float mapmarkerradius = 10;

        void Init()
        {
            LoadVariables();
			permission.RegisterPermission(MapMyAirdropHUD, this);
			permission.RegisterPermission(MapMyAirdropBanner, this);
        }

		void Unload()
		{
			foreach (var paire in dropradius)
			{
				if (paire.Value != null)
				{
					paire.Value.Kill();
					paire.Value.SendUpdate();
					if (debug) {Puts($"AIRDROP MAPMARKER KILLED");}
				}
			}
			DestoyAllUi();
		}

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
			DestroyOneBanner(player);
			DestroyOneHUD(player);
        }

		void DestoyAllUi()
		{
			DestroyAllHUD();
			DestroyAllBanner();
		}

#region MESSAGES

		protected override void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
			{
				{"DroppedMsg", "Aidrop dropped ! Check its position on your MAP (G) - magenta marker."},
				{"SpawnMsg", "A Cargo Plane has spawn. Airdrop will be notified on time."},
				{"LootedMsg", "Someone is looting a SupplyDrop. Marker changed to cyan color."},
				{"KilledMsg", "A Supplydrop has despawn."},
				{"HUDDistanceMsg", "<size=12><color=orange>{0}m.</color></size> away"},
				{"HUDAirdropMsg", "<color=white>AIRDROP</color><color=black>#</color>{0}\n{1}"},

			}, this, "en");

			lang.RegisterMessages(new Dictionary<string, string>
			{
				{"DroppedMsg", "Le Cargo a laché sa cargaison ! Retrouvez la sur la carte (G) - marqueur magenta."},
				{"SpawnMsg", "Un avion cargo vient d'apparaître. Vous serez informé au moment du largage."},
				{"LootedMsg", "Quelqu'un a ouvert une cargaison. Le marqueur est dorénavant bleu ciel."},
				{"KilledMsg", "Une cargaison a été supprimée."},
				{"HUDDistanceMsg", "à <size=12><color=orange>{0}m.</color></size>"},
				{"HUDAirdropMsg", "<color=white>AIRDROP</color><color=black>#</color>{0}\n{1}"},

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
            //SteamIDIcon = Convert.ToUInt64(GetConfig("Chat Settings", "SteamIDIcon", "76561197987461623"));        // SteamID FOR PLUGIN ICON - STEAM PROFILE CREATED FOR THIS PLUGIN / NONE YET /
            mapmarkerradius = Convert.ToSingle(GetConfig("Map Marker settings", "Radius (10 by default)", "10"));

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
////////////////////////////////////////////////////////////////////////////////

        void Destroytimer()
        {
            if (cargorefresh != null){cargorefresh.Destroy();}
        }

		void DestroyAllHUD()
		{
			List<string> todel = new List<string>();
			if (HUDlist != null)
			{
				foreach (var player in BasePlayer.activePlayerList.ToList())
				{
					todel = new List<string>();
					foreach (var playerhud in HUDlist)
					{
						if (playerhud.Key == player)
						{
							todel = playerhud.Value;

						}
					}
					foreach (var item in todel)
					{
						CuiHelper.DestroyUi(player, item);
					}
				}
			}
		}

		void DestroyOneHUD(BasePlayer player)
		{
			List<string> todel = new List<string>();
			if (HUDlist != null)
			{
				foreach (var playerhud in HUDlist)
				{
					if (playerhud.Key == player)
					{
						todel = playerhud.Value;
					}
				}
				foreach (var item in todel)
				{
					CuiHelper.DestroyUi(player, item);
				}
			}
			HUDlist.Remove(player);
		}

		void DestroyAllBanner()
		{
			List<string> todel = new List<string>();
			if (HUDlist != null)
			{
				foreach (var player in BasePlayer.activePlayerList.ToList())
				{
					todel = new List<string>();
					foreach (var playerbanner in bannerlist)
					{
						if (playerbanner.Key == player)
						{
							todel = playerbanner.Value;
						}
					}
					foreach (var item in todel)
					{
						CuiHelper.DestroyUi(player, item);
					}
				}
			}
		}

		void DestroyOneBanner(BasePlayer player)
		{
			List<string> todel = new List<string>();
			if (bannerlist != null)
			{
				foreach (var playerbanner in bannerlist)
				{
					if (playerbanner.Key == player)
					{
						todel = playerbanner.Value;
					}
				}
				foreach (var item in todel)
				{
					CuiHelper.DestroyUi(player, item);
				}
			}
			bannerlist.Remove(player);
		}

        void MarkerDisplayingDelete(BaseEntity Entity)
        {
			MapMarkerGenericRadius delmarker;
			dropradius.TryGetValue(Entity, out delmarker);
            foreach (var paire in dropradius){if (paire.Value == delmarker){delmarker.Kill();delmarker.SendUpdate();}}
			if (debug) {Puts($"AIRDROP MAPMARKER KILLED");}
		}

        void GenerateMarkers()
		{
			if (dropradius != null)
			{
				foreach (var paire in dropradius)
				{
					MapMarkerGenericRadius MapMarkerDel = paire.Value;
					if (MapMarkerDel != null){MapMarkerDel.Kill();MapMarkerDel.SendUpdate();}
				}
			}
			foreach (var paire in dropposition)
			{
				Vector3 position;
                position = paire.Value;
                bool looted;
                lootedornot.TryGetValue(paire.Key, out looted);
				MapMarkerGenericRadius MapMarker = GameManager.server.CreateEntity("assets/prefabs/tools/map/genericradiusmarker.prefab", position) as MapMarkerGenericRadius;
				if (MapMarker == null) return;
				MapMarker.alpha = 0.4f;
				MapMarker.color1 = Color.magenta;
                if (looted){MapMarker.color1 = Color.cyan;}
				MapMarker.color2 = Color.black;
				MapMarker.radius = mapmarkerradius;
				dropradius.Remove(paire.Key);
				dropradius.Add(paire.Key, MapMarker);
				if (debug) {Puts($"CARGO MARKER ADDED IN DICO");}
			}
            foreach (var markers in dropradius)
            {
				markers.Value.Spawn();
            	markers.Value.SendUpdate();
            }
		}

#region HUD

		private void CargoHUD(string reason)
		{
			if (debug) {Puts($"HUD STARTS");}
			DestroyAllHUD();
			HUDlist.Clear();
			double colonnegauche = 0.10;
			double colonnedroite = colonnegauche + 0.07;
			double lignehaut = 1.00;
			double lignebas = lignehaut - 0.05;
            int Round = 1;
            int round = -1;
			List<Vector3> positionlist = new List<Vector3>();
			List<BaseEntity> droplist = new List<BaseEntity>();
			Vector3[] positionarray;			
			BaseEntity[] droparray;
            /*if (reason == "spawn")
            {

            }*/
            if (reason == "dropped")
            {
				if (debug) {Puts($"HUD FOR DROP");}
				foreach (var Suppliez in entsupply)
				{
					Vector3 supplyupdated = Suppliez.Key.transform.position;
					if (debug) {Puts($"REFRESHED SUPPLY POSITION {supplyupdated}");}
					dropposition.Remove(Suppliez.Key);
					dropposition.Add(Suppliez.Key, supplyupdated);
				}
				foreach (var pair in dropposition)
				{
					//droplist.Remove(pair.Key);					
					droplist.Add(pair.Key);
					positionlist.Add(pair.Value);
				}
                droparray = droplist.ToArray();
                positionarray = positionlist.ToArray();
            	int dropnum = droplist.Count;
            	string message = "";
				List<string> HUDforplayers = new List<string>();
				foreach (var player in BasePlayer.activePlayerList.ToList())
				{
					HUDforplayers = new List<string>();
					for (Round = 1; Round <= dropnum ; Round++)     
					{
						if (debug) {Puts($"round {round} on {dropnum}");}
						round = round + 1;
						double colonnedecalage = 0.08 * round;
						bool HUDview = permission.UserHasPermission(player.UserIDString, MapMyAirdropHUD);
						var CuiElement = new CuiElementContainer();
						string CargoHUDBanner = CuiElement.Add(new CuiPanel{Image ={Color = "0.5 0.5 0.5 0.2"},RectTransform ={AnchorMin = $"{colonnegauche + colonnedecalage} {lignebas}",AnchorMax = $"{colonnedroite + colonnedecalage} {lignehaut}"},CursorEnabled = false});
						//}, new CuiElement().Parent = "Overlay", CargoHUDBanner);
						var closeButton = new CuiButton{Button ={Close = CargoHUDBanner,Color = "0.0 0.0 0.0 0.6"},RectTransform ={AnchorMin = "0.90 0.00",AnchorMax = "1.00 1.00"},Text ={Text = "X",FontSize = 8,Align = TextAnchor.MiddleCenter}};
						CuiElement.Add(closeButton, CargoHUDBanner);	// close button in case plugin reload while HUD are on.	
   					    if (debug) {Puts($"PLAYER BEFORE DISTANCE");}
                        Vector3 dropis = positionarray[round];
                        int dist = (int)Vector3.Distance(dropis, player.transform.position);
                        message = String.Format(lang.GetMessage("HUDDistanceMsg", this, player.UserIDString),dist.ToString());
				        if (debug) {Puts($"PLAYER DISTANCE MESSAGE DONE : {message}");}
                        var playerdistance = CuiElement.Add(new CuiLabel
                        {Text = {Text = String.Format(lang.GetMessage("HUDAirdropMsg", this, player.UserIDString),round+1,message), Color = "1.0 1.0 1.0 1.0", FontSize = 10, Align = TextAnchor.MiddleCenter},
                        RectTransform = {AnchorMin = $"0.0 0.0", AnchorMax = $"0.85 1.0"}
                        }, CargoHUDBanner);
						if (HUDview)
						{
							CuiHelper.AddUi(player, CuiElement);
						}
						HUDforplayers.Add(CargoHUDBanner);
					}				
					HUDlist.Remove(player);
					HUDlist.Add(player, HUDforplayers);
				}
            }
		}

#endregion

#region SPAWN DETECTION

        private void OnEntitySpawned(BaseEntity Entity)
        {
            if (Entity == null) return;
            if (Entity is CargoPlane)
            {
				DisplayBannerToAll("spawn");
				if (debug) {Puts($"SPAWN - CARGO");}
				//CargoHUD("spawn", null);
		    }
            if (Entity is SupplyDrop)
            {
				DisplayBannerToAll("dropped");
                SupplyDrop dropped = Entity as SupplyDrop;
                entsupply.Add(Entity, dropped);
                Vector3 supplyposition = Entity.transform.position;
                float supplyx = Entity.transform.position.x;
                float supplyz = Entity.transform.position.z;
                if (debug) {Puts($"SUPPLY SPAWNED x={supplyx} z={supplyz}");}		
				dropposition.Add(Entity, supplyposition);
                GenerateMarkers();
				Destroytimer();
				cargorefresh = timer.Repeat(5, 0, () =>
				{
					if (Entity != null) CargoHUD("dropped");
				});
                //dropped.RemoveParachute();
            }
		}

#endregion

        void OnEntityKill(BaseNetworkable entity)
        {
            BaseEntity killed = entity as BaseEntity;
			if (entsupply.ContainsKey(killed))
			{
				if (debug) {Puts($"KILL OF A SUPPLYDROP");}
                MarkerDisplayingDelete(killed);
                entsupply.Remove(killed);
                dropposition.Remove(killed);
                dropradius.Remove(killed);
				lootedornot.Remove(killed);
				DisplayBannerToAll("killed");
				IfNoMore();
			}
        }

		void IfNoMore()
		{
            if (dropposition.Count == 0)
            {
			    Destroytimer();
                GenerateMarkers();
                dropradius.Clear();
				dropposition.Clear();
				lootedornot.Clear();
				entsupply.Clear();
				DestoyAllUi();

            }
		}

		void OnLootEntity(BasePlayer player, BaseEntity entity)
		{
            if (entity is SupplyDrop)
            {
               // Vector3 playerpos = player.transform.position;
             	if (lootedornot.ContainsKey(entity))
					{
						bool looted;
						lootedornot.TryGetValue(entity, out looted);
						if (looted) return;
					}   
                foreach(var paire in entsupply)
                {
                    if (paire.Key == entity)
                    {
				        if (debug) {Puts($"PLAYER LOOTING A MARKED SUPPLYDROP");}
                        lootedornot.Remove(entity);
                        lootedornot.Add(entity, true);
				        DisplayBannerToAll("looted");
                    }
                }
                GenerateMarkers();
            }
        }
        
#region BANNER

		void DisplayBannerToAll(string reason)
		{
			DestroyAllBanner();
			bannerlist.Clear();
			foreach (var player in BasePlayer.activePlayerList.ToList())
			{
				List<string> bannerforplayers = new List<string>();

				double lignehaut = 0.90;
				double lignebas = 0.85;
				string message = string.Empty;
				switch (reason)
				{
					case "spawn" : 
					{
						message = lang.GetMessage("SpawnMsg", this, player.UserIDString);
						break;
					}
					case "dropped" : 
					{
						message = lang.GetMessage("DroppedMsg", this, player.UserIDString);
						break;
					}
					case "looted" : 
					{
						message = lang.GetMessage("LootedMsg", this, player.UserIDString);
						break;
					}
					case "killed" : 
					{
						message = lang.GetMessage("KilledMsg", this, player.UserIDString);
						lignehaut = lignehaut - 0.06;
						lignebas = lignebas - 0.06;
						break;
					}
				}
				var CuiElement = new CuiElementContainer();
				string CargoBanner = CuiElement.Add(new CuiPanel{Image ={Color = "0.5 0.5 0.5 0.30"},RectTransform ={AnchorMin = $"0.20 {lignebas}",AnchorMax = $"0.80 {lignehaut}"},CursorEnabled = false});                   
				var closeButton = new CuiButton{
					Button ={Close = CargoBanner,Color = "0.0 0.0 0.0 0.6"},RectTransform ={AnchorMin = "0.90 0.01",AnchorMax = "0.99 0.99"},Text ={Text = "X",FontSize = 20,Align = TextAnchor.MiddleCenter}};
				CuiElement.Add(closeButton, CargoBanner);              
				CuiElement.Add(new CuiLabel{
					Text ={Text = $"{message}",FontSize = 20,FadeIn = 1.0f,Align = TextAnchor.MiddleCenter,Color = "1.0 1.0 1.0 1"},RectTransform ={AnchorMin = "0.10 0.10",   AnchorMax = "0.90 0.90"}
					}, CargoBanner);

				bool bannerview = permission.UserHasPermission(player.UserIDString, MapMyAirdropBanner);

				if (bannerview)
				{
					CuiHelper.AddUi(player, CuiElement);
					timer.Once(6, () =>
						{
							//DestroyAllBanner();
							CuiHelper.DestroyUi(player, CargoBanner);
						});
				}
				bannerforplayers.Add(CargoBanner);
				bannerlist.Remove(player);
				bannerlist.Add(player, bannerforplayers);


				//var sound = new Effect("assets/bundled/prefabs/fx/player/howl.prefab", player, 0, Vector3.zero, Vector3.forward);
				//EffectNetwork.Send(sound, player.net.connection);
				//if (debug == true) {Puts($"BANNER AND SOUNDFX PLAYING FOR PLAYER {player.displayName}");}
			}

			//bannerlist.Add(CargoBanner);


		}

#endregion

	}
}