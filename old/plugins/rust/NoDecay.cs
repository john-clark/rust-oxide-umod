using System;
using System.Collections.Generic;
using System.Text;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("NoDecay", "Diesel_42o", "1.0.32", ResourceId = 1160)]  //Original Credit to Deicide666ra/Piarb
    [Description("Scales or disables decay of items")]

    class NoDecay : RustPlugin
    {
        private float c_twigMultiplier;
        private float c_woodMultiplier;
        private float c_stoneMultiplier;
        private float c_sheetMultiplier;
        private float c_armoredMultiplier;

        private float c_campfireMultiplier;
        private float c_highWoodWallMultiplier;
        private float c_highStoneWallMultiplier;
        private float c_barricadeMultiplier;
        private float c_trapMultiplier;
        private float c_deployablesMultiplier;
        private float c_boxMultiplier;
        private float c_sedanMultiplier;
        private float c_samMultiplier;
        private float c_baloonMultiplier;
        private float c_furnaceMultiplier;
        private float c_bbqMultiplier;
        private float c_boatMultiplier;
        private float c_minicopterMultiplier;
        private float c_watchtowerMultiplier;

        private bool c_outputToRcon;

        private bool g_configChanged;
        private string entity_name;

        void Loaded() => LoadConfigValues();

        protected override void LoadDefaultConfig() => Puts("New configuration file created.");

        void LoadConfigValues()
        {
            c_twigMultiplier = Convert.ToSingle(GetConfigValue("Mutipliers", "twigMultiplier", 1.0));
            c_woodMultiplier = Convert.ToSingle(GetConfigValue("Mutipliers", "woodMultiplier", 0.0));
            c_stoneMultiplier = Convert.ToSingle(GetConfigValue("Mutipliers", "stoneMultiplier", 0.0));
            c_sheetMultiplier = Convert.ToSingle(GetConfigValue("Mutipliers", "sheetMultiplier", 0.0));
            c_armoredMultiplier = Convert.ToSingle(GetConfigValue("Mutipliers", "armoredMultiplier", 0.0));

            c_deployablesMultiplier = Convert.ToSingle(GetConfigValue("Mutipliers", "deployablesMultiplier", 0.0));
            c_watchtowerMultiplier = Convert.ToSingle(GetConfigValue("Mutipliers", "watchtowerMultiplier", 0.0));
            c_boxMultiplier = Convert.ToSingle(GetConfigValue("Mutipliers", "boxMultiplier", 0.0));
            c_sedanMultiplier = Convert.ToSingle(GetConfigValue("Mutipliers", "sedanMultiplier", 0.0));
            c_baloonMultiplier = Convert.ToSingle(GetConfigValue("Mutipliers", "baloonMultiplier", 0.0));
            c_furnaceMultiplier = Convert.ToSingle(GetConfigValue("Mutipliers", "furnaceMultiplier", 0.0));
            c_bbqMultiplier = Convert.ToSingle(GetConfigValue("Mutipliers", "bbqMultiplier", 0.0));
            c_samMultiplier = Convert.ToSingle(GetConfigValue("Mutipliers", "samMultiplier", 0.0));
            c_campfireMultiplier = Convert.ToSingle(GetConfigValue("Mutipliers", "campfireMultiplier", 0.0));
            c_barricadeMultiplier = Convert.ToSingle(GetConfigValue("Mutipliers", "barricadesMultiplier", 0.0));
            c_trapMultiplier = Convert.ToSingle(GetConfigValue("Mutipliers", "trapMultiplier", 0.0));
            c_highWoodWallMultiplier = Convert.ToSingle(GetConfigValue("Mutipliers", "highWoodWallMultiplier", 0.0));
            c_highStoneWallMultiplier = Convert.ToSingle(GetConfigValue("Mutipliers", "highStoneWallMultiplier", 0.0));
            c_boatMultiplier = Convert.ToSingle(GetConfigValue("Mutipliers", "boatMultiplier", 0.0));
            c_minicopterMultiplier = Convert.ToSingle(GetConfigValue("Mutipliers", "minicopterMultiplier", 0.0));

            c_outputToRcon = Convert.ToBoolean(GetConfigValue("Debug", "outputToRcon", false));

            if (g_configChanged)
            {
                Puts("Configuration file updated.");
                SaveConfig();
            }
        }

        object GetConfigValue(string category, string setting, object defaultValue)
        {
            var data = Config[category] as Dictionary<string, object>;
            object value;

            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[category] = data;
                g_configChanged = true;
            }

            if (data.TryGetValue(setting, out value)) return value;
            value = defaultValue;
            data[setting] = value;
            g_configChanged = true;
            return value;
        }

        [HookMethod("SendHelpText")]
        private void SendHelpText(BasePlayer player)
        {
            var sb = new StringBuilder();
            sb.Append("<color=#05eb59>NoDecay 1.0.3</color> · Controls decay\n");
            sb.Append("  · ").AppendLine($"twig={c_twigMultiplier} - campfire={c_campfireMultiplier}");
            sb.Append("  · ").Append($"wood ={ c_woodMultiplier} - stone ={ c_stoneMultiplier} - sheet ={ c_sheetMultiplier} - armored ={ c_armoredMultiplier}");
            player.ChatMessage(sb.ToString());
        }

        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            var tick = DateTime.Now;

            entity_name = entity.LookupPrefab().name;
            try
            {
                if (!hitInfo.damageTypes.Has(Rust.DamageType.Decay)) return;

                var block = entity as BuildingBlock;

                if (entity.LookupPrefab().name == "campfire" || entity.LookupPrefab().name == "skull_fire_pit")
                    ProcessCampfireDamage(hitInfo);
                else if (entity.LookupPrefab().name == "box.wooden.large" ||
                        entity.LookupPrefab().name == "woodbox_deployed" ||
                        entity.LookupPrefab().name == "CoffinStorage")
                {
                    var before = hitInfo.damageTypes.Get(Rust.DamageType.Decay);
                    hitInfo.damageTypes.Scale(Rust.DamageType.Decay, c_boxMultiplier);

                    if (c_outputToRcon)
                        Puts($"Decay ({entity_name}) before: {before} after: {hitInfo.damageTypes.Get(Rust.DamageType.Decay)}");
                }
                else if (entity.LookupPrefab().name.Contains("deployed") ||
                        entity.LookupPrefab().name.Contains("reinforced") ||
                        entity.LookupPrefab().name.Contains("shopfront") ||
                        entity.LookupPrefab().name.Contains("bars") ||
                        entity.LookupPrefab().name.Contains("shutter") ||
                        entity.LookupPrefab().name.Contains("netting") ||
                        entity.LookupPrefab().name.Contains("door") ||
                        entity.LookupPrefab().name.Contains("hatch") ||
                        entity.LookupPrefab().name.Contains("garagedoor") ||
                        entity.LookupPrefab().name.Contains("cell") ||
                        entity.LookupPrefab().name.Contains("fence") ||
                        entity.LookupPrefab().name.Contains("grill") ||
                        entity.LookupPrefab().name.Contains("Candle") ||
                        entity.LookupPrefab().name.Contains("Strobe") ||
                        entity.LookupPrefab().name.Contains("speaker") ||
                        entity.LookupPrefab().name.Contains("Fog") ||
                        entity.LookupPrefab().name.Contains("Graveyard"))
                {
                    var before = hitInfo.damageTypes.Get(Rust.DamageType.Decay);
                    hitInfo.damageTypes.Scale(Rust.DamageType.Decay, c_deployablesMultiplier);

                    if (c_outputToRcon)
                        Puts($"Decay ({entity_name}) before: {before} after: {hitInfo.damageTypes.Get(Rust.DamageType.Decay)}");
                }
                else if (entity.LookupPrefab().name.Contains("furnace"))
                {
                    var before = hitInfo.damageTypes.Get(Rust.DamageType.Decay);
                    hitInfo.damageTypes.Scale(Rust.DamageType.Decay, c_furnaceMultiplier);

                    if (c_outputToRcon)
                        Puts($"Decay ({entity_name}) before: {before} after: {hitInfo.damageTypes.Get(Rust.DamageType.Decay)}");
                }
                else if (entity.LookupPrefab().name.Contains("sedan"))
                {
                    var before = hitInfo.damageTypes.Get(Rust.DamageType.Decay);
                    hitInfo.damageTypes.Scale(Rust.DamageType.Decay, c_sedanMultiplier);

                    if (c_outputToRcon)
                        Puts($"Decay ({entity_name}) before: {before} after: {hitInfo.damageTypes.Get(Rust.DamageType.Decay)}");
                }
                else if (entity.LookupPrefab().name == "SAM_Static")
                {
                    var before = hitInfo.damageTypes.Get(Rust.DamageType.Decay);
                    hitInfo.damageTypes.Scale(Rust.DamageType.Decay, c_samMultiplier);

                    if (c_outputToRcon)
                        Puts($"Decay ({entity_name}) before: {before} after: {hitInfo.damageTypes.Get(Rust.DamageType.Decay)}");
                }
                else if (entity.LookupPrefab().name == "HotAirBalloon")
                {
                    var before = hitInfo.damageTypes.Get(Rust.DamageType.Decay);
                    hitInfo.damageTypes.Scale(Rust.DamageType.Decay, c_baloonMultiplier);

                    if (c_outputToRcon)
                        Puts($"Decay ({entity_name}) before: {before} after: {hitInfo.damageTypes.Get(Rust.DamageType.Decay)}");
                }
                else if (entity.LookupPrefab().name == "BBQ.Deployed")
                {
                    var before = hitInfo.damageTypes.Get(Rust.DamageType.Decay);
                    hitInfo.damageTypes.Scale(Rust.DamageType.Decay, c_bbqMultiplier);

                    if (c_outputToRcon)
                        Puts($"Decay ({entity_name}) before: {before} after: {hitInfo.damageTypes.Get(Rust.DamageType.Decay)}");
                }
                else if (entity.LookupPrefab().name.Contains("watchtower"))
                {
                    var before = hitInfo.damageTypes.Get(Rust.DamageType.Decay);
                    hitInfo.damageTypes.Scale(Rust.DamageType.Decay, c_watchtowerMultiplier);

                    if (c_outputToRcon)
                        Puts($"Decay ({entity_name}) before: {before} after: {hitInfo.damageTypes.Get(Rust.DamageType.Decay)}");
                }
                else if (entity.LookupPrefab().name == "WaterBarrel" ||
                        entity.LookupPrefab().name == "jackolantern.angry" ||
                        entity.LookupPrefab().name == "jackolantern.happy" ||
                        entity.LookupPrefab().name == "water_catcher_small" ||
                        entity.LookupPrefab().name == "water_catcher_large")
                {
                    var before = hitInfo.damageTypes.Get(Rust.DamageType.Decay);
                    hitInfo.damageTypes.Scale(Rust.DamageType.Decay, c_deployablesMultiplier);

                    if (c_outputToRcon)
                        Puts($"Decay ({entity_name}) before: {before} after: {hitInfo.damageTypes.Get(Rust.DamageType.Decay)}");
                }
                else if (entity.LookupPrefab().name == "beartrap" ||
                        entity.LookupPrefab().name == "landmine" ||
                        entity.LookupPrefab().name == "spikes.floor")
                {
                    var before = hitInfo.damageTypes.Get(Rust.DamageType.Decay);
                    hitInfo.damageTypes.Scale(Rust.DamageType.Decay, c_trapMultiplier);

                    if (c_outputToRcon)
                        Puts($"Decay ({entity_name}) before: {before} after: {hitInfo.damageTypes.Get(Rust.DamageType.Decay)}");
                }
                else if (entity.LookupPrefab().name.Contains("barricade"))
                {
                    var before = hitInfo.damageTypes.Get(Rust.DamageType.Decay);
                    hitInfo.damageTypes.Scale(Rust.DamageType.Decay, c_barricadeMultiplier);

                    if (c_outputToRcon)
                        Puts($"Decay ({entity_name}) before: {before} after: {hitInfo.damageTypes.Get(Rust.DamageType.Decay)}");
                }
                else if (entity.LookupPrefab().name == "gates.external.high.stone")
                {
                    var before = hitInfo.damageTypes.Get(Rust.DamageType.Decay);
                    hitInfo.damageTypes.Scale(Rust.DamageType.Decay, c_highStoneWallMultiplier);

                    if (c_outputToRcon)
                        Puts($"Decay (high stone gate) before: {before} after: {hitInfo.damageTypes.Get(Rust.DamageType.Decay)}");
                }
                else if (entity.LookupPrefab().name == "gates.external.high.wood")
                {
                    var before = hitInfo.damageTypes.Get(Rust.DamageType.Decay);
                    hitInfo.damageTypes.Scale(Rust.DamageType.Decay, c_highWoodWallMultiplier);

                    if (c_outputToRcon)
                        Puts($"Decay (high wood gate) before: {before} after: {hitInfo.damageTypes.Get(Rust.DamageType.Decay)}");
                }
                else if (entity.LookupPrefab().name == "wall.external.high.stone")
                {
                    var before = hitInfo.damageTypes.Get(Rust.DamageType.Decay);
                    hitInfo.damageTypes.Scale(Rust.DamageType.Decay, c_highStoneWallMultiplier);

                    if (c_outputToRcon)
                        Puts($"Decay (high stone wall) before: {before} after: {hitInfo.damageTypes.Get(Rust.DamageType.Decay)}");
                }
                else if (entity.LookupPrefab().name == "wall.external.high.wood")
                {
                    var before = hitInfo.damageTypes.Get(Rust.DamageType.Decay);
                    hitInfo.damageTypes.Scale(Rust.DamageType.Decay, c_highWoodWallMultiplier);

                    if (c_outputToRcon)
                        Puts($"Decay (high wood wall) before: {before} after: {hitInfo.damageTypes.Get(Rust.DamageType.Decay)}");
                }
                else if (entity.LookupPrefab().name == "mining.pumpjack")
                {
                    var before = hitInfo.damageTypes.Get(Rust.DamageType.Decay);
                    hitInfo.damageTypes.Scale(Rust.DamageType.Decay, 0.0f);

                    if (c_outputToRcon)
                        Puts($"Decay (pumpjack) before: {before} after: {hitInfo.damageTypes.Get(Rust.DamageType.Decay)}");
                }
                else if (entity.LookupPrefab().name == "Rowboat" ||
                        entity.LookupPrefab().name == "RHIB")
                {
                    var before = hitInfo.damageTypes.Get(Rust.DamageType.Decay);
                    hitInfo.damageTypes.Scale(Rust.DamageType.Decay, c_boatMultiplier);

                    if (c_outputToRcon)
                        Puts($"Decay ({entity_name}) before: {before} after: {hitInfo.damageTypes.Get(Rust.DamageType.Decay)}");
                }
                else if (entity.LookupPrefab().name == "minicopter.entity")

                {
                    var before = hitInfo.damageTypes.Get(Rust.DamageType.Decay);
                    hitInfo.damageTypes.Scale(Rust.DamageType.Decay, c_minicopterMultiplier);

                    if (c_outputToRcon)
                        Puts($"Decay ({entity_name}) before: {before} after: {hitInfo.damageTypes.Get(Rust.DamageType.Decay)}");
                }
                else if (block != null)
                    ProcessBuildingDamage(block, hitInfo);
                else
                    Puts($"Unsupported decaying entity detected: {entity.LookupPrefab().name} --- please notify author");
            }
            finally
            {
                var ms = (DateTime.Now - tick).TotalMilliseconds;

                if (ms > 10) Puts($"NoDecay.OnEntityTakeDamage took {ms} ms to execute.");
            }
        }

        void ProcessCampfireDamage(HitInfo hitInfo)
        {
            var before = hitInfo.damageTypes.Get(Rust.DamageType.Decay);
            hitInfo.damageTypes.Scale(Rust.DamageType.Decay, c_campfireMultiplier);
            if (c_outputToRcon)
                Puts($"Decay campfire before: {before} after: {hitInfo.damageTypes.Get(Rust.DamageType.Decay)}");
        }

        void ProcessBuildingDamage(BuildingBlock block, HitInfo hitInfo)
        {
            var multiplier = 1.0f;
            var isHighWall = block.LookupPrefab().name.Contains("wall.external");

            string type = "other";

            switch (block.grade)
            {
                case BuildingGrade.Enum.Twigs:
                    multiplier = c_twigMultiplier;
                    type = "twig";
                    break;
                case BuildingGrade.Enum.Wood:
                    if (isHighWall)
                    {
                        multiplier = c_highWoodWallMultiplier;
                        type = "high wood wall";
                    }
                    else
                    {
                        multiplier = c_woodMultiplier;
                        type = "wood";
                    }
                    break;
                case BuildingGrade.Enum.Stone:
                    if (isHighWall)
                    {
                        multiplier = c_highStoneWallMultiplier;
                        type = "high stone wall";
                    }
                    else
                    {
                        multiplier = c_stoneMultiplier;
                        type = "stone";
                    }
                    break;
                case BuildingGrade.Enum.Metal:
                    multiplier = c_sheetMultiplier;
                    type = "sheet";
                    break;
                case BuildingGrade.Enum.TopTier:
                    multiplier = c_armoredMultiplier;
                    type = "armored";
                    break;
            };

            var before = hitInfo.damageTypes.Get(Rust.DamageType.Decay);
            hitInfo.damageTypes.Scale(Rust.DamageType.Decay, multiplier);

            if (c_outputToRcon)
                Puts($"Decay ({type}) before: {before} after: {hitInfo.damageTypes.Get(Rust.DamageType.Decay)}");
        }
    }
}