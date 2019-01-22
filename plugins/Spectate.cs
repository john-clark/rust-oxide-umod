using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// TODO: Fix weapons floating when spectating

namespace Oxide.Plugins
{
    [Info("Spectate", "Wulf/lukespragg", "0.4.3")]
    [Description("Allows only players with permission to spectate")]
    public class Spectate : CovalencePlugin
    {
        #region Localization

        private new void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandSpectate"] = "spectate",
                ["NotAllowed"] = "You are not allowed to use the '{0}' command",
                ["NoValidTargets"] = "No valid spectate targets",
                ["PlayersOnly"] = "Command '{0}' can only be used by a player",
                ["SpectateSelf"] = "You cannot spectate yourself",
                ["SpectateStart"] = "Started spectating {0}",
                ["SpectateStop"] = "Stopped spectating {0}",
                ["TargetIsSpectating"] = "{0} is currently spectating another player"
            }, this);

            // French
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandSpectate"] = "spectre",
                ["NotAllowed"] = "Vous n’êtes pas autorisé à utiliser la commande « {0} »",
                ["NoValidTargets"] = "Non valides spectate cibles",
                ["PlayersOnly"] = "Commande « {0} » seulement peut être utilisée que par un joueur",
                ["SpectateSelf"] = "Vous ne pouvez pas vous-même spectate",
                ["SpectateStart"] = "Commencé spectature {0}",
                ["SpectateStop"] = "Cessé de spectature {0}",
                ["TargetIsSpectating"] = "{0} est spectature actuellement un autre joueur"
            }, this, "fr");

            // German
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandSpectate"] = "gespenst",
                ["NotAllowed"] = "Sie sind nicht berechtigt, verwenden Sie den Befehl '{0}'",
                ["NoValidTargets"] = "Zuschauen Sie keine gültige Ziele",
                ["PlayersOnly"] = "Befehl '{0}' kann nur von einem Spieler verwendet werden",
                ["SpectateSelf"] = "Sie können nicht selbst als Zuschauer",
                ["SpectateStart"] = "Begann zuschauen {0}",
                ["SpectateStop"] = "Nicht mehr zuschauen {0}",
                ["TargetIsSpectating"] = "{0} ist derzeit ein anderer Spieler zuschauen"
            }, this, "de");

            // Russian
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandSpectate"] = "Призрак",
                ["NotAllowed"] = "Нельзя использовать команду «{0}»",
                ["NoValidTargets"] = "Нет допустимых spectate целей",
                ["PlayersOnly"] = "Команда «{0}» может использоваться только игрок",
                ["SpectateSelf"] = "Вы не можете spectate себя",
                ["SpectateStart"] = "Начал spectating {0}",
                ["SpectateStop"] = "Остановлен spectating {0}",
                ["TargetIsSpectating"] = "{0} в настоящее время spectating другой игрок"
            }, this, "ru");

            // Spanish
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandSpectate"] = "espectador",
                ["NotAllowed"] = "No se permite utilizar el comando '{0}'",
                ["NoValidTargets"] = "No válido espectador objetivos",
                ["PlayersOnly"] = "Comando '{0}' solo puede ser usado por un jugador",
                ["SpectateSelf"] = "Usted no puede sí mismo espectador",
                ["SpectateStart"] = "Comenzó a observar {0}",
                ["SpectateStop"] = "Dejado de ver {0}",
                ["TargetIsSpectating"] = "{0} está actualmente tenemos otro jugador"
            }, this, "es");
        }

        #endregion Localization

        #region Initialization

        private readonly Dictionary<string, Vector3> lastPositions = new Dictionary<string, Vector3>();
        private readonly Dictionary<string, string> spectating = new Dictionary<string, string>();

        private const string permUse = "spectate.use";

        private void Init()
        {
            permission.RegisterPermission(permUse, this);

            AddLocalizedCommand("CommandSpectate", "SpectateCommand");
        }

        #endregion Initialization

        #region Chat Command

        private void SpectateCommand(IPlayer player, string command, string[] args)
        {
            if (player.IsServer)
            {
                Message(player, "PlayersOnly", command);
                return;
            }

            if (!player.HasPermission(permUse))
            {
                Message(player, "NotAllowed", command);
                return;
            }

            BasePlayer basePlayer = player.Object as BasePlayer;
            if (basePlayer == null)
            {
                return;
            }

            if (!basePlayer.IsSpectating())
            {
                BasePlayer target = BasePlayer.Find(string.Join(" ", args.Select(v => v.ToString()).ToArray()));
                if (target == null || target.IsDead())
                {
                    Message(player, "NoValidTargets");
                    return;
                }

                if (ReferenceEquals(target, basePlayer))
                {
                    Message(player, "SpectateSelf");
                    return;
                }

                if (target.IsSpectating())
                {
                    Message(player, "TargetIsSpectating", target.displayName);
                    return;
                }

                // Store current location before spectating
                if (!lastPositions.ContainsKey(player.Id))
                {
                    lastPositions.Add(player.Id, basePlayer.transform.position);
                }
                else
                {
                    lastPositions[player.Id] = basePlayer.transform.position;
                }

                // Prep player for spectate mode
                HeldEntity heldEntity = basePlayer.GetActiveItem()?.GetHeldEntity() as HeldEntity;
                heldEntity?.SetHeld(false);

                // Put player in spectate mode
                basePlayer.SetPlayerFlag(BasePlayer.PlayerFlags.Spectating, true);
                basePlayer.gameObject.SetLayerRecursive(10);
                basePlayer.CancelInvoke("MetabolismUpdate");
                basePlayer.CancelInvoke("InventoryUpdate");
                basePlayer.ClearEntityQueue();
                basePlayer.SendEntitySnapshot(target);
                basePlayer.gameObject.Identity();
                basePlayer.SetParent(target);
                basePlayer.SetPlayerFlag(BasePlayer.PlayerFlags.ThirdPersonViewmode, true);
                player.Command("camoffset 0,1.3,0");

                // Notify player and store target name
                Message(player, "SpectateStart", target.displayName);
                if (!spectating.ContainsKey(player.Id))
                {
                    spectating.Add(player.Id, target.displayName);
                }
                else
                {
                    spectating[player.Id] = target.displayName;
                }
            }
            else
            {
                // Restore player to normal mode
                player.Command("camoffset", "0,1,0");
                basePlayer.SetParent(null);
                basePlayer.SetPlayerFlag(BasePlayer.PlayerFlags.Spectating, false);
                basePlayer.SetPlayerFlag(BasePlayer.PlayerFlags.ThirdPersonViewmode, false);
                basePlayer.gameObject.SetLayerRecursive(17);
                basePlayer.metabolism.Reset();
                basePlayer.InvokeRepeating("InventoryUpdate", 1f, 0.1f * UnityEngine.Random.Range(0.99f, 1.01f));

                // Restore player to previous state
                basePlayer.StartSleeping();
                HeldEntity heldEntity = basePlayer.GetActiveItem()?.GetHeldEntity() as HeldEntity;
                heldEntity?.SetHeld(true);

                // Teleport to original location after spectating
                if (lastPositions.ContainsKey(player.Id))
                {
                    Vector3 lastPosition = lastPositions[player.Id];
                    player.Teleport(lastPosition.x, lastPosition.y + 0.3f, lastPosition.z);
                    lastPositions.Remove(player.Id);
                }

                // Notify player and clear target name
                if (spectating.ContainsKey(player.Id))
                {
                    Message(player, "SpectateStop", spectating[player.Id]);
                    spectating.Remove(player.Id);
                }
                else
                {
                    Message(player, "SpectateStop", "?");
                }
            }
        }

        #endregion Chat Command

        #region Game Hooks

        private void OnUserConnected(IPlayer player) => ResetSpectate(player);

        private void OnUserDisconnected(IPlayer player) => ResetSpectate(player);

        // Reset player's camera offset and stored states
        private void ResetSpectate(IPlayer player)
        {
            player.Command("camoffset 0,1,0");

            if (spectating.ContainsKey(player.Id))
            {
                spectating.Remove(player.Id);
            }

            if (lastPositions.ContainsKey(player.Id))
            {
                lastPositions.Remove(player.Id);
            }
        }

        #endregion Game Hooks

        #region Helpers

        private string Lang(string key, string id = null, params object[] args)
        {
            return string.Format(lang.GetMessage(key, this, id), args);
        }

        private void AddLocalizedCommand(string key, string command)
        {
            foreach (string language in lang.GetLanguages(this))
            {
                Dictionary<string, string> messages = lang.GetMessages(language, this);
                foreach (KeyValuePair<string, string> message in messages.Where(m => m.Key.Equals(key)))
                {
                    if (!string.IsNullOrEmpty(message.Value))
                    {
                        AddCovalenceCommand(message.Value, command);
                    }
                }
            }
        }
        private void Message(IPlayer player, string key, params object[] args)
        {
            player.Reply(Lang(key, player.Id, args));
        }

        #endregion Helpers
    }
}
