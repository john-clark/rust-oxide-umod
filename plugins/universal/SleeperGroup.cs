using System;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("SleeperGroup", "Wulf/lukespragg", "0.1.0", ResourceId = 2250)]
    [Description("Puts players in a permissions group on disconnect if sleeping")]

    class SleeperGroup : CovalencePlugin
    {
        string permGroup;

        protected override void LoadDefaultConfig()
        {
            Config["Permissions Group"] = permGroup = GetConfig("Permissions Group", "sleeper");
            SaveConfig();
        }

        void Init()
        {
            LoadDefaultConfig();
            if (!permission.GroupExists(permGroup)) permission.CreateGroup(permGroup, permGroup, 0);
        }

        void OnUserConnected(IPlayer player)
        {
            if (player.BelongsToGroup(permGroup)) player.RemoveFromGroup(permGroup);
        }

        void OnUserDisconnected(IPlayer player)
        {
            if (player.IsSleeping && !player.BelongsToGroup(permGroup)) player.AddToGroup(permGroup);
        }

        T GetConfig<T>(string name, T value) => Config[name] == null ? value : (T)Convert.ChangeType(Config[name], typeof(T));
    }
}