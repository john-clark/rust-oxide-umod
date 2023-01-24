using System.Linq;

namespace Oxide.Plugins
{
    [Info("AntiBarricadeStacking", "k1lly0u", "0.1.0", ResourceId = 0)]
    class AntiBarricadeStacking : RustPlugin
    {
        bool initialized;       
        void OnServerInitialized() => initialized = true;      
        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (!initialized) return;
            if (entity is Barricade)
            {
                var barricades = BaseEntity.serverEntities.Where(x => x is Barricade).Where(x => x.transform.position == entity.transform.position).ToArray();
                if (barricades.Length > 1)
                {
                    for (int i = 0; i < barricades.Length - 1; i++)
                    {
                        barricades[i].GetComponent<BaseCombatEntity>().DieInstantly();
                    }
                }
            }
        }       
    }
}