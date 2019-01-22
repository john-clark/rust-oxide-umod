using Oxide.Core;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("OnScreenLogo", "Vlad-00003", "1.1.5", ResourceId = 2601)]
    [Description("Displays the button on the player screen.")]
    //Author info:
    //E-mail: Vlad-00003@mail.ru
    //Vk: vk.com/vlad_00003
    class OnScreenLogo : RustPlugin
    {
        private string PanelName = "GsAdX1wazasdsHs";
        private string Image = null;
        static OnScreenLogo instance;

        #region Config Setup
        private string Amax = "0.34 0.105";
        private string Amin = "0.26 0.025";
        private string ImageAddress = "https://fedoraproject.org/w/uploads/e/ee/Edition-server-full_one-color_black.png";
        #endregion

        #region Initialization
        protected override void LoadDefaultConfig()
        {
            PrintWarning("Generating default configuration file...");
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            GetConfig("Image. Link or name of the file in the data folder", ref ImageAddress);
            GetConfig("Minimum anchor", ref Amin);
            GetConfig("Maximum anchor", ref Amax);
            if (!ImageAddress.ToLower().Contains("http"))
            {
                ImageAddress = "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + ImageAddress;
            }
            permission.RegisterPermission("OnScreenLogo.refresh", this);
            LoadData();
            SaveConfig();
        }
        private void LoadData()
        {
            try
            {
                Image = Interface.Oxide.DataFileSystem.ReadObject<string>(Title);
            }
            catch (Exception ex)
            {
                if(ex is MissingMethodException)
                {
                    Image = null;
                    return;
                }
                RaiseError($"Failed to load data file. ({ex.Message})\nThe image would now be re-downloaded.");
                Image = null;
            }
        }
        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Title, Image);
        }
        void OnServerInitialized()
        {
            instance = this;
            if (Image == null)
                DownloadImage();
            else
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                {
                    CreateButton(player);
                }
        }
        void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, PanelName);
            }
        }
        #endregion

        #region Image Downloading
        [ConsoleCommand("osl.refresh")]
        private void cmdRedownload(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && arg.Connection.player != null)
            {
                BasePlayer player = arg.Connection.player as BasePlayer;

                if (!permission.UserHasPermission(player.UserIDString, "OnScreenLogo.refresh"))
                    return;
            }
            DownloadImage();
        }
        private void DownloadImage()
        {
            PrintWarning("Downloading image...");
            ServerMgr.Instance.StartCoroutine(DownloadImage(ImageAddress));
        }
        IEnumerator DownloadImage(string url)
        {
            using (var www = new WWW(url))
            {
                yield return www;
                if (instance == null) yield break;
                if (www.error != null)
                {
                    PrintError($"Failed to add image. File address possibly invalide\n {url}");
                }
                else
                {
                    var tex = www.texture;
                    byte[] bytes = tex.EncodeToPNG();
                    Image = FileStorage.server.Store(bytes, FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID).ToString();
                    SaveData();
                    PrintWarning("Image download is complete.");
                    foreach (BasePlayer player in BasePlayer.activePlayerList)
                    {
                        CreateButton(player);
                    }
                    UnityEngine.Object.DestroyImmediate(tex);
                    yield break;
                }
            }
        }
        #endregion

        #region UI
        void OnPlayerSleepEnded(BasePlayer player) => CreateButton(player);
        private void CreateButton(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, PanelName);
            CuiElementContainer elements = new CuiElementContainer();
            var panel = elements.Add(new CuiPanel
            {
                Image = {
                    Color = "0.00 0.00 0.00 0.00"
                },
                RectTransform = {
                    AnchorMin = Amin,
                    AnchorMax = Amax
                },
                CursorEnabled = false
            }, "Overlay", PanelName);
            var comp = new CuiRawImageComponent { Sprite = "assets/content/textures/generic/fulltransparent.tga" };
            if (Image != null)
            {
                comp.Png = Image;
            }
            elements.Add(new CuiElement
            {
                Parent = PanelName,
                Components =
                {
                    comp,
                    new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });
            CuiHelper.AddUi(player, elements);
        }
        #endregion

        #region Helpers
        private void GetConfig<T>(string Key, ref T var)
        {
            if (Config[Key] != null)
            {
                var = (T)Convert.ChangeType(Config[Key], typeof(T));
            }
            Config[Key] = var;
        }
        #endregion
    }
}