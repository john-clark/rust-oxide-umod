using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Healer", "Wulf/lukespragg", "2.5.0")]
    [Description("Allows players with permission to heal themselves or others")]
    public class Healer : CovalencePlugin
    {
        #region Configuration

        private Configuration config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Cooldown in seconds (0 to disable)")]
            public int Cooldown;

            [JsonProperty(PropertyName = "Maximum heal amount (1 - infinity)")]
            public int MaxAmount;

            public static Configuration DefaultConfig()
            {
                return new Configuration
                {
                    Cooldown = 30,
                    MaxAmount = 100
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config?.MaxAmount == null) LoadDefaultConfig();
            }
            catch
            {
                LogWarning($"Could not read oxide/config/{Name}.json, creating new config file");
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig() => config = Configuration.DefaultConfig();

        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion Configuration

        #region Localization

        private new void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandHeal"] = "heal",
                ["CommandHealAll"] = "healall",
                ["CommandUsage"] = "Usage: {0} <amount> <name or id> (target optional)",
                ["Cooldown"] = "Wait a bit before attempting to use '{0}' again",
                ["NotAllowed"] = "You are not allowed to use the '{0}' command",
                ["NoPlayersFound"] = "No players found with name or ID '{0}'",
                ["PlayerNotFound"] = "Player '{0}' was not found",
                ["PlayerWasHealed"] = "{0} was healed {1}",
                ["PlayersFound"] = "Multiple players were found, please specify: {0}",
                ["PlayersHealed"] = "All players have been healed {0}!",
                ["YouWereHealed"] = "You were healed {0}"
            }, this);

            // French
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandHeal"] = "guérir",
                ["CommandHealAll"] = "guérirtous",
                ["CommandUsage"] = "Utilisation : {0} <montant> <nom ou id> (objectif en option)",
                ["Cooldown"] = "Attendre un peu avant de tenter de réutiliser « {0} »",
                ["NotAllowed"] = "Vous n’êtes pas autorisé à utiliser la commande « {0} »",
                ["NoPlayersFound"] = "Aucun joueur trouvé avec le nom ou l’ID « {0} »",
                ["PlayerNotFound"] = "Player « {0} » n’a pas été trouvée",
                ["PlayerWasHealed"] = "{0} a été guéri {1}",
                ["PlayersFound"] = "Plusieurs joueurs ont été trouvées, veuillez préciser : {0}",
                ["PlayersHealed"] = "Tous les joueurs ont été guéris {0}!",
                ["YouWereHealed"] = "Vous avez été guéri {0}"
            }, this, "fr");

            // German
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandHeal"] = "zuheilen",
                ["CommandHealAll"] = "alleheilen",
                ["CommandUsage"] = "Verwendung: {0} <Betrag> <Name oder Id> (Ziel optional)",
                ["Cooldown"] = "Noch ein bisschen warten Sie, bevor Sie '{0}' wieder verwenden",
                ["NotAllowed"] = "Sie sind nicht berechtigt, verwenden Sie den Befehl '{0}'",
                ["NoPlayersFound"] = "Keine Spieler mit Namen oder die ID gefunden '{0}'",
                ["PlayerNotFound"] = "Player '{0}' wurde nicht gefunden",
                ["PlayerWasHealed"] = "{0} wurde geheilt {1}",
                ["PlayersFound"] = "Mehrere Spieler wurden gefunden, bitte angeben: {0}",
                ["PlayersHealed"] = "Alle Spieler sind geheilt worden {0}!",
                ["YouWereHealed"] = "Sie wurden geheilt {0}"
            }, this, "de");

            // Russian
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandHeal"] = "исцелить",
                ["CommandHealAll"] = "лечитьвсе",
                ["CommandUsage"] = "Использование: {0} <сумма> <имя или id> (цель необязательно)",
                ["Cooldown"] = "Подождите немного, прежде чем использовать «{0}» снова",
                ["NotAllowed"] = "Нельзя использовать команду «{0}»",
                ["NoPlayersFound"] = "Игроки не найдено имя или ID «{0}»",
                ["PlayerNotFound"] = "Игрок «{0}» не найден",
                ["PlayerWasHealed"] = "{0} был исцелен {1}",
                ["PlayersFound"] = "Несколько игроков были найдены, пожалуйста укажите: {0}",
                ["PlayersHealed"] = "Все игроки были исцелены {0}!",
                ["YouWereHealed"] = "Вы были зарубцевавшиеся {0}"
            }, this, "ru");

            // Spanish
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandHeal"] = "curar",
                ["CommandHealAll"] = "sanaratodos",
                ["CommandUsage"] = "Uso: {0} <cantidad> <nombre o id> (destino opcional)",
                ["Cooldown"] = "Esperar un poco antes de intentar volver a utilizar '{0}'",
                ["NotAllowed"] = "No se permite utilizar el comando '{0}'",
                ["NoPlayersFound"] = "No hay jugadores con ID o nombre '{0}'",
                ["PlayerNotFound"] = "Jugador '{0}' no se encontró",
                ["PlayerWasHealed"] = "{0} es {1} curado",
                ["PlayersFound"] = "Varios jugadores fueron encontrados, por favor especifique: {0}",
                ["PlayersHealed"] = "Todos los jugadores han sido sanados {0}!",
                ["YouWereHealed"] = "Fuiste sanado {0}"
            }, this, "es");
        }

        #endregion Localization

        #region Initialization

        private readonly Hash<string, float> cooldowns = new Hash<string, float>();

        private const string permAll = "healer.all";
        private const string permOthers = "healer.others";
        private const string permSelf = "healer.self";

        private void Init()
        {
            permission.RegisterPermission(permAll, this);
            permission.RegisterPermission(permOthers, this);
            permission.RegisterPermission(permSelf, this);

            AddLocalizedCommand("CommandHeal", "HealCommand");
            AddLocalizedCommand("CommandHealAll", "HealAllCommand");
        }

        #endregion Initialization

        #region Healing

        private void Heal(IPlayer player, float amount)
        {
#if RUST
            var basePlayer = player.Object as BasePlayer;
            basePlayer.metabolism.bleeding.value = 0;
            basePlayer.metabolism.calories.value += amount;
            basePlayer.metabolism.dirtyness.value = 0;
            basePlayer.metabolism.hydration.value += amount;
            basePlayer.metabolism.oxygen.value = 1;
            basePlayer.metabolism.poison.value = 0;
            basePlayer.metabolism.radiation_level.value = 0;
            basePlayer.metabolism.radiation_poison.value = 0;
            basePlayer.metabolism.temperature.value = 32;
            basePlayer.metabolism.wetness.value = 0;
            basePlayer.StopWounded();
#endif
            player.Heal(amount);
        }

        #endregion Healing

        #region Heal Command

        private void HealCommand(IPlayer player, string command, string[] args)
        {
            var amount = 0f;
            if (args.Length > 0) float.TryParse(args[0], out amount);
            if (amount > config.MaxAmount || amount.Equals(0f)) amount = config.MaxAmount;

            IPlayer target;
            if (args.Length >= 2) target = FindPlayer(args[1], player);
            else if (args.Length == 1) target = players.FindPlayer(args[0]);
            else target = player;

            // TODO: Show message and return if server is trying to heal self
            if (target == null) return;

            if ((Equals(target, player) && !player.HasPermission(permSelf)) || !player.HasPermission(permOthers))
            {
                Message(player, "NotAllowed", command);
                return;
            }

            if (args.Length == 0 && target.IsServer)
            {
                Message(player, "CommandUsage", command);
                return;
            }

            if (target.IsServer || !target.IsConnected)
            {
                var name = args.Length >= 2 ? args[1] : args.Length == 1 ? args[0] : "";
                Message(player, "PlayerNotFound", name);
                return;
            }

            if (!player.IsServer)
            {
                if (!cooldowns.ContainsKey(player.Id)) cooldowns.Add(player.Id, 0f);
                if (config.Cooldown != 0 && cooldowns[player.Id] + config.Cooldown > Interface.Oxide.Now)
                {
                    Message(player, "Cooldown", command);
                    return;
                }
            }

            if (amount > config.MaxAmount || amount.Equals(0)) amount = config.MaxAmount;

            Heal(target, amount);
            cooldowns[player.Id] = Interface.Oxide.Now;
            Message(target, "YouWereHealed", amount);

            if (!Equals(target, player)) Message(player, "PlayerWasHealed", target.Name.Sanitize(), amount);
        }

        #endregion Heal Command

        #region Heal All Command

        private void HealAllCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permAll))
            {
                Message(player, "NotAllowed", player.Id, command);
                return;
            }

            // TODO: Show message and return if no players are online

            if (!player.IsServer)
            {
                if (!cooldowns.ContainsKey(player.Id)) cooldowns.Add(player.Id, 0f);
                if (config.Cooldown != 0 && cooldowns[player.Id] + config.Cooldown > Interface.Oxide.Now)
                {
                    Message(player, "Cooldown", command);
                    return;
                }
            }

            var amount = 0f;
            if (args.Length > 0) float.TryParse(args[0], out amount);
            if (amount > config.MaxAmount || amount.Equals(0f)) amount = config.MaxAmount;

            foreach (var target in players.Connected.Where(t => t.Health < t.MaxHealth))
            {
                Heal(target, amount);
                cooldowns[player.Id] = Interface.Oxide.Now;
                Message(target, "YouWereHealed", amount);
            }

            Message(player, "PlayersHealed", amount);
        }

        #endregion Heal All Command

        #region Helpers

        private void AddLocalizedCommand(string key, string command)
        {
            foreach (var language in lang.GetLanguages(this))
            {
                var messages = lang.GetMessages(language, this);
                foreach (var message in messages.Where(m => m.Key.Equals(key)))
                    if (!string.IsNullOrEmpty(message.Value)) AddCovalenceCommand(message.Value, command);
            }
        }

        private IPlayer FindPlayer(string nameOrId, IPlayer player)
        {
            var foundPlayers = players.FindPlayers(nameOrId).ToArray();
            if (foundPlayers.Length > 1)
            {
                Message(player, "PlayersFound", string.Join(", ", foundPlayers.Select(p => p.Name).ToArray()));
                return null;
            }

            var target = foundPlayers.Length == 1 ? foundPlayers[0] : null;
            if (target == null || !target.IsConnected)
            {
                Message(player, "NoPlayersFound", nameOrId);
                return null;
            }

            return target;
        }

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private void Message(IPlayer player, string key, params object[] args) => player.Reply(Lang(key, player.Id, args));

        #endregion Helpers
    }
}
