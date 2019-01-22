using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("Fishing", "Colon Blow", "1.4.3", ResourceId = 1537)]
    class Fishing : RustPlugin
    {

        // fix for other weapons being used if head under water
        // made random rolls more random
        // added compound bow as bonus fishing item
        // added message when making pole

        #region load

        private static int waterlayer;
        private static int groundlayer;
        private bool Changed;

        Dictionary<ulong, string> GuiInfo = new Dictionary<ulong, string>();

        void Loaded()
        {
            lang.RegisterMessages(messages, this);
            LoadVariables();
            permission.RegisterPermission("fishing.allowed", this);
        }
        void LoadDefaultConfig()
        {
            Puts("No configuration file found, generating...");
            Config.Clear();
            LoadVariables();
        }

        void OnServerInitialized()
        {
            waterlayer = UnityEngine.LayerMask.GetMask("Water");
            groundlayer = UnityEngine.LayerMask.GetMask("Terrain", "World", "Construction");
        }

        bool IsAllowed(BasePlayer player, string perm)
        {
            if (permission.UserHasPermission(player.userID.ToString(), perm)) return true;
            return false;
        }

        #endregion

        #region Configuration

        bool ShowFishCatchIcon = true;
        bool allowrandomitemchance = true;
        static bool useattacktimer = true;
        static bool usecasttimer = true;
        bool useconditionlossonhit = true;
        float conditionlossmax = 5f;
        float conditionlossmin = 1f;
        bool useweaponmod = true;
        bool useattiremod = true;
        bool useitemmod = true;
        bool usetimemod = true;

        int common1catchitemid = -542577259;
        int common1catchamount = 5;
        int common1catchchance = 20;
        string common1iconurl = "http://i.imgur.com/rBEmhpg.png";

        int common2catchitemid = -1878764039;
        int common2catchamount = 1;
        int common2catchchance = 10;
        string common2iconurl = "http://i.imgur.com/HftxU00.png";

        int uncommoncatchitemid = -1878764039;
        int uncommoncatchamount = 2;
        int uncommoncatchchance = 5;
        string uncommoniconurl = "http://i.imgur.com/xReDQM1.png";

        int rarecatchitemid = -1878764039;
        int rarecatchamount = 5;
        int rarecatchchance = 1;
        string rareiconurl = "http://i.imgur.com/jMZxGf1.png";

        int randomitemchance = 1;
        string randomitemiconurl = "http://i.imgur.com/y2scGmZ.png";
        string randomlootprefab1 = "assets/bundled/prefabs/radtown/crate_basic.prefab";
        int randomlootprefab1chance = 40;
        string randomlootprefab2 = "assets/bundled/prefabs/radtown/crate_elite.prefab";
        int randomlootprefab2chance = 5;
        string randomlootprefab3 = "assets/bundled/prefabs/radtown/crate_mine.prefab";
        int randomlootprefab3chance = 15;
        string randomlootprefab4 = "assets/bundled/prefabs/radtown/crate_normal.prefab";
        int randomlootprefab4chance = 20;
        string randomlootprefab5 = "assets/bundled/prefabs/radtown/crate_normal_2.prefab";
        int randomlootprefab5chance = 20;


        int fishchancedefault = 10;
        int fishchancemodweaponbonus = 10;
        int fishchancemodattirebonus = 10;
        int fishchancemoditembonus = 10;
        int fishchancemodtimebonus = 10;
        float treasureDespawn = 200f;
        static float recasttime = 6f;
        static float refishtime = 6f;

        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }

        private void LoadConfigVariables()
        {
            CheckCfg("_Global - Show Fish Catch Indicator", ref ShowFishCatchIcon);
            CheckCfg("_Global - Enable Timer when Spear/Bow Fishing ? ", ref useattacktimer);
            CheckCfg("_Global - Enable Timer when Casting Fishing Pole ? ", ref usecasttimer);
            CheckCfg("_Global - Enable Random Item Condition Loss when Spear/Bow Fishing ? ", ref useconditionlossonhit);
            CheckCfgFloat("_Global - Random Item Conditon Loss Max percent  ", ref conditionlossmax);
            CheckCfgFloat("_Global - Random Item Conditon Loss Min percent  ", ref conditionlossmin);
            CheckCfg("_Global - Default Chance to catch SOMETHING  ", ref fishchancedefault);
            CheckCfg("_Global - Allow Random Item Chance", ref allowrandomitemchance);
            CheckCfg("_Global - Allow Bonus from Weapon", ref useweaponmod);
            CheckCfg("_Global - Allow Bonus from Attire", ref useattiremod);
            CheckCfg("_Global - Allow Bonus from Item", ref useitemmod);
            CheckCfg("_Global - Allow Bonus from Time of Day", ref usetimemod);
            CheckCfgFloat("_Global - Seconds to Wait after Fishing Attempt with Casting Pole, if enabled (default 6 second) ? ", ref recasttime);
            CheckCfgFloat("_Global - Seconds to Wait after Fishing Attempt with Spear/Bow, if enabled (default 6 seconds) ? ", ref refishtime);

            CheckCfg("Bonus - From Weapon (Percentage)", ref fishchancemodweaponbonus);
            CheckCfg("Bonus - From Attire (Percentage)", ref fishchancemodattirebonus);
            CheckCfg("Bonus - From Items (Percentage)", ref fishchancemoditembonus);
            CheckCfg("Bonus - From Time of Day (Percentage)", ref fishchancemodtimebonus);

            CheckCfg("Catch - Common Fish 1 - Item ID of Catch ", ref common1catchitemid);
            CheckCfg("Catch - Common Fish 1 - Amount of Catch ", ref common1catchamount);
            CheckCfg("Catch - Common Fish 1 - When a player does catch something, chances it will be this (default is Minnows) ", ref common1catchchance);
            CheckCfg("Catch - Common Fish 1 - Icon URL to show when this fish is caught (if enabled) ", ref common1iconurl);

            CheckCfg("Catch - Common Fish 2 - Item ID of Catch ", ref common2catchitemid);
            CheckCfg("Catch - Common Fish 2 - Amount of Catch ", ref common2catchamount);
            CheckCfg("Catch - Common Fish 2 - When a player does catch something, chances it will be this (default is Small Trout) ", ref common2catchchance);
            CheckCfg("Catch - Common Fish 2 - Icon URL to show when this fish is caught (if enabled) ", ref common2iconurl);

            CheckCfg("Catch - Uncommon Fish - Item ID of Catch ", ref uncommoncatchitemid);
            CheckCfg("Catch - Uncommon Fish - Amount of Catch ", ref uncommoncatchamount);
            CheckCfg("Catch - Uncommon Fish - When a player does catch something, chances it will be this (default is Small Trout) ", ref uncommoncatchchance);
            CheckCfg("Catch - Uncommon Fish - Icon URL to show when this fish is caught (if enabled) ", ref uncommoniconurl);

            CheckCfg("Catch - Rare Fish - Item ID of Catch ", ref rarecatchitemid);
            CheckCfg("Catch - Rare Fish - Amount of Catch ", ref rarecatchamount);
            CheckCfg("Catch - Rare Fish - When a player does catch something, chances it will be this  (default is Small Trout) ", ref rarecatchchance);
            CheckCfg("Catch - Rare Fish - Icon URL to show when this fish is caught (if enabled) ", ref rareiconurl);

            CheckCfg("Catch - Random Chest - Icon URL to show when this fish is caught (if enabled)", ref randomitemiconurl);
            CheckCfg("Catch - Random Chest - When a player does catch something, chances it will be this ", ref randomitemchance);

            CheckCfg("Random Chest - These chests will despawn themselves after this amount of time ", ref treasureDespawn);
            CheckCfg("Random Chest - Chest 1 - Prefab string ", ref randomlootprefab1);
            CheckCfg("Random Chest - Chest 1 - If a chest is found, chances its this ", ref randomlootprefab1chance);
            CheckCfg("Random Chest - Chest 2 - Prefab string ", ref randomlootprefab2);
            CheckCfg("Random Chest - Chest 2 - If a chest is found, chances its this ", ref randomlootprefab2chance);
            CheckCfg("Random Chest - Chest 3 - Prefab string ", ref randomlootprefab3);
            CheckCfg("Random Chest - Chest 3 - If a chest is found, chances its this ", ref randomlootprefab3chance);
            CheckCfg("Random Chest - Chest 4 - Prefab string ", ref randomlootprefab4);
            CheckCfg("Random Chest - Chest 4 - If a chest is found, chances its this ", ref randomlootprefab4chance);
            CheckCfg("Random Chest - Chest 5 - Prefab string ", ref randomlootprefab5);
            CheckCfg("Random Chest - Chest 5 - If a chest is found, chances its this ", ref randomlootprefab5chance);
        }

        private void CheckCfg<T>(string Key, ref T var)
        {
            if (Config[Key] is T)
                var = (T)Config[Key];
            else
                Config[Key] = var;
        }

        private void CheckCfgFloat(string Key, ref float var)
        {

            if (Config[Key] != null)
                var = System.Convert.ToSingle(Config[Key]);
            else
                Config[Key] = var;
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

        #endregion

        #region Localization

        string msg(string key, string playerId = null) => lang.GetMessage(key, this, playerId);

        Dictionary<string, string> messages = new Dictionary<string, string>()
        {
            ["missedfish"] = "You Missed the fish....",
            ["notlookingatwater"] = "You must be aiming at water !!!!",
            ["notstandinginwater"] = "You must be standing in water !!!!",
            ["alreadyfishing"] = "You are already fishing !!",
            ["toosoon"] = "Please wait to try that again !!",
            ["cantmove"] = "You must stay still while fishing !!!",
            ["wrongweapon"] = "You are not holding a fishing pole !!!",
            ["correctitem"] = "You must be holding a spear to make a fishing pole !! ",
            ["commonfish1"] = "You Got a Savis Island Swordfish",
            ["commonfish2"] = "You Got a Hapis Island RazorJaw",
            ["uncommonfish1"] = "You Got a Colon BlowFish",
            ["rarefish1"] = "You Got a Craggy Island Dorkfish",
            ["randomitem"] = "You found something in the water !!!",
            ["chancetext1"] = "Your chance to catch a fish is : ",
            ["chancetext2"] = "at Current time of : "
        };

        #endregion

        #region Commands

        [ChatCommand("castpole")]
        void cmdChatcastfishingpole(BasePlayer player, string command, string[] args)
        {
            if (!IsAllowed(player, "fishing.allowed")) return;
            var isfishing = player.GetComponent<FishingControl>();
            if (isfishing) { SendReply(player, msg("alreadyfishing", player.UserIDString)); return; }
            var incooldown = player.GetComponent<SpearFishingControl>();
            if (incooldown) { SendReply(player, msg("toosoon", player.UserIDString)); return; }
            if (!UsingFishingRod(player)) { SendReply(player, msg("wrongweapon", player.UserIDString)); return; }
            if (!LookingAtWater(player)) { SendReply(player, msg("notlookingatwater", player.UserIDString)); return; }
            Vector3 whitpos = new Vector3();
            RaycastHit whit;
            if (Physics.Raycast(player.eyes.HeadRay(), out whit, 50f, waterlayer)) whitpos = whit.point;
            var addfishing = player.gameObject.AddComponent<FishingControl>();
            addfishing.SpawnBobber(whitpos);
        }

        [ConsoleCommand("castpole")]
        void cmdConsoleCastFishingPole(ConsoleSystem.Arg arg)
        {
            var player = arg.Player() ?? null;
            if (player != null)
            {
                if (!IsAllowed(player, "fishing.allowed")) return;
                var isfishing = player.GetComponent<FishingControl>();
                if (isfishing) { SendReply(player, msg("alreadyfishing", player.UserIDString)); return; }
                var incooldown = player.GetComponent<SpearFishingControl>();
                if (incooldown) { SendReply(player, msg("toosoon", player.UserIDString)); return; }
                if (!UsingFishingRod(player)) { SendReply(player, msg("wrongweapon", player.UserIDString)); return; }
                if (!LookingAtWater(player)) { SendReply(player, msg("notlookingatwater", player.UserIDString)); return; }
                Vector3 whitpos = new Vector3();
                RaycastHit whit;
                if (Physics.Raycast(player.eyes.HeadRay(), out whit, 50f, waterlayer)) whitpos = whit.point;
                var addfishing = player.gameObject.AddComponent<FishingControl>();
                addfishing.SpawnBobber(whitpos);
            }
        }

        [ChatCommand("fishchance")]
        void cmdChatfishchance(BasePlayer player, string command, string[] args)
        {
            int catchchance = CatchFishModifier(player);
            SendReply(player, lang.GetMessage("chancetext1", this) + catchchance + "%\n");
        }

        [ChatCommand("makepole")]
        void cmdChatMakeFishingPole(BasePlayer player, string command, string[] args)
        {
            if (!IsAllowed(player, "fishing.allowed")) return;
            MakeFishingPole(player);
        }

        [ConsoleCommand("makepole")]
        void cmdConsoleMakeFishingPole(ConsoleSystem.Arg arg)
        {
            var player = arg.Player() ?? null;
            if (player != null)
            {
                if (!IsAllowed(player, "fishing.allowed")) return;
                MakeFishingPole(player);
            }
        }

        #endregion

        #region Hooks

        void SendInfoMessage(BasePlayer player, string message, float time)
        {
            player?.SendConsoleCommand("gametip.showgametip", message);
            timer.Once(time, () => player?.SendConsoleCommand("gametip.hidegametip"));
        }

        void MakeFishingPole(BasePlayer player)
        {
            Item activeItem = player.GetActiveItem();
            if (activeItem != null && activeItem.info.shortname.Contains("spear"))
            {
                activeItem.Remove(0f);
                ulong skinid = 1393234529;
                if (activeItem.info.shortname == "spear.stone") skinid = 1393231089;
                Item pole = ItemManager.CreateByItemID(1569882109, 1, skinid);
                if (!player.inventory.GiveItem(pole, null))
                {
                    pole.Drop(player.eyes.position, Vector3.forward, new Quaternion());
                    SendInfoMessage(player, "No Room in Inventory, Dropped New Fishing Pole !!", 5f);
                    return;
                }
                SendInfoMessage(player, "New Fishing Pole Placed in your Inventory !!", 5f);
                return;
            }
            SendReply(player, msg("correctitem", player.UserIDString));
        }

        void OnPlayerAttack(BasePlayer attacker, HitInfo hitInfo)
        {
            var player = attacker as BasePlayer;

            if (!IsAllowed(player, "fishing.allowed")) return;
            if (IsAllowed(player, "fishing.allowed"))
            {
                if (hitInfo?.HitEntity as BaseCombatEntity) return;
                if (hitInfo == null) return;

                Vector3 hitloc = hitInfo.HitPositionWorld;
                if (hitInfo.WeaponPrefab.ToString().Contains("spear") || hitInfo.WeaponPrefab.ToString().Contains("bow"))
                {
                    var isfishing = player.GetComponent<FishingControl>();
                    if (isfishing) { SendReply(player, msg("alreadyfishing", player.UserIDString)); return; }
                    var incooldown = player.GetComponent<SpearFishingControl>();
                    if (incooldown) { SendReply(player, msg("toosoon", player.UserIDString)); return; }
                    if (LookingAtWater(player))
                    {
                        if (useconditionlossonhit)
                        {
                            float maxloss = 1f - (conditionlossmax / 100f);
                            float minloss = 1f - (conditionlossmin / 100f);
                            Item activeItem = player.GetActiveItem();
                            activeItem.condition = activeItem.condition * UnityEngine.Random.Range(minloss, maxloss);
                        }
                        FishChanceRoll(player, hitloc);
                        if (useattacktimer) player.gameObject.AddComponent<SpearFishingControl>();
                        hitInfo.CanGather = true;
                        return;
                    }
                }

                if (player.IsHeadUnderwater())
                {
                    if (!hitInfo.WeaponPrefab.ToString().Contains("spear") || hitInfo.WeaponPrefab.ToString().Contains("bow")) return;
                    var isfishing = player.GetComponent<FishingControl>();
                    if (isfishing) { SendReply(player, msg("alreadyfishing", player.UserIDString)); return; }
                    var incooldown = player.GetComponent<SpearFishingControl>();
                    if (incooldown) { SendReply(player, msg("toosoon", player.UserIDString)); return; }
                    if (useconditionlossonhit)
                    {
                        float maxloss = 1f - (conditionlossmax / 100f);
                        float minloss = 1f - (conditionlossmin / 100f);
                        Item activeItem = player.GetActiveItem();
                        activeItem.condition = activeItem.condition * UnityEngine.Random.Range(minloss, maxloss);
                    }
                    FishChanceRoll(player, hitloc);
                    if (useattacktimer) player.gameObject.AddComponent<SpearFishingControl>();
                    hitInfo.CanGather = true;
                    return;
                }
            }
            else return;
        }

        public class IntUtil
        {
            private static System.Random random;

            private static void Init()
            {
                if (random == null) random = new System.Random();
            }
            public static int Random(int min, int max)
            {
                Init();
                return random.Next(min, max);
            }
        }

        void FishChanceRoll(BasePlayer player, Vector3 hitloc)
        {
            int roll = IntUtil.Random(1, 101);
            int totatchance = CatchFishModifier(player);
            if (roll < totatchance)
            {
                FishTypeRoll(player, hitloc);
                return;
            }
            else
                SendReply(player, msg("missedfish", player.UserIDString));
            return;
        }

        int CatchFishModifier(BasePlayer player)
        {
            int chances = new int();
            chances = fishchancedefault;
            if (useweaponmod)
            {
                Item activeItem = player.GetActiveItem();
                if (activeItem != null)
                {
                    if (activeItem.info.shortname == "spear.stone" || activeItem.skin == 1393231089 || activeItem.info.shortname == "crossbow" || activeItem.info.shortname == "bow.compound")
                    {
                        chances += fishchancemodweaponbonus;
                    }
                }
            }
            if (useattiremod)
            {
                int hasBoonieOn = player.inventory.containerWear.GetAmount(-23994173, true);
                if (hasBoonieOn > 0) chances += fishchancemodattirebonus;
            }
            if (useitemmod)
            {
                int hasPookie = player.inventory.containerMain.GetAmount(-1651220691, true);
                if (hasPookie > 0) chances += fishchancemoditembonus;
            }
            if (usetimemod)
            {
                var currenttime = TOD_Sky.Instance.Cycle.Hour;
                if ((currenttime < 8 && currenttime > 6) || (currenttime < 19 && currenttime > 16)) chances += fishchancemodtimebonus;
            }
            int totalchance = chances;
            return totalchance;
        }

        void FishTypeRoll(BasePlayer player, Vector3 hitloc)
        {
            int totalfishtypechance = rarecatchchance + uncommoncatchchance + common1catchchance + common2catchchance;
            var fishtyperoll = IntUtil.Random(1, totalfishtypechance + 1);
            if (allowrandomitemchance)
            {
                if (fishtyperoll < randomitemchance)
                {
                    catchFishCui(player, randomitemiconurl);
                    SendReply(player, msg("randomitem", player.UserIDString));
                    SpawnLootBox(player, hitloc);
                    return;
                }
            }
            if (fishtyperoll < rarecatchchance)
            {
                catchFishCui(player, rareiconurl);
                SendReply(player, msg("rarefish1", player.UserIDString));
                player.inventory.GiveItem(ItemManager.CreateByItemID(rarecatchitemid, rarecatchamount));
                player.Command("note.inv", rarecatchitemid, rarecatchamount);
                return;
            }
            if (fishtyperoll < rarecatchchance + uncommoncatchchance)
            {
                catchFishCui(player, uncommoniconurl);
                SendReply(player, msg("uncommonfish1", player.UserIDString));
                player.inventory.GiveItem(ItemManager.CreateByItemID(uncommoncatchitemid, uncommoncatchamount));
                player.Command("note.inv", uncommoncatchitemid, uncommoncatchamount);
                return;
            }
            if (fishtyperoll < rarecatchchance + uncommoncatchchance + common2catchchance)
            {
                catchFishCui(player, common2iconurl);
                SendReply(player, msg("commonfish2", player.UserIDString));
                player.inventory.GiveItem(ItemManager.CreateByItemID(common2catchitemid, common2catchamount));
                player.Command("note.inv", common2catchitemid, common2catchamount);
                return;
            }
            if (fishtyperoll < rarecatchchance + uncommoncatchchance + common2catchchance + common1catchchance)
            {
                catchFishCui(player, common1iconurl);
                SendReply(player, msg("commonfish1", player.UserIDString));
                player.inventory.GiveItem(ItemManager.CreateByItemID(common1catchitemid, common1catchamount));
                player.Command("note.inv", common1catchitemid, common1catchamount);
                return;
            }
        }

        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            var isfishing = player.GetComponent<FishingControl>();
            if (!isfishing) return;
            if (input != null)
            {
                if (input.WasJustPressed(BUTTON.FORWARD)) isfishing.playermoved = true;
                if (input.WasJustPressed(BUTTON.BACKWARD)) isfishing.playermoved = true;
                if (input.WasJustPressed(BUTTON.RIGHT)) isfishing.playermoved = true;
                if (input.WasJustPressed(BUTTON.LEFT)) isfishing.playermoved = true;
                if (input.WasJustPressed(BUTTON.JUMP)) isfishing.playermoved = true;
            }
        }

        void OnPlayerRespawned(BasePlayer player)
        {
            var isfishing = player.GetComponent<FishingControl>();
            if (isfishing) isfishing.OnDestroy();
            var hascooldown = player.GetComponent<SpearFishingControl>();
            if (hascooldown) hascooldown.OnDestroy();
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            var isfishing = player.GetComponent<FishingControl>();
            if (isfishing) isfishing.OnDestroy();
            var hascooldown = player.GetComponent<SpearFishingControl>();
            if (hascooldown) hascooldown.OnDestroy();
        }

        bool UsingFishingRod(BasePlayer player)
        {
            Item activeItem = player.GetActiveItem();
            if (activeItem != null && activeItem.info.shortname.Contains("fishingrod")) return true;
            return false;
        }

        bool LookingAtWater(BasePlayer player)
        {
            float whitDistance = 0;
            float ghitDistance = 100f;
            UnityEngine.Ray ray = new UnityEngine.Ray(player.eyes.position, player.eyes.HeadForward());

            var hitsw = UnityEngine.Physics.RaycastAll(ray, 25f, waterlayer);
            var hitsg = UnityEngine.Physics.RaycastAll(ray, 25f, groundlayer);

            foreach (var hit in hitsw)
            {
                whitDistance = hit.distance;
            }
            foreach (var hit in hitsg)
            {
                ghitDistance = hit.distance;
            }
            if (whitDistance > 0 && ghitDistance == null) return true;
            if (whitDistance < ghitDistance && whitDistance > 0) return true;
            return false;
        }

        void SpawnLootBox(BasePlayer player, Vector3 hitloc)
        {
            var randomlootprefab = randomlootprefab1;
            var rlroll = IntUtil.Random(1, 6);
            if (rlroll == 1) randomlootprefab = randomlootprefab1;
            if (rlroll == 2) randomlootprefab = randomlootprefab2;
            if (rlroll == 3) randomlootprefab = randomlootprefab3;
            if (rlroll == 4) randomlootprefab = randomlootprefab4;
            if (rlroll == 5) randomlootprefab = randomlootprefab5;

            var createdPrefab = GameManager.server.CreateEntity(randomlootprefab, hitloc);
            BaseEntity treasurebox = createdPrefab?.GetComponent<BaseEntity>();
            treasurebox.enableSaving = false;
            treasurebox?.Spawn();
            timer.Once(treasureDespawn, () => CheckTreasureDespawn(treasurebox));
        }

        void CheckTreasureDespawn(BaseEntity treasurebox)
        {
            if (treasurebox != null) treasurebox.Kill(BaseNetworkable.DestroyMode.None);
        }

        void Unload()
        {
            DestroyAll<FishingControl>();
            DestroyAll<SpearFishingControl>();
            foreach (var player in BasePlayer.activePlayerList)
            {
                string guiInfo;
                if (GuiInfo.TryGetValue(player.userID, out guiInfo)) CuiHelper.DestroyUi(player, guiInfo);
            }
        }

        static void DestroyAll<T>()
        {
            var objects = GameObject.FindObjectsOfType(typeof(T));
            if (objects != null)
                foreach (var gameObj in objects)
                    GameObject.Destroy(gameObj);
        }

        #endregion

        #region GUI

        void catchFishCui(BasePlayer player, string fishicon)
        {
            if (ShowFishCatchIcon) FishingGui(player, fishicon);
        }

        void FishingGui(BasePlayer player, string fishicon)
        {
            DestroyCui(player);

            var elements = new CuiElementContainer();
            GuiInfo[player.userID] = CuiHelper.GetGuid();

            if (ShowFishCatchIcon)
            {
                elements.Add(new CuiElement
                {
                    Name = GuiInfo[player.userID],
                    Parent = "Overlay",
                    Components =
                    {
                        new CuiRawImageComponent { Color = "1 1 1 1", Url = fishicon, Sprite = "assets/content/textures/generic/fulltransparent.tga" },
                        new CuiRectTransformComponent { AnchorMin = "0.220 0.03",  AnchorMax = "0.260 0.10" }
                    }
                });
            }

            CuiHelper.AddUi(player, elements);
            timer.Once(1f, () => DestroyCui(player));
        }


        void DestroyCui(BasePlayer player)
        {
            string guiInfo;
            if (GuiInfo.TryGetValue(player.userID, out guiInfo)) CuiHelper.DestroyUi(player, guiInfo);
        }

        #endregion

        #region Spear Fishing Control

        class SpearFishingControl : MonoBehaviour
        {
            BasePlayer player;
            public string anchormaxstr;
            Fishing fishing;
            public float counter;

            void Awake()
            {
                fishing = new Fishing();
                player = base.GetComponentInParent<BasePlayer>();
                counter = refishtime;
                if (!useattacktimer || counter < 0.1f) counter = 0.1f;
            }

            void FixedUpdate()
            {
                if (counter <= 0f) OnDestroy();
                counter = counter - 0.1f;
                fishingindicator(player, counter);
            }

            string GetGUIString(float counter)
            {
                string guistring = "0.60 0.145";
                var getrefreshtime = refishtime;
                if (counter < getrefreshtime * 0.1) { guistring = "0.42 0.145"; return guistring; }
                if (counter < getrefreshtime * 0.2) { guistring = "0.44 0.145"; return guistring; }
                if (counter < getrefreshtime * 0.3) { guistring = "0.46 0.145"; return guistring; }
                if (counter < getrefreshtime * 0.4) { guistring = "0.48 0.145"; return guistring; }
                if (counter < getrefreshtime * 0.5) { guistring = "0.50 0.145"; return guistring; }
                if (counter < getrefreshtime * 0.6) { guistring = "0.52 0.145"; return guistring; }
                if (counter < getrefreshtime * 0.7) { guistring = "0.54 0.145"; return guistring; }
                if (counter < getrefreshtime * 0.8) { guistring = "0.56 0.145"; return guistring; }
                if (counter < getrefreshtime * 0.9) { guistring = "0.58 0.145"; return guistring; }
                return guistring;
            }

            public void fishingindicator(BasePlayer player, float counter)
            {
                DestroyCui(player);
                var getrefreshtime = refishtime;
                string anchormaxstr = GetGUIString(counter);
                var fishingindicator = new CuiElementContainer();

                fishingindicator.Add(new CuiButton
                {
                    Button = { Command = $"", Color = "1.0 0.0 0.0 0.6" },
                    RectTransform = { AnchorMin = "0.40 0.125", AnchorMax = anchormaxstr },
                    Text = { Text = (""), FontSize = 14, Color = "1.0 1.0 1.0 0.6", Align = TextAnchor.MiddleRight }
                }, "Overall", "FishingGui");
                CuiHelper.AddUi(player, fishingindicator);
            }

            void DestroyCui(BasePlayer player)
            {
                CuiHelper.DestroyUi(player, "FishingGui");
            }

            public void OnDestroy()
            {
                DestroyCui(player);
                Destroy(this);
            }
        }

        #endregion

        #region Fishing Control

        class FishingControl : MonoBehaviour
        {
            BasePlayer player;
            public string anchormaxstr;
            Fishing fishing;
            public float counter;
            BaseEntity bobber;
            public bool playermoved;
            Vector3 bobberpos;

            void Awake()
            {
                fishing = new Fishing();
                player = base.GetComponentInParent<BasePlayer>();
                counter = recasttime;
                if (!usecasttimer || counter < 0.1f) counter = 0.1f;
                playermoved = false;
            }

            public void SpawnBobber(Vector3 pos)
            {
                float waterheight = TerrainMeta.WaterMap.GetHeight(pos);

                pos = new Vector3(pos.x, waterheight, pos.z);
                var createdPrefab = GameManager.server.CreateEntity("assets/prefabs/tools/fishing rod/bobber/bobber.prefab", pos, Quaternion.identity);
                bobber = createdPrefab?.GetComponent<BaseEntity>();
                bobber.enableSaving = false;
                bobber.transform.eulerAngles = new Vector3(270, 0, 0);
                bobber?.Spawn();
                bobberpos = bobber.transform.position;
            }

            void FixedUpdate()
            {
                bobberpos = bobber.transform.position;
                if (counter <= 0f) { RollForFish(bobberpos); return; }
                if (playermoved) { PlayerMoved(); return; }
                counter = counter - 0.1f;
                fishingindicator(player, counter);
            }

            void PlayerMoved()
            {
                if (bobber != null && !bobber.IsDestroyed) { bobber.Invoke("KillMessage", 0.1f); }
                fishing.SendReply(player, fishing.msg("cantmove", player.UserIDString));
                OnDestroy();
            }

            void RollForFish(Vector3 pos)
            {
                if (player != null) fishing.FishChanceRoll(player, pos);
                if (bobber != null && !bobber.IsDestroyed) { bobber.Invoke("KillMessage", 0.1f); }
                OnDestroy();
            }

            string GetGUIString(float counter)
            {
                string guistring = "0.60 0.145";
                var getrefreshtime = recasttime;
                if (counter < getrefreshtime * 0.1) { guistring = "0.42 0.145"; return guistring; }
                if (counter < getrefreshtime * 0.2) { guistring = "0.44 0.145"; return guistring; }
                if (counter < getrefreshtime * 0.3) { guistring = "0.46 0.145"; return guistring; }
                if (counter < getrefreshtime * 0.4) { guistring = "0.48 0.145"; return guistring; }
                if (counter < getrefreshtime * 0.5) { guistring = "0.50 0.145"; return guistring; }
                if (counter < getrefreshtime * 0.6) { guistring = "0.52 0.145"; return guistring; }
                if (counter < getrefreshtime * 0.7) { guistring = "0.54 0.145"; return guistring; }
                if (counter < getrefreshtime * 0.8) { guistring = "0.56 0.145"; return guistring; }
                if (counter < getrefreshtime * 0.9) { guistring = "0.58 0.145"; return guistring; }
                return guistring;
            }

            public void fishingindicator(BasePlayer player, float counter)
            {
                DestroyCui(player);
                string anchormaxstr = GetGUIString(counter);
                var fishingindicator = new CuiElementContainer();

                fishingindicator.Add(new CuiButton
                {
                    Button = { Command = $"", Color = "0.0 0.0 1.0 0.6" },
                    RectTransform = { AnchorMin = "0.40 0.125", AnchorMax = anchormaxstr },
                    Text = { Text = (""), FontSize = 14, Color = "1.0 1.0 1.0 0.6", Align = TextAnchor.MiddleRight }
                }, "Overall", "FishingGui");
                CuiHelper.AddUi(player, fishingindicator);
            }

            void DestroyCui(BasePlayer player)
            {
                CuiHelper.DestroyUi(player, "FishingGui");
            }

            public void OnDestroy()
            {
                DestroyCui(player);
                Destroy(this);
            }
        }

        #endregion
    }
}