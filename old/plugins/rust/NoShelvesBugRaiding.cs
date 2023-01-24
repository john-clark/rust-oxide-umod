using System.Collections.Generic;
using UnityEngine;
namespace Oxide.Plugins
{
    [Info("NoShelvesBugRaiding", "wazzzup", "1.1.0")]
    [Description("No bug raiding with shelfs")]
    public class NoShelvesBugRaiding : RustPlugin
    {
        bool OnShelves(BaseEntity entity)
        {
            GroundWatch component = entity.gameObject.GetComponent<GroundWatch>();
            List<Collider> list = Facepunch.Pool.GetList<Collider>();
            Vis.Colliders<Collider>(entity.transform.TransformPoint(component.groundPosition), component.radius, list, component.layers, QueryTriggerInteraction.Collide);
            foreach (Collider collider in list)
            {
                if (!((Object)collider.transform.root == (Object)entity.gameObject.transform.root))
                {
                    BaseEntity baseEntity = collider.gameObject.ToBaseEntity();
                    if ((!(bool)((Object)baseEntity) || !baseEntity.IsDestroyed && !baseEntity.isClient) && baseEntity.ShortPrefabName == "shelves")
                    {
                        Facepunch.Pool.FreeList<Collider>(ref list);
                        return true;
                    }
                }
            }
            Facepunch.Pool.FreeList<Collider>(ref list);
            return false;
        }


        void OnEntityBuilt(Planner plan, GameObject obj)
        {
            var entity = obj.GetComponent<BaseEntity>();
            if (entity != null && entity.ShortPrefabName == "shelves" && OnShelves(entity))
            {
                entity.Kill();
                BasePlayer player = plan.GetOwnerPlayer();
                SendReply(player, "Failed Check: Sphere Test (shelves/sockets/free/inside 1)");
                var newItem = ItemManager.CreateByItemID(2057749608, (int)1);
                if (newItem != null)
                {
                    player.inventory.GiveItem(newItem);
                }
            }
        }
    }
}