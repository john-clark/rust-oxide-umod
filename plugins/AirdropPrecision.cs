using UnityEngine;
using System.Reflection;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Airdrop Precision", "k1lly0u", "0.1.3", ResourceId = 2074)]
    [Description("Makes supply drops fall on the supply signal's position")]
    class AirdropPrecision : RustPlugin
    {
        #region Fields
        List<Vector3> thrownSignals;
        #endregion

        #region Oxide Hooks
        void OnServerInitialized()
        {
            thrownSignals = new List<Vector3>();
        }

        void OnExplosiveThrown(BasePlayer player, BaseEntity entity)
        {
            if (entity is SupplySignal)
            {
                timer.Once(3f, () =>
                {
                    if (entity.transform.position != null)
                    {
                        thrownSignals.Add(entity.transform.position);
                    }
                });
            }
        }

        void OnEntitySpawned(BaseEntity entity)
        {
            if (entity is CargoPlane)
            {
                var plane = entity.GetComponent<CargoPlane>();
                var location = plane.dropPosition;
                if (location != null)
                {
                    for (int i = 0; i < thrownSignals.Count; i++)
                    {
                        if (Vector2.Distance(new Vector2(thrownSignals[i].x, thrownSignals[i].z), new Vector2(location.x, location.z)) < 30)
                        {
                            plane.UpdateDropPosition(thrownSignals[i]);
                            thrownSignals.Remove(thrownSignals[i]);
                            break;
                        }
                    }
                }
            }
        }
        #endregion
    }
}