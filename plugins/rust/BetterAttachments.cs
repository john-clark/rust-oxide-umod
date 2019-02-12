using Newtonsoft.Json;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Rust;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("BetterAttachments", "ignignokt84", "0.0.3", ResourceId = 2326)]
	[Description("Plugin allowing for better control of weapon attachment attributes")]
	class BetterAttachments : RustPlugin
	{
		// configuration container
		AttachmentData data = new AttachmentData();
		// defaults cache - original attachment values are saved here so that
		// attachments can be reverted when this plugin is unloaded (otherwise
		// attachment modifications would be permanent)
		Dictionary<string,AttachmentModifier> defaults = new Dictionary<string,AttachmentModifier>();
		
		// load config
		void LoadConfig()
		{
			Config.Settings.NullValueHandling = NullValueHandling.Ignore;
			
			bool dirty = false;
			try {
				data = Config.ReadObject<AttachmentData>();
			} catch (Exception e) {
				data = new AttachmentData();
			}
			
			if(data == null || data.data == null || data.data.Count == 0)
				dirty |= LoadDefaultConfig();
			
			if(dirty)
				SaveData();
		}

		// save data
		void SaveData() => Config.WriteObject(data);

		// loads default configuration entries
		bool LoadDefaultConfig()
		{
			data.data.Clear();
			
			// flashlight defaults:
			// none
			AttachmentModifier flashlight = new AttachmentModifier();
			flashlight.name = "flashlight.entity";
			flashlight.sightAimCone = new Modifier(true);
			flashlight.sightAimCone.scalar = 0.95f;
			flashlight.recoil = new Modifier(true);
			flashlight.recoil.offset = -0.5f;
			flashlight.aimswaySpeed = new Modifier(true);
			flashlight.aimswaySpeed.scalar = 0.95f;
			flashlight.conditionLoss = 0f;
			data.data[flashlight.name] = flashlight;
			
			// holosight defaults:
			// none
			AttachmentModifier holosight = new AttachmentModifier();
			holosight.name = "holosight.entity";
			holosight.sightAimCone = new Modifier(true);
			holosight.sightAimCone.scalar = 0.9f;
			holosight.conditionLoss = 0f;
			data.data[holosight.name] = holosight;
			
			// lasersight defaults:
			// aimsway scalar 0.8
			// recoil scalar 0.85
			// sightAimCone scalar 0.25
			// hipAimCone scalar 0.25
			AttachmentModifier lasersight = new AttachmentModifier();
			lasersight.name = "lasersight.entity";
			lasersight.aimsway = new Modifier(true);
			lasersight.aimsway.scalar = 0.9f;
			lasersight.recoil = new Modifier(true);
			lasersight.recoil.scalar = 1f;
			lasersight.recoil.offset = -0.5f;
			lasersight.sightAimCone = new Modifier(true);
			lasersight.sightAimCone.scalar = 0.75f;
			lasersight.hipAimCone = new Modifier(true);
			lasersight.hipAimCone.scalar = 0.75f;
			lasersight.conditionLoss = 0f;
			data.data[lasersight.name] = lasersight;
			
			// muzzleBoost defaults:
			// repeatDelay scalar 0.9
			// projectileVelocity scalar 0.9
			// projectileDamage scalar 0.9
			// recoil scalar 0.98
			AttachmentModifier muzzleBoost = new AttachmentModifier();
			muzzleBoost.name = "muzzleboost.entity";
			muzzleBoost.repeatDelay = new Modifier(true);
			muzzleBoost.repeatDelay.scalar = 0.8f;
			muzzleBoost.projectileVelocity = new Modifier(true);
			muzzleBoost.projectileVelocity.scalar = 0.95f;
			muzzleBoost.projectileDamage = new Modifier(true);
			muzzleBoost.projectileDamage.scalar = 1f;
			muzzleBoost.recoil = new Modifier(true);
			muzzleBoost.recoil.scalar = 0.95f;
			muzzleBoost.conditionLoss = 0.1f;
			data.data[muzzleBoost.name] = muzzleBoost;
			
			// add catastrophic failure condition for muzzle boost
			CatastrophicFailure muzzleBoostFailure = new CatastrophicFailure();
			muzzleBoostFailure.enabled = true;
			muzzleBoostFailure.start = 0.15f; // start failure chances at 15%
			muzzleBoostFailure.target = 0.02f; // guaranteed failure at 2%
			muzzleBoostFailure.weaponDamage = 0.2f; // 20% of max weapon condition damaged on failure
			muzzleBoostFailure.playerDamage = 0.2f; // 20% of max player HP lost on failure
			data.failures[muzzleBoost.name] = muzzleBoostFailure;
			
			// muzzleBrake defaults:
			// recoil scalar 0.5
			// sightAimCone scalar 1.0 offset 1.0
			// hipAimCone scalar 1.0 offset 1.0
			AttachmentModifier muzzleBrake = new AttachmentModifier();
			muzzleBrake.name = "muzzlebrake.entity";
			muzzleBrake.aimsway = new Modifier(true);
			muzzleBrake.aimsway.scalar = 1.05f;
			muzzleBrake.aimswaySpeed = new Modifier(true);
			muzzleBrake.aimswaySpeed.scalar = 0.95f;
			muzzleBrake.recoil = new Modifier(true);
			muzzleBrake.recoil.scalar = 0.4f;
			muzzleBrake.sightAimCone = new Modifier(true);
			muzzleBrake.sightAimCone.scalar = 0.95f;
			muzzleBrake.sightAimCone.offset = 0f;
			muzzleBrake.hipAimCone = new Modifier(true);
			muzzleBrake.hipAimCone.scalar = 1f;
			muzzleBrake.hipAimCone.offset = 0f;
			data.data[muzzleBrake.name] = muzzleBrake;
			
			// add catastrophic failure condition for muzzle brake
			CatastrophicFailure muzzleBrakeFailure = new CatastrophicFailure();
			muzzleBrakeFailure.enabled = true;
			muzzleBrakeFailure.start = 0.10f; // start failure chances at 10%
			muzzleBrakeFailure.target = 0.01f; // guaranteed failure at 1%
			muzzleBrakeFailure.weaponDamage = 0.05f; // 5% of max weapon condition damaged on failure
			muzzleBrakeFailure.playerDamage = 0.1f; // 10% of max player HP lost on failure
			data.failures[muzzleBrake.name] = muzzleBrakeFailure;
			
			// silencer defaults:
			// projectileVelocity scalar 0.75
			// projectileDamage scalar 0.75
			// aimsway scalar 0.8
			// recoil scalar 0.8
			// sightAimCone scalar 0.7
			// hipAimCone scalar 0.6
			AttachmentModifier silencer = new AttachmentModifier();
			silencer.name = "silencer.entity";
			silencer.projectileVelocity = new Modifier(true);
			silencer.projectileVelocity.scalar = 1.05f;
			silencer.projectileDamage = new Modifier(true);
			silencer.projectileDamage.scalar = 1f;
			silencer.aimsway = new Modifier(true); 
			silencer.aimsway.scalar = 0.95f;
			silencer.recoil = new Modifier(true);
			silencer.recoil.scalar = 0.75f;
			silencer.sightAimCone = new Modifier(true);
			silencer.sightAimCone.scalar = 0.95f;
			silencer.hipAimCone = new Modifier(true);
			silencer.hipAimCone.scalar = 0.95f;
			silencer.conditionLoss = 0f;
			data.data[silencer.name] = silencer;
			
			// add catastrophic failure condition for muzzle brake
			CatastrophicFailure silencerFailure = new CatastrophicFailure();
			silencerFailure.enabled = true;
			silencerFailure.start = 0.10f; // start failure chances at 10%
			silencerFailure.target = 0.01f; // guaranteed failure at 1%
			silencerFailure.weaponDamage = 0.1f; // 10% of max weapon condition damaged on failure
			silencerFailure.playerDamage = 0.15f; // 15% of max player HP lost on failure
			data.failures[silencer.name] = silencerFailure;
			
			// scope defaults:
			// recoil scalar 0.8
			// sightAimCone scalar 0.7
			AttachmentModifier scope = new AttachmentModifier();
			scope.name = "smallscope.entity";
			scope.aimswaySpeed = new Modifier(true);
			scope.aimswaySpeed.scalar = 0.95f;
			scope.recoil = new Modifier(true);
			scope.recoil.scalar = 0.95f;
			scope.sightAimCone = new Modifier(true);
			scope.sightAimCone.scalar = 0.5f;
			data.data[scope.name] = scope;
			
			return true;
		}
		
		// plugin loaded
		void Loaded() {
			LoadConfig();
			checkAllAttachments();
		}
		
		// plugin unloaded
		void Unload()
		{
			restoreAllAttachments();
		}
		
		// check if spawned entity is an attachment and update it accordingly
		void OnEntitySpawned(BaseNetworkable entity)
		{
			if(entity is ProjectileWeaponMod)
			{
				ProjectileWeaponMod attachment = entity as ProjectileWeaponMod;
				if(attachment != null)
					modifyAttachment(attachment);
			}
		}
		
		// check if the default settings are saved
		bool checkDefaults(string prefabName)
		{
			return defaults.ContainsKey(prefabName);
		}
		
		// save default setting
		void saveDefaults(ProjectileWeaponMod mod)
		{
			if(defaults.ContainsKey(mod.ShortPrefabName))
				return;
			defaults[mod.ShortPrefabName] = new AttachmentModifier(mod);
			// print default settings
			//Puts("Default created:");
			//Puts(defaults[mod.ShortPrefabName].ToString());
		}
		
		// check all attachments and update where required
		void checkAllAttachments()
		{
			ProjectileWeaponMod[] attachments = (ProjectileWeaponMod[]) GameObject.FindObjectsOfType(typeof(ProjectileWeaponMod));
			if(attachments != null)
				foreach(ProjectileWeaponMod attachment in attachments)
					modifyAttachment(attachment);
		}
		
		// restore all attachments to their default values
		void restoreAllAttachments()
		{
			ProjectileWeaponMod[] attachments = (ProjectileWeaponMod[]) GameObject.FindObjectsOfType(typeof(ProjectileWeaponMod));
			if(attachments != null)
				foreach(ProjectileWeaponMod attachment in attachments)
					restoreDefaults(attachment);
		}
		
		// modify the attachment
		void modifyAttachment(ProjectileWeaponMod attachment)
		{
			AttachmentModifier m;
			if(data.data.TryGetValue(attachment.ShortPrefabName, out m))
			{
				if(m != null && m.enabled)
				{
					if(!checkDefaults(attachment.ShortPrefabName))
						saveDefaults(attachment);
					if(m.repeatDelay != null)
						updateModifier(ref attachment.repeatDelay, m.repeatDelay);
					if(m.projectileVelocity != null)
						updateModifier(ref attachment.projectileVelocity, m.projectileVelocity);
					if(m.projectileDamage != null)
						updateModifier(ref attachment.projectileDamage, m.projectileDamage);
					if(m.projectileDistance != null)
						updateModifier(ref attachment.projectileDistance, m.projectileDistance);
					if(m.aimsway != null)
						updateModifier(ref attachment.aimsway, m.aimsway);
					if(m.aimswaySpeed != null)
						updateModifier(ref attachment.aimswaySpeed, m.aimswaySpeed);
					if(m.recoil != null)
						updateModifier(ref attachment.recoil, m.recoil);
					if(m.sightAimCone != null)
						updateModifier(ref attachment.sightAimCone, m.sightAimCone);
					if(m.hipAimCone != null)
						updateModifier(ref attachment.hipAimCone, m.hipAimCone);
				}
			}
		}
		
		// restore the attachment to its default values
		void restoreDefaults(ProjectileWeaponMod attachment)
		{
			AttachmentModifier m;
			if(defaults.TryGetValue(attachment.ShortPrefabName, out m))
			{
				if(m != null)
				{
					if(m.repeatDelay != null)
						updateModifier(ref attachment.repeatDelay, m.repeatDelay);
					if(m.projectileVelocity != null)
						updateModifier(ref attachment.projectileVelocity, m.projectileVelocity);
					if(m.projectileDamage != null)
						updateModifier(ref attachment.projectileDamage, m.projectileDamage);
					if(m.projectileDistance != null)
						updateModifier(ref attachment.projectileDistance, m.projectileDistance);
					if(m.aimsway != null)
						updateModifier(ref attachment.aimsway, m.aimsway);
					if(m.aimswaySpeed != null)
						updateModifier(ref attachment.aimswaySpeed, m.aimswaySpeed);
					if(m.recoil != null)
						updateModifier(ref attachment.recoil, m.recoil);
					if(m.sightAimCone != null)
						updateModifier(ref attachment.sightAimCone, m.sightAimCone);
					if(m.hipAimCone != null)
						updateModifier(ref attachment.hipAimCone, m.hipAimCone);
				}
			}
		}
		
		// update a modifier
		void updateModifier(ref ProjectileWeaponMod.Modifier originalMod, Modifier newMod)
		{
			if(newMod == null) return;
			if(newMod.enabled != null)
				originalMod.enabled = newMod.enabled;
			if(newMod.scalar != null)
				originalMod.scalar = newMod.scalar;
			if(newMod.offset != null)
				originalMod.offset = newMod.offset;
		}
		
		// on lost condition, add condition back based on scaled amount
		void OnLoseCondition(Item item, ref float amount)
		{
			if(item == null || !item.hasCondition) return;
			BaseEntity entity = item.GetHeldEntity();
			if(entity == null) return;
			
			if(data.data.ContainsKey(entity.ShortPrefabName) && data.data[entity.ShortPrefabName].conditionLoss != null)
			{
				amount = amount * (1-data.data[entity.ShortPrefabName].conditionLoss);
				item.condition += amount;
			}
			if(shouldFail(entity.ShortPrefabName, item.conditionNormalized))
			{
				Item weaponItem = item.parent.parent;
				if(weaponItem == null) return;
				BasePlayer player = weaponItem.GetOwnerPlayer();
				if(player == null) return;
				BaseEntity weapon = player.GetHeldEntity();
				if(weapon != null && weapon is BaseProjectile)
				{
					float lostCondition = weaponItem.maxCondition * data.failures[entity.ShortPrefabName].weaponDamage;
					weaponItem.LoseCondition(lostCondition);
					
					float lostHP = player.MaxHealth() * data.failures[entity.ShortPrefabName].playerDamage;
					player.Hurt(lostHP, DamageType.Bullet);
					
					Effect.server.Run("assets/bundled/prefabs/fx/impacts/slash/metal/metal1.prefab", weapon, StringPool.closest, Vector3.zero, Vector3.zero);
					Effect.server.Run("assets/bundled/prefabs/fx/impacts/bullet/metal/metal1.prefab", weapon, StringPool.closest, Vector3.zero, Vector3.zero);
					Effect.server.Run("assets/prefabs/weapons/doubleshotgun/effects/pfx_bolt_shut_sparks.prefab", weapon, StringPool.closest, Vector3.zero, Vector3.zero);
				}
			}
		}
		
		// check whether the passed entity should fail
		// uses a logarithmic scale from start to target
		bool shouldFail(string name, float amount)
		{
			if(data.failures.ContainsKey(name) && data.failures[name].enabled)
			{
				if(amount == 0f) return true;
				if(amount == 1f) return false;
				if(data.failures[name].start <= amount) return false;
				if(data.failures[name].target == 0) return false;
				float target = amount/data.failures[name].target;
				float factor = 1f / data.failures[name].target * data.failures[name].start;
				float log = factor == 0f ? 0.0f : 1f - Mathf.Log(target, factor);
				if(log <= 0f) return false;
				return log >= UnityEngine.Random.value;
			}
			return false;
		}
		
		// wrapper class to hold configuration data all in one object
		private class AttachmentData
		{
			public Dictionary<string,AttachmentModifier> data = new Dictionary<string,AttachmentModifier>();
			public Dictionary<string,CatastrophicFailure> failures = new Dictionary<string,CatastrophicFailure>();
		}
		
		// container for modifier configuration for a single attachment type
		private class AttachmentModifier
		{
			public string name;
			public bool enabled = true;
			public Modifier repeatDelay;
			public Modifier projectileVelocity;
			public Modifier projectileDamage;
			public Modifier projectileDistance;
			public Modifier aimsway;
			public Modifier aimswaySpeed;
			public Modifier recoil;
			public Modifier sightAimCone;
			public Modifier hipAimCone;
			public float conditionLoss;
			
			public AttachmentModifier() {}
			
			public AttachmentModifier(ProjectileWeaponMod mod)
			{
				cloneAsDefaults(mod);
			}
			
			void cloneAsDefaults(ProjectileWeaponMod mod)
			{
				name = mod.ShortPrefabName;
				setModifier(ref repeatDelay, mod.repeatDelay);
				setModifier(ref projectileVelocity, mod.projectileVelocity);
				setModifier(ref projectileDamage, mod.projectileDamage);
				setModifier(ref projectileDistance, mod.projectileDistance);
				setModifier(ref aimsway, mod.aimsway);
				setModifier(ref aimswaySpeed, mod.aimswaySpeed);
				setModifier(ref recoil, mod.recoil);
				setModifier(ref sightAimCone, mod.sightAimCone);
				setModifier(ref hipAimCone, mod.hipAimCone);
			}
			
			void setModifier(ref Modifier internalMod, ProjectileWeaponMod.Modifier mod)
			{
				internalMod = new Modifier();
				internalMod.enabled = mod.enabled;
				internalMod.scalar = mod.scalar;
				internalMod.offset = mod.offset;
			}
			
			public string ToString() {
				string str = "name: " + name + Environment.NewLine +
							 "repeatDelay: " + repeatDelay.ToString() + Environment.NewLine +
							 "projectileVelocity: " + projectileVelocity.ToString() + Environment.NewLine +
							 "projectileDamage: " + projectileDamage.ToString() + Environment.NewLine +
							 "projectileDistance: " + projectileDistance.ToString() + Environment.NewLine +
							 "aimsway: " + aimsway.ToString() + Environment.NewLine +
							 "aimswaySpeed: " + aimswaySpeed.ToString() + Environment.NewLine +
							 "recoil: " + recoil.ToString() + Environment.NewLine +
							 "sightAimCone: " + sightAimCone.ToString() + Environment.NewLine +
							 "hipAimCone: " + hipAimCone.ToString();
				return str;
			}
		}
		
		// replication of ProjectileWeaponMod.Modifier as class instead of struct to allow nulls
		private class Modifier
		{
			public bool enabled;
			public float scalar;
			public float offset;
			
			public Modifier() {}
			public Modifier(bool enabled) {
				this.enabled = enabled;
			}
			public string ToString() {
				return "Modifier [" + enabled + ", " + scalar + ", " + offset + "]";
			}
		}
		
		private struct CatastrophicFailure {
			public bool enabled;
			public float start;
			public float target;
			public float weaponDamage;
			public float playerDamage;
		}
	}
}