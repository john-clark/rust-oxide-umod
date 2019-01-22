using System;

namespace Oxide.Plugins
{
	[Info("SyringeDenerf", "ignignokt84", "0.1.4", ResourceId = 1809)]
	[Description("Syringe healing customization plugin")]
	class SyringeDenerf : RustPlugin
	{
		private bool hasConfigChanged;
		float healAmount = 25f; // Instant heal amount
		float hotAmount = 10f; // Heal-over-time amount
		float hotTime = 10f; // Heal-over-time time
		bool usePermissions = false; // use permissions
		private const string perm = "syringedenerf.use";
		
		object OnHealingItemUse(HeldEntity item, BasePlayer target)
		{
			if(item is MedicalTool && item.ShortPrefabName.Contains("syringe"))
			{
				if(!hasPermission(target))
					return null;
				target.health = target.health + healAmount;
				target.metabolism.ApplyChange(MetabolismAttribute.Type.HealthOverTime, hotAmount, hotTime);
				return true;
			}
			return null;
		}
		
		// Check user permission
		bool hasPermission(BasePlayer player)
		{
			return permission.UserHasPermission(player.UserIDString, perm);
		}
		
		// Loaded
		void Loaded()
		{
			LoadConfig();
			permission.RegisterPermission(perm, this);
		}
		
		// loads default configuration
		protected override void LoadDefaultConfig()
		{
			Config.Clear();
			LoadConfig();
		}
		
		// loads config from file
		private void LoadConfig()
		{
			usePermissions = Convert.ToBoolean(GetConfig("Use Permissions", usePermissions));
			healAmount = Convert.ToSingle(GetConfig("Instant Heal Amount", healAmount));
			hotAmount = Convert.ToSingle(GetConfig("Heal-over-time Amount", hotAmount));
			hotTime = Convert.ToSingle(GetConfig("Heal-over-time Time", hotTime));
			
			if (!hasConfigChanged) return;
			SaveConfig();
			hasConfigChanged = false;
		}
		
		// get config options, or set to default value if not found
		private object GetConfig(string str, object defaultValue)
		{
			object value = Config[str];
			if (value == null)
			{
				value = defaultValue;
				Config[str] = value;
				hasConfigChanged = true;
			}
			return value;
		}
	}
}