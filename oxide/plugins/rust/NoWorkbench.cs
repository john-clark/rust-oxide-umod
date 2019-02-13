using Newtonsoft.Json;
using System.Linq;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("NoWorkbench", "k1lly0u", "0.1.50", ResourceId = 2645)]
    class NoWorkbench : RustPlugin
    {        
        private Dictionary<int, int> defaultBlueprints;

        #region Oxide Hooks  
        private void OnServerInitialized()
        {
            LoadVariables();
            defaultBlueprints = ItemManager.GetBlueprints().ToDictionary(x => x.targetItem.itemid, y => y.workbenchLevelRequired);

            foreach (ItemBlueprint bp in ItemManager.GetBlueprints())            
                bp.workbenchLevelRequired = 0;            

            foreach (BasePlayer player in BasePlayer.activePlayerList)
                OnPlayerInit(player);
        }
       
        private void OnPlayerInit(BasePlayer player)
        {
            if (player.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot))
            {
                timer.In(3, () => OnPlayerInit(player));
                return;
            }

            player.ClientRPCPlayer(null, player, "craftMode", 1);

            if (configData.NoBlueprints)
                UnlockAllBlueprints(player);             
        }        

        private void UnlockAllBlueprints(BasePlayer player)
        {
            ProtoBuf.PersistantPlayer playerInfo = SingletonComponent<ServerMgr>.Instance.persistance.GetPlayerInfo(player.userID);
            foreach (ItemBlueprint itemBlueprint in ItemManager.bpList)
            {
                if (itemBlueprint.userCraftable && !itemBlueprint.defaultBlueprint)
                {
                    if (!playerInfo.unlockedItems.Contains(itemBlueprint.targetItem.itemid))                   
                        playerInfo.unlockedItems.Add(itemBlueprint.targetItem.itemid);
                }
            }
            SingletonComponent<ServerMgr>.Instance.persistance.SetPlayerInfo(player.userID, playerInfo);
            player.SendNetworkUpdateImmediate(false);
            player.ClientRPCPlayer<int>(null, player, "UnlockedBlueprint", 0);
        }

        private void Unload()
        {
            foreach (ItemBlueprint bp in ItemManager.GetBlueprints())
                bp.workbenchLevelRequired = defaultBlueprints[bp.targetItem.itemid];
        }
        #endregion
       
        #region Config        
        private ConfigData configData;
        private class ConfigData
        {
            [JsonProperty(PropertyName = "Disable the need for blueprints")]
            public bool NoBlueprints { get; set; }            
        }

        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            ConfigData config = new ConfigData
            {
                NoBlueprints = false
            };
            SaveConfig(config);
        }

        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();

        private void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion
    }
}