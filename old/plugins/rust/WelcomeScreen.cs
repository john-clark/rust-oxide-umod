using System.Linq;
using Newtonsoft.Json;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("WelcomeScreen", "Orange", "1.0.2")]
    [Description("Showing welcoming image on player joining")]
    public class WelcomeScreen : RustPlugin
    {
        #region Oxide Hooks

        private void OnPlayerInit(BasePlayer player)
        {
            CheckPlayer(player);
        }

        private void Unload()
        {
            OnEnd();
        }

        #endregion

        #region Commands

        [ChatCommand("welcomescreen")]
        private void Cmd(BasePlayer player)
        {
            CheckPlayer(player);
        }

        #endregion

        #region GUI

        private const string elem = "welcomescreen.main";

        private void CreateGUI(BasePlayer player)
        {
            DestroyGUI(player);

            var Container = new CuiElementContainer();

            Container.Add(new CuiElement
            {
                Name = elem,
                FadeOut = config.fadeOut,
                Components =
                {
                    new CuiRawImageComponent {Color = $"1 1 1 {config.transparency}", FadeIn = config.fadeIn, Url = config.url},

                    new CuiRectTransformComponent {AnchorMin = config.anchorMin, AnchorMax = config.anchorMax},
                }
            });

            CuiHelper.AddUi(player, Container);

            timer.Once(config.duration, () => { DestroyGUI(player); });
        }

        private void DestroyGUI(BasePlayer player)
        {
            if (player == null) {return;}
            CuiHelper.DestroyUi(player, elem);
        }

        #endregion

        #region Config
        
        private ConfigData config;
        
        private class ConfigData
        {    
            [JsonProperty(PropertyName = "Image URL")]
            public string url;
            
            [JsonProperty(PropertyName = "Fade-in duration")]
            public float fadeIn;
            
            [JsonProperty(PropertyName = "Fade-out duration")]
            public float fadeOut;
            
            [JsonProperty(PropertyName = "Delay after joining to create image")]
            public float delay;
            
            [JsonProperty(PropertyName = "Delay after creating image to start fade out")]
            public float duration;
            
            [JsonProperty(PropertyName = "Anchor min (left bottom coordinate)")]
            public string anchorMin;
            
            [JsonProperty(PropertyName = "Anchor min (right top coordinate)")]
            public string anchorMax;
            
            [JsonProperty(PropertyName = "Image transparency")]
            public float transparency;
        }
        
        private ConfigData GetDefaultConfig()
        {
            return new ConfigData
            {
                url = "https://i.imgur.com/RhMXzvF.jpg",
                fadeIn = 5f,
                fadeOut = 5f,
                delay = 10f,
                duration = 20f,
                anchorMin = "0 0",
                anchorMax = "1 1",
                transparency = 1f
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

        #region Helpers

        private void OnEnd()
        {
            foreach (var player in BasePlayer.activePlayerList.ToList())
            {
                DestroyGUI(player);
            }
        }

        private void CheckPlayer(BasePlayer player)
        {
            if (player == null) {return;}
            if (player.IsReceivingSnapshot)
            {
                timer.Once(3f, () => { CheckPlayer(player); });
                return;
            }

            timer.Once(config.delay, () => { CreateGUI(player); });
        }

        #endregion
    }
}