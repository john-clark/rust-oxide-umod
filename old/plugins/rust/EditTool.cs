using Network;
using Newtonsoft.Json;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using ProtoBuf;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("EditTool", "Jake Rich", "1.0.2", ResourceId = 2743)]
    [Description("Modify Entities In A Map!")]

    public class EditTool : RustPlugin
    {
        public static ulong ToolSkinID = 1175592586;
        public static string ToolItemShortname = "coal";
        public static string ToolName = "Edit Tool";
        public static EditTool _plugin;
        public static float EditToolDistance = 50f;
        public static UIManager UI;
        public static PlayerDataController<ToolPlayerData> PlayerData;

        void Init()
        {
            _plugin = this;
            UI = new UIManager();
            PlayerData = new PlayerDataController<ToolPlayerData>();
            permission.RegisterPermission("edittool.use", this);
            for(var mode = ToolMode.Transform; mode < ToolMode.Last; mode++)
            {
                permission.RegisterPermission($"edittool.{mode.ToString().ToLower()}", this);
            }
        }

        void Unload()
        {
            UI.Unload();
        }

        void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (!player.IsAdmin)
            {
                if (!permission.UserHasPermission(player.UserIDString, "edittool.use"))
                {
                    return;
                }
            }
            if (!HasEditTool(player))
            {
                UI.ToolUI.Hide(player);
                UI.EntityInfoLabel.Text = "";
                UI.EntityInfoLabel.Refresh(player);
                return;
            }

            UI.ToolUI.Show(player);

            var data = PlayerData.Get(player);
            if (input.WasJustPressed(BUTTON.FIRE_THIRD))
            {
                data.SwitchMode();
            }

            BaseEntity entity;
            Vector3 hitPoint;
            if (!GetEntityLookingAt(player, out entity, out hitPoint, EditToolDistance))
            {
                NotLookingAtEntity(player);
            }
            if (entity == null)
            {
                NotLookingAtEntity(player);
            }
            else
            {
                LookingAtEntity(player, entity);
                UI.EntityInfoLabel.Refresh(player);
            }

            if (input.WasJustPressed(BUTTON.FIRE_PRIMARY))
            {
                if (input.IsDown(BUTTON.SPRINT))
                {
                    data.OnShiftLeftClick(entity, hitPoint);
                }
                else if (input.IsDown(BUTTON.RELOAD))
                {
                    data.OnReloadClick(entity, hitPoint);
                }
                else
                {
                    data.OnLeftClick(entity, hitPoint);
                }
            }
            if (input.WasJustPressed(BUTTON.FIRE_SECONDARY))
            {
                if (input.IsDown(BUTTON.SPRINT))
                {
                    data.OnShiftRightClick(entity, hitPoint);
                }
                else
                {
                    data.OnRightClick(entity, hitPoint);
                }
            }
            if (input.WasJustReleased(BUTTON.FIRE_PRIMARY))
            {
                data.OnLeftRelease();
            }
            if (input.WasJustReleased(BUTTON.FIRE_SECONDARY))
            {
                data.OnRightRelease();
            }
            data.OnPlayerInput(entity, input);
        }

        void OnEntityKill(BaseNetworkable entity)
        {
            var node = entity as OreResourceEntity;
            if (node == null)
            {
                return;
            }
            node._hotSpot?.Kill();
        }

        void NotLookingAtEntity(BasePlayer player)
        {
            UI.EntityInfoLabel.Text = "";
            UI.EntityInfoLabel.Refresh(player);
        }

        void LookingAtEntity(BasePlayer player, BaseEntity entity)
        {
            UI.EntityInfoLabel.Text = $"{entity.ShortPrefabName}";
        }

        public static bool IsEditTool(Item item)
        {
            if (item == null)
            {
                return false;
            }
            return item.skin == ToolSkinID;
        }

        public static bool HasEditTool(BasePlayer player)
        {
            var item = player.GetActiveItem();
            if (item == null)
            {
                return false;
            }
            return IsEditTool(item);
        }

        #region Entity Raycast

        //public static int EditToolLayer = LayerMask.GetMask("AI", "Construction", "Deployed", "Default", "Debris", "Ragdoll", "Tree", "Terrain", "World", "Vehicle Movement");
        public static int EditToolLayer = int.MaxValue;

        public static bool GetEntityLookingAt(BasePlayer player, out BaseEntity entity, out Vector3 hitPoint, float distance = 5f, bool announceOnNotFound = false)
        {
            entity = null;
            RaycastHit raycastHit;
            Ray ray = player.eyes.BodyRay();

            if (!Physics.Raycast(ray, out raycastHit, distance,EditToolLayer))
            {
                hitPoint = Vector3.zero;
                return false;
            }
            else
            {
                hitPoint = raycastHit.point;
                entity = raycastHit.GetEntity();
                if (entity != null)
                {
                    return true;
                }
            }
            if (announceOnNotFound)
            {
                _plugin.PrintToChat(player, GetLangMessage("NotLookingAtEntity", player));
            }
            return true;
        }

        #endregion

        [ChatCommand("edittool")]
        void GiveEditTool_Command(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
            {
                return;
            }
            GiveEditTool(player);
        }

        [ConsoleCommand("edittool.switchmode")]
        void SwitchEditToolMode_ConsoleCommand(ConsoleSystem.Arg args)
        {
            var data = PlayerData.Get(args.Player());
            if (data == null)
            {
                return;
            }
            data.SwitchMode();
        }

        public void GiveEditTool(BasePlayer player)
        {
            Item item = ItemManager.CreateByPartialName(ToolItemShortname, 1);
            if (item == null)
            {
                PrintError("WARNING! Failed to create edit tool!");
                return;
            }
            item.skin = ToolSkinID;
            item.name = ToolName;
            player.GiveItem(item);
        }

        public enum ToolMode
        {
            None = 0,
            Transform = 1,
            Clone = 2,
            EditData = 3,
            Spawn = 4,
            Diguise = 5,
            Last
        }

        public class ToolPlayerData : PlayerDataBase
        {
            public ToolMode Mode = ToolMode.None;
            //public string SavedPrefabName = "";
            //private Vector3 SavedPrefabPosition;
            //private Quaternion SavedPrefabRotation;
            //private byte[] savedBytes;
            //private BuildingGrade grade;
            private BaseEntity selectedEntity;
            private Vector3 lastTransformPosition;
            private Vector3 transformOffset;
            private float TranslateDistance;
            private EntitySaver _entitySaver = new EntitySaver();
            private Quaternion _lastRotation;

            public void SwitchMode()
            {
                if (!Player.IsAdmin)
                {
                    if (!_plugin.permission.UserHasPermission(Player.UserIDString, "edittool.use"))
                    {
                        Mode = ToolMode.None;
                        return;
                    }
                }
                OnLeaveMode(Mode);
                int loops = 0;
                while (loops < (int)ToolMode.Last * 2)
                {
                    Mode++;
                    loops++;
                    if (Mode >= ToolMode.Last)
                    {
                        Mode = ToolMode.Transform;
                    }
                    if (Mode == ToolMode.EditData)
                    {
                        if (EntityPropertiesLoaded() == false)
                        {
                            continue;
                        }
                    }
                    if (Mode == ToolMode.Spawn)
                    {
                        continue;
                    }
                    if (Mode == ToolMode.Diguise)
                    {
                        if (DisguiseLoaded() == false)
                        {
                            continue;
                        }
                    }

                    //Permission check
                    if (!Player.IsAdmin)
                    {
                        if (!_plugin.permission.UserHasPermission(Player.UserIDString, $"edittool.{Mode.ToString().ToLower()}"))
                        {
                            continue;
                        }
                    }
                    OnEnterMode(Mode);
                    break;
                }
            }

            public void OnLeaveMode(ToolMode mode)
            {
                DeselectEntity();
            }

            public void OnEnterMode(ToolMode mode)
            {
                UI.SwitchToolMode(Player, Mode);
            }

            public void OnShiftRightClick(BaseEntity entity, Vector3 hitPoint)
            {
                switch (Mode)
                {
                    case ToolMode.Clone:
                        {
                            if (entity == null)
                            {
                                return;
                            }
                            SavePrefabName(entity, true);
                            break;
                        }
                    case ToolMode.EditData:
                        {
                            selectedEntity = entity;
                            break;
                        }
                }
            }

            public void OnShiftLeftClick(BaseEntity entity, Vector3 hitPoint)
            {
                switch (Mode)
                {
                    case ToolMode.Clone:
                        {
                            SpawnPrefab(hitPoint, true);
                            break;
                        }
                    case ToolMode.EditData:
                        {
                            if (entity == null)
                            {
                                return;
                            }
                            CloneEntityData(selectedEntity, entity);
                            break;
                        }
                }
            }

            public void OnLeftClick(BaseEntity entity, Vector3 hitPoint)
            {
                switch (Mode)
                {
                    case ToolMode.Clone:
                        {
                            SpawnPrefab(hitPoint, false);
                            break;
                        }
                    case ToolMode.Transform:
                        {
                            if (entity == null)
                            {
                                return;
                            }
                            SelectEntity(entity);
                            TranslateDistance = Vector3.Distance(hitPoint, Player.eyes.position);
                            transformOffset = hitPoint - selectedEntity.transform.position;
                            break;
                        }
                    case ToolMode.EditData:
                        {
                            if (entity == null)
                            {
                                return;
                            }
                            var obj = new GameObject();
                            //obj.Identity();
                            //obj.SetActive(true);
                            //collider.bounds.SetMinMax(-bounds, bounds);
                            obj.layer = (int)Rust.Layer.Construction;
                            obj.transform.position = Player.transform.position;
                            //var rigid = obj.AddComponent<Rigidbody>();
                            //rigid.isKinematic = true;
                            //rigid.useGravity = false;
                            //rigid.detectCollisions = true;
                            var collider = obj.AddComponent<BoxCollider>();
                            collider.size = new Vector3(3f, 3f, 3f);
                            //collider.isTrigger = false;
                            //collider.enabled = true;
                            obj.SetActive(false);
                            Debug(string.Join(",", obj.GetComponents<Component>().Select(x => x.GetType().ToString()).ToArray()));
                            //Vector3 bounds = new Vector3(3f,3f,3f);
                            //_plugin.Puts($"Added collider! {obj.transform.position} {Player.transform.position} {obj.transform.localScale}");
                            break;
                        }
                }
            }

            public void OnLeftRelease()
            {
                switch(Mode)
                {
                    case ToolMode.Transform:
                        {
                            if (selectedEntity == null)
                            {
                                return;
                            }
                            if (selectedEntity.GetComponent<Rigidbody>() != null)
                            {
                                Vector3 velocity = (selectedEntity.transform.position - lastTransformPosition);
                                Debug($"Setting release velocity {velocity}");
                                selectedEntity.SetVelocity(velocity * 20);
                                selectedEntity.GetComponent<Rigidbody>().useGravity = true;
                            }
                            Debug(string.Join(",",selectedEntity.GetComponents<Component>().Select(x => x.GetType().ToString()).ToArray()));
                            DeselectEntity();
                            break;
                        }
                }
            }

            public void OnRightRelease()
            {

            }

            public void OnRightClick(BaseEntity entity, Vector3 hitPoint)
            {
                switch (Mode)
                {
                    case ToolMode.Clone:
                        {
                            if (entity == null)
                            {
                                return;
                            }
                            SavePrefabName(entity, false);
                            break;
                        }
                    case ToolMode.EditData:
                        {
                            if (entity == null)
                            {
                                return;
                            }
                            StartEditingEntityData(Player, entity);
                            break;
                        }
                }
            }

            public void OnReloadClick(BaseEntity entity, Vector3 hitPoint)
            {

            }

            public void OnPlayerInput(BaseEntity entity, InputState input)
            {
                switch(Mode)
                {
                    case ToolMode.Transform:
                        {
                            if (input.WasJustPressed(BUTTON.FIRE_SECONDARY))
                            {
                                _lastRotation = entity.transform.rotation;
                                SelectEntity(entity);
                            }
                            if (input.WasJustReleased(BUTTON.FIRE_SECONDARY))
                            {
                                DeselectEntity();
                            }
                            float translateSpeed = 0.4f;
                            if (input.IsDown(BUTTON.SPRINT))
                            {
                                translateSpeed = 1.2f;
                            }
                            if (input.IsDown(BUTTON.USE))
                            {
                                TranslateDistance = Mathf.MoveTowards(TranslateDistance, 0, translateSpeed);
                            }
                            if (input.IsDown(BUTTON.RELOAD))
                            {
                                TranslateDistance = Mathf.MoveTowards(TranslateDistance, EditToolDistance, translateSpeed);
                            }
                            if (input.IsDown(BUTTON.FIRE_SECONDARY))
                            {
                                RotationTick(input);
                            }
                            else
                            {
                                TransformTick();
                            }
                            break;
                        }
                }
            }

            private void SelectEntity(BaseEntity entity)
            {
                selectedEntity = entity;
            }

            private void DeselectEntity()
            {
                //Handling in TransformTick and RotationTick as performance impact isn't too bad
                //TryUpdateBuildingBlock(selectedEntity);
                selectedEntity = null;
                lastTransformPosition = default(Vector3);
            }

            private void TryUpdateBuildingBlock(BaseEntity entity)
            {
                var block = entity as BuildingBlock;
                if (block == null)
                {
                    return;
                }
                block.UpdateSkin(true);
            }

            private void TransformTick()
            {
                if (selectedEntity == null)
                {
                    return;
                }
                //_plugin.Puts($"SyncPosition: {selectedEntity.syncPosition}");
                var rigid = selectedEntity.GetComponent<Rigidbody>();
                //Disable rigid body's gravity, as they keep trying to fall when moved around
                if (rigid != null)
                {
                    rigid.useGravity = false;
                }
                lastTransformPosition = selectedEntity.transform.position;
                selectedEntity.transform.position = Player.eyes.BodyRay().origin + Player.eyes.BodyRay().direction * TranslateDistance - transformOffset;
                //Have to manually trigger building blocks to update their collider serverside
                TryUpdateBuildingBlock(selectedEntity);
                if (selectedEntity is AnimatedBuildingBlock)
                {
                    //Have to change entity flags for doors to update their position on client
                    //So just change a useless flag
                    //selectedEntity.SetFlag(BaseEntity.Flags.Open, !selectedEntity.HasFlag(BaseEntity.Flags.Open));
                }
                
                //Manually kill and respawn entities to make them update their position
                if (Net.sv.write.Start())
                {
                    Net.sv.write.PacketID(Message.Type.EntityDestroy);
                    Net.sv.write.UInt32(selectedEntity.net.ID);
                    Net.sv.write.UInt8(0);
                    Net.sv.write.Send(new SendInfo(selectedEntity.net.group.subscribers));
                }
                selectedEntity.SendNetworkUpdateImmediate();
                if (selectedEntity is BuildingBlock)
                {
                    //Have to tell the client to update building block's position on client
                    //selectedEntity.ClientRPC(null, "RefreshSkin");
                }
            }

            private void RotationTick(InputState input)
            {
                float multiplier = 8f;
                if (selectedEntity == null)
                {
                    return;
                }
                Vector3 center = selectedEntity.CenterPoint();

                //This will be used to fix the entity rotating on the players look angle
                Vector3 eyeLeveled = Player.eyes.position;
                Vector3 centerLeveled = center;
                eyeLeveled.y = 0;
                centerLeveled.y = 0;
                Ray ray = new Ray(eyeLeveled, eyeLeveled - centerLeveled);

                Vector3 difference = (new Vector3((input.previous.aimAngles.x - input.current.aimAngles.x) * -1f,
                                                  (input.previous.aimAngles.y - input.current.aimAngles.y),
                                                   0)) * multiplier;
                selectedEntity.transform.RotateAround(center, Player.eyes.BodyRight(), (input.previous.aimAngles.x - input.current.aimAngles.x) * multiplier);
                selectedEntity.transform.RotateAround(center, Player.eyes.BodyUp(), (input.previous.aimAngles.y - input.current.aimAngles.y) * multiplier);
                float rollRotate = 0f;
                if (input.IsDown(BUTTON.USE))
                {
                    rollRotate = 2f;
                }
                if (input.IsDown(BUTTON.RELOAD))
                {
                    rollRotate = -2f;
                }
                if (input.IsDown(BUTTON.SPRINT))
                {
                    rollRotate *= 4f;
                }
                selectedEntity.transform.RotateAround(center, Player.eyes.BodyForward(), rollRotate);
                if (input.IsDown(BUTTON.DUCK))
                {
                    float rotMuli = 9f;
                    selectedEntity.transform.rotation = Quaternion.Euler(Mathf.Round(selectedEntity.transform.rotation.x * rotMuli) / rotMuli,
                                                                         Mathf.Round(selectedEntity.transform.rotation.y * rotMuli) / rotMuli,
                                                                         Mathf.Round(selectedEntity.transform.rotation.z * rotMuli) / rotMuli);
                }


                TryUpdateBuildingBlock(selectedEntity);
                //Manually kill and respawn entities to make them update their position
                if (Net.sv.write.Start())
                {
                    Net.sv.write.PacketID(Message.Type.EntityDestroy);
                    Net.sv.write.UInt32(selectedEntity.net.ID);
                    Net.sv.write.UInt8(0);
                    Net.sv.write.Send(new SendInfo(selectedEntity.net.group.subscribers));
                }
                selectedEntity.SendNetworkUpdateImmediate();
            }

            private void SavePrefabName(BaseEntity entity, bool fullClone)
            {
                _entitySaver.SaveEntity(entity, fullClone);
            }

            private void SpawnPrefab(Vector3 position, bool fullClone)
            {
                _entitySaver.SpawnEntity(position, fullClone);
            }
        }

        public class EntitySaver
        {
            //public bool FullClone = false;
            private ulong _ownerID = 0;
            private string _prefabName = "";
            private Vector3 lastPos;
            private Quaternion _rotation;
            private byte[] savedBytes = null;
            private BuildingGrade.Enum _grade = BuildingGrade.Enum.None;
            private Vector3 velocity;

            public void SaveEntity(BaseEntity entity, bool fullClone)
            {
                _prefabName = entity.PrefabName;
                _rotation = entity.transform.rotation;
                lastPos = entity.transform.position;
                if (entity is BuildingBlock)
                {
                    _grade = (entity as BuildingBlock).grade;
                }
                if (!fullClone)
                {
                    savedBytes = null;
                    return;
                }
                savedBytes = entity.GetSaveCache().ToArray();
                if (entity.GetComponent<ServerProjectile>() != null)
                {
                    velocity = (Vector3)typeof(ServerProjectile).GetField("_currentVelocity", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(entity.GetComponent<ServerProjectile>());
                }
            }

            public void SpawnEntity(Vector3 position, bool fullClone)
            {
                if (_prefabName == "")
                {
                    return;
                }
                var entity = GameManager.server.CreateEntity(_prefabName, position, _rotation);
                if (entity == null)
                {
                    _plugin.PrintError("SpawnPrefab Failed!");
                }
                if (fullClone)
                {
                    entity.PreServerLoad();
                }
                GameObject.Destroy(entity.GetComponent<Spawnable>());
                if (entity is BuildingBlock && _grade > BuildingGrade.Enum.None)
                {
                    (entity as BuildingBlock).grade = _grade;
                }
                entity.Spawn();
                if (entity is BuildingBlock && _grade > BuildingGrade.Enum.None)
                {
                    (entity as BuildingBlock).SetGrade(_grade);
                    (entity as BuildingBlock).Heal(entity.MaxHealth());
                }
                if (!fullClone)
                {
                    return;
                }
                if (savedBytes == null)
                {
                    return;
                }
                var entityData = Entity.Deserialize(savedBytes);
                BaseEntity.LoadInfo loadInfo = new BaseNetworkable.LoadInfo() { fromDisk = true };
                loadInfo.msg = entityData;
                entity.Load(loadInfo);
                entity.PostServerLoad();
                entity.transform.position = position;
                SetupEntityIDs(entity);
                if (entity.GetComponent<ServerProjectile>() != null)
                {
                    entity.GetComponent<ServerProjectile>().InitializeVelocity(velocity);
                }
                if (entity is Signage)
                {
                    entity.SendNetworkUpdate();
                }
            }

            private void SetupEntityIDs(BaseEntity entity)
            {
                if (entity is StorageContainer)
                {
                    SetupItemContainerIDs((entity as StorageContainer).inventory);
                }
                else if (entity is BasePlayer)
                {
                    var player = entity as BasePlayer;
                    SetupItemContainerIDs(player.inventory.containerBelt);
                    SetupItemContainerIDs(player.inventory.containerMain);
                    SetupItemContainerIDs(player.inventory.containerWear);
                }
                else if (entity is DroppedItemContainer)
                {
                    SetupItemContainerIDs((entity as DroppedItemContainer).inventory);
                }
            }

            private void SetupItemContainerIDs(ItemContainer container)
            {
                container.uid = Net.sv.TakeUID();
                foreach (var item in container.itemList)
                {
                    SetupItem(item);
                }
            }

            private void SetupItem(Item item)
            {
                item.uid = Net.sv.TakeUID();
                if (item.contents != null)
                {
                    SetupItemContainerIDs(item.contents);
                }
                foreach (var mod in item.info.itemMods)
                {
                    mod.OnItemCreated(item);
                }
            }
        }

        public class UIManager
        {
            private UIImage Crosshair;
            public UILabel EntityInfoLabel;
            public UIBaseElement ToolUI;
            private UILabel ToolMode;

            public UIManager()
            {
                SetupUI_ToolUI();
                SetupUI_Crosshair();
            }

            public void Unload()
            {
                Crosshair?.HideAll();
                EntityInfoLabel?.HideAll();
                ToolMode?.HideAll();
            }

            void SetupUI_Crosshair()
            {
                float dotSize = 0.007f * 0.20f;
                float fadeIn = 0.25f;

                Crosshair = new UIImage(new Vector2(0.5f - dotSize, 0.5f - dotSize * 2), new Vector2(0.5f + dotSize, 0.5f + dotSize * 2), ToolUI);
                Crosshair.Image.Sprite = "assets/icons/circle_closed.png";

                EntityInfoLabel = new UILabel(new Vector2(0.4f, 0.50f), new Vector2(0.6f, 0.60f), "Entity", 12, "1 1 1 1", ToolUI, TextAnchor.MiddleCenter);
                EntityInfoLabel.AddOutline();
            }

            void SetupUI_ToolUI()
            {
                ToolUI = new UIBaseElement(new Vector2(0, 0), new Vector2(1, 1));
                ToolUI.conditionalShow = delegate (BasePlayer player)
                {
                    var item = player.GetActiveItem();
                    if (item == null)
                    {
                        return false;
                    }
                    return item.skin == ToolSkinID;
                };
                ToolMode = new UILabel(new Vector2(0.29f, 0.02f), new Vector2(0.39f, 0.12f), "Inactive", 16, "1 1 1 1", ToolUI, TextAnchor.MiddleRight);
                ToolMode.variableText = delegate (BasePlayer player)
                {
                    return $"{PlayerData.Get(player).Mode}";
                };
                ToolMode.AddOutline();
            }

            public void SwitchToolMode(BasePlayer player, ToolMode mode)
            {
                ToolMode.Refresh(player);
            }
        }

        #region Lang API

        public Dictionary<string, string> lang_en = new Dictionary<string, string>()
        {
            {"NotLookingAtEntity", "You aren't looking at an entity!" },
        };

        public static string GetLangMessage(string key, BasePlayer player)
        {
            return _plugin.lang.GetMessage(key, _plugin, player.UserIDString);
        }

        public static string GetLangMessage(string key, ulong player)
        {
            return _plugin.lang.GetMessage(key, _plugin, player.ToString());
        }

        public static string GetLangMessage(string key, string player)
        {
            return _plugin.lang.GetMessage(key, _plugin, player);
        }

        #endregion

        #region Entity Properties Integration

        [PluginReference]
        RustPlugin EntityProperties;

        public static bool EntityPropertiesLoaded()
        {
            if (_plugin.EntityProperties == null)
            {
                return false;
            }
            return _plugin.EntityProperties.IsLoaded;
        }

        public static void StartEditingEntityData(BasePlayer player, BaseEntity entity)
        {
            if (!EntityPropertiesLoaded())
            {
                return;
            }
            _plugin.EntityProperties.CallHook("StartEditingEntity", player, entity);
        }

        public static void StopEditingEntityData(BasePlayer player)
        {
            if (!EntityPropertiesLoaded())
            {
                return;
            }
            _plugin.EntityProperties.CallHook("StopEditingEntity", player);
        }

        public static void CloneEntityData(BaseEntity source, BaseEntity target)
        {
            if (!EntityPropertiesLoaded())
            {
                return;
            }
            _plugin.EntityProperties.CallHook("CloneEntitySettings", source, target);
        }

        #endregion

        #region Disguise Integration

        [PluginReference]
        RustPlugin Disguise;

        public static bool DisguiseLoaded()
        {
            if (_plugin.Disguise == null)
            {
                return false;
            }
            return _plugin.Disguise.IsLoaded;
        }

        #endregion

        #region Error Printing

        public static bool Debugging = true;

        public static void Debug(string format, params object[] args)
        {
            if (!Debugging)
            {
                return;
            }
            _plugin.Puts(format, args);
        }

        public static void Debug(object obj)
        {
            if (!Debugging)
            {
                return;
            }
            _plugin.Puts(obj.ToString());
        }

        public static void Error(string format, params object[] args)
        {
            _plugin.PrintError(format, args);
        }

        #endregion

        #region PlayerData

        public class PlayerDataBase
        {
            [JsonIgnore]
            public BasePlayer Player { get; set; }

            public string userID { get; set; } = "";

            public PlayerDataBase()
            {

            }

            public PlayerDataBase(BasePlayer player)
            {
                userID = player.UserIDString;
                Player = player;
            }
        }

        public class PlayerDataController<T> where T : PlayerDataBase
        {
            [JsonPropertyAttribute(Required = Required.Always)]
            private Dictionary<string, T> playerData { get; set; } = new Dictionary<string, T>();

            public T Get(string identifer)
            {
                T data;
                if (!playerData.TryGetValue(identifer, out data))
                {
                    data = Activator.CreateInstance<T>();
                    playerData[identifer] = data;
                }
                return data;
            }

            public T Get(ulong userID)
            {
                return Get(userID.ToString());
            }

            public T Get(BasePlayer player)
            {
                var data = Get(player.UserIDString);
                data.Player = player;
                return data;
            }
        }

        #endregion

        #region Rust Classes In JSON

        public class JSONSpawnPoint
        {
            public float xPos { get; set; }
            public float yPos { get; set; }
            public float zPos { get; set; }

            public float xRot { get; set; }
            public float yRot { get; set; }
            public float zRot { get; set; }
            public float wRot { get; set; }

            public BasePlayer.SpawnPoint ToSpawnPoint()
            {
                var newSpawn = new BasePlayer.SpawnPoint();

                newSpawn.pos.x = xPos;
                newSpawn.pos.y = yPos;
                newSpawn.pos.z = zPos;
                newSpawn.rot.x = xRot;
                newSpawn.rot.y = yRot;
                newSpawn.rot.z = zRot;
                newSpawn.rot.w = wRot;

                return newSpawn;
            }

            [JsonIgnore]
            public Vector3 Position
            {
                get { return new Vector3(xPos, yPos, zPos); }
                set { xPos = value.x; yPos = value.y; zPos = value.z; }
            }

            [JsonIgnore]
            public Quaternion Rotation
            {
                get { return new Quaternion(xRot, yRot, zRot, wRot); }
                set { xRot = value.x; yRot = value.y; zRot = value.z; wRot = value.w; }
            }

            public JSONSpawnPoint()
            {

            }

            public JSONSpawnPoint(Vector3 position, Quaternion rot)
            {
                Position = position;
                Rotation = rot;
            }
        }

        public class JSONEntity
        {
            public float xPos { get; set; }
            public float yPos { get; set; }
            public float zPos { get; set; }

            public float xRot { get; set; }
            public float yRot { get; set; }
            public float zRot { get; set; }
            public float wRot { get; set; }

            public string prefabName { get; set; }

            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
            public BuildingGrade.Enum grade { get; set; }

            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
            public int AssignedBaseID;

            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
            public ulong OwnerID = 0;

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public JSONStorageContainer Inventory;

            [JsonIgnore]
            public Vector3 Position
            {
                get { return new Vector3(xPos, yPos, zPos); }
            }

            [JsonIgnore]
            public Quaternion Rotation
            {
                get { return new Quaternion(xRot, yRot, zRot, wRot); }
            }

            [JsonIgnore]
            public BaseEntity Entity;

            public JSONEntity(BaseEntity entity) : this(entity, entity.transform.position, entity.transform.rotation)
            {

            }

            public JSONEntity(BaseEntity entity, Vector3 position, Quaternion rotation)
            {
                this.xPos = position.x;
                this.yPos = position.y;
                this.zPos = position.z;
                this.xRot = rotation.x;
                this.yRot = rotation.y;
                this.zRot = rotation.z;
                this.wRot = rotation.w;
                this.prefabName = entity.PrefabName;
                OwnerID = entity.OwnerID;
                if (entity.GetType() == typeof(BuildingBlock))
                {
                    grade = ((BuildingBlock)entity).grade;
                }
            }

            public JSONEntity()
            {

            }

            private BaseEntity CreateEntity(bool floating = true)
            {
                var obj = GameManager.server.CreatePrefab(prefabName, Position, Rotation, false);

                BaseEntity entity = obj.GetComponent<BaseEntity>();

                GameObject.Destroy(obj.GetComponent<Spawnable>());

                obj.AwakeFromInstantiate();

                entity.OwnerID = OwnerID;

                Entity = entity;

                PreSpawn(entity);

                return entity;
            }

            public void PreSpawn(BaseEntity entity)
            {
                if (entity is BuildingBlock)
                {
                    (entity as BuildingBlock).grade = grade;
                }
            }

            public void PostSpawn(BaseEntity entity)
            {
                BaseCombatEntity combat = entity as BaseCombatEntity;
                if (combat != null)
                {
                    if (combat.healthFraction < 1f)
                    {
                        combat.ChangeHealth(entity.MaxHealth());
                    }
                }
            }

            public BaseEntity Spawn()
            {
                var ent = CreateEntity(false);
                ent.Spawn();
                PostSpawn(ent);
                return ent;
            }

            public bool IsEntity(BaseEntity entity)
            {
                if (entity.transform.position != Position)
                {
                    return false;
                }
                if (entity.transform.rotation != Rotation)
                {
                    return false;
                }
                if (entity.PrefabName != prefabName)
                {
                    return false;
                }
                if (entity.OwnerID != OwnerID)
                {
                    return false;
                }
                return true;
            }
        }

        public class JSONStorageContainer
        {
            public int MaxRefills = -1; //-1 For unlimited
            public bool Locked = true;
            public bool PerPlayer = false;

            public JSONItemContainer SavedInventory = new JSONItemContainer();
        }

        public class JSONItemAmount
        {
            public string shortname = "";
            public int amount = 0;

            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
            public ulong skinID = 0;
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
            public int slot = 0;

            public JSONItemAmount()
            {

            }

            public JSONItemAmount(string shortname, int amount = 1, ulong skinID = 0)
            {
                this.shortname = shortname;
                this.amount = amount;
                this.skinID = skinID;
            }

            public JSONItemAmount(ItemAmount itemAmount) : this(itemAmount.itemDef.shortname, (int)itemAmount.amount)
            {

            }

            public JSONItemAmount(Item item, int slot = 0) : this(item.info.shortname, item.amount, item.skin)
            {
                this.slot = slot + 1;
            }

            public int ItemID()
            {
                var item = ItemManager.itemList.FirstOrDefault(x => x.shortname == shortname);
                if (item == null)
                {
                    return -1;
                }
                return item.itemid;
            }

            public Item CreateItem()
            {
                var item = ItemManager.CreateByPartialName(shortname, amount);
                if (item == null)
                {
                    return null;
                }
                item.skin = skinID;
                return item;
            }

            public void GiveToPlayer(BasePlayer player)
            {
                var item = CreateItem();
                if (item != null)
                {
                    player.GiveItem(item);
                }
            }

            public void AddToContainer(ItemContainer container)
            {
                var item = CreateItem();
                item.MoveToContainer(container, slot - 1, false);
            }
        }

        public class JSONItemContainer
        {
            public List<JSONItemAmount> Items = new List<JSONItemAmount>();

            public JSONItemContainer()
            {

            }

            public JSONItemContainer(ItemContainer container)
            {
                List<JSONItemAmount> items = new List<JSONItemAmount>();
                for (int i = 0; i < container.capacity; i++)
                {
                    var item = container.GetSlot(i);
                    items.Add(new JSONItemAmount(item, i));
                }
                Items = items;
            }

            public void Load(ItemContainer container)
            {
                container.Clear();
                foreach (var item in Items)
                {
                    item.AddToContainer(container);
                }
            }
        }

        #endregion

        #region Configuration Files

        public enum ConfigLocation
        {
            Data = 0,
            Config = 1,
            Logs = 2,
            Plugins = 3,
            Lang = 4,
            Custom = 5,
        }

        public class JSONFile<Type> where Type : class
        {
            private DynamicConfigFile _file;
            public string _name { get; set; }
            public Type Instance { get; set; }
            private ConfigLocation _location { get; set; }
            private string _path { get; set; }
            public bool SaveOnUnload = false;
            public bool Compressed = false;

            public JSONFile(string name, ConfigLocation location = ConfigLocation.Data, string path = null, string extension = ".json", bool saveOnUnload = false)
            {
                SaveOnUnload = saveOnUnload;
                _name = name.Replace(".json", "");
                _location = location;
                switch (location)
                {
                    case ConfigLocation.Data:
                        {
                            _path = $"{Oxide.Core.Interface.Oxide.DataDirectory}/{name}{extension}";
                            break;
                        }
                    case ConfigLocation.Config:
                        {
                            _path = $"{Oxide.Core.Interface.Oxide.ConfigDirectory}/{name}{extension}";
                            break;
                        }
                    case ConfigLocation.Logs:
                        {
                            _path = $"{Oxide.Core.Interface.Oxide.LogDirectory}/{name}{extension}";
                            break;
                        }
                    case ConfigLocation.Lang:
                        {
                            _path = $"{Oxide.Core.Interface.Oxide.LangDirectory}/{name}{extension}";
                            break;
                        }
                    case ConfigLocation.Custom:
                        {
                            _path = $"{path}/{name}{extension}";
                            break;
                        }
                }
                _file = new DynamicConfigFile(_path);
                Init();
            }

            public virtual void Init()
            {
                _plugin.OnRemovedFromManager.Add(new Action<Plugin, PluginManager>(Unload));
                Load();
            }

            public virtual void Load()
            {
                if (Compressed)
                {
                    LoadCompressed();
                    return;
                }

                if (!_file.Exists())
                {
                    Save();
                }
                Instance = _file.ReadObject<Type>();
                if (Instance == null)
                {
                    Instance = Activator.CreateInstance<Type>();
                    Save();
                }
                return;
            }

            private void LoadCompressed()
            {
                string str = _file.ReadObject<string>();
                if (str == null || str == "")
                {
                    Instance = Activator.CreateInstance<Type>();
                    return;
                }
                using (var compressedStream = new MemoryStream(Convert.FromBase64String(str)))
                using (var zipStream = new GZipStream(compressedStream, CompressionMode.Decompress))
                using (var resultStream = new MemoryStream())
                {
                    var buffer = new byte[4096];
                    int read;

                    while ((read = zipStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        resultStream.Write(buffer, 0, read);
                    }

                    Instance = JsonConvert.DeserializeObject<Type>(Encoding.UTF8.GetString(resultStream.ToArray()));
                }
            }

            public virtual void Save()
            {
                if (Compressed)
                {
                    SaveCompressed();
                    return;
                }

                _file.WriteObject(Instance);
                return;
            }

            private void SaveCompressed()
            {
                using (var stream = new MemoryStream())
                {
                    using (GZipStream zipStream = new GZipStream(stream, CompressionMode.Compress))
                    {
                        byte[] bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(Instance));
                        zipStream.Write(bytes, 0, bytes.Length);
                        zipStream.Close();
                        _file.WriteObject(Convert.ToBase64String(stream.ToArray()));
                    }
                }
            }

            public virtual void Reload()
            {
                Load();
            }

            private void Unload(Plugin sender, PluginManager manager)
            {
                if (SaveOnUnload)
                {
                    Save();
                }
            }
        }

        #endregion

        #region Jake's UI Framework

        private Dictionary<string, UICallbackComponent> UIButtonCallBacks { get; set; } = new Dictionary<string, UICallbackComponent>();

        void OnButtonClick(ConsoleSystem.Arg arg)
        {
            UICallbackComponent button;
            if (UIButtonCallBacks.TryGetValue(arg.cmd.Name, out button))
            {
                button.InvokeCallback(arg);
                return;
            }
            Puts("Unknown button command: {0}", arg.cmd.Name);
        }

        public class UIElement : UIBaseElement
        {
            public CuiElement Element { get; protected set; }
            public UIOutline Outline { get; set; }
            public CuiRectTransformComponent transform { get; protected set; }
            public float FadeOut
            {
                get
                {
                    return Element == null ? _fadeOut : Element.FadeOut;
                }
                set
                {
                    if (Element != null)
                    {
                        Element.FadeOut = value;
                    }
                    _fadeOut = value;
                }
            }
            private float _fadeOut = 0f;

            public string Name { get { return Element.Name; } }

            public UIElement(UIBaseElement parent = null) : base(parent)
            {

            }

            public UIElement(Vector2 position, float width, float height, UIBaseElement parent = null) : this(position, new Vector2(position.x + width, position.y + height), parent)
            {

            }

            public UIElement(Vector2 min, Vector2 max, UIBaseElement parent = null) : base(min, max, parent)
            {
                transform = new CuiRectTransformComponent();
                Element = new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = this._parent == null ? this.Parent : this._parent.Parent,
                    Components =
                        {
                            transform,
                        },
                    FadeOut = _fadeOut,
                };
                UpdatePlacement();

                Init();
            }

            public void AddOutline(string color = "0 0 0 1", string distance = "1 -1")
            {
                Outline = new UIOutline(color, distance);
                Element.Components.Add(Outline.component);
            }

            public virtual void Init()
            {

            }

            public override void Show(BasePlayer player, bool children = true)
            {
                if (this is UIElement)
                {
                    if (!CanShow(player))
                    {
                        _shouldShow = false;
                        return;
                    }
                    _shouldShow = true;

                    if (conditionalSize != null)
                    {
                        Vector2 returnSize = conditionalSize.Invoke(player);
                        if (returnSize != null)
                        {
                            SetSize(returnSize.x, returnSize.y);
                        }
                    }

                    if (conditionalPosition != null)
                    {
                        Vector2 returnPos = conditionalPosition.Invoke(player);
                        if (returnPos != null)
                        {
                            SetPosition(returnPos.x, returnPos.y);
                        }
                    }
                }
                if (AddPlayer(player))
                {
                    SafeAddUi(player, Element);
                }
                base.Show(player, children);
            }

            public override void Hide(BasePlayer player, bool children = true)
            {
                base.Hide(player, children);
                if (RemovePlayer(player))
                {
                    SafeDestroyUi(player, Element);
                }
            }

            public override void UpdatePlacement()
            {
                base.UpdatePlacement();
                if (transform != null)
                {
                    transform.AnchorMin = $"{globalPosition.x} {globalPosition.y}";
                    transform.AnchorMax = $"{globalPosition.x + globalSize.x} {globalPosition.y + globalSize.y}";
                }
                //RefreshAll();
            }

            public void SetPositionAndSize(CuiRectTransformComponent trans)
            {
                transform.AnchorMin = trans.AnchorMin;
                transform.AnchorMax = trans.AnchorMax;

                //_plugin.Puts($"POSITION [{transform.AnchorMin},{transform.AnchorMax}]");

                RefreshAll();
            }

            public void SetParent(UIElement element)
            {
                Element.Parent = element.Element.Name;
                UpdatePlacement();
            }

            public void SetParent(string parent)
            {
                Element.Parent = parent;
                Parent = parent;
            }

        }

        public class UIButton : UIElement, UICallbackComponent
        {
            public CuiButtonComponent buttonComponent { get; private set; }
            public CuiTextComponent textComponent { get; private set; }
            public UILabel Label { get; set; }
            private string _textColor { get; set; }
            private string _buttonText { get; set; }
            public string Text { set { textComponent.Text = value; } }
            public Func<BasePlayer, string> variableText { get; set; }

            public Action<ConsoleSystem.Arg> onCallback;

            private int _fontSize;

            public UIButton(Vector2 min = default(Vector2), Vector2 max = default(Vector2), string buttonText = "", string buttonColor = "0 0 0 0.85", string textColor = "1 1 1 1", int fontSize = 15, UIBaseElement parent = null) : base(min, max, parent)
            {
                buttonComponent = new CuiButtonComponent();

                _fontSize = fontSize;
                _textColor = textColor;
                _buttonText = buttonText;

                buttonComponent.Command = CuiHelper.GetGuid();
                buttonComponent.Color = buttonColor;

                Element.Components.Insert(0, buttonComponent);

                _plugin.cmd.AddConsoleCommand(buttonComponent.Command, _plugin, "OnButtonClick");

                _plugin.UIButtonCallBacks[buttonComponent.Command] = this;

                Label = new UILabel(new Vector2(0, 0), new Vector2(1, 1), fontSize: _fontSize, parent: this);

                textComponent = Label.text;

                Label.text.Align = TextAnchor.MiddleCenter;
                Label.text.Color = _textColor;
                Label.Text = _buttonText;
                Label.text.FontSize = _fontSize;

            }

            public UIButton(Vector2 position, float width, float height, string buttonText = "", string buttonColor = "0 0 0 0.85", string textColor = "1 1 1 1", int fontSize = 15, UIBaseElement parent = null) : this(position, new Vector2(position.x + width, position.y + height), buttonText, buttonColor, textColor, fontSize, parent)
            {

            }

            public override void Init()
            {
                base.Init();

            }

            public void AddChatCommand(string fullCommand)
            {
                if (fullCommand == null)
                {
                    return;
                }
                onCallback += (arg) =>
                {
                    _plugin.rust.RunClientCommand(arg.Player(), $"chat.say \"/{fullCommand}\"");
                };
            }

            public void AddCallback(Action<BasePlayer> callback)
            {
                if (callback == null)
                {
                    return;
                }
                onCallback += (args) => { callback(args.Player()); };
            }

            public override void Show(BasePlayer player, bool children = true)
            {
                if (variableText != null)
                {
                    try
                    {
                        Text = variableText.Invoke(player);
                    }
                    catch (Exception ex)
                    {
                        _plugin.Puts($"UIButton.variableText failed!\n{ex}");
                    }
                }
                base.Show(player, children);
            }

            public void InvokeCallback(ConsoleSystem.Arg args)
            {
                if (onCallback == null)
                {
                    return;
                }
                onCallback.Invoke(args);
            }
        }

        public class UIBackgroundText : UIPanel
        {
            public UILabel Label;

            public UIBackgroundText(Vector2 min = default(Vector2), Vector2 max = default(Vector2), UIBaseElement parent = null, string backgroundColor = "0 0 0 0.85", string labelText = "", int fontSize = 12, string fontColor = "1 1 1 1", TextAnchor alignment = TextAnchor.MiddleCenter) : base(min, max, parent)
            {
                Label = new UILabel(new Vector2(0, 0), new Vector2(1, 1), labelText, fontSize, fontColor, parent, alignment);
            }
        }

        public class UILabel : UIElement
        {
            public CuiTextComponent text { get; private set; }

            public UILabel(Vector2 min = default(Vector2), Vector2 max = default(Vector2), string labelText = "", int fontSize = 12, string fontColor = "1 1 1 1", UIBaseElement parent = null, TextAnchor alignment = TextAnchor.MiddleCenter) : base(min, max, parent)
            {

                if (min == Vector2.zero && max == Vector2.zero)
                {
                    max = Vector2.one;
                }

                text = new CuiTextComponent();

                text.Text = labelText;
                ColorString = fontColor;
                text.Align = alignment;
                text.FontSize = fontSize;

                Element.Components.Insert(0, text);
            }

            public UILabel(Vector2 min, float width, float height, string labelText = "", int fontSize = 12, string fontColor = "1 1 1 1", UIBaseElement parent = null, TextAnchor alignment = TextAnchor.MiddleCenter) : this(min, new Vector2(min.x + width, min.y + height), labelText, fontSize, fontColor, parent, alignment)
            {

            }

            public string Text { set { if (value == null) { text.Text = ""; } else { text.Text = value; } text.Text = value; } } //I love single line statments
            public TextAnchor Allign { set { text.Align = value; } }
            public Color Color { set { text.Color = value.ToString(); } }
            public string ColorString { set { text.Color = value.Replace("f", ""); } } //Prevent me from breaking UI with 0.1f instead of 0.1

            public Func<BasePlayer, string> variableText { get; set; }
            public Func<BasePlayer, string> variableFontColor { get; set; }

            public override void Show(BasePlayer player, bool children = true)
            {
                if (variableText != null)
                {
                    try
                    {
                        Text = variableText.Invoke(player);
                    }
                    catch (Exception ex)
                    {
                        _plugin.Puts($"UILabel.variableText failed!\n{ex}");
                    }
                }
                if (variableFontColor != null)
                {
                    try
                    {
                        ColorString = variableFontColor.Invoke(player);
                    }
                    catch (Exception ex)
                    {
                        _plugin.Puts($"UILabel.variableFontColor failed!\n{ex}");
                    }
                }
                base.Show(player, children);
            }

            public override void Init()
            {
                base.Init();

                if (_parent != null)
                {
                    if (_parent is UIButton)
                    {
                        Element.Parent = (_parent as UIButton).Name;
                        transform.AnchorMin = $"{localPosition.x} {localPosition.y}";
                        transform.AnchorMax = $"{localPosition.x + localSize.x} {localPosition.y + localSize.y}";
                    }
                }
            }

        }

        public class UIImageBase : UIElement
        {
            public UIImageBase(Vector2 min, Vector2 max, UIBaseElement parent) : base(min, max, parent)
            {
            }

            private CuiNeedsCursorComponent needsCursor { get; set; }

            private bool requiresFocus { get; set; }

            public bool CursorEnabled
            {
                get
                {
                    return requiresFocus;
                }
                set
                {
                    if (value)
                    {
                        needsCursor = new CuiNeedsCursorComponent();
                        Element.Components.Add(needsCursor);
                    }
                    else
                    {
                        Element.Components.Remove(needsCursor);
                    }

                    requiresFocus = value;
                }
            }
        }

        public class UIPanel : UIImageBase
        {
            private CuiImageComponent panel;

            public string Color { get { return panel.Color; } set { panel.Color = value; } }
            public string Material { get { return panel.Material; } set { panel.Material = value; } }

            public Func<BasePlayer, string> variableColor { get; set; }

            public UIPanel(Vector2 min, Vector2 max, UIBaseElement parent = null, string color = "0 0 0 0.85") : base(min, max, parent)
            {
                panel = new CuiImageComponent
                {
                    Color = color,
                };

                Element.Components.Insert(0, panel);
            }

            public UIPanel(Vector2 position, float width, float height, UIBaseElement parent = null, string color = "0 0 0 .85") : this(position, new Vector2(position.x + width, position.y + height), parent, color)
            {

            }

            public override void Show(BasePlayer player, bool children = true)
            {
                if (variableColor != null)
                {
                    try
                    {
                        panel.Color = variableColor.Invoke(player);
                    }
                    catch (Exception ex)
                    {
                        _plugin.Puts($"UIPanel.variableColor failed!\n{ex}");
                    }
                }
                base.Show(player, children);
            }
        }

        public class UIButtonContainer : UIPanel
        {
            private float _padding;
            private float _buttonHeight;
            private string _buttonColor;

            private List<UIButton> buttons = new List<UIButton>();

            public UIButtonContainer(Vector2 min, Vector2 max, UIBaseElement parent, string bgColor, float buttonHeight, float padding, string buttonColor = "0 0 0 0.85") : base(min, max, parent, bgColor)
            {
                _padding = padding;
                _buttonHeight = buttonHeight;
                _buttonColor = buttonColor;
            }

            public UIButton AddButton(string text, int fontSize, string textColor = "1 1 1 1", string buttonColor = "", Action<BasePlayer> callback = null)
            {
                if (buttonColor == "")
                {
                    buttonColor = _buttonColor;
                }
                float max = 1f - buttons.Count * _padding - buttons.Count * _buttonHeight;
                UIButton button = new UIButton(new Vector2(0f, max - _buttonHeight), new Vector2(1f, max), text, buttonColor, textColor, fontSize, this);
                if (callback != null)
                {
                    button.AddCallback(callback);
                }
                buttons.Add(button);
                return button;
            }
        }

        public class UIPagedElements : UIBaseElement
        {
            private UIButton nextPage { get; set; }
            private UIButton prevPage { get; set; }
            private float _elementHeight { get; set; }
            private float _elementSpacing { get; set; }
            private int _elementWidth { get; set; }
            private Dictionary<BasePlayer, int> ElementIndex = new Dictionary<BasePlayer, int>();

            private List<UIBaseElement> Elements = new List<UIBaseElement>();

            public UIPagedElements(Vector2 min, Vector2 max, float elementHeight, float elementSpacing, UIBaseElement parent = null, int elementWidth = 1) : base(min, max, parent)
            {
                _elementHeight = elementHeight;
                _elementSpacing = elementSpacing;
                _elementWidth = elementWidth;
            }

            public void NewElement(UIBaseElement element)
            {
                SetParent(this);
                Elements.Add(element);
            }

            public void NewElements(IEnumerable<UIBaseElement> elements)
            {
                foreach (var element in elements)
                {
                    SetParent(this);
                }
                Elements.AddRange(elements);
            }

            public override void Show(BasePlayer player, bool showChildren = true)
            {
                foreach (var element in Elements)
                {
                    element.Hide(player);
                }
                int elements = Mathf.FloorToInt((1f - (_elementSpacing * 2)) / (_elementHeight + _elementSpacing));
                int index = 0;
                ElementIndex.TryGetValue(player, out index);
                for (int i = index; i < elements; i++)
                {
                    //_plugin.Puts($"Index is {index}");
                    if (i >= Elements.Count)
                    {
                        break;
                    }
                    var element = Elements[i];
                    element.SetPosition(0f, 1f - (_elementHeight * (i + 1)) - (_elementWidth * (i + 1)));
                    element.SetSize(1f, _elementHeight);
                    element.Show(player);
                    //_plugin.Puts($"Element at {element.localPosition} {element.localSize}");
                }
                base.Show(player, showChildren);
            }

            public override void Hide(BasePlayer player, bool hideChildren = true)
            {
                base.Hide(player, hideChildren);
                foreach (var element in Elements)
                {
                    element.Hide(player);
                }
            }
        }

        public class UIButtonConfiguration
        {
            public string ButtonName { get; set; }
            public string ButtonCommand { get; set; }
            public string ButtonColor { get; set; }
            public Action<BasePlayer> callback { get; set; }
        }

        public class UIImage : UIImageBase
        {
            public CuiImageComponent Image { get; private set; }
            public string Sprite { get { return Image.Sprite; } set { Image.Sprite = value; } }
            public string Material { get { return Image.Material; } set { Image.Material = value; } }
            public string PNG { get { return Image.Png; } set { Image.Png = value; } }

            public UIImage(Vector2 min, Vector2 max, UIBaseElement parent = null) : base(min, max, parent)
            {
                Image = new CuiImageComponent();
                Element.Components.Insert(0, Image);
            }

            public UIImage(Vector2 position, float width, float height, UIBaseElement parent = null) : this(position, new Vector2(position.x + width, position.y + height), parent)
            {

            }

            public Func<BasePlayer, string> variableSprite { get; set; }
            public Func<BasePlayer, string> variablePNG { get; set; }

            public override void Show(BasePlayer player, bool children = true)
            {
                if (variableSprite != null)
                {
                    try
                    {
                        Image.Sprite = variableSprite.Invoke(player);
                    }
                    catch (Exception ex)
                    {
                        _plugin.Puts($"UIImage.variableSprite failed!\n{ex}");
                    }
                }
                if (variablePNG != null)
                {
                    try
                    {
                        Image.Png = variablePNG.Invoke(player);
                    }
                    catch (Exception ex)
                    {
                        _plugin.Puts($"UIImage.variablePNG failed!\n{ex}");
                    }
                }
                base.Show(player, children);
            }
        }

        public class UIRawImage : UIImageBase
        {
            public CuiRawImageComponent Image { get; private set; }

            public string Material { get { return Image.Material; } set { Image.Material = value; } }
            public string Sprite { get { return Image.Sprite; } set { Image.Sprite = value; } }
            public string PNG { get { return Image.Png; } set { Image.Png = value; } }
            public string Color { get { return Image.Color; } set { Image.Color = value; } }

            public UIRawImage(Vector2 position, float width, float height, UIBaseElement parent = null, string url = null) : this(position, new Vector2(position.x + width, position.y + height), parent, url)
            {

            }

            public UIRawImage(Vector2 min, Vector2 max, UIBaseElement parent = null, string url = null) : base(min, max, parent)
            {
                Image = new CuiRawImageComponent()
                {
                    Url = url,
                    Sprite = "assets/content/textures/generic/fulltransparent.tga"
                };

                Element.Components.Insert(0, Image);
            }

            public Func<BasePlayer, string> variablePNG { get; set; }

            public Func<BasePlayer, string> variableURL { get; set; }

            public Func<BasePlayer, string> variablePNGURL { get; set; }

            public override void Show(BasePlayer player, bool children = true)
            {
                if (variablePNGURL != null)
                {
                    string url = variablePNGURL.Invoke(player);
                    if (string.IsNullOrEmpty(url))
                    {
                        Image.Png = null;
                        Image.Url = null;
                    }
                    ulong num;
                    if (ulong.TryParse(url, out num))
                    {
                        Image.Png = url;
                        Image.Url = null;
                    }
                    else
                    {
                        Image.Png = null;
                        Image.Url = url;
                    }
                }
                else
                {
                    if (variablePNG != null)
                    {
                        Image.Png = variablePNG.Invoke(player);
                        if (string.IsNullOrEmpty(Image.Png))
                        {
                            Image.Png = null;
                        }
                    }
                    if (variableURL != null)
                    {
                        Image.Url = variableURL.Invoke(player);
                        if (string.IsNullOrEmpty(Image.Url))
                        {
                            Image.Url = null;
                        }
                    }
                }

                base.Show(player, children);
            }
        }

        public class UIBaseElement
        {
            public Vector2 localPosition { get; set; } = new Vector2();
            public Vector2 localSize { get; set; } = new Vector2();
            public Vector2 globalSize { get; set; } = new Vector2();
            public Vector2 globalPosition { get; set; } = new Vector2();
            public HashSet<BasePlayer> players { get; set; } = new HashSet<BasePlayer>();
            public UIBaseElement _parent { get; set; }
            public HashSet<UIBaseElement> children { get; set; } = new HashSet<UIBaseElement>();
            public Vector2 min { get { return localPosition; } }
            public Vector2 max { get { return localPosition + localSize; } }
            public string Parent { get; set; } = "Hud.Menu";
            public bool _shouldShow = true;

            public Func<BasePlayer, bool> conditionalShow { get; set; }
            public Func<BasePlayer, Vector2> conditionalSize { get; set; }
            public Func<BasePlayer, Vector2> conditionalPosition { get; set; }

            public UIBaseElement(UIBaseElement parent = null)
            {
                this._parent = parent;
            }

            public UIBaseElement(Vector2 min, Vector2 max, UIBaseElement parent = null) : this(parent)
            {
                localPosition = min;
                localSize = max - min;
                SetParent(parent);
                UpdatePlacement();
            }

            public UIBaseElement(Vector2 min, float width, float height, UIBaseElement parent = null) : this(min, new Vector2(min.x + width, min.y + height), parent)
            {

            }

            public void AddElement(UIBaseElement element)
            {
                if (element == this)
                {
                    _plugin.Puts("[UI FRAMEWORK] WARNING: AddElement() trying to add self as parent!");
                    return;
                }
                if (!children.Contains(element))
                {
                    children.Add(element);
                }
            }

            public void RemoveElement(UIBaseElement element)
            {
                children.Remove(element);
            }

            public void Refresh(BasePlayer player)
            {
                Hide(player);
                Show(player);
            }

            public bool AddPlayer(BasePlayer player)
            {
                if (!players.Contains(player))
                {
                    players.Add(player);
                    return true;
                }

                foreach (var child in children)
                {
                    child.AddPlayer(player);
                }

                return false;
            }

            public bool RemovePlayer(BasePlayer player)
            {
                return players.Remove(player);
            }

            public void Show(IEnumerable<BasePlayer> players)
            {
                foreach (BasePlayer player in players.ToList())
                {
                    Show(player);
                }
            }

            public virtual void SetParent(UIBaseElement parent)
            {
                if (parent != null && this != parent)
                {
                    parent.AddElement(this);
                }
                _parent = parent;
            }

            public virtual void Hide(BasePlayer player, bool hideChildren = true)
            {
                foreach (var child in children)
                {
                    child.Hide(player, hideChildren);
                }

                if (GetType() == typeof(UIBaseElement))
                {
                    RemovePlayer(player);
                }
            }

            public void Hide(IEnumerable<BasePlayer> players)
            {
                foreach (BasePlayer player in players.ToList())
                {
                    Hide(player);
                }
            }

            public virtual bool Toggle(BasePlayer player)
            {
                if (players.Contains(player))
                {
                    Hide(player);
                    return false;
                }
                Show(player);
                return true;
            }

            public virtual void Show(BasePlayer player, bool showChildren = true)
            {
                if (player == null || player.gameObject == null)
                {
                    players.Remove(player);
                    return;
                }

                if (GetType() == typeof(UIBaseElement))
                {
                    if (!CanShow(player))
                    {
                        _shouldShow = false;
                        return;
                    }
                    _shouldShow = true;

                    if (conditionalSize != null)
                    {
                        Vector2 returnSize = conditionalSize.Invoke(player);
                        if (returnSize != null)
                        {
                            SetSize(returnSize.x, returnSize.y);
                        }
                    }

                    if (conditionalPosition != null)
                    {
                        Vector2 returnPos = conditionalPosition.Invoke(player);
                        if (returnPos != null)
                        {
                            SetPosition(returnPos.x, returnPos.y);
                        }
                    }

                    AddPlayer(player);
                }

                foreach (var child in children)
                {
                    child.Show(player, showChildren);
                }
            }

            public bool CanShow(BasePlayer player)
            {
                if (_parent != null)
                {
                    if (!_parent.CanShow(player))
                    {
                        return false;
                    }
                }
                if (conditionalShow == null)
                {
                    return true;
                }
                if (player == null)
                {
                    return false;
                }
                if (player.gameObject == null)
                {
                    return false;
                }
                if (!player.IsConnected)
                {
                    return false;
                }
                try
                {
                    if (conditionalShow.Invoke(player))
                    {
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    _plugin.Puts($"UIBaseElement.conditionShow failed!\n{ex}");
                }
                return false;
            }

            public void HideAll()
            {
                foreach (BasePlayer player in players.ToList())
                {
                    if (player == null || player.gameObject == null)
                    {
                        players.Remove(player);
                        continue;
                    }
                    Hide(player);
                }
            }

            public void RefreshAll()
            {
                foreach (BasePlayer player in players.ToList())
                {
                    if (player == null || player.gameObject == null)
                    {
                        players.Remove(player);
                        continue;
                    }
                    Refresh(player);
                }
            }

            public void SafeAddUi(BasePlayer player, CuiElement element)
            {
                try
                {
                    //_plugin.Puts(JsonConvert.SerializeObject(element));
                    List<CuiElement> elements = new List<CuiElement>();
                    elements.Add(element);
                    CuiHelper.AddUi(player, elements);
                }
                catch (Exception ex)
                {

                }
            }

            public void SafeDestroyUi(BasePlayer player, CuiElement element)
            {
                try
                {
                    //_plugin.Puts($"Deleting {element.Name} to {player.userID}");
                    CuiHelper.DestroyUi(player, element.Name);
                }
                catch (Exception ex)
                {

                }
            }

            public void SetSize(float x, float y)
            {
                localSize = new Vector2(x, y);
                UpdatePlacement();
            }

            public void SetPosition(float x, float y)
            {
                localPosition = new Vector2(x, y);
                UpdatePlacement();
            }

            public virtual void UpdatePlacement()
            {
                if (_parent == null)
                {
                    globalSize = localSize;
                    globalPosition = localPosition;
                }
                else
                {
                    globalSize = Vector2.Scale(_parent.globalSize, localSize);
                    globalPosition = _parent.globalPosition + Vector2.Scale(_parent.globalSize, localPosition);
                }

                foreach (var child in children)
                {
                    child.UpdatePlacement();
                }
            }
        }

        public class UIReflectionElement : UIPanel
        {
            private object config { get; set; }
            private object _target { get { return Field.GetValue(config); } set { Field.SetValue(config, value); } }
            private FieldInfo Field { get; set; }

            private UILabel Text { get; set; }
            private UIInputField InputField { get; set; }
            private UIButton editButton { get; set; }

            private bool EditBox { get; set; } = false;

            public UIReflectionElement(Vector2 min, Vector2 max, FieldInfo field, object configuration, UIBaseElement parent = null) : base(min, max, parent)
            {
                config = configuration;
                Field = field;

                Text = new UILabel(new Vector2(0.05f, 0f), new Vector2(0.4f, 1f), "Amount", parent: this, alignment: TextAnchor.MiddleLeft);
                Text.variableText = delegate (BasePlayer player)
                {
                    return GetVisualText();
                };
                Text.AddOutline("0 0 0 1");

                //editButton = new UIButton(new Vector2(0.0125f, 0.15f), new Vector2(0.0375f, 0.85f), "","1 1 1 1", "1 1 1 1", 12, this);
                editButton = new UIButton(new Vector2(0.80f, 0.15f), new Vector2(0.90f, 0.85f), "Edit", "0 0 0 1", "1 1 1 1", 12, this);
                editButton.AddCallback((player) =>
                {
                    EditBox = true;
                    InputField.Refresh(player);
                });
                editButton.AddOutline("1 1 1 1", "0.75 -0.75");

                InputField = new UIInputField(new Vector2(0.45f, 0f), new Vector2(0.60f, 1f), this, "", TextAnchor.MiddleCenter);
                InputField.AddCallback((player, text) =>
                {
                    //TODO: Check if player has permissions to edit config values
                    if (String.IsNullOrEmpty(text))
                    {
                        return;
                    }
                    EditBox = false;
                    AssignValue(text);
                    InputField.InputField.Text = _target.ToString();
                    Text.Refresh(player);
                    InputField.Refresh(player);
                });
                InputField.AddOutline("1 1 1 1", "0.75 -0.75");
                InputField.conditionalShow = delegate (BasePlayer player)
                {
                    return EditBox;
                };
            }

            public override void Show(BasePlayer player, bool children = true)
            {

                base.Show(player, children);
            }

            public string GetVisualText()
            {
                if (_target == null)
                {
                    return $"{Field.FieldType.Name} = <color=#3B8AD6FF>NULL</color>";
                }
                string elementText = ((_target is IEnumerable && !(_target is string)) ? $" Count : {0}" : "");
                string valueText = (IsValueType(_target) ? $" = <color=#3B8AD6FF>{_target.ToString()}</color>" : "");
                if (_target is string)
                {
                    if (string.IsNullOrEmpty(_target as string))
                    {
                        valueText = $" = <color=#D69D85FF>\'\' \'\'</color>";
                    }
                    else
                    {
                        valueText = $" = <color=#D69D85FF>\'\'{_target}\'\'</color>";
                    }
                }
                return $"{Field.Name.Replace("<", "").Replace(">", "").Replace("k__BackingField", "")}{elementText}{valueText}";
                //return $"<color=#4EC8B0FF>{Field.FieldType.Name}</color> {Field.Name.Replace("<","").Replace(">","").Replace("k__BackingField","")}{elementText}{valueText}";
            }

            public void AssignValue(string text)
            {
                if (_target is string)
                {
                    _target = text;
                }
                else if (_target is int)
                {
                    int val = 0;
                    if (int.TryParse(text, out val))
                    {
                        _target = val;
                    }
                }
                else if (_target is uint)
                {
                    uint val = 0;
                    if (uint.TryParse(text, out val))
                    {
                        _target = val;
                    }
                }
                else if (_target is float)
                {
                    float val = 0;
                    if (float.TryParse(text, out val))
                    {
                        _target = val;
                    }
                }
            }
        }

        public class UIGridDisplay
        {
            public UIGridDisplay(Vector2 min, Vector2 max, int width, int height, float paddingX, float paddingY)
            {

            }
        }

        public static bool IsValueType(object obj)
        {
            return obj.GetType().IsValueType || obj is string;
        }

        public class ObjectMemoryInfo
        {
            public string name { get; set; }
            public int memoryUsed { get; set; }
            public int elements { get; set; }
            public object _target { get; set; }
            private int currentLayer { get; set; } = 0;
            public ObjectMemoryInfo _parent { get; set; }
            private bool _autoExpand { get; set; }

            public List<ObjectMemoryInfo> children { get; set; } = new List<ObjectMemoryInfo>();
            public List<MethodInfo> methods { get; set; } = new List<MethodInfo>();


            public ObjectMemoryInfo(object targetObject, int layers, string variableName = "", ObjectMemoryInfo parent = null, bool autoExpand = false)
            {
                _autoExpand = autoExpand;
                _parent = parent;
                name = variableName;
                name = name.Replace("<", "");
                name = name.Replace(">", "");
                name = name.Replace("k__BackingField", "");
                currentLayer = layers - 1;
                _target = targetObject;
                SetupObject();
                if (autoExpand)
                {
                    Expand();
                }
            }

            public void Expand()
            {
                CalculateSubObjects();
            }

            public void SetupObject()
            {
                #region Elements

                if (_target is IEnumerable)
                {
                    elements = GetCount();
                }
                if (_target is HashSet<object>)
                {
                    elements = (_target as HashSet<object>).Count;
                }

                #endregion

                #region Memory Usage
                var Type = _target?.GetType();
                if (Type == null)
                {
                    return;
                }
                if (Type == typeof(int))
                {
                    memoryUsed = 4;
                }
                else if (Type == typeof(string))
                {
                    memoryUsed = (_target as string).Length;
                }
                else if (Type == typeof(BaseNetworkable))
                {
                    memoryUsed = 8;
                }
                else if (_target is IDictionary)
                {
                    memoryUsed = elements * 16;
                }
                else if (_target is IList)
                {
                    memoryUsed = elements * 8;
                }
                else if (Type == typeof(int))
                {
                    memoryUsed = 4;
                }
                foreach (var child in children)
                {
                    memoryUsed += child.memoryUsed;
                }
                #endregion


                #region Methods

                foreach (var method in Type.GetMethods())
                {
                    if (method?.GetParameters().Length != 0)
                    {
                        continue;
                    }
                    methods.Add(method);
                }
                #endregion
            }
            private int GetCount()
            {
                int? c = (_target as IEnumerable).Cast<object>()?.Count();
                if (c != null)
                {
                    return (int)c;
                }
                return 0;
            }

            public string GetInfo()
            {
                return (_target is IEnumerable) ? $"Count : {elements}" : GetMemoryUsage();
            }

            public string GetVisualText()
            {
                if (_target == null)
                {
                    return $"{name} = <color=#3B8AD6FF>NULL</color>";
                }
                string elementText = ((_target is IEnumerable && !(_target is string)) ? $" Count : {elements}" : "");
                string valueText = (IsValueType(_target) ? $" = <color=#3B8AD6FF>{_target.ToString()}</color>" : "");
                return $"<color=#4EC8B0FF>{GetTypeName(_target.GetType())}</color> {name}{elementText}{valueText}";
            }

            public static string GetMethodText(MethodInfo info)
            {
                return $"{(info.IsPublic ? "<color=#3B8AD6FF>public " : "<color=#3B8AD6FF>private ")}{(info.IsVirtual ? "virtual</color> " : "</color>")}{$"<color=#4EC8B0FF>{GetTypeName(info.ReturnType)}</color> "}{info.Name}()";
            }

            private static string GetTypeName(Type type)
            {
                if (type == null)
                {
                    return "";
                }
                string generic = type.IsGenericType ? $"<{string.Join(",", type.GetGenericArguments().Select(x => GetTypeName(x)).ToArray())}>" : "";
                string name = type.Name;
                if (name.Contains("`"))
                {
                    name = name.Remove(name.IndexOf('`', 2));
                }
                return $"{name}{generic}";
            }

            private string GetMemoryUsage()
            {
                if (memoryUsed > 1000000000)
                {
                    return $"{Math.Round((double)memoryUsed / 1000000000, 2)}GB";
                }
                if (memoryUsed > 1000000)
                {
                    return $"{Math.Round((double)memoryUsed / 1000000, 2)}MB";
                }
                if (memoryUsed > 1000)
                {
                    return $"{Math.Round((double)memoryUsed / 1000, 2)}KB";
                }
                return $"{memoryUsed}B";
            }

            public void CalculateSubObjects()
            {
                children.Clear();
                try
                {
                    if (currentLayer < 0)
                    {
                        return;
                    }
                    if (_target == null)
                    {
                        return;
                    }
                    var Type = _target.GetType();
                    if (Type == null)
                    {
                        return;
                    }
                    if (_target is string) //No need to expand these
                    {
                        return;
                    }
                    if (_target is IEnumerable)
                    {
                        int index = 0;
                        var objects = (_target as IEnumerable).Cast<object>();
                        if (objects == null)
                        {
                            return;
                        }
                        foreach (var item in objects)
                        {
                            children.Add(new ObjectMemoryInfo(item, currentLayer, index.ToString(), this, _autoExpand));
                            index++;
                        }
                    }
                    else
                    {
                        foreach (var field in Type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static))
                        {
                            object target = field.GetValue(_target);
                            if (!CheckParents())
                            {
                                continue;
                            }
                            children.Add(new ObjectMemoryInfo(target, currentLayer, field.Name, this));
                        }
                    }
                }
                catch (Exception ex)
                {
                    _plugin.Puts(ex.ToString());
                }
            }

            public bool CheckParents()
            {
                ObjectMemoryInfo parent = _parent;
                while (parent != null)
                {
                    if (parent._target == _target)
                    {
                        return false;
                    }
                    parent = parent._parent;
                }
                return true;
            }

            public List<string> GetOutput(int layer = 0, bool justLists = false)
            {
                List<string> returnValue = new List<string>();
                string padding = new string('\t', layer);
                if (_target != null)
                {
                    if (_target is IEnumerable || !justLists || children.Count != 0)
                    {
                        returnValue.Add(padding + $"{_target.GetType().Name} {name} {GetMemoryUsage()}");
                    }
                }
                if (children.Count > 0)
                {
                    returnValue.Add(padding + "{");
                    foreach (var child in children)
                    {
                        returnValue.AddRange(child.GetOutput(layer + 1, justLists));
                    }
                    returnValue.Add(padding + "}");
                }
                return returnValue;
            }

            public string PrintOutput(bool justLists = false)
            {
                return string.Join(System.Environment.NewLine, GetOutput(0, justLists).ToArray());
            }
        }

        public class UICheckbox : UIButton
        {
            public UICheckbox(Vector2 min, Vector2 max, UIBaseElement parent = null) : base(min, max, parent: parent)
            {

            }
        }

        public class UIOutline
        {
            public CuiOutlineComponent component;

            public string Color { get { return _color; } set { _color = value; UpdateComponent(); } }
            public string Distance { get { return _distance; } set { _distance = value; UpdateComponent(); } }

            private string _color = "0 0 0 1";
            private string _distance = "0.25 0.25";

            public UIOutline()
            {

            }

            public UIOutline(string color, string distance)
            {
                _color = color;
                _distance = distance;
                UpdateComponent();
            }

            private void UpdateComponent()
            {
                if (component == null)
                {
                    component = new CuiOutlineComponent();
                }
                component.Color = _color;
                component.Distance = _distance;
            }
        }

        public interface UICallbackComponent
        {
            void InvokeCallback(ConsoleSystem.Arg args);

        }

        public class UIInputField : UIPanel, UICallbackComponent
        {
            public CuiInputFieldComponent InputField { get; set; }

            public Action<ConsoleSystem.Arg> onCallback;

            public UIInputField(Vector2 min, Vector2 max, UIBaseElement parent, string defaultText = "Enter Text Here", TextAnchor align = TextAnchor.MiddleLeft, int fontSize = 12, string panelColor = "0 0 0 0.85", string textColor = "1 1 1 1", bool password = false, int charLimit = 100) : base(min, max, parent, panelColor)
            {
                var input = new UIInput_Raw(Vector2.zero, Vector2.one, this, defaultText, align, fontSize, textColor, password, charLimit);

                InputField = input.InputField;

                _plugin.cmd.AddConsoleCommand(InputField.Command, _plugin, "OnButtonClick");

                _plugin.UIButtonCallBacks[InputField.Command] = this;
            }

            public void AddCallback(Action<BasePlayer, string> callback)
            {
                if (callback == null)
                {
                    return;
                }
                onCallback += (args) => { callback(args.Player(), string.Join(" ", args.Args)); };
            }

            public void InvokeCallback(ConsoleSystem.Arg args)
            {
                if (onCallback == null)
                {
                    return;
                }
                onCallback.Invoke(args);
            }
        }

        public class UIInput_Raw : UIElement
        {
            public CuiInputFieldComponent InputField { get; set; }

            public UIInput_Raw(Vector2 min, Vector2 max, UIBaseElement parent, string defaultText = "Enter Text Here", TextAnchor align = TextAnchor.MiddleLeft, int fontSize = 12, string textColor = "1 1 1 1", bool password = false, int charLimit = 100) : base(min, max, parent)
            {
                InputField = new CuiInputFieldComponent()
                {
                    Align = align,
                    CharsLimit = charLimit,
                    Color = textColor,
                    FontSize = fontSize,
                    IsPassword = password,
                    Text = defaultText,
                    Command = CuiHelper.GetGuid(),
                };

                Element.Components.Insert(0, InputField);
            }
        }

        #endregion
    }
}