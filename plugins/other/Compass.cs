using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Compass", "bazuka5801", "1.0.1", ResourceId = 2605)]
    class Compass : RustPlugin
    {
        private class CompassPlayer : MonoBehaviour
        {
            private new Transform transform;
            private BasePlayer player;

            private float current = 0f;
            private float last = 0f;

            private const float Start = -0.383f;
            private const float Finish = -0.9715f;
            void Awake()
            {
                transform = GetComponent<Transform>();
                player = GetComponent<BasePlayer>();
                DrawCompass();
            }

            public void OnTick()
            {
                current = Mathf.Lerp(Start, Finish, player.eyes.rotation.eulerAngles.y/360);
                if (NeedRedraw())
                    DrawCompass();
            }

            public void SetCurrent(float main) => current = main;

            public void DrawCompass()
            {
                CuiHelper.DestroyUi(player, "bazuka5801.compass");
                CuiHelper.DestroyUi( player, "bazuka5801.compassHandle" );
                CuiHelper.AddUi(player, Format( CompassJSON, current, current + CompassWidth));
            }

            private bool NeedRedraw()
            {
                if (Math.Abs(current - last) > 0.0004)
                {
                    last = current;
                    return true;
                }
                return false;
            }

            private string Format(string input, params object[] args)
            {
                for (int i = 0; i < args.Length; i++)
                    input = input.Replace($"{{{i}}}", args[i].ToString());
                return input;
            }
        }

        #region Fields

        private static string CompassJSON;
        private static float CompassWidth = 2.354167f;
        private bool loaded = false;
        private Dictionary<BasePlayer, CompassPlayer> compassPlayers = new Dictionary<BasePlayer, CompassPlayer>();
        #endregion

        #region CANFIG


        object GetVariable( string menu, string datavalue, object defaultValue )
        {
            var data = Config[ menu ] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[ menu ] = data;
            }
            object value;
            if (!data.TryGetValue( datavalue, out value ))
            {
                value = defaultValue;
                data[ datavalue ] = value;
            }
            return value;
        }
        protected override void LoadDefaultConfig()
        {
            updateRate = float.Parse(GetVariable("Main", "Refresh Time", 30).ToString() );
            SaveConfig();
        }

        private float updateRate;
        #endregion

        #region Oxide Hooks

        void OnServerInitialized()
        {
            LoadDefaultConfig();
            ServerMgr.Instance.StartCoroutine(LoadImages());
            timer.Every(1/30f, CompassTick);
        }

        void Unload()
        {
            foreach (var player in compassPlayers.Keys.ToList())
            {
                DestroyCompassPlayer(player);
            }
            m_FileManager.SaveData();
            UnityEngine.Object.Destroy( FileManagerObject );
        }

        void OnPlayerInit( BasePlayer player )
        {
            if (player.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot))
            {
                timer.Once(0.1f, () => OnPlayerInit(player));
                return;
            }

            CreateCompassPlayer(player);
        }

        void OnPlayerDisconnected( BasePlayer player )
        {
            DestroyCompassPlayer(player);
        }

        #endregion

        #region Core

        void CreateCompassPlayer(BasePlayer player)
        {
            var cPlayer = player.gameObject.AddComponent<CompassPlayer>();

            compassPlayers[player] = cPlayer;
        }

        void DestroyCompassPlayer( BasePlayer player )
        {
            CompassPlayer cPlayer;
            if (!compassPlayers.TryGetValue( player, out cPlayer )) return;
            UnityEngine.Object.Destroy(cPlayer);
            compassPlayers.Remove(player);
        }

        void CompassTick()
        {
            foreach (var cPlayer in compassPlayers.Values)
            {
                cPlayer.OnTick();
            }
        }

        void OnLoaded()
        {
            var cont = new CuiElementContainer();
            cont.Add(new CuiElement()
            {
                Name = $"bazuka5801.compass",
                Parent = "Hud",
                Components =
                {
                    new CuiRawImageComponent()
                    {
                        Png = images["compass"],
                        Sprite = "assets/content/textures/generic/fulltransparent.tga"
                    },
                    new CuiOutlineComponent()
                    {
                        Distance = "0.4 -0.4"
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "{0} 0.9360",
                        AnchorMax = "{1} 0.9676"
                    }
                }
            });
            cont.Add(new CuiPanel()
            {
                Image = {Color = "0 0 0 1"},
                RectTransform = {AnchorMin = "0.4985677 0.9041489", AnchorMax = "0.5014323 0.9884187"}
            }, "Hud", "bazuka5801.compassHandle");
            CompassJSON = cont.ToJson();
            foreach (var player in BasePlayer.activePlayerList)
            {
                CreateCompassPlayer( player );
            }
        }

        #endregion

        #region Images

        IEnumerator LoadImages()
        {
            InitFileManager();
            foreach (var name in images.Keys.ToList())
            {
                yield return m_FileManager.StartCoroutine( m_FileManager.LoadFile( name, images[ name ]) );
                images[ name ] = m_FileManager.GetPng( name );
            }
            Puts( "Images uploaded" );
            loaded = true;
            OnLoaded();
        }

        Dictionary<string,string> images  =new Dictionary<string, string>()
        {
            ["compass"] = "http://i.imgur.com/IVHP1yc.png"
        };

        #endregion

        #region File Manager

        private GameObject FileManagerObject;
        private FileManager m_FileManager;

        /// <summary>
        /// Инициализация скрипта взаимодействующего с файлами сервера
        /// </summary>
        void InitFileManager()
        {
            FileManagerObject = new GameObject( "FileManagerObject" );
            m_FileManager = FileManagerObject.AddComponent<FileManager>();
        }

        class FileManager : MonoBehaviour
        {
            int loaded = 0;
            int needed = 0;

            public bool IsFinished => needed == loaded;
            const ulong MaxActiveLoads = 10;
            Dictionary<string, FileInfo> files = new Dictionary<string, FileInfo>();

            DynamicConfigFile dataFile = Interface.Oxide.DataFileSystem.GetFile( "Compass/Images" );

            private class FileInfo
            {
                public string Url;
                public string Png;
            }

            public void SaveData()
            {
                dataFile.WriteObject( files );
            }

            public string GetPng( string name ) => files[ name ].Png;

            private void Awake()
            {
                files = dataFile.ReadObject<Dictionary<string, FileInfo>>() ?? new Dictionary<string, FileInfo>();
            }

            public IEnumerator LoadFile( string name, string url)
            {
                if (files.ContainsKey( name ) && files[ name ].Url == url && !string.IsNullOrEmpty( files[ name ].Png )) yield break;
                files[ name ] = new FileInfo() { Url = url };
                needed++;
                yield return StartCoroutine( LoadImageCoroutine( name, url ) );
            }

            IEnumerator LoadImageCoroutine( string name, string url)
            {
                using (WWW www = new WWW( url ))
                {
                    yield return www;
                    if (string.IsNullOrEmpty( www.error ))
                    {
                        var bytes = www.bytes;
                        var entityId = CommunityEntity.ServerInstance.net.ID;
                        var crc32 = FileStorage.server.Store( bytes, FileStorage.Type.png, entityId ).ToString();
                        files[ name ].Png = crc32;
                    }
                }
                loaded++;
            }
        }

        #endregion
    }
}
