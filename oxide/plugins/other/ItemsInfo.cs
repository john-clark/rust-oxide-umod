using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Items Info", "Iv Misticos", "1.0.3")]
    [Description("Get actual information about items.")]
    class ItemsInfo : RustPlugin
    {
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "Incorrect Arguments", "Please, specify correct arguments." }
            }, this);
        }

        private void Init()
        {
            cmd.AddConsoleCommand("itemsinfo.all", this, CommandConsoleHandle);
            cmd.AddConsoleCommand("itemsinfo.find", this, CommandConsoleHandle);
        }

        private bool CommandConsoleHandle(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin)
                return false;
            
            arg.ReplyWith(GetItemsInfo(arg.Args, arg.cmd.FullName == "itemsinfo.find", arg.Player()?.UserIDString));
            return true;
        }

        private string GetItemsInfo(IReadOnlyList<string> parameters, bool search, string id)
        {
            if (parameters == null || (search && parameters.Count < 2) || parameters.Count < 1)
                return GetMsg("Incorrect Arguments", id);
            
            var reply = new StringBuilder();
            var items = ItemManager.itemList;
            var itemsCount = items.Count;

            var found = 0;
            for (var i = 0; i < itemsCount; i++)
            {
                var item = items[i];
                if (search && item.shortname.IndexOf(parameters[0], StringComparison.CurrentCultureIgnoreCase) == -1)
                    continue;

                for (var j = search ? 1 : 0; j < parameters.Count; j++)
                {
                    switch (parameters[j])
                    {
                        case "number":
                        {
                            reply.Append($"#{++found}\n");
                            break;
                        }
                        
                        case "shortname":
                        {
                            reply.Append($"Shortname: {item.shortname}\n");
                            break;
                        }

                        case "id":
                        {
                            reply.Append($"ID: {item.itemid}\n");
                            break;
                        }

                        case "name":
                        {
                            reply.Append($"Name: {item.displayName.english}\n");
                            break;
                        }

                        case "description":
                        {
                            reply.Append($"Description: {item.displayDescription.english}\n");
                            break;
                        }

                        case "condition":
                        {
                            reply.Append($"Max Condition: {item.condition.max}\n");
                            break;
                        }

                        case "repair":
                        {
                            reply.Append($"Is Repairable: {item.condition.repairable}\n");
                            break;
                        }
                    }
                }
            }

            return reply.ToString();
        }

        private string GetMsg(string key, string userId = null) => lang.GetMessage(key, this, userId);
    }
}