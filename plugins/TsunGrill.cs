using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;  
using System.Collections;
using UnityEngine;
namespace Oxide.Plugins
{ 
    [Info("Tsunderellas food on grill", "Gartia", "1.0.1")]
    [Description("Adds Tsunderellas food on grill")]

 
    class TsunGrill : RustPlugin
    {
		List<FOG> FOGLIST = new List<FOG>();

		void OnFindBurnable(BaseOven oven)
		{
			if(oven.GetComponent<BaseEntity>()==null)return;
			if(oven.GetComponent<BaseEntity>().GetComponent<FOG>()==null)return;
			oven.GetComponent<BaseEntity>().GetComponent<FOG>().AddFood(allChicken);
		}
		void OnOvenToggle(BaseOven oven, BasePlayer player)
		{
			if(oven.GetComponent<BaseEntity>().ShortPrefabName.ToString()!="bbq.deployed")return;
			if(oven.GetComponent<FOG>()!=null)return;
			oven.GetComponent<BaseEntity>().gameObject.AddComponent<FOG>();
			FOGLIST.Add(oven.GetComponent<BaseEntity>().GetComponent<FOG>());
		}
		void init()
		{
			LoadDefaultConfig();
		}
		
		bool allChicken;
        private bool Changed;
        private object GetConfig(string menu, string datavalue, object defaultValue)
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
        private void LoadVariables()
        {
            allChicken = Convert.ToBoolean(GetConfig("Grill", "allChicken", true));
            if (Changed)
            {
                SaveConfig();
                Changed = false;
            }
        }
        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }
		class FOG : MonoBehaviour
        {
			BaseEntity entity;
			BaseOven oven;
			public List<Item> ItemList = new List<Item>();
			private IEnumerator coroutine;
			bool wait;

			private IEnumerator Wait(float waitTime)
			{
				while (true)
				{
					yield return new WaitForSeconds(waitTime);
					wait=false;
				}
			}
			
		    void Awake()
            {
				entity = GetComponent<BaseEntity>();  
				oven=GetComponent<BaseOven>();  
			}
			public void AddFood(bool allChicken)
			{
				if(wait)return;
				wait=true;
 				for (int i = 0; i < ItemList.Count; i++)
				{
					if(ItemList[i]!=null){
						Item removeitem=ItemList[i];
						ItemList.Remove(removeitem);
						removeitem.DoRemove();
					}
				}  
				

				for (int u = 0; u < (oven.inventory.itemList.Count); u++)
				{
					if(oven.inventory.itemList[u]==null)continue;
					if(oven.inventory.itemList[u].info==null)continue;
					var itemid=oven.inventory.itemList[u].info.itemid;
					if(itemid==null)continue;
					if(itemid==3655341||itemid==1436001773)continue;
					
					var pos=entity.transform.position+new Vector3(0,0.8f,0);
					var ang=entity.transform.eulerAngles;
					//itemAmount.itemDef.itemid
					var appear=oven.inventory.itemList[u].info.itemid;
					if(allChicken){
						//burned
							if(itemid==-225234813||itemid==-2066726403||itemid==1711323399||itemid==-1014825244||itemid==968732481||itemid==-1714986849)appear=1711323399;
							//cooked
								if(itemid==-2043730634||itemid==1734319168||itemid==-202239044||itemid==-2078972355||itemid==-991829475||itemid==991728250||itemid==-1691991080)appear=1734319168;
								//uncooked
									if(itemid==1325935999||itemid==-1658459025||itemid==-322501005||itemid==-533484654||itemid==-642008142||itemid==-253819519||itemid==179448791)appear=-1658459025;
										//spoiled
											if(itemid==431617507||itemid==661790782||itemid==-726947205)appear=-726947205;
					}
					Item item = ItemManager.CreateByItemID(appear, 1, (ulong)0);
					ItemList.Add(item);
					DroppedItem food = item.Drop(pos, Vector3.zero).GetComponent<DroppedItem>();
					food.SetParent(entity);
					var offset=0f;
					var offset2=0f;
					if(u>=6){offset=0.2f;offset2=6;}
					food.transform.localPosition=new Vector3(-0.1f+offset,0.8f,-0.38f+(u-offset2)*0.15f);
					food.transform.eulerAngles=ang;
					food.transform.hasChanged = true;
					food.SendNetworkUpdateImmediate();
					food.GetComponent<Rigidbody>().isKinematic = true;
					food.GetComponent<Rigidbody>().useGravity = false;
					food.allowPickup = false;
					food.GetComponent<Rigidbody>().detectCollisions=true;
					food.CancelInvoke((Action)Delegate.CreateDelegate(typeof(Action), food, "IdleDestroy")); 
					
				}  

				coroutine =Wait(5);
				StartCoroutine(coroutine);
			}
			public void Delete()
			{
				Destroy();
			}
			void Destroy()
            {
 				for (int i = 0; i < ItemList.Count; i++)
				{
					if(ItemList[i]!=null){
						ItemList[i].DoRemove();
					}
				}  
                enabled = false;
                CancelInvoke();
                Destroy(this);
            } 
		}
		
		void Unload()
		{
 				for (int i = 0; i < FOGLIST.Count; i++)
				{
					FOGLIST[i].Delete();
				}  
		}
	}
}