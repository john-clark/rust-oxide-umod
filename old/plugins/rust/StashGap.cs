using System;
using System.Collections.Generic;
using System.Linq;
using Rust;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Stash Gap", "nivex", "0.1")]
    [Description("Bring balance to stashes.")]
    class StashGap : RustPlugin
    {
        void OnServerInitialized()
        {
            LoadMessages();
            LoadVariables();

            int updated = 0, existing = 0;
            var scale = new Vector3(stashBalanceWidth, stashBalanceHeight, stashBalanceWidth);

            foreach (var entity in BaseEntity.serverEntities.Where(e => e is StashContainer).ToList())
            {
                if (entity.gameObject.transform.localScale != scale)
                {
                    entity.gameObject.transform.localScale = scale;
                    entity.gameObject.layer = (int)Layer.Prevent_Building;
                    updated++;
                }
                else
                    existing++;
            }

            Puts(msg("Updated", null, updated, existing));
        }

        void OnEntityBuilt(Planner planner, GameObject gameObject)
        {
            if (gameObject?.ToBaseEntity() is StashContainer)
            {
                gameObject.transform.localScale = new Vector3(stashBalanceWidth, stashBalanceHeight, stashBalanceWidth);
                gameObject.layer = (int)Layer.Prevent_Building;
            }
        }

        void OnHammerHit(BasePlayer player, HitInfo info)
        {
            if (!player.IsAdmin || info?.HitEntity == null || !(info.HitEntity is StashContainer))
                return;

            var stash = (StashContainer) info.HitEntity;
            string contents = GetContents(stash);

            if (string.IsNullOrEmpty(contents))
                contents = msg("NoInventory", player.UserIDString);

            player.ChatMessage(msg("Owner", player.UserIDString, covalence.Players.FindPlayerById(stash.OwnerID.ToString())?.Name ?? msg("Unknown", player.UserIDString), stash.OwnerID));
            player.ChatMessage(msg("Contents", player.UserIDString, contents));
        }

        void cmdStashGap(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
                return;

            int drawn = 0;

            foreach(var stash in BaseEntity.serverEntities.Where(e => e is StashContainer).Cast<StashContainer>().ToList())
            {
                double distance = Math.Round(Vector3.Distance(stash.transform.position, player.transform.position), 2);

                if (distance > drawDistance)
                    continue;

                string text = showDistantContents ? string.Format("S <color=orange>{0}</color> {1}", distance, GetContents(stash)) : string.Format("S <color=orange>{0}</color>", distance);

                player.SendConsoleCommand("ddraw.text", drawTime, Color.yellow, stash.transform.position, text);
                drawn++;
            }

            if (drawn > 0)
                player.ChatMessage(msg("Drawn", player.UserIDString, drawn, drawDistance));
            else
                player.ChatMessage(msg("None", player.UserIDString, drawDistance));
        }

        string GetContents(StashContainer stash)
        {
            var items = stash.inventory?.itemList?.Select(item => string.Format("{0} ({1})", item.info.displayName.translated, item.amount))?.ToArray() ?? new string[0];

            return items.Length > 0 ? string.Join(", ", items) : string.Empty;
        }

        #region Config
        private bool Changed;
        string szChatCommand;
        float drawTime;
        float drawDistance;
        bool showDistantContents;
        float stashBalanceHeight;
        float stashBalanceWidth;

        void LoadMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Updated"] = "{0} new stashes updated. {1} stashes already updated.",
                ["Unknown"] = "No owner",
                ["Owner"] = "<color=yellow>Owner</color>: {0} ({1})",
                ["Contents"] = "<color=yellow>Contents</color>: {0}",
                ["Drawn"] = "Showing <color=yellow>{0}</color> stashes within <color=orange>{1}m</color>",
                ["None"] = "No stashes within <color=orange>{0}m</color>.",
                ["NoInventory"] = "No inventory.",
            }, this);
        }

        void LoadVariables()
        {
            szChatCommand = Convert.ToString(GetConfig("Settings", "Command Name", "sg"));

            if (!string.IsNullOrEmpty(szChatCommand))
                cmd.AddChatCommand(szChatCommand, this, cmdStashGap);

            drawTime = Convert.ToSingle(GetConfig("Settings", "Draw Time", 30f));
            drawDistance = Convert.ToSingle(GetConfig("Settings", "Draw Distance", 500f));
            showDistantContents = Convert.ToBoolean(GetConfig("Settings", "Show Distant Contents", true));
            stashBalanceHeight = Convert.ToSingle(GetConfig("Settings", "Balance Height", 3f));
            stashBalanceWidth = Convert.ToSingle(GetConfig("Settings", "Balance Width", 2f));

            if (Changed)
            {
                SaveConfig();
                Changed = false;
            }
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            Config.Clear();
            LoadVariables();
        }

        object GetConfig(string menu, string datavalue, object defaultValue)
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

        string msg(string key, string id = null, params object[] args) => string.Format(id == null ? RemoveFormatting(lang.GetMessage(key, this, id)) : lang.GetMessage(key, this, id), args);
        string RemoveFormatting(string source) => source.Contains(">") ? System.Text.RegularExpressions.Regex.Replace(source, "<.*?>", string.Empty) : source;

        #endregion
    }
}
