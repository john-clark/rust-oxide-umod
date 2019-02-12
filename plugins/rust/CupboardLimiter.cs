using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Convert = System.Convert;

namespace Oxide.Plugins
{
    [Info("Cupboard Limiter", "BuzZ", "0.0.1")]
    [Description("Set a maximum cupboards for player(s)")]

/*======================================================================================================================= 
*
*   
*   10th december 2018
*   command : none
*
*   0.0.1   20181210    creation
*
*	THANKS TO THE OXIDE/UMOD TEAM for coding quality, ideas, and time spent for the community
*
*   IsAuthed(BasePlayer) : Boolean _ BuildingPrivlidge from StorageContainer
*
*=======================================================================================================================*/

    public class CupboardLimiter : RustPlugin
    {       
        bool debug = false;
        const string CupboardLimitMax = "cupboardlimiter.max"; 
        int limit = 1;

        private bool ConfigChanged;

        void Init()
        {
            permission.RegisterPermission(CupboardLimitMax, this);
            LoadVariables();
        }

#region MESSAGES

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"MaxLimit", "You have reached the maximum cupboard limit."},
            }, this, "en");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"MaxLimit", "Vous avez atteint le nombre maximum de cupboard."},
            }, this, "fr");
        }

#endregion
#region CONFIG

    protected override void LoadDefaultConfig()
        {
            LoadVariables();
        }

        private void LoadVariables()
        {
            limit = Convert.ToInt32(GetConfig("Limit Settings", "Max Cupboard by player", "1"));

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
//////////////////////////////////////////////////////////////////////////////////
        void OnEntitySpawned(BaseEntity entity, UnityEngine.GameObject gameObject)
        {
            if(entity.ShortPrefabName.Contains("cupboard.tool"))
            {
                if (debug)Puts($"a cupboard is spawning in world");
                if(entity.OwnerID == null) return;
                BasePlayer player = BasePlayer.FindByID(entity.OwnerID);
                if (player == null) return;
                if(player.IsSleeping() == true || player.IsConnected == false) return;
                if (debug)Puts($"sleep|offline check");
////////////////// HOW MANY CHECK
                bool limited = permission.UserHasPermission(player.UserIDString, CupboardLimitMax);
                if (limited)
                {
                    int count = new int();
                    count = TellMeHowManyCupboard (player);
                    if (debug)Puts($"cupboard count {count}");
//////////////// EXTRA CHECK IF PLAYER HAS ABNORMAL CUPBOARD COUNT
                    if (count - limit > 1 ) PrintWarning($"PLAYER {player.displayName} has {count-1}x cupboards !");
/////////// CANCEL IF LIMIT REACHED
                    if (count > limit)
                    {
                        CancelThisCupboard (entity, player);
                        return;
                    }
                }
            }
        }

        void CancelThisCupboard (BaseEntity entity, BasePlayer player)
        {
            if (debug)Puts($"cancelling a cupboard");
            SendReply(player, lang.GetMessage("MaxLimit", this));  
            entity.KillMessage();
            var itemtogive = ItemManager.CreateByItemID(-97956382, 1);
            if (itemtogive != null) player.inventory.GiveItem(itemtogive);
        }

        private int TellMeHowManyCupboard (BasePlayer player)
        {
            List<BaseEntity> playercups = new List<BaseEntity>();
            int count = 0;
            foreach (var entity in UnityEngine.Object.FindObjectsOfType<BaseEntity>())
            {
                if(entity.ShortPrefabName.Contains("cupboard.tool"))
                {
                    if (entity.OwnerID == player.userID) playercups.Add(entity);
                }
            }
            if (playercups != null)
            {
                count = playercups.Count();
            }
            return count;
        }
    }
}