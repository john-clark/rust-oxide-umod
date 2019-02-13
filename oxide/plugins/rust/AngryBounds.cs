using System;
using System.Text;
using System.Collections.Generic;

using UnityEngine;

namespace Oxide.Plugins
{
    [Info("AngryBounds", "Tori1157", "1.2.0")]
    [Description("Prevents players from building outside of map bounds.")]

    public class AngryBounds : RustPlugin
    {
        #region Fields

        private bool Changed;

        private decimal boundChange;

        #endregion

        #region Loading

        private void Init() => LoadVariables();

        private void LoadVariables()
        {
            boundChange = Convert.ToDecimal(GetConfig("Options", "Boundary Adjust Size", 0));

            if (Changed)
            {
                SaveConfig();
                Changed = false;
            }
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new config file");
            Config.Clear();
            LoadVariables();
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Out Of Bounds"] = "<color=red>You're out of bounds, can't build here!</color>",
            }, this);
        }

        #endregion

        private void OnEntityBuilt(Planner plan, GameObject go)
        {
            var player = plan.GetOwnerPlayer();
            var entity = go.ToBaseEntity();

            if (player == null || entity == null)
                return;

            var block = entity.GetComponent<BuildingBlock>();

            if (player != null && CheckPlayerPosition(player))
            {

                NextTick(() =>
                {
                    entity.Kill();
                    player.ChatMessage(lang.GetMessage("Out Of Bounds", this, player.UserIDString));
                });

                if (block != null)
                {
                    foreach (var refundItem in block.BuildCost()) // Credit to Ryan for this code.
                        player.GiveItem(ItemManager.CreateByItemID(refundItem.itemDef.itemid, (int)refundItem.amount));
                }
                else
                {
                    string item = ClearName(entity.ShortPrefabName);

                    if (item == null)
                        return;

                    int itemId = (int)entity.prefabID;

                    ItemDefinition definition = ItemManager.itemList.Find((ItemDefinition x) => x.shortname == item);

                    if (definition == null)
                    {
                        ItemDefinition IdDefinition = ItemManager.FindItemDefinition(itemId);

                        if (IdDefinition != null)
                        {
                            player.GiveItem(ItemManager.CreateByItemID(itemId, 1));
                            return;
                        }

                        return;
                    }

                    player.GiveItem(ItemManager.CreateByPartialName(item, 1));
                }
            }
        }

        private bool CheckPlayerPosition(BasePlayer player)
        {
            var worldHalf = World.Size / 2;
            var worldAdded = (Math.Sign(boundChange) == -1 ? worldHalf - Decimal.Negate(boundChange) : worldHalf + boundChange);

            var playerX = Convert.ToDecimal(player.transform.position.x);
            var playerZ = Convert.ToDecimal(player.transform.position.z);

            var positionX = (Math.Sign(playerX) == -1 ? Decimal.Negate(playerX) : playerX);
            var positionZ = (Math.Sign(playerZ) == -1 ? Decimal.Negate(playerZ) : playerZ);

            if (Math.Sign(boundChange) == 0)
                return positionX > worldHalf || positionZ > worldHalf;
            else
                return positionX > worldAdded || positionZ > worldAdded;
        }

        #region Helpers

        string ClearName(string item)
        {
            var replace = new[] { ".deployed", "_deployed", "_leather", "_small", "_large" };
            var output = new StringBuilder(item);

            foreach (var r in replace)
                output.Replace(r, string.Empty);

            return output.ToString();
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

        #endregion
    }
}