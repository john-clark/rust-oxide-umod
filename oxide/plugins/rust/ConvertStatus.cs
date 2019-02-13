using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Convert Status", "Orange", "1.0.8")]
    [Description("Change your admin status by a command")]
    public class ConvertStatus : RustPlugin
    {
        #region Vars

        private const string perm_use = "convertstatus.use";

        #endregion
    
        #region Oxide Hooks

        private void Init()
        {
            permission.RegisterPermission(perm_use, this);
            lang.RegisterMessages(messagesEN, this);
            lang.RegisterMessages(messagesRU, this, "ru");
        }

        #endregion

        #region Commands

        [ChatCommand("convert")]
        private void CmdConvert(BasePlayer p)
        {
            Convert(p);
        }

        #endregion

        #region Helpers

        private void Convert(BasePlayer p)
        {
            if (!HasPerm(p, perm_use))
            {
                message(p, "NOPERM");
                return;
            }

            if (p.IsAdmin)
            {
                if (p.IsFlying)
                {
                    p.SendConsoleCommand("noclip");
                    message(p, "NOCLIP");
                    timer.Once(1f, () => Convert(p));
                    return;
                }
                
                ServerUsers.Set(p.userID, ServerUsers.UserGroup.None, "", "");
                p.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
                p.Connection.authLevel = 0;
                permission.RemoveUserGroup(p.UserIDString, "admin");
            }
            else
            {
                ServerUsers.Set(p.userID, ServerUsers.UserGroup.Owner, "", "");
                p.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                p.Connection.authLevel = 2;
                permission.AddUserGroup(p.UserIDString, "admin");
            }
            
            var a = p.IsAdmin ? "into" : "out of";
            PrintWarning($"{p.displayName} converted {a} admin status");
            message(p, "CHANGED", p.IsAdmin);
            ServerUsers.Save();
        }

        private bool HasPerm(BasePlayer p, string s)
        {
            return permission.UserHasPermission(p.UserIDString, s);
        }

        #endregion

        #region Language

        private Dictionary<string, string> messagesEN = new Dictionary<string, string>
        {
            {"NOPERM", "You don't have permission to that command!"},
            {"CHANGED", "Admin status now is <color=cyan>{0}</color>"},
            {"NOCLIP", "Fly will be deactivated in 1 sec. Don't use it in next 3 seconds or you will be banned!"},
        };
        
        private Dictionary<string, string> messagesRU = new Dictionary<string, string>
        {
            {"NOPERM", "У вас нет доступа к этой команде!"},
            {"CHANGED", "Ваш админ статус теперь <color=cyan>{0}</color>"},
            {"NOCLIP", "Режим полёта будет выключен через 1 секунду. Не используйте его в ближайшие 3 секуны или вы будете забанены!"},
        };

        private void message(BasePlayer player, string key, params object[] args)
        {
            player.ChatMessage(string.Format(lang.GetMessage(key, this, player.UserIDString), args));
        }

        #endregion
    }
}