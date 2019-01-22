using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Instant Research", "Artasan/Tori1157", "2.0.3", ResourceId = 1318)]
    [Description("Allows control over research speed.")]
    public class InstantResearch : RustPlugin
    {
        #region Fields

        private bool Changed;
        private bool Override;

        private float researchSpeed;

        private const string instantPermission = "instantresearch.instant";
        private const string controlledPermission = "instantresearch.controlled";

        #endregion

        #region Loaded

        private void Init()
        {
            permission.RegisterPermission("instantresearch.instant", this);
            permission.RegisterPermission("Instantresearch.controlled", this);

            LoadVariables();
        }

        private void LoadVariables()
        {
            researchSpeed = Convert.ToSingle(GetConfig("Options", "Research Speed", 10));
            Override = Convert.ToBoolean(GetConfig("Options", "Override 'Controlled' with 'Instant'", false));

            if (Changed)
            {
                SaveConfig();
                Changed = false;
            }
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new config file");
            Config.Clear();
            LoadVariables();
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Both Permission"] = "<color=red>You can't have both permissions!</color>\n\nNow using default research speed, contact an <color=cyan>Administrator</color> to fix the issue.",
            }, this);
        }

        #endregion

        #region Functions

        private void OnItemResearch(ResearchTable table, Item targetItem, BasePlayer player)
        {
            if (player == null) return;

            if (CanInstant(player) && CanControlled(player))
            {
                if (Override)
                {
                    table.researchDuration = 0f;
                    return;
                }

                rust.SendChatMessage(player, "", lang.GetMessage("Both Permission", this, player.UserIDString));
                table.researchDuration = 10f;
                return;
            }

            if (!CanControlled(player) && !CanInstant(player))
            {
                table.researchDuration = 10f;
                return;
            }

            if (CanInstant(player))
            {
                table.researchDuration = 0f;
            }

            if (CanControlled(player))
            {
                table.researchDuration = researchSpeed;
            }
        }

        #endregion

        #region Helpers

        bool CanInstant(BasePlayer player)
        {
            return permission.UserHasPermission(player.UserIDString, instantPermission);
        }

        bool CanControlled(BasePlayer player)
        {
            return permission.UserHasPermission(player.UserIDString, controlledPermission);
        }

        private object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;

            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                Changed = true;
            }

            object value;

            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                Changed = true;
            }
            return value;
        }

        #endregion
    }
}