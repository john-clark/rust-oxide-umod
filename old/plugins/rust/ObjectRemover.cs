using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Game.Rust.Libraries;
using UnityEngine;
using Time = UnityEngine.Time;
// ReSharper disable UnusedMember.Local

namespace Oxide.Plugins
{
    [Info("Object Remover", "Iv Misticos", "3.0.2")]
    [Description("Removes furnaces, lanterns, campfires, buildings etc. on command")]
    class ObjectRemover : RustPlugin
    {
        #region Variables
        
        private const string ShortnameCupboard = "cupboard.tool";

        #endregion

        #region Configuration

        private Configuration _config = new Configuration();

        public class Configuration
        {
            [JsonProperty(PropertyName = "Object Command Permission")]
            public string PermissionUse = "objectremover.use";
            
            [JsonProperty(PropertyName = "Object Command")]
            public string Command = "object";
            
            public string Prefix = "[<color=#ffbf00> Object Remover </color>] ";
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
            }
            catch
            {
                Config.WriteObject(_config, false, $"{Interface.Oxide.ConfigDirectory}/{Name}.jsonError");
                PrintError("The configuration file contains an error and has been replaced with a default config.\n" +
                           "The error configuration file was saved in the .jsonError extension");
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig() => _config = new Configuration();

        protected override void SaveConfig() => Config.WriteObject(_config);

        #endregion
        
        #region Hooks

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "No Rights", "You do not have enough rights" },
                { "Count", "We found {count} entities in {time}s." },
                { "Removed", "You have removed {count} entities in {time}s." },
                { "Help", "Object command usage:\n" +
                          "/object (entity) [parameters]\n" +
                          "Parameters:\n" +
                          "action count/remove - Count or remove entities\n" +
                          "radius NUM - Radius\n" +
                          "inside true/false - Entities inside the cupboard\n" +
                          "outside true/false - Entities outside the cupboard" },
                { "No Console", "Please log in as a player to use that command" }
            }, this);
        }

        private void OnServerInitialized()
        {
            LoadDefaultMessages();
            LoadConfig();
            
            if (!permission.PermissionExists(_config.PermissionUse))
                permission.RegisterPermission(_config.PermissionUse, this);

            var cmdLib = GetLibrary<Command>();
            cmdLib.AddChatCommand(_config.Command, this, CommandChatObject);
            cmdLib.AddConsoleCommand(_config.Command, this, CommandConsoleObject);
        }
        
        #endregion

        #region Commands

        private void CommandChatObject(BasePlayer player, string command, string[] args)
        {
            var id = player.UserIDString;
            if (!permission.UserHasPermission(id, _config.PermissionUse))
            {
                player.ChatMessage(_config.Prefix + GetMsg("No Rights", id));
                return;
            }

            if (args.Length < 1)
            {
                player.ChatMessage(_config.Prefix + GetMsg("Help", id));
                return;
            }

            var options = new RemoveOptions();
            options.Parse(args);
            
            var entity = args[0];
            var before = Time.realtimeSinceStartup;
            var objects = FindObjects(player.transform.position, entity, options);
            var count = objects.Count;

            if (options.Count)
            {
                player.ChatMessage(_config.Prefix + GetMsg("Count", id).Replace("{count}", count.ToString()).Replace("{time}", (Time.realtimeSinceStartup - before).ToString("0.###")));
            }
            else
            {
                for (var i = 0; i < count; i++)
                {
                    var ent = objects[i];
                    if (ent == null || ent.IsDestroyed)
                        continue;
                    ent.Kill();
                }
                
                player.ChatMessage(_config.Prefix + GetMsg("Removed").Replace("{count}", count.ToString()).Replace("{time}", (Time.realtimeSinceStartup - before).ToString("0.###")));
            }
        }
        
        private bool CommandConsoleObject(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null)
            {
                arg.ReplyWith(GetMsg("No Console"));
                return true;
            }
            
            CommandChatObject(player, string.Empty, arg.Args ?? new string[0]);
            return false;
        }

        #endregion
        
        #region Remove Options

        private class RemoveOptions
        {
            public float Radius = 10f;
            public bool Count = true;
            public bool OutsideCupboard = true;
            public bool InsideCupboard = true;

            public void Parse(string[] args)
            {
                for (var i = 1; i + 1 < args.Length; i += 2)
                {
                    switch (args[i])
                    {
                        case "a":
                        case "action":
                        {
                            Count = args[i + 1] != "remove";

                            break;
                        }
                        
                        case "r":
                        case "radius":
                        {
                            if (!float.TryParse(args[i + 1], out Radius))
                                Radius = 10f;

                            break;
                        }

                        case "oc":
                        case "outside":
                        {
                            if (!bool.TryParse(args[i + 1], out OutsideCupboard))
                                OutsideCupboard = true;
                            
                            break;
                        }

                        case "ic":
                        case "inside":
                        {
                            if (!bool.TryParse(args[i + 1], out InsideCupboard))
                                InsideCupboard = true;

                            break;
                        }
                    }
                }
            }
        }
        
        #endregion

        #region Helpers

        private List<BaseEntity> FindObjects(Vector3 startPos, string entity, RemoveOptions options)
        {
            var entities = new List<BaseEntity>();
            if (options.Radius > 0)
            {
                var entitiesList = new List<BaseEntity>();
                Vis.Entities(startPos, options.Radius, entitiesList);
                var entitiesCount = entitiesList.Count;
                for (var i = entitiesCount - 1; i >= 0; i--)
                {
                    var ent = entitiesList[i];

                    if (IsNeededObject(ent, entity, options))
                        entities.Add(ent);
                }
            }
            else
            {
                var ents = UnityEngine.Object.FindObjectsOfType<BaseEntity>();
                var entsCount = ents.Length;
                for (var i = 0; i < entsCount; i++)
                {
                    var ent = ents[i];
                    if (IsNeededObject(ent, entity, options))
                        entities.Add(ent);
                }
            }

            return entities;
        }

        private bool IsNeededObject(BaseEntity entity, string shortname, RemoveOptions options)
        {
            var isAll = shortname.Equals("all");
            return (isAll || entity.ShortPrefabName.IndexOf(shortname, StringComparison.CurrentCultureIgnoreCase) != -1) &&
                   (options.InsideCupboard && entity.GetBuildingPrivilege() != null ||
                    options.OutsideCupboard && entity.GetBuildingPrivilege() == null);
        }

        private string GetMsg(string key, string userId = null) => lang.GetMessage(key, this, userId);

        #endregion
    }
}