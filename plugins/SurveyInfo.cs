using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using UnityEngine;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("Survey Info", "Diesel_42o", "0.1.8", ResourceId = 2463)]
    [Description("Displays Loot from Survey Charges")]

    class SurveyInfo : RustPlugin
    {
        [PluginReference]
        Plugin GatherManager;

        private const string UsePermission = "surveyinfo.use";
        private string Icon;
        private string PrefixColor;
        private string ResultsAmountColor;
        private string ResultResourseNameColor;
        private bool configChanged;

        private readonly Hash<int, SurveyData> _activeSurveyCharges = new Hash<int, SurveyData>();
        private enum SurveyLootItemIdEnum { Stones = -892070738, MetalOre = -1059362949, MetalFrag = 688032252, SulfurOre = 889398893, HighQualityMetal = 2133577942 }

        private void Loaded()
        {
            LoadLang();
            LoadConfigValues();
            permission.RegisterPermission(UsePermission, this);
        }

        protected override void LoadDefaultConfig() => Puts("NEW configuration file created.");

        void LoadConfigValues()
        {
            Icon = Convert.ToString(GetConfig("Settings", "icon", "0"));
            PrefixColor = Convert.ToString(GetConfig("Colors", "PrefixColor", "#fa58ac")); // Pink
            ResultsAmountColor = Convert.ToString(GetConfig("Colors", "ResultsAmountColor", "#05eb59")); // Green
            ResultResourseNameColor = Convert.ToString(GetConfig("Colors", "ResultResourseNameColor", "#ffa500"));  // Orange
            SaveConfig();

            if (configChanged)
            {
                Puts("Configuration file UPDATED.");
                SaveConfig();
            }
        }

        object GetConfig(string category, string setting, object defaultValue)
        {
            var data = Config[category] as Dictionary<string, object>;
            object value;

            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[category] = data;
                configChanged = true;
            }

            if (data.TryGetValue(setting, out value)) return value;
            value = defaultValue;
            data[setting] = value;
            configChanged = true;
            return value;
        }

        private void LoadLang()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Prefix"] = "[ Survey Info ]",
            }, this);
        }

        private void Init()
        {
            if (GatherManager == null) return;

            Dictionary<string, object> defaultSurveyResourceModifiers = new Dictionary<string, object>();
            Dictionary<string, object> configSurveyResourceModifiers = GetConfig(GatherManager, "Options", "SurveyResourceModifiers", defaultSurveyResourceModifiers);
            Dictionary<string, float> surveyResourceModifiers = new Dictionary<string, float>();

            foreach (var entry in configSurveyResourceModifiers)
            {
                float rate;

                if (!float.TryParse(entry.Value.ToString(), out rate)) continue;
                surveyResourceModifiers.Add(entry.Key, rate);
            }

            if (surveyResourceModifiers.Count == 0) return;

            int newBestScore = 0;

            foreach (SurveyLootItemIdEnum item in Enum.GetValues(typeof(SurveyLootItemIdEnum)))
            {
                double gatherManagerMulitiplier = 1;
                float val;
                string itemName = ItemManager.FindItemDefinition((int)item).displayName.english;

                if (surveyResourceModifiers.TryGetValue(itemName, out val))
                    gatherManagerMulitiplier = val;
                else if (surveyResourceModifiers.TryGetValue("*", out val))
                    gatherManagerMulitiplier = val;
            }
        }

        private void OnSurveyGather(SurveyCharge survey, Item item)
        {
            Hash<int, SurveyItem> surveyItems = _activeSurveyCharges[survey.GetInstanceID()].Items;

            int itemId = item.info.itemid;

            if (surveyItems[itemId] != null)
            {
                surveyItems[itemId].Amount += item.amount;
            }
            else
            {
                surveyItems[itemId] = new SurveyItem(item.info.displayName.translated, item.amount);
            }
        }

        private void OnExplosiveThrown(BasePlayer player, BaseEntity entity)
        {
            if (!(entity is SurveyCharge)) return;

            SurveyData data = new SurveyData(entity.GetInstanceID());
            _activeSurveyCharges.Add(entity.GetInstanceID(), data);

            timer.Once(5.5f, () =>
            {
                if (data.Items.Count > 0)
                {
                    if (HasPermission(player, UsePermission))
                    {
                        DisplaySurveyLoot(player, data);
                    }
                }
            });
        }

        private void DisplaySurveyLoot(BasePlayer player, SurveyData data)
        {
            SendReply(player, "<color=" + GetConfig("Colors", "PrefixColor", player) + ">" + Lang("Prefix", player.UserIDString) + "</color>", null, Icon);

            foreach (KeyValuePair<int, SurveyItem> item in data.Items)
            {
                SendReply(player, "<color=" + GetConfig("Colors", "ResultsAmountColor", player) + ">" + item.Value.Amount + " x</color>" + " <color=" + GetConfig("Colors", "ResultResourseNameColor", player) + ">" + item.Value.DisplayName + "</color>", null, Icon);
            }
        }

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private bool HasPermission(BasePlayer player, string perm) => permission.UserHasPermission(player.UserIDString, perm);

        private T GetConfig<T>(Plugin plugin, string category, string setting, T defaultValue)
        {
            var data = plugin.Config[category] as Dictionary<string, object>;
            object value;

            if (data == null)
            {
                data = new Dictionary<string, object>();
                plugin.Config[category] = data;
            }
            if (data.TryGetValue(setting, out value)) return (T)Convert.ChangeType(value, typeof(T));
            value = defaultValue;
            data[setting] = value;
            return (T)Convert.ChangeType(value, typeof(T));
        }

        private class SurveyData
        {
            public Hash<int, SurveyItem> Items { get; }

            public SurveyData(int surveyId)
            {
                Items = new Hash<int, SurveyItem>();
            }

            public int GetAmountByItemId(int id)
            {
                return Items[id]?.Amount ?? 0;
            }
        }

        private class SurveyItem
        {
            public string DisplayName { get; }
            public int Amount { get; set; }

            public SurveyItem(string displayName, int amount)
            {
                DisplayName = displayName;
                Amount = amount;
            }
        }
    }
}