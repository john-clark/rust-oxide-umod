// Reference: Rust.Global
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using Rust;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Shrinking Radiation Zone", "k1lly0u", "0.1.2", ResourceId = 2214)]
    class ShrinkingRadZone : RustPlugin
    {
        #region Fields
        static ShrinkingRadZone instance;
        private Dictionary<string, ShrinkZone> activeZones;

        private const string sphereEnt = "assets/prefabs/visualization/sphere.prefab";
        #endregion

        #region Classes
        class ShrinkZone : MonoBehaviour
        {
            private string zoneId;
            private Vector3 position;
            private float initialRadius;
            private float modifiedRadius;
            private float targetRadius;

            private bool isDisabled;

            private float timeToTake;
            private float timeTaken;

            private float radStrength;
            private float bufferSize;

            private SphereEntity innerSphere;
            private SphereEntity outerSphere;
            private SphereCollider innerCollider;
            private TriggerRadiation radTrigger;

            void Awake()
            {
                gameObject.layer = (int)Layer.Reserved1;
                gameObject.name = $"ShrinkZone {UnityEngine.Random.Range(0, int.MaxValue)}";
                enabled = false;

                isDisabled = true;
                bufferSize = instance.configData.RadiationBuffer;
                radStrength = instance.configData.RadiationStrength;

                targetRadius = instance.configData.FinalZoneSize;
                timeToTake = instance.configData.ShrinkTime;
                timeTaken = 0;
            }
            void OnDestroy()
            {
                CancelInvoke();
                innerSphere.Kill();
                outerSphere.Kill();
                Destroy(gameObject);
            }

            void Update()
            {
                if (isDisabled) return;

                timeTaken = timeTaken + UnityEngine.Time.deltaTime;
                float single = Mathf.InverseLerp(0f, timeToTake, timeTaken);

                modifiedRadius = initialRadius * (1 - single);

                innerSphere.currentRadius = (initialRadius * 2) * (1 - single);
                //outerSphere.currentRadius = ((initialRadius * 2) * (1 - single)) + bufferSize;

                innerSphere.SendNetworkUpdateImmediate();
                //outerSphere.SendNetworkUpdateImmediate();

                innerCollider.radius = modifiedRadius;
                //radTrigger.radiationSize = modifiedRadius + bufferSize;

                if (modifiedRadius <= targetRadius)
                {
                    isDisabled = true;
                    RadzoneFinished();
                }
            }
            void OnTriggerEnter(Collider obj)
            {
                var player = obj?.GetComponentInParent<BasePlayer>();
                if (player != null)
                {
                    RemoveFromTrigger(player);
                }
            }
            void OnTriggerExit(Collider obj)
            {
                var player = obj?.GetComponentInParent<BasePlayer>();
                if (player != null)
                {
                    AddToTrigger(player);
                }
            }
            public void CreateZones(string zoneId, Vector3 position, float initialRadius, float timeToTake)
            {
                transform.position = position;
                transform.rotation = new Quaternion();

                this.zoneId = zoneId;
                this.initialRadius = initialRadius;
                this.timeToTake = timeToTake;

                innerSphere = (SphereEntity)GameManager.server.CreateEntity(sphereEnt, position, new Quaternion(), true);
                innerSphere.currentRadius = initialRadius * 2;
                innerSphere.lerpSpeed = 0;
                innerSphere.enableSaving = false;
                innerSphere.Spawn();

                var innerRB = innerSphere.gameObject.AddComponent<Rigidbody>();
                innerRB.useGravity = false;
                innerRB.isKinematic = true;

                innerCollider = gameObject.AddComponent<SphereCollider>();
                innerCollider.transform.position = innerSphere.transform.position;
                innerCollider.isTrigger = true;
                innerCollider.radius = initialRadius;

                outerSphere = (SphereEntity)GameManager.server.CreateEntity(sphereEnt, position, new Quaternion(), true);
                outerSphere.currentRadius = (initialRadius * 2) + bufferSize;
                outerSphere.lerpSpeed = 0;
                outerSphere.enableSaving = false;
                outerSphere.Spawn();

                radTrigger = outerSphere.gameObject.AddComponent<TriggerRadiation>();
                radTrigger.RadiationAmountOverride = radStrength;
                radTrigger.radiationSize = initialRadius + bufferSize;
                radTrigger.interestLayers = LayerMask.GetMask("Player (Server)");
                radTrigger.enabled = true;

                isDisabled = false;

                gameObject.SetActive(true);
                enabled = true;
            }
            public void PauseShrink()
            {
                if (!isDisabled)
                    isDisabled = true;
                else isDisabled = false;
            }
            void RemoveFromTrigger(BasePlayer player)
            {
                player.LeaveTrigger(radTrigger);
            }
            void AddToTrigger(BasePlayer player)
            {
                player.EnterTrigger(radTrigger);
            }
            private void RadzoneFinished()
            {
                Interface.CallHook("RadzoneEnd", zoneId);
            }
        }
        #endregion

        #region Oxide Hooks
        void Loaded()
        {
            activeZones = new Dictionary<string, ShrinkZone>();
            permission.RegisterPermission("shrinkingradzone.use", this);
            lang.RegisterMessages(messages, this);
            instance = this;
        }
        void OnServerInitialized() => LoadVariables();
        void Unload()
        {
            var shrinkZones = UnityEngine.Object.FindObjectsOfType<ShrinkZone>();
            if (shrinkZones != null)
                foreach (var obj in shrinkZones)
                    UnityEngine.Object.Destroy(obj);
        }
        #endregion

        #region Functions
        public void RemoveRadzone(string id)
        {
            if (activeZones.ContainsKey(id))
            {
                UnityEngine.Object.DestroyImmediate(activeZones[id]);
                activeZones.Remove(id);
            }
        }
        #endregion

        #region API
        private string CreateShrinkZone(Vector3 position, float radius, float time)
        {
            var zoneId = CuiHelper.GetGuid();
            var zone = new GameObject().gameObject.AddComponent<ShrinkZone>();
            activeZones.Add(zoneId, zone);
            zone.CreateZones(zoneId, position, radius, time);
            return zoneId;
        }
        private void ToggleZoneShrink(string id)
        {
            if (activeZones.ContainsKey(id))
            {
                activeZones[id].PauseShrink();
            }
        }
        private void DestroyShrinkZone(string id)
        {
            if (activeZones.ContainsKey(id))
            {
                UnityEngine.Object.DestroyImmediate(activeZones[id]);
                activeZones.Remove(id);
            }
        }
        #endregion

        #region Commands
        [ChatCommand("shrink")]
        void cmdShrink(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin && !permission.UserHasPermission(player.UserIDString, "shrinkingradzone.use")) return;
            if (args.Length == 0)
            {
                SendReply(player, $"<color=#00CC00>{Title}</color>  <color=#939393>v</color><color=#00CC00>{Version}</color> <color=#939393>-</color> <color=#00CC00>{Author}</color>");
                SendReply(player, msg("/shrink on me - Starts a shrinking rad zone on your position", player.UserIDString));
                SendReply(player, msg("/shrink on <x> <z> - Starts a shrinking rad zone on the specified position", player.UserIDString));
                SendReply(player, msg("/shrink stop - Destroys all active zones", player.UserIDString));
                SendReply(player, msg("/shrink buffer <## value> - Set the radiation buffer size", player.UserIDString));
                SendReply(player, msg("/shrink startsize <## value> - Set the initial zone size", player.UserIDString));
                SendReply(player, msg("/shrink endsize <## value> - Set the final zone size", player.UserIDString));
                SendReply(player, msg("/shrink strength <## value> - Set the radiation strength (rads per second)", player.UserIDString));
                SendReply(player, msg("/shrink time <## value>  - Set the time it takes to shrink (in seconds)", player.UserIDString));
                return;
            }
            switch (args[0].ToLower())
            {
                case "on":
                    if (args.Length >= 2)
                    {
                        object position = null;
                        if (args[1].ToLower() == "me")
                            position = player.transform.position;
                        else if (args.Length > 2)
                        {
                            float x;
                            float z;
                            if (float.TryParse(args[1], out x) && float.TryParse(args[2], out z))
                            {
                                var temp = new Vector3(x, 0, z);
                                var height = TerrainMeta.HeightMap.GetHeight((Vector3)temp);
                                position = new Vector3(x, height, z);
                            }
                        }
                        if (position is Vector3)
                        {
                            CreateShrinkZone((Vector3) position, configData.InitialZoneSize, configData.ShrinkTime);
                            SendReply(player, "Zone Created!");
                            return;
                        }
                    }
                    else
                    {
                        SendReply(player, "/shrink on me\n/shrink on <x> <z>");
                        return;
                    }
                    return;
                case "stop":
                    foreach (var zone in activeZones)
                    {
                        UnityEngine.Object.DestroyImmediate(zone.Value);
                        Interface.CallHook("RadzoneEnd", zone.Key);
                    }

                    activeZones.Clear();
                    SendReply(player, msg("All zones destroyed"));
                    return;
                case "buffer":
                    if (args.Length == 2)
                    {
                        float value;
                        if (float.TryParse(args[1], out value))
                        {
                            configData.RadiationBuffer = value;
                            SaveConfig();
                            SendReply(player, string.Format(msg("Radiation buffer set to : {0}", player.UserIDString), value));
                        }
                        else SendReply(player, msg("You must enter a number value", player.UserIDString));
                    }
                    else SendReply(player, msg("/shrink buffer <##>", player.UserIDString));
                    return;
                case "startsize":
                    if (args.Length == 2)
                    {
                        float value;
                        if (float.TryParse(args[1], out value))
                        {
                            configData.InitialZoneSize = value;
                            SaveConfig();
                            SendReply(player, string.Format(msg("Initial size set to : {0}", player.UserIDString), value));
                        }
                        else SendReply(player, msg("You must enter a number value", player.UserIDString));
                    }
                    else SendReply(player, msg("/shrink startsize <##>", player.UserIDString));
                    return;
                case "endsize":
                    if (args.Length == 2)
                    {
                        float value;
                        if (float.TryParse(args[1], out value))
                        {
                            configData.FinalZoneSize = value;
                            SaveConfig();
                            SendReply(player, string.Format(msg("Final size set to : {0}", player.UserIDString), value));
                        }
                        else SendReply(player, msg("You must enter a number value", player.UserIDString));
                    }
                    else SendReply(player, msg("/shrink endsize <##>", player.UserIDString));
                    return;
                case "strength":
                    if (args.Length == 2)
                    {
                        float value;
                        if (float.TryParse(args[1], out value))
                        {
                            configData.RadiationStrength = value;
                            SaveConfig();
                            SendReply(player, string.Format(msg("Radiation strength set to : {0}", player.UserIDString), value));
                        }
                        else SendReply(player, msg("You must enter a number value", player.UserIDString));
                    }
                    else SendReply(player, msg("/shrink strength <##>", player.UserIDString));
                    return;
                case "time":
                    if (args.Length == 2)
                    {
                        float value;
                        if (float.TryParse(args[1], out value))
                        {
                            configData.ShrinkTime = value;
                            SaveConfig();
                            SendReply(player, string.Format(msg("Shrink time set to : {0}", player.UserIDString), value));
                        }
                        else SendReply(player, msg("You must enter a number value", player.UserIDString));
                    }
                    else SendReply(player, msg("/shrink time <##>", player.UserIDString));
                    return;
                default:
                    break;
            }
        }
        [ConsoleCommand("shrink")]
        void ccmdShrink(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null) return;
            if (arg.Args.Length == 0)
            {
                SendReply(arg, $"{Title}  v{Version} - {Author}");
                SendReply(arg, "shrink on <x> <z> - Starts a shrinking rad zone on the specified position");
                SendReply(arg, "shrink stop - Destroys all active zones");
                SendReply(arg, "shrink buffer <## value> - Set the radiation buffer size");
                SendReply(arg, "shrink startsize <## value> - Set the initial zone size");
                SendReply(arg, "shrink endsize <## value> - Set the final zone size");
                SendReply(arg, "shrink strength <## value> - Set the radiation strength (rads per second)");
                SendReply(arg, "shrink time <## value>  - Set the time it takes to shrink (in seconds)");
                return;
            }
            switch (arg.Args[0].ToLower())
            {
                case "on":
                    if (arg.Args.Length >= 2)
                    {
                        object position = null;
                        if (arg.Args.Length > 2)
                        {
                            float x;
                            float z;
                            if (float.TryParse(arg.Args[1], out x) && float.TryParse(arg.Args[2], out z))
                            {
                                var temp = new Vector3(x, 0, z);
                                var height = TerrainMeta.HeightMap.GetHeight((Vector3)temp);
                                position = new Vector3(x, height, z);
                            }
                        }
                        if (position is Vector3)
                        {
                            CreateShrinkZone((Vector3)position, configData.InitialZoneSize, configData.ShrinkTime);
                            SendReply(arg, "Zone Created!");
                            return;
                        }
                    }
                    else
                    {
                        SendReply(arg, "/shrink on me\n/shrink on <x> <z>");
                        return;
                    }
                    return;
                case "stop":
                    foreach (var zone in activeZones)
                    {
                        UnityEngine.Object.DestroyImmediate(zone.Value);
                        Interface.CallHook("RadzoneEnd", zone.Key);
                    }

                    activeZones.Clear();
                    SendReply(arg, msg("All zones destroyed"));
                    return;
                case "buffer":
                    if (arg.Args.Length == 2)
                    {
                        float value;
                        if (float.TryParse(arg.Args[1], out value))
                        {
                            configData.RadiationBuffer = value;
                            SaveConfig();
                            SendReply(arg, string.Format(msg("Radiation buffer set to : {0}"), value));
                        }
                        else SendReply(arg, msg("You must enter a number value"));
                    }
                    else SendReply(arg, msg("shrink buffer <##>"));
                    return;
                case "startsize":
                    if (arg.Args.Length == 2)
                    {
                        float value;
                        if (float.TryParse(arg.Args[1], out value))
                        {
                            configData.InitialZoneSize = value;
                            SaveConfig();
                            SendReply(arg, string.Format(msg("Initial size set to : {0}"), value));
                        }
                        else SendReply(arg, msg("You must enter a number value"));
                    }
                    else SendReply(arg, msg("shrink startsize <##>"));
                    return;
                case "endsize":
                    if (arg.Args.Length == 2)
                    {
                        float value;
                        if (float.TryParse(arg.Args[1], out value))
                        {
                            configData.FinalZoneSize = value;
                            SaveConfig();
                            SendReply(arg, string.Format(msg("Final size set to : {0}"), value));
                        }
                        else SendReply(arg, msg("You must enter a number value"));
                    }
                    else SendReply(arg, msg("shrink endsize <##>"));
                    return;
                case "strength":
                    if (arg.Args.Length == 2)
                    {
                        float value;
                        if (float.TryParse(arg.Args[1], out value))
                        {
                            configData.RadiationStrength = value;
                            SaveConfig();
                            SendReply(arg, string.Format(msg("Radiation strength set to : {0}"), value));
                        }
                        else SendReply(arg, msg("You must enter a number value"));
                    }
                    else SendReply(arg, msg("shrink strength <##>"));
                    return;
                case "time":
                    if (arg.Args.Length == 2)
                    {
                        float value;
                        if (float.TryParse(arg.Args[1], out value))
                        {
                            configData.ShrinkTime = value;
                            SaveConfig();
                            SendReply(arg, string.Format(msg("Shrink time set to : {0}"), value));
                        }
                        else SendReply(arg, msg("You must enter a number value"));
                    }
                    else SendReply(arg, msg("shrink time <##>"));
                    return;
                default:
                    break;
            }
        }
        #endregion

        #region Config
        private ConfigData configData;
        class ConfigData
        {
            public float RadiationBuffer { get; set; }
            public float FinalZoneSize { get; set; }
            public float InitialZoneSize { get; set; }
            public float ShrinkTime { get; set; }
            public float RadiationStrength { get; set; }
        }
        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }
        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                FinalZoneSize = 20,
                InitialZoneSize = 150,
                RadiationBuffer = 50,
                RadiationStrength = 40,
                ShrinkTime = 120
            };
            SaveConfig(config);
        }
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion

        #region Localization
        string msg(string key, string playerId = null) => lang.GetMessage(key, this, playerId);

        Dictionary<string, string> messages = new Dictionary<string, string>
        {
            {"Radiation strength set to : {0}","Radiation strength set to : {0}" },
            {"You must enter a number value","You must enter a number value" },
            {"Shrink time set to : {0}","Shrink time set to : {0}" },
            {"Final size set to : {0}","Final size set to : {0}" },
            {"Initial size set to : {0}","Initial size set to : {0}" },
            {"Radiation buffer set to : {0}","Radiation buffer set to : {0}" },
            {"Zone Created!","Zone Created!" },
            {"/shrink on me - Starts a shrinking rad zone on your position","/shrink on me - Starts a shrinking rad zone on your position" },
            {"/shrink on <x> <z> - Starts a shrinking rad zone on the specified position","/shrink on <x> <z> - Starts a shrinking rad zone on the specified position" },
            {"/shrink buffer <## value> - Set the radiation buffer size","/shrink buffer <## value> - Set the radiation buffer size" },
            {"/shrink startsize <## value> - Set the initial zone size","/shrink startsize <## value> - Set the initial zone size" },
            {"/shrink endsize <## value> - Set the final zone size","/shrink endsize <## value> - Set the final zone size" },
            {"/shrink strength <## value> - Set the radiation strength (rads per second)","/shrink strength <## value> - Set the radiation strength (rads per second)" },
            {"/shrink time <## value>  - Set the time it takes to shrink (in seconds)","/shrink time <## value>  - Set the time it takes to shrink (in seconds)" },
            {"All zones destroyed","All zones destroyed" }
        };
        #endregion
    }
}