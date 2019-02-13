using Oxide.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Night Door", "Slydelix", "1.7.2", ResourceId = 2684)]
    class NightDoor : RustPlugin
    {
        private bool hideTime, BypassAdmin, BypassPerm, UseRealTime, AutoDoor, useMulti, intialized = false;
        private string startTime, endTime;

        private const string usePerm = "nightdoor.use";
        private const string createIntervalPerm = "nightdoor.createinterval";
        private const string bypassPerm = "nightdoor.bypass";

        #region config

        protected override void LoadDefaultConfig()
        {
            Config["Allow admins to open time-limited door"] = BypassAdmin = GetConfig("Allow admins to open time-limited door", false);
            Config["Allow players with bypass permission to open time-limited door"] = BypassPerm = GetConfig("Allow players with bypass permission to open time-limited door", false);
            Config["Beginning time (HH:mm)"] = startTime = GetConfig("Beginning time (HH:mm)", "00:00");
            Config["Use multiple time intervals"] = useMulti = GetConfig("Use multiple time intervals", false);
            Config["End time (HH:mm)"] = endTime = GetConfig("End time (HH:mm)", "00:00");
            Config["Use system (real) time"] = UseRealTime = GetConfig("Use system (real) time", false);
            Config["Don't show time to player when unable to open door"] = hideTime = GetConfig("Don't show time to player when unable to open door", false);
            Config["Automatic door closing/opening"] = AutoDoor = GetConfig("Automatic door closing/opening", false);
            SaveConfig();
        }

        #endregion
        #region lang

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                //Default messages
                {"ServerWiped", "Wipe du serveur détecté, remise à zéro du fichier de données"},
                {"WipeManual", "Remise a zéro manuelle du fichier de données de Night Door"},
                {"Warning_StartBiggerThanEnd", "La configuration ne semble pas correcte ! (l'heure de début se situe après l'heure de fin)"},
                {"Warning_DefaultConfig", "00:00 défini comme fin et comme début ! Changez ces valeurs par celles que vous désirez"},
                {"NoPermission", "Vous n'avez pas la permission pour utiliser cette commande"},
                //Player messages
                {"PlayerCannotOpen", "Cette {0} ne peut pas être ouverte."},
                {"PlayerCannotOpenTime", "Cette {0} ne peut être ouverte avant {1}"},
                {"PlayerCannotPlace", "Un verrou ne peut pas être placé sur cette {0}"},           
                //Messages for /timeperiod
                {"TimeIntervalRemovedDoor", "La porte à {0} peut maintenant être ouverte car la période a été supprimée !"},
                {"TimeIntevalSyntax", "Erreur de syntaxe ! <color=silver>/timeperiod create <nom> <HH:mm>(heure de début) <HH:mm>(heure de fin)</color>"},
                {"TimeIntervalRemoveSyntax", "Erreur de syntaxe ! <color=silver>/timeperiod remove <nom></color>"},
                {"TimeIntervalCreated", "Nouvelle période créée sous le nom '{0}' ({1} - {2})"},
                {"TimeIntervalRemoved", "Période avec le nom '{0}' a été supprimée"},
                {"TimeIntervalNotSetUp", "Il n'y a aucune période enregistrée"},
                {"TimeIntervalList", "Liste de toutes les périodes: \n{0}"},
                {"TimeIntervalExists", "Une période avec ce nom existe déjà"},
                {"TimeIntervalDisabled", "Les périodes multiples ne sont pas activées." },
                {"TimeIntervalMissingTimePeriod", "Erreur de syntaxe ! /nd add <nom de la période que la porte doit utiliser>"},
                {"TimeIntervalNotFound", "Impossible de trouver une période avec ce nom '{0}'"},
                {"HowTo26Hour", "Pour créer une période incluant 2 jours différents, comme (22:00 - 02:00), ajouter 24 à la dernière valeur (22:00 - 26:00)" },
                {"HowToUseTimeIntervals", "Pour cr  éer une periode saisir <color=silver>/timeperiod create <nom de la période> <HH:mm> (heure de début) <HH:mm> (heure de fin)</color>\nPour supprimer une période saisir <color=silver>/timeperiod remove <nom></color>\nPour obtenir une liste de toutes les périodes saisir <color=silver>/timeperiod list</color>"},
                //Messages for /nd
                {"NoEntity", "Vous ne regardez pas d'entité valide" },
                {"EntityNotDoor", "L'entité que vous regardez n'est pas ouvrable" },
                {"NoLockedEnts", "Aucune entité avec période trouvée"},
                {"ShowingEnts", "Affiche toutes les entités avec période"},
                { "SyntaxError", "Erreur de syntaxe ! Saisir /nd help pour plus d'information"},
                {"DoorNowLocked", "Cette {0} est maintenant sous la période (temps par défaut)"},
                {"DoorCustomLocked", "Cette {0} est maintenant sous la période ('{1}' ({2} - {3})"},
                {"NotTimeLocked", "Cette {0} n'est pas restreinte à une période"},
                {"AlreadyLocked", "Cette {0} est déjà restreint à une période"},
                {"DoorUnlocked", "Cette {0} n'est plus restreinte à une période"},
                {"InfoAboutDoor", "Cette {0} est maintenant sous période\nLa période est {1} ({2} - {3})"},
                {"ListOfCommands", "Liste des commandes :\n<color=silver>*Vous devez être en train de regarder une porte/trappe/porche pour que la plupart de ces commandes*</color>\n<color=silver>/nd add</color> - Rend l'entité ouvrable uniquement pendant la période par défaut (voir config)\n<color=silver>/nd add <periode></color> Rend l'entité ouvrable uniquement pendant la période spécifiée (/timeperiod)\n<color=silver>/nd remove</color> Rend l'entité à son état 'normal' (ouvrable a tout moment)\n<color=silver>/nd show</color> Affiche toutes les entités sous période\n<color=silver>/nd info</color> Affiche si la porte/trappe/porche est sous période et informe sur la période le cas échéant\nPériode actuelle : {0} - {1}"}
            }, this, "fr");

            lang.RegisterMessages(new Dictionary<string, string>()
            {
                //Default messages
                {"ServerWiped", "Server wipe detected, wiping data file"},
                {"WipeManual", "Manually wiped Night Door data file"},
                {"Warning_StartBiggerThanEnd", "Config seems to be set up incorrectly! (Start time value is bigger than end time value)"},
                {"Warning_DefaultConfig", "Detected 00:00 as both end and start time! Change these to values you want"},
                {"NoPermission", "You don't have permission to use this command"},
                //Player messages
                {"PlayerCannotOpen", "This {0} cannot be opened."},
                {"PlayerCannotOpenTime", "This {0} cannot be opened until {1}"},
                {"PlayerCannotPlace", "A lock cannot be placed on this {0}"},           
                //Messages for /timeperiod
                {"TimeIntervalRemovedDoor", "Door at {0} can now be opened at any time because the time period got removed!"},
                {"TimeIntevalSyntax", "Wrong syntax! <color=silver>/timeperiod create <name> <HH:mm>(starting time) <HH:mm>(ending time)</color>"},
                {"TimeIntervalRemoveSyntax", "Wrong syntax! <color=silver>/timeperiod remove <name></color>"},
                {"TimeIntervalCreated", "Created a new time period with name '{0}' ({1} - {2})"},
                {"TimeIntervalRemoved", "Removed a time period with name '{0}'"},
                {"TimeIntervalNotSetUp", "There are no time periods set up"},
                {"TimeIntervalList", "List of all time periods: \n{0}"},
                {"TimeIntervalExists", "A time period with that name already exists"},
                {"TimeIntervalDisabled", "Multiple time intervals are disabled." },
                {"TimeIntervalMissingTimePeriod", "Wrong syntax! /nd add <name of time period the door will use>"},
                {"TimeIntervalNotFound", "Couldn't find a time period with name '{0}'"},
                {"HowTo26Hour", "To create time periods that includes 2 days, like 22:00 - 02:00 add 24 to the last value (22:00 - 26:00)" },
                {"HowToUseTimeIntervals", "To create a time period type <color=silver>/timeperiod create <name of time period> <HH:mm> (starting time) <HH:mm> (ending time)</color>\nTo delete a time period type <color=silver>/timeperiod remove <name></color>\nFor list of all time periods type <color=silver>/timeperiod list</color>"},
                //Messages for /nd
                {"NoEntity", "You are not looking at an entity" },
                {"EntityNotDoor", "The entity you are looking at is not openable" },
                {"NoLockedEnts", "No time locked entites found"},
                {"ShowingEnts", "Showing all time locked entites"},
                { "SyntaxError", "Wrong syntax! Type /nd help for more info"},
                {"DoorNowLocked", "This {0} is now time locked (default time)"},
                {"DoorCustomLocked", "This {0} is now time locked (Time period '{1}' ({2} - {3})"},
                {"NotTimeLocked", "This {0} isn't time locked"},
                {"AlreadyLocked", "This {0} is already time locked"},
                {"DoorUnlocked", "This {0} is not time locked anymore"},
                {"InfoAboutDoor", "This {0} is time locked\nTime period is {1} ({2} - {3})"},
                {"ListOfCommands", "List of commands:\n<color=silver>*You have to look at the door/hatch/gate for most of the commands to work*</color>\n<color=silver>/nd add</color> - Makes the entity openable only during default time period(config time)\n<color=silver>/nd add <time period></color> Makes the entity openable only during specified time period (/timeperiod)\n<color=silver>/nd remove</color> Makes the entity 'normal' again (openable at any time)\n<color=silver>/nd show</color> shows all time locked entites\n<color=silver>/nd info</color> shows if the door/hatch/gate is time locked and the time period if it is\nCurrent time period: {0} - {1}"}
            }, this, "en");
        }

        private T GetConfig<T>(string name, T defaultValue)
        {
            if (Config[name] == null) return defaultValue;
            return (T)Convert.ChangeType(Config[name], typeof(T));
        }

        #endregion
        #region DataFile

        private class StoredData
        {
            public Dictionary<uint, string> IDlist = new Dictionary<uint, string>();
            public HashSet<TimeInfo> TimeEntries = new HashSet<TimeInfo>();

            public StoredData()
            {
            }
        }

        private class TimeInfo
        {
            public string name;
            public string start;
            public string end;

            public TimeInfo(string nameIn, string startInput, string endInput)
            {
                name = nameIn;
                start = startInput;
                end = endInput;
            }
        }

        private StoredData storedData;

        #endregion
        #region Hooks

        private void OnNewSave(string filename)
        {
            PrintWarning(lang.GetMessage("ServerWiped", this));
            storedData.IDlist.Clear();
            SaveFile();
        }

        private void OnServerInitialized()
        {
            timer.In(10f, () =>
            {
                intialized = true;
            });
            Repeat();
            DoDefaultT();
        }

        private void Init()
        {
            LoadDefaultConfig();
            permission.RegisterPermission(usePerm, this);
            permission.RegisterPermission(createIntervalPerm, this);
            permission.RegisterPermission(bypassPerm, this);
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>("NightDoor_NEW");
            if (GetDateTime(startTime) > GetDateTime(endTime)) PrintWarning(lang.GetMessage("Warning_StartBiggerThanEnd", this));
            if (startTime == "00:00" && endTime == "00:00") PrintWarning(lang.GetMessage("Warning_DefaultConfig", this));
        }

        private void SaveFile() => Interface.Oxide.DataFileSystem.WriteObject("NightDoor_NEW", storedData);

        private void Unload() => SaveFile();

        private void OnServerSave() => CheckAllEntites();

        private void OnEntityKill(BaseNetworkable entity) => CheckAllEntites();

        private void OnDoorOpened(Door door, BasePlayer player)
        {
            BaseEntity doorEnt = door.GetEntity();
            if (doorEnt == null) return;

            string name = GetNameFromEntity(doorEnt);
            uint entID = doorEnt.net.ID;
            if (storedData.IDlist.ContainsKey(door.net.ID))
            {
                float time = ConVar.Env.time;
                if (CanOpen(player, entID, time)) return;
                door.CloseRequest();

                string entname = storedData.IDlist[entID];

                foreach (var entry in storedData.TimeEntries)
                {
                    if (entry.name == entname)
                    {
                        if (hideTime)
                        {
                            SendReply(player, lang.GetMessage("PlayerCannotOpen", this, player.UserIDString), name);
                            return;
                        }

                        SendReply(player, lang.GetMessage("PlayerCannotOpenTime", this, player.UserIDString), name, entry.start);
                        return;
                    }
                }
                return;
            }
        }

        private void OnItemDeployed(Deployer deployer, BaseEntity entity)
        {
            if (entity == null || deployer == null) return;

            BasePlayer player = deployer?.GetOwnerPlayer();
            string item = GetNameFromEntity(entity);

            if (entity is Door)
            {
                BaseEntity codelockent = entity?.GetSlot(BaseEntity.Slot.Lock);

                if (storedData.IDlist.ContainsKey(entity.net.ID))
                {
                    Item codelock;
                    if (codelockent?.PrefabName == "assets/prefabs/locks/keypad/lock.code.prefab") codelock = ItemManager.CreateByName("lock.code", 1);
                    else codelock = ItemManager.CreateByName("lock.key", 1);

                    if (codelock != null)
                    {
                        player.GiveItem(codelock);
                        SendReply(player, lang.GetMessage("PlayerCannotPlace", this, player.UserIDString), item);
                    }
                    codelockent?.KillMessage();
                    return;
                }
            }
        }

        private object CanPickupEntity(BaseCombatEntity entity, BasePlayer player)
        {
            if (entity == null) return null;
            if (storedData.IDlist.ContainsKey(entity.net.ID)) return true;
            return null;
        }

        #endregion
        #region stuff
        private string GetNameFromEntity(BaseEntity entity)
        {
            Dictionary<string, string> EntList = new Dictionary<string, string> {
                {"door.hinged.toptier", "Armored Door" },
                {"gates.external.high.wood", "High External Wooden Gate" },
                {"gates.external.high.stone", "High External Stone Gate" },
                {"wall.frame.shopfront", "Shop Front" },
                {"wall.frame.garagedoor", "Garage Door" },
                {"shutter.wood.a", "Wood Shutters" },
                {"floor.ladder.hatch", "Ladder Hatch" },
                {"wall.frame.cell.gate", "Prison Cell Gate" },
                {"wall.frame.fence.gate", "Chainlink Fence Gate" },
                {"door.double.hinged.wood", "Wood Double Door" },
                {"door.double.hinged.toptier", "Armored Double Door" },
                {"door.double.hinged.metal", "Sheet Metal Double Door" },
                {"door.hinged.metal", "Sheet Metal Door" }
            };

            string itemName = "entity";

            if (EntList.ContainsKey(entity?.ShortPrefabName))
                itemName = EntList[entity?.ShortPrefabName];

            return itemName;
        }
        private void Repeat()
        {
            timer.Every(5f, () => {
                CheckDoors();
                CheckAllEntites();
            });
        }

        private void DoDefaultT()
        {
            timer.Once(1f, () => {
                foreach (var entry in storedData.TimeEntries)
                {
                    if (entry.name == "default")
                    {
                        entry.start = startTime;
                        entry.end = endTime;
                        SaveFile();
                        return;
                    }
                }
                
                var cfgTime = new TimeInfo("default", startTime, endTime);
                storedData.TimeEntries.Add(cfgTime);
                SaveFile();
                return;
            });
        }

        private void CheckDoors()
        {
            if (!AutoDoor) return;

            float time = ConVar.Env.time;

            if (storedData.IDlist.Count == 0) return;

            foreach (var entry in storedData.IDlist.ToList())
            {
                BaseEntity ent = BaseNetworkable.serverEntities.Find(entry.Key) as BaseEntity;

                if (ent == null || ent.IsDestroyed) continue;

                if (CanOpen(null, ent.net.ID, time))
                {
                    ent.SetFlag(BaseEntity.Flags.Open, true);
                    ent.SendNetworkUpdateImmediate();
                }

                else
                {
                    ent.SetFlag(BaseEntity.Flags.Open, false);
                    ent.SendNetworkUpdateImmediate();
                }
            }
        }

        private bool CanOpen(BasePlayer player, uint ID, float now)
        {
            if (player != null)
            {
                if (player.IsAdmin && BypassAdmin) return true;
                if (permission.UserHasPermission(player.UserIDString, bypassPerm) && BypassPerm) return true;
            }

            if (UseRealTime)
            {
                foreach (var entry in storedData.IDlist)
                {
                    if (entry.Key == ID)
                    {
                        foreach (var ent in storedData.TimeEntries)
                        {
                            if (ent.name == entry.Value)
                            {
                                string ending = ent.end;
                                int valInt = int.Parse(ending.Split(':')[0]);
                                if (valInt > 24)
                                {
                                    DateTime start_changed = GetDateTime(ID, true);
                                    DateTime end_changed = GetDateTime(ID, false);
                                    start_changed = start_changed.AddDays(-1);
                                    end_changed = end_changed.AddDays(-1);
                                    if ((DateTime.Now >= start_changed) && (DateTime.Now <= end_changed)) return true;
                                    return false;
                                }

                                else
                                {
                                    DateTime start = GetDateTime(ID, true);
                                    DateTime end = GetDateTime(ID, false);
                                    if ((DateTime.Now >= start) && (DateTime.Now <= end)) return true;
                                    return false;
                                }
                            }
                        }
                    }
                }
            }

            //wtf is this even
            float a = GetFloat(ID, true);
            float b = GetFloat(ID, false);
            float c = b - 24f;
            if (c < 0) c = 99999f;
            if (now >= a && now <= b) return true;
            if (c != 99999f)
            {
                if (now >= 0f && now <= c) return true;
            }

            return false;
        }

        private void CheckAllEntites()
        {
            if (!intialized || storedData.IDlist.Count == 0) return;

            foreach (var entry in storedData.IDlist.ToList())
            {
                BaseNetworkable ent = BaseNetworkable.serverEntities.Find(entry.Key) ?? null;

                if (ent == null || ent.IsDestroyed)
                {
                    storedData.IDlist.Remove(entry.Key);
                    SaveFile();
                }
            }   
        }

        private float GetFloat(uint ID, bool start)
        {
            string input = "";
            foreach (var entry in storedData.TimeEntries)
            {
                if (entry.name == storedData.IDlist[ID])
                {
                    if (start) input = entry.start;
                    else input = entry.end;
                }
            }

            string[] parts = input.Split(':');
            int hourInt = int.Parse(parts[0]);
            int minInt = int.Parse(parts[1]);

            float min = (float)minInt / 60;

            return (hourInt + min);
        }

        private float GetFloat(string in2)
        {
            string[] parts = in2.Split(':');
            string h = "", m = "";
            int hourInt = int.Parse(parts[0]);
            int minInt = int.Parse(parts[1]);

            float min = (float)minInt / 60;
            return (hourInt + min);
        }

        private DateTime GetDateTime(uint ID, bool start)
        {
            DateTime final = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 0, 0, 0);
            string input = "";
            foreach (var entry in storedData.TimeEntries)
            {
                if (entry.name == storedData.IDlist[ID])
                {
                    if (start) input = entry.start;
                    else input = entry.end;
                }
            }

            string[] parts = input.Split(':');
            int mInt = int.Parse(parts[1]);
            int hInt = int.Parse(parts[0]);

            if (hInt > 24 && start)
            {
                final = final.AddHours(hInt);
                final = final.AddMinutes(mInt);
                final = final.AddDays(-1);
                return final;
            }

            final = final.AddHours(hInt);
            final = final.AddMinutes(mInt);
            return final;
        }

        private DateTime GetDateTime(string input)
        {
            DateTime final = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 0, 0, 0);
            string[] parts = input.Split(':');
            int mInt = int.Parse(parts[1]);
            int hInt = int.Parse(parts[0]);
            final = final.AddHours(hInt);
            final = final.AddMinutes(mInt);
            return final;
        }

        private BaseEntity GetLookAtEntity(BasePlayer player, float maxDist = 250, int coll = -1)
        {
            if (player == null || player.IsDead()) return null;
            RaycastHit hit;
            var currentRot = Quaternion.Euler(player?.serverInput?.current?.aimAngles ?? Vector3.zero) * Vector3.forward;
            var ray = new Ray((player?.eyes?.position ?? Vector3.zero), currentRot);
            if (Physics.Raycast(ray, out hit, maxDist))
            {
                var ent = hit.GetEntity() ?? null;
                if (ent != null && !(ent?.IsDestroyed ?? true)) return ent;
            }
            return null;
        }

        #endregion
        #region commands

        [ConsoleCommand("wipedoordata")]
        private void nightdoorwipeccmd(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null) return;
            storedData.IDlist.Clear();
            storedData.TimeEntries.Clear();
            SaveFile();
            Puts(lang.GetMessage("WipeManual", this));
        }

        [ChatCommand("timeperiod")]
        private void timeCmd(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, createIntervalPerm))
            {
                SendReply(player, lang.GetMessage("NoPermission", this, player.UserIDString));
                return;
            }

            if (!useMulti)
            {
                SendReply(player, lang.GetMessage("TimeIntervalDisabled", this, player.UserIDString));
                return;
            }

            if (args.Length < 1)
            {
                SendReply(player, lang.GetMessage("HowToUseTimeIntervals", this, player.UserIDString));
                return;
            }

            switch (args[0].ToLower())
            {
                case "create":
                    {
                        if (args.Length < 4)
                        {
                            SendReply(player, lang.GetMessage("TimeIntevalSyntax", this, player.UserIDString));
                            return;
                        }

                        string Inputname = args[1];
                        string time1 = args[2];
                        string time2 = args[3];

                        foreach (var timeEntry in storedData.TimeEntries)
                        {
                            if (timeEntry.name.ToLower() == Inputname.ToLower())
                            {
                                SendReply(player, lang.GetMessage("TimeIntervalExists", this, player.UserIDString));
                                return;
                            }
                        }

                        //Most complicated methods VVVVV

                        if (!time1.Contains(":") && !time2.Contains(":"))
                        {
                            SendReply(player, lang.GetMessage("TimeIntevalSyntax", this, player.UserIDString));
                            return;
                        }

                        string[] split1, split2;
                        split2 = time2.Split(':');
                        split1 = time1.Split(':');
                        string[] allNumbers = { split1[0], split1[1], split2[0], split2[1] };
                        foreach (string p in allNumbers)
                        {
                            foreach (var ew in p)
                            {
                                if (ew == '0' || ew == '1' || ew == '2' || ew == '3' || ew == '4' || ew == '5' || ew == '6' || ew == '7' || ew == '8' || ew == '9') continue;

                                else
                                {
                                    SendReply(player, lang.GetMessage("TimeIntevalSyntax", this, player.UserIDString));
                                    return;
                                }
                            }

                            if (p.Length != 2)
                            {
                                SendReply(player, lang.GetMessage("TimeIntevalSyntax", this, player.UserIDString));
                                return;
                            }
                        }

                        float a = GetFloat(time1);
                        float b = GetFloat(time2);

                        if (a > b)
                        {
                            SendReply(player, lang.GetMessage("HowTo26Hour", this, player.UserIDString));
                            return;
                        }

                        TimeInfo entry = new TimeInfo(Inputname, time1, time2);
                        storedData.TimeEntries.Add(entry);
                        SendReply(player, lang.GetMessage("TimeIntervalCreated", this, player.UserIDString), Inputname, time1, time2);
                        SaveFile();
                        return;
                    }

                case "remove":
                    {
                        if (args.Length < 2)
                        {
                            SendReply(player, lang.GetMessage("TimeIntervalRemoveSyntax", this, player.UserIDString));
                            return;
                        }

                        string Inputname = args[1];
                        List<TimeInfo> toRemove = new List<TimeInfo>();
                        List<uint> idList = new List<uint>();

                        foreach (var entry in storedData.TimeEntries)
                        {
                            if (entry.name == Inputname && entry.name != "default")
                            {
                                toRemove.Add(entry);
                                foreach (var ent in storedData.IDlist)
                                {
                                    if (ent.Value == Inputname)
                                    {
                                        idList.Add(ent.Key);
                                        BaseNetworkable entity = BaseNetworkable.serverEntities.Find(ent.Key);
                                        SendReply(player, lang.GetMessage("TimeIntervalRemovedDoor", this, player.UserIDString), entity.transform.position.ToString());
                                        player.SendConsoleCommand("ddraw.sphere", 10f, Color.green, entity.transform.position, 1f);
                                    }
                                }

                                SendReply(player, lang.GetMessage("TimeIntervalRemoved", this, player.UserIDString), entry.name);
                                foreach (var ent2 in toRemove) storedData.TimeEntries.Remove(ent2);
                                foreach (var ent3 in idList) storedData.IDlist.Remove(ent3);
                                SaveFile();
                                return;
                            }
                        }
                        SendReply(player, lang.GetMessage("TimeIntervalNotFound", this, player.UserIDString), Inputname);
                        return;
                    }

                case "list":
                    {
                        List<string> L = new List<string>();
                        if (storedData.TimeEntries.Count < 1)
                        {
                            SendReply(player, lang.GetMessage("TimeIntervalNotSetUp", this, player.UserIDString));
                            return;
                        }

                        foreach (var entry in storedData.TimeEntries)
                            L.Add(entry.name + " (" + entry.start + " - " + entry.end + ")");

                        string finished = string.Join("\n", L.ToArray());
                        SendReply(player, lang.GetMessage("TimeIntervalList", this, player.UserIDString), finished);
                        return;
                    }

                default:
                    {
                        SendReply(player, lang.GetMessage("TimeIntevalSyntax", this, player.UserIDString));
                        return;
                    }
            }

        }

        [ChatCommand("nd")]
        private void nightdoorcmd(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, usePerm))
            {
                SendReply(player, lang.GetMessage("NoPermission", this, player.UserIDString));
                return;
            }

            if (args.Length < 1)
            {
                SendReply(player, lang.GetMessage("SyntaxError", this, player.UserIDString));
                return;
            }

            BaseEntity ent = GetLookAtEntity(player, 10f);  

            string name = GetNameFromEntity(ent);
            Puts(name);
            switch (args[0].ToLower())
            {
                case "add":
                    {
                        if (ent == null)
                        {
                            SendReply(player, lang.GetMessage("NoEntity", this, player.UserIDString));
                            return;
                        }

                        if (!(ent is Door))
                        {
                            SendReply(player, lang.GetMessage("EntityNotDoor", this, player.UserIDString));
                            return;
                        }

                        if (storedData.IDlist.ContainsKey(ent.net.ID))
                        {
                            SendReply(player, lang.GetMessage("AlreadyLocked", this, player.UserIDString), name);
                            return;
                        }

                        if (args.Length < 2)
                        {
                            storedData.IDlist.Add(ent.net.ID, "default");
                            SendReply(player, lang.GetMessage("DoorNowLocked", this, player.UserIDString), name);
                            SaveFile();
                            return;
                        }

                        string tiName = args[1];
                        if (string.IsNullOrEmpty(tiName))
                        {
                            SendReply(player, lang.GetMessage("TimeIntervalMissingTimePeriod", this, player.UserIDString));
                            return;
                        }

                        foreach (var entry in storedData.TimeEntries)
                        {
                            if (entry.name.ToLower() == tiName.ToLower())
                            {
                                storedData.IDlist.Add(ent.net.ID, entry.name);
                                SendReply(player, lang.GetMessage("DoorCustomLocked", this, player.UserIDString), name, entry.name, entry.start, entry.end);
                                SaveFile();
                                return;
                            }
                        }
                        SendReply(player, lang.GetMessage("TimeIntervalNotFound", this, player.UserIDString), tiName);
                        return;
                    }

                case "remove":
                    {
                        if (ent == null)
                        {
                            SendReply(player, lang.GetMessage("NoEntity", this, player.UserIDString));
                            return;
                        }

                        if (!(ent is Door))
                        {
                            SendReply(player, lang.GetMessage("EntityNotDoor", this, player.UserIDString));
                            return;
                        }

                        if (!storedData.IDlist.ContainsKey(ent.net.ID))
                        {
                            SendReply(player, lang.GetMessage("NotTimeLocked", this, player.UserIDString), name);
                            return;
                        }

                        storedData.IDlist.Remove(ent.net.ID);
                        SendReply(player, lang.GetMessage("DoorUnlocked", this, player.UserIDString), name);
                        SaveFile();
                        return;
                    }

                case "info":
                    {
                        if (ent == null)
                        {
                            SendReply(player, lang.GetMessage("NoEntity", this, player.UserIDString));
                            return;
                        }

                        if (!(ent is Door))
                        {
                            SendReply(player, lang.GetMessage("EntityNotDoor", this, player.UserIDString));
                            return;
                        }

                        if (!storedData.IDlist.ContainsKey(ent.net.ID))
                        {
                            SendReply(player, lang.GetMessage("NotTimeLocked", this, player.UserIDString), name);
                            return;
                        }

                        string type = storedData.IDlist[ent.net.ID];
                        string start = "", end = "";

                        if (type == "default")
                        {
                            string p1, p2, p3, p4;
                            p1 = (GetDateTime(startTime).Hour.ToString() == "0") ? "00" : GetDateTime(startTime).Hour.ToString();
                            p2 = (GetDateTime(startTime).Minute.ToString() == "0") ? "00" : GetDateTime(startTime).Minute.ToString();
                            p3 = (GetDateTime(endTime).Hour.ToString() == "0") ? "00" : GetDateTime(endTime).Hour.ToString();
                            p4 = (GetDateTime(endTime).Minute.ToString() == "0") ? "00" : GetDateTime(endTime).Minute.ToString();
                            start = p1 + ":" + p2;
                            end = p3 + ":" + p4;
                            SendReply(player, lang.GetMessage("InfoAboutDoor", this, player.UserIDString), name, type, start, end);
                            return;
                        }

                        foreach (var entry in storedData.TimeEntries)
                        {
                            if (entry.name == type)
                            {
                                start = entry.start;
                                end = entry.end;
                            }
                        }

                        SendReply(player, lang.GetMessage("InfoAboutDoor", this, player.UserIDString), name, type, start, end);
                        return;
                    }

                case "show":
                    {
                        if (storedData.IDlist.Count == 0)
                        {
                            SendReply(player, lang.GetMessage("NoLockedEnts", this, player.UserIDString));
                            return;
                        }

                        SendReply(player, lang.GetMessage("ShowingEnts", this, player.UserIDString));
                        foreach (var entry in storedData.IDlist)
                        {
                            BaseNetworkable EntityN = BaseNetworkable.serverEntities.Find(entry.Key);
                            if (EntityN == null)
                            {
                                CheckAllEntites();
                                continue;
                            }
                            Vector3 pos = EntityN.transform.position;
                            pos.y += 1f;
                            player.SendConsoleCommand("ddraw.sphere", 10f, Color.green, pos, 1f);
                        }
                        return;
                    }

                case "help":
                    {
                        string start, end;
                        string p1, p2, p3, p4;
                        p1 = (GetDateTime(startTime).Hour.ToString() == "0") ? "00" : GetDateTime(startTime).Hour.ToString();
                        p2 = (GetDateTime(startTime).Minute.ToString() == "0") ? "00" : GetDateTime(startTime).Minute.ToString();
                        p3 = (GetDateTime(endTime).Hour.ToString() == "0") ? "00" : GetDateTime(endTime).Hour.ToString();
                        p4 = (GetDateTime(endTime).Minute.ToString() == "0") ? "00" : GetDateTime(endTime).Minute.ToString();
                        start = p1 + ":" + p2;
                        end = p3 + ":" + p4;

                        SendReply(player, lang.GetMessage("ListOfCommands", this, player.UserIDString), start, end);
                        return;
                    }

                default:
                    {
                        SendReply(player, lang.GetMessage("SyntaxError", this, player.UserIDString));
                        return;
                    }
            }
        }
        #endregion
    }
}