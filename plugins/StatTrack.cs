using System;

namespace Oxide.Plugins
{
    [Info("Stat Track", "Orange", "1.0.3")]
    [Description("Allows your players to get statstracking kills on items")]
    public class StatTrack : RustPlugin
    {
        #region Vars

        private const string permUse = "stattrack.use";
        private string trackMark;
        private string trackColor;
        private bool trackNpc;

        #endregion

        #region Oxide Hooks

        private void Init()
        {
            LoadDefaultConfig();
            permission.RegisterPermission(permUse, this);
        }

        private void OnPlayerDie(BasePlayer player, HitInfo info)
        {
            CheckDeath(player, info);
        }

        #endregion

        #region Config

        protected override void LoadDefaultConfig()
        {
            Config["Text color"] = trackColor = GetConfig("Text color", "#e76d34");
            Config["Text"] = trackMark = GetConfig("Text", "Stat Track");
            Config["Track NPC kills?"] = trackNpc = GetConfig("Track NPC kills?", false);
            SaveConfig();
        }

        private T GetConfig<T>(string name, T value)
        {
            return Config[name] == null ? value : (T) Convert.ChangeType(Config[name], typeof(T));
        }

        #endregion

        #region Helpers

        private void CheckDeath(BasePlayer victim, HitInfo info)
        {    
            if (info == null) {return;}
            var initiator = info.InitiatorPlayer;
            if (initiator == null) {return;}
            if (initiator.IsNpc) {return;}
            if (victim.IsNpc && !trackNpc) {return;}
            if (initiator == victim) {return;}
            if (!permission.UserHasPermission(initiator.UserIDString, permUse)) {return;}
            var item = initiator.GetActiveItem();
            if (item == null) {return;}
            var kills = IsStatTrackItem(item.name) ? GetStatTrackKills(item.name) + 1 : 1;
            item.name = $"<color={trackColor}>[{trackMark}]</color> {item.info.displayName.english}, <color={trackColor}>{kills} kills</color>";
            item.MarkDirty();
        }

        #endregion

        #region StatTrack

        private bool IsStatTrackItem(string name)
        {
            return name != null && name.Contains(trackMark);
        }

        private int GetStatTrackKills(string name)
        {
            name = name.TrimEnd("kills</color>".ToCharArray());
            var killsS = name.Substring(name.LastIndexOf(">") + 1);
            return Convert.ToInt32(killsS);
        }

        #endregion

        
    }
}