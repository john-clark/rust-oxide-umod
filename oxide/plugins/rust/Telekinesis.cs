using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Telekinesis", "redBDGR", "2.0.8")]
    [Description("Control objects with your mind!")]
    class Telekinesis : RustPlugin
    {
        private bool Changed;
        private static Telekinesis plugin;
        private const string permissionNameADMIN = "telekinesis.admin";
        private const string permissionNameRESTRICTED = "telekinesis.restricted";

        private float maxDist = 3f;
        private float autoDisableLength = 60f;
        private bool restrictedBuildingAuthOnly = true;
        private bool restrictedCanMoveBasePlayers = false;
        private bool restrictedOwnerIdOnly;
        private float restrictedGrabDistance = 20f;

        private Dictionary<string, BaseEntity> grabList = new Dictionary<string, BaseEntity>();
        private Dictionary<string, UndoInfo> undoDic = new Dictionary<string, UndoInfo>();

        private class UndoInfo
        {
            public Vector3 pos;
            public Quaternion rot;
            public BaseEntity entity;
        }

        private void Init()
        {
            plugin = this;
            permission.RegisterPermission(permissionNameADMIN, this);
            permission.RegisterPermission(permissionNameRESTRICTED, this);
            LoadVariables();

            lang.RegisterMessages(new Dictionary<string, string>
            {
                //chat
                ["No Permission"] = "You are not allowed to use this command!",
                ["Grab tool start"] = "The telekinesis tool has been enabled",
                ["Grab tool end"] = "The telekinesis tool has been disabled",
                ["Invalid entity"] = "No valid entity was found",
                ["Building Blocked"] = "You are not allowed to use this tool if you are building blocked",
                ["No Undo Found"] = "No undo data was found!",
                ["Undo Success"] = "Your last telekinesis movement was undone",
                ["TLS Mode Changed"] = "Current mode: {0}",
            }, this);
        }

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }

        private void LoadVariables()
        {
            maxDist = Convert.ToSingle(GetConfig("Settings", "Restricted max distance", 3f));
            autoDisableLength = Convert.ToSingle(GetConfig("Settings", "Auto disable length", 60f));
            restrictedBuildingAuthOnly = Convert.ToBoolean(GetConfig("Settings", "Restricted Cannot Use If Building Blocked", true));
            restrictedOwnerIdOnly = Convert.ToBoolean(GetConfig("Settings", "Restricted OwnerID Only", false));
            restrictedGrabDistance = Convert.ToSingle(GetConfig("Settings", "Restricted Grab Distance", 20f));
            restrictedCanMoveBasePlayers = Convert.ToBoolean(GetConfig("Settings", "Restricted Cannot Move Players (Sleeping or Awake)", false));

            if (!Changed) return;
            SaveConfig();
            Changed = false;
        }

        private void OnEntityKill(BaseNetworkable entity)
        {
            BaseEntity ent = entity as BaseEntity;
            if (ent == null)
                return;
            TelekinesisComponent tls = ent.GetComponent<TelekinesisComponent>();
            if (!tls)
                return;
            tls.DestroyThis();
            /*
            string x = "0";
            foreach (var entry in grabList)
                if (ent = entry.Value)
                    x = entry.Key;
            if (x != "0")
            {
                grabList.Remove(x);
                undoDic.Remove(x);
            }
            */
        }

        [ChatCommand("tls")]
        private void GrabCMD(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, permissionNameADMIN) && !permission.UserHasPermission(player.UserIDString, permissionNameRESTRICTED))
            {
                player.ChatMessage(msg("No Permission", player.UserIDString));
                return;
            }
            if (restrictedBuildingAuthOnly)
                if (permission.UserHasPermission(player.UserIDString, permissionNameRESTRICTED))
                    if (!player.CanBuild())
                    {
                        player.ChatMessage(msg("Building Blocked", player.UserIDString));
                        return;
                    }
            if (args.Length == 1)
                if (args[0] == "undo")
                {
                    if (!undoDic.ContainsKey(player.UserIDString))
                    {
                        player.ChatMessage(msg("No Undo Found", player.UserIDString));
                        return;
                    }
                    if (!undoDic[player.UserIDString].entity.IsValid())
                        return;
                    undoDic[player.UserIDString].entity.GetComponent<TelekinesisComponent>().DestroyThis();
                    undoDic[player.UserIDString].entity.transform.position = undoDic[player.UserIDString].pos;
                    undoDic[player.UserIDString].entity.transform.rotation = undoDic[player.UserIDString].rot;
                    undoDic[player.UserIDString].entity.SendNetworkUpdate();
                    player.ChatMessage(msg("Undo Success", player.UserIDString));
                    undoDic.Remove(player.UserIDString);
                    return;
                }
            if (grabList.ContainsKey(player.UserIDString))
            {
                BaseEntity ent = grabList[player.UserIDString];
                TelekinesisComponent grab = ent.GetComponent<TelekinesisComponent>();
                if (grab)
                    grab.DestroyThis();
                grabList.Remove(player.UserIDString);
                return;
            }
            BaseEntity grabEnt = GrabEntity(player);
            if (grabEnt == null)
            {
                player.ChatMessage(msg("Invalid entity", player.UserIDString));
                return;
            }
            RemoveActiveItem(player);
            player.ChatMessage(msg("Grab tool start", player.UserIDString));
        }

        // Active item removal code courtesy of Fujikura
        private void RemoveActiveItem(BasePlayer player)
        {
            foreach (var item in player.inventory.containerBelt.itemList.Where(x => x.IsValid() && x.GetHeldEntity()).ToList())
            {
                var slot = item.position;
                item.RemoveFromContainer();
                item.MarkDirty();
                timer.Once(0.15f, () =>
                {
                    if (item == null)
                        return;
                    item.MoveToContainer(player.inventory.containerBelt, slot);
                    item.MarkDirty();
                });
            }
        }

        private BaseEntity GrabEntity(BasePlayer player)
        {
            BaseEntity ent = FindEntity(player);
            if (ent == null)
                return null;
            if (PlayerIsRestricted(player))
            {
                if (restrictedOwnerIdOnly) // Target object ID restriction
                    if (ent.OwnerID != player.userID)
                        return null;
                if (Vector3.Distance(ent.transform.position, player.transform.position) >= restrictedGrabDistance) // Distance restriction
                    return null;
                if (!restrictedCanMoveBasePlayers)
                    if (ent.GetComponent<BasePlayer>() != null)
                        return null;
            }
            TelekinesisComponent grab = ent.gameObject.AddComponent<TelekinesisComponent>();
            grab.originPlayer = player;
            if (undoDic.ContainsKey(player.UserIDString))
                undoDic[player.UserIDString] = new UndoInfo { pos = ent.transform.position, rot = ent.transform.rotation, entity = ent };
            else
                undoDic.Add(player.UserIDString, new UndoInfo { pos = ent.transform.position, rot = ent.transform.rotation, entity = ent });
            grabList.Add(player.UserIDString, ent);
            timer.Once(autoDisableLength, () =>
            {
                if (grab)
                    grab.DestroyThis();
            });
            return ent;
        }

        private bool PlayerIsRestricted(BasePlayer player)
        {
            return permission.UserHasPermission(player.UserIDString, permissionNameRESTRICTED);
        }

        private static BaseEntity FindEntity(BasePlayer player)
        {
            RaycastHit hit;
            if (!Physics.Raycast(player.eyes.HeadRay(), out hit)) return null;
            if (hit.GetEntity() == null)
                return null;
            BaseEntity entity = hit.GetEntity();
            return entity;
        }

        private class TelekinesisComponent : MonoBehaviour
        {
            public BasePlayer originPlayer;
            private BaseEntity target;
            private StabilityEntity stab;
            private float entDis = 2f;
            private float vertOffset = 1f;
            private float maxDis = plugin.maxDist;
            private bool isRestricted;
            private float nextTime;
            private string mode = "distance";

            private void Awake()
            {
                nextTime = Time.time + 0.5f;
                target = gameObject.GetComponent<BaseEntity>();
                stab = target?.GetComponent<StabilityEntity>();

                plugin.NextTick(() =>
                {
                    if (!originPlayer) return;
                    if (plugin.permission.UserHasPermission(originPlayer.UserIDString, permissionNameRESTRICTED))
                        isRestricted = true;
                });
            }

            private void Update()
            {
                if (originPlayer == null)
                    return;
                if (isRestricted)
                    if (originPlayer.CanBuild() == false)
                    {
                        DestroyThis();
                        return;
                    }
                if (originPlayer.serverInput.IsDown(BUTTON.RELOAD))
                {
                    if (Time.time > nextTime)
                    {
                        switch (mode)
                        {
                            case "distance":
                                mode = "rotate (horizontal)";
                                originPlayer.ChatMessage(string.Format(plugin.msg("TLS Mode Changed", originPlayer.UserIDString), mode));
                                break;
                            case "rotate (horizontal)":
                                mode = "rotate (vertical)";
                                originPlayer.ChatMessage(string.Format(plugin.msg("TLS Mode Changed", originPlayer.UserIDString), mode));
                                break;
                            case "rotate (vertical)":
                                mode = "rotate (horizontal2)";
                                originPlayer.ChatMessage(string.Format(plugin.msg("TLS Mode Changed", originPlayer.UserIDString), mode));
                                break;
                            case "rotate (horizontal2)":
                                mode = "vertical offset";
                                originPlayer.ChatMessage(string.Format(plugin.msg("TLS Mode Changed", originPlayer.UserIDString), mode));
                                break;
                            case "vertical offset":
                                mode = "distance";
                                originPlayer.ChatMessage(string.Format(plugin.msg("TLS Mode Changed", originPlayer.UserIDString), mode));
                                break;
                        }
                        nextTime = Time.time + 0.5f;
                    }
                }
                if (originPlayer.serverInput.IsDown(BUTTON.FIRE_PRIMARY))
                {
                    switch (mode)
                    {
                        case "distance":
                            if (isRestricted)
                            {
                                if (entDis <= maxDis)
                                    entDis = entDis + 0.01f;
                            }
                            else
                                entDis = entDis + 0.01f;
                            break;
                        case "rotate (horizontal)":
                            gameObject.transform.Rotate(0, +0.5f, 0);
                            break;
                        case "rotate (vertical)":
                            gameObject.transform.Rotate(0, 0, -0.5f);
                            break;
                        case "rotate (horizontal2)":
                            gameObject.transform.Rotate(+0.5f, 0, 0);
                            break;
                        case "vertical offset":
                            vertOffset += 0.02f;
                            break;
                    }
                }
                if (originPlayer.serverInput.IsDown(BUTTON.FIRE_SECONDARY))
                {
                    switch (mode)
                    {
                        case "distance":
                            entDis = entDis - 0.01f;
                            break;
                        case "rotate (horizontal)":
                            gameObject.transform.Rotate(0, -0.5f, 0);
                            break;
                        case "rotate (vertical)":
                            gameObject.transform.Rotate(0, 0, +0.5f);
                            break;
                        case "rotate (horizontal2)":
                            gameObject.transform.Rotate(-0.5f, 0, 0);
                            break;
                        case "vertical offset":
                            vertOffset -= 0.02f;
                            break;
                    }
                }
                //if (!rotateMode)
                    //gameObject.transform.LookAt(originPlayer.transform);
                //else
                    //gameObject.transform.Rotate(gameObject.transform.rotation.x, roty, gameObject.transform.rotation.z);
                target.transform.position = Vector3.Lerp(target.transform.position, originPlayer.transform.position + originPlayer.eyes.HeadRay().direction * entDis + new Vector3(0, vertOffset, 0), UnityEngine.Time.deltaTime * 15f);
                if (stab?.grounded == false)
                    stab.grounded = true;

                target.transform.hasChanged = true;
                target.UpdateNetworkGroup();
                target.SendNetworkUpdateImmediate();
            }

            public void DestroyThis()
            {
                //if (plugin.undoDic.ContainsKey(originPlayer.UserIDString))
                //    plugin.undoDic.Remove(originPlayer.UserIDString);
                if (plugin.grabList.ContainsKey(originPlayer.UserIDString))
                    plugin.grabList.Remove(originPlayer.UserIDString);
                originPlayer.ChatMessage(plugin.msg("Grab tool end", originPlayer.UserIDString));
                Destroy(this);
            }
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

        string msg(string key, string id = null) => lang.GetMessage(key, this, id);
    }
}
