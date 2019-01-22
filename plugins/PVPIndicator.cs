using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("PVPIndicator", "Orange", "1.2.0")]
    [Description("Makes logo on entering PVP zones or PVP mode")]
    public class PVPIndicator : RustPlugin
    {
        #region Vars

        [PluginReference] private Plugin ZoneManager;

        #endregion
        
        #region Oxide Hooks
        
        private void Unload()
        {
            OnEnd();
        }

        private void OnEnterZone(string ZoneID, BasePlayer player)
        {
            CheckZone(ZoneID, player, false);
        }

        private void OnExitZone(string ZoneID, BasePlayer player)
        {
            CheckZone(ZoneID, player, true);
        }

        #endregion

        #region Helpers

        private void OnEnd()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                DestroyGUI(player);
            }
        }
        
        private void CheckZone(string ZoneID, BasePlayer player, bool leave)
        {
            var name = ZoneManager?.Call<string>("GetZoneName", ZoneID) ?? "null";
            if (name != "DynamicPVP") {return;}

            if (leave)
            {
                DestroyGUI(player, elem);
            }
            else
            {
                DynamicPVPGUI(player);
            }
        }

        #endregion
        
        #region Config

        private ConfigData config;
        
        private class ConfigData
        {
            [JsonProperty(PropertyName = "DynamicPVP settings")]
            public OGUIOptions DynamicPVP = new OGUIOptions();

            public class OGUIOptions
            {
                [JsonProperty(PropertyName = "Mininal Anchor (left bottom coordinate)")]
                public string anchorMin;
                
                [JsonProperty(PropertyName = "Maximal Anchor (right top coordinate)")]
                public string anchorMax;

                [JsonProperty(PropertyName = "Link to image that will pop up (url)")]
                public string link;

                [JsonProperty(PropertyName = "Color of image")]
                public string color;
            }
        }
        
        private ConfigData GetDefaultConfig()
        {
            return new ConfigData
            {
                DynamicPVP = new ConfigData.OGUIOptions
                {
                    anchorMin = "0 0",
                    anchorMax = "0.1 0.1",
                    link = "https://i.imgur.com/JWrsJqI.jpg",
                    color = "1 1 1 1"
                }
            };
        }
        
        protected override void LoadConfig()
        {
            base.LoadConfig();
   
            try
            {
                config = Config.ReadObject<ConfigData>();
            }
            catch
            {
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            PrintError("Configuration file is corrupt(or not exists), creating new one!");
            config = GetDefaultConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        #endregion

        #region GUI

        private const string elem = "PVPIndicator.DynamicPVP";

        private void DynamicPVPGUI(BasePlayer player)
        {
            DestroyGUI(player, elem);
            
            var container = new CuiElementContainer();
            var cfg = config.DynamicPVP;

            container.Add(new CuiElement
            {
                Name = elem,
                Components =
                {
                    new CuiRawImageComponent {Url = cfg.link, Color = cfg.color},
                    new CuiRectTransformComponent {AnchorMin = cfg.anchorMin, AnchorMax = cfg.anchorMax},
                }
            });

            CuiHelper.AddUi(player, container);
        }

        private void DestroyGUI(BasePlayer player, string element = null)
        {
            if (element == null)
            {
                CuiHelper.DestroyUi(player, elem);
                return;
            }

            CuiHelper.DestroyUi(player, element);
        }

        #endregion
    }
}