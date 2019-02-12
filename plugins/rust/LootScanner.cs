using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Loot Scanner", "Sorrow", "0.3.0")]
    [Description("Allow player to scan loot container with Binoculars")]

    class LootScanner : RustPlugin
    {
        #region Fields
        [PluginReference]
        Plugin PopupNotifications;

        private const int BinocularsId = -1262185308;
        private const string SupplyDrop = "supply_drop";
        private const string CH47Crate = "codelockedhackablecrate";
        private static readonly Dictionary<string, string> PrefabList = new Dictionary<string, string>();

        private const string PermissionWorld = "lootscanner.world";
        private const string PermissionPlayer = "lootscanner.player";
        private const string PermissionSupplyDrop = "lootscanner.supplydrop";
        private const string PermissionCH47Crate = "lootscanner.ch47crate";

        internal static string _sideOfGui;
        internal static float _positionY;
        internal static string _colorNone;
        internal static string _colorCommon;
        internal static string _colorUncommon;
        internal static string _colorRare;
        internal static string _colorVeryRare;
        internal static bool _hideAirdrop;
        internal static bool _hideCH47Crate;
        internal static bool _hideCrashsiteCrate;
        internal static bool _hideCommonLootableCrate;
        #endregion

        #region uMod Hooks
        private void OnServerInitialized()
        {
            InitPrefabList();

            permission.RegisterPermission(PermissionWorld, this);
            permission.RegisterPermission(PermissionPlayer, this);
            permission.RegisterPermission(PermissionSupplyDrop, this);
            permission.RegisterPermission(PermissionCH47Crate, this);

            _sideOfGui = Convert.ToString(Config["Define the side of GUI (Left - Right)"]);
            _positionY = Convert.ToSingle(Config["Define the y position of GUI"]);
            _colorNone = Convert.ToString(Config["Color None"]);
            _colorCommon = Convert.ToString(Config["Color Common"]);
            _colorUncommon = Convert.ToString(Config["Color Uncommon"]);
            _colorRare = Convert.ToString(Config["Color Rare"]);
            _colorVeryRare = Convert.ToString(Config["Color Very Rare"]);
            _hideAirdrop = Convert.ToBoolean(Config["Hide Airdrop's content"]);
            _hideCH47Crate = Convert.ToBoolean(Config["Hide CH47 crate content"]);
            _hideCrashsiteCrate = Convert.ToBoolean(Config["Hide heli and Bradley crate content"]);
            _hideCommonLootableCrate = Convert.ToBoolean(Config["Hide common lootable container"]);
        }

        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (player == null || input == null || !input.IsDown(BUTTON.FIRE_SECONDARY) && player.GetActiveItem()?.info.itemid == BinocularsId)
            {
                CuiHelper.DestroyUi(player, player.UserIDString);
                return;
            }

            if (input.WasJustPressed(BUTTON.USE) && player.GetActiveItem()?.info.itemid == BinocularsId)
            {
                var entity = GetEntityScanned(player, input);
                if (entity == null) return;

                var storage = entity.GetComponent<StorageContainer>();
                if (storage == null) return;


                if (entity.OwnerID != 0 && permission.UserHasPermission(player.UserIDString, PermissionPlayer) ||
                    entity.ShortPrefabName == SupplyDrop &&
                    permission.UserHasPermission(player.UserIDString, PermissionSupplyDrop) ||
                    entity.ShortPrefabName == CH47Crate &&
                    permission.UserHasPermission(player.UserIDString, PermissionCH47Crate) ||
                    entity.OwnerID == 0 && permission.UserHasPermission(player.UserIDString, PermissionWorld))
                {
                    var itemDefinition = ItemManager.FindItemDefinition(entity.ShortPrefabName);

                    var storageName = itemDefinition != null
                        ? itemDefinition.displayName.english
                        : GetPrefabName(entity);

                    var title = "<color=orange>[Loot Scanner]</color>\n> " + storageName + " <";

                    if (storage.inventory.itemList.Count > 0)
                    {
                        var scannerMessage = "";
                        if (entity.ShortPrefabName == SupplyDrop && _hideAirdrop || entity.ShortPrefabName == CH47Crate && _hideCH47Crate ||
                            LootContainer.spawnType.CRASHSITE.Equals(entity.GetComponent<LootContainer>()?.SpawnType) && _hideCrashsiteCrate ||
                            (LootContainer.spawnType.TOWN.Equals(entity.GetComponent<LootContainer>()?.SpawnType) || LootContainer.spawnType.ROADSIDE.Equals(entity.GetComponent<LootContainer>()?.SpawnType)) 
                            && _hideCommonLootableCrate)
                        {
                            scannerMessage = BuildRarityScannerMessage(scannerMessage, storage.inventory.itemList);
                        } else
                        {
                            foreach (var item in storage.inventory.itemList)
                            {
                                scannerMessage = BuildScannerMessage(scannerMessage, item);
                            }
                        }


                        var ui = UI.ConstructScanUi(player, title, scannerMessage);
                        CuiHelper.DestroyUi(player, player.UserIDString);
                        CuiHelper.AddUi(player, ui);
                    }
                    else
                    {
                        var ui = UI.ConstructScanUi(player, title, "> " + string.Format(lang.GetMessage("EmptyStorage", this, player.UserIDString), storageName));
                        CuiHelper.DestroyUi(player, player.UserIDString);
                        CuiHelper.AddUi(player, ui);
                    }
                }
                else
                {
                    var permissions = permission.GetUserPermissions(player.UserIDString)
                        .Where(p => p.Contains("lootscanner")).ToList();

                    if (permissions.Count > 1)
                    {
                        SendInfoMessage(player, string.Format(lang.GetMessage("TwoPermissions", this, player.UserIDString), BeautifyPermissionName(permissions[0]), BeautifyPermissionName(permissions[1])));
                    }
                    else if (permissions.Count == 1)
                    {
                        SendInfoMessage(player, string.Format(lang.GetMessage("OnePermission", this, player.UserIDString), BeautifyPermissionName(permissions[0])));
                    }
                    else
                    {
                        SendInfoMessage(player, lang.GetMessage("NoPermissions", this, player.UserIDString));
                    }
                }
            }
        }

        private void OnPlayerActiveItemChanged(BasePlayer player, Item oldItem, Item newItem)
        {
            if (newItem?.info.itemid == BinocularsId && 
                (permission.UserHasPermission(player.UserIDString, PermissionWorld) || 
                permission.UserHasPermission(player.UserIDString, PermissionPlayer) || 
                permission.UserHasPermission(player.UserIDString, PermissionSupplyDrop) ||
                permission.UserHasPermission(player.UserIDString, PermissionCH47Crate)))
            {
                SendInfoMessage(player, string.Format(lang.GetMessage("InfoMessage", this, player.UserIDString), BUTTON.USE));
            }

            if (oldItem?.info.itemid == BinocularsId)
            {
                CuiHelper.DestroyUi(player, player.UserIDString);
            }
        }

        #endregion

        #region Helpers
        private static void InitPrefabList()
        {
            foreach (var item in ItemManager.GetItemDefinitions())
            {
                var itemModDeployable = item?.GetComponent<ItemModDeployable>();
                if (itemModDeployable == null) continue;

                var resourcePath = itemModDeployable.entityPrefab.resourcePath;
                var name = SplitPrefabName(resourcePath);
                if (!PrefabList.ContainsKey(name)) PrefabList.Add(name, item.displayName.english);
            }
        }

        private static string BuildScannerMessage(string scannerMessage, Item item)
        {
            var sb = new StringBuilder();

            sb.Append(scannerMessage);
            sb.Append(" > ");
            sb.Append("<color="+ GetColorOfItem(item) + ">");
            sb.Append(item.info.displayName.english);
            sb.Append("</color>");
            if (item.amount > 1)
            {
                sb.Append(" ");
                sb.Append("x");
                sb.Append(item.amount);
            }
            sb.Append("\n");
            return sb.ToString();
        }

        private static string BuildRarityScannerMessage(string scannerMessage, List<Item> itemList)
        {
            var none = 0;
            var common = 0;
            var uncommon = 0;
            var rare = 0;
            var veryRare = 0;

            foreach(var item in itemList)
            {
                switch((int)item?.info?.rarity)
                {
                    case 0:
                        none += 1;
                        break;
                    case 1:
                        common += 1;
                        break;
                    case 2:
                        uncommon += 1;
                        break;
                    case 3:
                        rare += 1;
                        break;
                    case 4:
                        veryRare += 1;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            var sb = new StringBuilder();
            sb.Append(scannerMessage);

            if (veryRare > 0)
            {
                var item = veryRare > 1 ? "Items" : "Item";
                sb.Append(" > ");
                sb.Append("<color=" + _colorVeryRare + ">");
                sb.Append("Very Rare " + item);
                sb.Append("</color>");
                sb.Append(" ");
                sb.Append("x");
                sb.Append(veryRare);
                sb.Append("\n");
            }

            if (rare > 0)
            {
                var item = rare > 1 ? "Items" : "Item";
                sb.Append(" > ");
                sb.Append("<color=" + _colorRare + ">");
                sb.Append("Rare " + item);
                sb.Append("</color>");
                sb.Append(" ");
                sb.Append("x");
                sb.Append(rare);
                sb.Append("\n");
            }

            if (uncommon > 0)
            {
                var item = uncommon > 1 ? "Items" : "Item";
                sb.Append(" > ");
                sb.Append("<color=" + _colorUncommon + ">");
                sb.Append("Uncommon " + item);
                sb.Append("</color>");
                sb.Append(" ");
                sb.Append("x");
                sb.Append(uncommon);
                sb.Append("\n");
            }

            if (common > 0)
            {
                var item = common > 1 ? "Items" : "Item";
                sb.Append(" > ");
                sb.Append("<color=" + _colorCommon + ">");
                sb.Append("Common " + item);
                sb.Append("</color>");
                sb.Append(" ");
                sb.Append("x");
                sb.Append(common);
                sb.Append("\n");
            }

            if (none > 0)
            {
                var item = none > 1 ? "Items" : "Item";
                sb.Append(" > ");
                sb.Append("<color=" + _colorNone + ">");
                sb.Append("None " + item);
                sb.Append("</color>");
                sb.Append(" ");
                sb.Append("x");
                sb.Append(none);
                sb.Append("\n");
            }


            return sb.ToString();
        }

        private void SendInfoMessage(BasePlayer player, string message)
        {
            player?.SendConsoleCommand("gametip.showgametip", message);
            timer.Once(3f, () => player?.SendConsoleCommand("gametip.hidegametip"));
        }

        private static string GetColorOfItem(Item item)
        {
            var color = _colorNone;
            switch ((int)item?.info?.rarity)
            {
                case 0:
                    color = _colorNone;
                    break;
                case 1:
                    color = _colorCommon;
                    break;
                case 2:
                    color = _colorUncommon;
                    break;
                case 3:
                    color = _colorRare;
                    break;
                case 4:
                    color = _colorVeryRare;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            return color;
        }

        private static BaseEntity GetEntityScanned(BasePlayer player, InputState input) // Thanks to ignignokt84
        {
            // Get player position + 1.6y as eye-level
            var playerEyes = player.transform.position + new Vector3(0f, 1.6f, 0f);
            var direction = Quaternion.Euler(input.current.aimAngles) * Vector3.forward;
            // Raycast in the direction the player is looking
            var hits = Physics.RaycastAll(playerEyes, direction);
            // Maximum distance when player can use loot scanner
            var closest = 10000f;
            var target = Vector3.zero;
            Collider collider = null;
            // Find the closest hit
            foreach (var hit in hits)
            {
                var name = hit.collider.gameObject.name;
                if (hit.collider.gameObject.layer == 18 || hit.collider.gameObject.layer == 29) // Skip Triggers layer
                    continue;
                // Ignore zones, meshes, and landmark nobuild hits
                if (name.StartsWith("Zone Manager") ||
                    name == "prevent_building" ||
                    name == "preventBuilding" ||
                    name == "Mesh")
                    continue;

                if (!(hit.distance < closest)) continue;
                closest = hit.distance;
                target = hit.point;
                collider = hit.collider;
            }
            if (target == Vector3.zero) return null;
            var entity = collider?.gameObject.ToBaseEntity();
            return entity;
        }

        private static string GetPrefabName(BaseEntity entity)
        {
            var name = SplitPrefabName(entity.gameObject.name).Replace("static", "deployed");

            return PrefabList.ContainsKey(name) ? PrefabList[name] : BeautifyPrefabName(entity.ShortPrefabName);
        }

        private static string SplitPrefabName(string prefabName)
        {
            return prefabName.Split('/').Last();
        }

        private static string BeautifyPrefabName(string str)
        {
            str = Regex.Replace(str, "[0-9\\(\\)]", string.Empty).Replace('_', ' ').Replace('-', ' ').Replace('.', ' ').Replace("static", string.Empty);
            var textInfo = new CultureInfo("en-US").TextInfo;
            return textInfo.ToTitleCase(str).Trim();
        }

        private static string BeautifyPermissionName(string str)
        {
            return str.Split('.').Last();
        }

        #endregion

        #region UI
        private class UI
        {
            private static CuiElementContainer CreateElementContainer(string name, string color, string anchorMin, string anchorMax, string parent = "Overlay")
            {
                var elementContainer = new CuiElementContainer()
                {
                    new CuiElement()
                    {
                        Name = name,
                        Parent = parent,
                        Components =
                        {
                            new CuiImageComponent()
                            {
                                Color = color,
                            },
                            new CuiRectTransformComponent()
                            {
                                AnchorMin = anchorMin,
                                AnchorMax = anchorMax
                            }
                        }
                    },
                };
                return elementContainer;
            }

            private static void CreateLabel(string name, string parent, ref CuiElementContainer container, TextAnchor textAnchor, string text, string color, int fontSize, string anchorMin, string anchorMax)
            {
                container.Add(new CuiElement()
                {
                    Name = name,
                    Parent = parent,
                    Components =
                    {
                        new CuiTextComponent()
                        {
                            Text = text,
                            Align = textAnchor,
                            FontSize = fontSize,
                            Font = "droidsansmono.ttf",
                            Color = color

                        },
                        new CuiRectTransformComponent()
                        {
                            AnchorMin = anchorMin,
                            AnchorMax = anchorMax
                        }
                    }
                });
            }

            private static void CreateElement(string name, string parent, ref CuiElementContainer container, string anchorMin, string anchorMax, string color)
            {
                container.Add(new CuiElement()
                {
                    Name = name,
                    Parent = parent,
                    Components =
                    {
                        new CuiImageComponent()
                        {
                            Color = color
                        },
                        new CuiRectTransformComponent()
                        {
                            AnchorMin = anchorMin,
                            AnchorMax = anchorMax
                        }
                    }
                });
            }

            public static CuiElementContainer ConstructScanUi(BasePlayer player, string title, string message)
            {
                var height = 0.66f;
                var anchorX = "0.78" + " " + (_positionY - height);
                var anchorY = "0.990" + " " + _positionY;

                if (_sideOfGui.Equals("Left", StringComparison.InvariantCultureIgnoreCase))
                {
                    anchorX = "0.01" + " " + (_positionY - height);
                    anchorY = "0.22" + " " + _positionY;
                }

                var container = CreateElementContainer(player.UserIDString, "1 1 1 0.0", anchorX, anchorY); 
                CreateElement("uiLabel", player.UserIDString, ref container, "0.05 0.05", "0.95 0.95", "0.3 0.3 0.3 0.0");
                CreateElement("uiLabelPadded", "uiLabel", ref container, "0.05 0.05", "0.95 0.95", "0 0 0 0");
                CreateLabel("uiLabelText", "uiLabelPadded", ref container, TextAnchor.MiddleCenter, title, "0.98 0.996 0.98 1", 14, "0 0.80", "1 1");
                CreateLabel("uiLabelText", "uiLabelPadded", ref container, TextAnchor.UpperLeft, message, "0.98 0.996 0.98 1", 11, "0 0", "1 0.80");

                return container;
            }
        }
        #endregion

        #region Config
        private new void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            Config.Clear();

            Config["Color None"] = "white";
            Config["Color Common"] = "#56c63f";
            Config["Color Uncommon"] = "#0097ff";
            Config["Color Rare"] = "#b675f3";
            Config["Color Very Rare"] = "#ffbf17";

            Config["Define the side of GUI (Left - Right)"] = "Right";
            Config["Define the y position of GUI"] = 0.98f;

            Config["Hide Airdrop's content"] = false;
            Config["Hide CH47 crate content"] = false;
            Config["Hide heli and Bradley crate content"] = false;
            Config["Hide common lootable container"] = false;

            SaveConfig();
        }
        #endregion

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"InfoMessage", "Press {0} while looking through the binoculars to scan a container."},
                {"TwoPermissions", "You're only allowed to use Loot Scanner on {0}'s and {1}'s loot containers."},
                {"OnePermission", "You're only allowed to use Loot Scanner on {0}'s loot containers."},
                {"NoPermissions", "You're not allowed to use Loot Scanner."},
                {"EmptyStorage", "{0} is empty."},
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"InfoMessage", "Appuyez sur {0} tout en regardant à travers les jumelles pour scanner un conteneur."},
                {"TwoPermissions", "Vous n'êtes autorisé à utiliser Loot Scanner que sur les conteneurs de butin de {0} et {1}."},
                {"OnePermission", "Vous n'êtes autorisé à utiliser Loot Scanner que sur les conteneurs de butin de {0}."},
                {"NoPermissions", "Vous n'êtes pas autorisé à utiliser Loot Scanner."},
                {"EmptyStorage", "{0} est vide."},
            }, this, "fr");
        }

        #endregion
    }
}
