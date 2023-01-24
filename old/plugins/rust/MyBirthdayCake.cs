using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;   //String.
using Convert = System.Convert;
using Oxide.Core;   //storeddata

namespace Oxide.Plugins
{
    [Info("My Birthday Cake", "BuzZ[PHOQUE]", "0.0.1")]
    [Description("Throw a Birthday Cake Bomb")]

/*======================================================================================================================= 
*   THANKS TO THE OXIDE/UMOD TEAM for coding quality, ideas, and time spent for the community
*
*   13th december 2018
*   0.0.1   20181213    creation
*
*=======================================================================================================================*/

    public class MyBirthdayCake : RustPlugin
    {
        bool debug = false;
        private bool ConfigChanged;

        string Prefix = "[MBC] ";
        ulong SteamIDIcon = 76561198357983957;
        float flamingtime = 30f;
        string cakename = "Birthday SPECIAL EDITION";
        const string CakeChatCommand = "mybirthdaycake.use";
    
    class StoredData
    {
        public Dictionary<uint, Cake> CakeInDaWorld = new Dictionary<uint,Cake>();
        public StoredData()
        {
        }
    }
        private StoredData storedData;

    class Cake
    {
        public ulong playerownerID;
        public bool explosion;
        //public bool smokebirthday;
        public bool fire;
        public bool oilflames;
        public bool napalmflames;
        public bool flameonthrow;
    }

        void Init()
        {
            LoadVariables();
            permission.RegisterPermission(CakeChatCommand, this);
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
        }

#region MESSAGES

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"NoPermMsg", "You don't have permission to do that."},
                {"SpecifyMsg", "Please specify - explose - oil - napalm - fire"},
                {"GiftMsg", "Gift : Birthday Cake with {0} !"},
                {"WishMsg", "Wish a Happy Birthday !!"},
                {"NotYoursMsg", "This Cake is not yours ! It is no more a special one ..."},

            }, this, "en");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"NoPermMsg", "Vous n'avez pas la permission."},
                {"SpecifyMsg", "Merci de préciser - explose - oil - napalm - fire"},
                {"GiftMsg", "Cadeau : un beau gâteau {0} !"},
                {"WishMsg", "Souhaitez un Joyeux Zanniversaire!!"},
                {"NotYoursMsg", "Ce gâteau n'est pas à vous, suppression de ses capacités meurtrières ..."},

            }, this, "fr");
        }

#endregion

#region CONFIG

    protected override void LoadDefaultConfig()
        {
            LoadVariables();
        }

        private void LoadVariables()
        {
            SteamIDIcon = Convert.ToUInt64(GetConfig("Chat Settings", "SteamIDIcon", "76561198357983957"));        // SteamID FOR PLUGIN ICON - STEAM PROFILE CREATED FOR THIS PLUGIN / NONE YET /
            Prefix = Convert.ToString(GetConfig("Chat Settings", "Plugin Prefix", "[MBC] "));
            flamingtime = Convert.ToSingle(GetConfig("Flames Settings", "Duration in seconds", "30"));
            cakename = Convert.ToString(GetConfig("Cake Settings", "Name", "Birthday SPECIAL EDITION"));

            if (!ConfigChanged) return;
            SaveConfig();
            ConfigChanged = false;
        }

        private object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                ConfigChanged = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                ConfigChanged = true;
            }
            return value;
        }

#endregion

        void Unload()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);
        }

/////////////////////////////////////////////////////////////////////////////
        void OnMeleeThrown(BasePlayer player, Item item)
        {
            if (storedData.CakeInDaWorld.ContainsKey(item.uid) == false) return;
            if (debug) Puts($"BIRTHDAYCAKE !!! {item}");
            timer.Once(5f, () =>
            {
                BirthdayIn5Seconds(item.uid, item);
            });
        }

        void BirthdayIn5Seconds(uint cakeid, Item item)
        {
            BaseEntity entity = item.GetWorldEntity();
            if (entity == null)
            {
                if (debug) Puts("ENTITY NULL !");
                return;
            }
            Cake cake = new Cake();
            storedData.CakeInDaWorld.TryGetValue(cakeid, out cake);
            if (cake == null) return;
            // type of explosion
            ////if (cake.smokebirthday) BirthdayBoomBoom(item, "smoke", entity);
            if (cake.explosion) BirthdayBoomBoom(item, "explose", entity);
            // type of flames
            if (cake.oilflames) BirthdayBoomBoom(item, "oil", entity);
            if (cake.napalmflames) BirthdayBoomBoom(item, "napalm", entity);
            if (cake.fire) BirthdayBoomBoom(item, "fire", entity);
            NextFrame(() =>
            {
                entity.Kill();
                storedData.CakeInDaWorld.Remove(item.uid);
                });
            }
//////////////////
// DIFFERENTS CASES
//////////////////
        void BirthdayBoomBoom(Item item, string birthdaytype, BaseEntity entity)
        {
            if (debug) Puts("BoomBoom sequence");
            Vector3 daboom = new Vector3(entity.transform.position.x,entity.transform.position.y,entity.transform.position.z);
            if (debug) Puts($"Vector3 {daboom}");
            TimedExplosive boom = new TimedExplosive();
            //SmokeGrenade boomboom = new SmokeGrenade();
            if (birthdaytype == "explose")
            {
                BaseEntity GrenadeF1 = GameManager.server.CreateEntity("assets/prefabs/weapons/f1 grenade/grenade.f1.deployed.prefab", daboom, new Quaternion(), true);
                if (GrenadeF1 == null) if (debug) Puts("GrenadeF1 NULL BaseEntity ENTITY !!!!");
                else if (debug) Puts($"oright {GrenadeF1}");
                boom = GrenadeF1.GetComponent<TimedExplosive>();
                if (boom == null) Puts("boom NULL TimedExplosive ENTITY !!!!");
                if (debug) Puts("F1 BIRTHDAY !!!!");
                boom.Explode();
                return;
             }
            /*if (birthdaytype == "smoke")
            {
                prefab = "assets/prefabs/tools/smoke grenade/grenade.smoke.deployed.prefab";
                Puts("SMOKE BIRTHDAY !!!!");
                BaseEntity GrenadeSmoke = GameManager.server.CreateEntity(prefab, daboom, new Quaternion(), true);
                if (GrenadeSmoke == null) Puts("GrenadeSmoke NULL BaseEntity ENTITY !!!!");
                else Puts($"oright {GrenadeSmoke}");
                boomboom = GrenadeSmoke.GetComponent<SmokeGrenade>();
                if (boomboom != null) Puts($"SmokeGrenade {boomboom}");
                boomboom.smokeDuration = 20f;
                GrenadeSmoke.Spawn();
                //boomboom.Explode();
                return;
            }*/
            string prefab = string.Empty;
            if (birthdaytype == "fire") prefab = "assets/bundled/prefabs/fireball.prefab";
            if (birthdaytype == "oil") prefab = "assets/bundled/prefabs/oilfireballsmall.prefab";
            if (birthdaytype == "napalm") prefab = "assets/bundled/prefabs/napalm.prefab";
            BaseEntity GrenadeFlames = GameManager.server.CreateEntity(prefab, daboom, new Quaternion(), true);
            if (GrenadeFlames == null) if (debug) Puts("GrenadeFlames NULL BaseEntity ENTITY !!!!");
            else if (debug) Puts($"oright fireflames {GrenadeFlames}");
            GrenadeFlames?.Spawn();
            timer.Once(flamingtime, () => 
            {
                if (GrenadeFlames != null) GrenadeFlames.Kill();
            });
        }

//////////////////////////////
// CONSOLE COMMAND
/////////////////////////


//////////////////////
// CHAT COMMAND
//////////////////////

        [ChatCommand("cake")]
        private void CakeChatCommander(BasePlayer player, string command, string[] args)
        {
            bool isauth = permission.UserHasPermission(player.UserIDString, CakeChatCommand);
            if (!isauth)
            {
                Player.Message(player, lang.GetMessage("NoPermMsg", this, player.UserIDString),Prefix, SteamIDIcon);
                return;
            }
            CookDaCake(player, args);
        }

//////////////////////
// COOK DA CAKE
//////////////////////

        private void CookDaCake(BasePlayer player, string[] args)
        {
            Cake cake = new Cake();
            cake.playerownerID = player.userID;
            string wotsinside = "";
            if (args.Contains("explose"))
            {
                wotsinside = "|Xplosiv";
                cake.explosion = true;
            }
            /*if (args.Contains("smoke"))
            {
                wotsinside = $"{wotsinside}|Smoke";
                cake.smokebirthday = true;
            }*/
            if (args.Contains("oil"))
            {
                wotsinside = $"{wotsinside}|Oil";             
                cake.oilflames = true;
            }
            if (args.Contains("napalm"))
            {
                wotsinside = $"{wotsinside}|Napalm";
                cake.napalmflames = true;
            }
            if (args.Contains("fire"))
            {
                wotsinside = $"{wotsinside}|Fire";
                cake.fire = true;
            }
            if (wotsinside == "")
            {
                Player.Message(player, lang.GetMessage("SpecifyMsg", this, player.UserIDString),Prefix, SteamIDIcon);
                return;
            }
            Item caketogive = ItemManager.CreateByItemID(ItemManager.FindItemDefinition(1973165031).itemid,1,0);
            if (caketogive != null)
            {
                caketogive.name = cakename + wotsinside;
                player.GiveItem(caketogive);
                storedData.CakeInDaWorld.Add(caketogive.uid, cake);
                Player.Message(player, String.Format(lang.GetMessage("GiftMsg", this, player.UserIDString),wotsinside),Prefix, SteamIDIcon);
            }
        }

////////////////////////////////////
// WHEN PLAYER EQUIP CUSTOM ITEM - notification in CHAT
/////////////////////////

        void OnPlayerActiveItemChanged(BasePlayer player, Item oldItem, Item newItem)
        {
            if (player == null) return;
            if (newItem == null) return;
            uint todel = new uint();
            foreach (var item in storedData.CakeInDaWorld)
            {
                if (newItem.uid == item.Key)
                {
                    Cake cake = item.Value;
                    if (cake.playerownerID == player.userID)
                    Player.Message(player, lang.GetMessage("WishMsg", this, player.UserIDString),Prefix, SteamIDIcon);
                    else
                    {
                        Player.Message(player, lang.GetMessage("NotYoursMsg", this, player.UserIDString),Prefix, SteamIDIcon);
                        todel = item.Key;
                    }
                }
            }
            if (todel != null) storedData.CakeInDaWorld.Remove(todel);
        }
    }
}

