using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Facepunch;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("PuzzleChainRepair", "bazuka5801/Misstake", "1.0.2")]
    public class PuzzleChainRepair : RustPlugin
    {
        public void PuzzlePrefabRespawn(ConsoleSystem.Arg arg)
        {

            foreach (BaseNetworkable baseNetworkable in BaseNetworkable.serverEntities.Where<BaseNetworkable>((Func<BaseNetworkable, bool>)(x => x is IOEntity && ((BaseEntity)x).OwnerID == 0)).ToList<BaseNetworkable>())
                baseNetworkable.Kill(BaseNetworkable.DestroyMode.None);
            foreach (MonumentInfo monument in TerrainMeta.Path.Monuments)
            {
                GameObject prefab = GameManager.server.FindPrefab(monument.gameObject.name);
                if (!((UnityEngine.Object)prefab == (UnityEngine.Object)null))
                {
                    Dictionary<IOEntity, IOEntity> dictionary = new Dictionary<IOEntity, IOEntity>();
                    foreach (IOEntity componentsInChild in prefab.GetComponentsInChildren<IOEntity>(true))
                    {
                        Quaternion rot = monument.transform.rotation * componentsInChild.transform.rotation;
                        Vector3 pos = monument.transform.TransformPoint(componentsInChild.transform.position);
                        BaseEntity newEntity = GameManager.server.CreateEntity(componentsInChild.PrefabName, pos, rot, true);
                        IOEntity ioEntity = newEntity as IOEntity;
                        if ((UnityEngine.Object)ioEntity != (UnityEngine.Object)null)
                        {
                            dictionary.Add(componentsInChild, ioEntity);
                            DoorManipulator doorManipulator = newEntity as DoorManipulator;
                            if ((UnityEngine.Object)doorManipulator != (UnityEngine.Object)null)
                            {
                                List<Door> list = Facepunch.Pool.GetList<Door>();
                                global::Vis.Entities<Door>(newEntity.transform.position, 10f, list, -1, QueryTriggerInteraction.Collide);
                                Door door = list.OrderBy<Door, float>((Func<Door, float>)(x => x.Distance(newEntity.transform.position))).FirstOrDefault<Door>();
                                if ((UnityEngine.Object)door != (UnityEngine.Object)null)
                                    doorManipulator.targetDoor = door;
                                Facepunch.Pool.FreeList<Door>(ref list);
                            }
                            CardReader cardReader1 = newEntity as CardReader;
                            if ((UnityEngine.Object)cardReader1 != (UnityEngine.Object)null)
                            {
                                CardReader cardReader2 = componentsInChild as CardReader;
                                if ((UnityEngine.Object)cardReader2 != (UnityEngine.Object)null)
                                {
                                    cardReader1.accessLevel = cardReader2.accessLevel;
                                    cardReader1.accessDuration = cardReader2.accessDuration;
                                }
                            }
                            TimerSwitch timerSwitch1 = newEntity as TimerSwitch;
                            if ((UnityEngine.Object)timerSwitch1 != (UnityEngine.Object)null)
                            {
                                TimerSwitch timerSwitch2 = componentsInChild as TimerSwitch;
                                if ((UnityEngine.Object)timerSwitch2 != (UnityEngine.Object)null)
                                    timerSwitch1.timerLength = timerSwitch2.timerLength;
                            }
                        }
                    }

                    foreach (KeyValuePair<IOEntity, IOEntity> keyValuePair in dictionary)
                    {
                        IOEntity key = keyValuePair.Key;
                        IOEntity ioEntity = keyValuePair.Value;
                        for (int index = 0; index < key.outputs.Length; ++index)
                        {
                            if (!((UnityEngine.Object)key.outputs[index].connectedTo.Get() == (UnityEngine.Object)null))
                            {
                                ioEntity.outputs[index].connectedTo.ioEnt = dictionary[key.outputs[index].connectedTo.ioEnt];
                                ioEntity.outputs[index].connectedToSlot = key.outputs[index].connectedToSlot;
                            }


                        }

                    }
                    foreach (BaseNetworkable baseNetworkable in dictionary.Values)
                        baseNetworkable.Spawn();
                }
            }
        }

        [ConsoleCommand("puzzlerepair")]
        void cmdPuzzleFix(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin)
            {
                SendReply(arg, "NotAllowed");
                return;
            }

            PuzzlePrefabRespawn(arg);
        }

        [ConsoleCommand("debug.puzzleprefabrespawn")]
        void cmdOverride(ConsoleSystem.Arg arg)
        {
            Puts("Override succesfull");
            if (!arg.IsAdmin)
            {
                SendReply(arg, "NotAllowed");
                return;
            }

            PuzzlePrefabRespawn(arg);
        }
    }
}
