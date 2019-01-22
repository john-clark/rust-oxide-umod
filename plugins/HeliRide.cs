using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Heli Ride", "ColonBlow", "1.1.14", ResourceId = 2274)]
    [Description("Allows players to fly the Patrol Helicopter")]
    public class HeliRide : RustPlugin
    {

        // Fix for rocket speed being way too slow

        [PluginReference]
        private Plugin Chute, Vanish;

        private static Dictionary<ulong, HeliData> HeliFlying = new Dictionary<ulong, HeliData>();
        private static Dictionary<ulong, HeliDamage> DamagedHeli = new Dictionary<ulong, HeliDamage>();
        private static Dictionary<ulong, HasParachute> AddParachute = new Dictionary<ulong, HasParachute>();

        public class HeliData { public BasePlayer player; }

        public class HeliDamage { public BasePlayer player; }

        public class HasParachute { public BasePlayer player; }

        private bool Changed;
        private static bool ShowCockpitOverlay = true;
        private static bool ShowCrosshair = true;
        private static bool UseParachutes = true;
        private static bool SpawnCrates = false;
        private static bool UseAutoVanish = false;
        private static double RocketDelay = 0.2;
        private static float RocketMax = 36f;
        private static float NapalmMax = 36f;
        private static double RocketNapalmReloadTime = 20;
        private static float BulletDamage = 50f;

        private void Loaded()
        {
            if (Chute == null)
            {
                PrintWarning("Chute plugin not found. To enable player parachutes when there helicopter dies, install Chute plugin!");
            }
            if (Vanish == null)
            {
                PrintWarning("Vanish plugin not found. Player will be visable and UseAutoVanish will be turned off");
                UseAutoVanish = false;
            }
            LoadVariables();
            permission.RegisterPermission("heliride.allowed", this);
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Creating a new config file");
            Config.Clear();
            LoadVariables();
        }

        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"noheli", "You are not flying a helicopter."},
                {"notallowed", "You are not allowed to access that command."},
                {"notflying", "You must be noclipping to activate Helicopter."}
            }, this);
        }

        private bool isAllowed(BasePlayer player, string perm) => permission.UserHasPermission(player.UserIDString, perm);

        ////////////////////////////////////////////////////////////////////////////////
        ///// Configuration Stuff
        ////////////////////////////////////////////////////////////////////////////////

        private void LoadConfigVariables()
        {
            CheckCfg("ShowCockpitOverlay", ref ShowCockpitOverlay);
            CheckCfg("ShowCrosshair", ref ShowCrosshair);
            CheckCfg("UseParachutes", ref UseParachutes);
            CheckCfg("SpawnCrates", ref SpawnCrates);
            CheckCfg("UseAutoVanish", ref UseAutoVanish);
            CheckCfg("RocketDelay", ref RocketDelay);
            CheckCfg("RocketNapalmReloadTime", ref RocketNapalmReloadTime);
            CheckCfgFloat("BulletDamage", ref BulletDamage);
            CheckCfgFloat("Max Rockets Loaded", ref RocketMax);
            CheckCfgFloat("Max Napalm Loaded", ref NapalmMax);
        }

        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }

        private void CheckCfg<T>(string Key, ref T var)
        {
            if (Config[Key] is T)
                var = (T)Config[Key];
            else
                Config[Key] = var;
        }

        private void CheckCfgFloat(string Key, ref float var)
        {
            if (Config[Key] != null)
                var = Convert.ToSingle(Config[Key]);
            else
                Config[Key] = var;
        }

        private object GetConfig(string menu, string datavalue, object defaultValue)
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

        ////////////////////////////////////////////////////////////////////////////////
        ///// Cancels Damage to Player while they are flying the helicopter
        ////////////////////////////////////////////////////////////////////////////////

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (entity is BasePlayer)
            {
                var player = (BasePlayer)entity;
                if (HeliFlying.ContainsKey(player.userID))
                {
                    //Puts("Heli null damage"); //debug damage null
                    hitInfo.damageTypes.ScaleAll(0);
                }
            }
        }

        private void OnPlayerTick(BasePlayer player)
        {
            if (!UseParachutes) return;
            if (UseParachutes)
            {
                if (!AddParachute.ContainsKey(player.userID)) return;
                if (AddParachute.ContainsKey(player.userID))
                {
                    if (Chute != null)
                    {
                        AddParachute.Remove(player.userID);
                        timer.Once(0.5f, () => Chute.Call("ExternalAddPlayerChute", player));
                        if (Vanish != null && UseAutoVanish)
                        {
                            timer.Once(0.5f, () => Vanish.Call("Reappear", player));
                        }
                    }
                    if (Chute == null)
                    {
                        AddParachute.Remove(player.userID);
                    }
                }
            }
        }

        ////////////////////////////////////////////////////////////////////////////////

        public bool CockpitOverlay => Config.Get<bool>("Show Custom Cockpit Overlay");
        public bool CrossHair => Config.Get<bool>("Show Custom Crosshair");

        private class FlyHelicopter : MonoBehaviour
        {
            public BasePlayer player;
            public BaseEntity helicopterBase;
            private BaseEntity rockets;

            public PatrolHelicopterAI heliAI;
            public BaseHelicopter heli;
            public HelicopterTurret heliturret;
            public InputState input;

            private RaycastHit hitInfo;

            public Vector3 PlayerPOS;
            public Vector3 target;
            public Vector3 CurrentPOS;
            private Vector3 direction;

            private float bulletDamage;
            private float rocketMax;
            private bool hasRockets;
            private float napalmMax;
            private bool hasNapalm;
            private double rocketcycletimer;
            private double reloadtimer;
            private double rocketDelay;
            private bool rocketcycle;
            private bool leftTubeFiredLast;
            private bool isReloading;
            private double rocketNapalmReploadTime;

            private void Awake()
            {
                player = GetComponent<BasePlayer>();
                input = player.serverInput;
                rocketcycletimer = 0.0;
                reloadtimer = 0.0;
                isReloading = false;
                rocketNapalmReploadTime = RocketNapalmReloadTime;
                rocketMax = RocketMax;
                hasRockets = true;
                napalmMax = NapalmMax;
                hasNapalm = true;
                rocketDelay = RocketDelay;
                bulletDamage = BulletDamage;
                rocketcycle = false;
                PlayerPOS = player.transform.position + player.eyes.BodyForward() * 3f;

                string prefab = "assets/prefabs/npc/patrol helicopter/patrolhelicopter.prefab";
                helicopterBase = GameManager.server.CreateEntity(prefab);
                heliAI = helicopterBase.GetComponent<PatrolHelicopterAI>();
                heliAI.enabled = false;

                heliturret = helicopterBase.GetComponent<HelicopterTurret>();

                heli = helicopterBase.GetComponent<BaseHelicopter>();
                heli.InitalizeWeakspots();

                if (!SpawnCrates) heli.maxCratesToSpawn = 0;
                heli.bulletDamage = bulletDamage;

                heli.spotlightTarget = FindTarget(target);
                helicopterBase.Spawn();

                if (ShowCockpitOverlay) CockpitOverlay(player);
                if (ShowCrosshair) CrosshairOverlay(player);

                helicopterBase.transform.localPosition = PlayerPOS;
                helicopterBase.transform.rotation = player.eyes.rotation;
            }

            //////////////////////////////////////////////////////////////////////////////////////

            public void CockpitOverlay(BasePlayer player)
            {
                var cockpitcui = new CuiElementContainer();

                cockpitcui.Add(new CuiElement
                {
                    Name = "CockpitGuiOverlay",
                    Components =
                        {
                            new CuiRawImageComponent { Color = "1 1 1 1", Url = "http://i.imgur.com/6O0hMC5.png", Sprite = "assets/content/textures/generic/fulltransparent.tga" },
                            new CuiRectTransformComponent { AnchorMin = "0 0",  AnchorMax = "1 1"}
                        }
                });
                CuiHelper.AddUi(player, cockpitcui);
            }

            public void CrosshairOverlay(BasePlayer player)
            {
                var crosshaircui = new CuiElementContainer();

                crosshaircui.Add(new CuiElement
                {
                    Name = "CrosshairGuiOverlay",
                    Components =
                        {
                            new CuiRawImageComponent { Color = "1 1 1 1", Url = "http://i.imgur.com/yweKHFT.png", Sprite = "assets/content/textures/generic/fulltransparent.tga" },
                            new CuiRectTransformComponent { AnchorMin = "0.450 0.450",  AnchorMax = "0.540 0.550"}
                        }
                });
                CuiHelper.AddUi(player, crosshaircui);
            }

            public void DamageOverlay(BasePlayer player)
            {
                var damageoverlay = new CuiElementContainer();

                damageoverlay.Add(new CuiElement
                {
                    Name = "DamageGuiOverlay",
                    Components =
                        {
                            new CuiRawImageComponent { Color = "1 1 1 1", Url = "http://i.imgur.com/XrpqTdP.png", Sprite = "assets/content/textures/generic/fulltransparent.tga" },
                            new CuiRectTransformComponent { AnchorMin = "0.35 0.25",  AnchorMax = "0.60 0.70"}
                        }
                });
                CuiHelper.AddUi(player, damageoverlay);
            }

            public void HealthIndicator(BasePlayer player, float health)
            {
                CuiHelper.DestroyUi(player, "HealthGui");
                var healthstr = health.ToString();
                var rocketstr = isReloading ? "R" : rocketMax.ToString();
                var napalmstr = isReloading ? "R" : napalmMax.ToString();
                var dispalystr = isReloading ? "Reloading     " + healthstr + "     Reloading" : "N: " + napalmstr + "         " + healthstr + "         R: " + rocketstr;

                var healthindicator = new CuiElementContainer();
                healthindicator.Add(new CuiButton
                {
                    Button = { Command = "", Color = "0.0 0.0 0.0 1.0" },
                    RectTransform = { AnchorMin = "0.40 0.15", AnchorMax = "0.60 0.18" },
                    Text = { Text = dispalystr, FontSize = 18, Color = "1.0 0.0 0.0 0.2", Align = TextAnchor.MiddleCenter }
                }, "Overall", "HealthGui");
                CuiHelper.AddUi(player, healthindicator);
            }

            //////////////////////////////////////////////////////////////////////////////////////

            private void FixedUpdate()
            {
                player = GetComponent<BasePlayer>();
                if (player.IsDead() || !player.IsFlying)
                {
                    heliAI._currentState = PatrolHelicopterAI.aiState.DEATH;
                }
                if (heliAI._currentState == PatrolHelicopterAI.aiState.DEATH)
                {
                    heliAI.enabled = true;
                    DestroyCui(player);
                    GameObject.Destroy(this);
                    return;
                }

                if (rocketMax <= 0) hasRockets = false;
                if (napalmMax <= 0) hasNapalm = false;
                Vector3 PlayerPOS = player.transform.position - player.eyes.BodyForward() * 5 + Vector3.down * 0.45f;
                CurrentPOS = helicopterBase.transform.position;
                Vector3 direction = Quaternion.Euler(input.current.aimAngles) * Vector3.fwd;

                heli.spotlightTarget = FindTarget(target);

                helicopterBase.transform.localPosition = PlayerPOS;
                helicopterBase.transform.rotation = Quaternion.Lerp(helicopterBase.transform.rotation, player.eyes.rotation, 2f * Time.deltaTime);

                helicopterBase.transform.eulerAngles = new Vector3(0, helicopterBase.transform.eulerAngles.y, 0);

                BaseCombatEntity helientity = helicopterBase.GetComponent<BaseCombatEntity>();
                float health = helientity.Health();

                HealthIndicator(player, health);
                if (health <= 3000f && ShowCockpitOverlay)
                {
                    if (!DamagedHeli.ContainsKey(player.userID))
                    {
                        if (ShowCockpitOverlay)
                        {
                            DamageOverlay(player);
                            DamagedHeli.Add(player.userID, new HeliDamage
                            {
                                player = player
                            });
                        }
                    }
                }
                if (isReloading)
                {
                    reloadtimer += Time.deltaTime;
                    if (reloadtimer >= rocketNapalmReploadTime)
                    {
                        isReloading = false;
                        rocketMax = RocketMax;
                        hasRockets = true;
                        napalmMax = NapalmMax;
                        hasNapalm = true;
                        reloadtimer = 0.0;
                    }
                }
                if (rocketcycle)
                {
                    rocketcycletimer += Time.deltaTime;
                    if (rocketcycletimer >= rocketDelay)
                    {
                        rocketcycle = false;
                        rocketcycletimer = 0.0;
                    }
                }
                if (health > 3000f && ShowCockpitOverlay)
                {
                    if (DamagedHeli.ContainsKey(player.userID))
                    {
                        CuiHelper.DestroyUi(player, "DamageGuiOverlay");
                        DamagedHeli.Remove(player.userID);
                    }
                }

                if (input.IsDown(BUTTON.RELOAD))
                {
                    isReloading = true;
                }

                if (input.IsDown(BUTTON.DUCK))
                {
                    Vector3 downPos = player.transform.position + Vector3.down * (UnityEngine.Time.deltaTime * 3f);
                    player.ClientRPCPlayer(null, player, "ForcePositionTo", downPos);
                    player.SendNetworkUpdate();
                }

                if (input.IsDown(BUTTON.FIRE_PRIMARY))
                {
                    target = FindTarget(target);
                    FireGuns(target);
                }
                if (input.IsDown(BUTTON.FIRE_SECONDARY))
                {
                    if (!hasRockets || isReloading) return;
                    if (!rocketcycle) { leftTubeFiredLast = !leftTubeFiredLast; FireRocket(leftTubeFiredLast, direction, PlayerPOS, true); }
                    rocketcycle = true;
                }
                if (input.IsDown(BUTTON.FIRE_THIRD))
                {
                    if (!hasNapalm || isReloading) return;
                    if (!rocketcycle) { leftTubeFiredLast = !leftTubeFiredLast; FireRocket(leftTubeFiredLast, direction, PlayerPOS, false); }
                    rocketcycle = true;
                }
            }

            private void FireGuns(Vector3 target)
            {
                heliAI.FireGun(target, ConVar.PatrolHelicopter.bulletAccuracy, true);
                heliAI.FireGun(target, ConVar.PatrolHelicopter.bulletAccuracy, false);
            }

            private void FireRocket(bool leftTubeFiredLast, Vector3 direction, Vector3 PlayerPOS, bool isrocket)
            {
                RaycastHit hit;
                string projectile;
                if (isrocket) { rocketMax = rocketMax - 1f; }
                if (!isrocket) { napalmMax = napalmMax - 1f; }
                float num = 4f;
                projectile = isrocket ? heliAI.rocketProjectile.resourcePath : heliAI.rocketProjectile_Napalm.resourcePath;
                Vector3 origin = PlayerPOS + Vector3.down;
                if (num > 0f)
                {
                    direction = (Vector3)(Quaternion.Euler(UnityEngine.Random.Range((float)(-num * 0.5f), (float)(num * 0.5f)), UnityEngine.Random.Range((float)(-num * 0.5f), (float)(num * 0.5f)), UnityEngine.Random.Range((float)(-num * 0.5f), (float)(num * 0.5f))) * direction);
                }
                float maxDistance = 1f;
                if (Physics.Raycast(origin, direction, out hit, maxDistance, -1063040255))
                {
                    maxDistance = hit.distance - 0.1f;
                }
                Transform transform = !leftTubeFiredLast ? heliAI.helicopterBase.rocket_tube_right.transform : heliAI.helicopterBase.rocket_tube_left.transform;
                Effect.server.Run(heliAI.helicopterBase.rocket_fire_effect.resourcePath, heliAI.helicopterBase, StringPool.Get(!leftTubeFiredLast ? "rocket_tube_right" : "rocket_tube_left"), Vector3.zero, Vector3.forward, null, true);
                Vector3 rocketPos = !leftTubeFiredLast ? heliAI.helicopterBase.rocket_tube_right.transform.position : heliAI.helicopterBase.rocket_tube_left.transform.position;
                rockets = GameManager.server.CreateEntity(projectile, rocketPos);
                if (rockets != null)
                {
                    rockets.SendMessage("InitializeVelocity", (Vector3)(direction * 50f));
                    rockets.Spawn();
                }
            }

            private Vector3 FindTarget(Vector3 target)
            {
                if (!UnityEngine.Physics.Raycast(player.eyes.HeadRay(), out hitInfo, Mathf.Infinity, -1063040255))
                {
                }
                Vector3 hitpoint = hitInfo.point;
                return hitpoint;
            }

            public void DestroyCui(BasePlayer player)
            {
                CuiHelper.DestroyUi(player, "CockpitGuiOverlay");
                CuiHelper.DestroyUi(player, "CrosshairGuiOverlay");
                CuiHelper.DestroyUi(player, "DamageGuiOverlay");
                CuiHelper.DestroyUi(player, "HealthGui");
                DamagedHeli.Remove(player.userID);
            }

            private void addplayerchute()
            {
                if (!UseParachutes) return;
                AddParachute.Add(player.userID, new HasParachute
                {
                    player = player
                });
            }

            public void OnDestroy()
            {
                player = GetComponent<BasePlayer>();

                DestroyCui(player);
                DamagedHeli.Remove(player.userID);
                HeliFlying.Remove(player.userID);

                if (helicopterBase == null) return;
                if (heliAI._currentState == PatrolHelicopterAI.aiState.DEATH)
                {
                    heliAI.enabled = true;
                    heli.bulletDamage = 0f;
                    GameObject.Destroy(this);
                    addplayerchute();
                    return;
                }
                helicopterBase.Kill();
                GameObject.Destroy(this);
            }
        }

        ////////////////////////////////////////////////////////////////////////////////
        // Chat and Console Commands
        ////////////////////////////////////////////////////////////////////////////////

        [ChatCommand("flyheli")]
        private void chatFlyHeli(BasePlayer player, string command, string[] args)
        {
            string SteamID = player.userID.ToString();
            if (isAllowed(player, "heliride.allowed"))
            {
                var playerheli = player.GetComponent<FlyHelicopter>();

                if (HeliFlying.ContainsKey(player.userID))
                {
                    GameObject.Destroy(playerheli);
                    if (Vanish != null && UseAutoVanish) { Vanish.Call("Reappear", player); }
                    HeliFlying.Remove(player.userID);
                    return;
                }
                if (playerheli != null)
                {
                    GameObject.Destroy(playerheli);
                    if (Vanish != null && UseAutoVanish) { Vanish.Call("Reappear", player); }
                    HeliFlying.Remove(player.userID);
                    return;
                }

                if (playerheli == null)
                {
                    if (!player.IsFlying) { SendReply(player, lang.GetMessage("notflying", this, SteamID)); return; }
                    if (Vanish != null && UseAutoVanish) { Vanish.Call("Disappear", player); }
                    timer.Once(1f, () => AddHeli(player));
                    return;
                }
            }

            if (!isAllowed(player, "heliride.allowed"))
            {
                SendReply(player, lang.GetMessage("notallowed", this, SteamID));
            }
        }

        [ConsoleCommand("flyheli")]
        private void cmdConsoleFlyHeli(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            string SteamID = player.userID.ToString();
            if (isAllowed(player, "heliride.allowed"))
            {
                var playerheli = player.GetComponent<FlyHelicopter>();

                if (HeliFlying.ContainsKey(player.userID))
                {
                    GameObject.Destroy(playerheli);
                    HeliFlying.Remove(player.userID);
                    if (Vanish != null && UseAutoVanish) { Vanish.Call("Reappear", player); }
                    return;
                }
                if (playerheli != null)
                {
                    GameObject.Destroy(playerheli);
                    if (Vanish != null && UseAutoVanish) { Vanish.Call("Reappear", player); }
                    HeliFlying.Remove(player.userID);
                    return;
                }

                if (playerheli == null)
                {
                    if (!player.IsFlying) { SendReply(player, lang.GetMessage("notflying", this, SteamID)); return; }
                    if (Vanish != null && UseAutoVanish) { Vanish.Call("Disappear", player); }
                    timer.Once(1f, () => AddHeli(player));
                    return;
                }
            }

            if (!isAllowed(player, "heliride.allowed"))
            {
                SendReply(player, lang.GetMessage("notallowed", this, SteamID));
            }
        }

        [ConsoleCommand("showcockpit")]
        private void cmdConsoleShowCockpit(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            var playerheli = player.GetComponent<FlyHelicopter>();

            if (!playerheli)
            {
                string SteamID = player.userID.ToString();
                SendReply(player, lang.GetMessage("noheli", this, SteamID));
                return;
            }

            if (playerheli)
            {
                playerheli.CockpitOverlay(player);
                playerheli.CrosshairOverlay(player);
            }
        }

        [ConsoleCommand("hidecockpit")]
        private void cmdConsoleHideCockpit(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            var playerheli = player.GetComponent<FlyHelicopter>();

            if (!playerheli)
            {
                string SteamID = player.userID.ToString();
                SendReply(player, lang.GetMessage("noheli", this, SteamID));
                return;
            }

            if (playerheli)
            {
                CuiHelper.DestroyUi(player, "CockpitGuiOverlay");
                CuiHelper.DestroyUi(player, "CrosshairGuiOverlay");
                CuiHelper.DestroyUi(player, "DamageGuiOverlay");
            }
        }

        //////////////////////////////////////////////////////////////////////////////////////////////////

        private void AddHeli(BasePlayer player)
        {
            if (player.IsFlying)
            {
                player.gameObject.AddComponent<FlyHelicopter>();
                HeliFlying.Add(player.userID, new HeliData
                {
                    player = player
                });
                return;
            }

            if (!player.IsFlying)
            {
                string SteamID = player.userID.ToString();
                SendReply(player, lang.GetMessage("notflying", this, SteamID));
            }
        }

        //////////////////////////////////////////////////////////////////////////////////////////////////

        private void Unload()
        {
            DestroyAll<FlyHelicopter>();
            foreach (var player in BasePlayer.activePlayerList)
            {
                RemoveHeliComponents(player);
            }
        }

        private static void DestroyAll<T>()
        {
            var objects = GameObject.FindObjectsOfType(typeof(T));
            if (objects != null)
            {
                foreach (var gameObj in objects)
                {
                    GameObject.Destroy(gameObj);
                }
            }
        }

        private void RemoveHeliComponents(BasePlayer player)
        {
            var playerheli = player.GetComponent<FlyHelicopter>();
            if (playerheli != null)
            {
                playerheli.DestroyCui(player);
                GameObject.Destroy(playerheli);
            }
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            RemoveHeliComponents(player);
        }

        private void OnPlayerRespawned(BasePlayer player)
        {
            RemoveHeliComponents(player);
        }
    }
}
