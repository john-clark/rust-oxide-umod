using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;

namespace Oxide.Plugins
{
    [Info("BlockBoxPlacement", "wazzzup", "0.0.3", ResourceId = 2312)]
    [Description("Blocks box and oven placement under foundations")]
    public class BlockBoxPlacement : RustPlugin
    {

        private object CanChangeGrade(BasePlayer player, BuildingBlock block, BuildingGrade.Enum iGrade)
        {
            if (DeployVolume.Check(block.transform.position, block.transform.rotation, PrefabAttribute.server.FindAll<DeployVolume>(block.prefabID), ~(1 << block.gameObject.layer)))
            {
                return false;
            }
            return null;
        }
    }
}