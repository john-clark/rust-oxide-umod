//#define DEBUG
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Healthy Guns", "Wulf/lukespragg", "2.0.0", ResourceId = 2262)]
    [Description("Makes weapons in barrels/crates spawn in good condition")]
    public class HealthyGuns : CovalencePlugin
    {
        private void OnServerInitialized()
        {
            Puts("Overriding default weapon conditions");

            timer.Repeat(60f, 0, () =>
            {
                var count = 0;
                foreach (var container in UnityEngine.Object.FindObjectsOfType<LootContainer>())
                {
                    if (container != null) RepairContainerContents(container);
                    count++;
                }
#if DEBUG
                Puts($"Refreshed {count} loot containers");
#endif  
            });
        }

        private void RepairContainerContents(BaseNetworkable entity)
        {
            var container = entity as LootContainer;
            if (container == null) return;

            foreach (var item in container.inventory.itemList.ToList())
            {
                var definition = ItemManager.FindItemDefinition(item.info.itemid);
                if (item.hasCondition && definition.category == ItemCategory.Weapon && !item.condition.Equals(item.info.condition.max))
                {
                    item.condition = item.info.condition.max;
#if DEBUG
                    Puts($"{item} condition set to {item.condition}");
#endif
                }
            }
        }

        private void Unload() => Puts("Restored default weapon conditions");
    }
}