using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using UnityEngine;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Knock Knock", "MisterPixie", "1.1.12")]
    [Description("Allows users to set messages on doors when someone knocks")]
    class KnockKnock : RustPlugin
    {
        public string KnockPerm = "knockknock.allow";
        public string KnockAutoPerm = "knockknock.auto.allow";
        List<string> doors = new List<string>()
        {
            "door.double.hinged.metal",
            "door.double.hinged.toptier",
            "door.double.hinged.wood",
            "door.hinged.metal",
            "door.hinged.toptier",
            "door.hinged.wood",
            "wall.frame.garagedoor"
        };

        #region Data
        Dictionary<ulong, KnockData> knockData = new Dictionary<ulong, KnockData>();

        private class KnockData
        {
            public bool EnableAutoMessages { get; set; }
            public string AutoMessage { get; set; }
            public Dictionary<int, KnockMessages> knockMessages { get; set; }
        }

        private class KnockMessages
        {
            public string Message { get; set; }
        }

        void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("KnockData", knockData);
        }

        void OnServerSave()
        {
            SaveData();
        }

        #endregion

        #region Lang

        private string Lang(string key, string id = null, params object[] args) => configData.prefixs.UsePerfix ? string.Format(configData.prefixs.Prefix + lang.GetMessage(key, this, id), args) : string.Format(lang.GetMessage(key, this, id), args);

        private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPermission"] = "You don't have permissions to run this command.",
                ["NoDoor"] = "Make sure you are looking at a door.",
                ["DontOwnDoor"] = "You don't own this door",
                ["MessageAdded"] = "New Message added to Door\n{0}",
                ["RemovedMessage"] = "Door Message Removed.",
                ["DoorMessage"] = "{0}",
                ["NoMessageOnDoor"] = "The door does not have a message set.",
                ["Commands"] = "List of Commands:\n/{0} add (Message) - Adds Message to your door\n/{0} remove - Removes door's message",
                ["PermCommands"] = "List of Commands:\n/{0} add (Message) - Adds Message to your door\n/{0} remove - Removes door's message\n/{0} auto - Turns on auto messages\n/{0} auto (Message) - Set's a message for auto message",
                ["AddCommand"] = "Incorrect Syntax - /{0} add (Message)",
                ["ToggleAutoOn"] = "Auto messages have been turned On",
                ["ToggleAutoOff"] = "Auto messages have been turned Off",
                ["AutoMessageSet"] = "Your Auto message has been set\n{0}"


            }, this);
        }
        #endregion

        private void Init()
        {
            LoadVariables();
            cmd.AddChatCommand(configData.Command, this, "KnockCommand");
            permission.RegisterPermission(KnockPerm, this);
            permission.RegisterPermission(KnockAutoPerm, this);
            knockData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, KnockData>>("KnockData");
            if (!configData.EnableAuto)
            {
                Unsubscribe("OnEntityBuilt");
            }
        }

        void Unload()
        {
            SaveData();
        }

        void OnNewSave()
        {
            foreach(var i in knockData)
            {
                i.Value.knockMessages.Clear();
            }
        }

        void OnEntityBuilt(Planner plan, GameObject go)
        {
            var player = plan.GetOwnerPlayer();

            if (player == null)
                return;

            if (!permission.UserHasPermission(player.UserIDString, KnockPerm) || !permission.UserHasPermission(player.UserIDString, KnockAutoPerm))
                return;

            if (plan.GetDeployable() == null || !doors.Contains(plan.GetDeployable().hierachyName))
                return;

            KnockData value;

            if (!knockData.TryGetValue(player.userID, out value))
            {
                knockData.Add(player.userID, new KnockData()
                {
                    AutoMessage = string.Empty,
                    EnableAutoMessages = false,
                    knockMessages = new Dictionary<int, KnockMessages>()
                });
            }
            else
            {
                if (value.EnableAutoMessages)
                {
                    RaycastHit rhit;
                    if (!Physics.Raycast(player.eyes.HeadRay(), out rhit))
                    {
                        return;
                    }

                    var entity = rhit.GetEntity();

                    value.knockMessages.Add(entity.GetInstanceID(), new KnockMessages()
                    {
                        Message = value.AutoMessage
                    });
                }
            }
            
        }

        private void KnockCommand(BasePlayer player, string command, string[] args)
        {
            if(configData.permissions.UsePermission)
            {
                if (!permission.UserHasPermission(player.UserIDString, KnockPerm))
                {
                    SendReply(player, Lang("NoPermission", player.UserIDString));
                    return;
                }
            }

            KnockData knockvalue;

            if (!knockData.TryGetValue(player.userID, out knockvalue))
            {
                knockData.Add(player.userID, new KnockData()
                {
                    AutoMessage = "",
                    EnableAutoMessages = false,
                    knockMessages = new Dictionary<int, KnockMessages>()
                });
            }

            if (args.Length == 0)
            {
                if (configData.EnableAuto)
                {
                    if (permission.UserHasPermission(player.UserIDString, KnockAutoPerm))
                    {
                        SendReply(player, Lang("PermCommands", player.UserIDString, configData.Command));
                    }
                    else
                    {
                        SendReply(player, Lang("Commands", player.UserIDString, configData.Command));
                    }
                }
                else
                {
                    SendReply(player, Lang("Commands", player.UserIDString, configData.Command));
                }
            }
            else
            {
                if (configData.EnableAuto)
                {
                    if (args[0].ToLower() == "auto")
                    {
                        KnockAuto(player, args, knockvalue);
                        return;
                    }
                }

                var id = GetRaycast(player);

                if (id == null)
                    return;

                switch (args[0].ToLower())
                {
                    case "add":
                        KnockAdd(player, args, id.GetInstanceID(), knockvalue);
                        break;
                    case "remove":
                        KnockRemove(player, args, id.GetInstanceID(), knockvalue);
                        break;
                    default:
                        if (configData.EnableAuto)
                        {
                            if (permission.UserHasPermission(player.UserIDString, KnockAutoPerm))
                            {
                                SendReply(player, Lang("PermCommands", player.UserIDString, configData.Command));
                            }
                            else
                            {
                                SendReply(player, Lang("Commands", player.UserIDString, configData.Command));
                            }
                        }
                        else
                        {
                            SendReply(player, Lang("Commands", player.UserIDString, configData.Command));
                        }
                        break;
                }
            }
        }

        private void OnDoorKnocked(Door door, BasePlayer player)
        {
            KnockData knockvalue;
            KnockMessages knockmsgvalue;

            if (!knockData.TryGetValue(door.OwnerID, out knockvalue) || !knockvalue.knockMessages.TryGetValue(door.GetInstanceID(), out knockmsgvalue))
            {
                return;
            }

            SendReply(player, Lang("DoorMessage", player.UserIDString, knockmsgvalue.Message));
        }

        private void KnockAdd(BasePlayer player, string[] args, int id, KnockData value)
        {
            if (args.Length <= 1)
            {
                SendReply(player, Lang("AddCommand", player.UserIDString, configData.Command));
            }
            else
            {
                KnockMessages knockmsgvalue;

                string message = string.Join(" ", args.Skip(1).ToArray());
                if (!value.knockMessages.TryGetValue(id, out knockmsgvalue))
                {

                    value.knockMessages.Add(id, new KnockMessages()
                    {
                        Message = message
                    });

                    SendReply(player, Lang("MessageAdded", player.UserIDString, message));
                }
                else
                {
                    knockmsgvalue.Message = message;
                    SendReply(player, Lang("MessageAdded", player.UserIDString, message));
                }
            }
        }

        private void KnockRemove(BasePlayer player, string[] args, int id, KnockData value)
        {
            if (value.knockMessages.ContainsKey(id))
            {
                value.knockMessages.Remove(id);
                SendReply(player, Lang("RemovedMessage", player.UserIDString));
            }
            else
            {
                SendReply(player, Lang("NoMessageOnDoor", player.UserIDString));
            }
        }

        private void KnockAuto(BasePlayer player, string[] args, KnockData value)
        {
            if(!permission.UserHasPermission(player.UserIDString, KnockAutoPerm))
            {
                SendReply(player, Lang("NoPermission", player.UserIDString));
                return;
            }

            if (args.Length <= 1)
            {
                if (value.EnableAutoMessages)
                {
                    value.EnableAutoMessages = false;
                    SendReply(player, Lang("ToggleAutoOff", player.UserIDString));
                }
                else
                {
                    value.EnableAutoMessages = true;
                    SendReply(player, Lang("ToggleAutoOn", player.UserIDString));
                }
            }
            else
            {
                string message = string.Join(" ", args.Skip(1).ToArray());
                value.AutoMessage = message;
                SendReply(player, Lang("AutoMessageSet", player.UserIDString, message));
            }
        }

        private BaseEntity GetRaycast(BasePlayer player)
        {
            RaycastHit rhit;
            if (!Physics.Raycast(player.eyes.HeadRay(), out rhit))
            {
                return null;
            }

            var entity = rhit.GetEntity();

            if (entity == null || !doors.Contains(entity.ShortPrefabName))
            {
                SendReply(player, Lang("NoDoor", player.UserIDString));
                return null;
            }

            if (entity.OwnerID != player.userID)
            {
                SendReply(player, Lang("DontOwnDoor", player.UserIDString));
                return null;
            }

            return entity;
        }

        #region Config

        private class Permissions
        {
            [JsonProperty(PropertyName = "Permission to use Auto Message Function")]
            public bool UseAutoPerm;

            [JsonProperty(PropertyName = "Permission to use Main Plugin Functions")]
            public bool UsePermission;
        }

        private class Prefixs
        {
            [JsonProperty(PropertyName = "Message Prefix")]
            public string Prefix;

            [JsonProperty(PropertyName = "Use Message Prefix")]
            public bool UsePerfix;
        }


        private ConfigData configData;
        private class ConfigData
        {
            [JsonProperty(PropertyName = "Permission Settings")]
            public Permissions permissions;

            [JsonProperty(PropertyName = "Prefix Settings")]
            public Prefixs prefixs;

            [JsonProperty(PropertyName = "Enable Auto Function")]
            public bool EnableAuto;

            public string Command;
        }

        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                permissions = new Permissions
                {
                    UseAutoPerm = true,
                    UsePermission = true,
                },
                prefixs = new Prefixs
                {
                    Prefix = "<color=#cf4d4d>KnockKnock: </color>",
                    UsePerfix = false
                },
                EnableAuto = true,
                Command = "knock",
            };
            SaveConfig(config);
        }
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion
    }
}
