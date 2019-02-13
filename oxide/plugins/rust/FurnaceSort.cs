using Oxide.Core;
using System.Collections.Generic;
using System.Linq;
using System;
using Oxide.Core.Configuration;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("FurnaceSort", "Pattrik", "1.0.0")]
    class FurnaceSort : RustPlugin
    {
        #region FIELDS

        List<ulong> usePlayers = new List<ulong>();
        static List<ulong> activePlayers = new List<ulong>();
        Dictionary<BaseOven, BasePlayer> ovens = new Dictionary<BaseOven, BasePlayer>();

        private string furnacePanelMin = "0.895 0.42";
        private string furnacePanelMax = "0.945 0.46";
        private string furnaceTextMin = "0.65 0.42";
        private string furnaceTextMax = "0.89 0.46";

        private string furnaceBigPanelMin = "0.895 0.52";
        private string furnaceBigPanelMax = "0.945 0.56";
        private string furnaceBigTextMin = "0.65 0.52";
        private string furnaceBigTextMax = "0.89 0.56";


        #endregion

        #region OXIDE HOOKS

        void OnServerInitialized()
        {
            LoadDefaultConfig();
            LoadData();
        }

        void Unload()
        {
            SaveData();
            foreach (var dest in UnityEngine.Object.FindObjectsOfType<UIDestroyer>())
            {
                dest.Dest();
            }
        }


        void OnLootEntity(BasePlayer player, BaseEntity lootable)
        {
            if (player == null || lootable == null) return;
            var furnace = lootable.GetComponent<BaseOven>();
            if (furnace == null || !furnace.name.Contains("furnace")) return;

            DrawUI(player, furnace.ShortPrefabName != "furnace");
        }

        void DrawUI(BasePlayer player, bool bigFurnace = false)
        {
            bool use = usePlayers.Contains(player.userID);
            var container = new CuiElementContainer
            {
                {
                    new CuiPanel()
                    {
                        RectTransform = {AnchorMin = bigFurnace ? furnaceBigPanelMin : furnacePanelMin, AnchorMax =bigFurnace ? furnaceBigPanelMax : furnacePanelMax},
                        Image = new CuiImageComponent() {Color = "0 0 0 0"}
                    },
                    "Overlay", "furnacesort"
                },
                {
                    new CuiLabel()
                    {
                        RectTransform = {AnchorMin = bigFurnace ? furnaceBigTextMin : furnaceTextMin, AnchorMax = bigFurnace ? furnaceBigTextMax : furnaceTextMax},
                        Text =
                        {
                            Text = "Automatic sorting furnaces:",
                            FontSize = 16,
                            Align = TextAnchor.MiddleRight
                        }
                    },
                    "Overlay", "furnacesort.lbl"
                },
                {
                    new CuiButton()
                    {
                        Button = {Command = "furnacesort.switch", Color = use ? "0.62 0.18 0.18 1" : "0.4 0.66 0.2 1"},
                        RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                        Text = {Align = TextAnchor.MiddleCenter, Text = use ? "[OFF]" : "[ON]"}
                    },
                    "furnacesort", "furnacesort.switchbtn"
                }
            };
            CuiHelper.AddUi(player, container);
            var uiDestroyer = player.inventory.loot.entitySource.gameObject.AddComponent<UIDestroyer>();
            uiDestroyer.player = player;
            activePlayers.Add(player.userID);
            ovens[(BaseOven)player.inventory.loot.entitySource] = player;
        }

        private bool work = false;

        void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            if (work || item == null) return;
            work = true;
            var oven = container.entityOwner as BaseOven;
            if (oven == null)
            {
                work = false;
                return;
            }
            BasePlayer player;
            if (!ovens.TryGetValue(oven, out player))
            {
                work = false;
                return;
            }
            if (!usePlayers.Contains(player.userID))
            {
                work = false;
                return;
            }
            if (!(item.info.shortname == "sulfur.ore" || item.info.shortname == "metal.ore" ||
                  item.info.shortname == "hq.metal.ore"))
            {
                work = false;
                return;
            }
            int i1 = container.GetAmount(3655341, false) > 0 ? 0 : 1;
            int i2 = container.GetAmount(1436001773, false) > 0 ? 0 : 1;
            var max = container.capacity - container.itemList.Count - i1 - i2;
            if (max == 0)
            {
                work = false;
                return;
            }
            if (item.amount < max)
            {
                work = false;
                return;
            }
            int cellAmount = item.amount / max;
            for (int j = 1; j < max; j++)
            {
                var it = item.SplitItem(cellAmount);
                if (!it.MoveToContainer(container, -1, false))
                {
                    it.MoveToContainer(player.inventory.containerMain);
                }
            }
            work = false;
        }


        [ConsoleCommand("furnacesort.switch")]
        void cmdSwitch(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            if (usePlayers.Contains(player.userID))
            {
                usePlayers.Remove(player.userID);
            }
            else
            {
                usePlayers.Add(player.userID);
            }
            player.inventory.loot.entitySource.GetComponent<UIDestroyer>().Dest();
            DrawUI(player, player.inventory.loot?.entitySource?.ShortPrefabName != "furnace");
        }

        class UIDestroyer : MonoBehaviour
        {
            public BasePlayer player;
            void PlayerStoppedLooting(BasePlayer player)
            {
                CuiHelper.DestroyUi(player, "furnacesort.lbl");
                CuiHelper.DestroyUi(player, "furnacesort.switchbtn");
                CuiHelper.DestroyUi(player, "furnacesort");
                activePlayers.Remove(player.userID);
                Destroy(this);
            }

            public void Dest()
            {
                CuiHelper.DestroyUi(player, "furnacesort.lbl");
                CuiHelper.DestroyUi(player, "furnacesort.switchbtn");
                CuiHelper.DestroyUi(player, "furnacesort");
                activePlayers.Remove(player.userID);
                Destroy(this);
            }
        }

        #endregion

        #region CORE

        #endregion

        #region DATA

        private DynamicConfigFile saveFile = Interface.Oxide.DataFileSystem.GetFile("FurnaceSort");


        void LoadData()
        {
            usePlayers = saveFile.ReadObject<List<ulong>>() ?? new List<ulong>();
        }

        void OnServerSave()
        {
            SaveData();
        }

        void SaveData()
        {
            saveFile.WriteObject(usePlayers);
        }

        #endregion
    }
}