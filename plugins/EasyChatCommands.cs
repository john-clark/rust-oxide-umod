using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Network;

namespace Oxide.Plugins
{
    [Info("Easy Chat Commands", "Jake", "0.1.0")]
    [Description("Custom Info Commands")]

    class EasyChatCommands : RustPlugin
    {
        public Dictionary<string, string> chatCommands { get; set; }
        private const string configFileName = "EasyChatCommands";


        void Loaded()
        {
            //Load chat commands
            if (!Oxide.Core.Interface.Oxide.DataFileSystem.ExistsDatafile(configFileName))
            {
                chatCommands = new Dictionary<string, string>();
                chatCommands.Add("exampleinfocommand", "Example Reply, You need a comma here -->");
                chatCommands.Add("exampleinfocommand2", "No comma on the last item -->");
                Core.Interface.Oxide.DataFileSystem.WriteObject(configFileName, chatCommands);
                Puts($"WARNING: Plugin first loaded, edit {configFileName}.json with your chat commands!");
            }
            else
            {
                try
                {
                    chatCommands = Core.Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string,string>>(configFileName);
                }
                catch (Exception ex)
                {
                    Puts("ERROR: Config file is formatted incorrectly! EasyChatCommands are NOT LOADED!!!");
                    Puts("Exeption:");
                    Puts(ex.ToString());
                }
            }
            foreach(string command in chatCommands.Keys)
            {
                cmd.AddChatCommand(command, this, "OnChatInfoCommand");
            }
            Puts("Loaded Chat Commands");
        }

        void Unload()
        {

        }

        void OnChatInfoCommand(BasePlayer player, string command)
        {
            string reply;
            if (chatCommands.TryGetValue(command, out reply))
            {
                PrintToChat(player, reply);
            }
        }

    }
}
