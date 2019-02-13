using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Invulnerable Players", "Hamster", "1.0.4")]
    [Description("Prevents aggression / targeting of entities to the player")]
    public class InvulnerablePlayers : RustPlugin
    {
        
        private const string PermUse = "invulnerableplayers.use";
        private List<BasePlayer> _data = new List<BasePlayer>();
        
        #region Hook

        private void Init()
        {
            permission.RegisterPermission(PermUse, this);
        }
        
		 protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "No Permission", "You don't have enough permissions" },
                { "Enabled", "<b><color=#9acd32><size=16>Enabled</size></color></b>" },
                { "Disabled", "<b><color=#ce422b><size=16>Disabled</size></color></b>" }
            }, this);
        }
		
        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            var basePlayer = entity as BasePlayer;
            if (basePlayer == null || !_data.Contains(basePlayer))
                return null;

            return false;
        }
        
        private object CanHelicopterStrafeTarget(PatrolHelicopterAI entity, BasePlayer target)
        {
            if (_data.Contains(target))
                return false;

            return null;
        }
        
        // turret/heli
        private object CanBeTargeted(BaseCombatEntity entity, MonoBehaviour behaviour)
        {
            var basePlayer = entity as BasePlayer;

            if (basePlayer != null && _data.Contains(basePlayer))
                return false;

            return null;
        }
        
        //apc
        private object CanBradleyApcTarget(BradleyAPC apc, BaseEntity entity)
        {
            var basePlayer = entity as BasePlayer;
            
            if (basePlayer != null && _data.Contains(basePlayer))
                return false;

            return null;
        }

        // heli
        private object CanHelicopterTarget(PatrolHelicopterAI heli, BasePlayer basePlayer)
        {
            if (_data.Contains(basePlayer))
                return false;

            return null;
        }
        
        //scientist NPCs
        private object OnNpcPlayerTarget(NPCPlayerApex npc, BaseEntity entity)
        {
            var basePlayer = entity as BasePlayer;
            
            if (basePlayer != null && _data.Contains(basePlayer) && !IsNpc(basePlayer))
                return false;

            return null;
        }

        //animals
        private object OnNpcTarget(BaseNpc npc, BaseEntity entity)
        {
            var basePlayer = entity as BasePlayer;
            
            if (basePlayer != null && _data.Contains(basePlayer))
                return false;

            return null;
        }

        private bool CanNpcAttack(BaseNpc npc, BaseEntity entity)
        {
            var basePlayer = entity as BasePlayer;
            if (basePlayer != null && basePlayer.IsAdmin) { return false; };
            return true;
        }
        
        private object OnTurretTarget(AutoTurret turret, BaseCombatEntity entity)
        {
            var basePlayer = entity as BasePlayer;

            if (basePlayer != null && _data.Contains(basePlayer))
                return false;

            return null;
        }
        
        //Traps and mines
        private object OnTrapTrigger(BaseTrap trap, GameObject go)
        {
            var basePlayer = go.GetComponent<BasePlayer>();
            if (basePlayer != null && _data.Contains(basePlayer))
                return false;

            return null;
        }
        
        private object OnPlayerLand(BasePlayer basePlayer, float num)
        {
            if (_data.Contains(basePlayer))
            {
                return false;
            }
            return null;
        }
        
        private static bool IsNpc(BasePlayer player) //исключение НПС
        {
            if (player == null) return false;
            //BotSpawn
            if (player is NPCPlayer)
                return true;
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (player is NPCPlayerApex)
                return true;
#pragma warning disable 184
            if (player is BaseNpc)
#pragma warning restore 184
                return true;
            //HumanNPC
            if (!(player.userID >= 76560000000000000L || player.userID <= 0L))
                return true;
            return false;
        }

        #endregion

        #region Metabolism

        private object OnRunPlayerMetabolism(PlayerMetabolism metabolism)
        {
            var basePlayer = metabolism.GetComponent<BasePlayer>();

            if (basePlayer != null && _data.Contains(basePlayer))
            {
                basePlayer.InitializeHealth(100, 100);
                metabolism.oxygen.Add(metabolism.oxygen.max);
                metabolism.wetness.Add(-metabolism.wetness.max);
                metabolism.radiation_level.Add(-metabolism.radiation_level.max);
                metabolism.radiation_poison.Add(-metabolism.radiation_poison.max);
                metabolism.temperature.Reset();
                metabolism.hydration.Add(metabolism.hydration.max);
                metabolism.calories.Add(metabolism.calories.max);
                metabolism.bleeding.Reset();
                return false;
            }
            return null;
        }

        #endregion

        #region Command
        
        [ChatCommand("inv")]
        private void CmdCraftBoat(BasePlayer basePlayer, string command, string[] args)
        {
            var ids = basePlayer.UserIDString;
            if (!permission.UserHasPermission(ids, PermUse) && !basePlayer.IsAdmin)
            {
                basePlayer.ChatMessage(GetMsg("No Permission", ids));
                return;
            }

            if (_data.Contains(basePlayer))
            {
                _data.Remove(basePlayer);
                basePlayer.ChatMessage(GetMsg("Disabled", ids));
                return;
            }
            
            _data.Add(basePlayer);
            basePlayer.ChatMessage(GetMsg("Enabled", ids));
        }

        #endregion

        #region Hh

        private string GetMsg(string key, string userId = null) => lang.GetMessage(key, this, userId);

        #endregion
        
    }
}