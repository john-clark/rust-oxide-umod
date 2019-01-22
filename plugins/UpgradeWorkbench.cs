using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Upgrade Workbench", "mvrb", "0.1.4")]
    [Description("Allows players to upgrade workbenches using a command")]
    class UpgradeWorkbench : RustPlugin
    {
        private const string permissionUse = "upgradeworkbench.use";
        private const string permissionNoCost = "upgradeworkbench.nocost";

        private Dictionary<string, Dictionary<int, int>> WorkbenchIngredients = new Dictionary<string, Dictionary<int, int>>();

        private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoWorkbenchFound"] = "No Workbench found. Make sure you are looking at a Workbench.",
                ["UpgradeComplete"] = "You have successfully upgraded your workbench.",
                ["Error: Level3"] = "You can't upgrade a Tier 3 Workbench.",
                ["Error: NotEnoughResources"] = "You do not have the required resources to upgrade this Workbech: \n",
                ["Error: NotEmpty"] = "You can only upgrade empty workbenches! Please empty the workbench.",
                ["Error: NoPermission"] = "You do not have permission to use this command."
            }, this);
        }

        private void OnServerInitialized()
        {
            permission.RegisterPermission(permissionUse, this);
            permission.RegisterPermission(permissionNoCost, this);

            foreach (ItemDefinition itemDef in ItemManager.GetItemDefinitions())
            {
                if (itemDef.name.Contains("workbench"))
                {
                    foreach (var ingredient in itemDef.Blueprint.ingredients)
                    {
                        string formattedItemDefName = itemDef.name.Replace(".item", string.Empty);

                        if (!WorkbenchIngredients.ContainsKey(formattedItemDefName))
                        {
                            WorkbenchIngredients.Add(formattedItemDefName, new Dictionary<int, int>() { { ingredient.itemid, (int)ingredient.amount } });
                        }
                        else
                        {
                            WorkbenchIngredients[formattedItemDefName].Add(ingredient.itemid, (int)ingredient.amount);
                        }
                    }
                }
            }
        }

        [ChatCommand("upgradewb")]
        private void cmdUpgradeWorkbench(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, permissionUse))
            {
                player.ChatMessage(Lang("Error: NoPermission", player.UserIDString));
                return;
            }

            RaycastHit hit;
            var raycast = Physics.Raycast(player.eyes.HeadRay(), out hit, 5f, 2097409);
            BaseEntity entity = raycast ? hit.GetEntity() : null;

            if (entity == null || entity && !entity.ShortPrefabName.StartsWith("workbench"))
            {
                player.ChatMessage(Lang("NoWorkbenchFound", player.UserIDString));
                return;
            }

            if (entity.ShortPrefabName.StartsWith("workbench3"))
            {
                player.ChatMessage(Lang("Error: Level3", player.UserIDString));
                return;
            }

            if ((entity as StorageContainer)?.inventory.itemList.Count > 0)
            {
                player.ChatMessage(Lang("Error: NotEmpty", player.UserIDString));
                return;
            }

            TryUpgradeWorkbench(player, entity);
        }

        private bool ReplaceWorkbench(BaseEntity originalWorkbench)
        {
            if (originalWorkbench == null)
            {
                PrintWarning("Failed to get original Workbench");
                return false;
            }

            string workbenchName = originalWorkbench.ShortPrefabName.StartsWith("workbench1") ? "assets/prefabs/deployable/tier 2 workbench/workbench2.deployed.prefab" : "assets/prefabs/deployable/tier 3 workbench/workbench3.deployed.prefab";

            var workbench = GameManager.server.CreateEntity(workbenchName, originalWorkbench.transform.position, originalWorkbench.transform.rotation, true);

            if (workbench == null)
            {
                PrintWarning("Failed to spawn a new Workbench");
                return false;
            }

            workbench.OwnerID = originalWorkbench.OwnerID;
            workbench.Spawn();

            originalWorkbench.Kill();

            return true;
        }

        private class MissingItem
        {
            public int CurrentAmount;
            public int RequiredAmount;

            public MissingItem(int current, int required)
            {
                CurrentAmount = current;
                RequiredAmount = required;
            }
        }

        private void TryUpgradeWorkbench(BasePlayer player, BaseEntity entity)
        {
            if (!permission.UserHasPermission(player.UserIDString, permissionNoCost))
            {
				string wbName = string.Empty;
				
				if (entity.ShortPrefabName.Contains("1"))
				{
					wbName = "workbench2";
				}
				else
				{
					wbName = "workbench3";
				}
				
                Dictionary<int, MissingItem> MissingItems = new Dictionary<int, MissingItem>();

                foreach (KeyValuePair<int, int> entry in WorkbenchIngredients[wbName])
                {
                    int ingredientItemId = entry.Key;
                    int playerItemAmount = player.inventory.GetAmount(ingredientItemId);
                    int ingredientItemAmount = entry.Value;

                    if (playerItemAmount < ingredientItemAmount)
                    {
                        if (!MissingItems.ContainsKey(ingredientItemId))
                        {
                            MissingItems.Add(ingredientItemId, new MissingItem(playerItemAmount, ingredientItemAmount));
                        }
                    }
                }

                if (MissingItems.Count > 0)
                {
                    string msg = Lang("Error: NotEnoughResources", player.UserIDString);

                    foreach (KeyValuePair<int, MissingItem> entry in MissingItems)
                    {
                        Item item = ItemManager.CreateByItemID(entry.Key);

                        if (item == null)
                        {
                            PrintWarning($"Skipping invalid item: {entry.Key}");
                            continue;
                        }

                        string itemName = item.info.displayName.english;

                        msg += $"- {entry.Value.CurrentAmount}/{entry.Value.RequiredAmount} {itemName} \n";
                    }

                    player.ChatMessage(msg);

                    return;
                }

                if (ReplaceWorkbench(entity))
                {
                    foreach (KeyValuePair<int, int> entry in WorkbenchIngredients[wbName])
                    {
                        Item item = ItemManager.CreateByItemID(entry.Key, entry.Value);

                        if (item == null)
                        {
                            PrintWarning($"Skipping invalid item: {entry.Key}");
                            continue;
                        }

                        player.inventory.Take(new List<Item>(), item.info.itemid, item.amount);
                        player.Command(string.Concat("note.inv ", item.info.itemid, " ", -item.amount));
                    }
                }
            }
			else
			{
				ReplaceWorkbench(entity);
			}

            player.ChatMessage(Lang("UpgradeComplete", player.UserIDString));
        }

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
    }
}