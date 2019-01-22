using System;
using System.Collections.Generic;
using ProtoBuf;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("NoRecoil", "Kappasaurus", "1.0.6")]
    class NoRecoil : RustPlugin
    {
        #region Variables

        private bool banEnabled;
        private bool kickEnabled = true;
        private float recoilTimer = 0.6f;


        private readonly Dictionary<ulong, NoRecoilData> data = new Dictionary<ulong, NoRecoilData>();
        private readonly Dictionary<ulong, Timer> detections = new Dictionary<ulong, Timer>();
        private readonly int detectionDiscardSeconds = 300;
        private readonly int violationProbability = 30;
        private readonly int maximumViolations = 30;
        private readonly Dictionary<string, int> probabilityModifiers = new Dictionary<string, int>() {
            {"weapon.mod.muzzleboost", -5},
            {"weapon.mod.silencer", 5},
            {"weapon.mod.holosight", 5},
            {"crouching", 8},
            {"aiming", 5}

        };

        private readonly List<string> blacklistedAttachments = new List<string>()
        {
            "weapon.mod.muzzlebreak",
            "weapon.mod.lasersight",
            "weapon.mod.small.scope"
        };

        #endregion

        #region NoRecoilData Class

        public class NoRecoilData
        {
            public int Ticks = 0;
            public int Count;
            public int Violations;
        }

        #endregion

        #region Init

        void Init() => LoadConfig();

        #endregion

        #region Hooks

        private void OnWeaponFired(BaseProjectile projectile, BasePlayer player, ItemModProjectile mod, ProjectileShoot projectileShoot)
        {

            var item = player.GetActiveItem();
            if (!(item.info.shortname == "rifle.ak" || item.info.shortname == "lmg.m249"))
                return;

            if (item.contents.itemList.Any(x => blacklistedAttachments.Contains(x.info.shortname)))
                return;

            NoRecoilData info;
            if (!data.TryGetValue(player.userID, out info))
                data.Add(player.userID, info = new NoRecoilData());

            UnityEngine.Vector3 eyesDirection = player.eyes.HeadForward();

            if (eyesDirection.y < -0.80)
                return;

            info.Ticks++;

            int probModifier = 0;
            foreach (Item attachment in item.contents.itemList)
                if (probabilityModifiers.ContainsKey(attachment.info.shortname))
                    probModifier += probabilityModifiers[attachment.info.shortname];

            if (player.modelState.aiming && probabilityModifiers.ContainsKey("aiming"))
                probModifier += probabilityModifiers["aiming"];

            if (player.IsDucked() && probabilityModifiers.ContainsKey("crouching"))
                probModifier += probabilityModifiers["crouching"];

            Timer detectionTimer;
            if (detections.TryGetValue(player.userID, out detectionTimer))
                detectionTimer.Reset(detectionDiscardSeconds);
            else
                detections.Add(player.userID, timer.Once(detectionDiscardSeconds, delegate ()
                {
                    if (info.Violations > 0)
                        info.Violations--;
                }));

            timer.Once(recoilTimer, () =>
            {
                ProcessRecoil(projectile, player, mod, projectileShoot, info, probModifier, eyesDirection);
            });

        }

        #endregion

        #region Config

        private new void LoadConfig()
        {
            GetConfig(ref banEnabled, "Automatic banning enabled");
            GetConfig(ref kickEnabled, "Automatic kicking enabled");
            GetConfig(ref recoilTimer, "Developer option, use with care", "Recoil timer");

            SaveConfig();
        }

        #endregion

        #region Helpers

        protected override void LoadDefaultConfig() => PrintWarning("Generating new configuration file...");

        private void GetConfig<T>(ref T variable, params string[] path)
        {
            if (path.Length == 0)
                return;

            if (Config.Get(path) == null)
            {
                Config.Set(path.Concat(new object[] { variable }).ToArray());
                PrintWarning($"Added field to config: {string.Join("/", path)}");
            }

            variable = (T)Convert.ChangeType(Config.Get(path), typeof(T));
        }

        private void ProcessRecoil(BaseProjectile projectile, BasePlayer player, ItemModProjectile mod, ProjectileShoot projectileShoot, NoRecoilData info, int probModifier, UnityEngine.Vector3 eyesDirection)
        {
            var nextEyesDirection = player.eyes.HeadForward();
            if (Math.Abs(nextEyesDirection.y - eyesDirection.y) < .009 &&
                nextEyesDirection.y < .8) info.Count++;

            if (info.Ticks <= 10) return;

            var prob = 100 * info.Count / info.Ticks;

            if (prob > ((100 - violationProbability) + probModifier))
            {
                info.Violations++;
                PrintWarning($"{player.displayName} ({player.UserIDString}), {prob}% probability, {info.Violations.ToString()} violations.");
                LogToFile("violations", $"[{DateTime.Now.ToString()}] {player.displayName} ({player.UserIDString}), {prob}% probability, {info.Violations.ToString()} violations.", this, false);

                                 if (info.Violations > maximumViolations)
                                    if (banEnabled)
                                        Player.Ban(player, "Recoil Scripts");
                                    else if (kickEnabled)
                                        Player.Kick(player, "Recoil Scripts"); 
            }

            foreach (BasePlayer _player in BasePlayer.activePlayerList)
                if (_player.IsAdmin && prob > ((100 - violationProbability) + probModifier))
                    SendReply(_player, $"<size=12>NoRecoil: {player.displayName} ({player.UserIDString}), {prob}% probability, {info.Violations.ToString()} violations.</size>");

            info.Ticks = 0;
            info.Count = 0;
        }

        #endregion
    }
}