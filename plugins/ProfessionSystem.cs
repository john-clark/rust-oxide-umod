#region Header
using CodeHatch.Common;
using CodeHatch.Engine.Core.Cache;
using CodeHatch.Engine.Modules.SocialSystem;
using CodeHatch.Engine.Networking;
using CodeHatch.ItemContainer;
using CodeHatch.Networking.Events;
using CodeHatch.Networking.Events.Entities;
using CodeHatch.Networking.Events.Players;
using CodeHatch.Thrones.SocialSystem;
using CodeHatch.UserInterface.Dialogues;
using Oxide.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("ProfessionSystem", "juk3b0x", "2.2.0")] // Known/Future Issues: 1:Might want to specify the Resource/Item by its Unique ID, 2:Add German Language support
    public class ProfessionSystem : ReignOfKingsPlugin
    {
#endregion
        #region Language API
        private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "Title", "Choose Your Profession" },
                { "DescTitle", "Available Professions" },
                { "NewOnServer", "You are new on this server and thus you have to choose a profession! \nChoose one of the following : \n{0}Stone Mason \n{1}Blacksmith \n{2}Soldier \n{3}Archer \n{4}Carpenter \n{5}Farmer \n{6}Collector \n{7}Assassin[FFFFFF] \n\n" },
                { "StoneMasonDesc", "As a Stone Mason you will : \n - have {0} inventory slots \n - deal {1} percent damage \n - receive {2} percent damage \n - of course only be able to work with stone \n\n" },
                { "BlacksmithDesc", "As a Blacksmith you will : \n - have {0} inventory slots \n - deal {1} percent damage \n - receive {2} percent damage \n - of course only be able to work metal \n\n" },
                { "SoldierDesc", "As a Soldier you will : \n - have {0} inventory slots \n - deal {1} percent damage \n - receive {2} percent damage \n - only be able to produce simple things \n\n" },
                { "ArcherDesc", "As an Archer you will : \n - have {0} inventory slots \n - deal {1} percent damage \n - receive {2} percent damage \n - only be able to produce simple things and bolts/arrows \n\n" },
                { "CarpenterDesc", "As a Carpenter you will : \n - have {0} inventory slots \n - deal {1} percent damage \n - receive {2} percent damage \n - be able to produce interior, bows, crossbows and work any kind of wood \n\n" },
                { "FarmerDesc", "As a Farmer you will : \n - have {0} inventory slots \n - deal {1} percent damage \n - receive {2} percent damage \n - be able to tame wild animals (including werewolves) \n - be able to harvest regrowing materials from animals on your territory (wool, feathers etc.) \n - be able to produce ay clothing except armor \n\n" },
                { "CollectorDesc", "As a Collector you will : \n - have {0} inventory slots \n - deal {1} percent damage \n - receive {2} percent damage \n - be able to collect a lot of material \n - be the only class capable of farming \n\n" },
                { "AssassinDesc", "As an Assassin you will : \n - have {0} inventory slots \n - deal {1} percent damage (with daggers, throwing knives and crossbows) \n - receive {2} percent damage \n - will be the only class to be able to collect bounties \n - will receive extra damage when attacking at night \n - will only be able to prduce basic stuff, bolts and throwing knives \n\n" },
                { "Choose", "Choose wisely, changing your profession later will cost {0} {1} !" },
                { "Change", "You decided to change your profession. \nChoose one of the following : \n{0}Stone Mason \n{1}Blacksmith \n{2}Soldier \n{3}Archer \n{4}Carpenter \n{5}Farmer \n{6}Collector \n{7}Assassin[FFFFFF]\n\n" },
                { "Careful", "[FF0000]CAREFUL [FFFFFF], it will cost  {0} {1} to change AND you will lose all other items on your inventory!" },
                { "FailedToChoose", "You have failed to choose a profession, type /changeprofession to do that!" },
                { "NotExistingProfession", "The profession you tried to choose does not exist, please type /professions, to see which professions are availale!" },
                { "KillViolator", "You need to choose a profession, or you die upon attacking!!!" },
                { "StickOrWhip", "You must use a WOODEN STICK or a WHIP to tame animals!" },
                { "NoCrestedTaming", "If you are trying to TAME this animal, do it in the wilds, you cannot TAME on crested Land!" },
                { "PreventDamageViolator", "You cannot attack until you choose a profession!" },
                { "WrongProfession", "You are a {0}! [FFFFFF]You cannot craft [FF0000]{1}![FFFFFF]" },
                { "NoFarmer", "You are not a Farmer you cannot collect animals from your territory!" },
                { "OnlyMateOnTerritory", "You can only Mate Animals on your territory!" },
                { "WrongMatingArgs", "The animal you entered does not exist!" },
                { "Harvested", "You harvested {0} {1} and {2} {3}! ! You now have to wait for the server to restart, to harvest again!" },
                { "NoHarvestableAnimals", "There are no harvestable animals on your territory!" },
                { "NoMatableAnimals", "There are no matable animals around!" },
                { "LooseInventoryTitle", "[FF0000]ATTENTION!!!!!![FFFFFF]" },
                { "LooseInventory", "[FF0000]Beware!!!!!![FFFFFF]\n If you change your profession, you will loose all your current inventory, drop it to the ground before changing your profession!" },
                { "ResourcesToMate", "You need {0} {1} to mate {2} {3}!" },
                { "MateSuccess", "You have successfully mated your {0}!" },
                { "MateFailBite", "You tried to mate your {0} but the male attacked you!" },
                { "MateFailDie", "You have successfully mated your {0}, but the offspring died at birth, you collect its remains...!" },
                { "OnlyCollectOnGuildTerritory", "You can only collect on your own guilds territory!" },
                { "OnlyHarvestOnGuildTerritory", "You need to be on YOUR territory to harvest animals!" },
                { "OnHarvestTimeout", "You have already harvested your animals, wait for the server restart to do it again!" },
                { "OnMateTimeout", "You already have mated these animals, wait for the server restart or mate another species!" },
                { "ChosenWorker", "Congratulations! You have chosen to become a {0}{1}[FFFFFF]!" },
                { "NotEnough", "You don't have enough {0} to change your Profession!" }
            }, this);
        }
        string GetMessage(string key, string userId = null) => lang.GetMessage(key, this, userId);
        #endregion
        #region Lists
        List<ulong> HarvestedSinceLastRestart = new List<ulong>();
        List<ulong> MatedChickenSinceLastRestart = new List<ulong>();
        List<ulong> MatedDuckSinceLastRestart = new List<ulong>();
        List<ulong> MatedGrizzlySinceLastRestart = new List<ulong>();
        List<ulong> MatedRabbitsSinceLastRestart = new List<ulong>();
        List<ulong> MatedSheepSinceLastRestart = new List<ulong>();
        List<ulong> MatedWolfSinceLastRestart = new List<ulong>();
        List<ulong> MatedWerewolfSinceLastRestart = new List<ulong>();
        private Dictionary<ulong,string> _ProfessionList = new Dictionary<ulong,string>();
        // _ProfessionList.Key = SteamId
        // _ProfessionList.Value = Playername including prefix (worker or Warrior)
        Dictionary<ulong, Dictionary<string, int>> _ProfessionLevels = new Dictionary<ulong, Dictionary<string, int>>();
        #endregion
        #region List loading and saving
        private void LoadLists()
        {
            _ProfessionList = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<ulong,string>>("ProfessionList");
            _ProfessionLevels = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<ulong,Dictionary<string,int>>>("ProfessionLevels");
        }
        private void SaveLists()
        {
            Interface.GetMod().DataFileSystem.WriteObject("ProfessionList", _ProfessionList);
            Interface.GetMod().DataFileSystem.WriteObject("ProfessionLevels", _ProfessionLevels);
        }
        void Loaded()
        {
            LoadLists();
        }
        #endregion
        #region Config
        bool KillViolator;
        bool AddPrefix;
        string ResourceToChangeProfession;
        int ResourceAmountToChange;

        string StoneMasonCol;
        int StoneMasonInv;
        float StoneMasonDmg;
        float StoneMasonDef;

        string BlacksmithCol;
        int BlacksmithInv;
        float BlacksmithDmg;
        float BlacksmithDef;

        string SoldierCol;
        int SoldierInv;
        float SoldierDmg;
        float SoldierDef;

        string ArcherCol;
        int ArcherInv;
        float ArcherDmg;
        float ArcherDef;

        string CarpenterCol;
        int CarpenterInv;
        float CarpenterDmg;
        float CarpenterDef;

        string FarmerCol;
        int FarmerInv;
        float FarmerDmg;
        float FarmerDef;
        int FarmerHarvestAmount;
        float FarmerTameChanceSheep;
        float FarmerTameChanceChicken;
        float FarmerTameChanceBear;
        float FarmerTameChanceRabbit;
        float FarmerTameChanceWolf;
        float FarmerTameChanceWerewolf;
        float FarmerTameChanceMoose;
        float FarmerTameChanceDeer;
        float FarmerTameChanceDuck;
        float FarmerTameChanceRooster;
        string FarmerResourceToMateChicken;
        string FarmerResourceToMateRabbit;
        string FarmerResourceToMateDuck;
        string FarmerResourcesToMateWolf0;
        string FarmerResourcesToMateWolf1;
        string FarmerResourcesToMateGrizzly0;
        string FarmerResourcesToMateGrizzly1;
        string FarmerResourcesToMateGrizzly2;
        string FarmerResourcesToMateWerewolf0;
        string FarmerResourcesToMateWerewolf1;
        string FarmerResourcesToMateWerewolf2;
        string FarmerResourcesToMateWerewolf3;
        string FarmerResourceToMateSheep;
        int AmountToMateChicken;
        int AmountToMateRabbit;
        int AmountToMateDuck;
        int AmountToMateWolf;
        int AmountToMateGrizzly;
        int AmountToMateWerewolf;
        int AmountToMateSheep;
        int FarmerChanceToMateChicken;
        int FarmerChanceToMateDuck;
        int FarmerChanceToMateGrizzly;
        int FarmerChanceToMateRabbit;
        int FarmerChanceToMateSheep;
        int FarmerChanceToMateWolf;
        int FarmerChanceToMateWerewolf;

        string CollectorCol;
        int CollectorInv;
        float CollectorDmg;
        float CollectorDef;

        string AssassinCol;
        int AssassinInv;
        float AssassinDmg;
        float AssassinDef;

        void Init() => LoadDefaultConfig();

        protected override void LoadDefaultConfig()
        {
            // General Config
            Config["Add a Prefix"] = AddPrefix = GetConfig("Add a Prefix", true);
            Config["Kill Violators"] = KillViolator = GetConfig("Kill Violators", true);
            Config["AMOUNT of the Resource required to change Profession"] = ResourceAmountToChange = GetConfig("AMOUNT of the Resource required to change Profession", 10);
            Config["ID of the Resource required to change Profession"] = ResourceToChangeProfession = GetConfig("ID of the Resource required to change Profession", "Diamond");
            // StoneMason Config
            Config["Color for the Stone Mason-Prefix"] = StoneMasonCol = GetConfig("Color for the Stone Mason-Prefix", "[FFFFFF]");
            Config["Inventory Space for Stone Mason"] = StoneMasonInv = GetConfig("Inventory Space for Stone Mason", 10);
            Config["Percent of Damage a Stone Mason does"] = StoneMasonDmg = GetConfig("Percent of Damage a Stone Mason does", 75f);
            Config["Percent of Damage a Stone Mason takes"] = StoneMasonDef = GetConfig("Percent of Damage a Stone Mason takes", 100f);
            // Blacksmith Config
            Config["Color for the Blacksmith-Prefix"] = BlacksmithCol = GetConfig("Color for the Blacksmith-Prefix", "[000000]");
            Config["Inventory Space for Blacksmith"] = BlacksmithInv = GetConfig("Inventory Space for Blacksmith", 10);
            Config["Percent of Damage a Blacksmith does"] = BlacksmithDmg = GetConfig("Percent of Damage a Blacksmith does", 75f);
            Config["Percent of Damage a Blacksmith takes"] = BlacksmithDef = GetConfig("Percent of Damage a Blacksmith takes", 100f);
            // Soldier Config
            Config["Color for the Soldier-Prefix"] = SoldierCol = GetConfig("Color for the Soldier-Prefix", "[808080]");
            Config["Inventory Space for Soldier"] = SoldierInv = GetConfig("Inventory Space for Soldier", 2);
            Config["Percent of Damage a Soldier does"] = SoldierDmg = GetConfig("Percent of Damage a Soldier does", 100f);
            Config["Percent of Damage a Soldier takes"] = SoldierDef = GetConfig("Percent of Damage a Soldier takes", 75f);
            // Archer Config
            Config["Color for the Archer-Prefix"] = ArcherCol = GetConfig("Color for the Archer-Prefix", "[FFFF00]");
            Config["Inventory Space for Archer"] = ArcherInv = GetConfig("Inventory Space for Archer", 2);
            Config["Percent of Damage an Archer does (with ranged Weapons)"] = ArcherDmg = GetConfig("Percent of Damage an Archer does (with ranged Weapons)", 150f);
            Config["Percent of Damage an Archer takes"] = ArcherDef = GetConfig("Percent of Damage an Archer takes", 100f);
            // Carpenter Config
            Config["Color for the Carpenter-Prefix"] = CarpenterCol = GetConfig("Color for the Carpenter-Prefix", "[D2691E]");
            Config["Inventory Space for Carpenter"] = CarpenterInv = GetConfig("Inventory Space for Carpenter", 10);
            Config["Percent of Damage a Carpenter does"] = CarpenterDmg = GetConfig("Percent of Damage a Carpenter does", 75f);
            Config["Percent of Damage a Carpenter takes"] = CarpenterDef = GetConfig("Percent of Damage a Carpenter takes", 100f);
            // Farmer Config
            Config["Color for the Farmer-Prefix"] = FarmerCol = GetConfig("Color for the Farmer-Prefix", "[008000]");
            Config["Inventory Space for Farmer"] = FarmerInv = GetConfig("Inventory Space for Farmer", 10);
            Config["Percent of Damage a Farmer does (to Players)"] = FarmerDmg = GetConfig("Percent of Damage a Farmer does (to Players)", 50f);
            Config["Percent of Damage a Farmer takes"] = FarmerDef = GetConfig("Percent of Damage a Farmer takes", 125f);
            Config["Amount of Resources the Farmer can harvest from animals"] = FarmerHarvestAmount = GetConfig("Amount of Resources the Farmer can harvest from animals", 20);
            Config["Chance of Taming a Sheep (Farmer)"] = FarmerTameChanceSheep = GetConfig("Chance of Taming a Sheep (Farmer)", 20f);
            Config["Chance of Taming a Chicken(Farmer)"] = FarmerTameChanceChicken = GetConfig("Chance of Taming a Chicken(Farmer)", 20f);
            Config["Chance of Taming a Rooster(Farmer)"] = FarmerTameChanceRooster = GetConfig("Chance of Taming a Rooster(Farmer)", 20f);
            Config["Chance of Taming a Rabbit(Farmer)"] = FarmerTameChanceRabbit = GetConfig("Chance of Taming a Rabbit(Farmer)", 30f);
            Config["Chance of Taming a Wolf(Farmer)"] = FarmerTameChanceWolf = GetConfig("Chance of Taming a Wolf(Farmer)", 10f);
            Config["Chance of Taming a Grizzly-Bear(Farmer)"] = FarmerTameChanceBear = GetConfig("Chance of Taming a Grizzly-Bear(Farmer)", 5f);
            Config["Chance of Taming a Duck(Farmer)"] = FarmerTameChanceDuck = GetConfig("Chance of Taming a Duck(Farmer)", 17f);
            Config["Chance of Taming a Werewolf(Farmer)"] = FarmerTameChanceWerewolf = GetConfig("Chance of Taming a Werewolf(Farmer)", 2f);

            Config["ID of the Resource needed to mate Chicken(Farmer)"] = FarmerResourceToMateChicken = GetConfig("ID of the Resource needed to mate Chicken(Farmer)", "Grain Seed");
            Config["Resourcetype needed to mate Rabbit(Farmer)"] = FarmerResourceToMateRabbit = GetConfig("Resourcetype needed to mate Rabbit(Farmer)", "Carrot");
            Config["Resourcetype needed to mate Duck(Farmer)"] = FarmerResourceToMateDuck = GetConfig("Resourcetype needed to mate Duck(Farmer)", "Bread");
            Config["Resourcetypes needed to mate Wolf(Farmer)"] = FarmerResourcesToMateWolf0 = GetConfig("Resourcetypes needed to mate Wolf(Farmer)","Meat");
            Config["Resourcetypes needed to mate Wolf(Farmer)"] = FarmerResourcesToMateWolf1 = GetConfig("Resourcetypes needed to mate Wolf(Farmer)", "Hay");
            Config["Resourcetypes needed to mate Grizzly(Farmer)"] = FarmerResourcesToMateGrizzly0 = GetConfig("Resourcetypes needed to mate Grizzl(Farmer)","Berries");
            Config["Resourcetypes needed to mate Grizzly(Farmer)"] = FarmerResourcesToMateGrizzly1 = GetConfig("Resourcetypes needed to mate Grizzl(Farmer)","Meat");
            Config["Resourcetypes needed to mate Grizzly(Farmer)"] = FarmerResourcesToMateGrizzly2 = GetConfig("Resourcetypes needed to mate Grizzl(Farmer)","Hay");
            Config["Resourcetypes needed to mate Werewolf(Farmer)"] = FarmerResourcesToMateWerewolf0 = GetConfig("Resourcetypes needed to mate Werewolf(Farmer)", "Meat");
            Config["Resourcetypes needed to mate Werewolf(Farmer)"] = FarmerResourcesToMateWerewolf1 = GetConfig("Resourcetypes needed to mate Werewolf(Farmer)","Blood");
            Config["Resourcetypes needed to mate Werewolf(Farmer)"] = FarmerResourcesToMateWerewolf2 = GetConfig("Resourcetypes needed to mate Werewolf(Farmer)", "Liver");
            Config["Resourcetypes needed to mate Werewolf(Farmer)"] = FarmerResourcesToMateWerewolf3 = GetConfig("Resourcetypes needed to mate Werewolf(Farmer)","Heart" );
            Config["Resourcetype needed to mate Sheep(Farmer)"] = FarmerResourceToMateSheep = GetConfig("Resourcetype needed to mate Sheep(Farmer)", "Hay");
            Config["Resourceamount needed to mate Chicken(Farmer)"] = AmountToMateChicken = GetConfig("Resourceamount needed to mate Chicken(Farmer)", 10);
            Config["Resourceamount needed to mate Rabbit(Farmer)"] = AmountToMateRabbit = GetConfig("Resourceamount needed to mate Rabbit(Farmer)", 10);
            Config["Resourceamount needed to mate Duck(Farmer)"] = AmountToMateDuck = GetConfig("Resourceamount needed to mate Duck(Farmer)", 10);
            Config["Resourceamount needed to mate Wolf(Farmer)"] = AmountToMateWolf = GetConfig("Resourceamount needed to mate Wolf(Farmer)", 25);
            Config["Resourceamount needed to mate Grizzly(Farmer)"] = AmountToMateGrizzly = GetConfig("Resourceamount needed to mate Grizzly(Farmer)", 25);
            Config["Resourceamount needed to mate Werewolf(Farmer)"] = AmountToMateWerewolf = GetConfig("Resourceamount needed to mate Werewolf(Farmer)", 50);
            Config["Resourceamount needed to mate Sheep(Farmer)"] = AmountToMateSheep = GetConfig("Resourceamount needed to mate Sheep(Farmer)", 10);

            Config["Chance to mate Chicken(Farmer)"] = FarmerChanceToMateChicken = GetConfig("Chance to mate Chicken(Farmer)", 40);
            Config["Chance to mate Duck(Farmer)"] = FarmerChanceToMateDuck = GetConfig("Chance to mate Duck(Farmer)", 40);
            Config["Chance to mate Rabbit(Farmer)"] = FarmerChanceToMateGrizzly = GetConfig("Chance to mate Rabbit(Farmer)", 3);
            Config["Chance to mate Rabbit(Farmer)"] = FarmerChanceToMateRabbit = GetConfig("Chance to mate Rabbit(Farmer)", 75);
            Config["Chance to mate Sheep(Farmer)"] = FarmerChanceToMateSheep = GetConfig("Chance to mate Sheep(Farmer)", 30);
            Config["Chance to mate Wolf(Farmer)"] = FarmerChanceToMateWolf = GetConfig("Chance to mate Wolf(Farmer)", 10);
            Config["Chance to mate Werewolf(Farmer)"] = FarmerChanceToMateWerewolf = GetConfig("Chance to mate Werewolf(Farmer)", 10);
            // Collector Config
            Config["Color for the Colletor-Prefix"] = CollectorCol = GetConfig("Color for the Colletor-Prefix", "[0000FF]");
            Config["Inventory Space for Colletor"] = CollectorInv = GetConfig("Inventory Space for Colletor", 50);
            Config["Percent of Damage a Colletor does"] = CollectorDmg = GetConfig("Percent of Damage a Colletor does", 90f);
            Config["Percent of Damage a Collector takes"] = CollectorDef = GetConfig("Percent of Damage a Collector takes", 75f);
    // Assassin Config
            Config["Color for the Assassin-Prefix"] = AssassinCol = GetConfig("Color for the Assassin-Prefix", "[FF0000]");
            Config["Inventory Space for Assassin"] = AssassinInv = GetConfig("Inventory Space for Assassin", 2);
            Config["Percent of Damage an Assassin does(with daggers and throwing knives)"] = AssassinDmg = GetConfig("Percent of Damage an Assassin does(with daggers and throwing knives)", 200f);
            Config["Percent of Damage an Assassin takes"] = AssassinDef = GetConfig("Percent of Damage an Assassin takes", 125f);

            SaveConfig();
        }

        T GetConfig<T>(string name, T defaultValue) => Config[name] == null ? defaultValue : (T)System.Convert.ChangeType(Config[name], typeof(T));

        #endregion
        #region The Magic
        private void OnPlayerSpawn (PlayerFirstSpawnEvent e)
        {
            LoadLists();
            Dictionary<string, int> Temp = new Dictionary<string, int>();
            if (!e.Player.CurrentCharacter.HasCompletedCreation)
            {
                Temp.Add("Stone Mason", 0);
                Temp.Add("Blacksmith", 0);
                Temp.Add("Soldier", 0);
                Temp.Add("Archer", 0);
                Temp.Add("Carpenter", 0);
                Temp.Add("Farmer", 0);
                Temp.Add("Colletor", 0);
                Temp.Add("Assassin", 0);
                _ProfessionLevels.Add(e.Player.Id, Temp);
                SaveLists();
                ShowDescriptions(e.Player);
               // e.Player.ShowInputPopup(string.Format(GetMessage("Title", e.Player.Id.ToString())), string.Format(GetMessage("NewOnServer", e.Player.Id.ToString()), StoneMasonCol, BlacksmithCol, SoldierCol, ArcherCol, CarpenterCol, FarmerCol, CollectorCol, AssassinCol) + string.Format(GetMessage("Choose", e.Player.Id.ToString()), ResourceAmountToChange.ToString(), ResourceToChangeProfession.ToString()), "", "ok", "cancel", (selection, dialogue, data) => ChooseProfession(e.Player, selection, dialogue, data));
            }
            if (HasProfession(e.Player) && AddPrefix)
            {
                ProfessionString(e.Player);
                AddPrefixColor(ProfessionString(e.Player));
                if (e.Player.DisplayNameFormat.Contains(ReturnPrefix(ProfessionString(e.Player)))) return;
                e.Player.DisplayNameFormat = AddPrefixColor(ProfessionString(e.Player)) + ReturnPrefix(ProfessionString(e.Player)) + "[FFFFFF]" + e.Player.DisplayName;
            }
        }
        void OnPlayerSpawn(PlayerRespawnRandomlyEvent e)
        {
            LoadLists();
            e.Player.GetInventory().MaximumSlots = GetInvSpaceFromString(ReturnProfessionFromPlayerId(e.Player.Id));
            if (e.Player.DisplayNameFormat.Contains(ReturnPrefix(ProfessionString(e.Player)))) return;
            e.Player.DisplayNameFormat = AddPrefixColor(ProfessionString(e.Player)) + ReturnPrefix(ProfessionString(e.Player)) + "[FFFFFF]" + e.Player.DisplayName;
        }
        void OnPlayerRespawn(PlayerRespawnEvent e)
        {
            LoadLists();
            e.Player.GetInventory().MaximumSlots = GetInvSpaceFromString(ReturnProfessionFromPlayerId(e.Player.Id));
            if (e.Player.DisplayNameFormat.Contains(ReturnPrefix(ProfessionString(e.Player)))) return;
            e.Player.DisplayNameFormat = AddPrefixColor(ProfessionString(e.Player)) + ReturnPrefix(ProfessionString(e.Player)) + "[FFFFFF]" + e.Player.DisplayName;
        }
        void OnPlayerRespawn(PlayerRespawnAtBaseEvent e)
        {
            LoadLists();
            e.Player.GetInventory().MaximumSlots = GetInvSpaceFromString(ReturnProfessionFromPlayerId(e.Player.Id));
            if (e.Player.DisplayNameFormat.Contains(ReturnPrefix(ProfessionString(e.Player)))) return;
            e.Player.DisplayNameFormat = AddPrefixColor(ProfessionString(e.Player)) + ReturnPrefix(ProfessionString(e.Player)) + "[FFFFFF]" + e.Player.DisplayName;
        }
        void OnPlayerRespawn(PlayerRespawnAtBedEvent e)
        {
            LoadLists();
            e.Player.GetInventory().MaximumSlots = GetInvSpaceFromString(ReturnProfessionFromPlayerId(e.Player.Id));
            if (e.Player.DisplayNameFormat.Contains(ReturnPrefix(ProfessionString(e.Player)))) return;
            e.Player.DisplayNameFormat = AddPrefixColor(ProfessionString(e.Player)) + ReturnPrefix(ProfessionString(e.Player)) + "[FFFFFF]" + e.Player.DisplayName;
        }
        private void ChooseProfession(Player player, Options selection, Dialogue dialogue, object contextData)
        {
            string typedProfessionString = dialogue.ValueMessage.ToString();
            var PlayerId = player.Id;
            if (selection == Options.OK)
            {
                if (ReturnProperProfession(typedProfessionString) == "")
                {
                    PrintToChat(player, string.Format(GetMessage("NotExistingProfession", player.Id.ToString())));
                    return;
                }
                if (_ProfessionList.ContainsKey(player.Id))
                {
                    _ProfessionList.Remove(player.Id);
                    SaveLists();
                }
                _ProfessionList.Add(PlayerId, ReturnProperProfession(typedProfessionString));
                SaveLists();
                player.ClearInventory();
                var inventory = player.GetInventory();
                inventory.MaximumSlots = GetInvSpaceFromString(typedProfessionString);
                inventory.Contents.SetMaxSlotCount(inventory.MaximumSlots);
                if (AddPrefix)
                {
                    if (!player.CurrentCharacter.HasCompletedCreation)
                    {
                        player.DisplayNameFormat = AddPrefixColor(ReturnProperProfession(typedProfessionString)) + ReturnPrefix(ReturnProperProfession(typedProfessionString)) + "[FFFFFF]" + "%name%";
                        return;
                    }
                    player.DisplayNameFormat = "%name%";
                    player.DisplayNameFormat = AddPrefixColor(ReturnProperProfession(typedProfessionString)) + ReturnPrefix(ReturnProperProfession(typedProfessionString)) + "[FFFFFF]" + "%name%";
                    return;
                }
                return;
            }
            if (!player.CurrentCharacter.HasCompletedCreation)
            {
                PrintToChat(player, string.Format(GetMessage("You have cancelled to choose a profession, you will have only 1 inventoryslot! to choose your profession later, type /changeprofession")));
            }
            PrintToChat(player, "You have cancelled your profession change");
            return;
        }
        [ChatCommand("professions")]
        void ShowDescriptions (Player player)
        {
            player.ShowPopup(string.Format(GetMessage("DescTitle")), string.Format(GetMessage("StoneMasonDesc",player.Id.ToString()),StoneMasonInv.ToString(), StoneMasonDmg.ToString(), StoneMasonDef.ToString()) + string.Format(GetMessage("BlacksmithDesc", player.Id.ToString()),BlacksmithInv.ToString(), BlacksmithDmg.ToString(), BlacksmithDef.ToString()) + string.Format(GetMessage("SoldierDesc", player.Id.ToString()), SoldierInv.ToString(), SoldierDmg.ToString(), SoldierDef.ToString()) + string.Format(GetMessage("ArcherDesc", player.Id.ToString()), ArcherInv.ToString(), ArcherDmg.ToString(), ArcherDef.ToString()) , "OK");
            player.ShowPopup(string.Format(GetMessage("DescTitle")), string.Format(GetMessage("CarpenterDesc", player.Id.ToString()), CarpenterInv.ToString(), CarpenterDmg.ToString(), CarpenterDef.ToString()) + string.Format(GetMessage("FarmerDesc", player.Id.ToString()), FarmerInv.ToString(), FarmerDmg.ToString(), FarmerDef.ToString()) + string.Format(GetMessage("CollectorDesc", player.Id.ToString()), CollectorInv.ToString(), CollectorDmg.ToString(), CollectorDef.ToString()) + string.Format(GetMessage("AssassinDesc", player.Id.ToString()), AssassinInv.ToString(), AssassinDmg.ToString(), AssassinDef.ToString()), "OK");
            player.ShowPopup(string.Format(GetMessage("LooseInventoryTitle")), string.Format(GetMessage("LooseInventory", player.Id.ToString())));
        }
        [ChatCommand("changeprofession")]
        private void ProfessionChange (Player player)
        {
            LoadLists();
            if (!HasProfession(player))
            {
                ShowDescriptions(player);
                player.ShowInputPopup(string.Format(GetMessage("Title", player.Id.ToString())), string.Format(GetMessage("NewOnServer", player.Id.ToString()), StoneMasonCol, BlacksmithCol, SoldierCol, ArcherCol, CarpenterCol, FarmerCol, CollectorCol, AssassinCol) + string.Format(GetMessage("Choose", player.Id.ToString()), ResourceAmountToChange.ToString(), ResourceToChangeProfession.ToString()), "", "ok", "cancel", (selection, dialogue, data) => ChooseProfession(player, selection, dialogue, data));
                return;
            }
            if (!CanRemoveResource(player,ResourceToChangeProfession,ResourceAmountToChange))
            {
                PrintToChat(player, string.Format(GetMessage("NotEnough", player.Id.ToString()), ResourceToChangeProfession));
                return;
            }
            player.ShowInputPopup(string.Format(GetMessage("Title", player.Id.ToString())), string.Format(GetMessage("Change", player.Id.ToString()), StoneMasonCol, BlacksmithCol, SoldierCol, ArcherCol, CarpenterCol, FarmerCol, CollectorCol, AssassinCol) + string.Format(GetMessage("Choose", player.Id.ToString()), ResourceAmountToChange.ToString(), ResourceToChangeProfession.ToString()), "", "ok", "cancel", (selection, dialogue, data) => ChooseProfession(player, selection, dialogue, data));
            return;
        }
        private void OnEntityHealthChange(EntityDamageEvent e)
        {
            if (e.Damage.DamageSource == null || !e.Damage.DamageSource.IsPlayer) return;
            if (!HasProfession(e.Damage.DamageSource.Owner))
            {
                if (KillViolator == true)
                {
                    e.Cancel();
                    e.Damage.DamageSource.Owner.Kill();
                    PrintToChat(e.Damage.DamageSource.Owner,string.Format(GetMessage("KillViolator", e.Damage.DamageSource.Owner.Id.ToString())));
                }
                e.Cancel();
                PrintToChat(e.Damage.DamageSource.Owner, string.Format(GetMessage("PreventDamageViolator", e.Damage.DamageSource.Owner.Id.ToString())));
            }
            var crestScheme = SocialAPI.Get<CrestScheme>();
            if (e.Damage.DamageSource.IsPlayer && e.Entity.IsPlayer)
            {
                e.Damage.Amount = e.Damage.Amount / 100 * ReturnDamageModifier(e.Damage.DamageSource.OwnerId, e.Damage.Damager.name) / 100 * ReturnDefenseModifier(e.Entity.OwnerId);
            }
            if (e.Damage.DamageSource.IsPlayer &&  ProfessionString(e.Damage.DamageSource.Owner) == "farmer" && !e.Entity.IsPlayer)
            {
                if (!e.Damage.Damager.name.ToLower().Contains("stick") && !e.Damage.Damager.name.ToLower().Contains("whip"))
                {
                    PrintToChat(e.Damage.DamageSource.Owner, string.Format(GetMessage("StickOrWhip", e.Damage.DamageSource.Owner.Id.ToString())));
                    return;
                }
                if (crestScheme.GetCrestAt(e.Entity.Position) != null)
                {
                    PrintToChat(e.Damage.DamageSource.Owner, string.Format(GetMessage("NoCrestedTaming", e.Damage.DamageSource.Owner.Id.ToString())));
                }
                TameAnimal(e.Damage.DamageSource.Owner, e.Entity);
            }
            return;
        }
        void OnItemCraft(ItemCrafterStartEvent e)
        {
            LoadLists();
            float[] Area = new float[4];
            Area[0] = (e.Entity.Position.x + 10f);
            Area[1] = (e.Entity.Position.z + 10f);
            Area[2] = (e.Entity.Position.x - 10f);
            Area[3] = (e.Entity.Position.z - 10f);
            Player PlayerWhoOrderedCraft = CheckAroundStation(Area);
            if (PlayerWhoOrderedCraft == Server.GetPlayerByName("Server")) return;
            if (!PlayerCanCraftItem(PlayerWhoOrderedCraft,e.Crafter.Product.GetNameKey()))
            {
                PrintToChat(PlayerWhoOrderedCraft, string.Format(GetMessage("WrongProfession",PlayerWhoOrderedCraft.Id.ToString()), AddPrefixColor(ProfessionString(PlayerWhoOrderedCraft)) + ReturnProperProfession(ProfessionString(PlayerWhoOrderedCraft)) , e.Crafter.Product.Name));
                e.Crafter.Cancel();
                return;
            }
            return;
        }
        void TameAnimal (Player player, Entity animal)
        {
            LoadLists();
            System.Random RNG = new System.Random();
            int sheep = RNG.Next(1, 100);
            int chicken = RNG.Next(1, 100);
            int rooster = RNG.Next(1, 100);
            int rabbit = RNG.Next(1, 100);
            int wolf = RNG.Next(1, 100);
            int bear = RNG.Next(1, 100);
            int duck = RNG.Next(1, 100);
            int werewolf = RNG.Next(1, 100);
            if (animal.name.ToLower().Contains("grizzly") && bear <= FarmerTameChanceBear)
            {
                GiveItem(player, "Grizzly Bear");
                animal.Position = new UnityEngine.Vector3(-10000, -10000, -10000);
            }
            if (animal.name.ToLower().Contains("chicken") && chicken <= FarmerTameChanceChicken)
            {
                GiveItem(player, "Chicken");
                animal.Position = new UnityEngine.Vector3(-10000, -10000, -10000);
            }
            if (animal.name.ToLower().Contains("sheep") && sheep <= FarmerTameChanceSheep)
            {
                GiveItem(player, "Sheep");
                animal.Position = new UnityEngine.Vector3(-10000, -10000, -10000);
            }
            if (animal.name.ToLower().Contains("rooster") && rooster <= FarmerTameChanceRooster)
            {
                GiveItem(player, "Rooster");
                animal.Position = new UnityEngine.Vector3(-10000, -10000, -10000);
            }
            if (animal.name.ToLower().Contains("rabbit") && rabbit <= FarmerTameChanceRabbit)
            {
                GiveItem(player, "Rabbit");
                animal.Position = new UnityEngine.Vector3(-10000, -10000, -10000);
            }
            if (animal.name.ToLower().Contains("duck") && duck <= FarmerTameChanceDuck)
            {
                GiveItem(player, "Duck");
                animal.Position = new UnityEngine.Vector3(-10000, -10000, -10000);
            }
            if (animal.name.Contains("Wolf") && wolf <= FarmerTameChanceWolf)
            {
                GiveItem(player, "Wolf");
                animal.Position = new UnityEngine.Vector3(-10000, -10000, -10000);
            }
            if (animal.name.ToLower().Contains("were") && werewolf <= FarmerTameChanceWerewolf)
            {
                GiveItem(player, "Werewolf");
                animal.Position = new UnityEngine.Vector3(-10000, -10000, -10000);
            }
        }
        [ChatCommand("collectanimals")]
        private void CollectAllAnimalsOnTerritory(Player player, string cmd)
        {
            LoadLists();
            if (ProfessionString(player) != "farmer")
            {
                PrintToChat(player, string.Format(GetMessage("NoFarmer",player.Id.ToString())));
                return;
            }
            List<Entity> _globalEntList = new List<Entity>();
            _globalEntList = Entity.GetAll();
            var crestScheme = SocialAPI.Get<CrestScheme>();

            if (crestScheme.GetCrestAt(player.Entity.Position) == null || crestScheme.GetCrestAt(player.Entity.Position).GuildName != PlayerExtensions.GetGuild(player).Name)
            {
                PrintToChat(player, string.Format(GetMessage("OnlyCollectOnGuildTerritory", player.Id.ToString())));
                return;
            }
            foreach (Entity ent in _globalEntList)
            {
                if (!ent.name.ToLower().Contains("sheep") && !ent.name.Contains("Wolf") && !ent.name.ToLower().Contains("duck") && !ent.name.ToLower().Contains("chicken") && !ent.name.ToLower().Contains("rooster") && !ent.name.ToLower().Contains("rabbit")) continue;
                if (crestScheme.GetCrestAt(ent.Position) == null || crestScheme.GetCrestAt(ent.Position).GuildName != PlayerExtensions.GetGuild(player).Name) continue;
                if (ent.TryGet<IHealth>().CurrentHealth == 0) continue;
                string entname = ent.name.Replace("[Entity] ", "");
                string id = InvDefinitions.Instance.Blueprints.GetBlueprintForName(entname).Name;
                GiveItem(player, id);
                ent.Position = new UnityEngine.Vector3(-1000, -1000, -1000);
            }
        }
        [ChatCommand("harvestanimals")]
        void HarvestAnimals (Player player)
        {
            LoadLists();
            foreach (ulong playerid in HarvestedSinceLastRestart)
            {
                if (playerid == player.Id)
                {
                    PrintToChat(player, string.Format(GetMessage("OnHarvestTimeout", player.Id.ToString())));
                    return;
                }
            }
            int sheepcount = 0;
            int chickencount = 0;

            List<Entity> _globalEntList = new List<Entity>();
            _globalEntList = Entity.GetAll();
            var crestScheme = SocialAPI.Get<CrestScheme>();
            if (crestScheme.GetCrestAt(player.Entity.Position) == null || crestScheme.GetCrestAt(player.Entity.Position).GuildName != PlayerExtensions.GetGuild(player).Name)
            {
                PrintToChat(player, string.Format(GetMessage("OnlyHarvestOnGuildTerritory", player.Id.ToString())));
                return;
            }
            foreach (Entity entity in _globalEntList)
            {
                if (crestScheme.GetCrestAt(entity.Position) == null) continue;
                if (crestScheme.GetCrestAt(entity.Position).GuildName == PlayerExtensions.GetGuild(player).Name)
                {
                    if (entity.name.ToLower().Contains("sheep")) sheepcount++;
                    if (entity.name.ToLower().Contains("chicken")) chickencount++;
                    if (entity.name.ToLower().Contains("duck")) chickencount++;
                    if (entity.name.ToLower().Contains("rooster")) chickencount++;
                }
            }
            if (sheepcount == 0 && chickencount == 0)
            {
                PrintToChat(player, string.Format(GetMessage("NoHarvestableAnimals", player.Id.ToString())));
            }
            if (sheepcount >0) sheepcount = sheepcount * FarmerHarvestAmount;
            if (chickencount > 0) chickencount = chickencount * FarmerHarvestAmount;
            var inventory = player.GetInventory();
            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("wool", true, true);
            var invGameItemStack = new InvGameItemStack(blueprintForName, sheepcount, null);
            ItemCollection.AutoMergeAdd(inventory.Contents, invGameItemStack);
            var blueprintForName1 = InvDefinitions.Instance.Blueprints.GetBlueprintForName("feather", true, true);
            var invGameItemStack1 = new InvGameItemStack(blueprintForName1, chickencount, null);
            ItemCollection.AutoMergeAdd(inventory.Contents, invGameItemStack1);
            PrintToChat(player, string.Format(GetMessage("Harvested",player.Id.ToString()),sheepcount.ToString(), blueprintForName.Name, chickencount.ToString(), blueprintForName1.Name));
            HarvestedSinceLastRestart.Add(player.Id);
        }
        [ChatCommand("mate")]
        void BreedAnimals(Player player, string cmd, string[] arg)
        {
            LoadLists();
            if (SocialAPI.Get<CrestScheme>().GetCrestAt(player.Entity.Position) == null || SocialAPI.Get<CrestScheme>().GetCrestAt(player.Entity.Position).GuildName != PlayerExtensions.GetGuild(player).Name)
            {
                PrintToChat(player, string.Format(GetMessage("OnlyMateOnTerritory", player.Id.ToString())));
                return;
            }
            string argument = string.Concat(arg);
            if (argument.ToLower() == "chicken") { MateChicken(player); return; }
            if (argument.ToLower() == "duck") { MateDucks(player); return; }
            if (argument.ToLower() == "grizzly") { MateGrizzly(player); return; }
            if (argument.ToLower() == "rabbit") { MateRabbits(player); return; }
            if (argument.ToLower() == "sheep") { MateSheep(player); return; } 
            if (argument.ToLower() == "wolf") { MateWolves(player); return; }
            if (argument.ToLower() == "werewolf") { MateWerewolves(player); return; }
            PrintToChat(player, string.Format(GetMessage("WrongMatingArgs", player.Id.ToString())));
            return;
        }
        // Creating the RNG dirextly in the Class
        static System.Random RNG = new System.Random();
        void MateChicken(Player player)
        {
            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Chicken", true, true);
            string animalstring = blueprintForName.Name;
            if (MatedChickenSinceLastRestart.Contains(player.Id))
            {
                PrintToChat(player, string.Format(GetMessage("OnMateTimeout", player.Id.ToString())));
                return;
            }
            //Initializing the RNG in the Method
            int successorfailure;
            int chickenorrooster;
            int biteordie;       
            int chickencount = 0;
            int roostercount = 0;
            int attemptstomate = 0;
            foreach (Entity ent in Entity.GetAll())
            {
                if (isInArea(ent, GenerateAreaAroundPlayer(player, 10)))
                {
                    if (ent.name.ToLower().Contains("chicken")) chickencount++;
                    if (ent.name.ToLower().Contains("rooster")) roostercount++;
                }
            }
            if (roostercount == 0 || chickencount ==0)
            {
                PrintToChat(player, string.Format(GetMessage("NoMatableAnimals", player.Id.ToString())));
                return;
            }
            if (roostercount < chickencount) attemptstomate = roostercount;
            else attemptstomate = chickencount;
            if (!CanRemoveResource(player, FarmerResourceToMateChicken , AmountToMateChicken * attemptstomate))
            {
                PrintToChat(player, string.Format(GetMessage("ResourcesToMate",player.Id.ToString()),(AmountToMateChicken * attemptstomate).ToString(), FarmerResourceToMateChicken, attemptstomate.ToString() , animalstring));
                return;
            }
            RemoveItemsFromInventory(player, FarmerResourceToMateChicken, AmountToMateChicken * attemptstomate);
            while (attemptstomate>0)
            {
                
                //calling the RNG in the Loop
                successorfailure = RNG.Next(1, 100);
                chickenorrooster = RNG.Next(1, 100);
                biteordie = RNG.Next(1, 100);
                if (FarmerChanceToMateChicken >= successorfailure)
                {
                    if (chickenorrooster <= 50)
                    {
                        GiveItem(player, "Chicken");
                        
                        attemptstomate--;
                        PrintToChat(player, string.Format(GetMessage("MateSuccess", player.Id.ToString()), animalstring));
                        continue;
                    }
                    else
                    {
                        GiveItem(player, "Rooster");
                        attemptstomate--;
                        PrintToChat(player, string.Format(GetMessage("MateSuccess", player.Id.ToString()), animalstring));
                        continue;
                    }
                }
                if (biteordie <= 50)
                {
                    PrintToChat(player, string.Format(GetMessage("MateFailBite",player.Id.ToString()),animalstring));
                    Bite(player);
                    attemptstomate--;
                    continue;
                }
                else
                {
                    PrintToChat(player, string.Format(GetMessage("MateFailDie",player.Id.ToString()),animalstring));
                    GiveItem(player, "Bone");
                    GiveItem(player, "Feather");
                    attemptstomate--;
                    continue;
                }
            }
            MatedChickenSinceLastRestart.Add(player.Id);
        }
        void MateDucks(Player player)
        {
            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Duck", true, true);
            string animalstring = blueprintForName.Name;
            if (MatedDuckSinceLastRestart.Contains(player.Id))
            {
                PrintToChat(player, string.Format(GetMessage("OnMateTimeout", player.Id.ToString())));
                return;
            }
            //Initializing the RNG in the Method
            int successorfailure;
            int biteordie;
            int duckcount = 0;
            int attemptstomate = 0;
            foreach (Entity ent in Entity.GetAll())
            {
                if (isInArea(ent, GenerateAreaAroundPlayer(player, 10)))
                {
                    if (ent.name.ToLower().Contains("duck")) duckcount++;
                }
            }
            if (duckcount/2 <1)
            {
                PrintToChat(player, string.Format(GetMessage("NoMatableAnimals", player.Id.ToString())));
                return;
            }
            attemptstomate = Math.DivRem(duckcount, 2, out duckcount);
            if (!CanRemoveResource(player, FarmerResourceToMateDuck, AmountToMateDuck * attemptstomate))
            {
                PrintToChat(player, string.Format(GetMessage("ResourcesToMate", player.Id.ToString()), (AmountToMateDuck * attemptstomate).ToString(), FarmerResourceToMateDuck, attemptstomate.ToString(), animalstring));
                return;
            }
            RemoveItemsFromInventory(player, FarmerResourceToMateDuck, AmountToMateDuck * attemptstomate);
            while (attemptstomate > 0)
            {
                //calling the RNG in the Loop
                successorfailure = RNG.Next(1, 100);
                biteordie = RNG.Next(1, 100);
                if (FarmerChanceToMateDuck >= successorfailure)
                {
                        GiveItem(player, "Duck");
                        attemptstomate--;
                    PrintToChat(player, string.Format(GetMessage("MateSuccess", player.Id.ToString()), animalstring));
                    continue;
                }
                if (biteordie <= 50)
                {
                    PrintToChat(player, string.Format(GetMessage("MateFailBite", player.Id.ToString()), animalstring));
                    Bite(player);
                    attemptstomate--;
                    continue;
                }
                else
                {
                    PrintToChat(player, string.Format(GetMessage("MateFailDie", player.Id.ToString()), animalstring));
                    GiveItem(player, "Duck Feet");
                    GiveItem(player, "Feather");
                    attemptstomate--;
                    continue;
                }
            }
            MatedDuckSinceLastRestart.Add(player.Id);
        }
        void MateGrizzly(Player player)
        {
            string Message = "";
            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Grizzly Bear", true, true);
            string animalstring = blueprintForName.Name;
            if (MatedGrizzlySinceLastRestart.Contains(player.Id))
            {
                PrintToChat(player, string.Format(GetMessage("OnMateTimeout", player.Id.ToString())));
                return;
            }
            //Initializing the RNG in the Method
            int successorfailure;
            int biteordie;
            int grizzlycount = 0;
            int attemptstomate = 0;
            foreach (Entity ent in Entity.GetAll())
            {
                if (isInArea(ent, GenerateAreaAroundPlayer(player, 10)))
                {
                    if (ent.name.ToLower().Contains("grizzly")) grizzlycount++;
                }
            }
            if (grizzlycount / 2 < 1)
            {
                PrintToChat(player, string.Format(GetMessage("NoMatableAnimals", player.Id.ToString())));
                return;
            }
            attemptstomate = Math.DivRem(grizzlycount, 2, out grizzlycount);
            if (!CanRemoveResource(player, FarmerResourcesToMateGrizzly0, AmountToMateGrizzly * attemptstomate) || !CanRemoveResource(player, FarmerResourcesToMateGrizzly1, AmountToMateGrizzly * attemptstomate) || !CanRemoveResource(player, FarmerResourcesToMateGrizzly2, AmountToMateGrizzly * attemptstomate))
            {
                if(CanRemoveResource(player, FarmerResourcesToMateGrizzly0, AmountToMateGrizzly * attemptstomate))
                {
                    Message = Message + string.Format(GetMessage("ResourcesToMate", player.Id.ToString()), (AmountToMateGrizzly * attemptstomate).ToString(), FarmerResourcesToMateGrizzly0, attemptstomate.ToString(), animalstring) + "\n";
                }
                if (CanRemoveResource(player, FarmerResourcesToMateGrizzly1, AmountToMateGrizzly * attemptstomate))
                {
                    Message = Message + string.Format(GetMessage("ResourcesToMate", player.Id.ToString()), (AmountToMateGrizzly * attemptstomate).ToString(), FarmerResourcesToMateGrizzly1, attemptstomate.ToString(), animalstring) + "\n";
                }
                if (CanRemoveResource(player, FarmerResourcesToMateGrizzly2, AmountToMateGrizzly * attemptstomate))
                {
                    Message = Message + string.Format(GetMessage("ResourcesToMate", player.Id.ToString()), (AmountToMateGrizzly * attemptstomate).ToString(), FarmerResourcesToMateGrizzly2, attemptstomate.ToString(), animalstring) + "\n";
                }
                PrintToChat(player, Message);
                return;
            }
            if (CanRemoveResource(player, FarmerResourcesToMateGrizzly0, AmountToMateGrizzly * attemptstomate) && CanRemoveResource(player, FarmerResourcesToMateGrizzly1, AmountToMateGrizzly * attemptstomate) && CanRemoveResource(player, FarmerResourcesToMateGrizzly2, AmountToMateGrizzly * attemptstomate))
            {
                RemoveItemsFromInventory(player, FarmerResourcesToMateGrizzly0, AmountToMateGrizzly * attemptstomate);
                RemoveItemsFromInventory(player, FarmerResourcesToMateGrizzly1, AmountToMateGrizzly * attemptstomate);
                RemoveItemsFromInventory(player, FarmerResourcesToMateGrizzly2, AmountToMateGrizzly * attemptstomate);
                while (attemptstomate > 0)
                {
                    //calling the RNG in the Loop
                    successorfailure = RNG.Next(1, 100);
                    biteordie = RNG.Next(1, 100);
                    if (FarmerChanceToMateGrizzly >= successorfailure)
                    {
                        GiveItem(player, "Grizzly Bear");
                        attemptstomate--;
                        PrintToChat(player, string.Format(GetMessage("MateSuccess", player.Id.ToString()), animalstring));
                        continue;
                    }
                    if (biteordie <= 50)
                    {
                        PrintToChat(player, string.Format(GetMessage("MateFailBite", player.Id.ToString()), animalstring));
                        Bite(player);
                        attemptstomate--;
                        continue;
                    }
                    else
                    {
                        PrintToChat(player, string.Format(GetMessage("MateFailDie", player.Id.ToString()), animalstring));
                        GiveItem(player, "Meat");
                        GiveItem(player, "Fat");
                        GiveItem(player, "Bear Pelt");
                        GiveItem(player, "Blood");
                        attemptstomate--;
                        continue;
                    }
                }
                MatedGrizzlySinceLastRestart.Add(player.Id);
            }
        }
        void MateRabbits(Player player)
        {
            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Rabbit", true, true);
            string animalstring = blueprintForName.Name;
            if (MatedRabbitsSinceLastRestart.Contains(player.Id))
            {
                PrintToChat(player, string.Format(GetMessage("OnMateTimeout", player.Id.ToString())));
                return;
            }
            //Initializing the RNG in the Method
            int successorfailure;
            int biteordie;
            int rabbitcount = 0;
            int attemptstomate = 0;
            foreach (Entity ent in Entity.GetAll())
            {
                if (isInArea(ent, GenerateAreaAroundPlayer(player, 10)))
                {
                    if (ent.name.ToLower().Contains("rabbit")) rabbitcount++;
                }
            }
            if (rabbitcount / 2 < 1)
            {
                PrintToChat(player, string.Format(GetMessage("NoMatableAnimals", player.Id.ToString())));
                return;
            }
            attemptstomate = Math.DivRem(rabbitcount, 2, out rabbitcount);
            if (CanRemoveResource(player, FarmerResourceToMateRabbit, AmountToMateRabbit * attemptstomate))
            {
                RemoveItemsFromInventory(player, FarmerResourceToMateRabbit, AmountToMateRabbit * attemptstomate);
                while (attemptstomate > 0)
                {
                    //calling the RNG in the Loop
                    successorfailure = RNG.Next(1, 100);
                    biteordie = RNG.Next(1, 100);
                    if (FarmerChanceToMateRabbit >= successorfailure)
                    {
                        GiveItem(player, "Rabbit");
                        attemptstomate--;
                        PrintToChat(player, string.Format(GetMessage("MateSuccess", player.Id.ToString()), animalstring));
                        continue;
                    }
                    if (biteordie <= 50)
                    {
                        PrintToChat(player, string.Format(GetMessage("MateFailBite", player.Id.ToString()), animalstring));
                        Bite(player);
                        attemptstomate--;
                        continue;
                    }
                    else
                    {
                        PrintToChat(player, string.Format(GetMessage("MateFailDie", player.Id.ToString()), animalstring));
                        GiveItem(player, "Meat");
                        GiveItem(player, "Blood");
                        attemptstomate--;
                        continue;
                    }
                }
                MatedRabbitsSinceLastRestart.Add(player.Id);
            }
            else
            {
                PrintToChat(player, string.Format(GetMessage("ResourcesToMate", player.Id.ToString()), (AmountToMateRabbit * attemptstomate).ToString(), FarmerResourceToMateRabbit, attemptstomate.ToString(), animalstring));
                return;
            }
        }
        void MateSheep(Player player)
        {
            
            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Sheep", true, true);
            string animalstring = blueprintForName.Name;
            if (MatedSheepSinceLastRestart.Contains(player.Id))
            {
                PrintToChat(player, string.Format(GetMessage("OnMateTimeout", player.Id.ToString())));
                return;
            }
            //Initializing the RNG in the Method
            int successorfailure;
            int biteordie;
            int sheepcount = 0;
            int attemptstomate = 0;
            foreach (Entity ent in Entity.GetAll())
            {
                if (isInArea(ent, GenerateAreaAroundPlayer(player, 10)))
                {
                    if (ent.name.ToLower().Contains("sheep")) sheepcount++;
                }
            }
            if (sheepcount / 2 < 1)
            {
                PrintToChat(player, string.Format(GetMessage("NoMatableAnimals", player.Id.ToString())));
                return;
            }
            attemptstomate = Math.DivRem(sheepcount, 2, out sheepcount);
            if (CanRemoveResource(player, FarmerResourceToMateSheep, AmountToMateSheep * attemptstomate))
            {
                RemoveItemsFromInventory(player, FarmerResourceToMateSheep, AmountToMateSheep * attemptstomate);
                while (attemptstomate > 0)
                {
                    //calling the RNG in the Loop
                    successorfailure = RNG.Next(1, 100);
                    biteordie = RNG.Next(1, 100);
                    if (FarmerChanceToMateSheep >= successorfailure)
                    {
                        GiveItem(player, "Sheep");
                        attemptstomate--;
                        PrintToChat(player, string.Format(GetMessage("MateSuccess", player.Id.ToString()), animalstring));
                        continue;
                    }
                    if (biteordie <= 50)
                    {
                        PrintToChat(player, string.Format(GetMessage("MateFailBite", player.Id.ToString()), animalstring));
                        Bite(player);
                        attemptstomate--;
                        continue;
                    }
                    else
                    {
                        PrintToChat(player, string.Format(GetMessage("MateFailDie", player.Id.ToString()), animalstring));
                        GiveItem(player, "Wool");
                        GiveItem(player, "Blood");
                        attemptstomate--;
                        continue;
                    }
                }
                MatedSheepSinceLastRestart.Add(player.Id);
            }
            else
            {
                PrintToChat(player, string.Format(GetMessage("ResourcesToMate", player.Id.ToString()), (AmountToMateSheep * attemptstomate).ToString(), FarmerResourceToMateSheep, attemptstomate.ToString(), animalstring));
                return;
            }
        }
        void MateWolves(Player player)
        {
            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Wolf", true, true);
            string animalstring = blueprintForName.Name;
            string Message = "";
            if (MatedWolfSinceLastRestart.Contains(player.Id))
            {
                PrintToChat(player, string.Format(GetMessage("OnMateTimeout", player.Id.ToString())));
                return;
            }
            //Initializing the RNG in the Method
            int successorfailure;
            int biteordie;
            int wolfcount = 0;
            int attemptstomate = 0;
            foreach (Entity ent in Entity.GetAll())
            {
                if (isInArea(ent, GenerateAreaAroundPlayer(player, 10)))
                {
                    if (ent.name.Contains("Wolf")) wolfcount++;
                }
            }
            if (wolfcount / 2 < 1)
            {
                PrintToChat(player, string.Format(GetMessage("NoMatableAnimals", player.Id.ToString())));
                return;
            }
            attemptstomate = Math.DivRem(wolfcount, 2, out wolfcount);
            if (!CanRemoveResource(player, FarmerResourcesToMateWolf0, AmountToMateWolf * attemptstomate) || !CanRemoveResource(player, FarmerResourcesToMateWolf1, AmountToMateWolf * attemptstomate))
            {
                if (CanRemoveResource(player, FarmerResourcesToMateWolf0, AmountToMateWolf * attemptstomate))
                {
                    Message = Message + string.Format(GetMessage("ResourcesToMate", player.Id.ToString()), (AmountToMateWolf * attemptstomate).ToString(), FarmerResourcesToMateWolf0, attemptstomate.ToString(), animalstring) + "\n";
                }
                if (CanRemoveResource(player, FarmerResourcesToMateWolf1, AmountToMateWolf * attemptstomate))
                {
                    Message = Message + string.Format(GetMessage("ResourcesToMate", player.Id.ToString()), (AmountToMateWolf * attemptstomate).ToString(), FarmerResourcesToMateWolf1, attemptstomate.ToString(), animalstring) + "\n";
                }
                PrintToChat(player, Message);
                return;
            }
            if (CanRemoveResource(player, FarmerResourcesToMateWolf0, AmountToMateWolf * attemptstomate) && CanRemoveResource(player, FarmerResourcesToMateWolf1, AmountToMateWolf * attemptstomate))
            {
                RemoveItemsFromInventory(player, FarmerResourcesToMateWolf0, AmountToMateWolf * attemptstomate);
                RemoveItemsFromInventory(player, FarmerResourcesToMateWolf1, AmountToMateWolf * attemptstomate);
                while (attemptstomate > 0)
                {
                    //calling the RNG in the Loop
                    successorfailure = RNG.Next(1, 100);
                    biteordie = RNG.Next(1, 100);
                    if (FarmerChanceToMateWolf >= successorfailure)
                    {
                        GiveItem(player, "Wolf");
                        attemptstomate--;
                        PrintToChat(player, string.Format(GetMessage("MateSuccess", player.Id.ToString()), animalstring));
                        continue;
                    }
                    if (biteordie <= 50)
                    {
                        PrintToChat(player, string.Format(GetMessage("MateFailBite", player.Id.ToString()), animalstring));
                        Bite(player);
                        attemptstomate--;
                        continue;
                    }
                    else
                    {
                        PrintToChat(player, string.Format(GetMessage("MateFailDie", player.Id.ToString()), animalstring));
                        GiveItem(player, "Meat");
                        GiveItem(player, "Blood");
                        GiveItem(player, "Bone");
                        GiveItem(player, "Fat");
                        attemptstomate--;
                        continue;
                    }
                }
                MatedWolfSinceLastRestart.Add(player.Id);
            }
        }
        void MateWerewolves(Player player)
        {
            string Message = "";
            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Werewolf", true, true);
            string animalstring = blueprintForName.Name;
            if (MatedWerewolfSinceLastRestart.Contains(player.Id))
            {
                PrintToChat(player, string.Format(GetMessage("OnMateTimeout", player.Id.ToString())));
                return;
            }
            //Initializing the RNG in the Method
            int successorfailure;
            int biteordie;
            int werewolfcount = 0;
            int attemptstomate = 0;
            foreach (Entity ent in Entity.GetAll())
            {
                if (isInArea(ent, GenerateAreaAroundPlayer(player, 10)))
                {
                    if (ent.name.Contains("were")) werewolfcount++;
                }
            }
            if (werewolfcount / 2 < 1)
            {
                PrintToChat(player, string.Format(GetMessage("NoMatableAnimals", player.Id.ToString())));
                return;
            }
            attemptstomate = Math.DivRem(werewolfcount, 2, out werewolfcount);
            if (!CanRemoveResource(player, FarmerResourcesToMateWerewolf0, AmountToMateWerewolf * attemptstomate) || !CanRemoveResource(player, FarmerResourcesToMateWerewolf1, AmountToMateWerewolf * attemptstomate) || !CanRemoveResource(player, FarmerResourcesToMateWerewolf2, AmountToMateWerewolf * attemptstomate) || !CanRemoveResource(player, FarmerResourcesToMateWerewolf3, AmountToMateWerewolf * attemptstomate))
            {
                if (CanRemoveResource(player, FarmerResourcesToMateWerewolf0, AmountToMateWerewolf * attemptstomate))
                {
                    Message = Message + string.Format(GetMessage("ResourcesToMate", player.Id.ToString()), (AmountToMateWerewolf * attemptstomate).ToString(), FarmerResourcesToMateWerewolf0, attemptstomate.ToString(), animalstring) + "\n";
                }
                if (CanRemoveResource(player, FarmerResourcesToMateWerewolf1, AmountToMateWerewolf * attemptstomate))
                {
                    Message = Message + string.Format(GetMessage("ResourcesToMate", player.Id.ToString()), (AmountToMateWerewolf * attemptstomate).ToString(), FarmerResourcesToMateWerewolf1, attemptstomate.ToString(), animalstring) + "\n";
                }
                if (CanRemoveResource(player, FarmerResourcesToMateWerewolf2, AmountToMateWerewolf * attemptstomate))
                {
                    Message = Message + string.Format(GetMessage("ResourcesToMate", player.Id.ToString()), (AmountToMateWerewolf * attemptstomate).ToString(), FarmerResourcesToMateWerewolf2, attemptstomate.ToString(), animalstring) + "\n";
                }
                if (CanRemoveResource(player, FarmerResourcesToMateWerewolf3, AmountToMateWerewolf * attemptstomate))
                {
                    Message = Message + string.Format(GetMessage("ResourcesToMate", player.Id.ToString()), (AmountToMateWerewolf * attemptstomate).ToString(), FarmerResourcesToMateWerewolf3, attemptstomate.ToString(), animalstring) + "\n";
                }
                PrintToChat(player, Message);
                return;
            }
            if (CanRemoveResource(player, FarmerResourcesToMateWerewolf0, AmountToMateWerewolf * attemptstomate) && CanRemoveResource(player, FarmerResourcesToMateWerewolf1, AmountToMateWerewolf * attemptstomate) && CanRemoveResource(player, FarmerResourcesToMateWerewolf2, AmountToMateWerewolf * attemptstomate) && CanRemoveResource(player, FarmerResourcesToMateWerewolf3, AmountToMateWerewolf * attemptstomate))
            {
                RemoveItemsFromInventory(player, FarmerResourcesToMateWerewolf0, AmountToMateWolf * attemptstomate);
                RemoveItemsFromInventory(player, FarmerResourcesToMateWerewolf1, AmountToMateWolf * attemptstomate);
                RemoveItemsFromInventory(player, FarmerResourcesToMateWerewolf2, AmountToMateWolf * attemptstomate);
                RemoveItemsFromInventory(player, FarmerResourcesToMateWerewolf3, AmountToMateWolf * attemptstomate);
                while (attemptstomate > 0)
                {
                    //calling the RNG in the Loop
                    successorfailure = RNG.Next(1, 100);
                    biteordie = RNG.Next(1, 100);
                    if (FarmerChanceToMateWerewolf >= successorfailure)
                    {
                        GiveItem(player, "Werewolf");
                        attemptstomate--;
                        PrintToChat(player, string.Format(GetMessage("MateSuccess", player.Id.ToString()), animalstring));
                        continue;
                    }
                    if (biteordie <= 50)
                    {
                        PrintToChat(player, string.Format(GetMessage("MateFailBite", player.Id.ToString()), animalstring));
                        Bite(player);
                        attemptstomate--;
                        continue;
                    }
                    else
                    {
                        PrintToChat(player, string.Format(GetMessage("MateFailDie", player.Id.ToString()), animalstring));
                        GiveItem(player, "Meat");
                        GiveItem(player, "Blood");
                        GiveItem(player, "Fat");
                        GiveItem(player, "Fang");
                        GiveItem(player, "Diamond");
                        attemptstomate--;
                        continue;
                    }
                }
                MatedWolfSinceLastRestart.Add(player.Id);
            }
        }
        #endregion
        #region Utility
        void GiveItem(Player farmer, string item)
        {

            var inventory = farmer.GetInventory();
            var blueprintForID = InvDefinitions.Instance.Blueprints.GetBlueprintForName(item, true, true);
            var invGameItemStack = new InvGameItemStack(blueprintForID, 1, null);
            ItemCollection.AutoMergeAdd(inventory.Contents, invGameItemStack);
        }
        string ReturnProperProfession (string professionstring)
        {
            if (professionstring.ToLower().Contains("stone")) return "stonemason";
            if (professionstring.ToLower().Contains("blacksmith")) return "blacksmith";
            if (professionstring.ToLower().Contains("soldier")) return "soldier";
            if (professionstring.ToLower().Contains("archer")) return "archer";
            if (professionstring.ToLower().Contains("carpenter")) return "carpenter";
            if (professionstring.ToLower().Contains("farmer")) return "farmer";
            if (professionstring.ToLower().Contains("collector")) return "collector";
            if (professionstring.ToLower().Contains("assassin")) return "assassin";
            return "";
        }
        int GetInvSpaceFromString (string professionstring)
        {
            if (professionstring.ToLower().Contains("stone")) return StoneMasonInv;
            if (professionstring.ToLower().Contains("blacksmith")) return BlacksmithInv;
            if (professionstring.ToLower().Contains("soldier")) return SoldierInv;
            if (professionstring.ToLower().Contains("archer")) return ArcherInv;
            if (professionstring.ToLower().Contains("carpenter")) return CarpenterInv;
            if (professionstring.ToLower().Contains("farmer")) return FarmerInv;
            if (professionstring.ToLower().Contains("collector")) return CollectorInv;
            if (professionstring.ToLower().Contains("assassin")) return AssassinInv;
            return 0;
        }
        string AddPrefixColor(string professionstring)
        {
            if (professionstring.ToLower()=="stonemason") return StoneMasonCol;
            if (professionstring.ToLower()=="blacksmith") return BlacksmithCol;
            if (professionstring.ToLower()=="soldier") return SoldierCol;
            if (professionstring.ToLower()=="archer") return ArcherCol;
            if (professionstring.ToLower()=="carpenter") return CarpenterCol;
            if (professionstring.ToLower()=="farmer") return FarmerCol;
            if (professionstring.ToLower()=="collector") return CollectorCol;
            if (professionstring.ToLower()=="assassin") return AssassinCol;
            return "NOJOB";
        } 
        string ReturnPrefix(string professionstring)
        {
            if (professionstring.ToLower() == "stonemason") return "[Stone-Mason]";
            if (professionstring.ToLower() == "blacksmith") return "[Blacksmith]";
            if (professionstring.ToLower() == "soldier") return "[Soldier]";
            if (professionstring.ToLower() == "archer") return "[Archer]";
            if (professionstring.ToLower() == "carpenter") return "[Carpenter]";
            if (professionstring.ToLower() == "farmer") return "[Farmer]";
            if (professionstring.ToLower() == "collector") return "[Collector]";
            if (professionstring.ToLower() == "assassin") return "[Assassin]";
            return "";
        }
        float ReturnDamageModifier(ulong playerid, string weapon)
        {
            string profession = "";
            foreach (var playerids in _ProfessionList)
            {
                if (playerid != playerids.Key)
                {
                    continue;
                }
                profession = playerids.Value;
            }
            if (profession.ToLower() == "stonemason") return StoneMasonDmg;
            if (profession.ToLower() == "blacksmith") return BlacksmithDmg;
            if (profession.ToLower() == "soldier" && !weapon.ToLower().Contains("throwing") && !weapon.ToLower().Contains("bow")&& !weapon.ToLower().Contains("dagger")) return SoldierDmg;
            if (profession.ToLower() == "archer" && weapon.ToLower().Contains("bow")) return ArcherDmg;
            if (profession.ToLower() == "carpenter") return CarpenterDmg;
            if (profession.ToLower() == "farmer") return FarmerDmg;
            if (profession.ToLower() == "collector") return CollectorDmg;
            if (profession.ToLower() == "assassin" && (weapon.ToLower().Contains("throwing") || weapon.ToLower().Contains("dagger"))) return AssassinDmg;
            return 0f;
        }
        float ReturnDefenseModifier(ulong playerid)
        {
            string profession = "";
            foreach (var playerids in _ProfessionList)
            {
                if (playerid != playerids.Key)
                {
                    continue;
                }
                profession = playerids.Value;
            }
            if (profession.ToLower() == "stonemason") return StoneMasonDef;
            if (profession.ToLower() == "blacksmith") return BlacksmithDef;
            if (profession.ToLower() == "soldier") return SoldierDef;
            if (profession.ToLower() == "archer") return ArcherDef;
            if (profession.ToLower() == "carpenter") return CarpenterDef;
            if (profession.ToLower() == "farmer") return FarmerDef;
            if (profession.ToLower() == "collector") return CollectorDef;
            if (profession.ToLower() == "assassin") return AssassinDef;
            return 0f;
        }
        float[] GenerateAreaAroundPlayer(Player player, float value)
        {
            float[] Area = new float[4];
            Area[0] = (player.Entity.Position.x + value);
            Area[1] = (player.Entity.Position.z + value);
            Area[2] = (player.Entity.Position.x - value);
            Area[3] = (player.Entity.Position.z - value);
            return Area;
        }
        string ReturnProfessionFromPlayerId(ulong playerID)
        {
            foreach (var player in _ProfessionList)
            {
                if (player.Key != playerID) continue;
                return player.Value;
            }
            return "";
        }
        private void DoNothing(Player player, Options selection, Dialogue dialogue, object contextData)
        {
            //Do nothing
        }
        private string Capitalise(string word)
        {
            string finalText = "";
            finalText = char.ToUpper(word[0]).ToString();
            var spaceFound = 0;
            for (var i = 1; i < word.Length; i++)
            {
                if (word[i] == ' ')
                {
                    spaceFound = i + 1;
                }
                if (i == spaceFound)
                {
                    finalText = finalText + char.ToUpper(word[i]).ToString();
                }
                else finalText = finalText + word[i].ToString();
            }
            return finalText;
        }
        private bool CanRemoveResource(Player player, string resource, int amount)
        {
            var inventory = player.CurrentCharacter.Entity.GetContainerOfType(CollectionTypes.Inventory);
            var foundAmount = 0;
            foreach (var item in inventory.Contents.Where(item => item != null))
            {
                if (item.Blueprint.Name == resource)
                {
                    foundAmount = foundAmount + item.StackAmount;
                }
            }
            if (foundAmount >= amount) return true;
            return false;
        }
        public void RemoveItemsFromInventory(Player player, string resource, int amount)
        {
            ItemCollection inventory = player.GetInventory().Contents;
            int removeAmount = 0;
            int amountRemaining = amount;
            foreach (InvGameItemStack item in inventory.Where(item => item != null))
            {
                if (item.Blueprint.Name != resource) continue;
                removeAmount = amountRemaining;
                if (item.StackAmount < removeAmount) removeAmount = item.StackAmount;
                inventory.SplitItem(item, removeAmount);
                amountRemaining = amountRemaining - removeAmount;
            }
        }
        void Bite (Player player)
        {
            Vector3 newForce = new Vector3(0, 0, 0);
            CodeHatch.Damaging.Damage newDmg = new CodeHatch.Damaging.Damage
            {
                Amount = player.GetHealth().CurrentHealth / 100 * 25,
                Damager = player.Entity,
                DamageSource = player.Entity,
                DamageTypes = CodeHatch.Damaging.DamageType.Falling

            };
            //newDmg.Force = newForce;
            //newDmg.ImpactDamage = (0);
            //newDmg.IsFatal = false;
            //newDmg.MiscDamage = newDmg.Amount;
            EntityDamageEvent bite = new EntityDamageEvent(player.Entity,newDmg);
            bite.Damage.DamageTypes = CodeHatch.Damaging.DamageType.Any;
            EventManager.CallEvent((BaseEvent)bite);
        }
        #endregion
        #region Checks
        bool isAbleToBountyHunt(Player player)
        {
            if (ReturnProperProfession(ProfessionString(player)) == "assassin") return true;
            return false;
        }
        bool HasProfession(Player player)
        {
            if (_ProfessionList.Count == 0) return false;
            if (!_ProfessionList.Keys.Contains(player.Id)) return false;
            foreach (KeyValuePair<ulong,string> kvp in _ProfessionList)
            {
                if (kvp.Key != player.Id) continue;

                return true;
            }
            
            return false;
        }
        string ProfessionString(Player player)
        {
            foreach (var players in _ProfessionList)
            {
                if ((players.Key != player.Id))
                {
                    continue;
                }
                return players.Value;
            }
            return "NOJOB";
        }
        bool isCraftableByBlacksmith(string blueprintNameKey)
        {
            if (blueprintNameKey.Contains("Armour.IronArmour")) return true;
            if (blueprintNameKey.Contains("Armour.SteelArmour")) return true;
            if (blueprintNameKey.Contains("BuildingMaterials.Blocks.Tier6.ReinforcedWood(Iron)")) return true;
            if (blueprintNameKey.Contains("Shield.Iron")) return true;
            if (blueprintNameKey.Contains("Shield.Steel")) return true;
            if (blueprintNameKey.Contains("Weapons.Melee.Iron")) return true;
            if (blueprintNameKey.Contains("Weapons.Melee.Steel")) return true;
            if (blueprintNameKey.Contains("Weapons.Projectile.Iron")) return true;
            if (blueprintNameKey.Contains("Weapons.Projectile.Steel")) return true;
            switch (blueprintNameKey)
            {
                case ("Items.AreaProtection.Crests.IronCrest"):
                    return true;
                case ("Items.AreaProtection.Crests.SteelCrest"):
                    return true;
                case ("Items.AreaProtection.Totems.IronTotem"):
                    return true;
                case ("Items.BuildingMaterials.Doors.IronDoor"):
                    return true;
                case ("Items.BuildingMaterials.Gates.IronGate"):
                    return true;
                case ("Items.BuildingMaterials.Windows.IronBarWindow"):
                    return true;
                case ("Items.Containers.IronChest"):
                    return true;
                case ("Items.Containers.SteelChest"):
                    return true;
                case ("Items.Dungeon.LargeIronCage"):
                    return true;
                case ("Items.Dungeon.LargeIronHangingCage"):
                    return true;
                case ("Items.Dungeon.SmallIronCage"):
                    return true;
                case ("Items.Dungeon.SmallIronHangingCage"):
                    return true;
                case ("Items.Dungeon.SteelCage"):
                    return true;
                case ("Items.Equipment.IronShackles"):
                    return true;
                case ("Items.Equipment.Lockpick"):
                    return true;
                case ("Items.Furniture.BellGong"):
                    return true;
                case ("Items.Furniture.LordsBath"):
                    return true;
                case ("Items.Lights.Candle"):
                    return true;
                case ("Items.Lights.CandleStand"):
                    return true;
                case ("Items.Lights.Chandelier"):
                    return true;
                case ("Items.Lights.HangingLantern"):
                    return true;
                case ("Items.Lights.HangingTorch"):
                    return true;
                case ("Items.Lights.IronFloorTorch"):
                    return true;
                case ("Items.Lights.SmallWallLantern"):
                    return true;
                case ("Items.Lights.SmallWallTorch"):
                    return true;
                case ("Items.Lights.StandingIronTorch"):
                    return true;
                case ("Items.Paintable.MediumSteelHangingSign"):
                    return true;
                case ("Items.Paintable.SmallSteelHangingSign"):
                    return true;
                case ("Items.Paintable.SmallSteelSignpost"):
                    return true;
                case ("Items.Paintable.SteelPictureFrame"):
                    return true;
                case ("Items.Resources.SteelCompound"):
                    return true;
                case ("Items.Stations.Anvil"):
                    return true;
                case ("Items.Traps.BladedPillar"):
                    return true;
                case ("Items.Traps.IronBearTrap"):
                    return true;
                case ("Items.Traps.IronSpikes(Hidden)"):
                    return true;
                case ("Items.Traps.IronSpikes"):
                    return true;
                case ("Items.Weapons.HarvestingTool.IronHatchet"):
                    return true;
                case ("Items.Weapons.HarvestingTool.IronPickaxe"):
                    return true;
                case ("Items.Weapons.HarvestingTool.IronWoodCuttersAxe"):
                    return true;
                case ("Items.Weapons.HarvestingTool.Scythe"):
                    return true;
                case ("Items.Weapons.HarvestingTool.SteelHatchet"):
                    return true;
                case ("Items.Weapons.HarvestingTool.SteelPickaxe"):
                    return true;
                case ("Items.Weapons.HarvestingTool.SteelWoodCuttersAxe"):
                    return true;
                case ("Items.Weapons.Melee.ExecutionersAxe"):
                    return true;
                case ("Items.Weapons.Siege.BallistaBolt"):
                    return true;
                case ("Items.Lights.WallLantern"):
                    return true;
                case ("Items.Lights.WallTorch"):
                    return true;
                default: return false;
            }
        }
        bool isCraftableByStoneMason(string blueprintNameKey)
        {
           
            if (blueprintNameKey.Contains("BuildingMaterials.Blocks.Tier5")) return true;
            if (blueprintNameKey.Contains("BuildingMaterials.Blocks.Tier7")) return true;
            if (blueprintNameKey.Contains("Weapons.Melee.Stone")) return true;
            if (blueprintNameKey.Contains("Weapons.Projectile.Stone")) return true;
            switch (blueprintNameKey)
            {
                case ("Items.AreaProtection.Totems.StoneTotem"):
                    return true;
                case ("Items.BuildingMaterials.Doors.StoneArch"):
                    return true;
                case ("Items.BuildingMaterials.Gates.LongWoodDrawbridge"):
                    return true;
                case ("Items.BuildingMaterials.Gates.WoodDrawbridge"):
                    return true;
                case ("Items.BuildingMaterials.Windows.StoneSlitWindow"):
                    return true;
                case ("Items.Lights.GreatFireplace"):
                    return true;
                case ("Items.Lights.GroundTorch"):
                    return true;
                case ("Items.Lights.StoneFireplace"):
                    return true;
                case ("Items.Resources.StoneSlab"):
                    return true;
                case ("Items.Stations.Granary"):
                    return true;
                case ("Items.Stations.Well"):
                    return true;
                case ("Items.Weapons.Siege.TrebuchetStone"):
                    return true;
                default: return false;
            }
        }
        bool isCraftableByCarpenter(string blueprintNameKey)
        {
            if (blueprintNameKey.Contains("Armour.WoodArmour")) return true;
            if (blueprintNameKey.Contains("BuildingMaterials.Blocks.Tier3")) return true;
            if (blueprintNameKey.Contains("BuildingMaterials.Blocks.Tier4")) return true;
            if (blueprintNameKey.Contains("Shield.Wood")) return true;
            if (blueprintNameKey.Contains("Traps.Wood")) return true;
            if (blueprintNameKey.Contains("Weapons.Projectile.Wood")) return true;
            else
            {
                switch (blueprintNameKey)
                {
                    case ("Items.AreaProtection.Totems.WoodTotem"):
                        return true;
                    case ("Items.BuildingMaterials.Defences.DefensiveBarricade"):
                        return true;
                    case ("Items.BuildingMaterials.Defences.LogFence"):
                        return true;
                    case ("Items.BuildingMaterials.Defences.WoodLedge"):
                        return true;
                    case ("Items.BuildingMaterials.Doors.ReinforcedWood(Iron)Door"):
                        return true;
                    case ("Items.BuildingMaterials.Doors.ReinforcedWood(Steel)Door"):
                        return true;
                    case ("Items.BuildingMaterials.Doors.WoodDoor"):
                        return true;
                    case ("Items.BuildingMaterials.Gates.ReinforcedWood(Iron)Gate"):
                        return true;
                    case ("Items.BuildingMaterials.Gates.WoodGate"):
                        return true;
                    case ("Items.BuildingMaterials.Windows.WoodShutters"):
                        return true;
                    case ("Items.Containers.HighQualityCabinet"):
                        return true;
                    case ("Items.Containers.MediumQualityBookcase"):
                        return true;
                    case ("Items.Containers.MediumQualityDresser"):
                        return true;
                    case ("Items.Containers.WoodChest"):
                        return true;
                    case ("Items.Dungeon.Guillotine"):
                        return true;
                    case ("Items.Dungeon.LargeGallows"):
                        return true;
                    case ("Items.Dungeon.Pillory"):
                        return true;
                    case ("Items.Dungeon.SmallGallows"):
                        return true;
                    case ("Items.Dungeon.WoodCage"):
                        return true;
                    case ("Items.Equipment.DjembeDrum"):
                        return true;
                    case ("Items.Equipment.RepairHammer"):
                        return true;
                    case ("Items.Equipment.WoodFlute"):
                        return true;
                    case ("Items.Furniture.ArcheryTarget"):
                        return true;
                    case ("Items.Furniture.BanquetTable"):
                        return true;
                    case ("Items.Furniture.Gazebo"):
                        return true;
                    case ("Items.Furniture.HayBaleTarget"):
                        return true;
                    case ("Items.Furniture.HighQualityBed"):
                        return true;
                    case ("Items.Furniture.LordsBed"):
                        return true;
                    case ("Items.Furniture.LordsLargeChair"):
                        return true;
                    case ("Items.Furniture.LordsSmallChair"):
                        return true;
                    case ("Items.Furniture.LowQualityBed"):
                        return true;
                    case ("Items.Furniture.LowQualityBench"):
                        return true;
                    case ("Items.Furniture.LowQualityChair"):
                        return true;
                    case ("Items.Furniture.LowQualityFence"):
                        return true;
                    case ("Items.Furniture.LowQualityShelf"):
                        return true;
                    case ("Items.Furniture.LowQualityStool"):
                        return true;
                    case ("Items.Furniture.LowQualityTable"):
                        return true;
                    case ("Items.Furniture.MediumQualityBed"):
                        return true;
                    case ("Items.Furniture.MediumQualityBench"):
                        return true;
                    case ("Items.Furniture.MediumQualityChair"):
                        return true;
                    case ("Items.Furniture.MediumQualityStool"):
                        return true;
                    case ("Items.Furniture.MediumQualityTable"):
                        return true;
                    case ("Items.Furniture.RockingHorse"):
                        return true;
                    case ("Items.Paintable.LargeWoodBillboard"):
                        return true;
                    case ("Items.Paintable.MediumStickBillboard"):
                        return true;
                    case ("Items.Paintable.MediumWoodBillboard"):
                        return true;
                    case ("Items.Paintable.SmallStickSignpost"):
                        return true;
                    case ("Items.Paintable.SmallWoodHangingSign"):
                        return true;
                    case ("Items.Paintable.SmallWoodSignpost"):
                        return true;
                    case ("Items.Paintable.WoodPictureFrame"):
                        return true;
                    case ("Items.Stations.Fletcher"):
                        return true;
                    case ("Items.Stations.Sawmill"):
                        return true;
                    case ("Items.Stations.Siegeworks"):
                        return true;
                    case ("Items.Stations.SpinningWheel"):
                        return true;
                    case ("Items.Stations.Woodworking"):
                        return true;
                    case ("Items.Weapons.Melee.WoodMace"):
                        return true;
                    case ("Items.Weapons.Melee.WoodSword"):
                        return true;
                    case ("Items.Weapons.Projectile.Crossbow"):
                        return true;
                    case ("Items.Weapons.Siege.Ballista"):
                        return true;
                    case ("Items.Weapons.Siege.Trebuchet"):
                        return true;
                    default: return false;
                }
            } 
        }
        bool isCraftableByFarmer(string blueprintNameKey)
        {
            if (blueprintNameKey.Contains("Armour.LeatherArmour")) return true;
            switch (blueprintNameKey)
            {
                case ("Items.AreaProtection.Crests.LeatherCrest"):
                    return true;
                case ("Items.Armour.Apparel.Tabard"):
                    return true;
                case ("Items.Consumables.Bandage"):
                    return true;
                case ("Items.Equipment.MediumBanner"):
                    return true;
                case ("Items.Equipment.Rope"):
                    return true;
                case ("Items.Equipment.SmallBanner"):
                    return true;
                case ("Items.Furniture.BearSkinRug"):
                    return true;
                case ("Items.Furniture.DeerHeadTrophy"):
                    return true;
                case ("Items.Weapons.HarvestingTool.WateringPot"):
                    return true;
                case ("Items.Weapons.Melee.Whip"):
                    return true;
                case ("Items.Weapons.Melee.WoodStick"):
                    return true;
                case ("Items.Weapons.Projectile.BoneLongbow"):
                    return true;
                default: return false;
            }
        }
        bool isCraftableByCollector(string blueprintNameKey)
        {
            if (blueprintNameKey.Contains("Armour.FernArmour")) return true;
            if (blueprintNameKey.Contains("Armour.FlowerArmour")) return true;
            if (blueprintNameKey.Contains("Armour.HayArmour")) return true;
            if (blueprintNameKey.Contains("Armour.Masks")) return true;
            switch (blueprintNameKey)
            {
                case ("Items.Weapons.Siege.TrebuchetHayBale"):
                    return true;
                default: return false;
            }
        }
        bool isCraftableByAll(string blueprintNameKey)
        {
            if (!isCraftableByBlacksmith(blueprintNameKey) && !isCraftableByCarpenter(blueprintNameKey) && !isCraftableByCollector(blueprintNameKey) && !isCraftableByFarmer(blueprintNameKey) && !isCraftableByStoneMason(blueprintNameKey)) return true;
            return false;
        }
        bool PlayerCanCraftItem(Player player, string blueprintNameKey)
        {
            switch (ProfessionString(player))
            {
                case "stonemason":
                    if (isCraftableByStoneMason(blueprintNameKey) || isCraftableByAll(blueprintNameKey)) return true;
                    return false;
                case "blacksmith":
                    if (isCraftableByBlacksmith(blueprintNameKey) || isCraftableByAll(blueprintNameKey)) return true;
                    return false;
                case "soldier":
                    if (isCraftableByAll(blueprintNameKey)) return true;
                    return false;
                case "archer":
                    if (isCraftableByAll(blueprintNameKey)) return true;
                    return false;
                case "carpenter":
                    if (isCraftableByCarpenter(blueprintNameKey) || isCraftableByAll(blueprintNameKey)) return true;
                    return false;
                case "farmer":
                    if (isCraftableByFarmer(blueprintNameKey) || isCraftableByAll(blueprintNameKey)) return true;
                    return false;
                case "collector":
                    if (isCraftableByCollector(blueprintNameKey) || isCraftableByAll(blueprintNameKey)) return true;
                    return false;
                case "assassin":
                    if (isCraftableByAll(blueprintNameKey)) return true;
                    return false;
                default: return false;
            }
        }
        bool isInArea(Entity ent, float[] Area)
        {
            var posX1 = Area[0];
            var posZ1 = Area[1];
            var posX2 = Area[2];
            var posZ2 = Area[3];
            var playerX = ent.Position.x;
            var playerZ = ent.Position.z;
            if ((playerX < posX1 && playerX > posX2) && (playerZ > posZ1 && playerZ < posZ2)) return true;
            if ((playerX < posX1 && playerX > posX2) && (playerZ < posZ1 && playerZ > posZ2)) return true;
            if ((playerX > posX1 && playerX < posX2) && (playerZ < posZ1 && playerZ > posZ2)) return true;
            if ((playerX > posX1 && playerX < posX2) && (playerZ > posZ1 && playerZ < posZ2)) return true;
            return false;
        }
        Player CheckAroundStation(float[] Area)
        {
            Player PlayerAtStation = null;

            var posX1 = Area[0];
            var posZ1 = Area[1];
            var posX2 = Area[2];
            var posZ2 = Area[3];
            foreach (Player player in Server.ClientPlayers)
            {
                var playerX = player.Entity.Position.x;
                var playerZ = player.Entity.Position.z;
                if ((playerX < posX1 && playerX > posX2) && (playerZ > posZ1 && playerZ < posZ2)) PlayerAtStation = player;
                if ((playerX < posX1 && playerX > posX2) && (playerZ < posZ1 && playerZ > posZ2)) PlayerAtStation = player;
                if ((playerX > posX1 && playerX < posX2) && (playerZ < posZ1 && playerZ > posZ2)) PlayerAtStation = player;
                if ((playerX > posX1 && playerX < posX2) && (playerZ > posZ1 && playerZ < posZ2)) PlayerAtStation = player;
            }
            if (PlayerAtStation == null) return Server.GetPlayerByName("Server");
            return PlayerAtStation;
        }
        #endregion
    }
}