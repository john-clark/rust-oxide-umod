using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Autoloot", "micaelr95", 0.1)]
    [Description("Autoloot entity")]
    public class Autoloot : RustPlugin
    {
        bool canLoot = false;
        BasePlayer globalPlayer;
        BaseEntity globalEntity;

        void OnFrame()
        {
            if (canLoot)
            {
                if (globalEntity as LootableCorpse)
                {
                    var corpse = globalEntity as LootableCorpse;
                    foreach (var container in corpse.containers)
                    {
                        foreach (var item in container.itemList.ToList())
                        {
                            globalPlayer.inventory.GiveItem(item);
                        }
                    }
                }
                else
                {
                    if (globalEntity as StorageContainer)
                    {
                        var container = globalEntity as StorageContainer;
                        foreach (var item in container.inventory.itemList.ToList())
                        {
                            globalPlayer.inventory.GiveItem(item);
                        }
                    }
                    else
                    {
                        if (globalEntity as DroppedItemContainer)
                        {
                            var backpack = globalEntity as DroppedItemContainer;
                            foreach (var item in backpack.inventory.itemList.ToList())
                            {
                                globalPlayer.inventory.GiveItem(item);
                            }
                        }
                        else
                        {
                            if (globalEntity as BasePlayer)
                            {
                                var sleeper = globalEntity as BasePlayer;
                                foreach (var item in sleeper.inventory.containerMain.itemList.ToList())
                                {
                                    globalPlayer.inventory.GiveItem(item);
                                }
                                foreach (var item in sleeper.inventory.containerBelt.itemList.ToList())
                                {
                                    globalPlayer.inventory.GiveItem(item);
                                }
                                foreach (var item in sleeper.inventory.containerWear.itemList.ToList())
                                {
                                    globalPlayer.inventory.GiveItem(item);
                                }
                            }
                            else
                            {
                                return;
                            }
                        }
                    }
                }
                canLoot = false;
            }
        }

        void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            globalPlayer = player;
            globalEntity = entity;

            CuiElementContainer elements = new CuiElementContainer();
            var panel = elements.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0.1 0.1 0.1 0.6"
                },
                RectTransform =
                {
                    AnchorMin = "0.86 0.92",
                    AnchorMax = "0.97 0.98"
                }
            }, "Overall", "panel");

            elements.Add(new CuiButton
            {
                Button =
                {
                    Command = "autoloot",
                    Color = "0.8 0.8 0.8 0.2"
                },
                Text =
                {
                    Text = "AutoLoot",
                    FontSize = 22,
                    Align = TextAnchor.MiddleCenter
                }
            }, panel);
            CuiHelper.AddUi(player, elements);
        }

        void OnLootEntityEnd(BasePlayer player, BaseCombatEntity entity)
        {
            globalPlayer = null;
            globalEntity = null;
            CuiHelper.DestroyUi(player, "panel");
        }

        [ConsoleCommand("autoloot")]
        void CanLoot()
        {
            canLoot = true;
        }
    }
}
