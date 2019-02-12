// Reference: System.Drawing

//If debug is defined it will add a stopwatch to the paste and copydata which can be used to profile copying and pasting.
//#define DEBUG

using Facepunch;
using Graphics = System.Drawing.Graphics;
using ImageFormat = System.Drawing.Imaging.ImageFormat;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Game.Rust;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using UnityEngine;
using Debug = System.Diagnostics.Debug;

namespace Oxide.Plugins
{
    [Info("Copy Paste", "Reneb & MiRror & Misstake", "4.1.9", ResourceId = 716)]
    [Description("Copy and paste buildings to save them or move them")]
	
    public class CopyPaste : RustPlugin
    {
        private int copyLayer 	= LayerMask.GetMask("Construction", "Prevent Building", "Construction Trigger", "Trigger", "Deployed", "Default", "Ragdoll")
				  , groundLayer = LayerMask.GetMask("Terrain", "Default")
				  , rayCopy 	= LayerMask.GetMask("Construction", "Deployed", "Tree", "Resource", "Prevent Building")
				  , rayPaste 	= LayerMask.GetMask("Construction", "Deployed", "Tree", "Terrain", "World", "Water", "Prevent Building");

        private string copyPermission 		= "copypaste.copy" 
					 , listPermission 		= "copypaste.list"
					 , pastePermission 		= "copypaste.paste"
					 , pastebackPermission 	= "copypaste.pasteback"
					 , undoPermission 		= "copypaste.undo"
					 , serverID 			= "Server"
					 , subDirectory 		= "copypaste/";

        private Dictionary<string, Stack<List<BaseEntity>>> lastPastes = new Dictionary<string, Stack<List<BaseEntity>>>();

        private Dictionary<string, SignSize> signSizes = new Dictionary<string, SignSize>()
        {
			//{"spinner.wheel.deployed", new SignSize(512, 512)},
			{"sign.pictureframe.landscape", new SignSize(256, 128)},
            {"sign.pictureframe.tall", new SignSize(128, 512)},
            {"sign.pictureframe.portrait", new SignSize(128, 256)},
            {"sign.pictureframe.xxl", new SignSize(1024, 512)},
            {"sign.pictureframe.xl", new SignSize(512, 512)},
            {"sign.small.wood", new SignSize(128, 64)},
            {"sign.medium.wood", new SignSize(256, 128)},
            {"sign.large.wood", new SignSize(256, 128)},
            {"sign.huge.wood", new SignSize(512, 128)},
            {"sign.hanging.banner.large", new SignSize(64, 256)},
            {"sign.pole.banner.large", new SignSize(64, 256)},
            {"sign.post.single", new SignSize(128, 64)},
            {"sign.post.double", new SignSize(256, 256)},
            {"sign.post.town", new SignSize(256, 128)},
            {"sign.post.town.roof", new SignSize(256, 128)},
            {"sign.hanging", new SignSize(128, 256)},
            {"sign.hanging.ornate", new SignSize(256, 128)},
        };

        private List<BaseEntity.Slot> checkSlots = new List<BaseEntity.Slot>()
        {
            BaseEntity.Slot.Lock,
            BaseEntity.Slot.UpperModifier,
            BaseEntity.Slot.MiddleModifier,
            BaseEntity.Slot.LowerModifier
        };

        public enum CopyMechanics { Building, Proximity }

        private class SignSize
        {
            public int width;
            public int height;

            public SignSize(int width, int height)
            {
                this.width = width;
                this.height = height;
            }
        }

        //Config

        private ConfigData config;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Copy Options")]
            public CopyOptions Copy { get; set; }

            [JsonProperty(PropertyName = "Paste Options")]
            public PasteOptions Paste { get; set; }

            [JsonProperty(PropertyName = "Amount of entities to paste per batch. Use to tweak performance impact of pasting")]
            [DefaultValue(15)]
            public int PasteBatchSize = 15;

            [JsonProperty(PropertyName = "Amount of entities to copy per batch. Use to tweak performance impact of copying")]
            [DefaultValue(100)]
            public int CopyBatchSize = 100;

            [JsonProperty(PropertyName = "Amount of entities to undo per batch. Use to tweak performance impact of undoing")]
            [DefaultValue(15)]
            public int UndoBatchSize = 15;

            public class CopyOptions
            {
                [JsonProperty(PropertyName = "Check radius from each entity (true/false)")]
                [DefaultValue(true)]
                public bool EachToEach { get; set; } = true;

                [JsonProperty(PropertyName = "Share (true/false)")]
                [DefaultValue(false)]
                public bool Share { get; set; } = false;

                [JsonProperty(PropertyName = "Tree (true/false)")]
                [DefaultValue(false)]
                public bool Tree { get; set; } = false;
            }

            public class PasteOptions
            {
                [JsonProperty(PropertyName = "Auth (true/false)")]
                [DefaultValue(false)]
                public bool Auth { get; set; } = false;

                [JsonProperty(PropertyName = "Deployables (true/false)")]
                [DefaultValue(true)]
                public bool Deployables { get; set; } = true;

                [JsonProperty(PropertyName = "Inventories (true/false)")]
                [DefaultValue(true)]
                public bool Inventories { get; set; } = true;

                [JsonProperty(PropertyName = "Vending Machines (true/false)")]
                [DefaultValue(true)]
                public bool VendingMachines { get; set; } = true;

                [JsonProperty(PropertyName = "Stability (true/false)")]
                [DefaultValue(true)]
                public bool Stability { get; set; } = true;

                [JsonProperty(PropertyName = "EntityOwner (true/false)")]
                [DefaultValue(true)]
                public bool EntityOwner { get; set; } = true;

            }
        }

        private void LoadVariables()
        {
            Config.Settings.DefaultValueHandling = DefaultValueHandling.Populate;

            config = Config.ReadObject<ConfigData>();

            Config.WriteObject(config, true);
        }

        protected override void LoadDefaultConfig()
        {
            var configData = new ConfigData
            {
                Copy = new ConfigData.CopyOptions(),
                Paste = new ConfigData.PasteOptions()
            };

            Config.WriteObject(configData, true);
        }

        //Hooks

        private void Init()
        {
            permission.RegisterPermission(copyPermission, this);
			permission.RegisterPermission(listPermission, this);
            permission.RegisterPermission(pastePermission, this);
			permission.RegisterPermission(pastebackPermission, this);
            permission.RegisterPermission(undoPermission, this);

            Dictionary<string, Dictionary<string, string>> compiledLangs = new Dictionary<string, Dictionary<string, string>>();

            foreach (var line in messages)
            {
                foreach (var translate in line.Value)
                {
                    if (!compiledLangs.ContainsKey(translate.Key))
                        compiledLangs[translate.Key] = new Dictionary<string, string>();

                    compiledLangs[translate.Key][line.Key] = translate.Value;
                }
            }

            foreach (var cLangs in compiledLangs)
            {
                lang.RegisterMessages(cLangs.Value, this, cLangs.Key);
            }
        }

        private void OnServerInitialized()
        {
            LoadVariables();

            Vis.colBuffer = new Collider[8192 * 16];

            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                Formatting = Newtonsoft.Json.Formatting.Indented,
                ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore
            };
        }

        #region API

        private object TryCopyFromSteamID(ulong userID, string filename, string[] args, Action callback = null)
        {
            var player = BasePlayer.FindByID(userID);

            if (player == null)
                return Lang("NOT_FOUND_PLAYER", player.UserIDString);

            RaycastHit hit;

            if (!Physics.Raycast(player.eyes.HeadRay(), out hit, 1000f, rayCopy))
                return Lang("NO_ENTITY_RAY", player.UserIDString);

            return TryCopy(hit.point, hit.GetEntity().GetNetworkRotation().eulerAngles, filename, DegreeToRadian(player.GetNetworkRotation().eulerAngles.y), args, player, callback);
        }

        private object TryPasteFromSteamID(ulong userID, string filename, string[] args, Action callback = null)
        {
            var player = BasePlayer.FindByID(userID);

            if (player == null)
                return Lang("NOT_FOUND_PLAYER", player.UserIDString);

            RaycastHit hit;

            if (!Physics.Raycast(player.eyes.HeadRay(), out hit, 1000f, rayPaste))
                return Lang("NO_ENTITY_RAY", player.UserIDString);
			
            return TryPaste(hit.point, filename, player, DegreeToRadian(player.GetNetworkRotation().eulerAngles.y), args, callback: callback);
        }

        private object TryPasteFromVector3(Vector3 pos, float rotationCorrection, string filename, string[] args, Action callback = null)
        {
            return TryPaste(pos, filename, null, rotationCorrection, args, callback: callback);
        }

        #endregion
        //Other methods

        private object CheckCollision(HashSet<Dictionary<string, object>> entities, Vector3 startPos, float radius)
        {
            foreach (var entityobj in entities)
            {
                if (Physics.CheckSphere((Vector3)entityobj["position"], radius, copyLayer))
                    return Lang("BLOCKING_PASTE", null);
            }

            return true;
        }

        private bool CheckPlaced(string prefabname, Vector3 pos, Quaternion rot)
        {
            List<BaseEntity> ents = new List<BaseEntity>();
            Vis.Entities<BaseEntity>(pos, 2f, ents);

            foreach (BaseEntity ent in ents)
            {
                if (ent.PrefabName == prefabname && ent.transform.position == pos && ent.transform.rotation == rot)
                    return true;
            }

            return false;
        }

        private object cmdPasteBack(BasePlayer player, string[] args)
        {
            string userIDString = (player == null) ? serverID : player.UserIDString;

            if (args.Length < 1)
                return Lang("SYNTAX_PASTEBACK", userIDString);

            var success = TryPasteBack(args[0], player, args.Skip(1).ToArray());

            if (success is string)
                return (string)success;

            return true;
        }

        private object cmdUndo(string userIDString, string[] args)
        {
            var player = BasePlayer.Find(userIDString);
            if (!lastPastes.ContainsKey(userIDString))
                return Lang("NO_PASTED_STRUCTURE", userIDString);

            HashSet<BaseEntity> entities = new HashSet<BaseEntity>(lastPastes[userIDString].Pop().ToList());

            UndoLoop(entities, player);

            return true;
        }

        private void UndoLoop(HashSet<BaseEntity> entities, BasePlayer player, int count = 0)
        {

            entities
                .Take(config.UndoBatchSize)
                .ToList()
                .ForEach(p =>
            {
                entities.Remove(p);

                // Cleanup the hotspot beloning to the node.
                var ore = p as OreResourceEntity;
                if (ore != null)
                {
                    ore.CleanupBonus();
                }

                if (p != null && !p.IsDestroyed)
                    p.Kill();
            });

            // If it gets stuck in infinite loop break the loop.
            if (entities.Count == count)
            {
                if (player != null)
                    SendReply(player, "Undo cancelled because of infinite loop.");
                else
                    Puts("Undo cancelled because of infinite loop.");
                return;
            }

            if(entities.Count > 0)
                NextTick(() => UndoLoop(entities, player, entities.Count));
            else
            {
                if (player != null)
                    SendReply(player, Lang("UNDO_SUCCESS", player.UserIDString));
                else
                    Puts(Lang("UNDO_SUCCESS"));

                if (lastPastes[player?.UserIDString ?? serverID].Count == 0)
                    lastPastes.Remove(player?.UserIDString ?? serverID);
            }
        }

        private void Copy(Vector3 sourcePos, Vector3 sourceRot, string filename, float RotationCorrection, CopyMechanics copyMechanics, float range, bool saveTree, bool saveShare, bool eachToEach, BasePlayer player, Action callback)
        {
            int currentLayer = copyLayer;

            if (saveTree)
                currentLayer |= LayerMask.GetMask("Tree");

            var copyData = new CopyData()
            {
                FileName = filename,
                CurrentLayer = currentLayer,
                RotCor = RotationCorrection,
                Range = range,
                SaveShare = saveShare,
                SaveTree = saveTree,
                CopyMechanics = copyMechanics,
                EachToEach = eachToEach,
                SourcePos = sourcePos,
                SourceRot = sourceRot,
                Player = player,
                callback = callback
            };

            copyData.CheckFrom.Push(sourcePos);

            NextTick(() => CopyLoop(copyData));;
        }

        private void CopyLoop(CopyData copyData)
        {
            var checkFrom = copyData.CheckFrom;
            var houseList = copyData.HouseList;
            var buildingID = copyData.BuildingID;
            var copyMechanics = copyData.CopyMechanics;
            var batchSize = checkFrom.Count < config.CopyBatchSize ? checkFrom.Count : config.CopyBatchSize;

            for(int i = 0; i < batchSize; i++)
            {
                if (checkFrom.Count == 0)
                    break;

                List<BaseEntity> list = Pool.GetList<BaseEntity>();
                Vis.Entities<BaseEntity>(checkFrom.Pop(), copyData.Range, list, copyData.CurrentLayer);

                foreach (var entity in list)
                {
                    if (!houseList.Add(entity))
                        continue;

                    if (copyMechanics == CopyMechanics.Building)
                    {
                        BuildingBlock buildingBlock = entity.GetComponentInParent<BuildingBlock>();

                        if (buildingBlock != null)
                        {
                            if (buildingID == 0)
                                buildingID = buildingBlock.buildingID;

                            if (buildingID != buildingBlock.buildingID)
                                continue;
                        }
                    }

                    if (copyData.EachToEach)
                        checkFrom.Push(entity.transform.position);

                    copyData.RawData.Add(EntityData(entity, entity.transform.position, entity.transform.rotation.eulerAngles / 57.29578f, copyData));
                }

                copyData.BuildingID = buildingID;
            }

            if (checkFrom.Count > 0)
            {
                NextTick(() => CopyLoop(copyData));
            }
            else
            {
                string path = subDirectory + copyData.FileName;
                var datafile = Interface.Oxide.DataFileSystem.GetDatafile(path);

                datafile.Clear();

                var sourcePos = copyData.SourcePos;

                datafile["default"] = new Dictionary<string, object>
                {
                    {"position", new Dictionary<string, object>
                        {
                            {"x", sourcePos.x.ToString()},
                            {"y", sourcePos.y.ToString()},
                            {"z", sourcePos.z.ToString()}
                        }
                    },
                    {"rotationy", copyData.SourceRot.y.ToString()},
                    {"rotationdiff", copyData.RotCor.ToString()}
                };

                datafile["entities"] = copyData.RawData;
                datafile["protocol"] = new Dictionary<string, object>
                {
                    {"items", 2},
                    {"version",  Version}
                };

                Interface.Oxide.DataFileSystem.SaveDatafile(path);
                SendReply(copyData.Player, Lang("COPY_SUCCESS", copyData.Player.UserIDString, copyData.FileName));

                copyData.callback?.Invoke();

                Interface.CallHook("OnCopyFinished", copyData.RawData);
            }
        }

        private float DegreeToRadian(float angle)
		{
		   return (float)(Math.PI * angle / 180.0f);
		}
		
        private Dictionary<string, object> EntityData(BaseEntity entity, Vector3 entPos, Vector3 entRot, CopyData copyData)
        {
            var normalizedPos = NormalizePosition(copyData.SourcePos, entPos, copyData.RotCor);

            entRot.y -= copyData.RotCor;

            var data = new Dictionary<string, object>
            {
                {"prefabname", entity.PrefabName},
                {"skinid", entity.skinID},
                {"flags", TryCopyFlags(entity)},
                {"pos", new Dictionary<string,object>
                    {
                        {"x", normalizedPos.x.ToString()},
                        {"y", normalizedPos.y.ToString()},
                        {"z", normalizedPos.z.ToString()}
                    }
                },
                {"rot", new Dictionary<string,object>
                    {
                        {"x", entRot.x.ToString()},
                        {"y", entRot.y.ToString()},
                        {"z", entRot.z.ToString()},
                    }
                },
                {"ownerid", entity.OwnerID }
            };

            TryCopySlots(entity, data, copyData.SaveShare);

            var buildingblock = entity as BuildingBlock;

            if (buildingblock != null)
            {
                data.Add("grade", buildingblock.grade);
            }

            var box = entity as StorageContainer;

            if (box != null)
            {
                var itemlist = new List<object>();

                foreach (Item item in box.inventory.itemList)
                {
                    var itemdata = new Dictionary<string, object>
                    {
                        { "condition", item.condition.ToString() },
                        { "id", item.info.itemid },
                        { "amount", item.amount },
                        { "skinid", item.skin },
                        { "position", item.position },
                        { "blueprintTarget", item.blueprintTarget },
                    };

                    if (!string.IsNullOrEmpty(item.text))
                        itemdata["text"] = item.text;

                    var heldEnt = item.GetHeldEntity();

                    if (heldEnt != null)
                    {
                        var projectiles = heldEnt.GetComponent<BaseProjectile>();

                        if (projectiles != null)
                        {
                            var magazine = projectiles.primaryMagazine;

                            if (magazine != null)
                            {
                                itemdata.Add("magazine", new Dictionary<string, object>
                                {
                                    { magazine.ammoType.itemid.ToString(), magazine.contents }
                                });
                            }
                        }
                    }

                    if (item?.contents?.itemList != null)
                    {
                        var contents = new List<object>();

                        foreach (Item itemContains in item.contents.itemList)
                        {
                            contents.Add(new Dictionary<string, object>
                            {
                                {"id", itemContains.info.itemid },
                                {"amount", itemContains.amount },
                            });
                        }

                        itemdata["items"] = contents;
                    }

                    itemlist.Add(itemdata);
                }

                data.Add("items", itemlist);
            }

            var sign = entity as Signage;

            if (sign != null)
            {
                var imageByte = FileStorage.server.Get(sign.textureID, FileStorage.Type.png, sign.net.ID);

                data.Add("sign", new Dictionary<string, object>
                {
                    {"locked", sign.IsLocked()}
                });

                if (sign.textureID > 0 && imageByte != null)
                    ((Dictionary<string, object>)data["sign"]).Add("texture", Convert.ToBase64String(imageByte));
            }

            if (copyData.SaveShare)
            {
                var sleepingBag = entity as SleepingBag;

                if (sleepingBag != null)
                {
                    data.Add("sleepingbag", new Dictionary<string, object>
                    {
                        {"niceName", sleepingBag.niceName },
                        {"deployerUserID", sleepingBag.deployerUserID },
                        {"isPublic", sleepingBag.IsPublic() },
                    });
                }

                var cupboard = entity as BuildingPrivlidge;

                if (cupboard != null)
                {
                    data.Add("cupboard", new Dictionary<string, object>
                    {
                        {"authorizedPlayers", cupboard.authorizedPlayers.Select(y => y.userid).ToList() }
                    });
                }

                var autoTurret = entity as AutoTurret;

                if (autoTurret != null)
                {
                    data.Add("autoturret", new Dictionary<string, object>
                    {
                        {"authorizedPlayers", autoTurret.authorizedPlayers.Select(p => p.userid).ToList() }
                    });
                }
            }

            var vendingMachine = entity as VendingMachine;

            if (vendingMachine != null)
            {
                var sellOrders = new List<object>();

                foreach (var vendItem in vendingMachine.sellOrders.sellOrders)
                {
                    sellOrders.Add(new Dictionary<string, object>
                    {
                        { "itemToSellID", vendItem.itemToSellID },
                        { "itemToSellAmount", vendItem.itemToSellAmount },
                        { "currencyID", vendItem.currencyID },
                        { "currencyAmountPerItem", vendItem.currencyAmountPerItem },
                        { "inStock", vendItem.inStock },
                        { "currencyIsBP", vendItem.currencyIsBP },
                        { "itemToSellIsBP", vendItem.itemToSellIsBP },
                    });
                }

                data.Add("vendingmachine", new Dictionary<string, object>
                {
                    {"shopName", vendingMachine.shopName },
                    {"isBroadcasting", vendingMachine.IsBroadcasting() },
                    {"sellOrders", sellOrders}
                });
            }

            var ioEntity = entity as IOEntity;

            if (ioEntity != null)
            {
                var ioData = new Dictionary<string, object>();
                List<object> inputs = ioEntity.inputs.Select(input => new Dictionary<string, object>
                    {
                        {"connectedID", input.connectedTo.entityRef.uid},
                        {"connectedToSlot", input.connectedToSlot},
                        {"niceName", input.niceName},
                        {"type", (int) input.type},
                    })
                    .Cast<object>()
                    .ToList();
                ioData.Add("inputs", inputs);

                List<object> outputs = new List<object>();
                foreach (IOEntity.IOSlot output in ioEntity.outputs)
                {
                    Dictionary<string, object> ioConnection = new Dictionary<string, object>
                    {
                        {"connectedID", output.connectedTo.entityRef.uid },
                        {"connectedToSlot", output.connectedToSlot },
                        {"niceName", output.niceName },
                        {"type", (int)output.type },
                    };

                    var linePoints = new List<Vector3>();
                    if (output.linePoints != null)
                    {
                        // Since Vector3 is struct should be able to simply save the Vecot directly.
                        foreach (Vector3 linePoint in output.linePoints)
                        {
                            linePoints.Add(NormalizePosition(copyData.SourcePos, linePoint, copyData.RotCor));
                        }
                    }
                    ioConnection.Add("linePoints", linePoints);

                    outputs.Add(ioConnection);
                }
                ioData.Add("outputs", outputs);
                ioData.Add("oldID", ioEntity.net.ID);
                var electricalBranch = ioEntity.GetComponentInParent<ElectricalBranch>();
                if (electricalBranch != null)
                {
                    ioData.Add("branchAmount", electricalBranch.branchAmount);
                }

                /*var counter = ioEntity.GetComponentInParent<PowerCounter>();
                if (counter != null)
                {
                    ioData.Add("targetNumber", counter.GetTarget());
                }*/

                var timer = ioEntity.GetComponentInParent<TimerSwitch>();
                if (timer != null)
                {
                    ioData.Add("timerLength", timer.timerLength);
                }

                data.Add("IOEntity", ioData);
            }

            return data;
        }

        private object FindBestHeight(HashSet<Dictionary<string, object>> entities, Vector3 startPos)
        {
            float maxHeight = 0f;

            foreach (var entity in entities)
            {
                if (((string)entity["prefabname"]).Contains("/foundation/"))
                {
                    var foundHeight = GetGround((Vector3)entity["position"]);

                    if (foundHeight != null)
                    {
                        var height = (Vector3)foundHeight;

                        if (height.y > maxHeight)
                            maxHeight = height.y;
                    }
                }
            }

            maxHeight += 1f;

            return maxHeight;
        }

        private bool FindRayEntity(Vector3 sourcePos, Vector3 sourceDir, out Vector3 point, out BaseEntity entity, int rayLayer)
        {
            RaycastHit hitinfo;
            entity = null;
            point = Vector3.zero;

            if (!Physics.Raycast(sourcePos, sourceDir, out hitinfo, 1000f, rayLayer))
                return false;

            entity = hitinfo.GetEntity();
            point = hitinfo.point;

            return true;
        }

        private void FixSignage(Signage sign, byte[] imageBytes)
        {
            if (!signSizes.ContainsKey(sign.ShortPrefabName))
                return;

            byte[] resizedImage = ImageResize(imageBytes, signSizes[sign.ShortPrefabName].width, signSizes[sign.ShortPrefabName].height);

            sign.textureID = FileStorage.server.Store(resizedImage, FileStorage.Type.png, sign.net.ID);
        }

        private object GetGround(Vector3 pos)
        {
            RaycastHit hitInfo;

            if (Physics.Raycast(pos, Vector3.up, out hitInfo, groundLayer))
                return hitInfo.point;

            if (Physics.Raycast(pos, Vector3.down, out hitInfo, groundLayer))
                return hitInfo.point;

            return null;
        }

		private int GetItemID(int itemID)
		{
			if(replaceItemID.ContainsKey(itemID))
				return replaceItemID[itemID];
		
			return itemID;
		}
		
        private bool HasAccess(BasePlayer player, string permName)
        {
            return player.IsAdmin || permission.UserHasPermission(player.UserIDString, permName);
        }

        private byte[] ImageResize(byte[] imageBytes, int width, int height)
        {
            Bitmap resizedImage = new Bitmap(width, height),
                   sourceImage = new Bitmap(new MemoryStream(imageBytes));

            Graphics.FromImage(resizedImage).DrawImage(sourceImage, new Rectangle(0, 0, width, height), new Rectangle(0, 0, sourceImage.Width, sourceImage.Height), GraphicsUnit.Pixel);

            MemoryStream ms = new MemoryStream();
            resizedImage.Save(ms, ImageFormat.Png);

            return ms.ToArray();
        }

        private string Lang(string key, string userID = null, params object[] args) => string.Format(lang.GetMessage(key, this, userID), args);

        private Vector3 NormalizePosition(Vector3 InitialPos, Vector3 CurrentPos, float diffRot)
        {
            var transformedPos = CurrentPos - InitialPos;
            var newX = (transformedPos.x * (float)System.Math.Cos(-diffRot)) + (transformedPos.z * (float)System.Math.Sin(-diffRot));
            var newZ = (transformedPos.z * (float)System.Math.Cos(-diffRot)) - (transformedPos.x * (float)System.Math.Sin(-diffRot));

            transformedPos.x = newX;
            transformedPos.z = newZ;

            return transformedPos;
        }

        private void Paste(ICollection<Dictionary<string, object>> entities, Dictionary<string, object> protocol, Vector3 startPos, BasePlayer player, bool stability, float RotationCorrection, float heightAdj, bool auth, Action callback)
        {

            var ioEntities = new Dictionary<uint, Dictionary<string, object>>();
            uint buildingID = 0;
			
			//Settings
			
			bool isItemReplace = !protocol.ContainsKey("items");

            var eulerRotation = new Vector3(0f, RotationCorrection * 57.2958f, 0f);
            var quaternionRotation = Quaternion.Euler(eulerRotation);

            var pasteData = new PasteData()
            {
                HeightAdj = heightAdj,
                IsItemReplace = isItemReplace,
                Entities = entities,
                Player = player,
                QuaternionRotation = quaternionRotation,
                StartPos = startPos,
                Stability = stability,
                Auth = auth,
                callback = callback
            };

            NextTick(() => PasteLoop(pasteData));
        }

        private void PasteLoop(PasteData pasteData)
        {
            var entities = pasteData.Entities;
            var todo = entities.Take(config.PasteBatchSize).ToArray();
            var player = pasteData.Player;

            foreach (var data in todo)
            {
                entities.Remove(data);
                var prefabname = (string)data["prefabname"];
                ulong skinid = ulong.Parse(data["skinid"].ToString());
                var pos = (Vector3)data["position"];
                var rot = (Quaternion)data["rotation"];

                ulong ownerId = player?.userID ?? 0;
                if (data.ContainsKey("ownerid"))
                {
                    ownerId = Convert.ToUInt64(data["ownerid"]);
                }

                if (CheckPlaced(prefabname, pos, rot))
                    continue;

                if (prefabname.Contains("pillar"))
                    continue;

                var entity = GameManager.server.CreateEntity(prefabname, pos, rot, true);

                if (entity == null)
                    continue;

                entity.transform.position = pos;
                entity.transform.rotation = rot;

                if (player != null)
                    entity.SendMessage("SetDeployedBy", player, SendMessageOptions.DontRequireReceiver);

                entity.OwnerID = ownerId;

                BuildingBlock buildingBlock = entity as BuildingBlock;

                if (buildingBlock != null)
                {
                    buildingBlock.blockDefinition = PrefabAttribute.server.Find<Construction>(buildingBlock.prefabID);
                    buildingBlock.SetGrade((BuildingGrade.Enum)data["grade"]);
                    if(!pasteData.Stability)
                        buildingBlock.grounded = true;

                }

                DecayEntity decayEntity = entity as DecayEntity;

                if (decayEntity != null)
                {
                    if (pasteData.BuildingID == 0)
                        pasteData.BuildingID = BuildingManager.server.NewBuildingID();

                    decayEntity.AttachToBuilding(pasteData.BuildingID);
                }

                StabilityEntity stabilityEntity = entity as StabilityEntity;

                if (stabilityEntity != null)
                {
                    if (!stabilityEntity.grounded)
                    {
                        stabilityEntity.grounded = true;
                        pasteData.StabilityEntities.Add(stabilityEntity);
                    }
                }

                entity.skinID = skinid;
                entity.Spawn();

                var baseCombat = entity as BaseCombatEntity;

                if (baseCombat != null)
                    baseCombat.ChangeHealth(baseCombat.MaxHealth());

                pasteData.PastedEntities.AddRange(TryPasteSlots(entity, data, pasteData));

                var box = entity as StorageContainer;

                if (box != null)
                {
                    Locker locker = box as Locker;

                    if (locker != null)
                        locker.equippingActive = true;

                    box.inventory.Clear();

                    var items = new List<object>();

                    if (data.ContainsKey("items"))
                        items = data["items"] as List<object>;

                    foreach (var itemDef in items)
                    {
                        var item = itemDef as Dictionary<string, object>;
                        var itemid = Convert.ToInt32(item["id"]);
                        var itemamount = Convert.ToInt32(item["amount"]);
                        var itemskin = ulong.Parse(item["skinid"].ToString());
                        var itemcondition = Convert.ToSingle(item["condition"]);

                        if (pasteData.IsItemReplace)
                            itemid = GetItemID(itemid);

                        var i = ItemManager.CreateByItemID(itemid, itemamount, itemskin);

                        if (i != null)
                        {
                            i.condition = itemcondition;

                            if (item.ContainsKey("text"))
                                i.text = item["text"].ToString();

                            if (item.ContainsKey("blueprintTarget"))
                            {
                                int blueprintTarget = Convert.ToInt32(item["blueprintTarget"]);

                                if (pasteData.IsItemReplace)
                                    blueprintTarget = GetItemID(blueprintTarget);

                                i.blueprintTarget = blueprintTarget;
                            }
                            if (item.ContainsKey("magazine"))
                            {
                                var heldent = i.GetHeldEntity();

                                if (heldent != null)
                                {
                                    var projectiles = heldent.GetComponent<BaseProjectile>();

                                    if (projectiles != null)
                                    {
                                        var magazine = item["magazine"] as Dictionary<string, object>;
                                        var ammotype = int.Parse(magazine.Keys.ToArray()[0]);
                                        var ammoamount = int.Parse(magazine[ammotype.ToString()].ToString());

                                        if (pasteData.IsItemReplace)
                                            ammotype = GetItemID(ammotype);

                                        projectiles.primaryMagazine.ammoType = ItemManager.FindItemDefinition(ammotype);
                                        projectiles.primaryMagazine.contents = ammoamount;
                                    }

                                    //TODO Doesn't add water to some containers

                                    if (item.ContainsKey("items"))
                                    {
                                        var itemContainsList = item["items"] as List<object>;

                                        foreach (var itemContains in itemContainsList)
                                        {
                                            var contents = itemContains as Dictionary<string, object>;

                                            int contentsItemID = Convert.ToInt32(contents["id"]);

                                            if (pasteData.IsItemReplace)
                                                contentsItemID = GetItemID(contentsItemID);

                                            i.contents.AddItem(ItemManager.FindItemDefinition(contentsItemID), Convert.ToInt32(contents["amount"]));
                                        }
                                    }
                                }
                            }

                            int targetPos = -1;

                            if (item.ContainsKey("position"))
                                targetPos = Convert.ToInt32(item["position"]);

                            i.MoveToContainer(box.inventory, targetPos);
                        }
                    }

                    if (locker != null)
                        locker.equippingActive = false;
                }

                var sign = entity as Signage;

                if (sign != null && data.ContainsKey("sign"))
                {
                    var signData = data["sign"] as Dictionary<string, object>;

                    if (signData.ContainsKey("texture"))
                    {
                        byte[] imageBytes = Convert.FromBase64String(signData["texture"].ToString());

                        FixSignage(sign, imageBytes);
                    }

                    if (Convert.ToBoolean(signData["locked"]))
                        sign.SetFlag(BaseEntity.Flags.Locked, true);

                    sign.SendNetworkUpdate();
                }

                var sleepingBag = entity as SleepingBag;

                if (sleepingBag != null && data.ContainsKey("sleepingbag"))
                {
                    var bagData = data["sleepingbag"] as Dictionary<string, object>;

                    sleepingBag.niceName = bagData["niceName"].ToString();
                    sleepingBag.deployerUserID = ulong.Parse(bagData["deployerUserID"].ToString());
                    sleepingBag.SetPublic(Convert.ToBoolean(bagData["isPublic"]));
                }

                var autoturret = entity as AutoTurret;

                if (autoturret != null)
                {
                    List<ulong> authorizedPlayers = new List<ulong>();

                    if (data.ContainsKey("autoturret"))
                    {
                        var autoTurretData = data["autoturret"] as Dictionary<string, object>;
                        authorizedPlayers = (autoTurretData["authorizedPlayers"] as List<object>).Select(Convert.ToUInt64).ToList();
                    }

                    if (player != null && !authorizedPlayers.Contains(player.userID) && pasteData.Auth)
                        authorizedPlayers.Add(player.userID);

                    foreach (var userID in authorizedPlayers)
                    {
                        autoturret.authorizedPlayers.Add(new PlayerNameID()
                        {
                            userid = Convert.ToUInt64(userID),
                            username = "Player"
                        });
                    }

                    autoturret.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                }

                var cupboard = entity as BuildingPrivlidge;

                if (cupboard != null)
                {
                    List<ulong> authorizedPlayers = new List<ulong>();

                    if (data.ContainsKey("cupboard"))
                    {
                        var cupboardData = data["cupboard"] as Dictionary<string, object>;
                        authorizedPlayers = (cupboardData["authorizedPlayers"] as List<object>).Select(Convert.ToUInt64).ToList();
                    }

                    if (player != null && !authorizedPlayers.Contains(player.userID) && pasteData.Auth)
                        authorizedPlayers.Add(player.userID);

                    foreach (var userID in authorizedPlayers)
                    {
                        cupboard.authorizedPlayers.Add(new PlayerNameID()
                        {
                            userid = Convert.ToUInt64(userID),
                            username = "Player"
                        });
                    }

                    cupboard.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                }

                var vendingMachine = entity as VendingMachine;

                if (vendingMachine != null && data.ContainsKey("vendingmachine"))
                {
                    var vendingData = data["vendingmachine"] as Dictionary<string, object>;

                    vendingMachine.shopName = vendingData["shopName"].ToString();
                    vendingMachine.SetFlag(BaseEntity.Flags.Reserved4, Convert.ToBoolean(vendingData["isBroadcasting"]));

                    var sellOrders = vendingData["sellOrders"] as List<object>;

                    foreach (var orderPreInfo in sellOrders)
                    {
                        var orderInfo = orderPreInfo as Dictionary<string, object>;

                        if (!orderInfo.ContainsKey("inStock"))
                        {
                            orderInfo["inStock"] = 0;
                            orderInfo["currencyIsBP"] = false;
                            orderInfo["itemToSellIsBP"] = false;
                        }

                        int itemToSellID = Convert.ToInt32(orderInfo["itemToSellID"]),
                            currencyID = Convert.ToInt32(orderInfo["currencyID"]);

                        if (pasteData.IsItemReplace)
                        {
                            itemToSellID = GetItemID(itemToSellID);
                            currencyID = GetItemID(currencyID);
                        }

                        vendingMachine.sellOrders.sellOrders.Add(new ProtoBuf.VendingMachine.SellOrder()
                        {
                            ShouldPool = false,
                            itemToSellID = itemToSellID,
                            itemToSellAmount = Convert.ToInt32(orderInfo["itemToSellAmount"]),
                            currencyID = currencyID,
                            currencyAmountPerItem = Convert.ToInt32(orderInfo["currencyAmountPerItem"]),
                            inStock = Convert.ToInt32(orderInfo["inStock"]),
                            currencyIsBP = Convert.ToBoolean(orderInfo["currencyIsBP"]),
                            itemToSellIsBP = Convert.ToBoolean(orderInfo["itemToSellIsBP"]),
                        });
                    }

                    vendingMachine.FullUpdate();
                }

                var ioEntity = entity as IOEntity;

                if (ioEntity != null)
                {
                    var ioData = new Dictionary<string, object>();

                    if (data.ContainsKey("IOEntity"))
                    {
                        ioData = data["IOEntity"] as Dictionary<string, object> ?? new Dictionary<string, object>();
                    }

                    ioData.Add("entity", ioEntity);
                    ioData.Add("newId", ioEntity.net.ID);

                    object oldIdObject = 0;
                    if (ioData.TryGetValue("oldID", out oldIdObject))
                    {
                        var oldId = Convert.ToUInt32(oldIdObject);
                        pasteData.IoEntities.Add(oldId, ioData);
                    }
                }

                var flagsData = new Dictionary<string, object>();

                if (data.ContainsKey("flags"))
                    flagsData = data["flags"] as Dictionary<string, object>;

                var flags = new Dictionary<BaseEntity.Flags, bool>();

                foreach (var flagData in flagsData)
                {
                    BaseEntity.Flags baseFlag;
                    if(Enum.TryParse(flagData.Key, out baseFlag))
                        flags.Add(baseFlag, Convert.ToBoolean(flagData.Value));
                }

                foreach (var flag in flags)
                {
                    entity.SetFlag(flag.Key, flag.Value);
                }

                pasteData.PastedEntities.Add(entity);
            }

            if (entities.Count > 0)
                NextTick(() => PasteLoop(pasteData));
            else
            {
                foreach (var ioData in pasteData.IoEntities.Values.ToArray())
                {
                    if (!ioData.ContainsKey("entity"))
                        continue;


                    var ioEntity = ioData["entity"] as IOEntity;

                    List<object> inputs = null;
                    if (ioData.ContainsKey("inputs"))
                        inputs = ioData["inputs"] as List<object>;

                    var electricalBranch = ioEntity as ElectricalBranch;
                    if (electricalBranch != null && ioData.ContainsKey("branchAmount"))
                    {
                        electricalBranch.branchAmount = Convert.ToInt32(ioData["branchAmount"]);
                    }

                    // Realized counter.targetCounterNumber is private, leaving it in in case signature changes.
                    /*var counter = ioEntity.GetComponentInParent<PowerCounter>();
                    if (counter != null)
                    {
                        counter.targetCounterNumber = Convert.ToInt32(ioData["targetNumber"]);
                    }*/

                    var timer = ioEntity as TimerSwitch;
                    if (timer != null && ioData.ContainsKey("timerLength"))
                    {
                        timer.timerLength = Convert.ToInt32(ioData["timerLength"]);
                    }

                    var doorManipulator = ioEntity as CustomDoorManipulator;
                    if (doorManipulator != null)
                    {
                        Door door = doorManipulator.FindDoor(true);
                        doorManipulator.SetTargetDoor(door);
                    }

                    if (inputs != null && inputs.Count > 0)
                    {
                        for (int index = 0; index < inputs.Count; index++)
                        {
                            var input = inputs[index] as Dictionary<string, object>;
                            object oldIdObject;
                            if (!input.TryGetValue("connectedID", out oldIdObject))
                                continue;

                            uint oldId = Convert.ToUInt32(oldIdObject);

                            if (oldId != 0 && pasteData.IoEntities.ContainsKey(oldId))
                            {
                                if (ioEntity.inputs[index] == null)
                                    ioEntity.inputs[index] = new IOEntity.IOSlot();

                                Dictionary<string, object> ioConnection = pasteData.IoEntities[oldId];

                                object temp;

                                if (ioConnection.ContainsKey("newId"))
                                {
                                    ioEntity.inputs[index].connectedTo.entityRef.uid =
                                        Convert.ToUInt32(ioConnection["newId"]);
                                }
                            }
                        }
                    }

                    List<object> outputs = null;
                    if (ioData.ContainsKey("outputs"))
                        outputs = ioData["outputs"] as List<object>;

                    if (outputs != null && outputs.Count > 0)
                    {
                        for (int index = 0; index < outputs.Count; index++)
                        {
                            var output = outputs[index] as Dictionary<string, object>;
                            var oldId = Convert.ToUInt32(output["connectedID"]);

                            if (oldId != 0 && pasteData.IoEntities.ContainsKey(oldId))
                            {
                                if (ioEntity.outputs[index] == null)
                                    ioEntity.outputs[index] = new IOEntity.IOSlot();

                                Dictionary<string, object> ioConnection = pasteData.IoEntities[oldId];

                                if (ioConnection.ContainsKey("newId"))
                                {
                                    var ioEntity2 = ioConnection["entity"] as IOEntity;
                                    var connectedToSlot = Convert.ToInt32(output["connectedToSlot"]);
                                    IOEntity.IOSlot ioOutput = ioEntity.outputs[index];

                                    ioOutput.connectedTo = new IOEntity.IORef();
                                    ioOutput.connectedTo.Set(ioEntity2);
                                    ioOutput.connectedToSlot = connectedToSlot;
                                    ioOutput.connectedTo.Init();

                                    ioEntity2.inputs[connectedToSlot].connectedTo = new IOEntity.IORef();
                                    ioEntity2.inputs[connectedToSlot].connectedTo.Set(ioEntity);
                                    ioEntity2.inputs[connectedToSlot].connectedToSlot = index;
                                    ioEntity2.inputs[connectedToSlot].connectedTo.Init();

                                    ioOutput.niceName = output["niceName"] as string;

                                    ioOutput.type = (IOEntity.IOType) Convert.ToInt32(output["type"]);
                                }

                                if (output.ContainsKey("linePoints"))
                                {
                                    var linePoints = output["linePoints"] as List<object>;
                                    if (linePoints != null)
                                    {
                                        List<Vector3> lineList = new List<Vector3>();
                                        foreach (var point in linePoints)
                                        {
                                            var linePoint = point as Dictionary<string, object>;
                                            // Get the relative positions (normalized) and convert it to an actual position.
                                            var normalizedPos =
                                                pasteData.QuaternionRotation * (new Vector3(
                                                    Convert.ToSingle(linePoint["x"]),
                                                    Convert.ToSingle(linePoint["y"]) + pasteData.HeightAdj,
                                                    Convert.ToSingle(linePoint["z"]))) + pasteData.StartPos;
                                            lineList.Add(normalizedPos);
                                        }

                                        ioEntity.outputs[index].linePoints = lineList.ToArray();
                                    }
                                }
                            }
                        }
                    }
                }

                pasteData.IoEntities.Values.ToList().ForEach(ioData =>
                {
                    var ioEntity = ioData["entity"] as IOEntity;

                    if (ioEntity != null)
                    {
                        ioEntity.MarkDirtyForceUpdateOutputs();
                        ioEntity.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                    }
                });

                foreach (var entity in pasteData.StabilityEntities)
                {
                    entity.grounded = false;
                    entity.InitializeSupports();
                    entity.UpdateStability();
                }

                if (player != null)
                {
                    SendReply(player, Lang("PASTE_SUCCESS", player.UserIDString));
#if DEBUG
                    SendReply(player, $"Stopwatch took: {pasteData.Sw.Elapsed.TotalMilliseconds} ms");
#endif
                }
                else
                {
                    Puts(Lang("PASTE_SUCCESS"));
                }

                if (!lastPastes.ContainsKey(player?.UserIDString ?? serverID))
                    lastPastes[player?.UserIDString ?? serverID] = new Stack<List<BaseEntity>>();

                lastPastes[player?.UserIDString ?? serverID].Push(pasteData.PastedEntities);

                pasteData.callback?.Invoke();

                Interface.CallHook("OnPasteFinished", pasteData.PastedEntities);
            }
        }

        private HashSet<Dictionary<string, object>> PreLoadData(List<object> entities, Vector3 startPos, float RotationCorrection, bool deployables, bool inventories, bool auth, bool vending)
        {
            var eulerRotation = new Vector3(0f, RotationCorrection, 0f);
            var quaternionRotation = Quaternion.EulerRotation(eulerRotation);
            var preloaddata = new HashSet<Dictionary<string, object>>();

            foreach (var entity in entities)
            {
                var data = entity as Dictionary<string, object>;

                if (!deployables && !data.ContainsKey("grade"))
                    continue;

                var pos = (Dictionary<string, object>)data["pos"];
                var rot = (Dictionary<string, object>)data["rot"];

                data.Add("position", quaternionRotation * (new Vector3(Convert.ToSingle(pos["x"]), Convert.ToSingle(pos["y"]), Convert.ToSingle(pos["z"]))) + startPos);
                data.Add("rotation", Quaternion.EulerRotation(eulerRotation + new Vector3(Convert.ToSingle(rot["x"]), Convert.ToSingle(rot["y"]), Convert.ToSingle(rot["z"]))));

                if (!inventories && data.ContainsKey("items"))
                    data["items"] = new List<object>();

                if (!vending && data["prefabname"].ToString().Contains("vendingmachine"))
                    data.Remove("vendingmachine");

                preloaddata.Add(data);
            }

            return preloaddata;
        }

        private object TryCopy(Vector3 sourcePos, Vector3 sourceRot, string filename, float RotationCorrection, string[] args, BasePlayer player, Action callback)
        {
            bool saveShare = config.Copy.Share, saveTree = config.Copy.Tree, eachToEach = config.Copy.EachToEach;
            CopyMechanics copyMechanics = CopyMechanics.Proximity;
            float radius = 3f;

            for (int i = 0; ; i = i + 2)
            {
                if (i >= args.Length)
                    break;

                int valueIndex = i + 1;

                if (valueIndex >= args.Length)
                    return Lang("SYNTAX_COPY", null);

                string param = args[i].ToLower();

                switch (param)
                {
                    case "e":
                    case "each":
                        if (!bool.TryParse(args[valueIndex], out eachToEach))
                            return Lang("SYNTAX_BOOL", null, param);

                        break;

                    case "m":
                    case "method":
                        switch (args[valueIndex].ToLower())
                        {
                            case "b":
                            case "building":
                                copyMechanics = CopyMechanics.Building;
                                break;

                            case "p":
                            case "proximity":
                                copyMechanics = CopyMechanics.Proximity;
                                break;
                        }

                        break;

                    case "r":
                    case "radius":
                        if (!float.TryParse(args[valueIndex], out radius))
                            return Lang("SYNTAX_RADIUS", null);

                        break;

                    case "s":
                    case "share":
                        if (!bool.TryParse(args[valueIndex], out saveShare))
                            return Lang("SYNTAX_BOOL", null, param);

                        break;

                    case "t":
                    case "tree":
                        if (!bool.TryParse(args[valueIndex], out saveTree))
                            return Lang("SYNTAX_BOOL", null, param);

                        break;

                    default:
                        return Lang("SYNTAX_COPY", null);
                }
            }

            Copy(sourcePos, sourceRot, filename, RotationCorrection, copyMechanics, radius, saveTree, saveShare, eachToEach, player, callback);

            return true;
        }

        private void TryCopySlots(BaseEntity ent, IDictionary<string, object> housedata, bool saveShare)
        {
            foreach (BaseEntity.Slot slot in checkSlots)
            {
                if (!ent.HasSlot(slot))
                    continue;

                var slotEntity = ent.GetSlot(slot);

                if (slotEntity == null)
                    continue;

                var codedata = new Dictionary<string, object>
                {
                    {"prefabname", slotEntity.PrefabName},
                    {"flags", TryCopyFlags(ent)}
                };

                if (slotEntity.GetComponent<CodeLock>())
                {
                    CodeLock codeLock = slotEntity.GetComponent<CodeLock>();

                    codedata.Add("code", codeLock.code);

                    if (saveShare)
                        codedata.Add("whitelistPlayers", codeLock.whitelistPlayers);

                    if (codeLock.guestCode != null && codeLock.guestCode.Length == 4)
                    {
                        codedata.Add("guestCode", codeLock.guestCode);

                        if (saveShare)
                            codedata.Add("guestPlayers", codeLock.guestPlayers);
                    }
                }
                else if (slotEntity.GetComponent<KeyLock>())
                {
                    KeyLock keyLock = slotEntity.GetComponent<KeyLock>();
                    var code = keyLock.keyCode;

                    if (keyLock.firstKeyCreated)
                        code |= 0x80;

                    codedata.Add("code", code.ToString());
                }

                string slotName = slot.ToString().ToLower();

                housedata.Add(slotName, codedata);
            }
        }

        private Dictionary<string, object> TryCopyFlags(BaseEntity entity)
        {
            var flags = new Dictionary<string, object>();

            foreach (BaseEntity.Flags flag in Enum.GetValues(typeof(BaseEntity.Flags)))
            {
                flags.Add(flag.ToString(), entity.HasFlag(flag));
            }

            return flags;
        }

        private object TryPaste(Vector3 startPos, string filename, BasePlayer player, float RotationCorrection, string[] args, bool autoHeight = true, Action callback = null)
        {
            var userID = player?.UserIDString;

            string path = subDirectory + filename;

            if (!Interface.Oxide.DataFileSystem.ExistsDatafile(path))
                return Lang("FILE_NOT_EXISTS", userID);

            var data = Interface.Oxide.DataFileSystem.GetDatafile(path);

            if (data["default"] == null || data["entities"] == null)
                return Lang("FILE_BROKEN", userID);

            float heightAdj = 0f, blockCollision = 0f;
            bool auth = config.Paste.Auth, inventories = config.Paste.Inventories, deployables = config.Paste.Deployables, vending = config.Paste.VendingMachines, stability = config.Paste.Stability, ownership = config.Paste.EntityOwner;

            for (int i = 0; ; i = i + 2)
            {
                if (i >= args.Length)
                    break;

                int valueIndex = i + 1;

                if (valueIndex >= args.Length)
                    return Lang("SYNTAX_PASTE_OR_PASTEBACK", userID);

                string param = args[i].ToLower();

                switch (param)
                {
                    case "a":
                    case "auth":
                        if (!bool.TryParse(args[valueIndex], out auth))
                            return Lang("SYNTAX_BOOL", userID, param);

                        break;

                    case "b":
                    case "blockcollision":
                        if (!float.TryParse(args[valueIndex], out blockCollision))
                            return Lang("SYNTAX_BLOCKCOLLISION", userID);

                        break;

                    case "d":
                    case "deployables":
                        if (!bool.TryParse(args[valueIndex], out deployables))
                            return Lang("SYNTAX_BOOL", userID, param);

                        break;

                    case "h":
                    case "height":
                        if (!float.TryParse(args[valueIndex], out heightAdj))
                            return Lang("SYNTAX_HEIGHT", userID);

                        break;

                    case "i":
                    case "inventories":
                        if (!bool.TryParse(args[valueIndex], out inventories))
                            return Lang("SYNTAX_BOOL", userID, param);

                        break;

                    case "s":
                    case "stability":
                        if (!bool.TryParse(args[valueIndex], out stability))
                            return Lang("SYNTAX_BOOL", userID, param);

                        break;

                    case "v":
                    case "vending":
                        if (!bool.TryParse(args[valueIndex], out vending))
                            return Lang("SYNTAX_BOOL", userID, param);

                        break;

                    case "o":
                    case "entityowner":
                        if (!bool.TryParse(args[valueIndex], out ownership))
                            return Lang("SYNTAX_BOOL", userID, param);

                        break;

                    case "autoheight":
                        if (!bool.TryParse(args[valueIndex], out autoHeight))
                            return Lang("SYNTAX_BOOL", userID, param);

                        break;

                    default:
                        return Lang("SYNTAX_PASTE_OR_PASTEBACK", userID);
                }
            }

            startPos.y += heightAdj;

            var preloadData = PreLoadData(data["entities"] as List<object>, startPos, RotationCorrection, deployables, inventories, auth, vending);

            if (autoHeight)
            {
                var bestHeight = FindBestHeight(preloadData, startPos);

                if (bestHeight is string)
                    return bestHeight;

                heightAdj += ((float)bestHeight - startPos.y);

                foreach (var entity in preloadData)
                {
                    var pos = ((Vector3)entity["position"]);
                    pos.y += heightAdj;

                    entity["position"] = pos;
                }
            }

            if (blockCollision > 0f)
            {
                var collision = CheckCollision(preloadData, startPos, blockCollision);

                if (collision is string)
                    return collision;
            }

			var protocol = new Dictionary<string, object>();
			
			if(data["protocol"] != null)
				protocol = data["protocol"] as Dictionary<string, object>;

            Paste(preloadData, protocol, startPos, player, stability, RotationCorrection, autoHeight ? heightAdj : 0, auth, callback);
            return true;
        }

        private List<BaseEntity> TryPasteSlots(BaseEntity ent, Dictionary<string, object> structure, PasteData pasteData)
        {
            List<BaseEntity> entitySlots = new List<BaseEntity>();

            foreach (BaseEntity.Slot slot in checkSlots)
            {
                string slotName = slot.ToString().ToLower();

                if (!ent.HasSlot(slot) || !structure.ContainsKey(slotName))
                    continue;

                var slotData = structure[slotName] as Dictionary<string, object>;
                BaseEntity slotEntity = GameManager.server.CreateEntity((string)slotData["prefabname"], Vector3.zero, new Quaternion(), true);

                if (slotEntity == null)
                    continue;

                slotEntity.gameObject.Identity();
                slotEntity.SetParent(ent, slotName);
                slotEntity.OnDeployed(ent);
                slotEntity.Spawn();

                ent.SetSlot(slot, slotEntity);

                entitySlots.Add(slotEntity);

                if (slotName != "lock" || !slotData.ContainsKey("code"))
                    continue;

                if (slotEntity.GetComponent<CodeLock>())
                {
                    string code = (string)slotData["code"];

                    if (!string.IsNullOrEmpty(code))
                    {
                        CodeLock codeLock = slotEntity.GetComponent<CodeLock>();
                        codeLock.code = code;
                        codeLock.hasCode = true;

                        if (pasteData.Auth && pasteData.Player != null)
                            codeLock.whitelistPlayers.Add(pasteData.Player.userID);

                        if (slotData.ContainsKey("whitelistPlayers"))
                        {
                            foreach (var userID in slotData["whitelistPlayers"] as List<object>)
                            {
                                codeLock.whitelistPlayers.Add(Convert.ToUInt64(userID));
                            }
                        }

                        if (slotData.ContainsKey("guestCode"))
                        {
                            string guestCode = (string)slotData["guestCode"];

                            codeLock.guestCode = guestCode;
                            codeLock.hasGuestCode = true;

                            if (slotData.ContainsKey("guestPlayers"))
                            {
                                foreach (var userID in slotData["guestPlayers"] as List<object>)
                                {
                                    codeLock.guestPlayers.Add(Convert.ToUInt64(userID));
                                }
                            }
                        }

                        codeLock.SetFlag(BaseEntity.Flags.Locked, true);
                    }
                }
                else if (slotEntity.GetComponent<KeyLock>())
                {
                    int code = Convert.ToInt32(slotData["code"]);
                    KeyLock keyLock = slotEntity.GetComponent<KeyLock>();

                    if ((code & 0x80) != 0)
                    {
                        keyLock.keyCode = (code & 0x7F);
                        keyLock.firstKeyCreated = true;
                        keyLock.SetFlag(BaseEntity.Flags.Locked, true);
                    }
                }
            }

            return entitySlots;
        }

        private object TryPasteBack(string filename, BasePlayer player, string[] args)
        {
            string path = subDirectory + filename;

            if (!Interface.Oxide.DataFileSystem.ExistsDatafile(path))
                return Lang("FILE_NOT_EXISTS", player?.UserIDString);

            var data = Interface.Oxide.DataFileSystem.GetDatafile(path);

            if (data["default"] == null || data["entities"] == null)
                return Lang("FILE_BROKEN", player?.UserIDString);

            var defaultdata = data["default"] as Dictionary<string, object>;
            var pos = defaultdata["position"] as Dictionary<string, object>;
            var rotationCorrection = Convert.ToSingle(defaultdata["rotationdiff"]);
            var startPos = new Vector3(Convert.ToSingle(pos["x"]), Convert.ToSingle(pos["y"]), Convert.ToSingle(pos["z"]));

            return TryPaste(startPos, filename, player, rotationCorrection, args, autoHeight: false);
        }

        //Сhat commands

        [ChatCommand("copy")]
        private void cmdChatCopy(BasePlayer player, string command, string[] args)
        {
            if (!HasAccess(player, copyPermission))
            {
                SendReply(player, Lang("NO_ACCESS", player.UserIDString));
                return;
            }

            if (args.Length < 1)
            {
                SendReply(player, Lang("SYNTAX_COPY", player.UserIDString));
                return;
            }

            var savename = args[0];
            var success = TryCopyFromSteamID(player.userID, savename, args.Skip(1).ToArray());

            if (success is string)
            {
                SendReply(player, (string)success);
                return;
            }
        }

        [ChatCommand("paste")]
        private void cmdChatPaste(BasePlayer player, string command, string[] args)
        {
            if (!HasAccess(player, pastePermission))
            {
                SendReply(player, Lang("NO_ACCESS", player.UserIDString));
                return;
            }

            if (args.Length < 1)
            {
                SendReply(player, Lang("SYNTAX_PASTE_OR_PASTEBACK", player.UserIDString));
                return;
            }

            var success = TryPasteFromSteamID(player.userID, args[0], args.Skip(1).ToArray());

            if (success is string)
            {
                SendReply(player, (string)success);
                return;
            }
        }

		[ChatCommand("list")]
        private void cmdChatList(BasePlayer player, string command, string[] args)
        {
            if (!HasAccess(player, listPermission))
            {
                SendReply(player, Lang("NO_ACCESS", player.UserIDString));
                return;
            }

            string[] files = Interface.Oxide.DataFileSystem.GetFiles(subDirectory);
            
			List<string> fileList = new List<string>();
			
            foreach(string file in files)
            {
                string[] strFileParts = file.Split('/');
                string justfile = strFileParts[strFileParts.Length - 1].Replace(".json", "");
                fileList.Add(justfile);
            }
			
            SendReply(player, Lang("AVAILABLE_STRUCTURES", player.UserIDString));
            SendReply(player, string.Join(", ", fileList.ToArray()));
        }
		
        [ChatCommand("pasteback")]
        private void cmdChatPasteBack(BasePlayer player, string command, string[] args)
        {
            if (!HasAccess(player, pastebackPermission))
            {
                SendReply(player, Lang("NO_ACCESS", player.UserIDString));
                return;
            }

            var result = cmdPasteBack(player, args);

            if (result is string)
                SendReply(player, (string)result);
        }

        [ChatCommand("undo")]
        private void cmdChatUndo(BasePlayer player, string command, string[] args)
        {
            if (!HasAccess(player, undoPermission))
            {
                SendReply(player, Lang("NO_ACCESS", player.UserIDString));
                return;
            }

            cmdUndo(player.UserIDString, args);
        }

        //Console commands [From Server]

        [ConsoleCommand("pasteback")]
        private void cmdConsolePasteBack(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin)
                return;

            var result = cmdPasteBack(arg.Player(), arg.Args);

            if (result is string)
                SendReply(arg, (string)result);
        }

        [ConsoleCommand("undo")]
        private void cmdConsoleUndo(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin)
                return;

            BasePlayer player = arg.Player();

            cmdUndo(player == null ? serverID : player.UserIDString, arg.Args);
        }

		//Replace between old ItemID to new ItemID
	
		private static readonly Dictionary<int, int> replaceItemID = new Dictionary<int, int>
        {
            { -1461508848, 1545779598 },
            { 2115555558, 588596902 },
            { -533875561, 785728077 },
            { 1621541165, 51984655 },
            { -422893115, -1691396643 },
            { 815896488, -1211166256 },
            { 805088543, -1321651331 },
            { 449771810, 605467368 },
            { 1152393492, 1712070256 },
            { 1578894260, -742865266 },
            { 1436532208, 1638322904 },
            { 542276424, -1841918730 },
            { 1594947829, -17123659 },
            { -1035059994, -1685290200 },
            { 1818890814, -1036635990 },
            { 1819281075, -727717969 },
            { 1685058759, -1432674913 },
            { 93029210, 1548091822 },
            { -1565095136, 352130972 },
            { -1775362679, 215754713 },
            { -1775249157, 14241751 },
            { -1280058093, -1023065463 },
            { -420273765, -1234735557 },
            { 563023711, -2139580305 },
            { 790921853, -262590403 },
            { -337261910, -2072273936 },
            { 498312426, -1950721390 },
            { 504904386, 1655650836 },
            { -1221200300, -559599960 },
            { 510887968, 15388698 },
            { -814689390, 866889860 },
            { 1024486167, 1382263453 },
            { 2021568998, 609049394 },
            { 97329, 1099314009 },
            { 1046072789, -582782051 },
            { 97409, -1273339005 },
            { -1480119738, -1262185308 },
            { 1611480185, 1931713481 },
            { -1386464949, 1553078977 },
            { 93832698, 1776460938 },
            { -1063412582, -586342290 },
            { -1887162396, -996920608 },
            { -55660037, 1588298435 },
            { 919780768, 1711033574 },
            { -365801095, 1719978075 },
            { 68998734, 613961768 },
            { -853695669, 1443579727 },
            { 271534758, 833533164 },
            { -770311783, -180129657 },
            { -1192532973, 1424075905 },
            { -307490664, 1525520776 },
            { 707427396, 602741290 },
            { 707432758, -761829530 },
            { -2079677721, 1783512007 },
            { -1342405573, -1316706473 },
            { -139769801, 1946219319 },
            { -1043746011, -700591459 },
            { 2080339268, 1655979682 },
            { -171664558, -1941646328 },
            { 1050986417, -1557377697 },
            { -1693683664, 1789825282 },
            { 523409530, 1121925526 },
            { 1300054961, 634478325 },
            { -2095387015, 1142993169 },
            { 1428021640, 1104520648 },
            { 94623429, 1534542921 },
            { 1436001773, -1938052175 },
            { 1711323399, 1973684065 },
            { 1734319168, -1848736516 },
            { -1658459025, -1440987069 },
            { -726947205, -751151717 },
            { -341443994, 363467698 },
            { 1540879296, 2009734114 },
            { 94756378, -858312878 },
            { 3059095, 204391461 },
            { 3059624, 1367190888 },
            { 2045107609, -778875547 },
            { 583366917, 998894949 },
            { 2123300234, 1965232394 },
            { 1983936587, -321733511 },
            { 1257201758, -97956382 },
            { -1144743963, 296519935 },
            { -1144542967, -113413047 },
            { -1144334585, -2022172587 },
            { 1066729526, -1101924344 },
            { -1598790097, 1390353317 },
            { -933236257, 1221063409 },
            { -1575287163, -1336109173 },
            { -2104481870, -2067472972 },
            { -1571725662, 1353298668 },
            { 1456441506, 1729120840 },
            { 1200628767, -1112793865 },
            { -778796102, 1409529282 },
            { 1526866730, 674734128 },
            { 1925723260, -1519126340 },
            { 1891056868, 1401987718 },
            { 1295154089, -1878475007 },
            { 498591726, 1248356124 },
            { 1755466030, -592016202 },
            { 726730162, 798638114 },
            { -1034048911, -1018587433 },
            { 252529905, 274502203 },
            { 471582113, -1065444793 },
            { -1138648591, 16333305 },
            { 305916740, 649305914 },
            { 305916742, 649305916 },
            { 305916744, 649305918 },
            { 1908328648, -1535621066 },
            { -2078972355, 1668129151 },
            { -533484654, 989925924 },
            { 1571660245, 1569882109 },
            { 1045869440, -1215753368 },
            { 1985408483, 528668503 },
            { 97513422, 304481038 },
            { 1496470781, -196667575 },
            { 1229879204, 952603248 },
            { -1722829188, 936496778 },
            { 1849912854, 1948067030 },
            { -1266285051, 1413014235 },
            { -1749787215, -1000573653 },
            { 28178745, -946369541 },
            { -505639592, -1999722522 },
            { 1598149413, -1992717673 },
            { -1779401418, -691113464 },
            { -57285700, -335089230 },
            { 98228420, 479143914 },
            { 1422845239, 999690781 },
            { 277631078, -1819763926 },
            { 115739308, 1366282552 },
            { -522149009, -690276911 },
            { 3175989, -1899491405 },
            { 718197703, -746030907 },
            { 384204160, 1840822026 },
            { -1308622549, 143803535 },
            { -217113639, -2124352573 },
            { -1580059655, -265876753 },
            { -1832205789, 1070894649 },
            { 305916741, 649305917 },
            { 936777834, 3222790 },
            { -1224598842, 200773292 },
            { -1976561211, -1506397857 },
            { -1406876421, 1675639563 },
            { -1397343301, -23994173 },
            { 1260209393, 850280505 },
            { -1035315940, 1877339384 },
            { -1381682752, 1714496074 },
            { 696727039, -1022661119 },
            { -2128719593, -803263829 },
            { -1178289187, -1903165497 },
            { 1351172108, 1181207482 },
            { -450738836, -1539025626 },
            { -966287254, -324675402 },
            { 340009023, 671063303 },
            { 124310981, -1478212975 },
            { 1501403549, -2094954543 },
            { 698310895, -1252059217 },
            { 523855532, 1266491000 },
            { 2045246801, -886280491 },
            { 583506109, -237809779 },
            { -148163128, 794356786 },
            { -132588262, -1773144852 },
            { -1666761111, 196700171 },
            { -465236267, 442289265 },
            { -1211618504, 1751045826 },
            { 2133577942, -1982036270 },
            { -1014825244, -682687162 },
            { -991829475, 1536610005 },
            { -642008142, -1709878924 },
            { 661790782, 1272768630 },
            { -1440143841, -1780802565 },
            { 569119686, 1746956556 },
            { 1404466285, -1102429027 },
            { -1616887133, -48090175 },
            { -1167640370, -1163532624 },
            { -1284735799, 1242482355 },
            { -1278649848, -1824943010 },
            { 776005741, 1814288539 },
            { 108061910, -316250604 },
            { 255101535, -1663759755 },
            { -51678842, 1658229558 },
            { -789202811, 254522515 },
            { 516382256, -132516482 },
            { 50834473, 1381010055 },
            { -975723312, 1159991980 },
            { 1908195100, -850982208 },
            { -1097452776, -110921842 },
            { 146685185, -1469578201 },
            { -1716193401, -1812555177 },
            { 193190034, -2069578888 },
            { 371156815, -852563019 },
            { 3343606, -1966748496 },
            { 825308669, -1137865085 },
            { 830965940, -586784898 },
            { 1662628660, -163828118 },
            { 1662628661, -163828117 },
            { 1662628662, -163828112 },
            { -1832205788, 1070894648 },
            { -1832205786, 1070894646 },
            { 1625090418, 181590376 },
            { -1269800768, -874975042 },
            { 429648208, -1190096326 },
            { -1832205787, 1070894647 },
            { -1832205785, 1070894645 },
            { 107868, 696029452 },
            { 997973965, -2012470695 },
            { -46188931, -702051347 },
            { -46848560, -194953424 },
            { -2066726403, -989755543 },
            { -2043730634, 1873897110 },
            { 1325935999, -1520560807 },
            { -225234813, -78533081 },
            { -202239044, -1509851560 },
            { -322501005, 1422530437 },
            { -1851058636, 1917703890 },
            { -1828062867, -1162759543 },
            { -1966381470, -1130350864 },
            { 968732481, 1391703481 },
            { 991728250, -242084766 },
            { -253819519, 621915341 },
            { -1714986849, 1827479659 },
            { -1691991080, 813023040 },
            { 179448791, -395377963 },
            { 431617507, -1167031859 },
            { 688032252, 69511070 },
            { -1059362949, -4031221 },
            { 1265861812, 1110385766 },
            { 374890416, 317398316 },
            { 1567404401, 1882709339 },
            { -1057402571, 95950017 },
            { -758925787, -1130709577 },
            { -1411620422, 1052926200 },
            { 88869913, -542577259 },
            { -2094080303, 1318558775 },
            { 843418712, -1962971928 },
            { -1569356508, -1405508498 },
            { -1569280852, 1478091698 },
            { 449769971, 1953903201 },
            { 590532217, -2097376851 },
            { 3387378, 1414245162 },
            { 1767561705, 1992974553 },
            { 106433500, 237239288 },
            { -1334615971, -1778159885 },
            { -135651869, 1722154847 },
            { -1595790889, 1850456855 },
            { -459156023, -1695367501 },
            { 106434956, -1779183908 },
            { -578028723, -1302129395 },
            { -586116979, 286193827 },
            { -1379225193, -75944661 },
            { -930579334, 649912614 },
            { 548699316, 818877484 },
            { 142147109, 1581210395 },
            { 148953073, 1903654061 },
            { 102672084, 980333378 },
            { 640562379, -1651220691 },
            { -1732316031, -1622660759 },
            { -2130280721, 756517185 },
            { -1725510067, -722241321 },
            { 1974032895, -1673693549 },
            { -225085592, -567909622 },
            { 509654999, 1898094925 },
            { 466113771, -1511285251 },
            { 2033918259, 1373971859 },
            { 2069925558, -1736356576 },
            { -1026117678, 803222026 },
            { 1987447227, -1861522751 },
            { 540154065, -544317637 },
            { 1939428458, 176787552 },
            { -288010497, -2002277461 },
            { -847065290, 1199391518 },
            { 3506021, 963906841 },
            { 649603450, 442886268 },
            { 3506418, 1414245522 },
            { 569935070, -1104881824 },
            { 113284, -1985799200 },
            { 1916127949, -277057363 },
            { -1775234707, -1978999529 },
            { -388967316, 1326180354 },
            { 2007564590, -575483084 },
            { -1705696613, 177226991 },
            { 670655301, -253079493 },
            { 1148128486, -1958316066 },
            { -141135377, 567235583 },
            { 109266897, -932201673 },
            { -527558546, 2087678962 },
            { -1745053053, -904863145 },
            { 1223860752, 573926264 },
            { -419069863, 1234880403 },
            { -1617374968, -1994909036 },
            { 2057749608, 1950721418 },
            { 24576628, -2025184684 },
            { -1659202509, 1608640313 },
            { 2107229499, -1549739227 },
            { 191795897, -765183617 },
            { -1009492144, 795371088 },
            { 2077983581, -1367281941 },
            { 378365037, 352499047 },
            { -529054135, -1199897169 },
            { -529054134, -1199897172 },
            { 486166145, -1023374709 },
            { 1628490888, 23352662 },
            { 1498516223, 1205607945 },
            { -632459882, -1647846966 },
            { -626812403, -845557339 },
            { 385802761, -1370759135 },
            { 2117976603, 121049755 },
            { 1338515426, -996185386 },
            { -1455694274, 98508942 },
            { 1579245182, 2070189026 },
            { -587434450, 1521286012 },
            { -163742043, 1542290441 },
            { -1224714193, -1832422579 },
            { 644359987, 826309791 },
            { -1962514734, -143132326 },
            { -705305612, 1153652756 },
            { -357728804, -1819233322 },
            { -698499648, -1138208076 },
            { 1213686767, -1850571427 },
            { 386382445, -855748505 },
            { 1859976884, 553887414 },
            { 960793436, 996293980 },
            { 1001265731, 2048317869 },
            { 1253290621, -1754948969 },
            { 470729623, -1293296287 },
            { 1051155022, -369760990 },
            { 865679437, -1878764039 },
            { 927253046, -1039528932 },
            { 109552593, 1796682209 },
            { -2092529553, 1230323789 },
            { 691633666, -363689972 },
            { -2055888649, 1629293099 },
            { 621575320, -41440462 },
            { -2118132208, 1602646136 },
            { -1127699509, 1540934679 },
            { -685265909, -92759291 },
            { 552706886, -1100422738 },
            { 1835797460, -1021495308 },
            { -892259869, 642482233 },
            { -1623330855, -465682601 },
            { -1616524891, 1668858301 },
            { 789892804, 171931394 },
            { -1289478934, -1583967946 },
            { -892070738, -2099697608 },
            { -891243783, -1581843485 },
            { 889398893, -1157596551 },
            { -1625468793, 1397052267 },
            { 1293049486, 1975934948 },
            { 1369769822, 559147458 },
            { 586484018, 1079279582 },
            { 110115790, 593465182 },
            { 1490499512, 1523195708 },
            { 3552619, 2019042823 },
            { 1471284746, 73681876 },
            { 456448245, -1758372725 },
            { 110547964, 795236088 },
            { 1588977225, -1667224349 },
            { 918540912, -209869746 },
            { -471874147, 1686524871 },
            { 205978836, 1723747470 },
            { -1044400758, -129230242 },
            { -2073307447, -1331212963 },
            { 435230680, 2106561762 },
            { -864578046, 223891266 },
            { 1660607208, 935692442 },
            { 260214178, -1478445584 },
            { -1847536522, 198438816 },
            { -496055048, -967648160 },
            { -1792066367, 99588025 },
            { 562888306, -956706906 },
            { -427925529, -1429456799 },
            { 995306285, 1451568081 },
            { -378017204, -1117626326 },
            { 447918618, -148794216 },
            { 313836902, 1516985844 },
            { 1175970190, -796583652 },
            { 525244071, -148229307 },
            { -1021702157, -819720157 },
            { -402507101, 671706427 },
            { -1556671423, -1183726687 },
            { 61936445, -1614955425 },
            { 112903447, -1779180711 },
            { 1817873886, -1100168350 },
            { 1824679850, -132247350 },
            { -1628526499, -1863559151 },
            { 547302405, -119235651 },
            { 1840561315, 2114754781 },
            { -460592212, -1379835144 },
            { 3655341, -151838493 },
            { 1554697726, 418081930 },
            { -1883959124, 832133926 },
            { -481416622, 1524187186 },
            { -481416621, -41896755 },
            { -481416620, -1607980696 },
            { -1151126752, 1058261682 },
            { -1926458555, 794443127 }
        };
		
        //Languages phrases

        private readonly Dictionary<string, Dictionary<string, string>> messages = new Dictionary<string, Dictionary<string, string>>
        {
            {"FILE_NOT_EXISTS", new Dictionary<string, string>() {
                {"en", "File does not exist"},
                {"ru", "Файл не существует"},
            }},
            {"FILE_BROKEN", new Dictionary<string, string>() {
                {"en", "File is broken, can not be paste"},
                {"ru", "Файл поврежден, вставка невозможна"},
            }},
            {"NO_ACCESS", new Dictionary<string, string>() {
                {"en", "You don't have the permissions to use this command"},
                {"ru", "У вас нет прав доступа к данной команде"},
            }},
            {"SYNTAX_PASTEBACK", new Dictionary<string, string>() {
                {"en", "Syntax: /pasteback <Target Filename> <options values>\nheight XX - Adjust the height\nvending - Information and sellings in vending machine"},
                {"ru", "Синтаксис: /pasteback <Название Объекта> <опция значение>\nheight XX - Высота от земли\nvending - Информация и товары в торговом автомате"},
            }},
            {"SYNTAX_PASTE_OR_PASTEBACK", new Dictionary<string, string>() {
                {"en", "Syntax: /paste or /pasteback <Target Filename> <options values>\nheight XX - Adjust the height\nautoheight true/false - sets best height, carefull of the steep\nblockcollision XX - blocks the entire paste if something the new building collides with something\ndeployables true/false - false to remove deployables\ninventories true/false - false to ignore inventories\nvending - Information and sellings in vending machine"},
                {"ru", "Синтаксис: /paste or /pasteback <Название Объекта> <опция значение>\nheight XX - Высота от земли\nautoheight true/false - автоматически подобрать высоту от земли\nblockcollision XX - блокировать вставку, если что-то этому мешает\ndeployables true/false - false для удаления предметов\ninventories true/false - false для игнорирования копирования инвентаря\nvending - Информация и товары в торговом автомате"},
            }},
            {"PASTEBACK_SUCCESS", new Dictionary<string, string>() {
                {"en", "You've successfully placed back the structure"},
                {"ru", "Постройка успешно вставлена на старое место"},
            }},
            {"PASTE_SUCCESS", new Dictionary<string, string>() {
                {"en", "You've successfully pasted the structure"},
                {"ru", "Постройка успешно вставлена"},
            }},
            {"SYNTAX_COPY", new Dictionary<string, string>() {
                {"en", "Syntax: /copy <Target Filename> <options values>\n radius XX (default 3)\n method proximity/building (default proximity)\nbuilding true/false (saves structures or not)\ndeployables true/false (saves deployables or not)\ninventories true/false (saves inventories or not)"},
                {"ru", "Синтаксис: /copy <Название Объекта> <опция значение>\n radius XX (default 3)\n method proximity/building (по умолчанию proximity)\nbuilding true/false (сохранять постройку или нет)\ndeployables true/false (сохранять предметы или нет)\ninventories true/false (сохранять инвентарь или нет)"},
            }},
            {"NO_ENTITY_RAY", new Dictionary<string, string>() {
                {"en", "Couldn't ray something valid in front of you"},
                {"ru", "Не удалось найти какой-либо объект перед вами"},
            }},
            {"COPY_SUCCESS", new Dictionary<string, string>() {
                {"en", "The structure was successfully copied as {0}"},
                {"ru", "Постройка успешно скопирована под названием: {0}"},
            }},
            {"NO_PASTED_STRUCTURE", new Dictionary<string, string>() {
                {"en", "You must paste structure before undoing it"},
                {"ru", "Вы должны вставить постройку перед тем, как отменить действие"},
            }},
            {"UNDO_SUCCESS", new Dictionary<string, string>() {
                {"en", "You've successfully undid what you pasted"},
                {"ru", "Вы успешно снесли вставленную постройку"},
            }},
            {"NOT_FOUND_PLAYER", new Dictionary<string, string>() {
                {"en", "Couldn't find the player"},
                {"ru", "Не удалось найти игрока"},
            }},
            {"SYNTAX_BOOL", new Dictionary<string, string>() {
                {"en", "Option {0} must be true/false"},
                {"ru", "Опция {0} принимает значения true/false"},
            }},
            {"SYNTAX_HEIGHT", new Dictionary<string, string>() {
                {"en", "Option height must be a number"},
                {"ru", "Опция height принимает только числовые значения"},
            }},
            {"SYNTAX_BLOCKCOLLISION", new Dictionary<string, string>() {
                {"en", "Option blockcollision must be a number, 0 will deactivate the option"},
                {"ru", "Опция blockcollision принимает только числовые значения, 0 позволяет отключить проверку"},
            }},
            {"SYNTAX_RADIUS", new Dictionary<string, string>() {
                {"en", "Option radius must be a number"},
                {"ru", "Опция radius принимает только числовые значения"},
            }},
            {"BLOCKING_PASTE", new Dictionary<string, string>() {
                {"en", "Something is blocking the paste"},
                {"ru", "Что-то препятствует вставке"},
            }},
            {"AVAILABLE_STRUCTURES", new Dictionary<string, string>() {
                {"en", "<color=orange>Доступные постройки:</color>"},
                {"ru", "<color=orange>Available structures:</color>"},
            }},
        };

        public class CopyData
        {
            public BasePlayer Player;
            public Stack<Vector3> CheckFrom = new Stack<Vector3>();
            public HashSet<BaseEntity> HouseList = new HashSet<BaseEntity>();
            public List<object> RawData = new List<object>();
            public Vector3 SourcePos;
            public Vector3 SourceRot;
            public Action callback;

            public string FileName;
            public int CurrentLayer;
            public float RotCor;
            public float Range;
            public bool SaveTree;
            public bool SaveShare;
            public CopyMechanics CopyMechanics;
            public bool EachToEach;
            public uint BuildingID = 0;

#if DEBUG
            public Stopwatch Sw = new Stopwatch();
#endif
        }

        public class PasteData
        {
            public ICollection<Dictionary<string, object>> Entities;
            public List<BaseEntity> PastedEntities = new List<BaseEntity>();
            public Dictionary<uint, Dictionary<string, object>> IoEntities =
                new Dictionary<uint, Dictionary<string, object>>();
            public BasePlayer Player;
            public List<StabilityEntity> StabilityEntities = new List<StabilityEntity>();
            public Quaternion QuaternionRotation;
            public Action callback;

            public bool Auth;
            public Vector3 StartPos;
            public float HeightAdj;
            public bool Stability;
            public bool IsItemReplace;

            public uint BuildingID = 0;

#if DEBUG
            public Stopwatch Sw = new Stopwatch();
#endif
        }
    }
}
