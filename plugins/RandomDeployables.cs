using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Newtonsoft.Json;
using System.Reflection;
using Facepunch.Steamworks;
using Rust;

namespace Oxide.Plugins
{
    [Info("RandomDeployables", "Norn", "0.1.10", ResourceId = 2187)]
    [Description("Randomize deployable skins")]
    public class RandomDeployables : RustPlugin
    {
        private static Dictionary<string, int> deployedToItem = new Dictionary<string, int>();

        private void InitializeTable()
        {
            deployedToItem.Clear();
            List<ItemDefinition> ItemsDefinition = ItemManager.GetItemDefinitions() as List<ItemDefinition>;
            foreach (ItemDefinition itemdef in ItemsDefinition) { if (itemdef.GetComponent<ItemModDeployable>() != null && !deployedToItem.ContainsKey(itemdef.GetComponent<ItemModDeployable>().entityPrefab.resourcePath)) deployedToItem.Add(itemdef.GetComponent<ItemModDeployable>().entityPrefab.resourcePath, itemdef.itemid); }
        }

        private void OnServerInitialized()
        {
            InitializeTable();
            permission.RegisterPermission("randomdeployables.able", this);
            webrequest.Enqueue("http://s3.amazonaws.com/s3.playrust.com/icons/inventory/rust/schema.json", null, PullWorkshopIDS, this);
        }

        Dictionary<ulong, ulong> SkinFromInventoryID = new Dictionary<ulong, ulong>();

        private void PullWorkshopIDS(int code, string response)
        {
            if (response != null && code == 200)
            {
                SkinFromInventoryID.Clear();
                ulong wsdl;
                var schema = JsonConvert.DeserializeObject<Rust.Workshop.ItemSchema>(response);
                var defs = new List<Inventory.Definition>();
                foreach (var item in schema.items)
                {
                    if (string.IsNullOrEmpty(item.itemshortname)) continue;
                    if (item.workshopdownload == null) { wsdl = 0; } else { wsdl = Convert.ToUInt64(item.workshopdownload); } SkinFromInventoryID.Add(item.itemdefid, wsdl);
                    var steamItem = Global.SteamServer.Inventory.CreateDefinition((int)item.itemdefid);
                    steamItem.Name = item.name;
                    steamItem.SetProperty("itemshortname", item.itemshortname);
                    steamItem.SetProperty("workshopid", item.workshopid);
                    steamItem.SetProperty("workshopdownload", item.workshopdownload);
                    defs.Add(steamItem);
                }
                Global.SteamServer.Inventory.Definitions = defs.ToArray();
                foreach (var item in ItemManager.itemList)
                    item._skins2 = Global.SteamServer.Inventory.Definitions.Where(x => (x.GetStringProperty("itemshortname") == item.shortname) && !string.IsNullOrEmpty(x.GetStringProperty("workshopdownload"))).ToArray();
                Puts($"Pulled {SkinFromInventoryID.Count} skins.");
            }
            else
            {
                PrintWarning($"Failed to pull skins... Error {code}");
            }
        }

        protected override void LoadDefaultConfig()
        {
            Config["Settings", "AllowDefaultSkin"] = false;
        }

        private List<int> GetSkins(ItemDefinition def)
        {
            List<int> skins;
            skins = new List<int>();
            if(Convert.ToBoolean(Config["Settings", "AllowDefaultSkin"])) { skins.Add(0); }
            if (def.skins != null) skins.AddRange(def.skins.Select(skin => skin.id));
            if (def.skins2 != null) skins.AddRange(def.skins2.Select(skin => skin.Id));
            return skins;
        }

        private void OnEntityBuilt(Planner planner, GameObject gameObject)
        {
            BaseEntity e = gameObject.ToBaseEntity();
            BasePlayer player = planner.GetOwnerPlayer();
            if (permission.UserHasPermission(player.net.connection.userid.ToString(), "randomdeployables.able"))
            {
                if (!(e is BaseEntity) || player == null) { return; }
                if (deployedToItem.ContainsKey(e.PrefabName))
                {
                    var def = ItemManager.FindItemDefinition(deployedToItem[e.PrefabName]);
                    var skins = GetSkins(def);
                    if (skins.Count == 0) return;
                    ulong skinid = Convert.ToUInt64(skins.GetRandom());
                    if (skinid != 0 && SkinFromInventoryID.ContainsKey(skinid) && SkinFromInventoryID[skinid] != 0) { e.skinID = SkinFromInventoryID[skinid]; }
                    else { e.skinID = skinid; }
                    e.SendNetworkUpdate();
                }
            }
        }
    }
}