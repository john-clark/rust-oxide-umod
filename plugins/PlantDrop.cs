using UnityEngine;
// ReSharper disable SuggestBaseTypeForParameter

namespace Oxide.Plugins
{
    [Info("Plant Drop", "Iv Misticos", "1.1.0")]
    [Description("Allows planting crops anywhere by dropping the seed")]
    public class PlantDrop : RustPlugin
    {
        private const string PrefabCorn = "assets/prefabs/plants/corn/corn.entity.prefab";
        private const string PrefabHemp = "assets/prefabs/plants/hemp/hemp.entity.prefab";
        private const string PrefabPumpkin = "assets/prefabs/plants/pumpkin/pumpkin.entity.prefab";
        
        private void OnItemDropped(Item item, BaseEntity entity)
        {
            var shortname = item.info.shortname;
            var pos = entity.transform.position;

            // ReSharper disable once SwitchStatementMissingSomeCases
            switch (shortname)
            {
                case "seed.corn":
                    CreatePlant(entity, PrefabCorn, pos, item.amount);
                    break;

                case "seed.hemp":
                    CreatePlant(entity, PrefabHemp, pos, item.amount);
                    break;

                case "seed.pumpkin":
                    CreatePlant(entity, PrefabPumpkin, pos, item.amount);
                    break;
            }
        }

        private void CreatePlant(BaseEntity seed, string prefab, Vector3 pos, int amount)
        {
            RaycastHit hit;
            Physics.Raycast(pos, Vector3.down, out hit);
            if (hit.GetEntity() != null)
                return;

            pos.y = TerrainMeta.HeightMap.GetHeight(seed.transform.position);
            seed.Kill();

            for (var i = 0; i < amount; i++)
            {
                var plant = GameManager.server.CreateEntity(prefab, pos, Quaternion.identity) as PlantEntity;
                if (plant == null) continue;

                plant.Spawn();
            }
        }
    }
}
