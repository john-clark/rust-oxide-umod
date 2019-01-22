using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Phantom Sleepers", "nivex", "0.1.3")]
    [Description("Create phantom sleepers to lure ESP hackers.")]
    class PhantomSleepers : RustPlugin
    {
        bool init = false;
        const string playerPrefab = "assets/prefabs/player/player.prefab";
        const ulong phantomId = 612306;

        List<object> DefaultShirts() => new List<object> { "tshirt", "tshirt.long", "shirt.tanktop", "shirt.collared" };
        List<object> DefaultPants() => new List<object> { "pants" };
        List<object> DefaultHelms() => new List<object> { "metal.facemask" };
        List<object> DefaultVests() => new List<object> { "metal.plate.torso" };
        List<object> DefaultGloves() => new List<object> { "burlap.gloves" };
        List<object> DefaultBoots() => new List<object> { "shoes.boots" };
        List<object> DefaultWeapons() => new List<object> { "pistol.semiauto" };

        static List<uint> sleepers = new List<uint>();

        class Phantom : MonoBehaviour
        {
            BasePlayer clone;
            BasePlayer phantom;
            List<Item> items;
            uint cloneID;

            void Awake()
            {
                phantom = GetComponent<BasePlayer>();
                phantom.health = 100f;

                items = new List<Item>();

                phantom.inventory.DoDestroy();
                phantom.inventory.ServerInit(phantom);
                phantom.inventory.ServerUpdate(0f);
                phantom.SetPlayerFlag(BasePlayer.PlayerFlags.Sleeping, true);

                if (names.Count == 0)
                {
                    int attempts = BasePlayer.sleepingPlayerList.Count;

                    if (attempts > 0)
                    {
                        do
                        {
                            clone = BasePlayer.sleepingPlayerList[UnityEngine.Random.Range(0, BasePlayer.sleepingPlayerList.Count - 1)];
                        } while (Vector3.Distance(clone.transform.position, phantom.transform.position) < minDistance && --attempts > 0);

                        phantom.displayName = clone.displayName;
                    }
                    else
                        phantom.displayName = phantomsName;
                }
                else
                    phantom.displayName = names.GetRandom();

                phantom.SendNetworkUpdateImmediate(true);
                Equip();

                if (clone != null)
                {
                    if (!sleepers.Contains(clone.net.ID))
                    {
                        sleepers.Add(clone.net.ID);
                        cloneID = clone.net.ID;
                    }
                }
            }

            void OnDestroy()
            {
                if (sleepers.Contains(cloneID))
                {
                    sleepers.Remove(cloneID);
                }

                if (stripPhantoms)
                    foreach (Item item in items)
                        item?.Remove(0.01f);

                if (phantom != null && !phantom.IsDestroyed)
                    phantom.Kill();

                items.Clear();
                GameObject.Destroy(this);
            }

            void Equip()
            {
                if (gloves.Count > 0)
                {
                    Item item = ItemManager.CreateByName(gloves.GetRandom());
                    item.skin = Convert.ToUInt64(item.info.skins.GetRandom().id);
                    item.MarkDirty();

                    item.MoveToContainer(phantom.inventory.containerWear, -1, false);
                    items.Add(item);
                }

                if (boots.Count > 0)
                {
                    Item item = ItemManager.CreateByName(boots.GetRandom());
                    item.skin = Convert.ToUInt64(item.info.skins.GetRandom().id);
                    item.MarkDirty();

                    item.MoveToContainer(phantom.inventory.containerWear, -1, false);
                    items.Add(item);
                }

                if (helms.Count > 0)
                {
                    Item item = ItemManager.CreateByName(helms.GetRandom());
                    item.skin = Convert.ToUInt64(item.info.skins.GetRandom().id);
                    item.MarkDirty();

                    item.MoveToContainer(phantom.inventory.containerWear, -1, false);
                    items.Add(item);
                }

                if (vests.Count > 0)
                {
                    Item item = ItemManager.CreateByName(vests.GetRandom());
                    item.skin = Convert.ToUInt64(item.info.skins.GetRandom().id);
                    item.MarkDirty();

                    item.MoveToContainer(phantom.inventory.containerWear, -1, false);
                    items.Add(item);
                }

                if (shirts.Count > 0)
                {
                    Item item = ItemManager.CreateByName(shirts.GetRandom());
                    item.skin = Convert.ToUInt64(item.info.skins.GetRandom().id);
                    item.MarkDirty();

                    item.MoveToContainer(phantom.inventory.containerWear, -1, false);
                    items.Add(item);
                }

                if (pants.Count > 0)
                {
                    Item item = ItemManager.CreateByName(pants.GetRandom());
                    item.skin = Convert.ToUInt64(item.info.skins.GetRandom().id);
                    item.MarkDirty();

                    item.MoveToContainer(phantom.inventory.containerWear, -1, false);
                    items.Add(item);
                }

                if (weapons.Count > 0)
                {
                    Item item = ItemManager.CreateByName(weapons.GetRandom());
                    item.skin = Convert.ToUInt64(item.info.skins.GetRandom().id);

                    if (item.skin != 0 && item.GetHeldEntity())
                        item.GetHeldEntity().skinID = item.skin;

                    item.MarkDirty();
                    item.MoveToContainer(phantom.inventory.containerBelt, -1, false);
                    items.Add(item);
                }
            }
        }

        void ccmdCreatePhantom(ConsoleSystem.Arg arg)
        {
            if (!init || !arg.IsAdmin || arg.Player() == null)
                return;

            var player = arg.Player();

            RaycastHit hit;
            if (!Physics.Raycast(player.eyes.HeadRay(), out hit, maxDistance))
            {
                player.ChatMessage(msg("Failure", player.UserIDString));
                return;
            }

            var phantom = GameManager.server.CreateEntity(playerPrefab, hit.point, player.transform.rotation, true) as BasePlayer;
            phantom.userID = phantomId;
            phantom.UserIDString = phantomId.ToString();
            phantom.Spawn();
            phantom.gameObject.AddComponent<Phantom>();
        }

        void OnServerInitialized()
        {
            LoadVariables();

            if (!destroyPhantomCorpses)
                Unsubscribe(nameof(OnEntitySpawned));

            if (!preventPhantomLooting)
                Unsubscribe(nameof(OnLootEntity));

            if (!hideSleepers)
                Unsubscribe(nameof(CanNetworkTo));

            init = true;
        }

        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (!init || entity == null || !(entity is PlayerCorpse))
                return;

            var corpse = entity.GetComponent<PlayerCorpse>();

            if (corpse.playerSteamID == phantomId && !corpse.IsDestroyed)
                corpse.Kill();
        }

        void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            if (init && entity?.GetComponent<Phantom>() != null && player && !player.IsAdmin)
                NextTick(() => player.EndLooting());
        }

        object CanNetworkTo(BaseNetworkable entity, BasePlayer target)
        {
            if (entity == null || target == null || entity == target || target.IsAdmin)
                return null;

            if (sleepers.Contains(entity.net.ID))
                return false;

            return null;
        }

        void Unload()
        {
            var objects = GameObject.FindObjectsOfType(typeof(Phantom));

            if (objects != null)
                foreach (var gameObj in objects)
                    GameObject.Destroy(gameObj);

            shirts.Clear();
            pants.Clear();
            helms.Clear();
            vests.Clear();
            gloves.Clear();
            boots.Clear();
            weapons.Clear();
            sleepers.Clear();
            names.Clear();
        }

        #region Config
        bool Changed;
        string szConsoleCommand;
        bool destroyPhantomCorpses;
        bool preventPhantomLooting;
        static bool stripPhantoms;
        static bool hideSleepers;
        float maxDistance;
        static float minDistance;
        static List<string> shirts = new List<string>();
        static List<string> pants = new List<string>();
        static List<string> helms = new List<string>();
        static List<string> vests = new List<string>();
        static List<string> gloves = new List<string>();
        static List<string> boots = new List<string>();
        static List<string> weapons = new List<string>();
        static List<string> names = new List<string>();
        static string phantomsName;

        void LoadVariables()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Failure"] = "Unable to find a position. Try looking at the ground or another object.",
            }, this);

            hideSleepers = Convert.ToBoolean(GetConfig("Invisibility", "Hide Real Sleepers", false));
            stripPhantoms = Convert.ToBoolean(GetConfig("Settings", "Strip Phantoms On Death", true));
            destroyPhantomCorpses = Convert.ToBoolean(GetConfig("Settings", "Destroy Phantom Corpses", true));
            preventPhantomLooting = Convert.ToBoolean(GetConfig("Settings", "Prevent Phantom Looting", true));
            szConsoleCommand = Convert.ToString(GetConfig("Settings", "Console Command", "createphantom"));
            maxDistance = float.Parse(GetConfig("Settings", "Max Raycast Distance", 100f).ToString());
            minDistance = float.Parse(GetConfig("Settings", "Min Distance From Real Sleeper", 450f).ToString());
            phantomsName = Convert.ToString(GetConfig("Settings", "Default Name If No Sleepers", "luke"));

            var _shirts = GetConfig("Gear", "Shirts", DefaultShirts()) as List<object>;
            var _pants = GetConfig("Gear", "Pants", DefaultPants()) as List<object>;
            var _helms = GetConfig("Gear", "Helms", DefaultHelms()) as List<object>;
            var _vests = GetConfig("Gear", "Vests", DefaultVests()) as List<object>;
            var _gloves = GetConfig("Gear", "Gloves", DefaultGloves()) as List<object>;
            var _boots = GetConfig("Gear", "Boots", DefaultBoots()) as List<object>;
            var _weapons = GetConfig("Gear", "Weapons", DefaultWeapons()) as List<object>;
            var _names = GetConfig("Random Names", "List", new List<object>()) as List<object>;

            if (_shirts.Count > 0)
                shirts.AddRange(_shirts.Select(shirt => shirt.ToString()));

            if (_pants.Count > 0)
                pants.AddRange(_pants.Select(pant => pant.ToString()));

            if (_helms.Count > 0)
                helms.AddRange(_helms.Select(helm => helm.ToString()));

            if (_vests.Count > 0)
                vests.AddRange(_vests.Select(vest => vest.ToString()));

            if (_gloves.Count > 0)
                gloves.AddRange(_gloves.Select(glove => glove.ToString()));

            if (_boots.Count > 0)
                boots.AddRange(_boots.Select(boot => boot.ToString()));

            if (_weapons.Count > 0)
                weapons.AddRange(_weapons.Select(weapon => weapon.ToString()));

            if (_names.Count > 0)
                names.AddRange(_names.Select(name => name.ToString()));

            if (!string.IsNullOrEmpty(szConsoleCommand))
                cmd.AddConsoleCommand(szConsoleCommand, this, nameof(ccmdCreatePhantom));

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
