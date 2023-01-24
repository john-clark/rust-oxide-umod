using System;
using System.Collections.Generic;
using UnityEngine;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("FireArrows", "Colon Blow", "1.2.9", ResourceId = 1574)]
    class FireArrows : RustPlugin
    {

        [PluginReference]
        Plugin ZoneManager;

	bool Changed;
	
	Dictionary<ulong, FireArrowData> FireArrowOn = new Dictionary<ulong, FireArrowData>();
	Dictionary<ulong, FireBallData> FireBallOn = new Dictionary<ulong, FireBallData>();
	Dictionary<ulong, FireBombData> FireBombOn = new Dictionary<ulong, FireBombData>();
	Dictionary<ulong, FireArrowCooldown> Cooldown = new Dictionary<ulong, FireArrowCooldown>();
	Dictionary<ulong, string> GuiInfoFA = new Dictionary<ulong, string>();

	class FireArrowCooldown
        {
             	public BasePlayer player;
        }

        class FireArrowData
        {
             	public BasePlayer player;
        }

        class FireBombData
        {
             	public BasePlayer player;
        }

        class FireBallData
        {
             	public BasePlayer player;
        }

        void Loaded()
        {         
		LoadVariables();
            	lang.RegisterMessages(messagesFA, this);
		permission.RegisterPermission("firearrows.allowed", this);
		permission.RegisterPermission("firearrows.ball.allowed", this);
		permission.RegisterPermission("firearrows.bomb.allowed", this);
        }

        void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
		string guiInfo;
		if (GuiInfoFA.TryGetValue(player.userID, out guiInfo)) CuiHelper.DestroyUi(player, guiInfo);
            }
        }

        void LoadDefaultConfig()
        {
            	Puts("Creating a new config file");
            	Config.Clear();
            	LoadVariables();
        }
	

////////Configuration Stuff////////////////////////////////////////////////////////////////////////////

	static bool ShowArrowTypeIcon = true;
	static bool BlockinRestrictedZone = false;
	static bool UseProt = true;
	static bool UseBasicArrowsOnly = false;
	static float DamageFireArrow = 50f;
	static float DamageFireBall = 200f;
	static float DamageFireBomb = 500f;
	static float DamageRadius = 1f;
	static float DurationFireArrow = 10f;
	static float DurationFireBallArrow = 10f;
	static float DurationFireBombArrow = 10f;
	static float UsageCooldown = 60f;

	static string RestrictedZoneID = "24072018";
	static int cloth = 5;
	static int fuel = 5;
	static int oil = 5;
	static int explosives = 5;

	private string IconFireArrow = "http://i.imgur.com/3e8FWvt.png";
	private string IconFireBall = "http://i.imgur.com/USdpXGT.png";
	private string IconFireBomb = "http://i.imgur.com/0DpAHMn.png";

	bool isRestricted;
        private void LoadVariables()
        {
            	LoadConfigVariables();
            	SaveConfig();
        }

        private void LoadConfigVariables()
        {
        	CheckCfg("Icon - Show Arrow Type", ref ShowArrowTypeIcon);
		CheckCfg("Restriction - Block usage in Restricted Zone", ref BlockinRestrictedZone);
		CheckCfg("Damage Protection - Use Entity Protection Values", ref UseProt);
		CheckCfg("Ammo - Only allow HV and Wooden arrows", ref UseBasicArrowsOnly);
        	CheckCfgFloat("Damage - Fire Arrow", ref DamageFireArrow);
        	CheckCfgFloat("Damage - Fire Ball Arrow", ref DamageFireBall);
        	CheckCfgFloat("Damage - Fire Bomb Arrow", ref DamageFireBomb);
		CheckCfgFloat("Damage - Radius", ref DamageRadius);
		CheckCfgFloat("Duration - Fire Arrow", ref DurationFireArrow);
		CheckCfgFloat("Duration - Fire Ball Arrow", ref DurationFireBallArrow);
		CheckCfgFloat("Duration - Fire Bomb Arrow", ref DurationFireBombArrow);
		CheckCfg("Zone - Restricted Zone ID", ref RestrictedZoneID);
		CheckCfg("Required - All Arrows - Cloth Amount", ref cloth);
		CheckCfg("Required - All Arrows- Low Grade Fuel Amount", ref fuel);
		CheckCfg("Required - FireBall & FireBomb Arrows - Crude Oil", ref oil);
		CheckCfg("Required - FireBomb Arrows - Explosives", ref explosives);
        	CheckCfg("Icon - Fire Arrow", ref IconFireArrow);
        	CheckCfg("Icon - Fire Ball Arrow", ref IconFireBall);
        	CheckCfg("Icon - Fire Bomb Arrow", ref IconFireBomb);
		CheckCfgFloat("Cooldown - Time needed to wait", ref UsageCooldown);
        }

        private void CheckCfg<T>(string Key, ref T var)
        {
            if (Config[Key] is T)
                var = (T)Config[Key];
            else
                Config[Key] = var;
        }

        private void CheckCfgFloat(string Key, ref float var)
        {

            if (Config[Key] != null)
                var = Convert.ToSingle(Config[Key]);
            else
                Config[Key] = var;
        }

        object GetConfig(string menu, string datavalue, object defaultValue)
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

////////Language Settings////////////////////////////////////////////////////////////////////////////

       	Dictionary<string, string> messagesFA = new Dictionary<string, string>()
            	{
                	{"firearrowtxt", "Your Arrows are set for Fire."},
                	{"firearrowammotype", "You must only use HV or Wooden Arrows."},
			{"fireballarrowtxt", "Your Arrows are set for FireBall."},
			{"firebombarrowtxt", "Your Arrows are set for FireBomb."},
                	{"doesnothavemattxt", "You don't have required materials..."},
               	 	{"defaultarrowtxt", "Your Arrows are set for Normal."},
			{"restricted", "You are not allowed FireArrows in this Zone"},
			{"cooldown", "You must wait for cooldown to shoot again."},	
			{"deniedarrowtxt", "No Access to This Arrow Tier."}
            	};

////////Arrow Damage and FX control////////////////////////////////////////////////////////////////////

	void OnPlayerAttack(BasePlayer player, HitInfo hitInfo)
	{	
           	if (usingCorrectWeapon(player))
		{
			if (Cooldown.ContainsKey(player.userID)) return;
			if ((FireArrowOn.ContainsKey(player.userID)) || (FireBallOn.ContainsKey(player.userID)) || (FireBombOn.ContainsKey(player.userID)))
			{
				if (hitInfo.ProjectilePrefab.ToString().Contains("arrow_hv") || hitInfo.ProjectilePrefab.ToString().Contains("arrow_wooden"))
				{
					ArrowFX(player, hitInfo);
					return;
				}
				if (!UseBasicArrowsOnly && (hitInfo.ProjectilePrefab.ToString().Contains("arrow_fire") || hitInfo.ProjectilePrefab.ToString().Contains("arrow_bone")))
				{
					ArrowFX(player, hitInfo);
					return;
				}
				SendReply(player, lang.GetMessage("firearrowammotype", this));
				return;
			}
		}
	}

	void ArrowFX(BasePlayer player, HitInfo hitInfo)
	{
		if (FireArrowOn.ContainsKey(player.userID))
			{
				FireArrowFX(player, hitInfo);
				ArrowCooldownControl(player);
				return;
			}
		if (FireBallOn.ContainsKey(player.userID))
			{
				FireBallFX(player, hitInfo);
				ArrowCooldownControl(player);
				return;			
			}
		if (FireBombOn.ContainsKey(player.userID))
			{
				FireBombFX(player, hitInfo);
				ArrowCooldownControl(player);
				return;
			}
		else
		return;
	}

	void ArrowCooldownControl(BasePlayer player)
	{
		if (UsageCooldown <= 0f) return;
		if (UsageCooldown >= 1f)
			{
				
				Cooldown.Add(player.userID, new FireArrowCooldown { player = player, });
				timer.Once(UsageCooldown, () => Cooldown.Remove(player.userID));
			}
	}

	void ArrowCooldownToggle(BasePlayer player)
	{
		if (UsageCooldown <= 0f) return;
		NormalArrowToggle(player);
	}

	void FireArrowFX(BasePlayer player, HitInfo hitInfo)
	{
		if (!hasResources(player)) { tellDoesNotHaveMaterials(player); NormalArrowToggle(player); return; }
		if (!hitInfo.ProjectilePrefab.ToString().Contains("arrow")) return;
		applyBlastDamage(player, DamageFireArrow, Rust.DamageType.Heat, hitInfo);

		Effect.server.Run("assets/bundled/prefabs/fx/impacts/additive/fire.prefab", hitInfo.HitPositionWorld);
		BaseEntity FireArrow = GameManager.server.CreateEntity("assets/bundled/prefabs/fireball.prefab", hitInfo.HitPositionWorld);
		FireArrow?.Spawn();
		timer.Once(DurationFireArrow, () => FireArrow.Kill());
		ArrowCooldownToggle(player);
		return;
	}

	void FireBallFX(BasePlayer player, HitInfo hitInfo)
	{
		if (!notZoneRestricted(player)) { tellRestricted(player); return; }
		if (!hasResources(player)) { tellDoesNotHaveMaterials(player); NormalArrowToggle(player); return; }
		if (!hitInfo.ProjectilePrefab.ToString().Contains("arrow")) return;

		applyBlastDamage(player, DamageFireBall, Rust.DamageType.Heat, hitInfo);
		timer.Once(1, () => applyBlastDamage(player, DamageFireBall, Rust.DamageType.Heat, hitInfo));
		timer.Once(2, () => applyBlastDamage(player, DamageFireBall, Rust.DamageType.Heat, hitInfo));
		timer.Once(3, () => applyBlastDamage(player, DamageFireBall, Rust.DamageType.Heat, hitInfo));

		Effect.server.Run("assets/bundled/prefabs/fx/survey_explosion.prefab", hitInfo.HitPositionWorld);
		BaseEntity FireBallArrow = GameManager.server.CreateEntity("assets/bundled/prefabs/napalm.prefab", hitInfo.HitPositionWorld);
		FireBallArrow?.Spawn();
		timer.Once(DurationFireBallArrow, () => FireBallArrow.Kill());
		ArrowCooldownToggle(player);
		return;
	}

	void FireBombFX(BasePlayer player, HitInfo hitInfo)
	{
		if (!notZoneRestricted(player)) { tellRestricted(player); return; }
		if (!hasResources(player)) { tellDoesNotHaveMaterials(player); NormalArrowToggle(player); return; }
		if (!hitInfo.ProjectilePrefab.ToString().Contains("arrow")) return;
		applyBlastDamage(player, DamageFireBomb, Rust.DamageType.Explosion, hitInfo);

		Effect.server.Run("assets/bundled/prefabs/fx/weapons/landmine/landmine_explosion.prefab", hitInfo.HitPositionWorld);
		BaseEntity FireBombArrow = GameManager.server.CreateEntity("assets/bundled/prefabs/oilfireballsmall.prefab", hitInfo.HitPositionWorld);
		FireBombArrow?.Spawn();
		timer.Once(DurationFireBombArrow, () => FireBombArrow.Kill());
		ArrowCooldownToggle(player);
		
		return;
	}

	void applyBlastDamage(BasePlayer player, float damageamount, Rust.DamageType damagetype, HitInfo hitInfo)
	{
		if (!hitInfo.ProjectilePrefab.ToString().Contains("arrow")) return;
		playerBlastDamage(player, damageamount, damagetype, hitInfo);
	}

	void playerBlastDamage(BasePlayer player, float damageamount, Rust.DamageType damagetype, HitInfo hitInfo)
	{
	
        List<BaseCombatEntity> playerlist = new List<BaseCombatEntity>();
        Vis.Entities<BaseCombatEntity>(hitInfo.HitPositionWorld, DamageRadius, playerlist);

		foreach (BaseCombatEntity p in playerlist)
                {
		if (!(p is BuildingPrivlidge))
			{
			if (!hitInfo.ProjectilePrefab.ToString().Contains("arrow")) return;
			p.Hurt(damageamount, damagetype, player, UseProt);
			}
                }
	}

////////Arrow Toggle Control ////////////////////////////////////////////////////////////////////////////

	void OnPlayerInput(BasePlayer player, InputState input)
        {
        	if (input.WasJustPressed(BUTTON.FIRE_THIRD))
		{
			if (Cooldown.ContainsKey(player.userID))
			{
				SendReply(player, lang.GetMessage("cooldown", this));
				return;
			}
			ToggleArrowType(player);
		}
	}

	[ChatCommand("firearrow")]
        void cmdChatfirearrow(BasePlayer player, string command, string[] args)
	{
		if (Cooldown.ContainsKey(player.userID))
		{
			SendReply(player, lang.GetMessage("cooldown", this));
			return;
		}
		ToggleArrowType(player);
	}

	[ConsoleCommand("firearrow")]
        void cmdConsolefirearrow(ConsoleSystem.Arg arg)
	{
            if (arg.Connection == null)
            {
                SendReply(arg, "You can't use this command from the server console");
                return;
            }
		var player = arg.Player();
		if (Cooldown.ContainsKey(player.userID))
		{
			SendReply(player, lang.GetMessage("cooldown", this));
			return;
		}
		ToggleArrowType(player);
	}

	void ToggleArrowType(BasePlayer player)
       	{
		if (!usingCorrectWeapon(player)) return;

		if (FireArrowOn.ContainsKey(player.userID))
		{
			FireBallToggle(player);
			return;	
		}
		if (FireBallOn.ContainsKey(player.userID))
		{
			FireBombToggle(player);
			return;
		}
		if (FireBombOn.ContainsKey(player.userID))
		{
			NormalArrowToggle(player);
			return;
		}
		if ((!FireArrowOn.ContainsKey(player.userID)) || (!FireBallOn.ContainsKey(player.userID)) || (!FireBombOn.ContainsKey(player.userID)))
		{
			FireArrowToggle(player);
			return;
		}
		else
		NormalArrowToggle(player);
		return;
        }

	void NormalArrowToggle(BasePlayer player)
	{
		DestroyArrowData(player);
		SendReply(player, lang.GetMessage("defaultarrowtxt", this));
		DestroyCui(player);
		return;
	}

	void FireArrowToggle(BasePlayer player)
	{
		if (!IsAllowed(player, "firearrows.allowed"))
			{
			FireBallToggle(player);
			return;
			}
		DestroyArrowData(player);
		FireArrowOn.Add(player.userID, new FireArrowData
		{
		player = player,
		});
		SendReply(player, lang.GetMessage("firearrowtxt", this));
		DestroyCui(player);
		ArrowGui(player);
		return;
	}

	void FireBallToggle(BasePlayer player)
	{
		if (!IsAllowed(player, "firearrows.ball.allowed"))
			{
			FireBombToggle(player);
			return;
			}
		DestroyArrowData(player);
		FireBallOn.Add(player.userID, new FireBallData
		{
		player = player,
		});
		SendReply(player, lang.GetMessage("fireballarrowtxt", this));
		DestroyCui(player);
		ArrowGui(player);
		return;
	}

	void FireBombToggle(BasePlayer player)
	{
		if (!IsAllowed(player, "firearrows.bomb.allowed"))
			{
			NormalArrowToggle(player);
			return;
			}
		DestroyArrowData(player);
		FireBombOn.Add(player.userID, new FireBombData
		{
		player = player,
		});
		SendReply(player, lang.GetMessage("firebombarrowtxt", this));
		DestroyCui(player);
		ArrowGui(player);
		return;
	}

///////////Checks to see if player has resources for Arrow///////////////////////////////////////

	bool hasResources(BasePlayer player)
	{
		int cloth_amount = player.inventory.GetAmount(94756378);
		int fuel_amount = player.inventory.GetAmount(28178745);
		int oil_amount = player.inventory.GetAmount(1983936587);
		int explosives_amount = player.inventory.GetAmount(1755466030);

		if (FireArrowOn.ContainsKey(player.userID))
			{
			if (cloth_amount >= cloth && fuel_amount >= fuel)
				{
				player.inventory.Take(null, 28178745, fuel);
				player.inventory.Take(null, 94756378, cloth);
				player.Command("note.inv", 28178745, -fuel);
				player.Command("note.inv", 94756378, -cloth);
				return true;
				}
			return false;
			}
		if (FireBallOn.ContainsKey(player.userID))
			{
			if (cloth_amount >= cloth && fuel_amount >= fuel && oil_amount >= oil)
				{
				player.inventory.Take(null, 28178745, fuel);
				player.inventory.Take(null, 94756378, cloth);
				player.inventory.Take(null, 1983936587, oil);
				player.Command("note.inv", 28178745, -fuel);
				player.Command("note.inv", 94756378, -cloth);
				player.Command("note.inv", 1983936587, -oil);
				return true;
				}
			return false;
			}
		if (FireBombOn.ContainsKey(player.userID))
			{
			if (cloth_amount >= cloth && fuel_amount >= fuel && oil_amount >= oil && explosives_amount >= explosives)
				{
				player.inventory.Take(null, 28178745, fuel);
				player.inventory.Take(null, 94756378, cloth);
				player.inventory.Take(null, 1983936587, oil);
				player.inventory.Take(null, 1755466030, explosives);
				player.Command("note.inv", 28178745, -fuel);
				player.Command("note.inv", 94756378, -cloth);
				player.Command("note.inv", 1983936587, -oil);
				player.Command("note.inv", 1755466030, -explosives);
				return true;
				}
			return false;
			}

	return false;
	}

////////Shows Arrow type icons on player screen////////////////////////////////////////////////////////////////

	void ArrowCui(BasePlayer player)
	{
	if (ShowArrowTypeIcon) ArrowGui(player);
	}

        void ArrowGui(BasePlayer player)
        {
	DestroyCui(player);

        var elements = new CuiElementContainer();
        GuiInfoFA[player.userID] = CuiHelper.GetGuid();

        if (ShowArrowTypeIcon)
	{
		if (FireArrowOn.ContainsKey(player.userID))
        	{
        	elements.Add(new CuiElement
                	{
                    	Name = GuiInfoFA[player.userID],
			Parent = "Overlay",
                    	Components =
                    		{
                        	new CuiRawImageComponent { Color = "1 1 1 1", Url = IconFireArrow, Sprite = "assets/content/textures/generic/fulltransparent.tga" },
                        	new CuiRectTransformComponent { AnchorMin = "0.165 0.025",  AnchorMax = "0.210 0.095"}
                    		}
                	});
         	}
        	if (FireBallOn.ContainsKey(player.userID))
        	{
        	elements.Add(new CuiElement
                	{
                    	Name = GuiInfoFA[player.userID],
			Parent = "Overlay",
                    	Components =
                    		{
                        	new CuiRawImageComponent { Color = "1 1 1 1", Url = IconFireBall, Sprite = "assets/content/textures/generic/fulltransparent.tga" },
                        	new CuiRectTransformComponent { AnchorMin = "0.165 0.025",  AnchorMax = "0.210 0.095"}
                    		}
                	});
         	}
		if (FireBombOn.ContainsKey(player.userID))
        	{
        	elements.Add(new CuiElement
                	{
                    	Name = GuiInfoFA[player.userID],
			Parent = "Overlay",
                    	Components =
                    		{
                        	new CuiRawImageComponent { Color = "1 1 1 1", Url = IconFireBomb, Sprite = "assets/content/textures/generic/fulltransparent.tga" },
                        	new CuiRectTransformComponent { AnchorMin = "0.165 0.025",  AnchorMax = "0.210 0.095"}
                    		}
                	});
         	}

	}
         CuiHelper.AddUi(player, elements);
        }

////////Helpers////////////////////////////////////////////////////////////////////////////////

	bool notZoneRestricted(BasePlayer player)
	{
		isRestricted = false;
		var ZoneManager = plugins.Find("ZoneManager");
		bool Zone1Check = Convert.ToBoolean(ZoneManager?.Call("isPlayerInZone", RestrictedZoneID, player));
		if (Zone1Check)
		{
		isRestricted = true;
		}
		if (isRestricted) return false;
		return true;
	}
	
	void tellNotGrantedArrow(BasePlayer player)
	{
		SendReply(player, lang.GetMessage("deniedarrowtxt", this));
	}

	void tellDoesNotHaveMaterials(BasePlayer player)
	{
		SendReply(player, lang.GetMessage("doesnothavemattxt", this));
	}
        
	void tellRestricted(BasePlayer player)
	{
		SendReply(player, lang.GetMessage("restricted", this));
	}

	bool IsAllowed(BasePlayer player, string perm)
        {
        	if (permission.UserHasPermission(player.userID.ToString(), perm)) return true;
        	return false;
        }

	bool usingCorrectWeapon(BasePlayer player)
	{
	Item activeItem = player.GetActiveItem();
        if (activeItem != null && activeItem.info.shortname == "crossbow") return true;
	if (activeItem != null && activeItem.info.shortname == "bow.hunting") return true;
	return false;
	}

	void DestroyCui(BasePlayer player)
	{
		string guiInfo;
		if (GuiInfoFA.TryGetValue(player.userID, out guiInfo)) CuiHelper.DestroyUi(player, guiInfo);
	}

	void DestroyArrowData(BasePlayer player)
		{
		if (FireArrowOn.ContainsKey(player.userID))
			{
			FireArrowOn.Remove(player.userID);
			}
		if (FireBallOn.ContainsKey(player.userID))
			{
			FireBallOn.Remove(player.userID);
			}
		if (FireBombOn.ContainsKey(player.userID))
			{
			FireBombOn.Remove(player.userID);
			}
		else
		return;
		}

	void OnPlayerRespawned(BasePlayer player)
	{
                DestroyCui(player);
		DestroyArrowData(player);
	}

	void OnPlayerDisconnected(BasePlayer player, string reason)
	{
                DestroyCui(player);
		DestroyArrowData(player);
	}

    }


}