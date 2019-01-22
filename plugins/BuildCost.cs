using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("Building Cost", "Orange", "1.0.0")]
	[Description("Displays building cost of building")]
	public class BuildCost : RustPlugin
	{
		#region Vars
		
		private const string permUse = "buildcost.use";

		#endregion
		
		#region Commands
		
		[ChatCommand("cost")]
		private void Cmd(BasePlayer player)
		{
			Command(player);
		}
		
		#endregion

		#region Oxide Hooks

		private void Init()
		{
			lang.RegisterMessages(EN, this);
			permission.RegisterPermission(permUse, this);
		}

		#endregion

		#region Core
		
		private void Command(BasePlayer player)
		{
			if (!HasPerm(player))
			{
				return;
			}

			var building = GetBuilding(player);
			if (building?.decayEntities == null)
			{
				message(player, "Building");
				return;
			}

			var blocks = new List<DecayEntity>();
			var deployables = new List<DecayEntity>();
			
			foreach (var entity in building.decayEntities)
			{
				var cost = entity.BuildCost();
				if (cost == null)
				{
					deployables.Add(entity);
				}
				else
				{
					blocks.Add(entity);
				}
			}
			
			var cost1 = GetCost(blocks);
			var cost2 = GetCost(deployables);
			var text1 = GetTextFromCost(cost1);
			var text2 = GetTextFromCost(cost2);
			
			message(player, "Building Blocks", blocks.Count, text1);
			message(player, "Deployables", deployables.Count, text2);
		}

		private BuildingManager.Building GetBuilding(BasePlayer player)
		{
			RaycastHit rhit;
			if (!Physics.Raycast(player.eyes.HeadRay(), out rhit)) {return null;}
			var entity = rhit.GetEntity();
			if (entity == null) {return null;}
			var building = entity?.GetBuildingPrivilege()?.GetBuilding();
			return building;
		}

		private Dictionary<string, Dictionary<string, int>> GetCost(List<DecayEntity> entities)
		{
			var value = new Dictionary<string, Dictionary<string, int>>();

			foreach (var entity in entities)
			{
				var cost = entity.BuildCost() ?? ItemManager.FindItemDefinition(entity.ShortPrefabName)?.Blueprint?.ingredients;
				if (cost == null) {continue;}
				var entityName = entity.ShortPrefabName;
				
				if (!value.ContainsKey(entityName))
				{
					value.Add(entityName, new Dictionary<string, int>());
				}
				
				foreach (var item in cost)
				{
					var name = item.itemDef.displayName.english;
					var amount = Convert.ToInt32(item.amount);
					
					if (!value[entityName].ContainsKey(name))
					{
						value[entityName].Add(name, 0);
					}

					value[entityName][name] += amount;
				}
			}

			return value;
		}

		private string GetTextFromCost(Dictionary<string, Dictionary<string, int>> cost)
		{
			var format = lang.GetMessage("Pair", this);
			var text = "";
			
			foreach (var value in cost)
			{
				foreach (var value2 in value.Value)
				{
					text += string.Format(format, value.Key, value2.Key, value2.Value);
				}
			}

			return text;
		}

		#endregion

		#region Helpers

		private bool HasPerm(BasePlayer player)
		{
			if (permission.UserHasPermission(player.UserIDString, permUse))
			{
				return true;
			}

			message(player, "Permission");
			return false;
		}

		#endregion

		#region Language

		private Dictionary<string, string> EN = new Dictionary<string, string>
		{
			{"Permission", "You don' have permission to use that command!"},
			{"Building", "Sorry, we can't find building on your look position!"},
			{"Building Blocks", "<color=green>Building blocks x{0}:</color>\n{1}"},
			{"Deployables", "<color=yellow>Deployables x{0}:</color>\n{1}"},
			{"Pair", "{0} -> {1} x{2}\n"},
		};

		private void message(BasePlayer player, string key, params object[] args)
		{
			var message = string.Format(lang.GetMessage(key, this, player.UserIDString), args);
			player.ChatMessage(message);
		}

		#endregion
	}
}