using System;
 using System.Collections.Generic;
 using System.Linq;

 namespace Oxide.Plugins
 {
     [Info("DMDeployables", "ColonBlow", "1.1.11", ResourceId = 1240)]
     class DMDeployables : RustPlugin
     {
         private Dictionary<string, bool> deployables = new Dictionary<string, bool>();
         private Dictionary<string, string> prefabs = new Dictionary<string, string>();
         private bool init = false;

         void OnServerInitialized() => LoadVariables();

         void Unload()
         {
             deployables.Clear();
             prefabs.Clear();
         }

         object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
         {
             if (!init || entity == null || hitInfo == null)
                 return null;

             var kvp = prefabs.FirstOrDefault(x => x.Key == entity.PrefabName);

             return !string.IsNullOrEmpty(kvp.Value) && deployables.ContainsKey(kvp.Value) && deployables[kvp.Value] ? (object)false : null;
         }

         #region Config
         private bool Changed;

         void LoadVariables()
         {
             foreach (var itemDef in ItemManager.GetItemDefinitions().ToList())
             {
                 var mod = itemDef.GetComponent<ItemModDeployable>();

                 if (mod != null)
                 {
                     deployables[itemDef.displayName.translated] = Convert.ToBoolean(GetConfig("Deployables", string.Format("Block {0}", itemDef.displayName.translated), false));
                     prefabs[mod.entityPrefab.resourcePath] = itemDef.displayName.translated;
                 }
             }

             init = true;

             if (Changed)
             {
                 SaveConfig();
                 Changed = false;
             }
         }

         protected override void LoadDefaultConfig()
         {
             PrintWarning("Creating a new configuration file");
             Config.Clear();
             LoadVariables();
         }

         object GetConfig(string menu, string datavalue, object defaultValue)
         {
             var data = Config[menu] as Dictionary<string, object>;
             if (data == null)
             {
                 data = new Dictionary<string, object>();
                 Config[menu] = data;
                 Changed = true;
             }
             object value;
             if (!data.TryGetValue(datavalue, out value))
             {
                 value = defaultValue;
                 data[datavalue] = value;
                 Changed = true;
             }
             return value;
         }
         #endregion
     }
 }