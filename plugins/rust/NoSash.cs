using System.Collections.Generic;
using System;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Core.Configuration;
using Oxide.Core;
using System.Linq;
using UnityEngine;
using Oxide.Game.Rust.Cui;
using System.Collections;
using System.Reflection;

namespace Oxide.Plugins
{
    [Info("NoSash", "Jake_Rich", "1.0.0")]
    [Description("Sashes look stupid and aren't needed")]

    public class NoSash : RustPlugin
    {
        void OnServerInitialized()
        {
            foreach(var player in BaseNetworkable.serverEntities.OfType<BasePlayer>())
            {
                OnPlayerSpawn(player);
            }
        }

        void Unload()
        {
            foreach(var player in BaseNetworkable.serverEntities.OfType<BasePlayer>())
            {
                RemovePlayerHooks(player);
            }
        }

        void OnPlayerSpawn(BasePlayer player)
        {
            NextFrame(() =>
            {
                AddPlayerHooks(player);
            });
        }

        public void AddPlayerHooks(BasePlayer player)
        {
            player.inventory.containerBelt.onItemAddedRemoved += OnItemAddedRemoved;
            player.inventory.containerMain.onItemAddedRemoved += OnItemAddedRemoved;
            player.inventory.containerWear.onItemAddedRemoved += OnItemAddedRemoved;
        }

        public void RemovePlayerHooks(BasePlayer player)
        {
            player.inventory.containerBelt.onItemAddedRemoved -= OnItemAddedRemoved;
            player.inventory.containerMain.onItemAddedRemoved -= OnItemAddedRemoved;
            player.inventory.containerWear.onItemAddedRemoved -= OnItemAddedRemoved;
        }

        private void OnItemAddedRemoved(Item item, bool added)
        {
            var player = item?.parent?.playerOwner;
            if (player == null)
            {
                return;
            }
            player.SetPlayerFlag(BasePlayer.PlayerFlags.DisplaySash, false);
        }
    }
}