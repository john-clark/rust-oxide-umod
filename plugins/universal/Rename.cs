using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Rename", "Wulf/lukespragg", "0.3.0", ResourceId = 1184)]
    [Description("Allows players with permission to instantly rename other players or self")]

    class Rename : CovalencePlugin
    {
        #region Initialization

        StoredData storedData;
        const string permOthers = "rename.others";
        const string permSelf = "rename.self";

        bool persistent;
        bool preventAdmin;

        protected override void LoadDefaultConfig()
        {
            // Options
            Config["Persistent Renames (true/false)"] = persistent = GetConfig("Persistent Renames (true/false)", false);
            Config["Prevent Admin Renames (true/false)"] = preventAdmin = GetConfig("Prevent Admin Renames (true/false)", true);

            SaveConfig();
        }

        void Init()
        {
            LoadDefaultConfig();
            LoadDefaultMessages();
            LoadPersistentData();

            permission.RegisterPermission(permOthers, this);
            permission.RegisterPermission(permSelf, this);
        }

        #endregion

        #region Stored Data

        class StoredData
        {
            public readonly HashSet<PlayerInfo> Renames = new HashSet<PlayerInfo>();
        }

        class PlayerInfo
        {
            public string Id;
            public string Old;
            public string New;

            public PlayerInfo()
            {
            }

            public PlayerInfo(IPlayer player, string newName)
            {
                Id = player.Id;
                Old = player.Name;
                New = newName;
            }
        }

        void LoadPersistentData()
        {
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Title);
        }

        void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Title, storedData);

        #endregion

        #region Localization

        void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandUsageRename"] = "Usage: {0} <name or id> [new name] (new name only if renaming self)",
                ["CommandUsageReset"] = "Usage: {0} [name or id] (optional if resetting self)",
                ["NotAllowed"] = "You are not allowed to use the '{0}' command",
                ["PlayerIsAdmin"] = "{0} is admin and cannot be renamed",
                ["PlayerNameReset"] = "{0}'s name reset to {1}",
                ["PlayerNotFound"] = "{0} was not found",
                ["PlayerNotRenamed"] = "{0} is not renamed",
                ["PlayerRenamed"] = "{0} was renamed to {1}",
                ["YouWereRenamed"] = "You were renamed to {0}",
                ["YourNameReset"] = "Your name was reset to {0}"
            }, this);

            // French
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandUsageRename"] = "Utilisation : {0} <nom ou id> [nouveau nom] (nouveau nom uniquement si vous renommer)",
                ["CommandUsageReset"] = "Utilisation : {0} [nom ou id] (facultatif si la réinitialisation de soi)",
                ["NotAllowed"] = "Vous n’êtes pas autorisé à utiliser la commande « {0} »",
                ["PlayerIsAdmin"] = "Vous n’êtes pas autorisé à utiliser la commande « {0} »",
                ["PlayerNameReset"] = "Nom de {0} réinitialiser à {1}",
                ["PlayerNotFound"] = "{0} n’a pas été trouvée",
                ["PlayerNotRenamed"] = "{0} n’est pas renommé",
                ["PlayerRenamed"] = "A été renommé {0} {1}",
                ["YouWereRenamed"] = "Vous ont été renommées {0}",
                ["YourNameReset"] = "Votre nom a été réinitialisé à {0}"
            }, this, "fr");

            // German
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandUsageRename"] = "Verbrauch: {0} <Name oder Id> [neuer Name] (neuer Name nur, wenn Sie sich umbenennen)",
                ["CommandUsageReset"] = "Verwendung: {0} [Name oder Id] (optional, wenn selbst zurücksetzen)",
                ["NotAllowed"] = "Sie sind nicht berechtigt, verwenden Sie den Befehl '{0}'",
                ["PlayerIsAdmin"] = "{0} ist Admin und kann nicht umbenannt werden",
                ["PlayerNameReset"] = "{0} Namen auf {1} zurücksetzen",
                ["PlayerNotFound"] = "{0} wurde nicht gefunden",
                ["PlayerNotRenamed"] = "{0} wird nicht umbenannt",
                ["PlayerRenamed"] = "{0} wurde umbenannt in {1}",
                ["YouWereRenamed"] = "Sie wurden umbenannt in {0}",
                ["YourNameReset"] = "Ihr Name wurde auf {0} zurückgesetzt"
            }, this, "de");

            // Russian
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandUsageRename"] = "Использование: {0} <имя или id> [новое название] (новое имя только в случае переименования самостоятельно)",
                ["CommandUsageReset"] = "Использование: {0} [имя или id] (необязательно, если сброс самоуправления)",
                ["NotAllowed"] = "Нельзя использовать команду «{0}»",
                ["PlayerIsAdmin"] = "{0} является администратором и не может быть переименован",
                ["PlayerNameReset"] = "{0} сброс {1} имя",
                ["PlayerNotFound"] = "{0} не найден",
                ["PlayerNotRenamed"] = "{0} не переименован",
                ["PlayerRenamed"] = "{0} был переименован в {1}",
                ["YouWereRenamed"] = "Вы были переименованы в {0}",
                ["YourNameReset"] = "Ваше имя было сброшено в {0}"
            }, this, "ru");

            // Spanish
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandUsageRename"] = "Uso: {0} <nombre o id> [nuevo nombre] (nuevo nombre sólo si cambiar el nombre a sí mismo)",
                ["CommandUsageReset"] = "Uso: {0} [nombre o id] (opcional en caso de reajuste del uno mismo)",
                ["NotAllowed"] = "No se permite utilizar el comando '{0}'",
                ["PlayerIsAdmin"] = "{0} es admin y no se puede cambiar",
                ["PlayerNameReset"] = "Nombre de {0} a {1}",
                ["PlayerNotFound"] = "No se encontró {0}",
                ["PlayerNotRenamed"] = "{0} no se cambia el nombre",
                ["PlayerRenamed"] = "Fue retitulado {0} a {1}",
                ["YouWereRenamed"] = "Sie wurden umbenannt in {0}",
                ["YourNameReset"] = "Su nombre fue reajustado a {0}"
            }, this, "es");
        }

        #endregion

        #region Connections

        void OnUserConnected(IPlayer player)
        {
            var rename = storedData.Renames.FirstOrDefault(r => r.Id == player.Id);
            if (!persistent || rename == null) return;

            player.Rename(rename.New);
            Puts($"{rename.Old} was renamed to {rename.New}");
            player.Message(Lang("YouWereRenamed", player.Id, rename.New));

            var pos = player.Position();
            player.Teleport(pos.X, pos.Y, pos.Z);
        }

        #endregion

        #region Commands

        [Command("rename")]
        void RenameCommand(IPlayer player, string command, string[] args)
        {
            if (args.Length >= 2 && !player.HasPermission(permOthers) || !player.HasPermission(permSelf))
            {
                player.Reply(Lang("NotAllowed", player.Id, command));
                return;
            }

            if (args.Length == 0 || args.Length == 1 && player.Id == "server_console")
            {
                player.Reply(Lang("CommandUsageRename", player.Id, command));
                return;
            }

            var target = args.Length >= 2 ? players.FindPlayer(args[0]) : player;
            if (target == null)
            {
                player.Reply(Lang("PlayerNotFound", player.Id, args[0].Sanitize()));
                return;
            }

            if (args.Length >= 2 && preventAdmin && target.IsAdmin)
            {
                player.Reply(Lang("PlayerIsAdmin", player.Id, target.Name.Sanitize()));
                return;
            }

            var newName = args.Length >= 2 ? args[1].Sanitize() : args[0].Sanitize();
            target.Message(Lang("YouWereRenamed", target.Id, newName.Sanitize()));
            if (!Equals(target, player)) player.Reply(Lang("PlayerRenamed", player.Id, target.Name.Sanitize(), newName.Sanitize()));

            if (persistent)
            {
                storedData.Renames.RemoveWhere(r => r.Id == target.Id);
                storedData.Renames.Add(new PlayerInfo(target, newName));
                SaveData();
            }

            target.Rename(newName);
        }

        [Command("resetname", "namereset")]
        void NameResetCommand(IPlayer player, string command, string[] args)
        {
            if (args.Length >= 1 && !player.HasPermission(permOthers) || !player.HasPermission(permSelf))
            {
                player.Reply(Lang("NotAllowed", player.Id, command));
                return;
            }

            if (args.Length == 0 && player.Id == "server_console")
            {
                player.Reply(Lang("CommandUsageReset", player.Id, command));
                return;
            }

            var target = args.Length >= 1 ? players.FindPlayer(args[0]) : player;
            if (target == null)
            {
                player.Reply(Lang("PlayerNotFound", player.Id, args[0].Sanitize()));
                return;
            }

            var rename = storedData.Renames.FirstOrDefault(r => r.Id == target.Id);
            if (rename == null)
            {
                player.Reply(Lang("PlayerNotRenamed", player.Id, args[0].Sanitize()));
                return;
            }

            target.Message(Lang("YourNameReset", target.Id, rename.Old.Sanitize()));
            if (!Equals(target, player)) player.Reply(Lang("PlayerNameReset", player.Id, target.Name.Sanitize(), rename.Old.Sanitize()));

            if (target.IsConnected) target.Rename(rename.Old);
            storedData.Renames.Remove(rename);
            SaveData();
        }
        #endregion

        #region Helpers

        T GetConfig<T>(string name, T value) => Config[name] == null ? value : (T)Convert.ChangeType(Config[name], typeof(T));

        string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        #endregion
    }
}