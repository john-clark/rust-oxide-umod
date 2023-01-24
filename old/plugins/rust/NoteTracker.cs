using System;
using System.Collections.Generic;
using System.Linq;
using Facepunch.Extend;
using Newtonsoft.Json;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("NoteTracker", "PsychoTea", "1.0.2")]

    class NoteTracker : RustPlugin
    {
        const string permAdmin = "notetracker.admin";

        class NoteInfo
        {
            public string Text;
            public string DisplayName;
            public ulong UserID;
            public uint NoteID;
            public DateTime TimeStamp;

            public NoteInfo() { }

            public NoteInfo(BasePlayer player, string Text, uint NoteID)
            {
                this.Text = Text;
                this.DisplayName = player.displayName;
                this.UserID = player.userID;
                this.NoteID = NoteID;
                this.TimeStamp = DateTime.UtcNow;
            }
        }

        List<NoteInfo> NoteList = new List<NoteInfo>();

        #region Oxide Hooks

        void Init()
        {
            permission.RegisterPermission(permAdmin, this);

            lang.RegisterMessages(new Dictionary<string, string>()
            {
                { "NoPermission", "You don't have permission to use this." },
                { "NoteInfo-Tp-Usage", "Incorrect usage! /noteinfo tp {{UID}}" },
                { "NoNoteFound", "Please hold the note in the first slot of your hotbar :)" },
                { "NoteInfoTitle", "Info for note {NoteID}:" },
                { "NoteInfoItem", "[{TimeStamp}] {Name} ({UserID}): {Text}" },
                { "NoteInfoNone", "No updates to show." },
                { "CouldntFindNote", "The note #{UID} could not be found." },
                { "TeleportedToNote", "You have been teleported to note #{UID}." },
                { "ConsoleLogMessage", "[{TimeStamp}] {Name} ({UserID}) ID {UID}: {Text}" }
            }, this);

            ReadData();
        }

        void OnServerSave()
        {
            SaveData();
        }

        #endregion

        #region Config

        ConfigFile config;

        class ConfigFile
        {
            [JsonProperty(PropertyName = "Log Notes To Console")]
            public bool LogToConsole;

            public static ConfigFile DefaultConfig()
            {
                return new ConfigFile
                {
                    LogToConsole = true
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<ConfigFile>();
        }

        protected override void LoadDefaultConfig() => config = ConfigFile.DefaultConfig();

        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion

        #region Commands

        [ChatCommand("noteinfo")]
        void NoteInfoCommand(BasePlayer player, string command, string[] args)
        {
            if (!HasPerm(player))
            {
                SendReply(player, GetMessage("NoPermission", player));
                return;
            }

            if (args != null && args.Length > 1 && args[0].ToLower() == "tp")
            {
                uint noteID;
                if (!uint.TryParse(args[1], out noteID))
                {
                    SendReply(player, GetMessage("NoteInfo-Tp-Usage", player));
                    return;
                }

                var items = UnityEngine.Object.FindObjectsOfType(typeof(StorageContainer))?.Cast<StorageContainer>().Select(x => x.inventory.FindItemByUID(noteID)).Where(x => x != null);
                items.Concat(UnityEngine.Object.FindObjectsOfType(typeof(PlayerInventory))?.Cast<PlayerInventory>().Select(x => x.FindItemUID(noteID)).Where(x => x != null));
                if (items.Count() == 0)
                {
                    SendReply(player, GetMessage("CouldntFindNote", player).Replace("{UID}", noteID.ToString()));
                    return;
                }
                var item = items.First();

                var container = item.GetRootContainer();
                Teleport(player, container.dropPosition);
                SendReply(player, GetMessage("TeleportedToNote", player).Replace("{UID}", noteID.ToString()));
                return;
            }

            var slotZero = player.inventory.containerBelt.GetSlot(0);
            
            if (slotZero == null || slotZero.info.displayName.english != "Note")
            {
                SendReply(player, GetMessage("NoNoteFound", player));
                return;
            }

            SendReply(player, GetMessage("NoteInfoTitle", player).Replace("{NoteID}", slotZero.uid.ToString()));
            var updateList = NoteList.Where(x => x.NoteID == slotZero.uid).OrderBy(x => x.TimeStamp).ToList();
            foreach (var update in updateList)
            {
                SendReply(player, GetMessage("NoteInfoItem", player)
                                    .Replace("{TimeStamp}", update.TimeStamp.ToString())
                                    .Replace("{Name}", update.DisplayName)
                                    .Replace("{UserID}", update.UserID.ToString())
                                    .Replace("{Text}", update.Text.TrimEnd('\n')));
            }
            if (updateList.Count == 0) SendReply(player, GetMessage("NoteInfoNone", player));
        }

        [ConsoleCommand("note.update")]
        void NoteUpdateCommand(ConsoleSystem.Arg arg)
        {
            uint num = arg.GetUInt(0, 0);
            string str = arg.GetString(1, string.Empty);
            Item item = arg.Player().inventory.FindItemUID(num);
            if (item == null) return;
            item.text = str.Truncate(1024, null);
            item.MarkDirty();
            NoteList.Add(new NoteInfo(arg.Player(), item.text, item.uid));
            LogToConsole(arg.Player(), item.text, item.uid);
        }

        #endregion

        #region Functions

        void LogToConsole(BasePlayer player, string text, uint uid)
        {
            if (!config.LogToConsole) return;
            Puts(GetMessage("ConsoleLogMessage", null)
                    .Replace("{TimeStamp}", DateTime.UtcNow.ToString())
                    .Replace("{Name}", player.displayName)
                    .Replace("{UserID}", player.UserIDString)
                    .Replace("{UID}", uid.ToString())
                    .Replace("{Text}", text.TrimEnd('\n')));
        }

        void Teleport(BasePlayer player, Vector3 position)
        {
            if (player.net?.connection != null)
                player.ClientRPCPlayer(null, player, "StartLoading");
            StartSleeping(player);
            player.MovePosition(position);
            if (player.net?.connection != null)
                player.ClientRPCPlayer(null, player, "ForcePositionTo", position);
            if (player.net?.connection != null)
                player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);
            player.UpdateNetworkGroup();
            player.SendNetworkUpdateImmediate(false);
            if (player.net?.connection == null) return;
            try { player.ClearEntityQueue(null); } catch { }
            player.SendFullSnapshot();
        }

        void StartSleeping(BasePlayer player)
        {
            if (player.IsSleeping())
                return;
            player.SetPlayerFlag(BasePlayer.PlayerFlags.Sleeping, true);
            if (!BasePlayer.sleepingPlayerList.Contains(player))
                BasePlayer.sleepingPlayerList.Add(player);
            player.CancelInvoke("InventoryUpdate");
        }

        #endregion

        #region Helpers

        string GetMessage(string key, BasePlayer player) => lang.GetMessage(key, this, player?.UserIDString);

        bool HasPerm(BasePlayer player) => (player.IsAdmin || permission.UserHasPermission(player.UserIDString, permAdmin));

        void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(this.Title, NoteList);
        void ReadData() => NoteList = Interface.Oxide.DataFileSystem.ReadObject<List<NoteInfo>>(this.Title);

        #endregion
    }
}