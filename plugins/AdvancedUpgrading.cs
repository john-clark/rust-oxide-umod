using System.Collections.Generic;
using Oxide.Core;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Advanced Upgrading", "Ryan", "1.0.0")]
    class AdvancedUpgrading : RustPlugin
    {
        #region Declaration

        private ConfigFile CFile;
        private DataFileSystem DFS = Interface.Oxide.DataFileSystem;
        private Dictionary<uint, int> Entities = new Dictionary<uint, int>();
        private const string BypassPerm = "advancedupgrading.bypass";

        #endregion

        #region Configuration

        private class ConfigFile
        {
            [JsonProperty("Make hit data persist restarts")]
            public bool Persist;

            [JsonProperty("Grade and amount of hits it needs")]
            public Dictionary<BuildingGrade.Enum, int> Hits;

            public static ConfigFile DefaultConfig()
            {
                return new ConfigFile
                {
                    Persist = true,
                    Hits = new Dictionary<BuildingGrade.Enum, int>
                    {
                        [BuildingGrade.Enum.Wood] = 10,
                        [BuildingGrade.Enum.Stone] = 20,
                        [BuildingGrade.Enum.Metal] = 30,
                        [BuildingGrade.Enum.TopTier] = 40
                    }
                };
            }
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Generating default configuration file...");
            CFile = ConfigFile.DefaultConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                CFile = Config.ReadObject<ConfigFile>();
                if (CFile == null) Regenerate();
            }
            catch { Regenerate(); }
        }

        protected override void SaveConfig() => Config.WriteObject(CFile);

        private void Regenerate()
        {
            PrintWarning($"Configuration file at 'oxide/config/{Name}.json' seems to be corrupt! Regenerating...");
            CFile = ConfigFile.DefaultConfig();
            SaveConfig();
        }

        #endregion

        #region Lang

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoUpgrade"] = "You can't upgrade to <color=orange>{0}</color> yet, you need to hit it with a hammer <color=orange>{1}</color> more times.",
                ["UpgradeReady"] = "An upgrade to <color=orange>{0}</color> on this block is now available."
            }, this);
        }

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        #endregion

        #region Hooks

        private void Init()
        {
            if (!CFile.Persist) Unsubscribe(nameof(OnServerSave));
            else Entities = DFS.ReadObject<Dictionary<uint, int>>(Name);
            permission.RegisterPermission(BypassPerm, this);
        }

        private void OnServerSave() => DFS.WriteObject(Name, Entities);

        private void OnNewSave() => Entities.Clear();

        private void OnEntitySpawned(BaseNetworkable entity)
        {
            if (!(entity is BuildingBlock)) return;
            if(!Entities.ContainsKey(entity.net.ID)) Entities.Add(entity.net.ID, 0);
        }

        private object OnStructureUpgrade(BaseCombatEntity entity, BasePlayer player, BuildingGrade.Enum grade)
        {
            if (permission.UserHasPermission(player.UserIDString, BypassPerm)) return null;
            if (CFile.Hits.ContainsKey(grade) && Entities.ContainsKey(entity.net.ID) && CFile.Hits[grade] > Entities[entity.net.ID])
            {
                PrintToChat(player, Lang("NoUpgrade", player.UserIDString, grade.ToString(), CFile.Hits[grade] - Entities[entity.net.ID]));
                return false;
            }
            return null;
        }

        private void OnHammerHit(BasePlayer player, HitInfo info)
        {
            var entity = info?.HitEntity;
            if (entity != null)
            {
                if(!Entities.ContainsKey(entity.net.ID))
                    Entities.Add(entity.net.ID, 0);
                Entities[entity.net.ID]++;
                player.SendConsoleCommand("note.inv", -1565095136, 1, $"Total Hits: {Entities[entity.net.ID]}");
                foreach (var hit in CFile.Hits)
                {
                    if(hit.Value != Entities[entity.net.ID]) continue;
                    Effect.server.Run("assets/bundled/prefabs/fx/build/promote_" + hit.Key.ToString().ToLower() + ".prefab", player.transform.position);
                    PrintToChat(player, Lang("UpgradeReady", player.UserIDString, hit.Key));
                    break;
                }
            }
        }

        #endregion
    }
}