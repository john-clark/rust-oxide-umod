using System;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("SignTracker", "Wulf/lukespragg", "2.0.1", ResourceId = 1060)]
    [Description("Track who last updated a sign with name and Steam ID, and optional logging")]

    class SignTracker : CovalencePlugin
    {
        #region Initialization

        readonly Dictionary<uint, string> signs = new Dictionary<uint, string>();
        DynamicConfigFile storedData;

        const string permUse = "signtracker.use";

        bool logUpdates;

        protected override void LoadDefaultConfig()
        {
            // Options
            Config["Log Sign Updates (true/false)"] = logUpdates = GetConfig("Log Sign Updates (true/false)", true);

            SaveConfig();
        }

        void OnServerInitialized()
        {
            LoadDefaultConfig();
            LoadDefaultMessages();
            permission.RegisterPermission(permUse, this);
            storedData = Interface.Oxide.DataFileSystem.GetDatafile("SignTracker");
            foreach (var pair in storedData) signs.Add(uint.Parse(pair.Key), pair.Value.ToString());
        }

        #endregion

        #region Localization

        void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["LastUpdated"] = "Sign was last updated by {0} ({2}) on {1}",
                ["NoInformation"] = "No information available for this sign",
                ["NotAllowed"] = "You are not allowed to use the '{0}' command",
                ["SignUpdated"] = "Sign {0} by {1} updated on {2} by {3} ({4})"
            }, this);

            // French
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["LastUpdated"] = "Signe a été actualisée par {0} ({2}) sur {1}",
                ["NoInformation"] = "Aucune information disponible pour ce signe",
                ["NotAllowed"] = "Vous n’êtes pas autorisé à utiliser la commande « {0} »",
                ["SignUpdated"] = "Signe de {0} de {1} {2} mis à jour par {3} ({4})"
            }, this, "fr");

            // German
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["LastUpdated"] = "Zeichen wurde geaendert von {0} ({2}) auf {1}",
                ["NoInformation"] = "Keine Informationen für dieses Zeichen verfügbar",
                ["NotAllowed"] = "Sie sind nicht berechtigt, verwenden Sie den Befehl '{0}'",
                ["SignUpdated"] = "Melden Sie {0} von {1} {2} von {3} ({4}) aktualisiert"
            }, this, "de");

            // Russian
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["LastUpdated"] = "Знак был обновлен пользователем {0} ({2}) на {1}",
                ["NoInformation"] = "Нет информации для этого знака",
                ["NotAllowed"] = "Нельзя использовать команду «{0}»",
                ["SignUpdated"] = "Знак {0} по {1} обновляется на {2} {3} ({4})"
            }, this, "ru");

            // Spanish
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["LastUpdated"] = "Muestra la última actualización por {0} ({2}) en {1}",
                ["NoInformation"] = "No hay información disponible para este signo",
                ["NotAllowed"] = "No se permite utilizar el comando '{0}'",
                ["SignUpdated"] = "Firmar {0} {1} {2} actualizada por {3} ({4})"
            }, this, "es");
        }

        #endregion

        #region Chat Command

        [Command("sign")]
        void SignCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permUse))
            {
                player.Reply(Lang("NotAllowed", player.Id, command));
                return;
            }

            RaycastHit hit;
            var basePlayer = player.Object as BasePlayer;
            if (basePlayer == null || !Physics.Raycast(basePlayer.eyes.HeadRay(), out hit, 2.0f)) return;

            var sign = hit.transform.GetComponentInParent<Signage>();
            if (sign == null) return;

            if (signs.ContainsKey(sign.textureID))
            {
                var value = signs[sign.textureID].Split(',');
                player.Reply(Lang("LastUpdated", player.Id, value[0], value[2], value[1]));
            }
            else player.Reply(Lang("NoInformation", player.Id));
        }

        #endregion

        #region Sign Storage

        void OnSignUpdated(Signage sign, BasePlayer player)
        {
            if (signs.ContainsKey(sign.textureID)) signs[sign.textureID] = $"{player.displayName}, {player.userID}, {DateTime.Now}";
            else signs.Add(sign.textureID, $"{player.displayName}, {player.userID}, {DateTime.Now}");

            if (logUpdates) Log(Lang("SignUpdated", null, sign.textureID, sign.OwnerID, DateTime.Now, player.displayName, player.userID));
        }

        void SaveSignData()
        {
            storedData.Clear();
            foreach (var signea in signs) storedData[signea.Key.ToString()] = signea.Value;
            Interface.Oxide.DataFileSystem.SaveDatafile("SignTracker");
        }

        void OnServerShutdown() => SaveSignData();

        void OnServerSave() => SaveSignData();

        void Unload() => SaveSignData();

        #endregion

        #region Helpers

        T GetConfig<T>(string name, T value) => Config[name] == null ? value : (T)Convert.ChangeType(Config[name], typeof(T));

        string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        void Log(string text) => LogToFile("updates", text, this);

        #endregion
    }
}