using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using System.Reflection;

using Rust;

using Network.Visibility;
using Oxide.Core;
using Rust;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    #region zone
    public class Zone
    {
        public List<int> Bots;

        public virtual void Start()
        {

        }
    }
    #endregion

    #region bot
    public class Bot
    {
        public ulong Id;
        public int ZoneId;

        public float PosX;
        public float PosY;
        public float PosZ;

        private BasePlayer BasePlayer;

        public Bot(int zone, ulong id, Vector3 position)
        {
            Id = id;

            PosX = position.x;
            PosY = position.y;
            PosZ = position.z;
        }

        #region main methods
        public void Spawn()
        {
            AimTrain.Instance.Chat("Spawning!");

            var newPlayer = GameManager.server.CreateEntity("assets/prefabs/player/player.prefab", GetPos(), Quaternion.identity);
            newPlayer.Spawn();

            newPlayer.SetFlag(BaseEntity.Flags.Reserved1, true);
            FieldInfo modelStateField = typeof(BasePlayer).GetField("modelState", BindingFlags.Instance | BindingFlags.NonPublic);
            object modelState = modelStateField.GetValue(newPlayer);
            modelState.GetType().GetProperty("onground").SetValue(modelState, true, null);

            newPlayer.SendNetworkUpdate();

            BasePlayer = newPlayer.GetComponent<BasePlayer>();
            BasePlayer.userID = Id;

            AimTrain.Instance.Chat("Spawned!");
        }

        public void Kill()
        {
            if(BasePlayer != null)
            {
                BasePlayer.Kill();
                BasePlayer.SendNetworkUpdate();
            }
        }
        #endregion

        #region utility methods
        Vector3 GetPos()
        {
            return new Vector3(PosX, PosY, PosZ);
        }
        #endregion
    }
    #endregion

    #region plugin
    [Info("AimTrain", "Ardivaba", 0.1)]
    [Description("Mkes you Trauzillz")]
    public class AimTrain : RustPlugin
    {
        #region data
        public static AimTrain Instance;
        public static HashSet<Bot> Bots;
        public static bool Moving = true;
        #endregion

        #region public static main methods
        public static void CreateBot(Vector3 position)
        {
            Bot bot = new Bot(0, (ulong) Bots.Count, position);

            bot.Spawn();

            Bots.Add(bot);

            AimTrain.SaveBots();
        }

        public static void LoadBots()
        {
            Bots = Interface.Oxide.DataFileSystem.ReadObject<HashSet<Bot>>("Bots");
        }

        public static void SaveBots()
        {
            Interface.Oxide.DataFileSystem.WriteObject<HashSet<Bot>>("Bots", Bots, true);
        }
        #endregion

        #region main methods
        public void Chat(string chat)
        {
            PrintToChat(chat);
        }

        void UnlimitedAmmo(BaseProjectile projectile, BasePlayer player)
        {
            projectile.GetItem().condition = projectile.GetItem().info.condition.max;
            projectile.primaryMagazine.contents = projectile.primaryMagazine.capacity;
            projectile.SendNetworkUpdateImmediate();
        }
        #endregion

        #region oxide hooks
        void OnFrame()
        {
            StashContainer[] stashes = GameObject.FindObjectsOfType<StashContainer>();
            BasePlayer[] players = GameObject.FindObjectsOfType<BasePlayer>();

            int botCount = 0;
            foreach(BasePlayer player in players)
            {
                if (player.IsWounded() && player.HasFlag(BaseEntity.Flags.Reserved1))
                {
                    player.Kill();
                    player.SendNetworkUpdate();
                    headShots++;
                }
                if (player.HasFlag(BaseEntity.Flags.Reserved1))
                {
                    botCount++;
                }
            }

            if(botCount < botCounts)
            {
                StashContainer randomStash = stashes[UnityEngine.Random.Range(0, stashes.Length - 1)];
                SpawnBot(randomStash.transform.position);
            }
        }

        void Loaded()
        {
            AimTrain.Instance = this;
            AimTrain.LoadBots();
        }

        void OnWeaponFired(BaseProjectile projectile, BasePlayer player)
        {
                UnlimitedAmmo(projectile, player);
        }

        int headShots = 0;
        void OnPlayerAttack(BasePlayer attacker, HitInfo info)
        {
return;
            BasePlayer victim = info.HitEntity.GetComponent<BasePlayer>();
            if(victim != null)
            {
                if(victim.HasFlag(BaseEntity.Flags.Reserved1))
                {
                    if (info.isHeadshot)
                    {
                        //headShots++;
                    }
                }
            }

            if(headShots == 20)
            {
                PrintToChat("Headshot benchmark time: " + (DateTime.Now - startTime).TotalSeconds);
                headShots = 0;
                foreach (Bot bot in Bots)
                {
                    bot.Kill();
                }
            }
        }
        #endregion

        int botCounts = 10;
        #region commands
        [ConsoleCommand("botCount")]
        void CmdBotCount(ConsoleSystem.Arg arg)
        {
            botCounts = int.Parse(arg.Args[0]);
            PrintToChat("Bot count is now: " + botCounts);
        }

        [ConsoleCommand("createBot")]
        void CmdCreateBot(ConsoleSystem.Arg arg)
        {
            AimTrain.CreateBot(arg.Player().transform.position);
            PrintToChat("Where them bots at?");
        }

        [ConsoleCommand("killBots")]
        void CmdKillBots(ConsoleSystem.Arg arg)
        {
            foreach(Bot bot in Bots)
            {
                bot.Kill();
            }
        }


        [ConsoleCommand("stashes")]
        void CmdStashes(ConsoleSystem.Arg arg)
        {
            StashContainer[] stashes = GameObject.FindObjectsOfType<StashContainer>();
            foreach (StashContainer stash in stashes)
            {
                stash.Kill();
                stash.SendNetworkUpdate();
            }
        }

        [ConsoleCommand("moving")]
        void CmdMoving(ConsoleSystem.Arg arg)
        {
            AimTrain.Moving = !AimTrain.Moving;
        }

        DateTime startTime;

        [ConsoleCommand("kcmo")]
        void CmdKcmo(ConsoleSystem.Arg arg)
        {
            headShots = 0;
            startTime = DateTime.Now;

            BasePlayer[] players = GameObject.FindObjectsOfType<BasePlayer>();
            foreach(BasePlayer player in players)
            {
                if (player.HasFlag(BaseEntity.Flags.Reserved1))
                {
                    player.Kill();
                    player.SendNetworkUpdate();
                }
            }

            BaseCorpse[] corpses = GameObject.FindObjectsOfType<BaseCorpse>();
            foreach(BaseCorpse corpse in corpses)
            {
                corpse.Kill();
                corpse.SendNetworkUpdate();
            }
        }

        void SpawnBot(Vector3 position)
        {
            //AimTrain.Instance.Chat("Spawning!");

            var newPlayer = GameManager.server.CreateEntity("assets/prefabs/player/player.prefab", position, Quaternion.identity);
            newPlayer.Spawn();
            newPlayer.gameObject.AddComponent<BotMover>();

            newPlayer.SetFlag(BaseEntity.Flags.Reserved1, true);
            FieldInfo modelStateField = typeof(BasePlayer).GetField("modelState", BindingFlags.Instance | BindingFlags.NonPublic);
            object modelState = modelStateField.GetValue(newPlayer);
            modelState.GetType().GetProperty("onground").SetValue(modelState, true, null);

            newPlayer.SendNetworkUpdate();

            //AimTrain.Instance.Chat("Spawned!");
        }
        #endregion
    }
    #endregion

    #region
    public class BotMover : MonoBehaviour
    {
        BasePlayer basePlayer;
        Vector3 startPosition;
        Vector3 targetPosition;
        float moveSpeed = 1.0f;
        void Start()
        {
            basePlayer = GetComponent<BasePlayer>();
            startPosition = transform.position;
            targetPosition = startPosition;
            moveSpeed = UnityEngine.Random.Range(0.1f, 0.3f);
            basePlayer.ChangeHealth(100.0f);
            basePlayer.SendNetworkUpdate();

            int random = UnityEngine.Random.Range(0, 3);
            int clothes = UnityEngine.Random.Range(0, 2);

            if(random == 0)//Metal
            {
                basePlayer.inventory.GiveItem(ItemManager.CreateByItemID(-46848560), basePlayer.inventory.containerWear);
                basePlayer.inventory.GiveItem(ItemManager.CreateByItemID(1265861812), basePlayer.inventory.containerWear);
                basePlayer.inventory.GiveItem(ItemManager.CreateByItemID(-1595790889), basePlayer.inventory.containerWear);
            }
            else if( random == 1 )
            {
                int random2 = UnityEngine.Random.Range(0, 2);
                if(random2 == 0)
                {
                    basePlayer.inventory.GiveItem(ItemManager.CreateByItemID(1260209393), basePlayer.inventory.containerWear);
                }
                else if( random2 == 1)
                {
                    basePlayer.inventory.GiveItem(ItemManager.CreateByItemID(-2128719593), basePlayer.inventory.containerWear);
                }

                basePlayer.inventory.GiveItem(ItemManager.CreateByItemID(-288010497), basePlayer.inventory.containerWear);
                basePlayer.inventory.GiveItem(ItemManager.CreateByItemID(-1595790889), basePlayer.inventory.containerWear);
            }

            if(clothes == 0)
            {
                basePlayer.inventory.GiveItem(ItemManager.CreateByItemID(115739308), basePlayer.inventory.containerWear);
                basePlayer.inventory.GiveItem(ItemManager.CreateByItemID(707427396), basePlayer.inventory.containerWear);
                basePlayer.inventory.GiveItem(ItemManager.CreateByItemID(1767561705), basePlayer.inventory.containerWear);
            }
            if (clothes == 1)
            {
                basePlayer.inventory.GiveItem(ItemManager.CreateByItemID(106433500), basePlayer.inventory.containerWear);
                basePlayer.inventory.GiveItem(ItemManager.CreateByItemID(-1211618504), basePlayer.inventory.containerWear);
                basePlayer.inventory.GiveItem(ItemManager.CreateByItemID(115739308), basePlayer.inventory.containerWear);
            }
        }

        void FixedUpdate()
        {
            if (!AimTrain.Moving)
            {
                return;
            }

            if(Vector3.Distance(transform.position, targetPosition) < 1.0f)
            {
                //targetPosition = startPosition + transform.right * UnityEngine.Random.Range(-15, 15);
                StashContainer[] stashes = GameObject.FindObjectsOfType<StashContainer>();
                StashContainer randomStash = stashes[UnityEngine.Random.Range(0, stashes.Length - 1)];
                targetPosition = randomStash.transform.position;
            }

            Vector3 newPos = Vector3.Lerp(transform.position, targetPosition, Time.fixedDeltaTime * moveSpeed);
            newPos = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed);
            basePlayer.transform.position = newPos;
            basePlayer.Teleport(basePlayer);
            basePlayer.SendNetworkUpdate();
        }
    }
    #endregion
}
