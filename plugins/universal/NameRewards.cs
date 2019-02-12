using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("NameRewards", "Kappasaurus", "1.0.0", ResourceId = 0)]
    [Description("Adds players to a group based on phrases in their name")]

    class NameRewards : CovalencePlugin
    {
        ConfigData config;

        class ConfigData
        {
            public string Group { get; set; }
            public string[] Phrases { get; set; }
        }

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(new ConfigData
            {
                Group = "vip",
                Phrases = new[] { "Oxide", "Example" }
            }, true);
        }

        void Init()
        {
            config = Config.ReadObject<ConfigData>();
            if (!permission.GroupExists(config.Group))
                permission.CreateGroup(config.Group, config.Group, 0);
        }

        void OnUserConnected(IPlayer player)
        {
            foreach (var phrase in config.Phrases)
            {
                if (permission.UserHasGroup(player.Id, config.Group)) break;
                if (player.Name.ToLower().Contains(phrase.ToLower()))
                {
                    permission.AddUserGroup(player.Id, config.Group);
                    break;
                }
                permission.RemoveUserGroup(player.Id, config.Group);
            }
        }
    }
}