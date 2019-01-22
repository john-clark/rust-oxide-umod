using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;
using Diag = System.Diagnostics;

namespace Oxide.Plugins
{
	[Info("SaveMyMap", "FuJiCuRa", "1.3.4", ResourceId = 2111)] 
	class SaveMyMap : RustPlugin
	{
		bool Changed;
		SaveRestore saveRestore = null;
		bool wasShutDown;
		int Rounds;
		bool Initialized;
		string saveFolder;
		bool loadReload;
		string [] saveFolders;

		int saveInterval;
		int saveCustomAfter;
		bool callOnServerSave;
		float delayCallOnServerSave;
		bool saveAfterLoadFile;
		bool allowOutOfDateSaves;
		bool enableLoadOverride;
		bool onServerSaveUseCoroutine;
		int numberOfSaves;

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

		void LoadVariables()
		{
			saveInterval = Convert.ToInt32(GetConfig("Settings", "saveInterval", 1200));
			saveCustomAfter = Convert.ToInt32(GetConfig("Settings", "saveCustomAfter", 1));
			numberOfSaves = Convert.ToInt32(GetConfig("Settings", "numberOfSaves", 5));
			callOnServerSave = Convert.ToBoolean(GetConfig("Settings", "callOnServerSave", true));
			delayCallOnServerSave = Convert.ToInt32(GetConfig("Settings", "delayCallOnServerSave", 3));
			saveAfterLoadFile = Convert.ToBoolean(GetConfig("Settings", "saveAfterLoadFile", true));
			enableLoadOverride = Convert.ToBoolean(GetConfig("Settings", "enableLoadOverride", true));
			allowOutOfDateSaves = Convert.ToBoolean(GetConfig("Settings", "allowOutOfDateSaves", false));
			onServerSaveUseCoroutine = Convert.ToBoolean(GetConfig("Settings", "onServerSaveUseCoroutine", true));

			if (!Changed) return;
			SaveConfig();
			Changed = false;
		}
		
		void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
			                      {
									{"kickreason", "Map restore was initiated. Please wait a momemt"},
									{"loadedinfo", "Saveinverval set to: {0} sec. | Custom save after every '{1}' saves"},
									{"alreadysaving", "Server already saving"},
									{"customsavecomplete", "Custom saving complete"},
									{"needconfirm", "You need to confirm with 'force'"},
									{"definefilename", "You need to define a filename to load"},
									{"lastfilename", "You can load the last file by typing 'load' as name"},
									{"filenotfound", "The given filename was not found."},
									{"dirnotfound", "Save Directory not found. Will be recreated for next save."},									
									{"loadoverride", "Loadfile override succesful."},										
									{"loadoverridecancel", "Loadfile override aborted, map change detected."},		
			                      },this);
		}

		protected override void LoadDefaultConfig()
		{
			Config.Clear();
			LoadVariables();
		}

		void Loaded()
		{
			LoadVariables();
			LoadDefaultMessages();
			Rounds = 0;
			wasShutDown = false;
		}

		void Unload()
		{
			if (saveRestore != null)
				saveRestore.timedSave = true;
		}

		void OnServerInitialized()
		{
			saveFolders = SaveFolders();
			saveRestore = SingletonComponent<SaveRestore>.Instance;
			saveRestore.timedSave = false;
			saveFolder = $"{ConVar.Server.rootFolder}/saves/{0}/";
			timer.Every(saveInterval, () =>ServerMgr.Instance.StartCoroutine(SaveLoop()));
			Initialized = true;
			Puts(lang.GetMessage("loadedinfo", this), saveInterval, saveCustomAfter);
		}		
		
		IEnumerator SaveLoop()
		{
			if (!Initialized)
				yield return null;
			WaitForFixedUpdate waitU = new WaitForFixedUpdate();
			BaseEntity.saveList.RemoveWhere(p => !p);
            BaseEntity.saveList.RemoveWhere(p => p == null);
			Diag.Stopwatch stopwatch = Diag.Stopwatch.StartNew();
			foreach (BaseEntity current in BaseEntity.saveList)
				current.InvalidateNetworkCache();
			Debug.Log("Invalidate Network Cache took " + stopwatch.Elapsed.TotalSeconds.ToString("0.00") + " seconds");
			if (Rounds < saveCustomAfter && saveCustomAfter > 0)
			{
				IEnumerator original = SaveRestore.Save(ConVar.Server.rootFolder+"/"+World.SaveFileName, true);					
				while (original.MoveNext()) {} 
				Debug.Log("Saving complete");
				if (!callOnServerSave) Interface.Oxide.DataFileSystem.WriteObject(this.Title, new List<object>(new object[] { ConVar.Server.rootFolder+"/"+World.SaveFileName, "default" }) );
				Rounds++;
				CallOnServerSave();
			}
			else
			{
				string file = saveFolder + World.SaveFileName;
				DirectoryEx.Backup(SaveFolders());
				yield return waitU;
				ConVar.Server.GetServerFolder("saves/0/");
				yield return waitU;
				try {
					IEnumerator custom = SaveRestore.Save(file, true);					
					while (custom.MoveNext()) {}
					Debug.Log("Custom Saving complete");
					if (!callOnServerSave) Interface.Oxide.DataFileSystem.WriteObject(this.Title, new List<object>(new object[] { file, "custom" }) );}
				catch { PrintWarning(lang.GetMessage("dirnotfound", this)); }
				CallOnServerSave();
				Rounds = 0;
			}
			yield return null;
		}

		void OnPluginUnloaded(Plugin name)
		{
			if (Interface.Oxide.IsShuttingDown && !wasShutDown)
			{
				wasShutDown = true;
				saveRestore.timedSave = true;
			}
		}

		void OnServerSave(object file = null)
		{
			string type;
			if (file == null)
			{
				file = ConVar.Server.rootFolder+"/"+World.SaveFileName;
				type = "default";
			}
			else
				type = "custom";
			Interface.Oxide.DataFileSystem.WriteObject(this.Title, new List<object>(new object[] { file, type }) );
		}
		
		object OnSaveLoad(Dictionary<BaseEntity, ProtoBuf.Entity> dictionary)
		{
			if (Initialized || loadReload || !enableLoadOverride) return null;
			if (!loadReload)
			{
			List<string> filename = Interface.Oxide.DataFileSystem.ReadObject<List<string>>(this.Title);
			if (filename != null && filename.Count == 2)
				if (filename[1] == "custom")
				{
					loadReload = true;
					if (SaveRestore.Load(filename[0], allowOutOfDateSaves))
					{
						if (dictionary != null)
							dictionary.Clear();
						Puts(lang.GetMessage("loadoverride", this));
						return true;
					}
				}
			}
			return null;
		}
		
		void OnNewSave(string strFilename)
		{
			if (Initialized || loadReload || !enableLoadOverride) return;
			List<string> filename = Interface.Oxide.DataFileSystem.ReadObject<List<string>>(this.Title);
			if (filename != null && filename.Count == 2 && !filename[0].Contains(World.SaveFileName))
			{
				Puts(lang.GetMessage("loadoverridecancel", this));
				return;
			}
			if (filename != null && filename.Count == 2)
				if (filename[1] == "custom")
				{
					loadReload = true;
					if (SaveRestore.Load(filename[0], allowOutOfDateSaves))
						Puts(lang.GetMessage("loadoverride", this));
				}
		}
		
		void CallOnServerSave()
		{
			if (Interface.Oxide.IsShuttingDown)
				return;
			if (callOnServerSave)
				NextTick( () => timer.Once(delayCallOnServerSave, () =>
				{
					if (onServerSaveUseCoroutine)
						ServerMgr.Instance.StartCoroutine(SaveCoroutine());
					else
						Interface.CallHook("OnServerSave", null);
				}));
		}

		IEnumerator SaveCoroutine()
		{
			WaitForFixedUpdate waitU = new WaitForFixedUpdate();
			var allPlugins = plugins.GetAll().Where(r => !r.IsCorePlugin);
			foreach (var plugin in allPlugins.ToList())
			{
				plugin.CallHook("OnServerSave", null);
				yield return waitU;
			}
			yield return null;
		}		
		
		[ConsoleCommand("smm.save")]
		void cMapSave(ConsoleSystem.Arg arg)
		{
			if(arg.Connection != null && arg.Connection.authLevel < 2) return;
			if (SaveRestore.IsSaving) {
				SendReply(arg, lang.GetMessage("alreadysaving", this, arg.Connection != null ? arg.Connection.userid.ToString() : null ));
				return;
			}
			SaveBackupCreate();
			string saveName;
			saveName = saveFolder + World.SaveFileName;
			foreach (BaseEntity current in BaseEntity.saveList)
				current.InvalidateNetworkCache();
			Diag.Stopwatch stopwatch = Diag.Stopwatch.StartNew();
			UnityEngine.Debug.Log("Invalidate Network Cache took " + stopwatch.Elapsed.TotalSeconds.ToString("0.00") + " seconds");
			try {
				BaseEntity.saveList.RemoveWhere(p => !p);
				BaseEntity.saveList.RemoveWhere(p => p == null);
				IEnumerator enumerator = SaveRestore.Save(saveName, true);
				while (enumerator.MoveNext()) {}
				Interface.Oxide.DataFileSystem.WriteObject(this.Title, new List<object>(new object[] { saveName, "custom" }) );
				arg.ReplyWith(lang.GetMessage("customsavecomplete", this, arg.Connection != null ? arg.Connection.userid.ToString() : null )); }
			catch { PrintWarning(lang.GetMessage("dirnotfound", this)); }
			CallOnServerSave();
		}
		
		[ConsoleCommand("server.savemymap")]
		void cMapServerSave(ConsoleSystem.Arg arg)
		{		
			cMapSave(arg);
		}
		
		[ConsoleCommand("smm.loadmap")]
		void cLoadMap(ConsoleSystem.Arg arg)
		{
			if(arg.Connection != null && arg.Connection.authLevel < 2) return;
			if (arg.Args == null || arg.Args.Length != 1 || arg.Args[0] != "force")
			{
				SendReply(arg, lang.GetMessage("needconfirm", this, arg.Connection != null ? arg.Connection.userid.ToString() : null ));
				return;
			}
			foreach (var player in BasePlayer.activePlayerList.ToList())
				player.Kick(lang.GetMessage("kickreason", this, player.UserIDString));
			SaveRestore.Load(ConVar.Server.rootFolder+"/"+World.SaveFileName, allowOutOfDateSaves);
		}

		[ConsoleCommand("smm.loadfile")]
		void cLoadFile(ConsoleSystem.Arg arg)
		{
			if(arg.Connection != null && arg.Connection.authLevel < 2) return;
			if (arg.Args == null || arg.Args.Length < 1 )
			{
					SendReply(arg, lang.GetMessage("definefilename", this, arg.Connection != null ? arg.Connection.userid.ToString() : null ));
					return;
			}
			int folderNumber = -1;
			if (arg.Args[0].Length <= 4 && arg.Args[0] != "last" && !int.TryParse(arg.Args[0], out folderNumber))
			{
					SendReply(arg, lang.GetMessage("lastfilename", this, arg.Connection != null ? arg.Connection.userid.ToString() : null ));
					return;
			}			
			string file = "";
			if (arg.Args[0] == "last")
			{
				List<string> filename = Interface.Oxide.DataFileSystem.ReadObject<List<string>>(this.Title);
				if (filename != null)
					file = filename.First();
			}
			else if (int.TryParse(arg.Args[0], out folderNumber))
			{
				file = $"{ConVar.Server.rootFolder}/saves/{folderNumber}/{World.SaveFileName}";
			}
			if (file == "")
				file = saveFolder + arg.Args[0];

			foreach (var player in BasePlayer.activePlayerList.ToList())
				player.Kick(lang.GetMessage("kickreason", this));
			foreach (BaseEntity current in BaseEntity.saveList.ToList())
				if (current != null)
					current.Kill();
			BaseEntity.saveList.Clear();
			ItemManager.DoRemoves();
			if (SaveRestore.Load(file, allowOutOfDateSaves))
			{
				if (saveAfterLoadFile)
				{
					foreach (BaseEntity current in BaseEntity.saveList)
						current.InvalidateNetworkCache();
					SaveRestore.Save(true);
				}
			}
			else
			{
				SendReply(arg, lang.GetMessage("filenotfound", this, arg.Connection != null ? arg.Connection.userid.ToString() : null ));
				return;
			}
		}
		
		[ConsoleCommand("smm.loadnamed")]
		void cLoadNamed(ConsoleSystem.Arg arg)
		{
			if(arg.Connection != null && arg.Connection.authLevel < 2) return;
			if (arg.Args == null || arg.Args.Length < 1 )
			{
					SendReply(arg, lang.GetMessage("definefilename", this, arg.Connection != null ? arg.Connection.userid.ToString() : null ));
					return;
			}
			foreach (var player in BasePlayer.activePlayerList.ToList())
				player.Kick(lang.GetMessage("kickreason", this));
			foreach (BaseEntity current in BaseEntity.saveList.ToList())
				if (current != null)
					current.Kill();
			BaseEntity.saveList.Clear();
			ItemManager.DoRemoves();
			if (SaveRestore.Load(ConVar.Server.rootFolder+"/"+arg.Args[0], true))
			{
				if (saveAfterLoadFile)
				{
					foreach (BaseEntity current in BaseEntity.saveList)
						current.InvalidateNetworkCache();
					SaveRestore.Save(true);
				}
			}
			else
			{
				SendReply(arg, lang.GetMessage("filenotfound", this, arg.Connection != null ? arg.Connection.userid.ToString() : null ));
				return;
			}
		}

		[ConsoleCommand("smm.savefix")]
		void cLoadFix(ConsoleSystem.Arg arg)
		{
			if(arg.Connection != null && arg.Connection.authLevel < 2) return;
			BaseEntity.saveList.RemoveWhere(p => !p);
            BaseEntity.saveList.RemoveWhere(p => p == null);
			foreach (BaseEntity current in BaseEntity.saveList)
				current.InvalidateNetworkCache();
		}

		Int32 UnixTimeStampUTC()
		{
			Int32 unixTimeStamp;
			DateTime currentTime = DateTime.Now;
			DateTime zuluTime = currentTime.ToUniversalTime();
			DateTime unixEpoch = new DateTime(1970, 1, 1);
			unixTimeStamp = (Int32)(zuluTime.Subtract(unixEpoch)).TotalSeconds;
			return unixTimeStamp;
		}
		
		string [] SaveFolders()
		{
			string [] dp = new string[numberOfSaves];
			for (int i = 0; i < numberOfSaves; i++)
				dp[i] = $"{ConVar.Server.rootFolder}/saves/{i}/";
			return dp;
		}
		
		void SaveBackupCreate()
		{
			DirectoryEx.Backup(SaveFolders());
			ConVar.Server.GetServerFolder("saves/0/");
		}
	}
}