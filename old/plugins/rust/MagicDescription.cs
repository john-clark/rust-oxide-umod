using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Oxide.Plugins
{
    [Info("Magic Description", "Wulf/lukespragg", "1.4.0")]
    [Description("Adds dynamic information in the server description")]
    public class MagicDescription : RustPlugin
    {
        #region Configuration

        private Configuration config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Server description")]
            public string Description { get; set; } = "Powered by Oxide {magic.version} for Rust {magic.version protocol}";

            [JsonProperty(PropertyName = "Update interval (seconds)")]
            public int UpdateInterval { get; set; } = 300;

            [JsonProperty(PropertyName = "Show loaded plugins (true/false)")]
            public bool ShowPlugins { get; set; } = false;

            [JsonProperty(PropertyName = "Hidden plugins (filename or title)")]
            public List<string> HiddenPlugins { get; set; } = new List<string>();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            string configPath = $"{Interface.Oxide.ConfigDirectory}{Path.DirectorySeparatorChar}{Name}.json";
            PrintWarning($"Could not load a valid configuration file, creating a new configuration file at {configPath}");
            config = new Configuration();
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion Configuration

        #region Initialization

        private static readonly Regex varRegex = new Regex(@"\{(.*?)\}");
        private static bool serverInitialized;

        private void OnServerInitialized()
        {
            serverInitialized = true;

            UpdateDescription();
            timer.Every(config.UpdateInterval, () => UpdateDescription());

            if (!config.ShowPlugins)
            {
                Unsubscribe(nameof(OnPluginLoaded));
                Unsubscribe(nameof(OnPluginUnloaded));
            }
        }

        private void OnServerSave() => SaveConfig();

        #endregion Initialization

        #region Description Handling

        private string UpdateDescription(string text = "")
        {
            if (!string.IsNullOrEmpty(text))
            {
                config.Description = text;
            }

            StringBuilder newDescription = new StringBuilder(config.Description);

            foreach (Match match in varRegex.Matches(config.Description))
            {
                string command = match.Groups[1].Value;

                if (!string.IsNullOrEmpty(command))
                {
                    string reply = ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), command);
                    newDescription.Replace(match.ToString(), reply.Replace("\"", "") ?? "");
                }
            }

            if (config.ShowPlugins)
            {
                Plugin[] loadedPlugins = plugins.GetAll();

                if (loadedPlugins.Length != 0)
                {
                    int count = 0;
                    string pluginList = null;

                    foreach (Plugin plugin in loadedPlugins.Where(p => !p.IsCorePlugin))
                    {
                        if (!config.HiddenPlugins.Contains(plugin.Title) && !config.HiddenPlugins.Contains(plugin.Name))
                        {
                            pluginList += plugin.Title + ", ";
                            count++;
                        }
                    }
                    if (pluginList != null)
                    {
                        if (pluginList.EndsWith(", "))
                        {
                            pluginList = pluginList.Remove(pluginList.Length - 2);
                        }
                        newDescription.Append($"\n\nPlugins ({count}): {pluginList}");
                    }
                }
            }

            if (newDescription.ToString() != ConVar.Server.description)
            {
                ConVar.Server.description = newDescription.ToString();
                Puts($"Server description updated: \nmagic.description: \"{config.Description}\"");
            }

            return ConVar.Server.description;
        }

        private void OnPluginLoaded(Plugin plugin)
        {
            if (serverInitialized)
            {
                UpdateDescription();
            }
        }

        private void OnPluginUnloaded(Plugin plugin)
        {
            if (serverInitialized)
            {
                UpdateDescription();
            }
        }

        #endregion Description Handling

        #region Command Handling

        private object OnServerCommand(ConsoleSystem.Arg arg)
        {
            if (!serverInitialized || !arg.IsAdmin || arg.cmd.FullName != "server.description")
            {
                return null;
            }

            if (!arg.HasArgs() || arg.Args.GetValue(0) == null)
            {
                return null;
            }

            string magicDescription = string.Join(" ", arg.Args.ToArray());
            arg.ReplyWith($"server.description: \"{UpdateDescription(magicDescription)}\"");

            return true;
        }

        [ConsoleCommand("magic.description")]
        private void DescriptionCommand(ConsoleSystem.Arg arg)
        {
            if (arg.IsAdmin)
            {
                arg.ReplyWith($"magic.description: \"{config.Description}\"");
            }
        }

        [ConsoleCommand("magic.version")]
        private void VersionCommand(ConsoleSystem.Arg arg)
        {
            if (arg.IsAdmin)
            {
                switch (arg.FullString.ToLower())
                {
                    case "rust":
                    case "protocol":
                        arg.ReplyWith(Rust.Protocol.printable);
                        break;

                    case "branch":
                        arg.ReplyWith(Facepunch.BuildInfo.Current.Scm.Branch);
                        break;

                    case "date":
                    case "builddate":
                        arg.ReplyWith(Facepunch.BuildInfo.Current.BuildDate.ToLocalTime().ToString());
                        break;

                    default:
                        arg.ReplyWith(OxideMod.Version.ToString());
                        break;
                }
            }
        }

        #endregion Command Handling
    }
}
