using System;
using System.Collections.Generic;
using System.Text;

using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("HelpText", "Calytic/Domestos", "2.0.5", ResourceId = 676)]
    class HelpText : CovalencePlugin
    {
        private bool UseCustomHelpText;
        private bool AllowHelpTextFromOtherPlugins;
        //private List<object> CustomHelpText;
        private int BreakAfter;
        private Dictionary<string, Dictionary<string, object>> Pages = new Dictionary<string,Dictionary<string,object>>();

        private void Loaded()
        {
            CheckConfig();
            UseCustomHelpText = GetConfig("Settings", "UseCustomHelpText", false);
            AllowHelpTextFromOtherPlugins = GetConfig("Settings", "AllowHelpTextFromOtherPlugins", true);
            Pages.Add("default", GetConfig("Pages", "default", GetDefaultPages()));
            foreach(var group in permission.GetGroups()) {
                if(group != "default" && Config["Pages", group] != null) {
                    Pages.Add(group, GetConfig("Pages", group, GetEmptyPages()));
                }
            }

            BreakAfter = GetConfig("Settings", "BreakAfter", 10);
        }

        protected override void LoadDefaultConfig()
        {
            Config["Settings", "UseCustomHelpText"] = false;
            Config["Settings", "AllowHelpTextFromOtherPlugins"] = true;
            Config["Pages", "default"] = GetDefaultPages();

            Config["Settings", "BreakAfter"] = BreakAfter = 10;
            
            Config["VERSION"] = Version.ToString();
            SaveConfig();
        }

        protected Dictionary<string, object> GetDefaultPages()
        {
            return new Dictionary<string, object>() {{
                    "*", new List<object>() {
                        "custom helptext",
                    }
                },{
                    "mypage", new List<object>() {
                        "custom page helptext",
                }}
            };
        }

        protected Dictionary<string, object> GetEmptyPages()
        {
            return new Dictionary<string, object>();
        }

        [Command("help")]
        void cmdHelp(IPlayer player, string command, string[] args)
        {
            if (player == null) return;

            if (UseCustomHelpText)
            {
                StringBuilder sb = new StringBuilder();
                int i = 0;
                var page = "*";
                if(args != null && args.Length == 1) {
                    page = args[0];
                }

                foreach(KeyValuePair<string, Dictionary<string, object>> kvp in Pages) {
                    
                    var group = kvp.Key;
                    if (player.BelongsToGroup(group))
                    {
                        var pages = kvp.Value;
                        object currentPage;
                        if (pages.TryGetValue(page, out currentPage))
                        {
                            if (currentPage is List<object>)
                            {
                                foreach (var text in currentPage as List<object>)
                                {
                                    sb.AppendLine(text.ToString());
                                    i++;

                                    if (i % BreakAfter == 0)
                                    {
                                        player.Reply(sb.ToString());
                                        sb.Length = 0;
                                        i = 0;
                                    }
                                }
                            }
                            else if (currentPage is string)
                            {
                                sb.AppendLine(currentPage.ToString());
                                i++;

                                if (i % BreakAfter == 0)
                                {
                                    player.Reply(sb.ToString());
                                    sb.Length = 0;
                                    i = 0;
                                }
                            }
                        }
                        else if (page != "*")
                        {
                            player.Reply("No help page named '" + page + "' exists.");
                        }
                    }
                }
                

                if (i > 0)
                {
                    player.Reply(sb.ToString());
                }
            }

            if (AllowHelpTextFromOtherPlugins)
            {
                plugins.CallHook("SendHelpText", player.Object);
            }
        }

        void CheckConfig()
        {
            if (Config["VERSION"] == null)
            {
                // FOR COMPATIBILITY WITH INITIAL VERSIONS WITHOUT VERSIONED CONFIG
                ReloadConfig();
            }
            else if (GetConfig<string>("VERSION", "") != Version.ToString())
            {
                // ADDS NEW, IF ANY, CONFIGURATION OPTIONS
                ReloadConfig();
            }
        }

        protected void ReloadConfig()
        {
            Config["VERSION"] = Version.ToString();

            // NEW CONFIGURATION OPTIONS HERE
            Config["Settings","UseCustomHelpText"] = GetConfig("Settings", "UseCustomHelpText", false);
            Config["Settings", "AllowHelpTextFromOtherPlugins"] = GetConfig("Settings", "AllowHelpTextFromOtherPlugins", true);
            if (Config["Pages", "default", "*"] == null)
            {
                Config["Pages", "default", "*"] = GetConfig("Settings", "CustomHelpText", new List<object>() {
                    "custom helptext",
                    "custom helptext"
                });
            }
            Config["Settings", "BreakAfter"] = GetConfig("Settings", "BreakAfter", 10);
            // END NEW CONFIGURATION OPTIONS
            PrintWarning("Upgrading configuration file");
            SaveConfig();
        }

        private T GetConfig<T>(string name, T defaultValue)
        {
            if (Config[name] == null)
            {
                return defaultValue;
            }

            return (T)Convert.ChangeType(Config[name], typeof(T));
        }

        private T GetConfig<T>(string name, string name2, T defaultValue)
        {
            if (Config[name, name2] == null)
            {
                return defaultValue;
            }

            return (T)Convert.ChangeType(Config[name, name2], typeof(T));
        }
    }
}