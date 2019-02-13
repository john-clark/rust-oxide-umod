using System.Linq;
using System.Reflection;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("ResetCodeLocks", "Absolut", "1.0.2", ResourceId = 2348)]

    class ResetCodeLocks : RustPlugin
    {
        private FieldInfo CurrentCode;
        private FieldInfo CurrentGuestCode;
        private FieldInfo CodeLockWhiteList;
        private FieldInfo hasCode;
        private FieldInfo hasGuestCode;

        string TitleColor = "<color=orange>";
        string MsgColor = "<color=#A9A9A9>";

        void Loaded()
        {
            CurrentCode = typeof(CodeLock).GetField("code", (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));
            CurrentGuestCode = typeof(CodeLock).GetField("guestCode", (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));
            CodeLockWhiteList = typeof(CodeLock).GetField("whitelistPlayers", (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));
            hasCode = typeof(CodeLock).GetField("hasCode", (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));
            hasGuestCode = typeof(CodeLock).GetField("hasGuestCode", (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));
            lang.RegisterMessages(messages, this);
            permission.RegisterPermission(this.Name + ".allow", this);
        }

        Dictionary<string, string> messages = new Dictionary<string, string>()
        {
            {"title", "ResetCodeLocks: " },
            {"Instructions", "Commands For ResetCodeLocks:\n/code main '4 digit code' ---> Resets the code on all your CodeLocks to the code given\n/code guest '4 digit code' ---> Resets the guest code on all your CodeLocks to the code given\n--------------------\nExamples: /code guest 1234\n/code main 1234"},
            {"CodeLength", "The code must be 4 digits long. Example: 1234" },
            {"NoCodeLocksFound", "No code locks found!" },
            {"NumberCodeLocksReset", "You reset the code on {0} CodeLocks" },
            {"NoPerm", "You do not have permission to use this command" },
        };

        [ChatCommand("code")]
        private void chatcod(BasePlayer player, string cmd, string[] args)
        {
            if (player == null) return;
            if (!permission.UserHasPermission(player.UserIDString, this.Name + ".allow"))
            {
                GetSendMSG(player, "NoPerm");
                return;
            }
            if (args == null || args.Length < 2)
            {
                GetSendMSG(player, "Instructions");
                return;
            }
            int code;
            if (!int.TryParse(args[1], out code))
            {
                GetSendMSG(player, "Instructions");
                return;
            }
            if (args[1].Length != 4)
            {
                GetSendMSG(player, "CodeLength");
                return;
            }
            switch (args[0].ToLower())
            {
                case "main":
                    PlayerSetLocks(player, code.ToString());
                    break;
                case "guest":
                    PlayerSetLocks(player, code.ToString(), true);
                    break;
                default:
                    GetSendMSG(player, "Instructions");
                    return;
            }
        }
        private void GetSendMSG(BasePlayer player, string message, string arg1 = "", string arg2 = "", string arg3 = "")
        {
            string msg = string.Format(lang.GetMessage(message, this, player.UserIDString), arg1, arg2, arg3);
            SendReply(player, TitleColor + lang.GetMessage("title", this, player.UserIDString) + "</color>" + MsgColor + msg + "</color>");
        }

        private void PlayerSetLocks(BasePlayer player, string code, bool guest = false)
        {
            bool HasLocks = false;
            int num = 0;
            foreach (var entry in UnityEngine.Object.FindObjectsOfType<BaseEntity>().Where(k => k.GetSlot(BaseEntity.Slot.Lock) != null && (k.OwnerID == player.userID || (player.IsAdmin && k.OwnerID == 0))))
            {
                BaseEntity lockSlot = entry.GetSlot(BaseEntity.Slot.Lock);
                CodeLock codelock = lockSlot?.GetComponent<CodeLock>();
                if (codelock != null)
                {
                    HasLocks = true;
                    if (guest)
                    {
                        CurrentGuestCode.SetValue(codelock, code);
                        hasGuestCode.SetValue(codelock, true);
                        hasCode.SetValue(codelock, true);
                    }
                    else
                    {
                        CurrentCode.SetValue(codelock, code);
                        hasGuestCode.SetValue(codelock, true);
                        hasCode.SetValue(codelock, true);
                        List<ulong> whitelisted = CodeLockWhiteList.GetValue(codelock) as List<ulong>;
                        if (!whitelisted.Contains(player.userID))
                            whitelisted.Add(player.userID);
                        CodeLockWhiteList.SetValue(codelock, whitelisted);
                        codelock.SetFlag(BaseEntity.Flags.Locked, true);
                    }
                    num++;
                }
            }
            if (!HasLocks)
                GetSendMSG(player, "NoCodeLocksFound");
            else
                GetSendMSG(player, "NumberCodeLocksReset", num.ToString());
        }
    }
}