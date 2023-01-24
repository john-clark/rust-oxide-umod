using System.Linq;
using System.Collections.Generic;
using CodeHatch.Common;
using CodeHatch.Engine.Networking;
using CodeHatch.ItemContainer;
using CodeHatch.UserInterface.Dialogues;
using UnityEngine;
using CodeHatch.Networking.Events.Entities;
using CodeHatch.Networking.Events;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("KingsLottery", "Karltubing", "7.7.7")]
    [Description("A way for players to gamble")]

    class KingsLottery : ReignOfKingsPlugin
    {
        [PluginReference]
        private Plugin GrandExchange;
        string ItemUsedToPlayLottery;
        bool LotteryBatUsedToPlay;
        bool LotteryIronIngotUsedToPlay;
        bool LotterySteelIngotUsedToPlay;
        bool CanWinAdvancedFletcher;
        bool CanWinAfricanMask;
        bool CanWinAmberjackFish;
        bool CanWinAncientCrown;
        bool CanWinAncientSword;
        bool CanWinAnvil;
        bool CanWinApple;
        bool CanWinAppleSeed;
        bool CanWinArcheryTarget;
        bool CanWinAsianMask;
        bool CanWinAsianTribalMask;
        bool CanWinBabyChicken;
        bool CanWinBakedClay;
        bool CanWinBallista;
        bool CanWinBallistaBolt;
        bool CanWinBandage;
        bool CanWinBanquetTable;
        bool CanWinBascinetHelmet;
        bool CanWinBascinetPointedHelmet;
        bool CanWinBassFish;
        bool CanWinBat;
        bool CanWinBatWing;
        bool CanWinBeanSeed;
        bool CanWinBearHide;
        bool CanWinBearSkinRug;
        bool CanWinBeet;
        bool CanWinBeetSeed;
        bool CanWinBellGong;
        bool CanWinBellows;
        bool CanWinBentHorn;
        bool CanWinBerries;
        bool CanWinBerrySeed;
        bool CanWinBladedPillar;
        bool CanWinBlood;
        bool CanWinBone;
        bool CanWinBoneAxe;
        bool CanWinBoneDagger;
        bool CanWinBoneHorn;
        bool CanWinBoneLongbow;
        bool CanWinBoneSpikedClub;
        bool CanWinBread;
        bool CanWinBugNet;
        bool CanWinBurntBird;
        bool CanWinBurntMeat;
        bool CanWinButterfly;
        bool CanWinCabbage;
        bool CanWinCabbageSeed;
        bool CanWinCampfire;
        bool CanWinCandle;
        bool CanWinCandleStand;
        bool CanWinCarrot;
        bool CanWinCarrotSeed;
        bool CanWinCatMask;
        bool CanWinChandelier;
        bool CanWinChapelDeFerHelmet;
        bool CanWinChapelDeFerRoundedHelmet;
        bool CanWinCharcoal;
        bool CanWinChicken;
        bool CanWinClay;
        bool CanWinClayBlock;
        bool CanWinClayCorner;
        bool CanWinClayInvertedCorner;
        bool CanWinClayRamp;
        bool CanWinClayStairs;
        bool CanWinCobblestoneBlock;
        bool CanWinCobblestoneCorner;
        bool CanWinCobblestoneInvertedCorner;
        bool CanWinCobblestoneRamp;
        bool CanWinCobblestoneStairs;
        bool CanWinCookedBeans;
        bool CanWinCookedBird;
        bool CanWinCookedMeat;
        bool CanWinCrab;
        bool CanWinCrossbow;
        bool CanWinCrow;
        bool CanWinDeer;
        bool CanWinDeerHeadTrophy;
        bool CanWinDeerLegClub;
        bool CanWinDefensiveBarricade;
        bool CanWinDiamond;
        bool CanWinDirt;
        bool CanWinDjembeDrum;
        bool CanWinDriftwoodClub;
        bool CanWinDuck;
        bool CanWinDuckFeet;
        bool CanWinExecutionersAxe;
        bool CanWinExplosiveKeg;
        bool CanWinFang;
        bool CanWinFat;
        bool CanWinFeather;
        bool CanWinFern;
        bool CanWinFernBracers;
        bool CanWinFernHelmet;
        bool CanWinFernSandals;
        bool CanWinFernSkirt;
        bool CanWinFernVest;
        bool CanWinFireFly;
        bool CanWinFireWater;
        bool CanWinFirepit;
        bool CanWinFishingRod;
        bool CanWinFlatTopHelmet;
        bool CanWinFlax;
        bool CanWinFletcher;
        bool CanWinFlour;
        bool CanWinFlowerBracers;
        bool CanWinFlowerHelmet;
        bool CanWinFlowerSandals;
        bool CanWinFlowerSkirt;
        bool CanWinFlowerVest;
        bool CanWinFlowers;
        bool CanWinFluffyBed;
        bool CanWinFly;
        bool CanWinForestSprite;
        bool CanWinFuse;
        bool CanWinGazebo;
        bool CanWinGrain;
        bool CanWinGrainSeed;
        bool CanWinGranary;
        bool CanWinGraveMask;
        bool CanWinGreatFireplace;
        bool CanWinGrizzlyBear;
        bool CanWinGroundTorch;
        bool CanWinGuillotine;
        bool CanWinHangingLantern;
        bool CanWinHangingTorch;
        bool CanWinHay;
        bool CanWinHayBaleTarget;
        bool CanWinHayBracers;
        bool CanWinHayHelmet;
        bool CanWinHaySandals;
        bool CanWinHaySkirt;
        bool CanWinHayVest;
        bool CanWinHeart;
        bool CanWinHighQualityBed;
        bool CanWinHighQualityBench;
        bool CanWinHighQualityCabinet;
        bool CanWinHoe;
        bool CanWinIron;
        bool CanWinIronAxe;
        bool CanWinIronBarWindow;
        bool CanWinIronBattleAxe;
        bool CanWinIronBattleHammer;
        bool CanWinIronBearTrap;
        bool CanWinIronBuckler;
        bool CanWinIronChest;
        bool CanWinIronCrest;
        bool CanWinIronDagger;
        bool CanWinIronDoor;
        bool CanWinIronFlangedMace;
        bool CanWinIronFloorTorch;
        bool CanWinIronForkedSpear;
        bool CanWinIronGate;
        bool CanWinIronHalberd;
        bool CanWinIronHatchet;
        bool CanWinIronHeater;
        bool CanWinIronIngot;
        bool CanWinIronJavelin;
        bool CanWinIronMorningStarMace;
        bool CanWinIronPickaxe;
        bool CanWinIronPlateBoots;
        bool CanWinIronPlateGauntlets;
        bool CanWinIronPlateHelmet;
        bool CanWinIronPlatePants;
        bool CanWinIronPlateVest;
        bool CanWinIronShackles;
        bool CanWinIronSpear;
        bool CanWinIronSpikes;
        bool CanWinIronSpikesHidden;
        bool CanWinIronStarMace;
        bool CanWinIronSword;
        bool CanWinIronThrowingAxe;
        bool CanWinIronThrowingBattleAxe;
        bool CanWinIronThrowingKnife;
        bool CanWinIronTippedArrow;
        bool CanWinIronTotem;
        bool CanWinIronTower;
        bool CanWinIronWarHammer;
        bool CanWinIronWoodCuttersAxe;
        bool CanWinJapaneseDemon;
        bool CanWinJapaneseMask;
        bool CanWinJesterHatGreenPink;
        bool CanWinJesterHatOrangeBlack;
        bool CanWinJesterHatRainbow;
        bool CanWinJesterHatRed;
        bool CanWinJesterMaskGoldBlue;
        bool CanWinJesterMaskGoldRed;
        bool CanWinJesterMaskWhiteBlue;
        bool CanWinJesterMaskWhiteGold;
        bool CanWinKettleBoardHelmet;
        bool CanWinKettleHat;
        bool CanWinKoiFish;
        bool CanWinLargeGallows;
        bool CanWinLargeIronCage;
        bool CanWinLargeIronHangingCage;
        bool CanWinLargeWoodBillboard;
        bool CanWinLeatherCrest;
        bool CanWinLeatherHide;
        bool CanWinLightLeatherBoots;
        bool CanWinLightLeatherBracers;
        bool CanWinLightLeatherHelmet;
        bool CanWinLightLeatherPants;
        bool CanWinLightLeatherVest;
        bool CanWinLiver;
        bool CanWinLockPick;
        bool CanWinLogBlock;
        bool CanWinLogCorner;
        bool CanWinLogFence;
        bool CanWinLogInvertedCorner;
        bool CanWinLogRamp;
        bool CanWinLogStairs;
        bool CanWinLongHorn;
        bool CanWinLongWoodDrawbridge;
        bool CanWinLootSack;
        bool CanWinLordsBath;
        bool CanWinLordsBed;
        bool CanWinLordsLargeChair;
        bool CanWinLordsSmallChair;
        bool CanWinLowQualityBed;
        bool CanWinLowQualityBench;
        bool CanWinLowQualityChair;
        bool CanWinLowQualityFence;
        bool CanWinLowQualityShelf;
        bool CanWinLowQualityStool;
        bool CanWinLowQualityTable;
        bool CanWinLumber;
        bool CanWinMeat;
        bool CanWinMediumBanner;
        bool CanWinMediumQualityBed;
        bool CanWinMediumQualityBench;
        bool CanWinMediumQualityBookcase;
        bool CanWinMediumQualityChair;
        bool CanWinMediumQualityDresser;
        bool CanWinMediumQualityStool;
        bool CanWinMediumQualityTable;
        bool CanWinMediumSteelHangingSign;
        bool CanWinMediumStickBillboard;
        bool CanWinMediumWoodBillboard;
        bool CanWinMoose;
        bool CanWinNasalHelmet;
        bool CanWinOil;
        bool CanWinOnion;
        bool CanWinOnionSeed;
        bool CanWinPigeon;
        bool CanWinPillory;
        bool CanWinPineCone;
        bool CanWinPlagueDoctorMask;
        bool CanWinPlagueVillager;
        bool CanWinPlayerSleeper;
        bool CanWinPoplarSeed;
        bool CanWinPotionOfAntidote;
        bool CanWinPotionOfAppearance;
        bool CanWinRabbit;
        bool CanWinRabbitPelt;
        bool CanWinRavensFlour;
        bool CanWinRavensWater;
        bool CanWinRavensClay;
        bool CanWinRavensIron;
        bool CanWinRavensOil;
        bool CanWinRavensStone;
        bool CanWinRavensWood;
        bool CanWinRawBird;
        bool CanWinReinforcedWoodIronBlock;
        bool CanWinReinforcedWoodIronCorner;
        bool CanWinReinforcedWoodIronDoor;
        bool CanWinReinforcedWoodIronGate;
        bool CanWinReinforcedWoodIronInvertedCorner;
        bool CanWinReinforcedWoodIronRamp;
        bool CanWinReinforcedWoodIronStairs;
        bool CanWinReinforcedWoodIronTrapDoor;
        bool CanWinReinforcedWoodSteelDoor;
        bool CanWinRepairHammer;
        bool CanWinRockingHorse;
        bool CanWinRooster;
        bool CanWinRope;
        bool CanWinRoses;
        bool CanWinSack;
        bool CanWinSalmonFish;
        bool CanWinSawmill;
        bool CanWinScythe;
        bool CanWinSeagull;
        bool CanWinShardanaMask;
        bool CanWinSharpRock;
        bool CanWinSheep;
        bool CanWinSiegeworks;
        bool CanWinSimpleHelmet;
        bool CanWinSmallBanner;
        bool CanWinSmallGallows;
        bool CanWinSmallIronCage;
        bool CanWinSmallIronHangingCage;
        bool CanWinSmallSteelHangingSign;
        bool CanWinSmallSteelSignpost;
        bool CanWinSmallStickSignpost;
        bool CanWinSmallWallLantern;
        bool CanWinSmallWallTorch;
        bool CanWinSmallWoodHangingSign;
        bool CanWinSmallWoodSignpost;
        bool CanWinSmelter;
        bool CanWinSmithy;
        bool CanWinSodBlock;
        bool CanWinSodCorner;
        bool CanWinSodInvertedCorner;
        bool CanWinSodRamp;
        bool CanWinSodStairs;
        bool CanWinSpinningWheel;
        bool CanWinSplinteredClub;
        bool CanWinSpruceBranchesBlock;
        bool CanWinSpruceBranchesCorner;
        bool CanWinSpruceBranchesInvertedCorner;
        bool CanWinSpruceBranchesRamp;
        bool CanWinSpruceBranchesStairs;
        bool CanWinStag;
        bool CanWinStandingIronTorch;
        bool CanWinSteelAxe;
        bool CanWinSteelBattleAxe;
        bool CanWinSteelBattleHammer;
        bool CanWinSteelBolt;
        bool CanWinSteelBuckler;
        bool CanWinSteelCage;
        bool CanWinSteelChest;
        bool CanWinSteelCompound;
        bool CanWinSteelCrest;
        bool CanWinSteelDagger;
        bool CanWinSteelFlangedMace;
        bool CanWinSteelGreatsword;
        bool CanWinSteelHalbred;
        bool CanWinSteelHatchet;
        bool CanWinSteelHeater;
        bool CanWinSteelIngot;
        bool CanWinSteelJavelin;
        bool CanWinSteelMorningStarMace;
        bool CanWinSteelPickaxe;
        bool CanWinSteelPictureFrame;
        bool CanWinSteelPlateBoots;
        bool CanWinSteelPlateGauntlets;
        bool CanWinSteelPlateHelmet;
        bool CanWinSteelPlatePants;
        bool CanWinSteelPlateVest;
        bool CanWinSteelSpear;
        bool CanWinSteelStarMace;
        bool CanWinSteelSword;
        bool CanWinSteelThrowingBattleAxe;
        bool CanWinSteelThrowingKnife;
        bool CanWinSteelTippedArrow;
        bool CanWinSteelTower;
        bool CanWinSteelWarHammer;
        bool CanWinSteelWoodCuttersAxe;
        bool CanWinSticks;
        bool CanWinStiffBed;
        bool CanWinStone;
        bool CanWinStoneArch;
        bool CanWinStoneArrow;
        bool CanWinStoneBlock;
        bool CanWinStoneCorner;
        bool CanWinStoneCutter;
        bool CanWinStoneDagger;
        bool CanWinStoneFireplace;
        bool CanWinStoneHatchet;
        bool CanWinStoneInvertedCorner;
        bool CanWinStoneJavelin;
        bool CanWinStonePickaxe;
        bool CanWinStoneRamp;
        bool CanWinStoneSlab;
        bool CanWinStoneSlitWindow;
        bool CanWinStoneSpear;
        bool CanWinStoneStairs;
        bool CanWinStoneSword;
        bool CanWinStoneThrowingAxe;
        bool CanWinStoneThrowingKnife;
        bool CanWinStoneTotem;
        bool CanWinStoneWoodCuttersAxe;
        bool CanWinTabard;
        bool CanWinTannery;
        bool CanWinTearsOfTheGods;
        bool CanWinThatchBlock;
        bool CanWinThatchCorner;
        bool CanWinThatchInvertedCorner;
        bool CanWinThatchRamp;
        bool CanWinThatchStairs;
        bool CanWinTheaterMaskGoldRed;
        bool CanWinTheaterMaskWhiteBlue;
        bool CanWinTheaterMaskWhiteGold;
        bool CanWinTheaterMaskWhiteRed;
        bool CanWinTheatreMaskComedy;
        bool CanWinTheatreMaskTragedy;
        bool CanWinThrowingStone;
        bool CanWinTinker;
        bool CanWinTorch;
        bool CanWinTrebuchet;
        bool CanWinTrebuchetHayBale;
        bool CanWinTrebuchetStone;
        bool CanWinWallLantern;
        bool CanWinWallTorch;
        bool CanWinWarDrum;
        bool CanWinWasp;
        bool CanWinWater;
        bool CanWinWateringPot;
        bool CanWinWell;
        bool CanWinWencelasHelmet;
        bool CanWinWerewolf;
        bool CanWinWhip;
        bool CanWinWolf;
        bool CanWinWolfPelt;
        bool CanWinWood;
        bool CanWinWoodArrow;
        bool CanWinWoodBarricade;
        bool CanWinWoodBlock;
        bool CanWinWoodBracers;
        bool CanWinWoodBuckler;
        bool CanWinWoodCage;
        bool CanWinWoodChest;
        bool CanWinWoodCorner;
        bool CanWinWoodDoor;
        bool CanWinWoodDrawbridge;
        bool CanWinWoodFlute;
        bool CanWinWoodGate;
        bool CanWinWoodHeater;
        bool CanWinWoodHelmet;
        bool CanWinWoodInvertedCorner;
        bool CanWinWoodJavelin;
        bool CanWinWoodLedge;
        bool CanWinWoodMace;
        bool CanWinWoodPictureFrame;
        bool CanWinWoodRamp;
        bool CanWinWoodSandals;
        bool CanWinWoodShortBow;
        bool CanWinWoodShutters;
        bool CanWinWoodSkirt;
        bool CanWinWoodSpear;
        bool CanWinWoodSpikes;
        bool CanWinWoodStairs;
        bool CanWinWoodStick;
        bool CanWinWoodSword;
        bool CanWinWoodTotem;
        bool CanWinWoodTower;
        bool CanWinWoodVest;
        bool CanWinWoodworking;
        bool CanWinWool;
        bool CanWinWorkBench;
        bool CanWinWorms;
        bool CanWinJackpot;
        bool CanWinSteelArmorSetJackpot;
        bool CanWinIronArmorSetJackpot;
        bool CanWinLeatherArmorSetJackpot;
        bool CanWinBuilderSetJackpot;
        bool CanWinRobbed;
        bool CanWinGangAttack;
        bool CanWinGold1K;
        bool CanWinGold10K;
        bool CanWinGold100K;

        #region Initialization

        void Init()
        {
            LoadDefaultConfig();
            LoadDefaultMessages();
        }

        #endregion
        #region [LANGUAGE API]
        private void LoadDefaultMessages() => lang.RegisterMessages(new Dictionary<string, string>
            {
            { "1", "[FF0000]KingsLottery[FFFFFF] - Gamble for some awesome rewards or lose it all!" },
            { "2", "[FFFFFF]You Won  [00FF00] {0} [00FFFF] {1}" },
            { "3", "[00FFFF]Kings Lottery" },
            { "4", "[00FF00]Play Ticket" },
            { "5", "[00FF00]Play" },
            { "6", "[00FF00]******JACKPOT******" },
            { "7", "[00FF00]*****STEEL ARMOR SET JACKPOT*****" },
            { "8", "[00FF00]*****IRON ARMOR SET JACKPOT*****" },
            { "9", "[00FF00]**********LEATHER ARMOR SET JACKPOT*********" },
            { "10", "[00FF00]*****BUILDER SET JACKPOT*****"},
            { "11", "[FF0000]KingsLottery[FFFFFF]You were knocked out and your Lottery ticket was stolen!!!! You wake up in a pool of your own  [00FF00] 10 [00FFFF]Blood"},
            { "12", "[FFFFFF]Ouch!!!"},
            { "13", "[FFFFFF]Stop!!!"},
            { "14", "[FFFFFF]I think I am blacking out............................."},
            { "15", "[FF0000]KingsLottery[FFFFFF]A Gang of Bandits has brutally attacked you and has stolen your Lottery ticket, you are lying in a pool of your own Blood !!!  "},
            { "16", "[FF0000]KingsLottery[FFFFFF]Eyewitnesses have run to help you and alerted the the Authorities who have caught the Gang of Bandits and a Reward has been Given !!!"},
            { "17", "[FFFFFF]You recieve [00FF00] 10 [00FFFF]Bandages"},
            { "18", "[FFFFFF]You recieve [00FF00] 10 [00FFFF]Water"},
            { "19", "[FFFFFF]You recieve [00FF00] 10 [00FFFF]Cooked Meat"},
            { "20", "[FFFFFF]The Authorities share the Reward with you. Your share is [00FF00] 10 [00FFFF]Bat[FFFFFF]"},
            { "21", "[FFFFFF]and [00FF00] 10 [00FFFF]Diamonds[FFFFFF]"},
            { "22", "[FF0000]KingsLottery[FFFFFF]I just hope you survived your attack and are now a rich man , if you died you lost it all!!!"},
            { "23", "[FF0000]KingsLottery[FFFFFF]**************  BANDITS ATTACK YOU  *********************"},
            { "24", "[FF0000]KingsLottery[FFFFFF] You Just Won  [00FF00]1,000 [FFFF00]gold[FFFFFF]!!!     "},
            { "25", "[FF0000]KingsLottery[FFFFFF] You Just Won  [00FF00]10,000 [FFFF00]gold[FFFFFF]!!!    "},
            { "26", "[FF0000]KingsLottery[FFFFFF] You Just Won  [00FF00]100,000 [FFFF00]gold[FFFFFF]!!!   "},
            { "27", "[FF0000]KingsLottery[FFFFFF], You have less than 5 inventory slots free which would be required to win ArmorSets or the Jackpot, so we will cancel. Make room in your inventory and try again!"},
            { "28", "[FF0000]KingsLottery[FFFFFF] : It looks like you don't have[00FF00] 4 [FFFF00]Iron Ingots[FFFFFF] to play!!"},
            { "29", "[FF0000]KingsLottery[FFFFFF] : It looks like you don't have[00FF00] 1 [FFFF00]Steel Ingots[FFFFFF] to play!!"},
            { "30", "[FF0000]KingsLottery[FFFFFF]Congradulations"},
            { "31", "[FF0000]KingsLottery[FFFFFF] : It looks like you don't have[00FF00] 1 [FFFF00]Bat Token[FFFFFF] to play!!"},
            { "32", "[FF0000]KingsLottery[FFFFFF] Type [00FF00]/lottery[FFFFFF]  to play the KingsLottery Type [00FF00]/ticket[FFFFFF] to see what items are needed to play.Type [00FF00]/jackpots[FFFFFF] to see what Jackpots are available to win."},
            { "33", "[FF0000]KingsLottery[FFFFFF] You have a chance to win any of the 439 items in game , full Armor Sets , Gold [FF0000](requires GrandExchange Plugin)[FFFFFF] or chance to win one of many JackPots !!"},
            { "34", "[FF0000]KingsLottery[FFFFFF] Tickets cost vary from [00FF00] Free [FFFFFF] or [00FF00]4-Iron Ingots[FFFFFF] or [00FF00] 1-Steel Ingot[FFFFFF] or [00FF00] 1-Bat [FFFFFF] or [00FF00] ALL 3 [FFFFFF] to play the KingsLottery"},
            { "35", "[FF0000]KingsLottery[FFFFFF] All Items , Jackpots , Gold Rewards , and Items that are used to play Kings Lottery can be turned on or off in config file."},
            { "36", "[FF0000]KingsLottery[FFFFFF] Tickets will cost you "},
            { "37", "[FF0000]KingsLottery[FFFFFF] [00FF00] 4 [FFFF00] Iron Ingots [FFFFFF]  "},
            { "38", "[FF0000]KingsLottery[FFFFFF] [00FF00] 1 [FFFF00] Steel Ingot [FFFFFF]  "},
            { "39", "[FF0000]KingsLottery[FFFFFF] [00FF00] 1 [FFFF00] Bat Token [FFFFFF]  "},
            { "40", "[FF0000]KingsLottery[FFFFFF] JackPot is [00FF00] 10 [FFFF00] Bat : [FFFFFF][00FF00] 1 [FFFF00] Trebuchet : [00FF00] 1 [FFFF00] War Drum : [00FF00] 20 [FFFF00] Trebuchet Stone : [00FF00] 1 [FFFF00] Steel Greatsword [00FF00]"},
            { "41", "[FF0000]KingsLottery[FFFFFF] Steel Armor JackPot is [00FF00] 1 [FFFF00] Steel Plate Boots : [FFFFFF][00FF00] 1 [FFFF00] Steel Plate Gauntlets : [00FF00] 1 [FFFF00]Steel Plate Helmet : [00FF00] 1 [FFFF00] Steel Plate pants : [00FF00] 1 [FFFF00] Steel Plate Vest[00FF00]"},
            { "42", "[FF0000]KingsLottery[FFFFFF] Iron Armor JackPot is [00FF00] 1 [FFFF00] Iron Plate Boots : [FFFFFF][00FF00] 1 [FFFF00] Iron Plate Gauntlets : [00FF00] 1 [FFFF00]Iron Plate Helmet : [00FF00] 1 [FFFF00] Iron Plate Pants : [00FF00] 1 [FFFF00] Iron Plate Vest[00FF00]"},
            { "43", "[FF0000]KingsLottery[FFFFFF] Leather Armor JackPot is [00FF00] 1 [FFFF00] Light Leather Boots : [FFFFFF][00FF00] 1 [FFFF00] Light Leather Bracers : [00FF00] 1 [FFFF00]Light Leather Helmet : [00FF00] 1 [FFFF00] Light Leather Pants : [00FF00] 1 [FFFF00] Light Leather Vest[00FF00]"},
            { "44", "[FF0000]KingsLottery[FFFFFF] Builder JackPot is [00FF00] 700 [FFFF00] Stone Block : [FFFFFF][00FF00] 100 [FFFF00] Stone Ramp : [00FF00] 100 [FFFF00] Stone Stairs : [00FF00] 2 [FFFF00] Iron Door : [00FF00] 2 [FFFF00] Iron Gate [00FF00]"},
            { "45", "[FF0000]KingsLottery[FFFFFF] You get Robbed by Bandits you collect [00FF00] 10 [FFFF00] Blood [FFFFFF]"},
            { "46", "[FF0000]KingsLottery[FFFFFF] You get Robbed by a Gang of Bandits taking Damage but you get [00FF00] 10 [FFFF00] Bandages : [FFFFFF][00FF00] 10 [FFFF00] Cooked Meat : [00FF00] 10 [FFFF00] Water : [00FF00] 10 [FFFF00] Bat : [00FF00] 10 [FFFF00] Diamonds [00FF00]"},
            { "47", "[FF0000]KingsLottery[FFFFFF] You Win [00FF00] 1,000 [FFFF00] Gold [FFFFFF] !! "},
            { "48", "[FF0000]KingsLottery[FFFFFF] You Win [00FF00] 10,000 [FFFF00] Gold [FFFFFF] !! "},
            { "49", "[FF0000]KingsLottery[FFFFFF] You Win [00FF00] 100,000 [FFFF00] Gold [FFFFFF] !! "},
            { "50", "[FF0000]KingsLottery[FFFFFF] You can win one or more of the [00FF00] 439 [FFFF00] Items [FFFFFF] in game!!"},
            { "51", "[FF0000]KingsLottery[FFFFFF][00FF00]*********************************************************"},
            { "52", "[FF0000]KingsLottery[FFFFFF][00FF00]**********************LOTTERY HELP********************"},
            { "53", "[FF0000]KingsLottery[FFFFFF][00FF00]***********************TICKETS*************************"},
            { "54", "[FF0000]KingsLottery[FFFFFF][00FF00]***********************JACKPOTS************************"}
            }, this);
        #endregion

        #region [VARIABLES]
        string GetMessage(string key, string userId = null) => lang.GetMessage(key, this, userId);
        #endregion

        #region Config
        T GetConfig<T>(string name, T defaultValue) => Config[name] == null ? defaultValue : (T)System.Convert.ChangeType(Config[name], typeof(T));

        protected override void LoadDefaultConfig()
        {
            Config["Lottery Bat Used To Play"] = LotteryBatUsedToPlay = GetConfig("Lottery Bat Used To Play", true);
            Config["Lottery Iron Ingot Used To Play"] = LotteryIronIngotUsedToPlay = GetConfig("Lottery Iron Ingot Used To Play", false);
            Config["Lottery Steel Ingot Used To Play"] = LotterySteelIngotUsedToPlay = GetConfig("Lottery Steel Ingot Used To Play", false);
            Config["Player Can Win AdvancedFletcher"] = CanWinAdvancedFletcher = GetConfig("Player Can Win AdvancedFletcher", true);
            Config["Player Can Win AfricanMask"] = CanWinAfricanMask = GetConfig("Player Can Win AfricanMask", true);
            Config["Player Can Win AmberjackFish"] = CanWinAmberjackFish = GetConfig("Player Can Win AmberjackFish", true);
            Config["Player Can Win AncientCrown"] = CanWinAncientCrown = GetConfig("Player Can Win AncientCrown", false);
            Config["Player Can Win AncientSword"] = CanWinAncientSword = GetConfig("Player Can Win AncientSword", false);
            Config["Player Can Win Anvil"] = CanWinAnvil = GetConfig("Player Can Win Anvil", true);
            Config["Player Can Win Apple"] = CanWinApple = GetConfig("Player Can Win Apple", true);
            Config["Player Can Win AppleSeed"] = CanWinAppleSeed = GetConfig("Player Can Win AppleSeed", true);
            Config["Player Can Win ArcheryTarget"] = CanWinArcheryTarget = GetConfig("Player Can Win ArcheryTarget", true);
            Config["Player Can Win AsianMask"] = CanWinAsianMask = GetConfig("Player Can Win AsianMask", true);
            Config["Player Can Win AsianTribalMask"] = CanWinAsianTribalMask = GetConfig("Player Can Win AsianTribalMask", true);
            Config["Player Can Win BabyChicken"] = CanWinBabyChicken = GetConfig("Player Can Win BabyChicken", false);
            Config["Player Can Win BakedClay"] = CanWinBakedClay = GetConfig("Player Can Win BakedClay", true);
            Config["Player Can Win Ballista"] = CanWinBallista = GetConfig("Player Can Win Ballista", true);
            Config["Player Can Win BallistaBolt"] = CanWinBallistaBolt = GetConfig("Player Can Win BallistaBolt", true);
            Config["Player Can Win Bandage"] = CanWinBandage = GetConfig("Player Can Win Bandage", true);
            Config["Player Can Win BanquetTable"] = CanWinBanquetTable = GetConfig("Player Can Win BanquetTable", true);
            Config["Player Can Win BascinetHelmet"] = CanWinBascinetHelmet = GetConfig("Player Can Win BascinetHelmet", true);
            Config["Player Can Win BascinetPointedHelmet"] = CanWinBascinetPointedHelmet = GetConfig("Player Can Win BascinetPointedHelmet", true);
            Config["Player Can Win BassFish"] = CanWinBassFish = GetConfig("Player Can Win	BassFish", true);
            Config["Player Can Win Bat"] = CanWinBat = GetConfig("Player Can Win Bat", true);
            Config["Player Can Win BatWing"] = CanWinBatWing = GetConfig("Player Can Win BatWing", true);
            Config["Player Can Win BeanSeed"] = CanWinBeanSeed = GetConfig("Player Can Win BeanSeed", true);
            Config["Player Can Win BearHide"] = CanWinBearHide = GetConfig("Player Can Win BearHide", true);
            Config["Player Can Win BearSkinRug"] = CanWinBearSkinRug = GetConfig("Player Can Win BearSkinRug", true);
            Config["Player Can Win Beet"] = CanWinBeet = GetConfig("Player Can Win Beet", true);
            Config["Player Can Win BeetSeed"] = CanWinBeetSeed = GetConfig("Player Can Win BeetSeed", true);
            Config["Player Can Win BellGong"] = CanWinBellGong = GetConfig("Player Can Win BellGong", true);
            Config["Player Can Win Bellows"] = CanWinBellows = GetConfig("Player Can Win Bellows", true);
            Config["Player Can Win BentHorn"] = CanWinBentHorn = GetConfig("Player Can Win	BentHorn", true);
            Config["Player Can Win Berries"] = CanWinBerries = GetConfig("Player Can Win Berries", true);
            Config["Player Can Win BerrySeed"] = CanWinBerrySeed = GetConfig("Player Can Win BerrySeed", true);
            Config["Player Can Win BladedPillar"] = CanWinBladedPillar = GetConfig("Player Can Win	BladedPillar", true);
            Config["Player Can Win Blood"] = CanWinBlood = GetConfig("Player Can Win Blood", true);
            Config["Player Can Win Bone"] = CanWinBone = GetConfig("Player Can Win	Bone", true);
            Config["Player Can Win BoneAxe"] = CanWinBoneAxe = GetConfig("Player Can Win BoneAxe", true);
            Config["Player Can Win BoneDagger"] = CanWinBoneDagger = GetConfig("Player Can Win BoneDagger", true);
            Config["Player Can Win BoneHorn"] = CanWinBoneHorn = GetConfig("Player Can Win BoneHorn", true);
            Config["Player Can Win BoneLongbow"] = CanWinBoneLongbow = GetConfig("Player Can Win BoneLongbow", true);
            Config["Player Can Win BoneSpikedClub"] = CanWinBoneSpikedClub = GetConfig("Player Can Win BoneSpikedClub", true);
            Config["Player Can Win Bread"] = CanWinBread = GetConfig("Player Can Win Bread", true);
            Config["Player Can Win BugNet"] = CanWinBugNet = GetConfig("Player Can Win BugNet", true);
            Config["Player Can Win BurntBird"] = CanWinBurntBird = GetConfig("Player Can Win BurntBird", true);
            Config["Player Can Win BurntMeat"] = CanWinBurntMeat = GetConfig("Player Can Win BurntMeat", true);
            Config["Player Can Win Butterfly"] = CanWinButterfly = GetConfig("Player Can Win Butterfly", true);
            Config["Player Can Win Cabbage"] = CanWinCabbage = GetConfig("Player Can Win Cabbage", true);
            Config["Player Can Win CabbageSeed"] = CanWinCabbageSeed = GetConfig("Player Can Win CabbageSeed", true);
            Config["Player Can Win Campfire"] = CanWinCampfire = GetConfig("Player Can Win Campfire", true);
            Config["Player Can Win Candle"] = CanWinCandle = GetConfig("Player Can Win Candle", true);
            Config["Player Can Win CandleStand"] = CanWinCandleStand = GetConfig("Player Can Win CandleStand", true);
            Config["Player Can Win Carrot"] = CanWinCarrot = GetConfig("Player Can Win Carrot", true);
            Config["Player Can Win CarrotSeed"] = CanWinCarrotSeed = GetConfig("Player Can Win CarrotSeed", true);
            Config["Player Can Win CatMask"] = CanWinCatMask = GetConfig("Player Can Win CatMask", true);
            Config["Player Can Win Chandelier"] = CanWinChandelier = GetConfig("Player Can Win Chandelier", true);
            Config["Player Can Win ChapelDeFerHelmet"] = CanWinChapelDeFerHelmet = GetConfig("Player Can Win ChapelDeFerHelmet", true);
            Config["Player Can Win ChapelDeFerRoundedHelmet"] = CanWinChapelDeFerRoundedHelmet = GetConfig("Player Can Win ChapelDeFerRoundedHelmet", true);
            Config["Player Can Win Charcoal"] = CanWinCharcoal = GetConfig("Player Can Win Charcoal", true);
            Config["Player Can Win Chicken"] = CanWinChicken = GetConfig("Player Can Win Chicken", false);
            Config["Player Can Win Clay"] = CanWinClay = GetConfig("Player Can Win Clay", true);
            Config["Player Can Win ClayBlock"] = CanWinClayBlock = GetConfig("Player Can Win ClayBlock", true);
            Config["Player Can Win ClayCorner"] = CanWinClayCorner = GetConfig("Player Can Win ClayCorner", true);
            Config["Player Can Win ClayInvertedCorner"] = CanWinClayInvertedCorner = GetConfig("Player Can Win ClayInvertedCorner", true);
            Config["Player Can Win ClayRamp"] = CanWinClayRamp = GetConfig("Player Can Win ClayRamp", true);
            Config["Player Can Win ClayStairs"] = CanWinClayStairs = GetConfig("Player Can Win ClayStairs", true);
            Config["Player Can Win CobblestoneBlock"] = CanWinCobblestoneBlock = GetConfig("Player Can Win CobblestoneBlock", true);
            Config["Player Can Win CobblestoneCorner"] = CanWinCobblestoneCorner = GetConfig("Player Can Win CobblestoneCorner", true);
            Config["Player Can Win CobblestoneInvertedCorner"] = CanWinCobblestoneInvertedCorner = GetConfig("Player Can Win CobblestoneInvertedCorner", true);
            Config["Player Can Win CobblestoneRamp"] = CanWinCobblestoneRamp = GetConfig("Player Can Win CobblestoneRamp", true);
            Config["Player Can Win CobblestoneStairs"] = CanWinCobblestoneStairs = GetConfig("Player Can Win CobblestoneStairs", true);
            Config["Player Can Win CookedBeans"] = CanWinCookedBeans = GetConfig("Player Can Win CookedBeans", true);
            Config["Player Can Win CookedBird"] = CanWinCookedBird = GetConfig("Player Can Win CookedBird", true);
            Config["Player Can Win CookedMeat"] = CanWinCookedMeat = GetConfig("Player Can Win CookedMeat", true);
            Config["Player Can Win Crab"] = CanWinCrab = GetConfig("Player Can Win Crab", false);
            Config["Player Can Win Crossbow"] = CanWinCrossbow = GetConfig("Player Can Win Crossbow", true);
            Config["Player Can Win Crow"] = CanWinCrow = GetConfig("Player Can Win Crow", false);
            Config["Player Can Win Deer"] = CanWinDeer = GetConfig("Player Can Win Deer", false);
            Config["Player Can Win DeerHeadTrophy"] = CanWinDeerHeadTrophy = GetConfig("Player Can Win DeerHeadTrophy", true);
            Config["Player Can Win DeerLegClub"] = CanWinDeerLegClub = GetConfig("Player Can Win DeerLegClub", true);
            Config["Player Can Win DefensiveBarricade"] = CanWinDefensiveBarricade = GetConfig("Player Can Win DefensiveBarricade", true);
            Config["Player Can Win Diamond"] = CanWinDiamond = GetConfig("Player Can Win Diamond", true);
            Config["Player Can Win Dirt"] = CanWinDirt = GetConfig("Player Can Win Dirt", true);
            Config["Player Can Win DjembeDrum"] = CanWinDjembeDrum = GetConfig("Player Can Win DjembeDrum", true);
            Config["Player Can Win DriftwoodClub"] = CanWinDriftwoodClub = GetConfig("Player Can Win DriftwoodClub", true);
            Config["Player Can Win Duck"] = CanWinDuck = GetConfig("Player Can Win Duck", false);
            Config["Player Can Win DuckFeet"] = CanWinDuckFeet = GetConfig("Player Can Win DuckFeet", true);
            Config["Player Can Win ExecutionersAxe"] = CanWinExecutionersAxe = GetConfig("Player Can Win ExecutionersAxe", true);
            Config["Player Can Win ExplosiveKeg"] = CanWinExplosiveKeg = GetConfig("Player Can Win ExplosiveKeg", true);
            Config["Player Can Win Fang"] = CanWinFang = GetConfig("Player Can Win Fang", true);
            Config["Player Can Win Fat"] = CanWinFat = GetConfig("Player Can Win Fat", true);
            Config["Player Can Win Feather"] = CanWinFeather = GetConfig("Player Can Win Feather", true);
            Config["Player Can Win Fern"] = CanWinFern = GetConfig("Player Can Win Fern", true);
            Config["Player Can Win FernBracers"] = CanWinFernBracers = GetConfig("Player Can Win FernBracers", true);
            Config["Player Can Win FernHelmet"] = CanWinFernHelmet = GetConfig("Player Can Win	FernHelmet", true);
            Config["Player Can Win FernSandals"] = CanWinFernSandals = GetConfig("Player Can Win FernSandals", true);
            Config["Player Can Win FernSkirt"] = CanWinFernSkirt = GetConfig("Player Can Win FernSkirt", true);
            Config["Player Can Win FernVest"] = CanWinFernVest = GetConfig("Player Can Win FernVest", true);
            Config["Player Can Win FireFly"] = CanWinFireFly = GetConfig("Player Can Win FireFly", true);
            Config["Player Can Win FireWater"] = CanWinFireWater = GetConfig("Player Can Win FireWater", true);
            Config["Player Can Win Firepit"] = CanWinFirepit = GetConfig("Player Can Win Firepit", true);
            Config["Player Can Win FishingRod"] = CanWinFishingRod = GetConfig("Player Can Win	FishingRod", true);
            Config["Player Can Win FlatTopHelmet"] = CanWinFlatTopHelmet = GetConfig("Player Can Win FlatTopHelmet", true);
            Config["Player Can Win Flax"] = CanWinFlax = GetConfig("Player Can Win Flax", true);
            Config["Player Can Win Fletcher"] = CanWinFletcher = GetConfig("Player Can Win Fletcher", true);
            Config["Player Can Win Flour"] = CanWinFlour = GetConfig("Player Can Win Flour", true);
            Config["Player Can Win FlowerBracers"] = CanWinFlowerBracers = GetConfig("Player Can Win FlowerBracers", true);
            Config["Player Can Win FlowerHelmet"] = CanWinFlowerHelmet = GetConfig("Player Can Win FlowerHelmet", true);
            Config["Player Can Win FlowerSandals"] = CanWinFlowerSandals = GetConfig("Player Can Win FlowerSandals", true);
            Config["Player Can Win FlowerSkirt"] = CanWinFlowerSkirt = GetConfig("Player Can Win FlowerSkirt", true);
            Config["Player Can Win FlowerVest"] = CanWinFlowerVest = GetConfig("Player Can Win FlowerVest", true);
            Config["Player Can Win Flowers"] = CanWinFlowers = GetConfig("Player Can Win Flowers", true);
            Config["Player Can Win FluffyBed"] = CanWinFluffyBed = GetConfig("Player Can Win FluffyBed", true);
            Config["Player Can Win Fly"] = CanWinFly = GetConfig("Player Can Win Fly", true);
            Config["Player Can Win ForestSprite"] = CanWinForestSprite = GetConfig("Player Can Win ForestSprite", true);
            Config["Player Can Win Fuse"] = CanWinFuse = GetConfig("Player Can Win Fuse", true);
            Config["Player Can Win Gazebo"] = CanWinGazebo = GetConfig("Player Can Win Gazebo", true);
            Config["Player Can Win Grain"] = CanWinGrain = GetConfig("Player Can Win Grain", true);
            Config["Player Can Win GrainSeed"] = CanWinGrainSeed = GetConfig("Player Can Win GrainSeed", true);
            Config["Player Can Win Granary"] = CanWinGranary = GetConfig("Player Can Win Granary", true);
            Config["Player Can Win GraveMask"] = CanWinGraveMask = GetConfig("Player Can Win GraveMask", true);
            Config["Player Can Win GreatFireplace"] = CanWinGreatFireplace = GetConfig("Player Can Win GreatFireplace", true);
            Config["Player Can Win GrizzlyBear"] = CanWinGrizzlyBear = GetConfig("Player Can Win GrizzlyBear", false);
            Config["Player Can Win GroundTorch"] = CanWinGroundTorch = GetConfig("Player Can Win GroundTorch", true);
            Config["Player Can Win Guillotine"] = CanWinGuillotine = GetConfig("Player Can Win	Guillotine", true);
            Config["Player Can Win HangingLantern"] = CanWinHangingLantern = GetConfig("Player Can Win	HangingLantern", true);
            Config["Player Can Win HangingTorch"] = CanWinHangingTorch = GetConfig("Player Can Win HangingTorch", true);
            Config["Player Can Win Hay"] = CanWinHay = GetConfig("Player Can Win Hay", true);
            Config["Player Can Win HayBaleTarget"] = CanWinHayBaleTarget = GetConfig("Player Can Win HayBaleTarget", true);
            Config["Player Can Win HayBracers"] = CanWinHayBracers = GetConfig("Player Can Win HayBracers", true);
            Config["Player Can Win HayHelmet"] = CanWinHayHelmet = GetConfig("Player Can Win HayHelmet", true);
            Config["Player Can Win HaySandals"] = CanWinHaySandals = GetConfig("Player Can Win HaySandals", true);
            Config["Player Can Win HaySkirt"] = CanWinHaySkirt = GetConfig("Player Can Win	HaySkirt", true);
            Config["Player Can Win HayVest"] = CanWinHayVest = GetConfig("Player Can Win HayVest", true);
            Config["Player Can Win Heart"] = CanWinHeart = GetConfig("Player Can Win	Heart", true);
            Config["Player Can Win HighQualityBed"] = CanWinHighQualityBed = GetConfig("Player Can Win HighQualityBed", true);
            Config["Player Can Win HighQualityBench"] = CanWinHighQualityBench = GetConfig("Player Can Win HighQualityBench", true);
            Config["Player Can Win HighQualityCabinet"] = CanWinHighQualityCabinet = GetConfig("Player Can Win HighQualityCabinet", true);
            Config["Player Can Win Hoe"] = CanWinHoe = GetConfig("Player Can Win Hoe", true);
            Config["Player Can Win Iron"] = CanWinIron = GetConfig("Player Can Win Iron", true);
            Config["Player Can Win IronAxe"] = CanWinIronAxe = GetConfig("Player Can Win IronAxe", true);
            Config["Player Can Win IronBarWindow"] = CanWinIronBarWindow = GetConfig("Player Can Win IronBarWindow", true);
            Config["Player Can Win IronBattleAxe"] = CanWinIronBattleAxe = GetConfig("Player Can Win IronBattleAxe", true);
            Config["Player Can Win IronBattleHammer"] = CanWinIronBattleHammer = GetConfig("Player Can Win IronBattleHammer", true);
            Config["Player Can Win IronBearTrap"] = CanWinIronBearTrap = GetConfig("Player Can Win IronBearTrap", true);
            Config["Player Can Win IronBuckler"] = CanWinIronBuckler = GetConfig("Player Can Win IronBuckler", true);
            Config["Player Can Win IronChest"] = CanWinIronChest = GetConfig("Player Can Win IronChest", true);
            Config["Player Can Win IronCrest"] = CanWinIronCrest = GetConfig("Player Can Win IronCrest", true);
            Config["Player Can Win IronDagger"] = CanWinIronDagger = GetConfig("Player Can Win IronDagger", true);
            Config["Player Can Win IronDoor"] = CanWinIronDoor = GetConfig("Player Can Win IronDoor", true);
            Config["Player Can Win IronFlangedMace"] = CanWinIronFlangedMace = GetConfig("Player Can Win IronFlangedMace", true);
            Config["Player Can Win IronFloorTorch"] = CanWinIronFloorTorch = GetConfig("Player Can Win IronFloorTorch", true);
            Config["Player Can Win IronForkedSpear"] = CanWinIronForkedSpear = GetConfig("Player Can Win IronForkedSpear", true);
            Config["Player Can Win IronGate"] = CanWinIronGate = GetConfig("Player Can Win	IronGate", true);
            Config["Player Can Win IronHalberd"] = CanWinIronHalberd = GetConfig("Player Can Win IronHalberd", true);
            Config["Player Can Win IronHatchet"] = CanWinIronHatchet = GetConfig("Player Can Win IronHatchet", true);
            Config["Player Can Win IronHeater"] = CanWinIronHeater = GetConfig("Player Can Win	IronHeater", true);
            Config["Player Can Win IronIngot"] = CanWinIronIngot = GetConfig("Player Can Win IronIngot", true);
            Config["Player Can Win IronJavelin"] = CanWinIronJavelin = GetConfig("Player Can Win IronJavelin", true);
            Config["Player Can Win IronMorningStarMace"] = CanWinIronMorningStarMace = GetConfig("Player Can Win IronMorningStarMace", true);
            Config["Player Can Win IronPickaxe"] = CanWinIronPickaxe = GetConfig("Player Can Win IronPickaxe", true);
            Config["Player Can Win IronPlateBoots"] = CanWinIronPlateBoots = GetConfig("Player Can Win IronPlateBoots", true);
            Config["Player Can Win IronPlateGauntlets"] = CanWinIronPlateGauntlets = GetConfig("Player Can Win IronplateGauntlets", true);
            Config["Player Can Win IronPlateHelmet"] = CanWinIronPlateHelmet = GetConfig("Player Can Win IronPlateHelmet", true);
            Config["Player Can Win IronPlatePants"] = CanWinIronPlatePants = GetConfig("Player Can Win IronPlatePants", true);
            Config["Player Can Win IronPlateVest"] = CanWinIronPlateVest = GetConfig("Player Can Win IronPlateVest", true);
            Config["Player Can Win IronShackles"] = CanWinIronShackles = GetConfig("Player Can Win IronShackles", true);
            Config["Player Can Win IronSpear"] = CanWinIronSpear = GetConfig("Player Can Win IronSpear", true);
            Config["Player Can Win IronSpikes"] = CanWinIronSpikes = GetConfig("Player Can Win IronSpikes", true);
            Config["Player Can Win IronSpikes(Hidden)"] = CanWinIronSpikesHidden = GetConfig("Player Can Win IronSpikes(Hidden)", true);
            Config["Player Can Win IronStarMace"] = CanWinIronStarMace = GetConfig("Player Can Win IronStarMace", true);
            Config["Player Can Win IronSword"] = CanWinIronSword = GetConfig("Player Can Win IronSword", true);
            Config["Player Can Win IronThrowingAxe"] = CanWinIronThrowingAxe = GetConfig("Player Can Win IronThrowingAxe", true);
            Config["Player Can Win IronThrowingBattleAxe"] = CanWinIronThrowingBattleAxe = GetConfig("Player Can Win IronThrowingBattleAxe", true);
            Config["Player Can Win IronThrowingKnife"] = CanWinIronThrowingKnife = GetConfig("Player Can Win IronThrowingKnife", true);
            Config["Player Can Win IronTippedArrow"] = CanWinIronTippedArrow = GetConfig("Player Can Win IronTippedArrow", true);
            Config["Player Can Win IronTotem"] = CanWinIronTotem = GetConfig("Player Can Win IronTotem", true);
            Config["Player Can Win IronTower"] = CanWinIronTower = GetConfig("Player Can Win IronTower", true);
            Config["Player Can Win IronWarHammer"] = CanWinIronWarHammer = GetConfig("Player Can Win IronWarHammer", true);
            Config["Player Can Win IronWoodCuttersAxe"] = CanWinIronWoodCuttersAxe = GetConfig("Player Can Win IronWoodCuttersAxe", true);
            Config["Player Can Win JapaneseDemon"] = CanWinJapaneseDemon = GetConfig("Player Can Win JapaneseDemon", true);
            Config["Player Can Win JapaneseMask"] = CanWinJapaneseMask = GetConfig("Player Can Win JapaneseMask", true);
            Config["Player Can Win JesterHat(Green&Pink)"] = CanWinJesterHatGreenPink = GetConfig("Player Can Win JesterHat(Green&Pink)", true);
            Config["Player Can Win JesterHat(Orange&Black)"] = CanWinJesterHatOrangeBlack = GetConfig("Player Can Win JesterHat(Orange&Black)", true);
            Config["Player Can Win JesterHat(Rainbow)"] = CanWinJesterHatRainbow = GetConfig("Player Can Win JesterHat(Rainbow)", true);
            Config["Player Can Win JesterHat(Red)"] = CanWinJesterHatRed = GetConfig("Player Can Win JesterHat(Red)", true);
            Config["Player Can Win JesterMask(Gold&Blue)"] = CanWinJesterMaskGoldBlue = GetConfig("Player Can Win JesterMask(Gold&Blue)", true);
            Config["Player Can Win JesterMask(Gold&Red)"] = CanWinJesterMaskGoldRed = GetConfig("Player Can Win JesterMask(Gold&Red)", true);
            Config["Player Can Win JesterMask(White&Blue)"] = CanWinJesterMaskWhiteBlue = GetConfig("Player Can Win JesterMask(White&Blue)", true);
            Config["Player Can Win JesterMask(White&Gold)"] = CanWinJesterMaskWhiteGold = GetConfig("Player Can Win JesterMask(White&Gold)", true);
            Config["Player Can Win KettleBoardHelmet"] = CanWinKettleBoardHelmet = GetConfig("Player Can Win KettleBoardHelmet", true);
            Config["Player Can Win KettleHat"] = CanWinKettleHat = GetConfig("Player Can Win KettleBoardHat", true);
            Config["Player Can Win KoiFish"] = CanWinKoiFish = GetConfig("Player Can Win KoiFish", true);
            Config["Player Can Win LargeGallows"] = CanWinLargeGallows = GetConfig("Player Can Win LargeGallows", true);
            Config["Player Can Win LargeIronCage"] = CanWinLargeIronCage = GetConfig("Player Can Win LargeIronCage", true);
            Config["Player Can Win LargeIronHangingCage"] = CanWinLargeIronHangingCage = GetConfig("Player Can Win LargeIronHangingCage", true);
            Config["Player Can Win LargeWoodBillboard"] = CanWinLargeWoodBillboard = GetConfig("Player Can Win LargeWoodBillboard", true);
            Config["Player Can Win LeatherCrest"] = CanWinLeatherCrest = GetConfig("Player Can Win LeatherCrest", true);
            Config["Player Can Win LeatherHide"] = CanWinLeatherHide = GetConfig("Player Can Win LeatherHide", true);
            Config["Player Can Win LightLeatherBoots"] = CanWinLightLeatherBoots = GetConfig("Player Can Win LightLeatherBoots", true);
            Config["Player Can Win LightLeatherBracers"] = CanWinLightLeatherBracers = GetConfig("Player Can Win LightLeatherBracers", true);
            Config["Player Can Win LightLeatherHelmet"] = CanWinLightLeatherHelmet = GetConfig("Player Can Win LightLeatherHelmet", true);
            Config["Player Can Win LightLeatherPants"] = CanWinLightLeatherPants = GetConfig("Player Can Win LightLeatherPants", true);
            Config["Player Can Win LightLeatherVest"] = CanWinLightLeatherVest = GetConfig("Player Can Win LightLeatherVest", true);
            Config["Player Can Win Liver"] = CanWinLiver = GetConfig("Player Can Win Liver", true);
            Config["Player Can Win LockPick"] = CanWinLockPick = GetConfig("Player Can Win LockPick", true);
            Config["Player Can Win LogBlock"] = CanWinLogBlock = GetConfig("Player Can Win LogBlock", true);
            Config["Player Can Win LogCorner"] = CanWinLogCorner = GetConfig("Player Can Win LogCorner", true);
            Config["Player Can Win LogFence"] = CanWinLogFence = GetConfig("Player Can Win LogFence", true);
            Config["Player Can Win LogInvertedCorner"] = CanWinLogInvertedCorner = GetConfig("Player Can Win LogInvertedCorner", true);
            Config["Player Can Win LogRamp"] = CanWinLogRamp = GetConfig("Player Can Win LogRamp", true);
            Config["Player Can Win LogStairs"] = CanWinLogStairs = GetConfig("Player Can Win LogStairs", true);
            Config["Player Can Win LongHorn"] = CanWinLongHorn = GetConfig("Player Can Win LongHorn", true);
            Config["Player Can Win LongWoodDrawbridge"] = CanWinLongWoodDrawbridge = GetConfig("Player Can Win LongWoodDrawbridge", true);
            Config["Player Can Win LootSack"] = CanWinLootSack = GetConfig("Player Can Win LootSack", false);
            Config["Player Can Win Lord'sBath"] = CanWinLordsBath = GetConfig("Player Can Win Lord'sBath", true);
            Config["Player Can Win Lord'sBed"] = CanWinLordsBed = GetConfig("Player Can Win Lord'sBed", true);
            Config["Player Can Win Lord'sLargeChair"] = CanWinLordsLargeChair = GetConfig("Player Can Win Lord'sLargeChair", true);
            Config["Player Can Win Lord'sSmallChair"] = CanWinLordsSmallChair = GetConfig("Player Can Win Lord'sSmallChair", true);
            Config["Player Can Win LowQualityBed"] = CanWinLowQualityBed = GetConfig("Player Can Win LowQualityBed", true);
            Config["Player Can Win LowQualityBench"] = CanWinLowQualityBench = GetConfig("Player Can Win LowQualityBench", true);
            Config["Player Can Win LowQualityChair"] = CanWinLowQualityChair = GetConfig("Player Can Win LowQualityChair", true);
            Config["Player Can Win LowQualityFence"] = CanWinLowQualityFence = GetConfig("Player Can Win LowQualityFence", true);
            Config["Player Can Win LowQualityShelf"] = CanWinLowQualityShelf = GetConfig("Player Can Win LowQualityShelf", true);
            Config["Player Can Win LowQualityStool"] = CanWinLowQualityStool = GetConfig("Player Can Win LowQualityStool", true);
            Config["Player Can Win LowQualityTable"] = CanWinLowQualityTable = GetConfig("Player Can Win LowQualityTable", true);
            Config["Player Can Win Lumber"] = CanWinLumber = GetConfig("Player Can Win Lumber", true);
            Config["Player Can Win Meat"] = CanWinMeat = GetConfig("Player Can Win Meat", true);
            Config["Player Can Win MediumBanner"] = CanWinMediumBanner = GetConfig("Player Can Win MediumBanner", true);
            Config["Player Can Win MediumQualityBed"] = CanWinMediumQualityBed = GetConfig("Player Can Win MediumQualityBed", true);
            Config["Player Can Win MediumQualityBench"] = CanWinMediumQualityBench = GetConfig("Player Can Win MediumQualityBench", true);
            Config["Player Can Win MediumQualityBookcase"] = CanWinMediumQualityBookcase = GetConfig("Player Can Win MediumQualityBookcase", true);
            Config["Player Can Win MediumQualityChair"] = CanWinMediumQualityChair = GetConfig("Player Can Win MediumQualityChair", true);
            Config["Player Can Win MediumQualityDresser"] = CanWinMediumQualityDresser = GetConfig("Player Can Win MediumQualityDresser", true);
            Config["Player Can Win MediumQualityStool"] = CanWinMediumQualityStool = GetConfig("Player Can Win MediumQualityStool", true);
            Config["Player Can Win MediumQualityTable"] = CanWinMediumQualityTable = GetConfig("Player Can Win MediumQualityTable", true);
            Config["Player Can Win MediumSteelHangingSign"] = CanWinMediumSteelHangingSign = GetConfig("Player Can Win MediumSteelHangingSign", true);
            Config["Player Can Win MediumStickBillboard"] = CanWinMediumStickBillboard = GetConfig("Player Can Win MediumStickBillboard", true);
            Config["Player Can Win MediumWoodBillboard"] = CanWinMediumWoodBillboard = GetConfig("Player Can Win MediumWoodBillboard", true);
            Config["Player Can Win Moose"] = CanWinMoose = GetConfig("Player Can Win Moose", false);
            Config["Player Can Win NasalHelmet"] = CanWinNasalHelmet = GetConfig("Player Can Win NasalHelmet", true);
            Config["Player Can Win Oil"] = CanWinOil = GetConfig("Player Can Win Oil", true);
            Config["Player Can Win Onion"] = CanWinOnion = GetConfig("Player Can Win Onion", true);
            Config["Player Can Win OnionSeed"] = CanWinOnionSeed = GetConfig("Player Can Win OnionSeed", true);
            Config["Player Can Win Pigeon"] = CanWinPigeon = GetConfig("Player Can Win Pigeon", false);
            Config["Player Can Win Pillory"] = CanWinPillory = GetConfig("Player Can Win Pillory", true);
            Config["Player Can Win PineCone"] = CanWinPineCone = GetConfig("Player Can Win PineCone", true);
            Config["Player Can Win PlagueDoctorMask"] = CanWinPlagueDoctorMask = GetConfig("Player Can Win PlagueDoctorMask", true);
            Config["Player Can Win PlagueVillager"] = CanWinPlagueVillager = GetConfig("Player Can Win PlagueVillager", true);
            Config["Player Can Win PlayerSleeper"] = CanWinPlayerSleeper = GetConfig("Player Can Win PlayerSleeper", false);
            Config["Player Can Win PoplarSeed"] = CanWinPoplarSeed = GetConfig("Player Can Win PoplarSeed", true);
            Config["Player Can Win PotionOfAntidote"] = CanWinPotionOfAntidote = GetConfig("Player Can Win PotionOfAntidote", true);
            Config["Player Can Win PotionOfAppearance"] = CanWinPotionOfAppearance = GetConfig("Player Can Win PotionOfAppearance", true);
            Config["Player Can Win Rabbit"] = CanWinRabbit = GetConfig("Player Can Win Rabbit", false);
            Config["Player Can Win RabbitPelt"] = CanWinRabbitPelt = GetConfig("Player Can Win	RabbitPelt", true);
            Config["Player Can Win Ravens Flour"] = CanWinRavensFlour = GetConfig("Player Can Win Ravens Flour ", true);
            Config["Player Can Win Ravens Water"] = CanWinRavensWater = GetConfig("Player Can Win Ravens Water ", true);
            Config["Player Can Win Ravens Iron"] = CanWinRavensIron = GetConfig("Player Can Win Ravens Iron ", true);
            Config["Player Can Win Ravens Clay"] = CanWinRavensClay = GetConfig("Player Can Win Ravens Clay", true);
            Config["Player Can Win Ravens Oil"] = CanWinRavensOil = GetConfig("Player Can Win Ravens Oil", true);
            Config["Player Can Win Ravens Stone"] = CanWinRavensStone = GetConfig("Player Can Win Ravens Stone", true);
            Config["Player Can Win Ravens Wood"] = CanWinRavensWood = GetConfig("Player Can Win Ravens Wood", true);
            Config["Player Can Win RawBird"] = CanWinRawBird = GetConfig("Player Can Win RawBird", true);
            Config["Player Can Win ReinforcedWood(Iron)Block"] = CanWinReinforcedWoodIronBlock = GetConfig("Player Can Win	ReinforcedWood(Iron)Block", true);
            Config["Player Can Win ReinforcedWood(Iron)Corner"] = CanWinReinforcedWoodIronCorner = GetConfig("Player Can Win ReinforcedWood(Iron)Corner", true);
            Config["Player Can Win ReinforcedWood(Iron)Door"] = CanWinReinforcedWoodIronDoor = GetConfig("Player Can Win ReinforcedWood(Iron)Door", true);
            Config["Player Can Win ReinforcedWood(Iron)Gate"] = CanWinReinforcedWoodIronGate = GetConfig("Player Can Win ReinforcedWood(Iron)Gate", true);
            Config["Player Can Win ReinforcedWood(Iron)InvertedCorner"] = CanWinReinforcedWoodIronInvertedCorner = GetConfig("Player Can Win ReinforcedWood(Iron)InvertedCorner", true);
            Config["Player Can Win ReinforcedWood(Iron)Ramp"] = CanWinReinforcedWoodIronRamp = GetConfig("Player Can Win ReinforcedWood(Iron)Ramp", true);
            Config["Player Can Win ReinforcedWood(Iron)Stairs"] = CanWinReinforcedWoodIronStairs = GetConfig("Player Can Win ReinforcedWood(Iron)Stairs", true);
            Config["Player Can Win ReinforcedWood(Iron)TrapDoor"] = CanWinReinforcedWoodIronTrapDoor = GetConfig("Player Can Win ReinforcedWood(Iron)TrapDoor", true);
            Config["Player Can Win ReinforcedWood(Steel)Door"] = CanWinReinforcedWoodSteelDoor = GetConfig("Player Can Win ReinforcedWood(Steel)Door", true);
            Config["Player Can Win RepairHammer"] = CanWinRepairHammer = GetConfig("Player Can Win RepairHammer", true);
            Config["Player Can Win RockingHorse"] = CanWinRockingHorse = GetConfig("Player Can Win RockingHorse", true);
            Config["Player Can Win Rooster"] = CanWinRooster = GetConfig("Player Can Win Rooster", false);
            Config["Player Can Win Rope"] = CanWinRope = GetConfig("Player Can Win Rope", true);
            Config["Player Can Win Roses"] = CanWinRoses = GetConfig("Player Can Win Roses", true);
            Config["Player Can Win Sack"] = CanWinSack = GetConfig("Player Can Win Sack", false);
            Config["Player Can Win SalmonFish"] = CanWinSalmonFish = GetConfig("Player Can Win SalmonFish", true);
            Config["Player Can Win Sawmill"] = CanWinSawmill = GetConfig("Player Can Win Sawmill", true);
            Config["Player Can Win Scythe"] = CanWinScythe = GetConfig("Player Can Win Scythe", true);
            Config["Player Can Win Seagull"] = CanWinSeagull = GetConfig("Player Can Win Seagull", false);
            Config["Player Can Win ShardanaMask"] = CanWinShardanaMask = GetConfig("Player Can Win ShardanaMask", true);
            Config["Player Can Win SharpRock"] = CanWinSharpRock = GetConfig("Player Can Win SharpRock", true);
            Config["Player Can Win Sheep"] = CanWinSheep = GetConfig("Player Can Win Sheep", false);
            Config["Player Can Win Siegeworks"] = CanWinSiegeworks = GetConfig("Player Can Win Siegeworks", true);
            Config["Player Can Win SimpleHelmet"] = CanWinSimpleHelmet = GetConfig("Player Can Win SimpleHelmet", true);
            Config["Player Can Win SmallBanner"] = CanWinSmallBanner = GetConfig("Player Can Win SmallBanner", true);
            Config["Player Can Win SmallGallows"] = CanWinSmallGallows = GetConfig("Player Can Win SmallGallows", true);
            Config["Player Can Win SmallIronCage"] = CanWinSmallIronCage = GetConfig("Player Can Win SmallIronCage", true);
            Config["Player Can Win SmallIronHangingCage"] = CanWinSmallIronHangingCage = GetConfig("Player Can Win SmallIronHangingCage", true);
            Config["Player Can Win SmallSteelHangingSign"] = CanWinSmallSteelHangingSign = GetConfig("Player Can Win SmallSteelHangingSign", true);
            Config["Player Can Win SmallSteelSignpost"] = CanWinSmallSteelSignpost = GetConfig("Player Can Win SmallSteelSignpost", true);
            Config["Player Can Win SmallStickSignpost"] = CanWinSmallStickSignpost = GetConfig("Player Can Win SmallStickSignpost", true);
            Config["Player Can Win SmallWallLantern"] = CanWinSmallWallLantern = GetConfig("Player Can Win SmallWallLantern", true);
            Config["Player Can Win SmallWallTorch"] = CanWinSmallWallTorch = GetConfig("Player Can Win SmallWallTorch", true);
            Config["Player Can Win SmallWoodHangingSign"] = CanWinSmallWoodHangingSign = GetConfig("Player Can Win SmallWoodHangingSign", true);
            Config["Player Can Win SmallWoodSignpost"] = CanWinSmallWoodSignpost = GetConfig("Player Can Win SmallWoodSignpost", true);
            Config["Player Can Win Smelter"] = CanWinSmelter = GetConfig("Player Can Win Smelter", true);
            Config["Player Can Win Smithy"] = CanWinSmithy = GetConfig("Player Can Win Smithy", true);
            Config["Player Can Win SodBlock"] = CanWinSodBlock = GetConfig("Player Can Win SodBlock", true);
            Config["Player Can Win SodCorner"] = CanWinSodCorner = GetConfig("Player Can Win SodCorner", true);
            Config["Player Can Win SodInvertedCorner"] = CanWinSodInvertedCorner = GetConfig("Player Can Win SodInvertedCorner", true);
            Config["Player Can Win SodRamp"] = CanWinSodRamp = GetConfig("Player Can Win SodRamp", true);
            Config["Player Can Win SodStairs"] = CanWinSodStairs = GetConfig("Player Can Win	SodStairs", true);
            Config["Player Can Win SpinningWheel"] = CanWinSpinningWheel = GetConfig("Player Can Win	SpinningWheel", true);
            Config["Player Can Win SplinteredClub"] = CanWinSplinteredClub = GetConfig("Player Can Win	SplinteredClub", true);
            Config["Player Can Win SpruceBranchesBlock"] = CanWinSpruceBranchesBlock = GetConfig("Player Can Win	SpruceBranchesBlock", true);
            Config["Player Can Win SpruceBranchesCorner"] = CanWinSpruceBranchesCorner = GetConfig("Player Can Win	SpruceBranchesCorner", true);
            Config["Player Can Win SpruceBranchesInvertedCorner"] = CanWinSpruceBranchesInvertedCorner = GetConfig("Player Can Win	SpruceBranchesInvertedCorner	", true);
            Config["Player Can Win SpruceBranchesRamp"] = CanWinSpruceBranchesRamp = GetConfig("Player Can Win	SpruceBranchesRamp	", true);
            Config["Player Can Win SpruceBranchesStairs"] = CanWinSpruceBranchesStairs = GetConfig("Player Can Win	SpruceBranchesStairs	", true);
            Config["Player Can Win Stag"] = CanWinStag = GetConfig("Player Can Win	Stag", false);
            Config["Player Can Win StandingIronTorch"] = CanWinStandingIronTorch = GetConfig("Player Can Win StandingIronTorch", true);
            Config["Player Can Win SteelAxe"] = CanWinSteelAxe = GetConfig("Player Can Win	SteelAxe", true);
            Config["Player Can Win SteelBattleAxe"] = CanWinSteelBattleAxe = GetConfig("Player Can Win	SteelBattleAxe", true);
            Config["Player Can Win SteelBattleHammer"] = CanWinSteelBattleHammer = GetConfig("Player Can Win SteelBattleHammer", true);
            Config["Player Can Win SteelBolt"] = CanWinSteelBolt = GetConfig("Player Can Win SteelBolt", true);
            Config["Player Can Win SteelBuckler"] = CanWinSteelBuckler = GetConfig("Player Can Win	SteelBuckler", true);
            Config["Player Can Win SteelCage"] = CanWinSteelCage = GetConfig("Player Can Win	SteelCage	", true);
            Config["Player Can Win SteelChest"] = CanWinSteelChest = GetConfig("Player Can Win	SteelChest	", true);
            Config["Player Can Win SteelCompound"] = CanWinSteelCompound = GetConfig("Player Can Win	SteelCompound	", true);
            Config["Player Can Win SteelCrest"] = CanWinSteelCrest = GetConfig("Player Can Win	SteelCrest	", true);
            Config["Player Can Win SteelDagger"] = CanWinSteelDagger = GetConfig("Player Can Win	SteelDagger	", true);
            Config["Player Can Win SteelFlangedMace"] = CanWinSteelFlangedMace = GetConfig("Player Can Win	SteelFlangedMace	", true);
            Config["Player Can Win SteelGreatsword"] = CanWinSteelGreatsword = GetConfig("Player Can Win	SteelGreatsword	", true);
            Config["Player Can Win SteelHalbred"] = CanWinSteelHalbred = GetConfig("Player Can Win	SteelHalbred", true);
            Config["Player Can Win SteelHatchet"] = CanWinSteelHatchet = GetConfig("Player Can Win	SteelHatchet", true);
            Config["Player Can Win SteelHeater"] = CanWinSteelHeater = GetConfig("Player Can Win SteelHeater", true);
            Config["Player Can Win SteelIngot"] = CanWinSteelIngot = GetConfig("Player Can Win	SteelIngot", true);
            Config["Player Can Win SteelJavelin"] = CanWinSteelJavelin = GetConfig("Player Can Win SteelJavelin", true);
            Config["Player Can Win SteelMorningStarMace"] = CanWinSteelMorningStarMace = GetConfig("Player Can Win SteelMorningStarMace", true);
            Config["Player Can Win SteelPickaxe"] = CanWinSteelPickaxe = GetConfig("Player Can Win SteelPickaxe", true);
            Config["Player Can Win SteelPictureFrame"] = CanWinSteelPictureFrame = GetConfig("Player Can Win SteelPictureFrame", true);
            Config["Player Can Win SteelPlateBoots"] = CanWinSteelPlateBoots = GetConfig("Player Can Win SteelPlateBoots", true);
            Config["Player Can Win SteelPlateGauntlets"] = CanWinSteelPlateGauntlets = GetConfig("Player Can Win	SteelPlateGauntlets", true);
            Config["Player Can Win SteelPlateHelmet"] = CanWinSteelPlateHelmet = GetConfig("Player Can Win SteelPlateHelmet", true);
            Config["Player Can Win SteelPlatePants"] = CanWinSteelPlatePants = GetConfig("Player Can Win SteelPlatePants", true);
            Config["Player Can Win SteelPlateVest"] = CanWinSteelPlateVest = GetConfig("Player Can Win	SteelPlateVest", true);
            Config["Player Can Win SteelSpear"] = CanWinSteelSpear = GetConfig("Player Can Win	SteelSpear", true);
            Config["Player Can Win SteelStarMace"] = CanWinSteelStarMace = GetConfig("Player Can Win SteelStarMace	", true);
            Config["Player Can Win SteelSword"] = CanWinSteelSword = GetConfig("Player Can Win	SteelSword", true);
            Config["Player Can Win SteelThrowingBattleAxe"] = CanWinSteelThrowingBattleAxe = GetConfig("Player Can Win	SteelThrowingBattleAxe", true);
            Config["Player Can Win SteelThrowingKnife"] = CanWinSteelThrowingKnife = GetConfig("Player Can Win	SteelThrowingKnife", true);
            Config["Player Can Win SteelTippedArrow"] = CanWinSteelTippedArrow = GetConfig("Player Can Win	SteelTippedArrow", true);
            Config["Player Can Win SteelTower"] = CanWinSteelTower = GetConfig("Player Can Win	SteelTower", true);
            Config["Player Can Win SteelWarHammer"] = CanWinSteelWarHammer = GetConfig("Player Can Win	SteelWarHammer", true);
            Config["Player Can Win SteelWoodCuttersAxe"] = CanWinSteelWoodCuttersAxe = GetConfig("Player Can Win SteelWoodCuttersAxe", true);
            Config["Player Can Win Sticks"] = CanWinSticks = GetConfig("Player Can Win	Sticks", true);
            Config["Player Can Win StiffBed"] = CanWinStiffBed = GetConfig("Player Can Win	StiffBed", true);
            Config["Player Can Win Stone"] = CanWinStone = GetConfig("Player Can Win Stone", true);
            Config["Player Can Win StoneArch"] = CanWinStoneArch = GetConfig("Player Can Win StoneArch", true);
            Config["Player Can Win StoneArrow"] = CanWinStoneArrow = GetConfig("Player Can Win	StoneArrow", true);
            Config["Player Can Win StoneBlock"] = CanWinStoneBlock = GetConfig("Player Can Win	StoneBlock", true);
            Config["Player Can Win StoneCorner"] = CanWinStoneCorner = GetConfig("Player Can Win StoneCorner", true);
            Config["Player Can Win StoneCutter"] = CanWinStoneCutter = GetConfig("Player Can Win StoneCutter", true);
            Config["Player Can Win StoneDagger"] = CanWinStoneDagger = GetConfig("Player Can Win StoneDagger", true);
            Config["Player Can Win StoneFireplace"] = CanWinStoneFireplace = GetConfig("Player Can Win	StoneFireplace", true);
            Config["Player Can Win StoneHatchet"] = CanWinStoneHatchet = GetConfig("Player Can Win	StoneHatchet", true);
            Config["Player Can Win StoneInvertedCorner"] = CanWinStoneInvertedCorner = GetConfig("Player Can Win StoneInvertedCorner", true);
            Config["Player Can Win StoneJavelin"] = CanWinStoneJavelin = GetConfig("Player Can Win	StoneJavelin", true);
            Config["Player Can Win StonePickaxe"] = CanWinStonePickaxe = GetConfig("Player Can Win	StonePickaxe", true);
            Config["Player Can Win StoneRamp"] = CanWinStoneRamp = GetConfig("Player Can Win StoneRamp", true);
            Config["Player Can Win StoneSlab"] = CanWinStoneSlab = GetConfig("Player Can Win StoneSlab", true);
            Config["Player Can Win StoneSlitWindow"] = CanWinStoneSlitWindow = GetConfig("Player Can Win	StoneSlitWindow", true);
            Config["Player Can Win StoneSpear"] = CanWinStoneSpear = GetConfig("Player Can Win StoneSpear", true);
            Config["Player Can Win StoneStairs"] = CanWinStoneStairs = GetConfig("Player Can Win	StoneStairs", true);
            Config["Player Can Win StoneSword"] = CanWinStoneSword = GetConfig("Player Can Win StoneSword", true);
            Config["Player Can Win StoneThrowingAxe"] = CanWinStoneThrowingAxe = GetConfig("Player Can Win StoneThrowingAxe", true);
            Config["Player Can Win StoneThrowingKnife"] = CanWinStoneThrowingKnife = GetConfig("Player Can Win StoneThrowingKnife", true);
            Config["Player Can Win StoneTotem"] = CanWinStoneTotem = GetConfig("Player Can Win StoneTotem", true);
            Config["Player Can Win StoneWoodCuttersAxe"] = CanWinStoneWoodCuttersAxe = GetConfig("Player Can Win StoneWoodCuttersAxe", true);
            Config["Player Can Win Tabard"] = CanWinTabard = GetConfig("Player Can Win Tabard", true);
            Config["Player Can Win Tannery"] = CanWinTannery = GetConfig("Player Can Win Tannery", true);
            Config["Player Can Win TearsOfTheGods"] = CanWinTearsOfTheGods = GetConfig("Player Can Win	TearsOfTheGods", true);
            Config["Player Can Win ThatchBlock"] = CanWinThatchBlock = GetConfig("Player Can Win ThatchBlock", true);
            Config["Player Can Win ThatchCorner"] = CanWinThatchCorner = GetConfig("Player Can Win	ThatchCorner", true);
            Config["Player Can Win ThatchInvertedCorner"] = CanWinThatchInvertedCorner = GetConfig("Player Can Win ThatchInvertedCorner", true);
            Config["Player Can Win ThatchRamp"] = CanWinThatchRamp = GetConfig("Player Can Win ThatchRamp", true);
            Config["Player Can Win ThatchStairs"] = CanWinThatchStairs = GetConfig("Player Can Win	ThatchStairs", true);
            Config["Player Can Win TheaterMask(Gold&Red)"] = CanWinTheaterMaskGoldRed = GetConfig("Player Can Win TheaterMask(Gold&Red)", true);
            Config["Player Can Win TheaterMask(White&Blue)"] = CanWinTheaterMaskWhiteBlue = GetConfig("Player Can Win,TheaterMask(White&Blue)", true);
            Config["Player Can Win TheaterMask(White&Gold)"] = CanWinTheaterMaskWhiteGold = GetConfig("Player Can Win	TheaterMask(White&Gold)", true);
            Config["Player Can Win TheaterMask(White&Red)"] = CanWinTheaterMaskWhiteRed = GetConfig("Player Can Win TheaterMask(White&Red)", true);
            Config["Player Can Win TheatreMask(Comedy)"] = CanWinTheatreMaskComedy = GetConfig("Player Can Win TheatreMask(Comedy)", true);
            Config["Player Can Win TheatreMask(Tragedy)"] = CanWinTheatreMaskTragedy = GetConfig("Player Can Win TheatreMask(Tragedy)", true);
            Config["Player Can Win ThrowingStone"] = CanWinThrowingStone = GetConfig("Player Can Win ThrowingStone", true);
            Config["Player Can Win Tinker"] = CanWinTinker = GetConfig("Player Can Win	Tinker", true);
            Config["Player Can Win Torch"] = CanWinTorch = GetConfig("Player Can Win Torch", true);
            Config["Player Can Win Trebuchet"] = CanWinTrebuchet = GetConfig("Player Can Win	Trebuchet", true);
            Config["Player Can Win TrebuchetHayBale"] = CanWinTrebuchetHayBale = GetConfig("Player Can Win	TrebuchetHayBale", true);
            Config["Player Can Win TrebuchetStone"] = CanWinTrebuchetStone = GetConfig("Player Can Win	TrebuchetStone", true);
            Config["Player Can Win WallLantern"] = CanWinWallLantern = GetConfig("Player Can Win WallLantern", true);
            Config["Player Can Win WallTorch"] = CanWinWallTorch = GetConfig("Player Can Win WallTorch", true);
            Config["Player Can Win WarDrum"] = CanWinWarDrum = GetConfig("Player Can Win WarDrum", true);
            Config["Player Can Win Wasp"] = CanWinWasp = GetConfig("Player Can Win	Wasp", true);
            Config["Player Can Win Water"] = CanWinWater = GetConfig("Player Can Win Water", true);
            Config["Player Can Win WateringPot"] = CanWinWateringPot = GetConfig("Player Can Win WateringPot", true);
            Config["Player Can Win Well"] = CanWinWell = GetConfig("Player Can Win	Well", true);
            Config["Player Can Win WencelasHelmet"] = CanWinWencelasHelmet = GetConfig("Player Can Win	WencelasHelmet", true);
            Config["Player Can Win Werewolf"] = CanWinWerewolf = GetConfig("Player Can Win	Werewolf", false);
            Config["Player Can Win Whip"] = CanWinWhip = GetConfig("Player Can Win Whip", true);
            Config["Player Can Win Wolf"] = CanWinWolf = GetConfig("Player Can Win Wolf", false);
            Config["Player Can Win WolfPelt"] = CanWinWolfPelt = GetConfig("Player Can Win WolfPelt", true);
            Config["Player Can Win Wood"] = CanWinWood = GetConfig("Player Can Win Wood", true);
            Config["Player Can Win WoodArrow"] = CanWinWoodArrow = GetConfig("Player Can Win WoodArrow", true);
            Config["Player Can Win WoodBarricade"] = CanWinWoodBarricade = GetConfig("Player Can Win WoodBarricade", true);
            Config["Player Can Win WoodBlock"] = CanWinWoodBlock = GetConfig("Player Can Win	WoodBlock", true);
            Config["Player Can Win WoodBracers"] = CanWinWoodBracers = GetConfig("Player Can Win	WoodBracers", true);
            Config["Player Can Win WoodBuckler"] = CanWinWoodBuckler = GetConfig("Player Can Win	WoodBuckler", true);
            Config["Player Can Win WoodCage"] = CanWinWoodCage = GetConfig("Player Can Win WoodCage", true);
            Config["Player Can Win WoodChest"] = CanWinWoodChest = GetConfig("Player Can Win	WoodChest", true);
            Config["Player Can Win WoodCorner"] = CanWinWoodCorner = GetConfig("Player Can Win WoodCorner", true);
            Config["Player Can Win WoodDoor"] = CanWinWoodDoor = GetConfig("Player Can Win	WoodDoor", true);
            Config["Player Can Win WoodDrawbridge"] = CanWinWoodDrawbridge = GetConfig("Player Can Win	WoodDrawbridge", true);
            Config["Player Can Win WoodFlute"] = CanWinWoodFlute = GetConfig("Player Can Win WoodFlute", true);
            Config["Player Can Win WoodGate"] = CanWinWoodGate = GetConfig("Player Can Win	WoodGate", true);
            Config["Player Can Win WoodHeater"] = CanWinWoodHeater = GetConfig("Player Can Win WoodHeater", true);
            Config["Player Can Win WoodHelmet"] = CanWinWoodHelmet = GetConfig("Player Can Win WoodHelmet", true);
            Config["Player Can Win WoodInvertedCorner"] = CanWinWoodInvertedCorner = GetConfig("Player Can Win	WoodInvertedCorner", true);
            Config["Player Can Win WoodJavelin"] = CanWinWoodJavelin = GetConfig("Player Can Win WoodJavelin", true);
            Config["Player Can Win WoodLedge"] = CanWinWoodLedge = GetConfig("Player Can Win WoodLedge", true);
            Config["Player Can Win WoodMace"] = CanWinWoodMace = GetConfig("Player Can Win	WoodMace", true);
            Config["Player Can Win WoodPictureFrame"] = CanWinWoodPictureFrame = GetConfig("Player Can Win	WoodPictureFrame", true);
            Config["Player Can Win WoodRamp"] = CanWinWoodRamp = GetConfig("Player Can Win	WoodRamp", true);
            Config["Player Can Win WoodSandals"] = CanWinWoodSandals = GetConfig("Player Can Win WoodSandals", true);
            Config["Player Can Win WoodShort Bow"] = CanWinWoodShortBow = GetConfig("Player Can Win WoodShort Bow", true);
            Config["Player Can Win WoodShutters"] = CanWinWoodShutters = GetConfig("Player Can Win WoodShutters", true);
            Config["Player Can Win WoodSkirt"] = CanWinWoodSkirt = GetConfig("Player Can Win WoodSkirt", true);
            Config["Player Can Win WoodSpear"] = CanWinWoodSpear = GetConfig("Player Can Win WoodSpear", true);
            Config["Player Can Win WoodSpikes"] = CanWinWoodSpikes = GetConfig("Player Can Win WoodSpikes", true);
            Config["Player Can Win WoodStairs"] = CanWinWoodStairs = GetConfig("Player Can Win WoodStairs", true);
            Config["Player Can Win WoodStick"] = CanWinWoodStick = GetConfig("Player Can Win WoodStick", true);
            Config["Player Can Win WoodSword"] = CanWinWoodSword = GetConfig("Player Can Win WoodSword", true);
            Config["Player Can Win WoodTotem"] = CanWinWoodTotem = GetConfig("Player Can Win WoodTotem", true);
            Config["Player Can Win WoodTower"] = CanWinWoodTower = GetConfig("Player Can Win WoodTower", true);
            Config["Player Can Win WoodVest"] = CanWinWoodVest = GetConfig("Player Can Win WoodVest", true);
            Config["Player Can Win Woodworking"] = CanWinWoodworking = GetConfig("Player Can Win Woodworking", true);
            Config["Player Can Win Wool"] = CanWinWool = GetConfig("Player Can Win Wool", true);
            Config["Player Can Win WorkBench"] = CanWinWorkBench = GetConfig("Player Can Win WorkBench", true);
            Config["Player Can Win Worms"] = CanWinWorms = GetConfig("Player Can Win Worms", true);
            Config["Player Can Win X1 Jackpot"] = CanWinJackpot = GetConfig("Can Win X1Jackpot", true);
            Config["Player Can Win X2 Steel Armor Set Jackpot"] = CanWinSteelArmorSetJackpot = GetConfig("Player Can Win X2 SteelArmorSetJackpot", true);
            Config["Player Can Win X3 Iron Armor Set Jackpot"] = CanWinIronArmorSetJackpot = GetConfig("Player Can Win X3 IronArmorSetJackpot", true);
            Config["Player Can Win X4 Leather Armor Set Jackpot"] = CanWinLeatherArmorSetJackpot = GetConfig("Player Can Win X4 LeatherArmorSetJackpot", true);
            Config["Player Can Win X5 Builder Set Jackpot"] = CanWinBuilderSetJackpot = GetConfig("Player Can Win X5 BuilderSetJackpot", true);
            Config["Player Can Win X6 Robbed"] = CanWinRobbed = GetConfig("Player Can Win X6 Robbed", true);
            Config["Player Can Win X7 Gang Attack"] = CanWinGangAttack = GetConfig("Player Can Win X7 Gang Attack", true);
            Config["Player Can Win X8 Gold 1K"] = CanWinGold1K = GetConfig("Player Can Win X8 Gold 1K", true);
            Config["Player Can Win X9 Gold 10K"] = CanWinGold10K = GetConfig("Player Can Win X9 Gold 10K", true);
            Config["Player Can Win X91 Gold 100K"] = CanWinGold100K = GetConfig("Player Can Win X91 Gold 100K", true);

            SaveConfig();
        }
        #endregion

        #region [COMMANDS]

        [ChatCommand("lottery")]
        void Lottery(Player player, string command, string[] args)
        {
            var inventory = player.GetInventory().Contents;
            if (inventory.FreeSlotCount < 5)
            {
                //You have less than 5 inventory slots free which would be required to win ArmorSets or the Jackpot, so we will cancel. Make room in your inventory and try again!
                PrintToChat(player, string.Format(GetMessage("27", player.Id.ToString()), "445"));
                return;
            }
            if (LotteryIronIngotUsedToPlay)
                if (!CanRemoveResource(player, "Iron Ingot", 4))
                {
                    //It looks like you don't have 4 Iron Ingots  to play!!
                    PrintToChat(player, string.Format(GetMessage("28", player.Id.ToString()), "445"));
                    return;
                }
            if (LotteryIronIngotUsedToPlay)
                RemoveItemsFromInventory(player, "Iron Ingot", 4);

            if (LotterySteelIngotUsedToPlay)
                if (!CanRemoveResource(player, "Steel Ingot", 1))
                {
                    //It looks like you don't have a Steel Ingot to play!!
                    PrintToChat(player, string.Format(GetMessage("29", player.Id.ToString()), "445"));
                    return;
                }
            if (LotterySteelIngotUsedToPlay)
                RemoveItemsFromInventory(player, "Steel Ingot", 1);


            if (LotteryBatUsedToPlay)
                if (!CanRemoveResource(player, "Bat", 1))
                {
                    //It looks like you don't have a Bat to play!!
                    PrintToChat(player, string.Format(GetMessage("31", player.Id.ToString()), "445"));
                    return;
                }
            if (LotteryBatUsedToPlay)
                RemoveItemsFromInventory(player, "Bat", 1);

            player.ShowPopup("[00FFFF]Kings Lottery", "[00FF00]Play Ticket", "[00FF00]Play", (selection, dialogue, data) => RandomLottery(player));//ClosePopup(player, selection, dialogue, data));
            //player.ShowConfirmPopup("Kings Lottery", "Play Ticket", "Play", "Cancel", (selection, dialogue, data) => RandomLottery(player));
            timer.Once(16000f, () =>

            {
                Puts("RandomLottery(player);");
            });
        }

        [ChatCommand("lotteryhelp")]
        void SendPlayerHelpText(Player player, string command, string[] args)
        {
            PrintToChat(player, GetMessage("51", player.Id.ToString()));
            PrintToChat(player, GetMessage("52", player.Id.ToString()));
            PrintToChat(player, GetMessage("51", player.Id.ToString()));
            PrintToChat(player, GetMessage("1", player.Id.ToString()));
            PrintToChat(player, GetMessage("32", player.Id.ToString()));
            PrintToChat(player, GetMessage("33", player.Id.ToString()));
            PrintToChat(player, GetMessage("34", player.Id.ToString()));
            PrintToChat(player, GetMessage("35", player.Id.ToString()));
            PrintToChat(player, GetMessage("51", player.Id.ToString()));
            PrintToChat(player, GetMessage("51", player.Id.ToString()));
        }

        [ChatCommand("ticket")]
        void SendPlayerTicketInfo(Player player, string command, string[] args)
        {
            PrintToChat(player, GetMessage("53", player.Id.ToString()));
            PrintToChat(player, GetMessage("51", player.Id.ToString()));
            PrintToChat(player, GetMessage("36", player.Id.ToString()));
            if (LotteryIronIngotUsedToPlay)
                PrintToChat(player, GetMessage("37", player.Id.ToString()));
            if (LotterySteelIngotUsedToPlay)
                PrintToChat(player, GetMessage("38", player.Id.ToString()));
            if (LotteryBatUsedToPlay)
                PrintToChat(player, GetMessage("39", player.Id.ToString()));
                PrintToChat(player, GetMessage("51", player.Id.ToString()));
        }

        [ChatCommand("jackpots")]
        void SendPlayerJackPotsInfo(Player player, string command, string[] args)
        {
            PrintToChat(player, GetMessage("51", player.Id.ToString()));
            PrintToChat(player, GetMessage("54", player.Id.ToString()));
            PrintToChat(player, GetMessage("51", player.Id.ToString()));
            if (CanWinJackpot)
                PrintToChat(player, GetMessage("40", player.Id.ToString()));
            if (CanWinSteelArmorSetJackpot)
                PrintToChat(player, GetMessage("41", player.Id.ToString()));
            if (CanWinIronArmorSetJackpot)
                PrintToChat(player, GetMessage("42", player.Id.ToString()));
            if (CanWinLeatherArmorSetJackpot)
                PrintToChat(player, GetMessage("43", player.Id.ToString()));
            if (CanWinBuilderSetJackpot)
                PrintToChat(player, GetMessage("44", player.Id.ToString()));
            if (CanWinRobbed)
                PrintToChat(player, GetMessage("45", player.Id.ToString()));
            if (CanWinGangAttack)
                PrintToChat(player, GetMessage("46", player.Id.ToString()));
            if (CanWinGold1K)
                PrintToChat(player, GetMessage("47", player.Id.ToString()));
            if (CanWinGold10K)
                PrintToChat(player, GetMessage("48", player.Id.ToString()));
            if (CanWinGold100K)
                PrintToChat(player, GetMessage("49", player.Id.ToString()));
            
                PrintToChat(player, GetMessage("50", player.Id.ToString()));
                PrintToChat(player, GetMessage("51", player.Id.ToString()));
                PrintToChat(player, GetMessage("51", player.Id.ToString()));
        }

        #endregion

        #region Functions

        private void GiveItem(Player player, string itemName, int amount)
        {
            var inventory = player.GetInventory().Contents;
            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName(itemName, true, true);
            var invGameItemStack = new InvGameItemStack(blueprintForName, amount, null);
            ItemCollection.AutoMergeAdd(inventory, invGameItemStack);
        }

        void Bite(Player player)
        {
            CodeHatch.Damaging.Damage newDmg = new CodeHatch.Damaging.Damage();
            newDmg.Amount = player.GetHealth().CurrentHealth / 100 * 25;
            newDmg.Damager = player.Entity;
            newDmg.DamageSource = player.Entity;
            newDmg.DamageTypes = CodeHatch.Damaging.DamageType.Falling;
            Vector3 newForce = new Vector3(0, 0, 0);
            newDmg.Force = newForce;
            newDmg.ImpactDamage = (0);
            newDmg.IsFatal = false;
            newDmg.MiscDamage = newDmg.Amount;
            EntityDamageEvent bite = new EntityDamageEvent(player.Entity, newDmg);
            bite.Damage.DamageTypes = CodeHatch.Damaging.DamageType.Any;
            EventManager.CallEvent((BaseEvent)bite);
        }

        public void RemoveItemsFromInventory(Player player, string resource, int amount)
        {
            ItemCollection inventory = player.GetInventory().Contents;

            int removeAmount = 0;
            int amountRemaining = amount;

            foreach (InvGameItemStack item in inventory.Where(item => item != null))
            {
                if (item.Name != resource) continue;

                removeAmount = amountRemaining;
                if (item.StackAmount < removeAmount) removeAmount = item.StackAmount;
                inventory.SplitItem(item, removeAmount);
                amountRemaining = amountRemaining - removeAmount;
            }
        }
        private bool CanRemoveResource(Player player, string resource, int amount)
        {
            // Check player's inventory
            var inventory = player.CurrentCharacter.Entity.GetContainerOfType(CollectionTypes.Inventory);

            // Check how much the player has
            var foundAmount = 0;
            foreach (var item in inventory.Contents.Where(item => item != null))
            {
                if (item.Name == resource)
                {
                    foundAmount = foundAmount + item.StackAmount;
                }
            }

            if (foundAmount >= amount) return true;
            return false;
        }
        private void ClosePopup(Player player, Options selection, Dialogue dialogue, object contextData)
        {
            //Do nothing
        }

        void OnUserConnected(Player player)
        {
            if (player == null) return;
        }
        #endregion
        #region Hooks
        static System.Random random = new System.Random();
        void RandomLottery(Player player)
        {
            int rng;
            while (true)
            {
                rng = random.Next(449);
                switch (rng)
                {
                    case 0:

                        if (!CanWinAdvancedFletcher) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Advanced Fletcher", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Advanced Fletcher", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 1:

                        if (!CanWinAfricanMask) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("African Mask", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "African Mask", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 2:

                        if (!CanWinAmberjackFish) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Amberjack Fish", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 5;
                            GiveItem(player, "Amberjack Fish", 1);
                            GiveItem(player, "Amberjack Fish", 1);
                            GiveItem(player, "Amberjack Fish", 1);
                            GiveItem(player, "Amberjack Fish", 1);
                            GiveItem(player, "Amberjack Fish", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));

                        }
                        return;
                    case 3:

                        if (!CanWinAncientCrown) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Ancient Crown", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Ancient Crown", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 4:

                        if (!CanWinAncientSword) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Ancient Sword", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Ancient Sword", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 5:

                        if (!CanWinAnvil) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Anvil", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Anvil", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 6:

                        if (!CanWinApple) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Apple", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 25;
                            GiveItem(player, "Apple", 25);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;

                    case 7:

                        if (!CanWinAppleSeed) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Apple Seed", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 10;
                            GiveItem(player, "Apple Seed", 10);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 8:

                        if (!CanWinArcheryTarget) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Archery Target", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 3;
                            GiveItem(player, "Archery Target", 3);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 9:

                        if (!CanWinAsianMask) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Asian Mask", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Asian Mask", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 10:

                        if (!CanWinAsianTribalMask) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Asian Tribal Mask", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Asian Tribal Mask", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 11:

                        if (!CanWinBabyChicken) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Baby Chicken", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 2;
                            GiveItem(player, "Baby Chicken", 2);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 12:

                        if (!CanWinBakedClay) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Baked Clay", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 10;
                            GiveItem(player, "Baked Clay", 10);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 13:

                        if (!CanWinBallista) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Ballista", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Ballista", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 14:

                        if (!CanWinBallistaBolt) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Ballista Bolt", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 20;
                            GiveItem(player, "Ballista Bolt", 20);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 15:

                        if (!CanWinBandage) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Bandage", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 10;
                            GiveItem(player, "Bandage", 10);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 16:

                        if (!CanWinBanquetTable) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Banquet Table", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Banquet Table", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 17:

                        if (!CanWinBascinetHelmet) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Bascinet Helmet", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Bascinet Helmet", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 18:

                        if (!CanWinBascinetPointedHelmet) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Bascinet Pointed Helmet", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Bascinet Pointed Helmet", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 19:

                        if (!CanWinBassFish) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Bass Fish", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 5;
                            GiveItem(player, "Bass Fish", 1);
                            GiveItem(player, "Bass Fish", 1);
                            GiveItem(player, "Bass Fish", 1);
                            GiveItem(player, "Bass Fish", 1);
                            GiveItem(player, "Bass Fish", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 20:

                        if (!CanWinBat) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Bat", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 3;
                            GiveItem(player, "Bat", 3);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 21:

                        if (!CanWinBatWing) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Bat Wing", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 10;
                            GiveItem(player, "Bat Wing", 10);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 22:

                        if (!CanWinBeanSeed) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Bean Seed", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 25;
                            GiveItem(player, "Bean Seed", 25);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 23:

                        if (!CanWinBearHide) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Bear Hide", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Bear Hide", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 24:

                        if (!CanWinBearSkinRug) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Bear Skin Rug", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Bear Skin Rug", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 25:

                        if (!CanWinBeet) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Beet", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 25;
                            GiveItem(player, "Beet", 25);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 26:

                        if (!CanWinBeetSeed) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Beet Seed", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 10;
                            GiveItem(player, "Beet Seed", 10);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 27:

                        if (!CanWinBellGong) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Bell Gong", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Bell Gong", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 28:

                        if (!CanWinBellows) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Bellows", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Bellows", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 29:

                        if (!CanWinBentHorn) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Bent Horn", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Bent Horn", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 30:

                        if (!CanWinBerries) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Berries", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 25;
                            GiveItem(player, "Berries", 25);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 31:

                        if (!CanWinBerrySeed) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Berry Seed", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 10;
                            GiveItem(player, "Berry Seed", 10);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 32:

                        if (!CanWinBladedPillar) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Bladed Pillar", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Bladed Pillar", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 33:

                        if (!CanWinBlood) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Blood", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 10;
                            GiveItem(player, "Blood", 10);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 34:

                        if (!CanWinBone) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Bone", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 10;
                            GiveItem(player, "Bone", 10);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 35:

                        if (!CanWinBoneAxe) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Bone Axe", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Bone Axe", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 36:

                        if (!CanWinBoneDagger) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Bone Dagger", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Bone Dagger", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 37:

                        if (!CanWinBoneHorn) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Bone Horn", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Bone Horn", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 38:

                        if (!CanWinBoneLongbow) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Bone Longbow", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Bone Longbow", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 39:

                        if (!CanWinBoneSpikedClub) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Bone Spiked Club", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Bone Spiked Club", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 40:

                        if (!CanWinBread) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Bread", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 10;
                            GiveItem(player, "Bread", 10);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 41:

                        if (!CanWinBugNet) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Bug Net", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Bug Net", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 42:

                        if (!CanWinBurntBird) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Burnt Bird", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 10;
                            GiveItem(player, "Burnt Bird", 10);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 43:

                        if (!CanWinBurntMeat) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Burnt Meat", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 10;
                            GiveItem(player, "Burnt Meat", 10);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 44:

                        if (!CanWinButterfly) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Butterfly", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 10;
                            GiveItem(player, "Butterfly", 10);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 45:

                        if (!CanWinCabbage) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Cabbage", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 25;
                            GiveItem(player, "Cabbage", 25);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 46:

                        if (!CanWinCabbageSeed) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Cabbage Seed", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 10;
                            GiveItem(player, "Cabbage Seed", 10);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 47:

                        if (!CanWinCampfire) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Campfire", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Campfire", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 48:

                        if (!CanWinCandle) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Candle", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Candle", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 49:

                        if (!CanWinCandleStand) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Candle Stand", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Candle Stand", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 50:

                        if (!CanWinCarrot) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Carrot", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 25;
                            GiveItem(player, "Carrot", 25);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 51:

                        if (!CanWinCarrotSeed) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Carrot Seed", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 10;
                            GiveItem(player, "Carrot Seed", 10);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 52:

                        if (!CanWinCatMask) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Cat Mask", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Cat Mask", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 53:

                        if (!CanWinChandelier) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Chandelier", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Chandelier", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 54:

                        if (!CanWinChapelDeFerHelmet) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Chapel De Fer Helmet", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Chapel De Fer Helmet", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 55:

                        if (!CanWinChapelDeFerRoundedHelmet) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Chapel De Fer Rounded Helmet", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Chapel De Fer Rounded Helmet", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 56:

                        if (!CanWinCharcoal) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Charcoal", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 100;
                            GiveItem(player, "Charcoal", 100);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 57:

                        if (!CanWinChicken) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Chicken", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 2;
                            GiveItem(player, "Chicken", 2);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 58:

                        if (!CanWinClay) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Clay", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 100;
                            GiveItem(player, "Clay", 100);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 59:

                        if (!CanWinClayBlock) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Clay Block", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 50;
                            GiveItem(player, "Clay Block", 50);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 60:

                        if (!CanWinClayCorner) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Clay Corner", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 20;
                            GiveItem(player, "Clay Corner", 20);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 61:

                        if (!CanWinClayInvertedCorner) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Clay Inverted Cornerr", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 10;
                            GiveItem(player, "Clay Inverted Corner", 10);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 62:

                        if (!CanWinClayRamp) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Clay Ramp", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 20;
                            GiveItem(player, "Clay Ramp", 20);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 63:

                        if (!CanWinClayStairs) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Clay Stairs", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 20;
                            GiveItem(player, "Clay Stairs", 20);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 64:

                        if (!CanWinCobblestoneBlock) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Cobblestone Block", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 50;
                            GiveItem(player, "Cobblestone Block", 50);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 65:

                        if (!CanWinCobblestoneCorner) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Cobblestone Corner", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 20;
                            GiveItem(player, "Cobblestone Corner", 20);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 66:

                        if (!CanWinCobblestoneInvertedCorner) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Cobblestone Inverted Corner", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 10;
                            GiveItem(player, "Cobblestone Inverted Corner", 10);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 67:

                        if (!CanWinCobblestoneRamp) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Cobblestone Ramp", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 20;
                            GiveItem(player, "Cobblestone Ramp", 20);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 68:

                        if (!CanWinCobblestoneStairs) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Cobblestone Stairs", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 20;
                            GiveItem(player, "Cobblestone Stairs", 20);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 69:

                        if (!CanWinCookedBeans) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Cooked Beans", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 50;
                            GiveItem(player, "Cooked Beans", 25);
                            GiveItem(player, "Cooked Beans", 25);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 70:

                        if (!CanWinCookedBird) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Cooked Bird", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 25;
                            GiveItem(player, "Cooked Bird", 25);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 71:

                        if (!CanWinCookedMeat) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Cooked Meat", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 25;
                            GiveItem(player, "Cooked Meat", 25);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 72:

                        if (!CanWinCrab) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Crab", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Crab", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 73:

                        if (!CanWinCrossbow) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Crossbow", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Crossbow", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 74:

                        if (!CanWinCrow) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Crow", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Crow", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 75:

                        if (!CanWinDeer) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Deer", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 2;
                            GiveItem(player, "Deer", 2);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 76:

                        if (!CanWinDeerHeadTrophy) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Deer Head Trophy", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Deer Head Trophy", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 77:

                        if (!CanWinDeerLegClub) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Deer Leg Club", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Deer Leg Club", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 78:

                        if (!CanWinDefensiveBarricade) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Defensive Barricade", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 4;
                            GiveItem(player, "Defensive Barricade", 4);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 79:

                        if (!CanWinDiamond) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Diamond", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 2;
                            GiveItem(player, "Diamond", 2);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 80:

                        if (!CanWinDirt) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Dirt", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 100;
                            GiveItem(player, "Dirt", 100);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 81:

                        if (!CanWinDjembeDrum) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Djembe Drum", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Djembe Drum", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 82:

                        if (!CanWinDriftwoodClub) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Driftwood Club", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Driftwood Club", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 83:

                        if (!CanWinDuck) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Duck", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 2;
                            GiveItem(player, "Duck", 2);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 84:

                        if (!CanWinDuckFeet) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Duck Feet", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 10;
                            GiveItem(player, "Duck Feet", 10);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 85:

                        if (!CanWinExecutionersAxe) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Executioners Axe", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Executioners Axe", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 86:

                        if (!CanWinExplosiveKeg) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Explosive Keg", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 5;
                            GiveItem(player, "Explosive Keg", 5);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 87:

                        if (!CanWinFang) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Fang", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 10;
                            GiveItem(player, "Fang", 10);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 88:

                        if (!CanWinFat) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Fat", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 10;
                            GiveItem(player, "Fat", 10);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 89:

                        if (!CanWinFeather) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Feather", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 100;
                            GiveItem(player, "Feather", 100);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 90:

                        if (!CanWinFern) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Fern", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 10;
                            GiveItem(player, "Fern", 10);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 91:

                        if (!CanWinFernBracers) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Fern Bracers", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Fern Bracers", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 92:

                        if (!CanWinFernHelmet) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Fern Helmet", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Fern Helmet", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 93:

                        if (!CanWinFernSandals) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Fern Sandals", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Fern Sandals", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 94:

                        if (!CanWinFernSkirt) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Fern Skirt", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Fern Skirt", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 95:

                        if (!CanWinFernVest) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Fern Vest", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Fern Vest", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 96:

                        if (!CanWinFireFly) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Fire Fly", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 10;
                            GiveItem(player, "Fire Fly", 10);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 97:

                        if (!CanWinFireWater) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Fire Water", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 10;
                            GiveItem(player, "Fire Water", 10);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 98:

                        if (!CanWinFirepit) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Firepit", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Firepit", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 99:

                        if (!CanWinFishingRod) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Fishing Rod", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Fishing Rod", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 100:

                        if (!CanWinFlatTopHelmet) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Flat Top Helmet", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Flat Top Helmet", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 101:

                        if (!CanWinFlax) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Flax", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 100;
                            GiveItem(player, "Flax", 100);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 102:

                        if (!CanWinFletcher) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Fletcher", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Fletcher", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 103:

                        if (!CanWinFlour) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Flour", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 20;
                            GiveItem(player, "Flour", 10);
                            GiveItem(player, "Flour", 10);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 104:

                        if (!CanWinFlowerBracers) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Flower Bracers", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Flower Bracers", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 105:

                        if (!CanWinFlowerHelmet) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Flower Helmet", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Flower Helmet", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 106:

                        if (!CanWinFlowerSandals) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Flower Sandals", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Flower Sandals", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 107:

                        if (!CanWinFlowerSkirt) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Flower Skirt", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Flower Skirt", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 108:

                        if (!CanWinFlowerVest) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Flower Vest", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Flower Vest", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 109:

                        if (!CanWinFlowers) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Flowers", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 10;
                            GiveItem(player, "Flowers", 10);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 110:

                        if (!CanWinFluffyBed) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Fluffy Bed", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Fluffy Bed", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 111:

                        if (!CanWinFly) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Fly", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 10;
                            GiveItem(player, "Fly", 10);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 112:

                        if (!CanWinForestSprite) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Forest Sprite", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Forest Sprite", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 113:

                        if (!CanWinFuse) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Fuse", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 10;
                            GiveItem(player, "Fuse", 10);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 114:

                        if (!CanWinGazebo) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Gazebo", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Gazebo", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 115:

                        if (!CanWinGrain) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Grain", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 100;
                            GiveItem(player, "Grain", 100);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 116:

                        if (!CanWinGrainSeed) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Grain Seed", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 100;
                            GiveItem(player, "Grain Seed", 100);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 117:

                        if (!CanWinGranary) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Granary", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Granary", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 118:

                        if (!CanWinGraveMask) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Grave Mask", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Grave Mask", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 119:

                        if (!CanWinGreatFireplace) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Great Fireplace", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Great Fireplace", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 120:

                        if (!CanWinGrizzlyBear) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Grizzly Bear", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Grizzly Bear", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 121:

                        if (!CanWinGroundTorch) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Ground Torch", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Ground Torch", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 122:

                        if (!CanWinGuillotine) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Guillotine", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Guillotine", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 123:

                        if (!CanWinHangingLantern) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Hanging Lantern", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 3;
                            GiveItem(player, "Hanging Lantern", 3);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 124:

                        if (!CanWinHangingTorch) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Hanging Torch", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 3;
                            GiveItem(player, "Hanging Torch", 3);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 125:

                        if (!CanWinHay) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Hay", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 200;
                            GiveItem(player, "Hay", 200);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 126:

                        if (!CanWinHayBaleTarget) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Hay Bale Target", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 3;
                            GiveItem(player, "Hay Bale Target", 3);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 127:

                        if (!CanWinHayBracers) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Hay Bracers", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Hay Bracers", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 128:

                        if (!CanWinHayHelmet) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Hay Helmet", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Hay Helmet", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 129:

                        if (!CanWinHaySandals) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Hay Sandals", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Hay Sandals", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 130:

                        if (!CanWinHaySkirt) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Hay Skirt", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Hay Skirt", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 131:

                        if (!CanWinHayVest) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Hay Vest", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Hay Vest", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 132:

                        if (!CanWinHeart) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Heart", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 10;
                            GiveItem(player, "Heart", 10);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 133:

                        if (!CanWinHighQualityBed) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("High Quality Bed", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "High Quality Bed", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 134:

                        if (!CanWinHighQualityBench) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("High Quality Bench", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "High Quality Bench", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 135:

                        if (!CanWinHighQualityCabinet) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("High Quality Cabinet", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "High Quality Cabinet", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 136:

                        if (!CanWinHoe) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Hoe", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Hoe", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 137:

                        if (!CanWinIron) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Iron", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 100;
                            GiveItem(player, "Iron", 100);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 138:

                        if (!CanWinIronAxe) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Iron Axe", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Iron Axe", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 139:

                        if (!CanWinIronBarWindow) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Iron Bar Window", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 12;
                            GiveItem(player, "Iron Bar Window", 12);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 140:

                        if (!CanWinIronBattleAxe) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Iron Battle Axe", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Iron Battle Axe", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 141:

                        if (!CanWinIronBattleHammer) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Iron Battle Hammer", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Iron Battle Hammer", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 142:

                        if (!CanWinIronBearTrap) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Iron Bear Trap", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Iron Bear Trap", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 143:

                        if (!CanWinIronBuckler) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Iron Buckler", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Iron Buckler", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 144:

                        if (!CanWinIronChest) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Iron Chest", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Iron Chest", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 145:

                        if (!CanWinIronCrest) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Iron Crest", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Iron Crest", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 146:

                        if (!CanWinIronDagger) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Iron Dagger", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Iron Dagger", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 147:

                        if (!CanWinIronDoor) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Iron Door", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 2;
                            GiveItem(player, "Iron Door", 2);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 148:

                        if (!CanWinIronFlangedMace) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Iron Flanged Mace", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Iron Flanged Mace", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 149:

                        if (!CanWinIronFloorTorch) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Iron Floor Torch", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 2;
                            GiveItem(player, "Iron Floor Torch", 2);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 150:

                        if (!CanWinIronForkedSpear) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Iron Forked Spear", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Iron Forked Spear", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 151:

                        if (!CanWinIronGate) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Iron Gate", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Iron Gate", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 152:

                        if (!CanWinIronHalberd) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Iron Halberd", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Iron Halberd", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 153:

                        if (!CanWinIronHatchet) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Iron Hatchet", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Iron Hatchet", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 154:

                        if (!CanWinIronHeater) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Iron Heater", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Iron Heater", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 155:

                        if (!CanWinIronIngot) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Iron Ingot", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 10;
                            GiveItem(player, "Iron Ingot", 10);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 156:

                        if (!CanWinIronJavelin) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Iron Javelin", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 20;
                            GiveItem(player, "Iron Javelin", 20);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 157:

                        if (!CanWinIronMorningStarMace) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Iron Morning Star Mace", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Iron Morning Star Mace", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 158:

                        if (!CanWinIronPickaxe) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Iron Pickaxe", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Iron Pickaxe", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 159:

                        if (!CanWinIronPlateBoots) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Iron Plate Boots", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Iron Plate Boots", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 160:

                        if (!CanWinIronPlateGauntlets) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Iron Plate Gauntlets", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Iron Plate Gauntlets", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 161:

                        if (!CanWinIronPlateHelmet) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Iron Plate Helmet", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Iron Plate Helmet", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 162:

                        if (!CanWinIronPlatePants) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Iron Plate Pants", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Iron Plate Pants", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 163:

                        if (!CanWinIronPlateVest) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Iron Plate Vest", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Iron Plate Vest", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 164:

                        if (!CanWinIronShackles) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Iron Shackles", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Iron Shackles", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 165:

                        if (!CanWinIronSpear) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Iron Spear", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Iron Spear", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 166:

                        if (!CanWinIronSpikes) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Iron Spikes", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Iron Spikes", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 167:

                        if (!CanWinIronSpikesHidden) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Iron Spikes (Hidden)", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Iron Spikes (Hidden)", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 168:

                        if (!CanWinIronStarMace) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Iron Star Mace", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Iron Star Mace", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 169:

                        if (!CanWinIronSword) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Iron Sword", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Iron Sword", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 170:

                        if (!CanWinIronThrowingAxe) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Iron Throwing Axe", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 20;
                            GiveItem(player, "Iron Throwing Axe", 20);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 171:

                        if (!CanWinIronThrowingBattleAxe) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Iron Throwing Battle Axe", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 20;
                            GiveItem(player, "Iron Throwing Battle Axe", 20);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 172:

                        if (!CanWinIronThrowingKnife) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Iron Throwing Knife", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 20;
                            GiveItem(player, "Iron Throwing Knife", 20);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 173:

                        if (!CanWinIronTippedArrow) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Iron Tipped Arrow", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 25;
                            GiveItem(player, "Iron Tipped Arrow", 25);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 174:

                        if (!CanWinIronTotem) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Iron Totem", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Iron Totem", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 175:

                        if (!CanWinIronTower) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Iron Tower", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Iron Tower", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 176:

                        if (!CanWinIronWarHammer) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Iron War Hammer", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Iron War Hammer", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 177:

                        if (!CanWinIronWoodCuttersAxe) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Iron Wood Cutters Axe", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Iron Wood Cutters Axe", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 178:

                        if (!CanWinJapaneseDemon) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Japanese Demon", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Japanese Demon", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 179:

                        if (!CanWinJapaneseMask) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Japanese Mask", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Japanese Mask", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 180:

                        if (!CanWinJesterHatGreenPink) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Jester Hat (Green & Pink)", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Jester Hat (Green & Pink)", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 181:

                        if (!CanWinJesterHatOrangeBlack) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Jester Hat (Orange & Black)", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Jester Hat (Orange & Black)", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 182:

                        if (!CanWinJesterHatRainbow) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Jester Hat (Rainbow)", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Jester Hat (Rainbow)", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 183:

                        if (!CanWinJesterHatRed) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Jester Hat (Red)", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Jester Hat (Red)", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 184:

                        if (!CanWinJesterMaskGoldBlue) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Jester Mask (Gold & Blue)", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Jester Mask (Gold & Blue)", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 185:

                        if (!CanWinJesterMaskGoldRed) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Jester Mask (Gold & Red)", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Jester Mask (Gold & Red)", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 186:

                        if (!CanWinJesterMaskWhiteBlue) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Jester Mask (White & Blue)", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Jester Mask (White & Blue)", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 187:

                        if (!CanWinJesterMaskWhiteGold) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Jester Mask (White & Gold)", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Jester Mask (White & Gold)", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 188:

                        if (!CanWinKettleBoardHelmet) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Kettle Board Helmet", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Kettle Board Helmet", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 189:

                        if (!CanWinKettleHat) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Kettle Hat", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Kettle Hat", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 190:

                        if (!CanWinKoiFish) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Koi Fish", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 5;
                            GiveItem(player, "Koi Fish", 1);
                            GiveItem(player, "Koi Fish", 1);
                            GiveItem(player, "Koi Fish", 1);
                            GiveItem(player, "Koi Fish", 1);
                            GiveItem(player, "Koi Fish", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 191:

                        if (!CanWinLargeGallows) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Large Gallows", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Large Gallows", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 192:

                        if (!CanWinLargeIronCage) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Large Iron Cage", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Large Iron Cage", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 193:

                        if (!CanWinLargeIronHangingCage) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Large Iron Hanging Cage", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Large Iron Hanging Cage", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 194:

                        if (!CanWinLargeWoodBillboard) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Large Wood Billboard", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 2;
                            GiveItem(player, "Large Wood Billboard", 2);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 195:

                        if (!CanWinLeatherCrest) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Leather Crest", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Leather Crest", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 196:

                        if (!CanWinLeatherHide) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Leather Hide", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 20;
                            GiveItem(player, "Leather Hide", 20);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 197:

                        if (!CanWinLightLeatherBoots) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Light Leather Boots", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Light Leather Boots", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 198:

                        if (!CanWinLightLeatherBracers) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Light Leather Bracers", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Light Leather Bracers", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 199:

                        if (!CanWinLightLeatherHelmet) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Light Leather Helmet", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Light Leather Helmet", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 200:

                        if (!CanWinLightLeatherPants) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Light Leather Pants", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Light Leather Pants", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 201:

                        if (!CanWinLightLeatherVest) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Light Leather Vest", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Light Leather Vest", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 202:

                        if (!CanWinLiver) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Liver", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 10;
                            GiveItem(player, "Liver", 10);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 203:

                        if (!CanWinLockPick) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Lock Pick", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 3;
                            GiveItem(player, "Lock Pick", 3);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 204:

                        if (!CanWinLogBlock) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Log Block", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 50;
                            GiveItem(player, "Log Block", 50);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 205:

                        if (!CanWinLogCorner) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Log Corner", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 10;
                            GiveItem(player, "Log Corner", 10);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 206:

                        if (!CanWinLogFence) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Log Fence", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 10;
                            GiveItem(player, "Log Fence", 10);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 207:

                        if (!CanWinLogInvertedCorner) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Log Inverted Corner", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 10;
                            GiveItem(player, "Log Inverted Corner", 10);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 208:

                        if (!CanWinLogRamp) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Log Ramp", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 20;
                            GiveItem(player, "Log Ramp", 20);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 209:

                        if (!CanWinLogStairs) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Log Stairs", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 20;
                            GiveItem(player, "Log Stairs", 20);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 210:

                        if (!CanWinLongHorn) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Long Horn", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Long Horn", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 211:

                        if (!CanWinLongWoodDrawbridge) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Long Wood Drawbridge", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Long Wood Drawbridge", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 212:

                        if (!CanWinLootSack) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Loot Sack", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Loot Sack", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 213:

                        if (!CanWinLordsBath) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Lord's Bath", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Lord's Bath", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 214:

                        if (!CanWinLordsBed) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Lord's Bed", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Lord's Bed", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 215:

                        if (!CanWinLordsLargeChair) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Lord's Large Chair", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Lord's Large Chair", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 216:

                        if (!CanWinLordsSmallChair) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Lord's Small Chair", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Lord's Small Chair", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 217:

                        if (!CanWinLowQualityBed) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Low Quality Bed", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Low Quality Bed", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 218:

                        if (!CanWinLowQualityBench) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Low Quality Bench", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Low Quality Bench", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 219:

                        if (!CanWinLowQualityChair) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Low Quality Chair", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 2;
                            GiveItem(player, "Low Quality Chair", 2);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 220:

                        if (!CanWinLowQualityFence) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Low Quality Fence", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 12;
                            GiveItem(player, "Low Quality Fence", 12);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 221:

                        if (!CanWinLowQualityShelf) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Low Quality Shelf", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Low Quality Shelf", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 222:

                        if (!CanWinLowQualityStool) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Low Quality Stool", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 2;
                            GiveItem(player, "Low Quality Stool", 2);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 223:

                        if (!CanWinLowQualityTable) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Low Quality Table", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Low Quality Table", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 224:

                        if (!CanWinLumber) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Lumber", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 100;
                            GiveItem(player, "Lumber", 100);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 225:

                        if (!CanWinMeat) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Meat", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 20;
                            GiveItem(player, "Meat", 20);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 226:

                        if (!CanWinMediumBanner) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Medium Banner", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 2;
                            GiveItem(player, "Medium Banner", 2);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 227:

                        if (!CanWinMediumQualityBed) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Medium Quality Bed", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Medium Quality Bed", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 228:

                        if (!CanWinMediumQualityBench) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Medium Quality Bench", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Medium Quality Bench", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 229:

                        if (!CanWinMediumQualityBookcase) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Medium Quality Bookcase", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Medium Quality Bookcase", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 230:

                        if (!CanWinMediumQualityChair) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Medium Quality Chair", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 2;
                            GiveItem(player, "Medium Quality Chair", 2);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 231:

                        if (!CanWinMediumQualityDresser) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Medium Quality Dresser", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Medium Quality Dresser", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 232:

                        if (!CanWinMediumQualityStool) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Medium Quality Stool", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 2;
                            GiveItem(player, "Medium Quality Stool", 2);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 233:

                        if (!CanWinMediumQualityTable) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Medium Quality Table", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Medium Quality Table", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 234:

                        if (!CanWinMediumSteelHangingSign) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Medium Steel Hanging Sign", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 2;
                            GiveItem(player, "Medium Steel Hanging Sign", 2);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 235:

                        if (!CanWinMediumStickBillboard) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Medium Stick Billboard", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 2;
                            GiveItem(player, "Medium Stick Billboard", 2);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 236:

                        if (!CanWinMediumWoodBillboard) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Medium Wood Billboard", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 2;
                            GiveItem(player, "Medium Wood Billboard", 2);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 237:

                        if (!CanWinMoose) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Moose", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Moose", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 238:

                        if (!CanWinNasalHelmet) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Nasal Helmet", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Nasal Helmet", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 239:

                        if (!CanWinOil) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Oil", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 100;
                            GiveItem(player, "Oil", 100);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 240:

                        if (!CanWinOnion) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Onion", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 25;
                            GiveItem(player, "Onion", 25);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 241:

                        if (!CanWinOnionSeed) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Onion Seed", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 10;
                            GiveItem(player, "Onion Seed", 10);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 242:

                        if (!CanWinPigeon) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Pigeon", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Pigeon", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 243:

                        if (!CanWinPillory) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Pillory", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Pillory", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 244:

                        if (!CanWinPineCone) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Pine Cone", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 10;
                            GiveItem(player, "Pine Cone", 10);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 245:

                        if (!CanWinPlagueDoctorMask) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Plague Doctor Mask", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Plague Doctor Mask", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 246:

                        if (!CanWinPlagueVillager) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Plague Villager", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Plague Villager", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 247:

                        if (!CanWinPlayerSleeper) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Player Sleeper", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Player Sleeper", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 248:

                        if (!CanWinPoplarSeed) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Poplar Seed", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 10;
                            GiveItem(player, "Poplar Seed", 10);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 249:

                        if (!CanWinPotionOfAntidote) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Potion Of Antidote", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 2;
                            GiveItem(player, "Potion Of Antidote", 2);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 250:

                        if (!CanWinPotionOfAppearance) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Potion Of Appearance", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 2;
                            GiveItem(player, "Potion Of Appearance", 2);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 251:

                        if (!CanWinRabbit) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Rabbit", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 2;
                            GiveItem(player, "Rabbit", 2);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 252:

                        if (!CanWinRabbitPelt) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Rabbit Pelt", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 2;
                            GiveItem(player, "Rabbit Pelt", 2);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 253:

                        if (!CanWinRavensFlour) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Flour", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 50;
                            GiveItem(player, "Flour", 10);
                            GiveItem(player, "Flour", 10);
                            GiveItem(player, "Flour", 10);
                            GiveItem(player, "Flour", 10);
                            GiveItem(player, "Flour", 10);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 254:

                        if (!CanWinRavensWater) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Water", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1000;
                            GiveItem(player, "Water", 1000);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 255:

                        if (!CanWinRavensClay) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Clay", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1000;
                            GiveItem(player, "Clay", 1000);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 256:

                        if (!CanWinRavensIron) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Iron", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1000;
                            GiveItem(player, "Iron", 1000);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 257:

                        if (!CanWinRavensOil) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Oil", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1000;
                            GiveItem(player, "Oil", 1000);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 258:

                        if (!CanWinRavensStone) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Stone", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1000;
                            GiveItem(player, "Stone", 1000);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 259:

                        if (!CanWinRavensWood) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Wood", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1000;
                            GiveItem(player, "Wood", 1000);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 260:

                        if (!CanWinRawBird) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Raw Bird", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 10;
                            GiveItem(player, "Raw Bird", 10);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 261:

                        if (!CanWinReinforcedWoodIronBlock) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Reinforced Wood (Iron) Block", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 50;
                            GiveItem(player, "Reinforced Wood (Iron) Block", 50);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 262:

                        if (!CanWinReinforcedWoodIronCorner) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Reinforced Wood (Iron) Corner", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 10;
                            GiveItem(player, "Reinforced Wood (Iron) Corner", 10);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 263:

                        if (!CanWinReinforcedWoodIronDoor) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Reinforced Wood (Iron) Door", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 2;
                            GiveItem(player, "Reinforced Wood (Iron) Door", 2);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 264:

                        if (!CanWinReinforcedWoodIronGate) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Reinforced Wood (Iron) Gate", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Reinforced Wood (Iron) Gate", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 265:

                        if (!CanWinReinforcedWoodIronInvertedCorner) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Reinforced Wood (Iron) Inverted Corner", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 10;
                            GiveItem(player, "Reinforced Wood (Iron) Inverted Corner", 10);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 266:

                        if (!CanWinReinforcedWoodIronRamp) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Reinforced Wood (Iron) Ramp", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 20;
                            GiveItem(player, "Reinforced Wood (Iron) Ramp", 20);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 267:

                        if (!CanWinReinforcedWoodIronStairs) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Reinforced Wood (Iron) Stairs", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 20;
                            GiveItem(player, "Reinforced Wood (Iron) Stairs", 20);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 268:

                        if (!CanWinReinforcedWoodIronTrapDoor) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Reinforced Wood (Iron) Trap Door", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 2;
                            GiveItem(player, "Reinforced Wood (Iron) Trap Door", 2);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 269:

                        if (!CanWinReinforcedWoodSteelDoor) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Reinforced Wood (Steel) Door", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 2;
                            GiveItem(player, "Reinforced Wood (Steel) Door", 2);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 270:

                        if (!CanWinRepairHammer) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Repair Hammer", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Repair Hammer", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 271:

                        if (!CanWinRockingHorse) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Rocking Horse", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Rocking Horse", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 272:

                        if (!CanWinRooster) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Rooster", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 2;
                            GiveItem(player, "Rooster", 2);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 273:

                        if (!CanWinRope) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Rope", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Rope", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 274:

                        if (!CanWinRoses) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Roses", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 10;
                            GiveItem(player, "Roses", 10);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 275:

                        if (!CanWinSack) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Sack", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Sack", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 276:

                        if (!CanWinSalmonFish) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Salmon Fish", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 5;
                            GiveItem(player, "Salmon Fish", 1);
                            GiveItem(player, "Salmon Fish", 1);
                            GiveItem(player, "Salmon Fish", 1);
                            GiveItem(player, "Salmon Fish", 1);
                            GiveItem(player, "Salmon Fish", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 277:

                        if (!CanWinSawmill) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Sawmill", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Sawmill", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 278:

                        if (!CanWinScythe) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Scythe", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Scythe", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 279:

                        if (!CanWinSeagull) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Seagull", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Seagull", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 280:

                        if (!CanWinShardanaMask) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Shardana Mask", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Shardana Mask", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 281:

                        if (!CanWinSharpRock) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Sharp Rock", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 5;
                            GiveItem(player, "Sharp Rock", 5);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 282:

                        if (!CanWinSheep) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Sheep", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 2;
                            GiveItem(player, "Sheep", 2);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 283:

                        if (!CanWinSiegeworks) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Siegeworks", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Siegeworks", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 284:

                        if (!CanWinSimpleHelmet) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Simple Helmet", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Simple Helmet", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 285:

                        if (!CanWinSmallBanner) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Small Banner", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Small Banner", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 286:

                        if (!CanWinSmallGallows) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Small Gallows", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Small Gallows", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 287:

                        if (!CanWinSmallIronCage) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Small Iron Cage", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Small Iron Cage", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 288:

                        if (!CanWinSmallIronHangingCage) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Small Iron Hanging Cage", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Small Iron Hanging Cage", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 289:

                        if (!CanWinSmallSteelHangingSign) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Small Steel Hanging Sign", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Small Steel Hanging Sign", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 290:

                        if (!CanWinSmallSteelSignpost) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Small Steel Signpost", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Small Steel Signpost", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 291:

                        if (!CanWinSmallStickSignpost) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Small Stick Signpost", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Small Stick Signpost", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 292:

                        if (!CanWinSmallWallLantern) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Small Wall Lantern", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 3;
                            GiveItem(player, "Small Wall Lantern", 3);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 293:

                        if (!CanWinSmallWallTorch) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Small Wall Torch", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 3;
                            GiveItem(player, "Small Wall Torch", 3);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 294:

                        if (!CanWinSmallWoodHangingSign) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Small Wood Hanging Sign", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 2;
                            GiveItem(player, "Small Wood Hanging Sign", 2);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 295:

                        if (!CanWinSmallWoodSignpost) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Small Wood Signpost", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 2;
                            GiveItem(player, "Small Wood Signpost", 2);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 296:

                        if (!CanWinSmelter) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Smelter", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 2;
                            GiveItem(player, "Smelter", 2);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 297:

                        if (!CanWinSmithy) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Smithy", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 2;
                            GiveItem(player, "Smithy", 2);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 298:

                        if (!CanWinSodBlock) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Sod Block", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 50;
                            GiveItem(player, "Sod Block", 50);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 299:

                        if (!CanWinSodCorner) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Sod Corner", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 10;
                            GiveItem(player, "Sod Corner", 10);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 300:

                        if (!CanWinSodInvertedCorner) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Sod Inverted Corner", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 10;
                            GiveItem(player, "Sod Inverted Corner", 10);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 301:

                        if (!CanWinSodRamp) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Sod Ramp", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 20;
                            GiveItem(player, "Sod Ramp", 20);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 302:

                        if (!CanWinSodStairs) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Sod Stairs", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 20;
                            GiveItem(player, "Sod Stairs", 20);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 303:

                        if (!CanWinSpinningWheel) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Spinning Wheel", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Spinning Wheel", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 304:

                        if (!CanWinSplinteredClub) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Splintered Club", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Splintered Club", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 305:

                        if (!CanWinSpruceBranchesBlock) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Spruce Branches Block", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 50;
                            GiveItem(player, "Spruce Branches Block", 50);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 306:

                        if (!CanWinSpruceBranchesCorner) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Spruce Branches Corner", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 10;
                            GiveItem(player, "Spruce Branches Corner", 10);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 307:

                        if (!CanWinSpruceBranchesInvertedCorner) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Spruce Branches Inverted Corner", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 10;
                            GiveItem(player, "Spruce Branches Inverted Corner", 10);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 308:

                        if (!CanWinSpruceBranchesRamp) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Spruce Branches Ramp", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 20;
                            GiveItem(player, "Spruce Branches Ramp", 20);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 309:

                        if (!CanWinSpruceBranchesStairs) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Spruce Branches Stairs", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 20;
                            GiveItem(player, "Spruce Branches Stairs", 20);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 310:

                        if (!CanWinStag) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Stag", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Stag", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 311:

                        if (!CanWinStandingIronTorch) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Standing Iron Torch", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Standing Iron Torch", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 312:

                        if (!CanWinSteelAxe) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Steel Axe", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Steel Axe", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 313:

                        if (!CanWinSteelBattleAxe) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Steel Battle Axe", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Steel Battle Axe", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 314:

                        if (!CanWinSteelBattleHammer) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Steel Battle Hammer", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Steel Battle Hammer", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 315:

                        if (!CanWinSteelBolt) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Steel Bolt", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 20;
                            GiveItem(player, "Steel Bolt", 20);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 316:

                        if (!CanWinSteelBuckler) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Steel Buckler", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Steel Buckler", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 317:

                        if (!CanWinSteelCage) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Steel Cage", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Steel Cage", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 318:

                        if (!CanWinSteelChest) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Steel Chest", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Steel Chest", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 319:

                        if (!CanWinSteelCompound) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Steel Compound", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 20;
                            GiveItem(player, "Steel Compound", 20);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 320:

                        if (!CanWinSteelCrest) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Steel Crest", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Steel Crest", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 321:

                        if (!CanWinSteelDagger) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Steel Dagger", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Steel Dagger", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 322:

                        if (!CanWinSteelFlangedMace) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Steel Flanged Mace", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Steel Flanged Mace", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 323:

                        if (!CanWinSteelGreatsword) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Steel Greatsword", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Steel Greatsword", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 324:

                        if (!CanWinSteelHalbred) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Steel Halbred", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Steel Halbred", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 325:

                        if (!CanWinSteelHatchet) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Steel Hatchet", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Steel Hatchet", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 326:

                        if (!CanWinSteelHeater) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Steel Heater", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Steel Heater", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 327:

                        if (!CanWinSteelIngot) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Steel Ingot", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 20;
                            GiveItem(player, "Steel Ingot", 20);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 328:

                        if (!CanWinSteelJavelin) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Steel Javelin", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 20;
                            GiveItem(player, "Steel Javelin", 20);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 329:

                        if (!CanWinSteelMorningStarMace) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Steel Morning Star Mace", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Steel Morning Star Mace", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 330:

                        if (!CanWinSteelPickaxe) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Steel Pickaxe", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Steel Pickaxe", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 331:

                        if (!CanWinSteelPictureFrame) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Steel Picture Frame", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 2;
                            GiveItem(player, "Steel Picture Frame", 2);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 332:

                        if (!CanWinSteelPlateBoots) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Steel Plate Boots", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Steel Plate Boots", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 333:

                        if (!CanWinSteelPlateGauntlets) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Steel Plate Gauntlets", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Steel Plate Gauntlets", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 334:

                        if (!CanWinSteelPlateHelmet) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Steel Plate Helmet", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Steel Plate Helmet", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 335:

                        if (!CanWinSteelPlatePants) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Steel Plate Pants", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Steel Plate Pants", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 336:

                        if (!CanWinSteelPlateVest) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Steel Plate Vest", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Steel Plate Vest", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 337:

                        if (!CanWinSteelSpear) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Steel Spear", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Steel Spear", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 338:

                        if (!CanWinSteelStarMace) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Steel Star Mace", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Steel Star Mace", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 339:

                        if (!CanWinSteelSword) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Steel Sword", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Steel Sword", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 340:

                        if (!CanWinSteelThrowingBattleAxe) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Steel Throwing Battle Axe", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 20;
                            GiveItem(player, "Steel Throwing Battle Axe", 20);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 341:

                        if (!CanWinSteelThrowingKnife) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Steel Throwing  Knife", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 20;
                            GiveItem(player, "Steel Throwing  Knife", 20);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 342:

                        if (!CanWinSteelTippedArrow) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Steel Tipped Arrow", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 20;
                            GiveItem(player, "Steel Tipped Arrow", 20);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 343:

                        if (!CanWinSteelTower) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Steel Tower", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Steel Tower", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 344:

                        if (!CanWinSteelWarHammer) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Steel War Hammer", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Steel War Hammer", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 345:

                        if (!CanWinSteelWoodCuttersAxe) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Steel Wood Cutters Axe", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Steel Wood Cutters Axe", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 346:

                        if (!CanWinSticks) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Sticks", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 100;
                            GiveItem(player, "Sticks", 100);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 347:

                        if (!CanWinStiffBed) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Stiff Bed", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Stiff Bed", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 348:

                        if (!CanWinStone) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Stone", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 400;
                            GiveItem(player, "Stone", 400);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 349:

                        if (!CanWinStoneArch) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Stone Arch", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 10;
                            GiveItem(player, "Stone Arch", 10);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 350:

                        if (!CanWinStoneArrow) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Stone Arrow", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 30;
                            GiveItem(player, "Stone Arrow", 30);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 351:

                        if (!CanWinStoneBlock) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Stone Block", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 50;
                            GiveItem(player, "Stone Block", 50);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 352:

                        if (!CanWinStoneCorner) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Stone Corner", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 20;
                            GiveItem(player, "Stone Corner", 20);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 353:

                        if (!CanWinStoneCutter) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Stone Corner", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 20;
                            GiveItem(player, "Stone Corner", 20);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 354:

                        if (!CanWinStoneDagger) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Stone Dagger", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Stone Dagger", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 355:

                        if (!CanWinStoneFireplace) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Stone Fireplace", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Stone Fireplace", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 356:

                        if (!CanWinStoneHatchet) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Stone Hatchet", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Stone Hatchet", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 357:

                        if (!CanWinStoneInvertedCorner) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Stone Inverted Corner", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Stone Inverted Corner", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 358:

                        if (!CanWinStoneJavelin) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Stone Javelin", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 20;
                            GiveItem(player, "Stone Javelin", 20);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 359:

                        if (!CanWinStonePickaxe) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Stone Pickaxe", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Stone Pickaxe", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 360:

                        if (!CanWinStoneRamp) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Stone Ramp", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 20;
                            GiveItem(player, "Stone Ramp", 20);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 361:

                        if (!CanWinStoneSlab) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Stone Slab", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 10;
                            GiveItem(player, "Stone Slab", 10);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 362:

                        if (!CanWinStoneSlitWindow) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Stone Slit Window", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 3;
                            GiveItem(player, "Stone Slit Window", 3);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 363:

                        if (!CanWinStoneSpear) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Stone Spear", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Stone Spear", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 364:

                        if (!CanWinStoneStairs) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Stone Stairs", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 20;
                            GiveItem(player, "Stone Stairs", 20);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 365:

                        if (!CanWinStoneSword) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Stone Sword", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Stone Sword", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 366:

                        if (!CanWinStoneThrowingAxe) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Stone Throwing Axe", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 20;
                            GiveItem(player, "Stone Throwing Axe", 20);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 367:

                        if (!CanWinStoneThrowingKnife) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Stone Throwing Axe", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 20;
                            GiveItem(player, "Stone Throwing Knife", 20);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 368:

                        if (!CanWinStoneTotem) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Stone Totem", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Stone Totem", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 369:

                        if (!CanWinStoneWoodCuttersAxe) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Stone Wood Cutters Axe", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Stone Wood Cutters Axe", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 370:

                        if (!CanWinTabard) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Tabard", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Tabard", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 371:

                        if (!CanWinTannery) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Tannery", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Tannery", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 372:

                        if (!CanWinTearsOfTheGods) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Tears Of The Gods", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Tears Of The Gods", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 373:

                        if (!CanWinThatchBlock) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Thatch Block", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 50;
                            GiveItem(player, "Thatch Block", 50);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 374:

                        if (!CanWinThatchCorner) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Thatch Corner", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 10;
                            GiveItem(player, "Thatch Corner", 10);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 375:

                        if (!CanWinThatchInvertedCorner) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Thatch Inverted Corner", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 10;
                            GiveItem(player, "Thatch Inverted Corner", 10);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 376:

                        if (!CanWinThatchRamp) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Thatch Ramp", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 10;
                            GiveItem(player, "Thatch Ramp", 10);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 377:

                        if (!CanWinThatchStairs) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Thatch Stairs", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 20;
                            GiveItem(player, "Thatch Stairs", 20);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 378:

                        if (!CanWinTheaterMaskGoldRed) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Theater Mask (Gold & Red)", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Theater Mask (Gold & Red)", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 379:

                        if (!CanWinTheaterMaskWhiteBlue) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Theater Mask (White & Blue)", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Theater Mask (White & Blue)", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 380:

                        if (!CanWinTheaterMaskWhiteGold) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Theater Mask (White & Gold)", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Theater Mask (White & Gold)", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 381:

                        if (!CanWinTheaterMaskWhiteRed) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Theater Mask (White & Red)", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Theater Mask (White & Red)", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 382:

                        if (!CanWinTheatreMaskComedy) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Theatre Mask (Comedy)", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Theatre Mask (Comedy)", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 383:

                        if (!CanWinTheatreMaskTragedy) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Theatre Mask (Tragedy)", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Theatre Mask (Tragedy)", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 384:

                        if (!CanWinThrowingStone) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Throwing Stone", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 20;
                            GiveItem(player, "Throwing Stone", 20);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 385:

                        if (!CanWinTinker) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Tinker", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Tinker", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 386:

                        if (!CanWinTorch) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Torch", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Torch", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 387:

                        if (!CanWinTrebuchet) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Trebuchet", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Trebuchet", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 388:

                        if (!CanWinTrebuchetHayBale) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Trebuchet Hay Bale", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 10;
                            GiveItem(player, "Trebuchet Hay Bale", 10);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 389:

                        if (!CanWinTrebuchetStone) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Trebuchet Stone", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 20;
                            GiveItem(player, "Trebuchet Stone", 20);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 390:

                        if (!CanWinWallLantern) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Wall Lantern", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 2;
                            GiveItem(player, "Wall Lantern", 2);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 391:

                        if (!CanWinWallTorch) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Wall Torch", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 2;
                            GiveItem(player, "Wall Torch", 2);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 392:

                        if (!CanWinWarDrum) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("War Drum", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "War Drum", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 393:

                        if (!CanWinWasp) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Wasp", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Wasp", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 394:

                        if (!CanWinWater) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Water", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 200;
                            GiveItem(player, "Water", 200);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 395:

                        if (!CanWinWateringPot) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Watering Pot", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Watering Pot", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 396:

                        if (!CanWinWell) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Well", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Well", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 397:

                        if (!CanWinWencelasHelmet) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Wencelas Helmet", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Wencelas Helmet", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 398:

                        if (!CanWinWerewolf) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Werewolf", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Werewolf", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 399:

                        if (!CanWinWhip) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Whip", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Whip", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 400:

                        if (!CanWinWolf) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Wolf", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Wolf", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 401:

                        if (!CanWinWolfPelt) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Wolf Pelt", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 6;
                            GiveItem(player, "Wolf Pelt", 6);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 402:

                        if (!CanWinWood) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Wood", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 200;
                            GiveItem(player, "Wood", 200);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 403:

                        if (!CanWinWoodArrow) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Wood Arrow", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 20;
                            GiveItem(player, "Wood Arrow", 20);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 404:

                        if (!CanWinWoodBarricade) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Wood Barricade", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 4;
                            GiveItem(player, "Wood Barricade", 4);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 405:

                        if (!CanWinWoodBlock) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Wood Block", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 50;
                            GiveItem(player, "Wood Block", 50);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 406:

                        if (!CanWinWoodBracers) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Wood Bracers", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Wood Bracers", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 407:

                        if (!CanWinWoodBuckler) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Wood Buckler", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Wood Buckler", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 408:

                        if (!CanWinWoodCage) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Wood Cage", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Wood Cage", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 409:

                        if (!CanWinWoodChest) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Wood Chest", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Wood Chest", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 410:

                        if (!CanWinWoodCorner) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Wood Corner", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 10;
                            GiveItem(player, "Wood Corner", 10);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 411:

                        if (!CanWinWoodDoor) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Wood Door", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 2;
                            GiveItem(player, "Wood Door", 2);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 412:

                        if (!CanWinWoodDrawbridge) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Wood Drawbridge", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Wood Drawbridge", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 413:

                        if (!CanWinWoodFlute) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Wood Flute", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Wood Flute", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 414:

                        if (!CanWinWoodGate) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Wood Gate", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Wood Gate", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 415:

                        if (!CanWinWoodHeater) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Wood Heater", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Wood Heater", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 416:

                        if (!CanWinWoodHelmet) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Wood Helmet", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Wood Helmet", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 417:

                        if (!CanWinWoodInvertedCorner) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Wood Inverted Corner", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 20;
                            GiveItem(player, "Wood Inverted Corner", 20);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 418:

                        if (!CanWinWoodJavelin) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Wood Javelin", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 20;
                            GiveItem(player, "Wood Javelin", 20);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 419:

                        if (!CanWinWoodLedge) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Wood Ledge", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 2;
                            GiveItem(player, "Wood Ledge", 2);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 420:

                        if (!CanWinWoodMace) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Wood Mace", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 2;
                            GiveItem(player, "Wood Mace", 2);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 421:

                        if (!CanWinWoodPictureFrame) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Wood Picture Frame", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 2;
                            GiveItem(player, "Wood Picture Frame", 2);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 422:

                        if (!CanWinWoodRamp) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Wood Ramp", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 20;
                            GiveItem(player, "Wood Ramp", 20);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 423:

                        if (!CanWinWoodSandals) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Wood Sandals", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Wood Sandals", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 424:

                        if (!CanWinWoodShortBow) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Wood Short Bow", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Wood Short Bow", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 425:

                        if (!CanWinWoodShutters) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Wood Shutters", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 4;
                            GiveItem(player, "Wood Shutters", 4);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 426:

                        if (!CanWinWoodSkirt) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Wood Skirt", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Wood Skirt", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 427:

                        if (!CanWinWoodSpear) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Wood Spear", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Wood Spear", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 428:

                        if (!CanWinWoodSpikes) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Wood Spikes", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Wood Spikes", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 429:

                        if (!CanWinWoodStairs) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Wood Stairs", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 20;
                            GiveItem(player, "Wood Stairs", 20);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 430:

                        if (!CanWinWoodStick) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Wood Stick", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Wood Stick", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 431:

                        if (!CanWinWoodSword) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Wood Sword", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Wood Sword", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 432:

                        if (!CanWinWoodTotem) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Wood Totem", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Wood Totem", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 433:

                        if (!CanWinWoodTower) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Wood Tower", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Wood Tower", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 434:

                        if (!CanWinWoodVest) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Wood Vest", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Wood Vest", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 435:

                        if (!CanWinWoodworking) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Woodworking", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Woodworking", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 436:

                        if (!CanWinWool) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Wool", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 20;
                            GiveItem(player, "Wool", 20);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 437:

                        if (!CanWinWorkBench) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Work Bench", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            GiveItem(player, "Work Bench", 1);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 438:

                        if (!CanWinWorms) continue;
                        else
                        {
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Worms", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 10;
                            GiveItem(player, "Worms", 10);
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 439:

                        if (!CanWinJackpot) continue;
                        else
                        {
                            PrintToChat(player, string.Format(GetMessage("30", player.Id.ToString()), "439"));
                            PrintToChat(player, string.Format(GetMessage("6", player.Id.ToString()), "439"));
                            GiveItem(player, "Bat", 10);
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Bat", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 10;
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                            GiveItem(player, "Trebuchet", 1);
                            blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Trebuchet", true, true);
                            itemstring = blueprintForName.Name;
                            amount = 1;
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                            GiveItem(player, "War Drum", 1);
                            blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("War Drum", true, true);
                            itemstring = blueprintForName.Name;
                            amount = 1;
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                            GiveItem(player, "Trebuchet Stone", 20);
                            blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Trebuchet Stone", true, true);
                            itemstring = blueprintForName.Name;
                            amount = 20;
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                            GiveItem(player, "Steel Greatsword", 1);
                            blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Steel Greatsword", true, true);
                            itemstring = blueprintForName.Name;
                            amount = 1;
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 440:

                        if (!CanWinSteelArmorSetJackpot) continue;
                        else
                        {
                            PrintToChat(player, string.Format(GetMessage("30", player.Id.ToString()), "440"));
                            PrintToChat(player, string.Format(GetMessage("7", player.Id.ToString()), "440"));
                            GiveItem(player, "Steel Plate Boots", 1);
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Steel Plate Boots", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                            GiveItem(player, "Steel Plate Gauntlets", 1);
                            blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Steel Plate Gauntlets", true, true);
                            itemstring = blueprintForName.Name;
                            amount = 1;
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                            GiveItem(player, "Steel Plate Helmet", 1);
                            blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Steel Plate Helmet", true, true);
                            itemstring = blueprintForName.Name;
                            amount = 1;
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                            GiveItem(player, "Steel Plate Pants", 1);
                            blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Steel Plate Pants", true, true);
                            itemstring = blueprintForName.Name;
                            amount = 1;
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                            GiveItem(player, "Steel Plate Vest", 1);
                            blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Steel Plate Vest", true, true);
                            itemstring = blueprintForName.Name;
                            amount = 1;
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 441:

                        if (!CanWinIronArmorSetJackpot) continue;
                        else
                        {
                            PrintToChat(player, string.Format(GetMessage("30", player.Id.ToString()), "441"));
                            PrintToChat(player, string.Format(GetMessage("8", player.Id.ToString()), "441"));
                            GiveItem(player, "Iron Plate Boots", 1);
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Iron Plate Boots", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                            GiveItem(player, "Iron Plate Gauntlets", 1);
                            blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Iron Plate Gauntlets", true, true);
                            itemstring = blueprintForName.Name;
                            amount = 1;
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                            GiveItem(player, "Iron Plate Helmet", 1);
                            blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Iron Plate Helmet", true, true);
                            itemstring = blueprintForName.Name;
                            amount = 1;
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                            GiveItem(player, "Iron Plate Pants", 1);
                            blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Iron Plate Pants", true, true);
                            itemstring = blueprintForName.Name;
                            amount = 1;
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                            GiveItem(player, "Iron Plate Vest", 1);
                            blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Iron Plate Vest", true, true);
                            itemstring = blueprintForName.Name;
                            amount = 1;
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 442:

                        if (!CanWinLeatherArmorSetJackpot) continue;
                        else
                        {
                            PrintToChat(player, string.Format(GetMessage("30", player.Id.ToString()), "442"));
                            PrintToChat(player, string.Format(GetMessage("9", player.Id.ToString()), "442"));
                            GiveItem(player, "Light Leather Boots", 1);
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Light Leather Boots", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 1;
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                            GiveItem(player, "Light Leather Bracers", 1);
                            blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Light Leather Bracers", true, true);
                            itemstring = blueprintForName.Name;
                            amount = 1;
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                            GiveItem(player, "Light Leather Helmet", 1);
                            blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Light Leather Helmet", true, true);
                            itemstring = blueprintForName.Name;
                            amount = 1;
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                            GiveItem(player, "Light Leather Pants", 1);
                            blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Light Leather Pants", true, true);
                            itemstring = blueprintForName.Name;
                            amount = 1;
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                            GiveItem(player, "Light Leather Vest", 1);
                            blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Light Leather Vest", true, true);
                            itemstring = blueprintForName.Name;
                            amount = 1;
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 443:

                        if (!CanWinBuilderSetJackpot) continue;
                        else
                        {
                            PrintToChat(player, string.Format(GetMessage("30", player.Id.ToString()), "443"));
                            PrintToChat(player, string.Format(GetMessage("10", player.Id.ToString()), "443"));
                            GiveItem(player, "Stone Block", 700);
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Stone Block", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 700;
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                            GiveItem(player, "Stone Ramp", 100);
                            blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Stone Ramp", true, true);
                            itemstring = blueprintForName.Name;
                            amount = 100;
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                            GiveItem(player, "Stone Stairs", 100);
                            blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Stone Stairs", true, true);
                            itemstring = blueprintForName.Name;
                            amount = 100;
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                            GiveItem(player, "Iron Door", 2);
                            blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Iron Door", true, true);
                            itemstring = blueprintForName.Name;
                            amount = 2;
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                            GiveItem(player, "Iron Gate", 2);
                            blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Iron Gate", true, true);
                            itemstring = blueprintForName.Name;
                            amount = 2;
                            PrintToChat(player, string.Format(GetMessage("2", player.Id.ToString()), amount.ToString(), itemstring));
                        }
                        return;
                    case 444:

                        if (!CanWinRobbed) continue;
                        else
                        {
                            PrintToChat(player, string.Format(GetMessage("23", player.Id.ToString()), "445"));
                            Bite(player);
                            PrintToChat(player, string.Format(GetMessage("11", player.Id.ToString()), "44"));
                            GiveItem(player, "Blood", 10);
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Blood", true, true);
                            string itemstring = blueprintForName.Name;
                        }
                        return;
                    case 445:

                        if (!CanWinGangAttack) continue;
                        else
                        {
                            PrintToChat(player, string.Format(GetMessage("23", player.Id.ToString()), "445"));
                            Bite(player);
                            PrintToChat(player, string.Format(GetMessage("12", player.Id.ToString()), "445"));
                            Bite(player);
                            Bite(player);
                            PrintToChat(player, string.Format(GetMessage("13", player.Id.ToString()), "445"));
                            Bite(player);
                            PrintToChat(player, string.Format(GetMessage("14", player.Id.ToString()), "445"));
                            PrintToChat(player, string.Format(GetMessage("15", player.Id.ToString()), "445"));
                            PrintToChat(player, string.Format(GetMessage("16", player.Id.ToString()), "445"));
                            PrintToChat(player, string.Format(GetMessage("17", player.Id.ToString()), "445"));
                            GiveItem(player, "Bandage", 10);
                            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Bandage", true, true);
                            string itemstring = blueprintForName.Name;
                            int amount = 10;
                            GiveItem(player, "Water", 10);
                            blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Water", true, true);
                            itemstring = blueprintForName.Name;
                            amount = 10;
                            PrintToChat(player, string.Format(GetMessage("18", player.Id.ToString()), "445"));
                            GiveItem(player, "Cooked Meat", 10);
                            blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Cooked Meat", true, true);
                            itemstring = blueprintForName.Name;
                            amount = 10;
                            PrintToChat(player, string.Format(GetMessage("19", player.Id.ToString()), "445"));
                            GiveItem(player, "Bat", 10);
                            blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Bat", true, true);
                            itemstring = blueprintForName.Name;
                            amount = 10;
                            PrintToChat(player, string.Format(GetMessage("20", player.Id.ToString()), "445"));
                            GiveItem(player, "Diamond", 10);
                            blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Diamond", true, true);
                            itemstring = blueprintForName.Name;
                            amount = 10;
                            PrintToChat(player, string.Format(GetMessage("21", player.Id.ToString()), "445"));
                            PrintToChat(player, string.Format(GetMessage("22", player.Id.ToString()), "445"));
                        }
                        return;
                    case 446:

                        if (!CanWinGold1K) continue;
                        else
                        if (GrandExchange != null)
                        {
                            GrandExchange.CallHook("GiveGold", player, 1000);
                            PrintToChat(player, string.Format(GetMessage("24", player.Id.ToString()), "445")); return;
                        }
                        else continue;
                    case 447:

                        if (!CanWinGold10K) continue;
                        else
                        if (GrandExchange != null)
                        {
                            GrandExchange.CallHook("GiveGold", player, 10000);
                            PrintToChat(player, string.Format(GetMessage("25", player.Id.ToString()), "445")); return;
                        }
                        else continue;
                    case 448:

                        if (!CanWinGold100K) continue;
                        else
                        if (GrandExchange != null)
                        {
                            GrandExchange.CallHook("GiveGold", player, 100000);
                            PrintToChat(player, string.Format(GetMessage("26", player.Id.ToString()), "445")); return;
                        }
                        else continue;
                }
            }
        }

        void OnPlayerConnected(Player player)
        {
            Puts("OnPlayerConnected works!");
        }
        #endregion
    }
}