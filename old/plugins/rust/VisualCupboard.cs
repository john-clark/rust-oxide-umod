using System;

using Oxide.Core;
using UnityEngine;
using System.Collections.Generic;
using Oxide.Core.Plugins;
namespace Oxide.Plugins
{
     	[Info("VisualCupboard", "Colon Blow", "1.0.11", ResourceId = 2030)]
    	class VisualCupboard : RustPlugin
     	{
		void OnServerInitialized() { serverInitialized = true; }

        	void Loaded()
        	{
			LoadVariables();
			serverInitialized = true;
			lang.RegisterMessages(messages, this);
			permission.RegisterPermission("visualcupboard.allowed", this);
			permission.RegisterPermission("visualcupboard.admin", this);
		}

        	void LoadDefaultConfig()
        	{
            		Puts("Creating a new config file");
            		Config.Clear();
            		LoadVariables();
        	}

       	 	Dictionary<string, string> messages = new Dictionary<string, string>()
        	{
			{"notallowed", "You are not allowed to access that command." }
        	};

	////////////////////////////////////////////////////////////////////////////////////////////
	//	Configuration File
	////////////////////////////////////////////////////////////////////////////////////////////

		bool Changed;

		private static float UseCupboardRadius = 25f;
		float DurationToShowRadius = 60f;
		float ShowCupboardsWithinRangeOf = 50f;
		int VisualDarkness = 5;

		private static bool serverInitialized = false;

        	private void LoadConfigVariables()
        	{
        		CheckCfgFloat("My Cupboard Radius is (25 is default)", ref UseCupboardRadius);
        		CheckCfgFloat("Show Visuals On Cupboards Withing Range Of", ref ShowCupboardsWithinRangeOf);
			CheckCfgFloat("Show Visuals For This Long", ref DurationToShowRadius);
			CheckCfg("How Dark to make Visual Cupboard", ref VisualDarkness );
        	}

        	private void LoadVariables()
        	{
            		LoadConfigVariables();
            		SaveConfig();
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

	////////////////////////////////////////////////////////////////////////////////////////////
	//	Sphere entity used for Visual Cupboard Radius
	////////////////////////////////////////////////////////////////////////////////////////////

	class ToolCupboardSphere : MonoBehaviour
		{

			
			BaseEntity sphere;
			BaseEntity entity;
			public bool showall;

			Vector3 pos = new Vector3(0, 0, 0);
			Quaternion rot = new Quaternion();
			string strPrefab = "assets/prefabs/visualization/sphere.prefab";

			void Awake()
			{
				SpawnSphere();
			}

			void SpawnSphere()
			{
				entity = GetComponent<BaseEntity>();
				sphere = GameManager.server.CreateEntity(strPrefab, pos, rot, true);
				SphereEntity ball = sphere.GetComponent<SphereEntity>();
				ball.currentRadius = 1f;
				ball.lerpRadius = 2.0f*UseCupboardRadius;
				ball.lerpSpeed = 100f;
				showall = false;
				sphere.SetParent(entity);
				sphere.Spawn();
			}

           	 	void OnDestroy()
            		{
				if (sphere == null) return;
               			sphere.Kill(BaseNetworkable.DestroyMode.None);
            		}

		}

	////////////////////////////////////////////////////////////////////////////////////////////
	//	When player places a cupbaord, a Visual cupboard radius will pop up
	////////////////////////////////////////////////////////////////////////////////////////////

        	object CanNetworkTo(BaseEntity entity, BasePlayer target)
        	{
			var sphereobj = entity.GetComponent<ToolCupboardSphere>();
			if (sphereobj == null) return null;
            		if (sphereobj !=null && sphereobj.showall == false)
     
			{
				if (target.userID != entity.OwnerID) return false;
			}
            		return null;           
        	}

	////////////////////////////////////////////////////////////////////////////////////////////
	//	When player runs chat command, shows Cupboard Radius of nearby Tool Cupboards
	////////////////////////////////////////////////////////////////////////////////////////////
		
		[ChatCommand("showsphere")]
        	void cmdChatShowSphere(BasePlayer player, string command)
		{	
			AddSphere(player, false, false);
		}

		[ChatCommand("showsphereall")]
        	void cmdChatShowSphereAll(BasePlayer player, string command)
		{
			AddSphere(player, true, false);
		}

		[ChatCommand("showsphereadmin")]
        	void cmdChatShowSphereAdmin(BasePlayer player, string command)
		{
			if (isAllowed(player, "visualcupboard.admin"))
			{
				AddSphere(player, true, true);
				return;
			}
			else if (!isAllowed(player, "visualcupboard.admin"))
			{
				SendReply(player, lang.GetMessage("notallowed", this));
			 	return;	
			}
		}

		ToolCupboardSphere sphereobj;

		void AddSphere(BasePlayer player, bool showall, bool adminshow)
		{
			if (isAllowed(player, "visualcupboard.allowed") || isAllowed(player, "visualcupboard.admin"))
			{
				List<BaseCombatEntity> cblist = new List<BaseCombatEntity>();
				Vis.Entities<BaseCombatEntity>(player.transform.position, ShowCupboardsWithinRangeOf, cblist);
			
				foreach (BaseCombatEntity bp in cblist)
				{
					if (bp is BuildingPrivlidge)
					{
						if (bp.GetComponent<ToolCupboardSphere>() == null)
						{
							Vector3 pos = bp.transform.position;

							if (!adminshow)
							{
								if (player.userID == bp.OwnerID)
								{
            								for (int i = 0; i < VisualDarkness; i++)
            								{
										sphereobj = bp.gameObject.AddComponent<ToolCupboardSphere>();
										if (showall) sphereobj.showall = true;
										GameManager.Destroy(sphereobj, DurationToShowRadius);
									}
								}

							}
							if (adminshow)
							{
            							for (int i = 0; i < VisualDarkness; i++)
            							{
									sphereobj = bp.gameObject.AddComponent<ToolCupboardSphere>();
									sphereobj.showall = true;
									GameManager.Destroy(sphereobj, DurationToShowRadius);
								}
								player.SendConsoleCommand("ddraw.text", 10f, Color.red, pos+Vector3.up, FindPlayerName(bp.OwnerID));
								PrintWarning("Tool Cupboard Owner " + bp.OwnerID + " : " + FindPlayerName(bp.OwnerID));
							}	
						}
					}
				}
				return;
			}
			SendReply(player, lang.GetMessage("notallowed", this));
			 return;	
		}


		[ChatCommand("killsphere")]
        	void cmdChatDestroySphere(BasePlayer player, string command)
		{
			if (isAllowed(player, "visualcupboard.admin"))
			{
				DestroyAll<ToolCupboardSphere>();
				return;
			}
			else if (!isAllowed(player, "visualcupboard.admin"))
			{
				SendReply(player, lang.GetMessage("notallowed", this));
			 	return;	
			}
		}

	////////////////////////////////////////////////////////////////////////////////////////////

        	private string FindPlayerName(ulong userId)
        	{
           	 BasePlayer player = BasePlayer.FindByID(userId);
           	 if (player)
                return player.displayName;

            	player = BasePlayer.FindSleeping(userId);
           	 if (player)
                return player.displayName;

           	 var iplayer = covalence.Players.FindPlayer(userId.ToString());
            	if (iplayer != null)
                return iplayer.Name;

            	return "Unknown Entity Owner";
       		}

        	void Unload()
        	{
            		DestroyAll<ToolCupboardSphere>();
        	}
		
        	static void DestroyAll<T>()
        	{
            		var objects = GameObject.FindObjectsOfType(typeof(T));
            		if (objects != null)
                		foreach (var gameObj in objects)
                    		GameObject.Destroy(gameObj);
       		 }

		bool isAllowed(BasePlayer player, string perm) => permission.UserHasPermission(player.UserIDString, perm);
	}
}