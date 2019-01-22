/*
Copyright 2017, João Pinto <lamego.pinto@gmail.com>

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

/* 
    Plugin logic:
    a) Spawn a large wooden box at a random location
    b) Create an AK using a workshop skin id (golden alike) and place it on the container
    c) Track the AK position and mark it on the map, so that other players attempt to capture it
*/

// Requires: GUIAnnouncements

using System;
using System.Collections.Generic;
using System.Linq;
using Facepunch;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Rust;
using UnityEngine;


namespace Oxide.Plugins
{
    [Info("GoldenAKChallenge", "Lamego", "0.0.2")]

    class GoldenAKChallenge : RustPlugin
    {

        [PluginReference]
        private Plugin GUIAnnouncements;

        const int AK_ITEM_ID = -1461508848;
        const ulong GOLDEN_AK_SKIN_ID = 1167207039;

        private static GoldenAKChallenge plugin;

        #region AK tracking
        private BasePlayer holdingPlayer = null;    
        private DroppedItem droppedAK = null;    /* We need to keep this to destroy the tracker  */
        private Item goldenAK = null;
        private StorageContainer containerAK = null;
        private ulong currentOwnerID = 0;   /* UID of the last user holding the AK */
        private static MapMarkerGenericRadius mapMarker = null;
        #endregion

        #region Game Data
        class StoredData
        {
            public ulong currentOwnerID = 0;
            public uint  AK_ID = 0;
            public StoredData()
            {
            }
        }        
        StoredData gameData;
        #endregion

        Vector3 lastMarkerPos = Vector3.zero;   
        private Timer _timer;

        private class Tracker : MonoBehaviour
        {
            Transform targetTansform;
            float lastX = 0;
            float lastZ = 0;

            private void Awake()
            {
                targetTansform = gameObject.transform;
            }

            private void Update()
            {
                float currentX = targetTansform.position.x;
                float currentZ = targetTansform.position.z;
                if(targetTansform.hasChanged && ((currentX != lastX) || (currentZ != lastZ)))
                {
                    plugin.SetMapMarker(targetTansform.position);
                    lastX = currentX;
                    lastZ = currentZ;
                }
            }
        }   

        StorageContainer CreateLargeBox(Vector3 position)
        {
            const string boxShortname = "box.wooden.large";
            const string boxPrefab = "assets/prefabs/deployable/large wood storage/box.wooden.large.prefab";        
            ItemDefinition boxDef = ItemManager.FindItemDefinition(boxShortname);                
            StorageContainer box = GameManager.server.CreateEntity(boxPrefab, position, new Quaternion(), true) as StorageContainer;            
            box.Spawn();
            return box;
        }

        public Vector3 GetEventPosition()
        {
            int maxRetries = 500;
            Vector3 localeventPos;

            int blockedMask = LayerMask.GetMask(new[] { "Player (Server)", "Trigger", "Prevent Building" });
            List<int> BlockedLayers = new List<int> { (int)Layer.Water, (int)Layer.Construction, (int)Layer.Trigger, (int)Layer.Prevent_Building, (int)Layer.Deployed, (int)Layer.Tree };

            List<Vector3> monuments = new List<Vector3>(); // positions of monuments on the server
            monuments = UnityEngine.Object.FindObjectsOfType<MonumentInfo>().Select(monument => monument.transform.position).ToList();

            do
            {                
                localeventPos = GetSafeDropPosition(RandomDropPosition());

                if (Interface.CallHook("OnGAKOpen",localeventPos) != null)
                {
                    localeventPos = Vector3.zero;
                    continue;
                }

                foreach (var monument in monuments)
                {
                    if (Vector3.Distance(localeventPos, monument) < 150f) // don't put the treasure chest near a monument
                    {
                        localeventPos = Vector3.zero;
                        break;
                    }
                }
            } while (localeventPos == Vector3.zero && --maxRetries > 0);

            return localeventPos;
        }

        public Vector3 GetSafeDropPosition(Vector3 position)
        {
            var eventRadius = 25f; /* */
            RaycastHit hit;
            position.y += 200f;
            int blockedMask = LayerMask.GetMask(new[] { "Player (Server)", "Trigger", "Prevent Building" });
            List<int> BlockedLayers = new List<int> { (int)Layer.Water, (int)Layer.Construction, (int)Layer.Trigger, (int)Layer.Prevent_Building, (int)Layer.Deployed, (int)Layer.Tree };

            if (Physics.Raycast(position, Vector3.down, out hit))
            {
                if (!BlockedLayers.Contains(hit.collider?.gameObject?.layer ?? BlockedLayers[0]))
                {
                    position.y = Mathf.Max(hit.point.y, TerrainMeta.HeightMap.GetHeight(position));

                    var colliders = Pool.GetList<Collider>();
                    Vis.Colliders(position, eventRadius, colliders, blockedMask, QueryTriggerInteraction.Collide);

                    bool blocked = colliders.Count > 0;

                    Pool.FreeList<Collider>(ref colliders);

                    if (!blocked)
                        return position;
                }
            }

            return Vector3.zero;
        }

        public Vector3 RandomDropPosition() // CargoPlane.RandomDropPosition()
        {
            var vector = Vector3.zero;
            SpawnFilter filter = new SpawnFilter();

            float num = 100f, x = TerrainMeta.Size.x / 3f;
            do
            {
                vector = Vector3Ex.Range(-x, x);
            }
            while (filter.GetFactor(vector) == 0f && (num -= 1f) > 0f);
            vector.y = 0f;
            return vector;
        }



        public Vector3 GAKChestEvent(BasePlayer player = null) {
            Vector3 eventPos;
            var randomPos = GetEventPosition();                        
            if (randomPos == Vector3.zero)
            {
                PrintError("Unable to spaw randomPos");
                return Vector3.zero;
            }

            eventPos = randomPos;
            Puts("Spawned Golden AK box at "+eventPos.ToString());

            StorageContainer container = CreateLargeBox(eventPos);        
            if (!container)
            {
                PrintError("Unable to spaw container");
                return Vector3.zero;
            }

            goldenAK = ItemManager.CreateByItemID(AK_ITEM_ID, 1);     
            var weapon = goldenAK.GetHeldEntity() as BaseProjectile;                   
            ulong skin_id = 0;
            skin_id = GOLDEN_AK_SKIN_ID;
            goldenAK.skin = skin_id;                
            if (goldenAK.GetHeldEntity() != null) 
            { 
                goldenAK.GetHeldEntity().skinID  = skin_id;
            }
            container.inventory.Clear();
            weapon.primaryMagazine.contents = weapon.primaryMagazine.capacity;
            weapon.SendNetworkUpdateImmediate();            
            goldenAK.MoveToContainer(container.inventory, -1, true);                        
            container.inventory.MarkDirty();      
            containerAK = container;
            containerAK.gameObject.AddComponent<Tracker>();
            return container.transform.position;
        }

        object OnItemPickup(Item item, BasePlayer player)        
        {

            if(item != goldenAK)   /* We only care about our ak */
                return null;

            DestroyAnyTracker();
            holdingPlayer = player;
            containerAK = null;
            holdingPlayer.gameObject.AddComponent<Tracker>();            

            if(player.userID != currentOwnerID)                      
            {
                currentOwnerID = player.userID;    
                string msg = Lang("NotAllowedPerm", player.displayName);
                GUIAnnouncements?.Call("CreateAnnouncement", msg, "gray", "white", player);
            }            
            
            return null;
        }

        void OnItemDropped(Item item, BaseEntity entity)
        {

            if(item == goldenAK)
            {
                DestroyAnyTracker();                
                holdingPlayer = null;
                droppedAK = FindOnDroppedItems(item.uid);
                droppedAK.gameObject.AddComponent<Tracker>();
            }
        }
        

        void OnLootEntityEnd(BasePlayer player, BaseCombatEntity entity)
        {
            foreach (Item i in player.inventory.FindItemIDs(AK_ITEM_ID))
            {
                if(i == goldenAK)
                {               
                    DestroyAnyTracker();                    
                    holdingPlayer = player;           
                    holdingPlayer.gameObject.AddComponent<Tracker>();   
                    containerAK = null;
                    if(player.userID != currentOwnerID) { /* Owner change */
                        currentOwnerID = player.userID;                    
                        string msg = Lang("Picked", player.UserIDString, player.displayName);
                        GUIAnnouncements?.Call("CreateAnnouncement", msg, "gray", "white", player);
                    }
                    return;                    
                }
            }    

            // Holding player no longer holds it
            if (player == holdingPlayer)
            {
                if(entity is StorageContainer)
                {                    
                    DestroyAnyTracker();                    
                    containerAK = entity as StorageContainer;
                    containerAK.gameObject.AddComponent<Tracker>();
                    holdingPlayer = null;
                }
            }

        }

        // Don't allow AK to be placed on destrutible containers
        object CanAcceptItem(ItemContainer container, Item item)
        {
            if(item == goldenAK)
            {
                var entityOwner = container.entityOwner;
                if(entityOwner is Recycler || entityOwner is ResearchTable || entityOwner is LootContainer )
                    return ItemContainer.CanAcceptResult.CannotAccept;
            }
            return null;
        }

        DroppedItem FindOnDroppedItems(uint uid)
        {
            var dropped_items = UnityEngine.Object.FindObjectsOfType<DroppedItem>();
            foreach(DroppedItem item in dropped_items)
            {
                if(item.GetItem().uid == uid)
                {
                    return item;
                }
            }
            return null;
        }


        void OnEntityDeath(BaseCombatEntity victim, HitInfo info)        
        {
            if(holdingPlayer == null)
                return;

            var corpse = victim as BaseCorpse;
            var player = victim.ToPlayer();
            
            if (player == holdingPlayer)
            {
                DestroyAnyTracker();
                SetMapMarker(player.transform.position);
                holdingPlayer = null;
            }  
        }

        void OnServerInitialized()
        {

            plugin = this;       
            UpdateTrackingObjects();

            if(goldenAK == null)
            {
                // Started a new game
                GAKChestEvent();
            } else
                Puts("Resumed game from data");
            _timer = timer.Every(1, MapMarkerRefresh);
        }

        private void MapMarkerRefresh()
        {
            UpdateMarker(lastMarkerPos);
        }

        private void UpdateTrackingObjects()
        {
            currentOwnerID = gameData.currentOwnerID;
            if(gameData.AK_ID == 0)
                return;

            DestroyAnyTracker();

            // Search the AK on actives players inventory
            foreach(BasePlayer player in BasePlayer.activePlayerList)
            {
                foreach (Item i in player.inventory.FindItemIDs(AK_ITEM_ID))
                {
                    if(i.uid == gameData.AK_ID)
                    {
                        Puts("Found player holding AK ");
                        holdingPlayer = player;                        
                        holdingPlayer.gameObject.AddComponent<Tracker>();
                        currentOwnerID = player.userID;
                        goldenAK = i;
                        containerAK = null;
                        droppedAK = null;
                        return;
                    }
                }
            }

            // Search the AK on sleeping players inventory
            foreach(BasePlayer player in BasePlayer.sleepingPlayerList)
            {
                foreach (Item i in player.inventory.FindItemIDs(AK_ITEM_ID))
                {
                    if(i.uid == gameData.AK_ID)
                    {
                        Puts("Found sleeping player holding AK");                        
                        holdingPlayer = player;                        
                        holdingPlayer.gameObject.AddComponent<Tracker>();
                        currentOwnerID = player.userID;
                        goldenAK = i;
                        containerAK = null;
                        droppedAK = null;
                        return;
                    }
                }
            }

            // Search the AK on dropped items
            droppedAK = FindOnDroppedItems(gameData.AK_ID);
            if(droppedAK != null)
            {
                Puts("Found AK on droppedItem");
                droppedAK.gameObject.AddComponent<Tracker>();
                goldenAK = droppedAK.GetItem();          
                holdingPlayer = null;
                containerAK = null;
                return;
            }

            // Search the AK on containers
            foreach(StorageContainer container in UnityEngine.Object.FindObjectsOfType<StorageContainer>())
            {
                foreach (Item i in container.inventory.FindItemsByItemID(AK_ITEM_ID))
                {
                    if(i.uid == gameData.AK_ID)
                    {
                        Puts("Found AK on container");
                        containerAK = container;                        
                        containerAK.gameObject.AddComponent<Tracker>();
                        currentOwnerID = gameData.currentOwnerID;
                        holdingPlayer = null;
                        goldenAK = i;
                        return;
                    }
                }
            }
        }    

        private void SetMapMarker(Vector3 position)
        {
            lastMarkerPos = position;
        }

        private void UpdateMarker(Vector3 position)
        {
            mapMarker?.Kill();
            mapMarker = GameManager.server.CreateEntity("assets/prefabs/tools/map/genericradiusmarker.prefab", position) as MapMarkerGenericRadius;
            mapMarker.alpha = 0.8f;
            mapMarker.color1 = Color.red;
            mapMarker.color2 = Color.red;
            mapMarker.radius = 2;
            mapMarker.Spawn();
            mapMarker.SendUpdate();
        }       

        void DestroyAnyTracker() 
        {
            SetMapMarker(Vector3.zero);
            Tracker tracker = containerAK?.GetComponent<Tracker>() ?? holdingPlayer?.GetComponent<Tracker>() ?? droppedAK?.GetComponent<Tracker>();
            if (tracker != null)
                UnityEngine.Object.Destroy(tracker);
        }

        private new void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Picked"] = "The Golden AK was picked by {0}",
            }, this);
        }

        private void SaveData()
        {
            gameData.currentOwnerID = currentOwnerID;            
            gameData.AK_ID = goldenAK.uid;
            Interface.Oxide.DataFileSystem.WriteObject("GoldenAKChallenge", gameData);
        }
       
        private void LoadData()
        {

            try
            {
                gameData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>("GoldenAKChallenge");
            }
            catch (Exception ex)
            {
                if(ex is MissingMethodException)
                {
                    Puts("No data was found.");
                    gameData = new StoredData();
                    return;
                }
                RaiseError($"Failed to load data file. ({ex.Message})\n");
            }
        }

    	void Init()
        {   
            LoadData();			
        }

		void OnServerSave()
		{
			SaveData();
		}

        void Unload()
        {
            mapMarker?.Kill();
            mapMarker.SendUpdate();
            DestroyAnyTracker();
            SaveData();
        }           

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
    }
}