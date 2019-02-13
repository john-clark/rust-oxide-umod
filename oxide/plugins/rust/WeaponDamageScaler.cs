using System.Collections.Generic;
using System.Text;
using Oxide.Core.Configuration;
using Oxide.Core;
using System;
using System.Linq;
using Oxide.Core.Libraries.Covalence;
using System.Text.RegularExpressions;

namespace Oxide.Plugins
{
    //Body part scaling from k1lly0u's plugin, with permission (thanks, k1lly0u)
    //Further code cleanup/improvement with help of k1lly0u
    [Info("Weapon Damage Scaler", "Shady", "1.1.2", ResourceId = 1594)]
    [Description("Scale damage per weapon, ammo types, skins, prefabs, and per body part.")]
    internal class WeaponDamageScaler : RustPlugin
    {
        private Dictionary<ulong, string> skinIDName = new Dictionary<ulong, string>();
        WeaponData weaponData;
        private DynamicConfigFile wData;

        float GlobalDamageScale;

        private readonly Array buildingGrades = Enum.GetValues(typeof(BuildingGrade.Enum));
        private List<string> prefabNames = new List<string>();

        #region Data Management
        class ItemStructure
        {
            public string Name;
            public float GlobalModifier = 1.0f;
            public Dictionary<string, float> IndividualParts = new Dictionary<string, float>();
            public Dictionary<string, float> PrefabModifiers = new Dictionary<string, float>();
        }
        class WeaponData { public Dictionary<string, ItemStructure> Weapons = new Dictionary<string, ItemStructure>(); }

        private void InitializeWeaponData()
        {
            var skinNameDir = new List<string>();
            for(int i = 0; i < Rust.Workshop.Approved.All.Length; i++)
            {
                var skin = Rust.Workshop.Approved.All[i];
                if (skin.Skinnable.Category == Rust.Workshop.Category.Weapon) skinNameDir.Add(skin.Name);
            }
            ItemStructure outStructure;
            for (int i = 0; i < ItemManager.itemList.Count; i++)
            {
                var definition = ItemManager.itemList[i];
                if (definition == null) continue;
                if ((definition.category == ItemCategory.Weapon || definition.category == ItemCategory.Tool || definition.category == ItemCategory.Ammunition) && !definition.shortname.Contains("mod") && !weaponData.Weapons.TryGetValue(definition.shortname, out outStructure)) weaponData.Weapons[definition.shortname] = new ItemStructure { Name = definition.displayName.english, GlobalModifier = 1.0f, PrefabModifiers = new Dictionary<string, float>(), IndividualParts = CreateBodypartList() };

                var skinDir = ItemSkinDirectory.ForItem(definition) ?? null;
                if (skinDir != null && skinDir.Length > 0 && (definition.category == ItemCategory.Weapon || definition.category == ItemCategory.Tool))
                {
                    for(int j = 0; j < skinDir.Length; j++)
                    {
                        var skin = skinDir[j];
                        var name = skin.invItem?.displayName?.english ?? skin.name;
                        if (!skinNameDir.Contains(name)) skinNameDir.Add(name);
                    }
                }
            }
            for(int i = 0; i < skinNameDir.Count; i++)
            {
                var skin = skinNameDir[i];
                if (!weaponData.Weapons.TryGetValue(skin, out outStructure)) weaponData.Weapons[skin] = new ItemStructure { Name = skin, GlobalModifier = 1.0f, PrefabModifiers = new Dictionary<string, float>(), IndividualParts = CreateBodypartList() };
            }
            SaveData();        
        }
        private Dictionary<string, float> CreateBodypartList()
        {
            Dictionary<string, float> newData = new Dictionary<string, float>();
            for (int i = 0; i < Bodyparts.Length; i++) newData.Add(Bodyparts[i], 1.0f);
            return newData;
        }
        void SaveData() => wData?.WriteObject(weaponData);
        void LoadData()
        {
            try
            {
                weaponData = Interface.GetMod().DataFileSystem.ReadObject<WeaponData>("damagescaler_data");
                InitializeWeaponData();
            }
            catch(Exception ex)
            {
                PrintError(ex.ToString());
                PrintWarning("Unable to load data, creating new datafile!");
                weaponData = new WeaponData();                
            }
        }
        #endregion
        #region Config
        protected override void LoadDefaultConfig()
        {
            Config["GlobalDamageScaler"] = GlobalDamageScale = GetConfig("GlobalDamageScaler", 1.0f);
            SaveConfig();
        }
        T GetConfig<T>(string name, T defaultValue)
        {
            if (Config[name] == null) return defaultValue;
            return (T)Convert.ChangeType(Config[name], typeof(T));
        }
        public string[] Bodyparts = new string[]
               {
                    "r_forearm",
                    "l_forearm",
                    "l_upperarm",
                    "r_upperarm",
                    "r_hand",
                    "l_hand",
                    "pelvis",
                    "l_hip",
                    "r_hip",
                    "spine3",
                    "spine4",
                    "spine1",
                    "spine2",
                    "r_knee",
                    "r_foot",
                    "r_toe",
                    "l_knee",
                    "l_foot",
                    "l_toe",
                    "head",
                    "neck",
                    "jaw",
                    "r_eye",
                    "l_eye"
               };
        #endregion
        #region Localization       
        protected override void LoadDefaultMessages()
        {
            var messages = new Dictionary<string, string>
            {
                //DO NOT EDIT LANGUAGE FILES HERE!
                {"noPerms", "You do not have permission to use this command!"},
                {"invalidSyntax", "Invalid Syntax, usage example: setscale <weaponname> <x.x>"},
                {"itemSkinNotFound",   "Item/Skin: \"" + "{item}" + "\" does not exist, syntax example: setscale <WeaponOrAmmoOrSkinName> <x.x>" },
                {"invalidSyntaxBodyPart", "<color=orange>/scalebp weapon <shortname> <bone> <amount></color> - Scale damage done for <shortname> to <bone>"},
                {"bodyPartExample", "<color=orange>-- ex. /scalebp weapon rifle.ak pelvis 1.25</color> - Damage done from a assault rifle to a pelvis is set to 125%"},
                {"scaleList", "<color=orange>/scalebp list</color> - Displays all bones"},
                {"shortnameNotFound", "Could not find a weapon with the shortname: <color=orange>{0}</color>"},
                {"bonePartNotFound", "Could not find a bone called: <color=orange>{0}</color>. Check /scalebp list"},
                {"bodyPartExample2", "<color=orange>/scalebp weapon <shortname> <bone> <amount></color>"},
                {"successfullyChangedValueBP","You have changed <color=orange>{0}'s</color> damage against <color=orange>{1}</color> to <color=orange>{2}</color>x damage" },
                {"alreadySameValue", "This is already the value for the selected item!"},
                {"scaledItem", "Scaled \"" + "{engName}" + "\" (" + "{shortName}" + ") " + "to: " + "{scaledValue}"},
                {"prefabNotExist", "Prefab/Building grade does not exist: {0}" },
                {"scaledPrefab", "Scaled Prefab/Building grade \"{0}\" by {1} for weapon {2}" },
            };
            lang.RegisterMessages(messages, this);
        }
        private string GetMessage(string key, string steamId = null) => lang.GetMessage(key, this, steamId);
        #endregion
        #region Oxide Hooks
        private void OnServerInitialized()
        {
            var strs = GameManifest.Current.pooledStrings;
            for (int i = 0; i < strs.Length; i++)
            {
                var str = strs[i].str ?? string.Empty;
                prefabNames.Add(str);
            }

            for (int i = 0; i < ItemManager.itemList.Count; i++)
            {
                var item = ItemManager.itemList[i];
                var skinDir = ItemSkinDirectory.ForItem(item);
                if (skinDir == null || skinDir.Length < 1) continue;
                for(int j = 0; j < skinDir.Length; j++)
                {
                    var skin = skinDir[j];
                    var skinID = 0ul;
                    if (ulong.TryParse(skin.id.ToString(), out skinID)) skinIDName[skinID] = (skin.invItem?.displayName?.english ?? skin.name);
                }
            }

            for(int i = 0; i < Rust.Workshop.Approved.All.Length; i++)
            {
                var skin = Rust.Workshop.Approved.All[i];
                skinIDName[skin.WorkshopdId] = skin.Name;
            }
            wData = Interface.Oxide.DataFileSystem.GetFile("damagescaler_data");
            LoadData();
        }

        void Init()
        {
            RegisterPerm("weapondamagescaler.setscale");
            RegisterPerm("weapondamagescaler.setscalebp");
            RegisterPerm("weapondamagescaler.allowed");
            AddCovalenceCommand("setscale", "CmdSetScale");
            AddCovalenceCommand("scalebp", "cmdScaleBP");
            LoadDefaultConfig();
            LoadDefaultMessages();
        }

        void Unload() => SaveData();

        void OnServerSave() => SaveData();

        void OnExplosiveThrown(BasePlayer player, BaseEntity entity)
        {
            if (entity == null) return;
            ItemStructure itemStruct;
            var prefabName = (entity?.ShortPrefabName ?? string.Empty).Replace("_", ".").Replace(".deployed", "");
            if (weaponData.Weapons.TryGetValue(prefabName, out itemStruct))
            {
                var c4 = entity?.GetComponent<TimedExplosive>() ?? null;
                if (c4 != null) for (int i = 0; i < c4.damageTypes.Count; i++) c4.damageTypes[i].amount *= itemStruct?.GlobalModifier ?? 1f;
            }
        }

        private void OnRocketLaunched(BasePlayer player, BaseEntity entity)
        {
            var explosive = entity?.GetComponent<TimedExplosive>() ?? null;
            if (explosive == null) return;

            var ammoName = "ammo." + (entity?.ShortPrefabName ?? string.Empty).Replace("_", ".");
            ItemStructure itemStruct;
            if (weaponData.Weapons.TryGetValue(ammoName, out itemStruct)) for (int i = 0; i < explosive.damageTypes.Count; i++) explosive.damageTypes[i].amount *= itemStruct?.GlobalModifier ?? 1f;
        }

        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (entity == null || hitInfo == null) return;
            var attacker = hitInfo?.Initiator as BasePlayer;
            if (attacker != null && HasPermID(attacker.UserIDString, "weapondamagescaler.allowed"))
            {
                if (GlobalDamageScale != 1.0f) hitInfo?.damageTypes?.ScaleAll(GlobalDamageScale);
                else ScaleDealtDamage(hitInfo);
            }
        }
        #endregion
        #region Commands
        private void CmdSetScale(IPlayer player, string command, string[] args)
        {
            if (!HasPerm(player, "weapondamagescaler.setscale"))
            {
                SendReply(player, GetMessage("noPerms", player.Id));
                return;
            }
            if (args.Length <= 1)
            {
                SendReply(player, GetMessage("invalidSyntax", player.Id));
                return;
            }


            var itemName = args[0].ToLower();
            var englishName = GetEnglishName(itemName);
            var prefabName = (args.Length > 2) ? args[2].ToLower() : string.Empty;
            var shortName = string.Empty;

            foreach (var entry in weaponData.Weapons)
            {
                if (entry.Value.Name.ToLower() == itemName)
                {
                    shortName = entry.Key;
                    break;
                }
                else if (entry.Key.ToLower() == itemName)
                {
                    shortName = entry.Key;
                    break;
                }
            }
            if (string.IsNullOrEmpty(shortName)) shortName = skinIDName?.Where(p => p.Value.ToLower() == itemName)?.FirstOrDefault().Value ?? string.Empty;
            ItemStructure itemStruct;
            if (string.IsNullOrEmpty(shortName) || !weaponData.Weapons.TryGetValue(shortName, out itemStruct))
            {
                SendReply(player, GetMessage("itemSkinNotFound", player.Id).Replace("{item}", englishName));
                return;
            }

            var value = 0f;
            if (!float.TryParse(args[1], out value))
            {
                SendReply(player, GetMessage("invalidSyntax", player.Id));
                return;
            }

            if (!string.IsNullOrEmpty(shortName))
            {
                if (!string.IsNullOrEmpty(prefabName))
                {
                    if (!PrefabExists(prefabName) && !GradeExists(prefabName))
                    {
                        SendReply(player, string.Format(GetMessage("prefabNotExist", player.Id), prefabName));
                        return;
                    }
                    if (weaponData.Weapons[shortName].PrefabModifiers == null || weaponData.Weapons[shortName].PrefabModifiers.Count < 1) weaponData.Weapons[shortName].PrefabModifiers = new Dictionary<string, float>();
                    weaponData.Weapons[shortName].PrefabModifiers[prefabName] = value;
                    SendReply(player, string.Format(GetMessage("scaledPrefab", player.Id), prefabName, value, englishName));
                    return;
                }
                if (weaponData.Weapons[shortName].GlobalModifier == value)
                {
                    SendReply(player, GetMessage("alreadySameValue", player.Id));
                    return;
                }

                weaponData.Weapons[shortName].GlobalModifier = value;


                var sb = new StringBuilder(GetMessage("scaledItem", player.Id));

                var finalstring =
                    sb
                        .Replace("{engName}", englishName)
                        .Replace("{shortName}", shortName)
                        .Replace("{scaledValue}", value.ToString())
                        .ToString();

                SendReply(player, finalstring);
            }
            else SendReply(player, GetMessage("invalidSyntax", player.Id));
        }

        private void cmdScaleBP(IPlayer player, string command, string[] args)
        {
            if (!HasPerm(player, "weapondamagescaler.setscalebp"))
            {
                SendReply(player, GetMessage("noPerms", player.Id));
                return;
            }
            if (args.Length < 1)
            {
                SendReply(player, GetMessage("invalidSyntaxBodyPart", player.Id));
                SendReply(player, GetMessage("bodyPartExample", player.Id));
                SendReply(player, GetMessage("scaleList", player.Id)); //send in three messages to prevent it going invisible from character limit(?)
                return;
            }
            switch (args[0].ToLower())
            {
                case "weapon":
                    if (args.Length >= 3)
                    {
                        var shortName = string.Empty;
                        var engName = args[1].ToLower();

                        foreach (var entry in weaponData.Weapons)
                        {
                            if (entry.Value.Name.ToLower() == engName)
                            {
                                shortName = entry.Key;
                                break;
                            }
                            else if (entry.Key.ToLower() == engName)
                            {
                                shortName = entry.Key;
                                break;
                            }
                        }
                        if (!string.IsNullOrEmpty(shortName))
                        {
                            if (weaponData.Weapons.ContainsKey(shortName))
                            {
                                if (weaponData.Weapons[shortName].IndividualParts.ContainsKey(args[2].ToLower()))
                                {
                                    float i = 0;
                                    if (args.Length == 4)
                                        if (!float.TryParse(args[3], out i)) i = 1.0f;
                                    weaponData.Weapons[shortName].IndividualParts[args[2].ToLower()] = i;
                                    SaveData();
                                    SendReply(player, string.Format(GetMessage("successfullyChangedValueBP", player.Id), args[1], args[2], i));
                                    return;
                                }
                                SendReply(player, string.Format(GetMessage("bonePartNotFound", player.Id), args[2]));
                                return;
                            }
                            SendReply(player, string.Format(GetMessage("shortnameNotFound", player.Id), args[1]));
                            return;
                        }
                    }
                    SendReply(player, GetMessage("bodyPartExample2", player.Id));
                    return;

                case "list":
                    var bpPlayer = player?.Object as BasePlayer;
                    if (bpPlayer == null) return;
                    var BPsb = new StringBuilder();
                    for (int i = 0; i < Bodyparts.Length; i++)
                    {
                        var bp = Bodyparts[i];
                        var bpName = FirstUpper(bpPlayer?.skeletonProperties?.FindBone(StringPool.Get(bp))?.name?.english ?? bp);
                        BPsb.AppendLine(bp + " (" + bpName + "), ");
                    }
                    SendReply(bpPlayer, BPsb.ToString().TrimEnd().TrimEnd(','));
                    return;
            }
        }
        #endregion
        #region Util
        string GetSkinName(ulong skinID)
        {
            var skinName = string.Empty;
            skinIDName.TryGetValue(skinID, out skinName);
            return skinName;
        }
        ItemDefinition GetItemDefFromPrefabName(string shortprefabName)
        {
            if (string.IsNullOrEmpty(shortprefabName)) return null;
            var adjName = shortprefabName.Replace("_deployed", "").Replace(".deployed", "").Replace("_", "").Replace(".entity", "");
            var def = ItemManager.FindItemDefinition(adjName);
            if (def != null) return def;
            adjName = shortprefabName.Replace("_deployed", "").Replace(".deployed", "").Replace("_", ".").Replace(".entity", "");
            return ItemManager.FindItemDefinition(adjName);
        }

        //this code feels messy but it works I guess
        private void ScaleDealtDamage(HitInfo hitInfo)
        {
            if (hitInfo == null || hitInfo?.damageTypes == null) return;
            if (hitInfo?.Weapon == null && hitInfo?.WeaponPrefab == null) return;
            var bodypart = StringPool.Get((hitInfo?.HitBone ?? 0)) ?? string.Empty;
            var wepPrefab = hitInfo?.Weapon?.ShortPrefabName ?? hitInfo?.WeaponPrefab?.ShortPrefabName ?? string.Empty;
            var weaponName = hitInfo?.Weapon?.GetItem()?.info?.shortname ?? hitInfo?.WeaponPrefab?.GetItem()?.info?.shortname ?? string.Empty;
            if (string.IsNullOrEmpty(weaponName) && !string.IsNullOrEmpty(wepPrefab) && !wepPrefab.Contains("rocket") && !wepPrefab.Contains("explosive")) weaponName = GetItemDefFromPrefabName(wepPrefab)?.shortname ?? string.Empty;
            var ammoName = hitInfo?.Weapon?.GetItem()?.GetHeldEntity()?.GetComponent<BaseProjectile>()?.primaryMagazine?.ammoType?.shortname ?? string.Empty;
            var skinName = GetSkinName(hitInfo?.Weapon?.GetItem()?.skin ?? 0);
            ItemStructure weaponInfo = null;
            ItemStructure ammoInfo = null;
            ItemStructure skinInfo = null;
            if (!string.IsNullOrEmpty(weaponName)) weaponData.Weapons.TryGetValue(weaponName, out weaponInfo);
            if (!string.IsNullOrEmpty(ammoName)) weaponData.Weapons.TryGetValue(ammoName, out ammoInfo);
            if (!string.IsNullOrEmpty(skinName)) weaponData.Weapons.TryGetValue(skinName, out skinInfo);
            var ammoMod = ammoInfo?.GlobalModifier ?? 1.0f;
            var skinMod = skinInfo?.GlobalModifier ?? 1.0f;
            var prefabName = hitInfo?.HitEntity?.ShortPrefabName ?? string.Empty;
            var gradeName = (hitInfo?.HitEntity?.GetComponent<BuildingBlock>()?.grade ?? BuildingGrade.Enum.None).ToString().ToLower();


            var prefabModWeapon = 1.0f;
            var prefabModAmmo = 1.0f;
            var prefabModSkin = 1.0f;
            var individualModWeapon = 1.0f;
            var individualModAmmo = 1.0f;
            var individualModSkin = 1.0f;
            if (weaponInfo != null)
            {
                if (!weaponInfo.PrefabModifiers.TryGetValue(prefabName, out prefabModWeapon) && !weaponInfo.PrefabModifiers.TryGetValue(gradeName, out prefabModWeapon)) prefabModWeapon = 1.0f;
                if (!weaponInfo.IndividualParts.TryGetValue(bodypart, out individualModWeapon)) individualModWeapon = 1.0f;
            }
            if (ammoInfo != null)
            {
                if (!ammoInfo.PrefabModifiers.TryGetValue(prefabName, out prefabModAmmo) && !ammoInfo.PrefabModifiers.TryGetValue(gradeName, out prefabModAmmo)) prefabModAmmo = 1.0f;
                if (!ammoInfo.IndividualParts.TryGetValue(bodypart, out individualModAmmo)) individualModAmmo = 1.0f;
            }
            if (skinInfo != null)
            {
                if (skinInfo != null && !skinInfo.PrefabModifiers.TryGetValue(prefabName, out prefabModSkin) && !skinInfo.PrefabModifiers.TryGetValue(gradeName, out prefabModSkin)) prefabModSkin = 1.0f;
                if (!skinInfo.IndividualParts.TryGetValue(bodypart, out individualModSkin)) individualModSkin = 1.0f;
            }

            var prefabMod = (prefabModWeapon + prefabModAmmo + prefabModSkin) - 2;

            var globalMod = weaponInfo?.GlobalModifier ?? 1.0f;

            var individualMod = (individualModWeapon + individualModSkin + individualModAmmo) - 2;

            var totalMod = (globalMod + individualMod + ammoMod + prefabMod + skinMod) - 4;

            if (totalMod != 1.0f) hitInfo?.damageTypes?.ScaleAll(totalMod);
        }

        //Borrowed RemoveTags from BetterChat//
        private string RemoveTags(string phrase)
        {
            if (string.IsNullOrEmpty(phrase)) return phrase;
            var forbiddenTags = new List<string>{
                "</color>",
                "</size>",
                "<b>",
                "</b>",
                "<i>",
                "</i>"
            };
            phrase = new Regex("(<color=.+?>)").Replace(phrase, string.Empty);
            phrase = new Regex("(<size=.+?>)").Replace(phrase, string.Empty);
            var phraseSB = new StringBuilder(phrase);
            for (int i = 0; i < forbiddenTags.Count; i++) phraseSB.Replace(forbiddenTags[i], "");
            return phraseSB.ToString();
        }

        private void RegisterPerm(string perm) => permission.RegisterPermission(perm, this);

        private bool HasPermID(string userID, string perm) => permission.UserHasPermission(userID, perm);

        private bool HasPerm(IPlayer player, string perm) => player?.HasPermission(perm) ?? false;

        private void SendReply(IPlayer player, string message)
        {
            if (player == null || string.IsNullOrEmpty(message)) return;
            if (player.IsServer || player?.Object == null) message = RemoveTags(message); //remove tags for console (may no longer be needed? - needs to be checked)
            player.Message(message);
        }

        private bool PrefabExists(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName)) return false;
            return prefabNames.Any(p => p.EndsWith(prefabName, StringComparison.OrdinalIgnoreCase) || p.EndsWith(prefabName + ".prefab", StringComparison.OrdinalIgnoreCase));
        }

        private bool GradeExists(string gradeName)
        {
            if (string.IsNullOrEmpty(gradeName)) return false;
            foreach (var grade in buildingGrades) if (grade.ToString().Equals(gradeName, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private string GetEnglishName(string shortName) { return ItemManager.FindItemDefinition(shortName)?.displayName?.english ?? shortName; }

        private string FirstUpper(string original)
        {
            if (string.IsNullOrEmpty(original)) return string.Empty;
            var charArray = original.ToCharArray();
            charArray[0] = char.ToUpper(charArray[0]);
            return new string(charArray);
        }
        #endregion
    }
}