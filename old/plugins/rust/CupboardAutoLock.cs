using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Convert = System.Convert;

namespace Oxide.Plugins
{
    [Info("Cupboard Auto Lock", "BuzZ", "0.0.3")]
    [Description("Automatically add a codelock on cupboards. Option to lock access to its inventory")]

/*======================================================================================================================= 
*
*   
*   10th december 2018
*
*   0.0.1   20181210    creation
*
*	THANKS TO THE OXIDE/UMOD TEAM for coding quality, ideas, and time spent for the community
*
*
*=======================================================================================================================*/

    public class CupboardAutoLock : RustPlugin
    {

        bool debug = false;
        bool cupboardnorefill = false;
        private bool ConfigChanged;
        const string AutoLock = "cupboardautolock.code"; 

        void Init()
        {
            LoadVariables();
            permission.RegisterPermission(AutoLock, this);
        }

#region CONFIG

    protected override void LoadDefaultConfig()
        {
            LoadVariables();
        }

        private void LoadVariables()
        {
            cupboardnorefill = Convert.ToBoolean(GetConfig("Cupboard inventory access", "Block access", "false"));
            if (!ConfigChanged) return;
            SaveConfig();
            ConfigChanged = false;
        }

        private object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                ConfigChanged = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                ConfigChanged = true;
            }
            return value;
        }

#endregion

        void OnEntitySpawned(BaseEntity entity, UnityEngine.GameObject gameObject)
        {
            if(entity.ShortPrefabName.Contains("cupboard.tool"))
            {
                if (debug)Puts($"a cupboard is spawning in world");
                //if(entity.OwnerID == null) return;
                BasePlayer player = BasePlayer.FindByID(entity.OwnerID);
                if (player == null) return;
                if(player.IsSleeping() == true || player.IsConnected == false) return;
                bool isauth = permission.UserHasPermission(player.UserIDString, AutoLock);
                if (!isauth) return;
                BaseEntity slotEntity = GameManager.server.CreateEntity("assets/prefabs/locks/keypad/lock.code.prefab", new Vector3(), new Quaternion(), true);
                if (slotEntity == null)
                {
                    if (debug) Puts ("slotEntity is NULL");
                    return;
                }
                slotEntity.gameObject.Identity();
                slotEntity.SetParent(entity, 0);
                slotEntity.OnDeployed(entity);
                slotEntity.transform.localPosition = new Vector3(0, 1f, 0.4f);
                slotEntity.transform.localEulerAngles = new Vector3(-90, -90, 0);
                slotEntity.Spawn();
                if (cupboardnorefill) entity.SetFlag(BaseEntity.Flags.Locked, true, false);
                entity.SetSlot(BaseEntity.Slot.Lock, slotEntity);
                //CodeLock codeLock = slotEntity.GetComponent<CodeLock>();  FOR FUTURE
                if (debug)Puts($"placing a cupboard");
            }
        }
    }
}