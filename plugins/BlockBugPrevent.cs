using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("BlockBugPrevent", "sami37", "1.1.1", ResourceId = 2166)]
    [Description("Prevent foundation block build on another foundation.")]
    public class BlockBugPrevent : RustPlugin
    {
        void Loaded()
        {
			lang.RegisterMessages(new Dictionary<string,string>{
				["NotAllowed"] = "<color='#DD0000'>Your are not allowed to build foundation here.</color>"
			}, this);
        }

        private object RaycastAll<T>(Ray ray) where T : BaseEntity
        {
            var hits = Physics.RaycastAll(ray);
            GamePhysics.Sort(hits);
            var distance = 100f;
            object target = false;
            foreach (var hit in hits)
            {
                var ent = hit.GetEntity();
                if (ent is T && hit.distance < distance)
                {
                    target = ent;
                    break;
                }
            }

            return target;
        }

        void OnEntityBuilt(Planner planner, GameObject gameObject)
        {
            bool destroy = false;
            var player = planner.GetOwnerPlayer();
            if (player == null) return;
            BuildingBlock block = gameObject.GetComponent<BuildingBlock>();
            if (block == null) return;
            if (!block.PrefabName.Contains("foundation"))
                return;
            Vector3 sourcepos = block.transform.position;
            var entities = RaycastAll<BuildingBlock>(new Ray(sourcepos + new Vector3(0f, 0f, 0f), Vector3.up));
            if (entities.ToString() != "False")
            {
                BuildingBlock entitBlock = (BuildingBlock) entities;
                if (entitBlock.name.Contains("foundation") && !entitBlock.name.Contains("steps"))
                {
                    destroy = true;
                }
            }
            if (destroy)
            {
                block.Kill();
                SendReply(player, lang.GetMessage("NotAllowed", this));
            }
        }
    }
}