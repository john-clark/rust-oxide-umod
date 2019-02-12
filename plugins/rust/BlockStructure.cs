using System.Collections.Generic;
using UnityEngine;
using System;

namespace Oxide.Plugins
{
    [Info("BlockStructure", "Marat", "1.0.4", ResourceId = 2092)]
	[Description("Building blocks in the rocks, terrain and icebergs. Sets a limit build in height and depth in water.")]
	
    class BlockStructure : RustPlugin
    {
		void Loaded()
        {
			LoadConfiguration();
            LoadDefaultMessages();
			permission.RegisterPermission(permBS, this);
        }
		
		double HeightBlock = 20;
		double WaterBlock = -5;
		int AuthLvl = 3;
		bool ConfigChanged;
		bool usePermissions = true;
        bool BlockInHeight = true;
		bool BlockInWater = true;
		bool BlockInRock = true;
		bool BlockOnIceberg = false;
		bool BlockUnTerrain = true;
        string permBS = "blockstructure.allowed";
		
		protected override void LoadDefaultConfig() => PrintWarning("New configuration file created.");

        void LoadConfiguration()
        {
			HeightBlock = GetConfigValue("Options", "Height for block", HeightBlock);
			WaterBlock = GetConfigValue("Options", "Depth for block", WaterBlock);
			BlockInHeight = GetConfigValue("Options", "Block in Height", BlockInHeight);
			BlockInWater = GetConfigValue("Options", "Block in Water", BlockInWater);
			BlockInRock = GetConfigValue("Options", "Block In Rock", BlockInRock);
			BlockOnIceberg = GetConfigValue("Options", "Block On Iceberg", BlockOnIceberg);
			BlockUnTerrain = GetConfigValue("Options", "Block Under Terrain", BlockUnTerrain);
			usePermissions = GetConfigValue("Options", "UsePermissions", usePermissions);
			AuthLvl = GetConfigValue("Options", "Ignore Authorization Level", AuthLvl);
			
			if (!ConfigChanged) return;
            PrintWarning("Configuration file updated.");
            SaveConfig();
		}
		T GetConfigValue<T>(string category, string setting, T defaultValue)
        {
            var data = Config[category] as Dictionary<string, object>;
            object value;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[category] = data;
                ConfigChanged = true;
            }
            if (data.TryGetValue(setting, out value)) return (T)Convert.ChangeType(value, typeof(T));
            value = defaultValue;
            data[setting] = value;
            ConfigChanged = true;
            return (T)Convert.ChangeType(value, typeof(T));
        }
		void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
				["blockWater"] = "<size=16><color=yellow>You can not build in water deeper {0} meters</color></size>",
                ["blockHeight"] = "<size=16><color=yellow>You can not build higher {0} meters</color></size>",
				["block"] = "<size=16><color=red>You can not build here</color></size>",
				["blockIce"] = "<size=16><color=red>You can not build on iceberg</color></size>",
				["blockTerrain"] = "<size=16><color=#ffff00>You can not build under the terrain</color></size>",
            }, this, "en");
			
			lang.RegisterMessages(new Dictionary<string, string>
            {
				["blockWater"] = "<size=16><color=#ffff00>Вы не можете строить в воде глубже {0} метров</color></size>",
                ["blockHeight"] = "<size=16><color=#ffff00>Вы не можете строить выше {0} метров</color></size>",
				["block"] = "<size=16><color=#ff0000>Вы не можете строить здесь</color></size>",
				["blockIce"] = "<size=16><color=#ffff00>Вы не можете строить на айсберге</color></size>",
				["blockTerrain"] = "<size=16><color=#ffff00>Вы не можете строить под рельефом</color></size>"
            }, this, "ru");
        }
        void Block(BaseNetworkable block, BasePlayer player, bool Height, bool Water)
        {
            if (usePermissions && !IsAllowed(player.UserIDString, permBS) && player.net.connection.authLevel < AuthLvl && block && !block.IsDestroyed)
            {
                Vector3 Pos = block.transform.position;
                if (Height || Water)
                {
                    float height = TerrainMeta.HeightMap.GetHeight(Pos);
                    if (Height && Pos.y - height > HeightBlock)
                    {
                        Reply(player, Lang("blockHeight", player.UserIDString, HeightBlock));
                        block.Kill(BaseNetworkable.DestroyMode.Gib);
                        return;
                    }
                    else if (Water && height < 0 && height < WaterBlock && Pos.y < 2.8f )
                    {
                        Reply(player, Lang("blockWater", player.UserIDString, WaterBlock));
                        block.Kill(BaseNetworkable.DestroyMode.Gib);
                        return;
                    }
                }
				if (BlockInRock)
				{
				    Pos.y += 200;
                    RaycastHit[] hits = Physics.RaycastAll(Pos, Vector3.down, 199.0f);
                    Pos.y -= 200;
                    for (int i = 0; i < hits.Length; i++)
                    {
                        RaycastHit hit = hits[i];
                        if (hit.collider)
                        {
                            string ColName = hit.collider.name;
                            if ((ColName.StartsWith("rock", StringComparison.CurrentCultureIgnoreCase) || ColName.StartsWith("cliff", StringComparison.CurrentCultureIgnoreCase)) && (hit.point.y < Pos.y ? BlockInRock : hit.collider.bounds.Contains(Pos)))
                            {
							    var buildingBlock = block.GetComponent<BuildingBlock>();
                                if (buildingBlock != null)
                                {
                                    foreach (ItemAmount item in buildingBlock.blockDefinition.grades[(int)buildingBlock.grade].costToBuild)
                                    {
                                        player.inventory.GiveItem(ItemManager.CreateByItemID(item.itemid, (int)item.amount));
                                    }
                                }
								Reply(player, Lang("block", player.UserIDString));
                                block.Kill(BaseNetworkable.DestroyMode.Gib);
                                break;
						    }
                        }							
                    }
                }
				if (BlockOnIceberg)
				{
				    Pos.y += 200;
                    RaycastHit[] hits = Physics.RaycastAll(Pos, Vector3.down, 202.8f);
                    Pos.y -= 200;
                    for (int i = 0; i < hits.Length; i++)
                    {
                        RaycastHit hit = hits[i];
                        if (hit.collider)
                        {
                            string ColName = hit.collider.name;
                            if (BlockOnIceberg && ColName == "iceberg_COL")
                            {
								var buildingBlock = block.GetComponent<BuildingBlock>();
                                if (buildingBlock != null)
                                {
                                    foreach (ItemAmount item in buildingBlock.blockDefinition.grades[(int)buildingBlock.grade].costToBuild)
                                    {
                                        player.inventory.GiveItem(ItemManager.CreateByItemID(item.itemid, (int)item.amount));
                                    }
                                }
                                Reply(player, Lang("blockIce", player.UserIDString));
                                block.Kill(BaseNetworkable.DestroyMode.Gib);
                                break;
                            }
                        }							
                    }
					
                }
				if (BlockUnTerrain)
				{
				    Pos.y += 200;
                    RaycastHit[] hits = Physics.RaycastAll(Pos, Vector3.down, 199.0f);
                    Pos.y -= 200;
					bool isMining = block is MiningQuarry;
                    for (int i = 0; i < hits.Length; i++)
                    {
                        RaycastHit hit = hits[i];
                        if (hit.collider)
                        {
                            string ColName = hit.collider.name;
                            if (BlockUnTerrain && !isMining && ColName == "Terrain" && hit.point.y > Pos.y)
                            {
								var buildingBlock = block.GetComponent<BuildingBlock>();
                                if (buildingBlock != null)
                                {
                                    foreach (ItemAmount item in buildingBlock.blockDefinition.grades[(int)buildingBlock.grade].costToBuild)
                                    {
                                        player.inventory.GiveItem(ItemManager.CreateByItemID(item.itemid, (int)item.amount));
                                    }
                                }
                                Reply(player, Lang("blockTerrain", player.UserIDString));
                                block.Kill(BaseNetworkable.DestroyMode.Gib);
                                break;
                            }
                        }							
                    }
				}
            }
        }
		string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        void Reply(BasePlayer player, string message, string args = null) => PrintToChat(player, $"{message}", args);
        void OnEntityBuilt(Planner plan, GameObject obj) => Block(obj.GetComponent<BaseNetworkable>(), plan.GetOwnerPlayer(), BlockInHeight, BlockInWater);
		bool IsAllowed(string id, string perm) => permission.UserHasPermission(id, perm);
    }
}