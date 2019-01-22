using Oxide.Core;
using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("BanditHide", "mangiang", "1.0.6", ResourceId = 2470)]
    [Description("Hides name when wearing specific clothes")]
    class BanditHide : RustPlugin
    {
        #region Initialization
        StoredData storedData;

        /// <summary>
        /// New bandit Name
        /// </summary>
        string changedName = "Masked bandit";

        /// <summary>
        /// Default ChangedName 
        /// </summary>
        string DefaultChangedName = "Masked bandit";

        /// <summary>
        /// Items that hide the name
        /// </summary>
        List<string> masks = new List<string> {
        "mask",
        "burlap.headwrap"};

        /// <summary>
        /// Use Logs
        /// </summary>
        bool DebugMode = false;

        /// Permission
        string perm = "";
        #endregion

        /// <summary>
        /// Store the userId and the name of masked players
        /// </summary>
        class StoredData
        {
            public Dictionary<string, string> banditNames = new Dictionary<string, string>();

            public StoredData()
            {
            }
        }


        protected override void LoadDefaultConfig() => Puts("New configuration file created.");
        private void Init() => LoadConfigValues();

        /// <summary>
        /// Load bandit who already wear a mask
        /// </summary>
        void LoadConfigValues()
        {
            #region Use Logs
            string str = Config["Debug mode"] as string;
            if (str == null)
            {
                DebugMode = false;
                Config["Debug mode"] = DebugMode.ToString();
            }
            else
            {
                DebugMode = (str.Equals("True") ? true : false);
            }
            if (DebugMode)
            {
                Puts("Debug mode enabled");
                Log("Debug mode enabled");
            }
            #endregion

            #region Bandit Names
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>("BanditNames");
            if (DebugMode)
            {
                Log("Stored Data :");
                var enu = storedData.banditNames.GetEnumerator();
                while (enu.MoveNext())
                {
                    Log(enu.Current.Key + " => " + enu.Current.Value);
                }
            }
            #endregion

            #region Changed Name
            changedName = Config["Changed Name"] as string;
            if (changedName == null)
            {
                changedName = DefaultChangedName;
                Config["Changed Name"] = changedName;
            }

            if (changedName.Equals(""))
            {
                changedName = " ";
            }

            if (DebugMode)
            {
                Log("Changed Name : " + changedName);
            }
            #endregion

            #region Masks
            List<Object> tmpmasks = Config["Masks"] as List<Object>;
            if (tmpmasks == null)
            {
                masks = new List<string> { "mask", "burlap.headwrap" };
                Config["Masks"] = masks;
            }
            else
            {
                masks = new List<string>();
                foreach (Object mask in tmpmasks)
                {
                    masks.Add((string)mask);
                }
                Config["Masks"] = masks;
            }
            if (DebugMode)
            {
                foreach (string mk in masks)
                {
                    Log("Masks : " + mk);
                }
            }
            #endregion

            SaveConfig();
        }

        void OnServerInitialized()
        {
            /// Init the permission
            perm = this.Name.ToLowerInvariant() + ".use";
            /// Add permission bandithide.use
            permission.RegisterPermission(perm, this);

            LoadDefaultMessages();
        }

        void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Renamed"] = "You are renamed to ",
            }, this);

            // French
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Renamed"] = "Vous avez été renommé en ",
            }, this, "fr");
        }

        /// <summary>
        ///  Change back the name on respawn
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        object OnPlayerRespawned(BasePlayer player)
        {
            if (player.displayName.Equals(changedName) && permission.UserHasPermission(player.UserIDString, perm))
            {
                if (DebugMode)
                {
                    Log("Enter OnPlayerRespawned");
                }
                player.displayName = storedData.banditNames[player.UserIDString];
                if (DebugMode)
                {
                    Log("Renaming " + player.displayName + " (" + player.UserIDString + ") into " + changedName);
                }
                player.ChatMessage(lang.GetMessage("Renamed", this, player.UserIDString) + player.displayName);
                storedData.banditNames.Remove(player.UserIDString);
                player.Connection.username = player.displayName;
                player.SendNetworkUpdate();
                player.SendEntityUpdate();
                Interface.Oxide.DataFileSystem.WriteObject("BanditNames", storedData);
                if (DebugMode)
                {
                    Log("New name is " + player.displayName + " (" + player.UserIDString + ")");
                }
                if (DebugMode)
                {
                    Log("Exit OnPlayerRespawned");
                }
            }
            return null;
        }

        /// <summary>
        /// Change name on wake up if wearing a mask
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        object OnPlayerSleepEnded(BasePlayer player)
        {
            if (IsWearingAMask(player)
                && !storedData.banditNames.ContainsKey(player.UserIDString) && permission.UserHasPermission(player.UserIDString, perm))
            {
                if (DebugMode)
                {
                    Log("Enter OnPlayerSleepEnded");
                }
                if (!player.displayName.Equals(changedName))
                {
                    if (!storedData.banditNames.ContainsKey(player.UserIDString))
                        storedData.banditNames.Add(player.UserIDString, player.displayName);
                    if (DebugMode)
                    {
                        Log("Renaming " + player.displayName + " (" + player.UserIDString + ") into " + changedName);
                    }
                    player.displayName = changedName;
                    player.ChatMessage(lang.GetMessage("Renamed", this, player.UserIDString) + player.displayName);
                    player.Connection.username = changedName;
                    player.SendNetworkUpdate();
                    player.SendEntityUpdate();
                    Interface.Oxide.DataFileSystem.WriteObject("BanditNames", storedData);
                    if (DebugMode)
                    {
                        Log("New name is " + player.displayName + " (" + player.UserIDString + ")");
                    }
                }
                if (DebugMode)
                {
                    Log("Exit OnPlayerSleepEnded");
                }
            }

            return null;
        }

        /// <summary>
        /// Called when trying to wear an item
        /// </summary>
        /// <param name="inventory"></param>
        /// <param name="item"></param>
        /// <returns></returns>
        object CanWearItem(PlayerInventory inventory, Item item)
        {

            BasePlayer player = inventory.containerWear.GetOwnerPlayer();
            if (IsMask(item) && permission.UserHasPermission(player.UserIDString, perm))
            {
                if (DebugMode)
                {
                    Log("Enter CanWearItem");
                }
                if (!storedData.banditNames.ContainsKey(player.UserIDString))
                    storedData.banditNames.Add(player.UserIDString, player.displayName);

                if (DebugMode)
                {
                    Log("Renaming " + player.displayName + " (" + player.UserIDString + ") into " + changedName);
                }
                player.displayName = changedName;
                player.ChatMessage(lang.GetMessage("Renamed", this, player.UserIDString) + player.displayName);
                player.Connection.username = changedName;
                player.SendNetworkUpdate();
                player.SendEntityUpdate();
                Interface.Oxide.DataFileSystem.WriteObject("BanditNames", storedData);
                if (DebugMode)
                {
                    Log("New name is " + player.displayName + " (" + player.UserIDString + ")");
                }

                if (DebugMode)
                {
                    Log("Exit CanWearItem");
                }
            }

            return null;
        }

        /// <summary>
        /// Called when adding an item to a container
        /// </summary>
        /// <param name="container"></param>
        /// <param name="item"></param>
        /// <returns></returns>
        object OnItemAddedToContainer(ItemContainer container, Item item)
        {
            BasePlayer player = container.GetOwnerPlayer();
            if (player != null && permission.UserHasPermission(player.UserIDString, perm))
            {
                if (DebugMode)
                {
                    Log("Enter OnItemAddedToContainer");
                }
                // if the container is the "Wear Container"
                if (!storedData.banditNames.ContainsKey(player.UserIDString)
                    && player.inventory.containerWear == container)
                {
                    if (!player.displayName.Equals(changedName) && IsWearingAMask(player))
                    {
                        if (!storedData.banditNames.ContainsKey(player.UserIDString))
                            storedData.banditNames.Add(player.UserIDString, player.displayName);
                        if (DebugMode)
                        {
                            Log("Renaming " + player.displayName + " (" + player.UserIDString + ") into " + changedName);
                        }
                        player.displayName = changedName;
                        player.ChatMessage(lang.GetMessage("Renamed", this, player.UserIDString) + player.displayName);
                        player.Connection.username = changedName;
                        player.SendNetworkUpdate();
                        player.SendEntityUpdate();
                        Interface.Oxide.DataFileSystem.WriteObject("BanditNames", storedData);
                        if (DebugMode)
                        {
                            Log("New name is " + player.displayName + " (" + player.UserIDString + ")");
                        }
                    }
                }
                if (DebugMode)
                {
                    Log("Exit OnItemAddedToContainer");
                }
            }

            return null;
        }

        /// <summary>
        /// Called when removing an item from a container
        /// </summary>
        /// <param name="container"></param>
        /// <param name="item"></param>
        /// <returns></returns>
        object OnItemRemovedFromContainer(ItemContainer container, Item item)
        {
            BasePlayer player = container.GetOwnerPlayer();
            if (player != null && permission.UserHasPermission(player.UserIDString, perm))
            {
                if (DebugMode)
                {
                    Log("Enter OnItemRemovedFromContainer");
                }
                // if the container is the "Wear Container"
                if (storedData.banditNames.ContainsKey(player.UserIDString)
                && player.inventory.containerWear == container)
                {
                    if (player.displayName.Equals(changedName) && !IsWearingAMask(player))
                    {
                        if (DebugMode)
                        {
                            Log("Renaming " + player.displayName + " (" + player.UserIDString + ") into " + storedData.banditNames[player.UserIDString]);
                        }
                        player.displayName = storedData.banditNames[player.UserIDString];
                        player.ChatMessage(lang.GetMessage("Renamed", this, player.UserIDString) + player.displayName);
                        player.Connection.username = player.displayName;
                        player.SendNetworkUpdate();
                        player.SendEntityUpdate();
                        storedData.banditNames.Remove(player.UserIDString);
                        Interface.Oxide.DataFileSystem.WriteObject("BanditNames", storedData);
                        if (DebugMode)
                        {
                            Log("New name is " + player.displayName + " (" + player.UserIDString + ")");
                        }
                    }
                }
                if (DebugMode)
                {
                    Log("Exit OnItemRemovedFromContainer");
                }
            }
            return null;
        }

        bool IsWearingAMask(BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, perm))
            {
                foreach (Item it in player.inventory.containerWear.itemList)
                {
                    if (DebugMode)
                    {
                        Log("Item : " + it.info.displayName.english);
                    }
                    foreach (string maskName in masks)
                    {
                        if (DebugMode)
                        {
                            Log("Mask : " + maskName);
                        }
                        if (it.info.name.Contains(maskName))
                        {
                            if (DebugMode)
                            {
                                Log(player.displayName + "(" + player.UserIDString + ") is wearing a " + maskName);
                            }
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        bool IsMask(Item item)
        {
            if (DebugMode)
            {
                Log("Item : " + item.info.shortname);
            }
            foreach (string maskName in masks)
            {
                if (DebugMode)
                {
                    Log("Mask : " + maskName);
                }
                if (item.info.shortname.Contains(maskName))
                {
                    if (DebugMode)
                    {
                        Log("It is a mask");
                    }
                    return true;
                }
            }
            if (DebugMode)
            {
                Log("It is not a mask");
            }
            return false;
        }

        void Log(string text)
        {
            LogToFile("logs", $"[{DateTime.Now}] {text}", this);
        }
    }
}