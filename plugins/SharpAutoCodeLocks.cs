using Oxide.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Sharp Auto Code Locks", "NoSharp", "1.0.4")]
    [Description("A plugin that automatically adds a codelock with a code selected by the user.")]
    class SharpAutoCodeLocks : RustPlugin
    {
        #region Fields

        private const string PermissionUser = "sharpautocodelocks.user";

        private List<PlayerData> playerDatas;

        #endregion Fields

        #region Helpers 

        public void LoadPlayerDatas()
        {
            playerDatas = Interface.Oxide.DataFileSystem.ReadObject<List<PlayerData>>(this.Name);
        }

        public void SavePlayerDatas()
        {
            Interface.Oxide.DataFileSystem.WriteObject(this.Name, playerDatas);
        }

        public PlayerData GetPlayerFromPlayerDatas(BasePlayer player)
        {
            PlayerData playerData = null;
            foreach (var pData in playerDatas)
            {
                if (pData.PlayerId == player.UserIDString)
                {
                    playerData = pData;
                       
                    
                }
            }

            return playerData;
        }

        public void CreatePlayerData(BasePlayer player)
        {
            var playerData = new PlayerData(player.UserIDString);

            System.Random random = new System.Random();

            playerData.CodeLockCode = random.Next(1000, 9999).ToString();
            this.playerDatas.Add(playerData);
        }

        public string GetMessageFromLang(string langKey, BasePlayer player)
        {
            return lang.GetMessage(langKey, this, player.UserIDString);
        }

        public void SendMessageToPlayer(BasePlayer player, string langKey, params string[] messages )
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(String.Format(GetMessageFromLang(langKey, player), messages));

            SendReply(player, builder.ToString());
        }

        public bool DoesPlayerHavePermission(BasePlayer player, string perm)
        {
            return permission.UserHasPermission(player.UserIDString, perm);
        }

        public bool DoesPlayerHaveCodeLocks(BasePlayer player)
        {
            return player.inventory.GetAmount(1159991980) > 0;
        }

        public void RemoveCodeLockFromInventoy(BasePlayer player)
        {
            player.inventory.Take(null, 1159991980, 1);
        }

        #endregion Helpers

        #region Classes
        
        public class PlayerData
        {
            
            public bool IsAutoCodeLockActive { get; set; }
            
            public string CodeLockCode { get; set; }
            
            public string PlayerId { get; }

            public PlayerData(string playerId)
            {
                this.CodeLockCode = "1234";
                this.PlayerId = playerId;
            }

        }

        #endregion Classes

        #region Lang

        /// <summary>
        /// Creates a variable to access the lang messages, variable names mirror the name of the lang message key.
        /// </summary>
        struct LangAccessor {
            public const string LackPermissions = "LackPermissions";
            public const string IncorrectArgumentsLength = "IncorrectArgumentsLength";
            public const string IncorrentArgumentFormat = "IncorrectArgumentFormat";
            public const string NoCommandFound = "NoCommandFound";
            public const string FeaturesStatus = "FeaturesStatus";
            public const string OnPinUpdate = "OnPinUpdate";
            public const string OnCodeLockUpdate = "OnCodeLockAutoUpdate";
            public const string OnDoorPlace = "OnDoorPlace";
            public const string True = "True";
            public const string False = "False";
            public const string Active = "Active";
            public const string InActive = "Inactive";
        }

        #endregion Lang

        #region Oxide Hooks

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["LackPermissions"] = "<color=#DA70D6>SharpAutoCodeLock:</color> No Permissions",
                ["IncorrectArgumentsLength"] = "<color=#DA70D6>SharpAutoCodeLock:</color> Not the correct arguments.",
                ["IncorrectArgumentFormat"] = "<color=#DA70D6>SharpAutoCodeLock:</color> Incorrect Command Format",
                ["NoCommandFound"] = "<color=#DA70D6>SharpAutoCodeLock:</color> No Command found.",
                ["FeaturesStatus"] =
            "<color=#DA70D6>SharpAutoCodeLock:</color> Feature's Status \n" +
            "<color=#FCD12A>[pin (pin number)] Current Pin:</color> {0} \n" +
            "<color=#FCD12A>[lock][unlock] Auto Lock:</color> {1}",
                ["OnPinUpdate"] = "<color=#DA70D6>SharpAutoCodeLock:</color> Pin changed to: {0}",
                ["OnCodeLockAutoUpdate"] = "<color=#DA70D6>SharpAutoCodeLock:</color> Automatic Code Locking' is {0}",
                ["OnDoorPlace"] = "<color=#DA70D6>SharpAutoCodeLock:</color> Code lock placed! pin used: {0}",
                ["True"] = "<color=green> True </color>",
                ["False"] = "<color=red> False </color>",
                ["Active"] = "<color=green> Active </color>",
                ["InActive"] = "<color=red> Not Active </color>"
            }, this);
        }

        void OnServerInitialized()
        {
            LoadPlayerDatas();
            SavePlayerDatas();
            
            var playerList = BasePlayer.activePlayerList.ToList();
            foreach (var player in playerList )
            {
                var DoesPlayerExist = GetPlayerFromPlayerDatas(player);

                if (DoesPlayerExist == null)
                {
                    CreatePlayerData(player);
                }
            }

        }

        void Init()
        {
            permission.RegisterPermission(PermissionUser, this);
        }

        void OnPlayerConnected(Network.Message packet)
        {


            BasePlayer player = packet.Player();

            if (player == null) return;

            var DoesPlayerExist = GetPlayerFromPlayerDatas(player);

            if (DoesPlayerExist == null)
            {
                CreatePlayerData(player);
            }
        }

        void Unload()
        {
            SavePlayerDatas();
        }

        void OnEntityBuilt(Planner plan, GameObject go)
        {
            if (plan == null || go == null) return;
            

            if (go.ToBaseEntity() is Door)
            {
                
                var player = plan.GetOwnerPlayer();
                if (player == null) return;
                if (DoesPlayerHavePermission(player, PermissionUser))
                {
                    
                    var playerData = GetPlayerFromPlayerDatas(player);
                    if (playerData == null) return;
                    if (playerData.IsAutoCodeLockActive && DoesPlayerHaveCodeLocks(player))
                    {
                        var door = go.ToBaseEntity() as Door;
                        if (door == null) return;

                        var codeLock = GameManager.server.CreateEntity("assets/prefabs/locks/keypad/lock.code.prefab", new Vector3(), new Quaternion(), true);
                        if (codeLock == null) return;

                        codeLock.gameObject.Identity();
                        codeLock.SetParent(door, door.GetSlotAnchorName(BaseEntity.Slot.Lock));
                        codeLock.OnDeployed(door);
                        codeLock.Spawn();

                        door.SetSlot(BaseEntity.Slot.CenterDecoration, codeLock);

                        var codeLockLock = codeLock as CodeLock;

                        if (codeLockLock == null) return;

                        codeLockLock.code = playerData.CodeLockCode;
                        codeLockLock.SetFlag(BaseEntity.Flags.Locked, true);
                        codeLockLock.SendNetworkUpdate();
                        SendMessageToPlayer(player, LangAccessor.OnDoorPlace, playerData.CodeLockCode.ToString());
                        RemoveCodeLockFromInventoy(player);
                    }
                }
            }
        }

        #endregion Oxide Hooks

        #region Commands


        [ChatCommand("codelocks")]
        private void MainCommand(BasePlayer player, string command, string[] args)
        {

            var playerData = GetPlayerFromPlayerDatas(player);

            if (!(DoesPlayerHavePermission(player, PermissionUser)))
            {
                SendMessageToPlayer(player, LangAccessor.LackPermissions);
                return;
            }

            if (args.Length == 0)
            {
                SendMessageToPlayer(player, LangAccessor.FeaturesStatus,
                    playerData.CodeLockCode.ToString(),
                    (playerData.IsAutoCodeLockActive) ? LangAccessor.True : LangAccessor.False);
                
                return;
            }

            if (args.Length > 0)
            {
                switch (args[0].ToLower())
                {
                    case "lock":
                        playerData.IsAutoCodeLockActive = true;
                        SendMessageToPlayer(player, LangAccessor.OnCodeLockUpdate, LangAccessor.Active);
                        return;
                    case "unlock":
                        playerData.IsAutoCodeLockActive = false;
                        SendMessageToPlayer(player, LangAccessor.OnCodeLockUpdate, LangAccessor.InActive);
                        return;

                    case "pin":
                        if (args.Length != 2)
                        {
                            SendMessageToPlayer(player, LangAccessor.IncorrectArgumentsLength);
                            return;
                        }
                        int pinNumber;
                        var isInteger = int.TryParse(args[1], out pinNumber);

                        if (!isInteger || pinNumber > 9999)
                        {
                            SendMessageToPlayer(player, LangAccessor.IncorrentArgumentFormat);
                            return;
                        }

                        playerData.CodeLockCode = pinNumber.ToString();
                        
                        SendMessageToPlayer(player, LangAccessor.OnPinUpdate, pinNumber.ToString());
                        return;

                    default:
                        SendMessageToPlayer(player, LangAccessor.FeaturesStatus,
                        playerData.CodeLockCode.ToString(),
                        (playerData.IsAutoCodeLockActive) ? LangAccessor.True : LangAccessor.False);

                        return;
                        
                }
            }


        }


        #endregion
    }
}
