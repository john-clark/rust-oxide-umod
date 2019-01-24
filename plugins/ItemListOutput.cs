using System.Collections.Generic;
using System.Linq;
using System;

using UnityEngine;

namespace Oxide.Plugins
{
	[Info("ItemListOutput", "Waizujin", 1.0)]
	[Description("Outputs a list of items.")]
	public class ItemListOutput : RustPlugin
	{
		protected override void LoadDefaultConfig()
		{
			PrintWarning("Creating a new configuration file.");

			var gameObjectArray = FileSystem.LoadAll<GameObject>("Assets/", ".item");
			var itemList = gameObjectArray.Select(x => x.GetComponent<ItemDefinition>()).Where(x => x != null).ToList();

			foreach (var item in itemList)
			{

				//Config[item.shortname] = item.itemid + "," + item.rarity.ToString() + "," + item.stackable;
				Config[item.displayName.english.ToString()] = item.shortname;
			}
		}
	}
}
