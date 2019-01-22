using System.Collections.Generic;
using Oxide.Core;
using System.Linq;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Better Chat Toggle", "Ryan", "1.0.0")]
    [Description("Easily toggle chat tags and formatting for Better Chat")]
    public class BetterChatToggle : CovalencePlugin
    {
        #region Declaration

        private const string UsePerm = "betterchattoggle.use";
        private bool ConfigChanged;

        private Dictionary<string, bool> TagData;

        private List<string> Tags;
        private List<string> Commands;

        #endregion

        #region Configuration

        protected override void LoadDefaultConfig() => PrintWarning("Generating default configuration file...");

        private void InitConfig()
        {
            Tags = GetConfig(new List<string>
            {
                "★"
            }, "Tags");
            Commands = GetConfig(new List<string>
            {
                "tags",
                "tag"
            }, "Commands");

            if (ConfigChanged)
            {
                PrintWarning("Updated the configuration file with new/updated values.");
                SaveConfig();
            }
        }

        private T GetConfig<T>(T defaultVal, params string[] path)
        {
            var data = Config.Get(path);
            if (data != null)
            {
                return Config.ConvertValue<T>(data);
            }

            Config.Set(path.Concat(new object[] { defaultVal }).ToArray());
            ConfigChanged = true;
            return defaultVal;
        }

        #endregion

        #region Lang

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                ["Permission"] = "You don't have permission to use that command",
                ["Tags.Disabled"] = "You have disabled your tags from showing in chat.",
                ["Tags.Enabled"] = "You have enabled your tags. They will now show in chat."
            }, this);
        }

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        #endregion

        #region Methods

        private void LoadData()
        {
            TagData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, bool>>(Name);
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Name, TagData);
        }

        private void RegisterCommands()
        {
            if (!Commands.Any())
            {
                PrintWarning("No commands registered in the config. Registering default commands...");
                Commands = new List<string>
                {
                    "tags",
                    "tag"
                };
            }

            AddCovalenceCommand(Commands.ToArray(), nameof(TagCommand));
        }

        #endregion

        #region Hooks

        private void Init()
        {
            InitConfig();
            LoadData();
            RegisterCommands();
            permission.RegisterPermission(UsePerm, this);
        }

        private void Unload()
        {
            SaveData();
        }

        private object OnBetterChat(Dictionary<string, object> data)
        {
            var titles = (List<string>) data["Titles"];
            var player = (IPlayer) data["Player"];

            if (!titles.Any() || player == null)
            {
                return null;
            }

            bool value;
            if (!TagData.TryGetValue(player.Id, out value) || value)
            {
                return null;
            }

            foreach (var title in titles.ToList())
            {
                var formattedTitle = Formatter.ToPlaintext(title);

                if (!Tags.Contains(formattedTitle))
                {
                    continue;
                }

                titles.Remove(title);
            }

            data["Titles"] = titles;
            return data;
        }

        #endregion

        #region Commands

        private void TagCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(UsePerm))
            {
                player.Message(Lang("Permission", player.Id));
                return;
            }

            bool value;
            if (!TagData.TryGetValue(player.Id, out value) || !value)
            {
                player.Message(Lang("Tags.Enabled", player.Id));
                TagData[player.Id] = true;
                return;
            }

            player.Message(Lang("Tags.Disabled", player.Id));
            TagData[player.Id] = false;
            SaveData();
        }

        #endregion
    }
}