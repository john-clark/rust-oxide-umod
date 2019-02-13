using System;
using UnityEngine;
using System.Linq;  //ToList
using Oxide.Game.Rust.Cui;
using System.Collections.Generic;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Ragnarok", "BuzZ", "1.0.1")]
    [Description("A barrage of meteors and crappy weather")]
/*======================================================================================================================= 
*   26th december 2018
*   chat commands : 
*   original authors of this plugin : Drefetr et Shmitt
*
*   1.0.0   20181227    with new maintainer, new changes : added MapMarkers(can change rate to show more/less explosions) + HUD with permission + Removed from OnTick +
*                       permission.admin => /ragna_start + /ragna_stop + /ragna_timer = new possibilities to start/stop or launch on timer settings
*                       /ragna no need permission to see if Ragnarok is On or not. + changed config file to be more readable
*   1.0.1   20190209    added console commands ragna_start ragna_stop ragna_timer + weather in config % + changed ragnarok spawn locations
*
*=======================================================================================================================*/
    class Ragnarok : RustPlugin
    {

#region SETTINGS & VARIABLES
        bool debug = false;
        bool RagnarokIsOn;
        private bool ConfigChanged;

		const string RagnarokHUD = "ragnarok.hud"; 
		const string RagnarokAdmin = "ragnarok.admin"; 
        string ONRagnarokHUD;
		Timer RagnaTimer;
		Timer RagnaEndTimer;
		Timer RagnaMeteorTimer;
        float MarkerRate = 10f;
        bool MarkersIsOn;

    // Minimum clockwise angular deviation from the normal vector;
    // Where 0.0f is 0 rad, and 1.0f is 1/2 rad. 
        float minLaunchAngle = 0.25f;// (0.0f, ..., maxLaunchAngle).
    // Maximum clockwise angular deviation from the normal vector;
    // Where 0.0f is 0 rad, and 1.0f is 1/2 rad.
        float maxLaunchAngle = 0.5f;    // (minLaunchAngle, ..., 1.0f)
    // Minimum launch height (m); suggested sensible bounds:
    // x >= 1 * maxLaunchVelocity.
        float minLaunchHeight = 100.0f;
    // Maximum launch height (m); suggested sensible bounds:
    // x <= 10*minLaunchVelocity.
        float maxLaunchHeight = 250.0f;
    //Minimum launch velocity (m/s^-1).
        float minLaunchVelocity = 25.0f;
    // Maximum launch velocity (m/s^-1).
    // Suggested sensible maximum: 75.0f.
        float maxLaunchVelocity = 75.0f;
    //Seconds between Meteor(s) launch
        float meteorFrequency = 2f;
    //Maximum number of Meteors per cluster
        int maxClusterMeteors = 5;
    //The minimum range (+/- x, & +/- z) of a Meteor cluster
        int minClusterRange = 1;
    // The maximum range (+/- x, & +/- z) of a Meteor clutser.
        int maxClusterRange = 5;
    // Percent chance of the Meteor dropping loose resources at the point of impact.
        float spawnResourcePercent = 0.05f;
    // Percent chance of the Meteor spawning a resource node at the point of impact.
        float spawnResourceNodePercent = 1.0f;
    // For Timer mode - repeat every X minutes
        float repeater = 15;
    // For Timer mode - duration of Ragnarok in minutes
        float duration = 5;

        double cloud = 80;
        double fog = 80;

#endregion
#region INIT & CONFIG

        void Init()
        {
            LoadVariables();
			permission.RegisterPermission(RagnarokHUD, this);
			permission.RegisterPermission(RagnarokAdmin, this);
        }

        void OnServerInitialized()
        {
            if (duration > repeater)
            {
                PrintWarning("Check your configuration file. Duration is superior to repeat frequency. Change it please.");
            }
        }

        void Unload()
        {
            ConsoleSystem.Run(ConsoleSystem.Option.Server, "weather.clouds 0");
            ConsoleSystem.Run(ConsoleSystem.Option.Server, "weather.fog 0");
            RemoveRagnarokHUD();
        }

    protected override void LoadDefaultConfig()
        {
            LoadVariables();
        }

        private void LoadVariables()
        {
            meteorFrequency = Convert.ToSingle(GetConfig("Cluster", "Meteor Launch Frequency in seconds (2 by default)", "2"));
            maxClusterMeteors = Convert.ToInt32(GetConfig("Cluster", "Maximum number of Meteors (5 by default)", "5"));
            minClusterRange = Convert.ToInt32(GetConfig("Cluster Size", "Minimum range of cluster (1 by default)", "1"));
            maxClusterRange = Convert.ToInt32(GetConfig("Cluster Size", "Maximum range of cluster (5 by default)", "5"));
            minLaunchAngle = Convert.ToSingle(GetConfig("Launch Angle", "Minimum (0.25 by default)", "0.25"));
            maxLaunchAngle = Convert.ToSingle(GetConfig("Launch Angle", "Maximum (0.5 by default)", "0.5"));
            minLaunchHeight = Convert.ToSingle(GetConfig("Launch Height", "Minimum (100 by default)", "100"));
            maxLaunchHeight = Convert.ToSingle(GetConfig("Launch Height", "Maximum (250 by default)", "250"));
            minLaunchVelocity = Convert.ToSingle(GetConfig("Launch Velocity", "Minimum (25 by default)", "25"));
            maxLaunchVelocity = Convert.ToSingle(GetConfig("Launch Velocity", "Maximum (75 by default)", "75"));
            spawnResourcePercent = Convert.ToSingle(GetConfig("Resources Loot", "Percent chance of the Meteor dropping (0.05 by default)", "0.05"));
            spawnResourceNodePercent = Convert.ToSingle(GetConfig("Resources Loot", "Percent chance of the Meteor spawning node (1 by default)", "1"));
            repeater = Convert.ToSingle(GetConfig("Timer Settings", "Repeat Ragnarok every (15 by default) minutes", "15"));
            duration = Convert.ToSingle(GetConfig("Timer Settings", "Ragnarok will be there for (5 by default) minutes", "5"));
            cloud = Convert.ToDouble(GetConfig("Meteo Settings", "Cloud value (in percent)", "80"));
            fog = Convert.ToDouble(GetConfig("Meteo Settings", "Fog value (in percent)", "80"));

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
#region RAGNAROOOOK N ROLL
////////////////////////////////////////////////////////////////////////////////////
        void LaunchDaRagnarokOnDaFace()
        {
            ConsoleSystem.Run(ConsoleSystem.Option.Server, $"weather.clouds {(cloud/100).ToString()}");
            ConsoleSystem.Run(ConsoleSystem.Option.Server, $"weather.fog {(fog/100).ToString()}");
            RagnarokIsOn = true;

            DisplayRagnarokHUD();

            // Fetch a random position, with an altitude of {0}.
            var location = GetRandomMapPosition();
            var clusterSize = UnityEngine.Random.Range(1, maxClusterMeteors);

            RagnaMeteorTimer = timer.Every(meteorFrequency, () =>
            {
                for (var i = 0; i < clusterSize; i++)
                {
                    var r = UnityEngine.Random.Range(0.0f, 100.0f);

                    // Add a (slight) degree of randomness to the launch position(s):
                    location.x += UnityEngine.Random.Range(minClusterRange, maxClusterRange);
                    location.z += UnityEngine.Random.Range(minClusterRange, maxClusterRange);

                    if (r < spawnResourcePercent)
                        // Spawn a loose resource:
                        SpawnResource(location);

                    if (r < spawnResourceNodePercent)
                        // Spawn a resource node:
                        SpawnResourceNode(location);

                    SpawnMeteor(location);
                }
		    });
        }
#endregion
#region CHAT & CONSOLE COMMANDS
////////////////////////
// CHAT & CONSOLE COMMANDS
//////////////////////////
        [ChatCommand("ragna")]
        private void RagnarokEmptyChatCommand(BasePlayer player, string command, string[] args)
        {        
            SendReply(player, $"Ragnarok is {RagnarokIsOn}");
        }
//////////////////////
// CHAT START
//////////////////////
        [ChatCommand("ragna_start")]
        private void StartThisRagnarokBabeChatCommand(BasePlayer player, string command, string[] args)
        {        
            bool isadmin = permission.UserHasPermission(player.UserIDString, RagnarokAdmin);

            if (!isadmin)
            {
                SendReply(player, $"You don't have permission for this.");
                return;
            }
            else
            {
                if (!RagnarokIsOn)
                {
                    StartThisRagnarokBabe();
                    SendReply(player, $"Ragnarok has started !");
                }
                else SendReply(player, $"Ragnarok is already On.");
            }
        }

//////////////////////
// CONSOLE START
//////////////////////
        [ConsoleCommand("ragna_start")]
        private void StartThisRagnarokBabeConsoleCommand(ConsoleSystem.Arg arg)
        {
            if (!RagnarokIsOn) StartThisRagnarokBabe();
        }

//////////////////////
// CHAT TIMER LAUNCH
//////////////////////   
        [ChatCommand("ragna_timer")]
        private void GetThisRagnarokBabeOnTimerChatCommand(BasePlayer player, string command, string[] args)
        {
            bool isadmin = permission.UserHasPermission(player.UserIDString, RagnarokAdmin);
            if (!isadmin)
            {
                SendReply(player, $"You don't have permission for this.");
                return;
            }
            StopThisRagnarokBabeChatCommand(player, null, null);
            float repeaterseconds = repeater*60;
            // For Timer mode - duration of Ragnarok in minutes
            float durationseconds = duration*60;
            StartThisRagnarokBabeChatCommand(player, null, null);
            RagnaEndTimer = timer.Every(durationseconds, () =>
            {
                StopThisRagnarokBabe();
            });
            RagnaTimer = timer.Every(repeaterseconds, () =>
            {
                StartThisRagnarokBabe();
            });
            RagnarokIsOn = true;
            SendReply(player, $"Ragnarok has started on timer.\nWill repeat every {repeater} minutes, for {duration} minutes.");

        }
//////////////////////
// CONSOLE TIMER LAUNCH
//////////////////////   
        [ConsoleCommand("ragna_timer")]
        private void GetThisRagnarokBabeOnTimerConsoleCommand(ConsoleSystem.Arg arg)
        {
            StopThisRagnarokBabeConsoleCommand(null);
            float repeaterseconds = repeater*60;
            // For Timer mode - duration of Ragnarok in minutes
            float durationseconds = duration*60;
            StartThisRagnarokBabeConsoleCommand(null);
            RagnaEndTimer = timer.Every(durationseconds, () =>
            {
                StopThisRagnarokBabe();
            });
            RagnaTimer = timer.Every(repeaterseconds, () =>
            {
                StartThisRagnarokBabe();
            });
            RagnarokIsOn = true;
        }

//////////////////////
// CHAT STOP
//////////////////////

        [ChatCommand("ragna_stop")]
        private void StopThisRagnarokBabeChatCommand(BasePlayer player, string command, string[] args)
        {
            bool isadmin = permission.UserHasPermission(player.UserIDString, RagnarokAdmin);
            if (!isadmin)
            {
                SendReply(player, $"You don't have permission for this.");
                return;
            }
            if (RagnarokIsOn)
            {
                if (RagnaEndTimer != null) RagnaEndTimer.Destroy();
                if (RagnaTimer != null ) RagnaTimer.Destroy();
                StopThisRagnarokBabe();
                SendReply(player, $"Ragnarok has stopped.");
            }
            else SendReply(player, $"Ragnarok is already Off.");
        }
//////////////////
// CONSOLE STOP
//////////////////
        [ConsoleCommand("ragna_stop")]
        private void StopThisRagnarokBabeConsoleCommand(ConsoleSystem.Arg arg)
        {
            if (RagnarokIsOn)
            {
                if (RagnaEndTimer != null) RagnaEndTimer.Destroy();
                if (RagnaTimer != null ) RagnaTimer.Destroy();
                StopThisRagnarokBabe();
            }
        }

//////////////////
// STOP
//////////////
        void StopThisRagnarokBabe()
        {
            if (RagnaMeteorTimer != null) RagnaMeteorTimer.Destroy();
            ConsoleSystem.Run(ConsoleSystem.Option.Server, "weather.clouds 0");
            ConsoleSystem.Run(ConsoleSystem.Option.Server, "weather.fog 0");
            RagnarokIsOn = false;
            RemoveRagnarokHUD();
        }
///////////////
// START
///////////////
        void StartThisRagnarokBabe()
        {
            LaunchDaRagnarokOnDaFace();
        }


/////// change location


/////////// 
#endregion

//////////////////
// PLAYER CONNECTION - if Ragnarok on -> HUD
/////////////////////////


//////////////////
// PLAYER DISCONNECT - kill HUD
/////////////////////////

#region HUD
/////////////////////////
// HUD
//////////////////////////
		void DisplayRagnarokHUD()
		{
            foreach (BasePlayer player in BasePlayer.activePlayerList.ToList())
            {
                var CuiElement = new CuiElementContainer();
                ONRagnarokHUD = CuiElement.Add(new CuiPanel{Image ={Color = "0.0 0.0 0.0 0.0"},RectTransform ={AnchorMin = $"0.97 0.70",AnchorMax = $"1.0 0.73"},CursorEnabled = false
                    }, new CuiElement().Parent = "Overlay", ONRagnarokHUD);
                    
                        CuiElement.Add(new CuiElement
                        {
                            Name = CuiHelper.GetGuid(),
                            Parent = ONRagnarokHUD,
                            Components =
                                {
                                    new CuiRawImageComponent {Url = "https://i.ibb.co/m4SCRxc/ragnarok-01.png"},
                                    new CuiRectTransformComponent {AnchorMin = $"0.0 0.0", AnchorMax = $"1.0 1.0"}
                                }
                        });
                bool HUDview = permission.UserHasPermission(player.UserIDString, RagnarokHUD);
                if (HUDview) CuiHelper.AddUi(player, CuiElement);
            }
		}

		void RemoveRagnarokHUD()
		{
            foreach (BasePlayer player in BasePlayer.activePlayerList.ToList())
            {
                CuiHelper.DestroyUi(player, ONRagnarokHUD);
            }
		}

#endregion
#region SPAWNS
//////////////////////
// METEOR SPAWN
//////////////////////
        void SpawnMeteor(Vector3 origin)
        {
            var launchAngle = UnityEngine.Random.Range(minLaunchAngle, maxLaunchAngle);
            var launchHeight = UnityEngine.Random.Range(minLaunchHeight, maxLaunchHeight);
            var launchDirection = (Vector3.up * -launchAngle + Vector3.right).normalized;
            var launchPosition = origin - launchDirection * launchHeight;
            var r = UnityEngine.Random.Range(0, 3);
            ItemDefinition projectileItem;
            // Fetch rocket of type <x>:
            switch (r)
            {
                case 0:
                    projectileItem = GetBasicRocket();
                    break;

                case 1:
                    projectileItem = GetHighVelocityRocket();
                    break;

                case 2:
                    projectileItem = GetSmokeRocket();
                    break;

                default:
                    projectileItem = GetFireRocket();
                    break;
            }
            // Create the in-game "Meteor" entity:
            var component = projectileItem.GetComponent<ItemModProjectile>();
            var entity = GameManager.server.CreateEntity(component.projectileObject.resourcePath, launchPosition, new Quaternion(), true);
            if (entity == null)
            {
                if (debug) Puts("entity is NULL !!");
                return;
            }
            // Set Meteor speed:
            var serverProjectile = entity.GetComponent<ServerProjectile>();
            serverProjectile.speed = UnityEngine.Random.Range(minLaunchVelocity, maxLaunchVelocity);
            entity.SendMessage("InitializeVelocity", (object)(launchDirection * 1.0f));
            entity.OwnerID = 666999666999666;
            entity.Spawn();
            GenerateMarkers(launchPosition);
        }

/////////////////////////////////
// METEOR DAMAGE NULLED
////////////


//////////////////////
// RESOURCE SPAWN
//////////////////////
        void SpawnResource(Vector3 location)
        {
            string resourceName;
            int resourceQuantity;
            var r = UnityEngine.Random.Range(0, 3);
            switch (r)
            {
                case 1:
                    resourceName = "hq.metal.ore";
                    resourceQuantity = 100;
                    break;

                case 2:
                    resourceName = "metal.ore";
                    resourceQuantity = 1000;
                    break;

                case 3:
                    resourceName = "stones";
                    resourceQuantity = 2500;
                    break;

                default:
                    resourceName = "sulfur.ore";
                    resourceQuantity = 1000;
                    break;
            }
            ItemManager.CreateByName(resourceName, resourceQuantity).Drop(location, Vector3.up);
        }
//////////////////////
// RESOURCE NODE SPAWN
//////////////////////
        void SpawnResourceNode(Vector3 location)
        {
            var prefabName = "assets/bundled/prefabs/autospawn/resource/ores/";
            var r = UnityEngine.Random.Range(0, 2);
            switch (r)
            {
                case 1:
                    prefabName += "metal-ore";
                    break;

                case 2:
                    prefabName += "stone-ore";
                    break;

                default:
                    prefabName += "sulfur-ore";
                    break;
            }
            prefabName += ".prefab";
            // & spawn the ResourceNode at Vector3(location).
            var resourceNode = GameManager.server.CreateEntity(prefabName, location, new Quaternion(0, 0, 0, 0));
            resourceNode?.Spawn();
        }
#endregion
#region RANDOM ROCKETS
///////////////
// RANDOMIZED ROCKET TYPE - it it useful to separate from void ?
//////////////
        ItemDefinition GetBasicRocket()
        {
            return ItemManager.FindItemDefinition("ammo.rocket.basic");
        }
        ItemDefinition GetFireRocket()
        {
            return ItemManager.FindItemDefinition("ammo.rocket.fire");
        }
        ItemDefinition GetHighVelocityRocket()
        {
            return ItemManager.FindItemDefinition("ammo.rocket.hv");
        }
        ItemDefinition GetSmokeRocket()
        {
            return ItemManager.FindItemDefinition("ammo.rocket.smoke");
        }
#endregion
#region MAP
///////////////
// MAP
//////////////////
        Vector3 GetRandomMapPosition()
        {
//            var mapsize = GetMapSize() - 500f;
            var mapsize = GetMapSize();
            var randomX = UnityEngine.Random.Range(-mapsize, mapsize);
            var randomY = UnityEngine.Random.Range(-mapsize, mapsize);
            return new Vector3(randomX, 0f, randomY);
        }

        float GetMapSize()
        {
            return TerrainMeta.Size.x / 2;
        }

///////////////
// MAPMARKER - EXPLOSION
///////////////////
        void GenerateMarkers(Vector3 position)
		{
            if (!RagnarokIsOn) return;
            if (!MarkersIsOn)
            {
                MarkersIsOn = true;
                MapMarkerExplosion RagnarokMarker = new MapMarkerExplosion();
                RagnarokMarker = GameManager.server.CreateEntity("assets/prefabs/tools/map/explosionmarker.prefab", position) as MapMarkerExplosion;
                if (RagnarokMarker == null) return;
                RagnarokMarker.SetDuration(1);
                RagnarokMarker.Spawn();
                timer.Once(MarkerRate, () =>
                {
                    MarkersIsOn = false;
                });
            }
        }
#endregion
///////////////
// MESSAGE ONLINE PLAYERS
//////////////////
    }
}