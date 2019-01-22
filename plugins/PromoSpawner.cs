using UnityEngine;
using System.Collections.Generic;
using Oxide.Core;
using System;

namespace Oxide.Plugins
{
    [Info("PromoSpawner", "Vlad-00003", "1.1.0", ResourceId = 2410)]
    [Description("Spawns create on random place with note that contents promocode")]

    class PromoSpawner : RustPlugin
    {
        private string box = "assets/prefabs/deployable/large wood storage/box.wooden.large.prefab";
        //private List<ItemContainer> CreatedBoxes = new List<ItemContainer>();
        private Dictionary<ItemContainer, string> CreatedBoxes = new Dictionary<ItemContainer, string>();
        DateTime NextSpawn;
        Timer _timer;
        private PluginConfig config;
        private PluginData data;

        #region Config and Data
        
        private class PluginConfig
        {
            public int Tick { get; set; }
            public int SpawnPerTick { get; set; }
            public int BoxSkin { get; set; }
            public string TextBeforePromo { get; set; }
        }
        private class PluginData
        {
            public List<string> Promocodes { get; set; }
            public int Timer { get; set; }
            public List<string> SavedCodes { get; set; }
            public PluginData()
            {
                Promocodes = new List<string>()
                {
                };
                Timer = -1;
                SavedCodes = new List<string>();
            }
        }
        #endregion

        #region Default Config
        private PluginConfig DefaultConfig()
        {
            PluginConfig defaultConfig = new PluginConfig
            {
                Tick = 0,
                SpawnPerTick = 0,
                BoxSkin = 10122,
                TextBeforePromo = "Your personal Promocode is:"
            };
            return defaultConfig;
        }

        #endregion

        #region localization
        void LoadMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["No Permission"] = "You don't have permission to use this command.",
                ["Timer is running"] = "The timer is already running.",
                ["Start successfull"] = "Success! Promocodes start spawning.",
                ["Event stopped"] = "Event is stopped.",
                ["Promocode was looted"] = "Promocode at (X: {x} Y:{y} Z:{z}) was looted",
                ["New promocode created"] = "\nWe have created new promocode: {code} at X: {x} Y: {y} Z: {z}",
                ["Old promocode"] = "[OLD]Promocode \"{code}\" recreated at ({x};{y};{z})"
            }, this);
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["No Permission"] = "Недостаточно прав на выполнение данной команды.",
                ["Timer is running"] = "Таймер уже запущен.",
                ["Start successfull"] = "Удачно! Промокоды скоро начнут появляться на карте.",
                ["Event stopped"] = "Ивент остановлен.",
                ["Promocode was looted"] = "Промокод по координатам (X: {x} Y:{y} Z:{z}) забрали",
                ["New promocode created"] = "\nСоздан новый промокод {code} по координатам X: {x} Y: {y} Z: {z}",
                ["Old promocode"] = "[OLD]Промокод \"{code}\" воссоздан по координатам ({x};{y};{z})"
            }, this, "ru");
        }
        #endregion

        #region Initialization
        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(DefaultConfig(),true);//Запись стандартных конфигов
            PrintWarning("New configuration file created.");//Сообщаем, что создали новый конфиг
            LoadData();
            data.Promocodes.Add("Promocode 1");
            data.Promocodes.Add("Promocode 2");
            SaveData();
        }
        void LoadConfigValues()
        {
            config = Config.ReadObject<PluginConfig>();//Считываем конфиг
        }

        void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Title, data);
        }
        void LoadData()
        {
            try
            {
                data = Interface.Oxide.DataFileSystem.ReadObject<PluginData>(Title);
            }
            catch
            {
                data = new PluginData();
            }
        }
        void OnServerInitialized()
        {
            foreach (var code in data.SavedCodes)
            {
                var vec = GetRandomVector();
                CreateBox(code, vec);
                Puts(GetMsg("Old promocode").Replace("{code}",code).Replace("{x}", vec.x.ToString()).Replace("{y}", vec.y.ToString()).Replace("{z}", vec.z.ToString()));
            }
            data.SavedCodes.Clear();
            SaveData();

            foreach(var code in data.Promocodes)
            {
                Puts(code);
            }

            if (data.Timer >= 0)
            {
                NextSpawn = DateTime.Now.AddSeconds(data.Timer);
                _timer = timer.Once(data.Timer, () =>
                {
                    SpawnSeq();
                });
                return;
            }
        }
        void Loaded()
        {
            LoadConfigValues();
            LoadData();
            LoadMessages();

            if (config.Tick <= 0 || config.SpawnPerTick <= 0)
            {
                PrintWarning("Plugin config isn't set up - Tick and SpawnPerTick can't be 0");
                PrintWarning("Quiting...");
                Interface.Oxide.UnloadPlugin(Title);
                return;
            }
        }
        void Unload()
        {
            PrintWarning("Saving created promocodes, removing timers and existing codes...");
            LoadData();
            if (_timer != null && !_timer.Destroyed)
            {
                _timer.Destroy();
                int timeleft = (int)NextSpawn.Subtract(DateTime.Now).TotalSeconds;
                data.Timer = timeleft;
                SaveData();
            }
            foreach(var item in CreatedBoxes)
            {
                item.Key.entityOwner.Kill();
                data.SavedCodes.Add(item.Value);
                SaveData();
            }
        }
        #endregion

        #region Sub functions
        void CreateBox(string text,Vector3 pos)
        {
            var CreatedBox = GameManager.server.CreateEntity(box, pos) as StorageContainer;
            CreatedBox.transform.position = pos;
            //Скин не работает. Вернее работает не всегда. Причина не ясна
            CreatedBox.skinID = (ulong)config.BoxSkin;
            CreatedBox.Spawn();
            CreatedBox.transform.position.Normalize();
            CreatedBox.inventory.capacity = 1;
            var note = ItemManager.CreateByItemID(3387378, 1);
            note.text = text;
            note.MoveToContainer(CreatedBox.inventory);
            CreatedBoxes.Add(CreatedBox.inventory,text);
        }
        //Random Spawner by LaserHydra. LaserHydra, know what? You are awsome!!!!
        Vector3 GetRandomVector()
        {
            float max = ConVar.Server.worldsize / 2;

            float x = UnityEngine.Random.Range(max * (-1), max);
            //float y = UnityEngine.Random.Range(200, 300);
            float z = UnityEngine.Random.Range(max * (-1), max);

            object terrainHeight = GetTerrainHeight(new Vector3(x, 300, z));

            if (terrainHeight is Vector3)
                return (Vector3)terrainHeight;
            else
                return GetRandomVector();
                //return new Vector3(x, y, z);
        }

        object GetTerrainHeight(Vector3 location)
        {
            int mask = LayerMask.GetMask(new string[] { "Terrain", "World", "Construction" });
            float distanceToWater = location.y - TerrainMeta.WaterMap.GetHeight(location);
            RaycastHit rayHit;
            if (Physics.Raycast(new Ray(location, Vector3.down), out rayHit, distanceToWater, mask))
            {
                return rayHit.point;
            }

            return false;
        }
        #endregion

        void OnItemRemovedFromContainer(ItemContainer container, Item item)
        {
            if (CreatedBoxes.ContainsKey(container))
            {
                CreatedBoxes.Remove(container);
                var vec = container.entityOwner.transform.position;
                Puts(GetMsg("Promocode was looted").Replace("{x}", vec.x.ToString()).Replace("{y}", vec.y.ToString()).Replace("{z}", vec.z.ToString()));
                container.entityOwner.Kill();
            }
        }

        void SpawnSeq()
        {
            if (data.Promocodes.Count == 0)
            {
                data.Timer = -1;
                return;
            }

            int count;
            if (config.SpawnPerTick > data.Promocodes.Count)
            {
                count = data.Promocodes.Count;
            }else
            {
                count = config.SpawnPerTick;
            }

            for(int i = 0; i < count; i++)
            {
                var code = data.Promocodes[0];
                data.Promocodes.RemoveAt(0);
                var pos = GetRandomVector();
                Puts(GetMsg("New promocode created").Replace("{code}", code).Replace("{x}", pos.x.ToString()).Replace("{y}", pos.y.ToString()).Replace("{z}", pos.z.ToString()));
                code = config.TextBeforePromo + "\n" + code;
                CreateBox(code, pos);
            }
            NextSpawn = DateTime.Now.AddSeconds(config.Tick);
            SaveData();
            _timer = timer.Once(config.Tick, () =>
            {
                SpawnSeq();
            });
        }

        #region commands
        [ConsoleCommand("promo.start")]
        void ccStartSeq(ConsoleSystem.Arg args)
        {
            object uid = null;
            if(args.Player() != null)
            {
                uid = args.Player().userID;
            }
            if (args.Connection != null && args.Connection.authLevel < 2)
            {
                args.ReplyWith(GetMsg("No Permission", uid));
                return;
            }
            if(_timer!=null && !_timer.Destroyed)
            {
                args.ReplyWith(GetMsg("Timer is running",uid));
                return;
            }
            args.ReplyWith(GetMsg("Start successfull",uid));
            SpawnSeq();
        }

        [ChatCommand("startpromo")]
        void cmdStartSeq(BasePlayer player)
        {
            if (!player.IsAdmin)
            {
                PrintToChat(player,GetMsg("No Permission", player.userID));
                return;
            }
            if (_timer != null && !_timer.Destroyed)
            {
                PrintToChat(player,GetMsg("Timer is running", player.userID));
                return;
            }
            PrintToChat(player,GetMsg("Start successfull", player.userID));
            SpawnSeq();
        }
        [ConsoleCommand("promo.stop")]
        void ccStopSeq(ConsoleSystem.Arg args)
        {
            object uid = null;
            if (args.Player() != null)
            {
                uid = args.Player().userID;
            }
            if (args.Connection != null && args.Connection.authLevel < 2)
            {
                args.ReplyWith(GetMsg("No Permission",uid));
                return;
            }
            if (_timer != null && !_timer.Destroyed)
            {
                _timer.Destroy();
            }
            LoadData();
            data.Timer = -1;
            SaveData();
            args.ReplyWith(GetMsg("Event stopped",uid));
        }

        [ChatCommand("stoppromo")]
        void cmdStopSeq(BasePlayer player)
        {
            if (!player.IsAdmin)
            {
                PrintToChat(player,GetMsg("No Permission", player.userID));
                return;
            }
            if (_timer != null && !_timer.Destroyed)
            {
                _timer.Destroy();
            }
            LoadData();
            data.Timer = -1;
            SaveData();
            PrintToChat(player,GetMsg("Event stopped", player.userID));
        }
        #endregion

        #region Helpers
        string GetMsg(string key, object userID = null) => lang.GetMessage(key, this, userID == null ? null : userID.ToString());
        #endregion
    }
}