using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Sleep", "Wulf/lukespragg", "0.2.0", ResourceId = 1156)]
    [Description("Allows players with permission to get a well-rested sleep")]
    public class Sleep : CovalencePlugin
    {
        #region Configuration

        private Configuration config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Cure while sleeping (true/false)")]
            public bool CureWhileSleeping;

            [JsonProperty(PropertyName = "Heal while sleeping (true/false)")]
            public bool HealWhileSleeping;

            [JsonProperty(PropertyName = "Restore while sleeping (true/false)")]
            public bool RestoreWhileSleeping;

            [JsonProperty(PropertyName = "Curing rate (0 - 100)")]
            public int CuringRate;

            [JsonProperty(PropertyName = "Healing rate (0 - 100)")]
            public int HealingRate;

            [JsonProperty(PropertyName = "Restoration rate (0 - 100)")]
            public int RestorationRate;

            public static Configuration DefaultConfig()
            {
                return new Configuration
                {
                    CureWhileSleeping = false,
                    HealWhileSleeping = true,
                    RestoreWhileSleeping = true,
                    CuringRate = 5,
                    HealingRate = 5,
                    RestorationRate = 5
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config?.HealWhileSleeping == null) SaveConfig();
            }
            catch
            {
                LogWarning($"Could not read oxide/config/{Name}.json, creating new config file");
            }
            LoadDefaultConfig();
        }

        protected override void LoadDefaultConfig() => config = Configuration.DefaultConfig();

        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion

        #region Localization

        private new void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Command"] = "sleep",
                ["Dirty"] = "You seem to be a bit dirty, go take a dip!",
                ["Hungry"] = "You seem to be a bit hungry, eat something",
                ["NotAllowed"] = "You can't go to sleep right now",
                ["Restored"] = "You have awaken restored and rested!",
                ["Thirsty"] = "You seem to be a bit thirsty, drink something!"
            }, this);
        }

        #endregion

        #region Initialization

        private readonly Dictionary<string, Timer> sleepTimers = new Dictionary<string, Timer>();
        private const string permAllow = "sleep.allow";

        private void Init()
        {
            AddCommandAliases("Command", "SleepCommand");

            permission.RegisterPermission(permAllow, this);
        }

        #endregion

        #region Restoration

        private void Restore(BasePlayer player)
        {
            var bleeding = player.metabolism.bleeding.value;
            var calories = player.metabolism.calories.value;
            var comfort = player.metabolism.comfort.value;
            var heartRate = player.metabolism.heartrate.value;
            var poison = player.metabolism.poison.value;
            var radLevel = player.metabolism.radiation_level.value;
            var radPoison = player.metabolism.radiation_poison.value;
            var temperature = player.metabolism.temperature.value;

            if (config.CureWhileSleeping)
            {
                if (poison > 0) poison = poison - (poison / config.CuringRate);
                if (radLevel > 0) radLevel = radLevel - (radLevel / config.CuringRate);
                if (radPoison > 0) radPoison = radPoison - (radPoison / config.CuringRate);
            }

            if (config.HealWhileSleeping)
            {
                if (bleeding.Equals(1)) bleeding = 0;
                if (player.health < 100) player.health = player.health + (player.health / config.HealingRate);
            }

            if (config.RestoreWhileSleeping)
            {
                if (calories < 1000) calories = calories - (calories / config.RestorationRate);
                if (comfort < 0.5) comfort = comfort + (comfort / config.RestorationRate);
                if (heartRate > 0.5) heartRate = heartRate + (heartRate / config.RestorationRate);
                if (temperature < 20) temperature = temperature + (temperature / config.RestorationRate);
                else temperature = temperature - (temperature / config.RestorationRate);
            }
        }

        private void OnPlayerSleepEnded(BasePlayer basePlayer)
        {
            if (sleepTimers.ContainsKey(basePlayer.UserIDString)) sleepTimers[basePlayer.UserIDString].Destroy();

            var player = players.FindPlayerById(basePlayer.UserIDString);
            if (player == null) return;

            Message(player, "Restored");
            if (basePlayer.metabolism.calories.value < 40) Message(player, "YouAreHungry");
            if (basePlayer.metabolism.dirtyness.value > 0) Message(player, "YouAreDirty");
            if (basePlayer.metabolism.hydration.value < 40) Message(player, "YouAreThirsty");
        }

        #endregion

        #region Command

        private void SleepCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permAllow))
            {
                Message(player, "NotAllowed");
                return;
            }

            var basePlayer = player.Object as BasePlayer;
            basePlayer?.StartSleeping();

            sleepTimers[player.Id] = timer.Every(10f, () =>
            {
                if (!player.IsSleeping)
                {
                    sleepTimers[player.Id].Destroy();
                    return;
                }

                Restore(basePlayer);
            });

            Message(player, "WentToSleep");
        }

        #endregion

        #region Helpers

        private void AddCommandAliases(string key, string command)
        {
            foreach (var language in lang.GetLanguages(this))
            {
                var messages = lang.GetMessages(language, this);
                foreach (var message in messages.Where(m => m.Key.StartsWith(key))) AddCovalenceCommand(message.Value, command);
            }
        }

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private void Message(IPlayer player, string key, params object[] args) => player.Reply(Lang(key, player.Id, args));

        #endregion
    }
}