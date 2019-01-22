using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Reflection;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;
using ProtoBuf;
using Network;

namespace Oxide.Plugins
{
	[Info("Backup Extended", "Fujikura", "1.0.1")]
	class BackupExtended : RustPlugin
	{
		bool Changed;
		bool _backup;
		int currentRetry;
		bool wasShutDown;
		string [] backupFolders;
		string [] backupFoldersOxide;
		string [] backupFoldersShutdown;
		string [] backupFoldersShutdownOxide;

		int numberOfBackups;
		bool backupBroadcast;
		int backupDelay;
		bool useBroadcastDelay;
		string prefix;
		string prefixColor;
		bool useTimer;
		int timerInterval;
		int maxPlayers;
		int maxRetry;
		int delayRetrySeconds;
		string currentIdentity;
		bool includeOxideInBackups;

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
			numberOfBackups = Convert.ToInt32(GetConfig("Settings", "numberOfBackups", 8));
			includeOxideInBackups = Convert.ToBoolean(GetConfig("Settings", "includeOxideInBackups", true));
			backupBroadcast = Convert.ToBoolean(GetConfig("Notification", "backupBroadcast", false));
			backupDelay = Convert.ToInt32(GetConfig("Notification", "backupDelay", 5));
			useBroadcastDelay = Convert.ToBoolean(GetConfig("Notification", "useBroadcastDelay", true));
			prefix = Convert.ToString(GetConfig("Notification", "prefix", "BACKUP"));
			prefixColor = Convert.ToString(GetConfig("Notification", "prefixColor", "orange"));
			useTimer = Convert.ToBoolean(GetConfig("Timer", "useTimer", false));
			timerInterval = Convert.ToInt32(GetConfig("Timer", "timerInterval", 3600));
			maxPlayers = Convert.ToInt32(GetConfig("Timer", "maxPlayers", 20));
			maxRetry =  Convert.ToInt32(GetConfig("Timer", "maxRetry", 10));
			delayRetrySeconds = Convert.ToInt32(GetConfig("Timer", "delayRetrySeconds", 120));

			if (!Changed) return;
			SaveConfig();
			Changed = false;
		}

		void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
			                      {
									{"backupfinish", "Backup process finished."},
									{"backupannounce", "Starting server backup in {0} seconds."},
									{"backuprunning", "Running server backup."},
									{"backupautomatic", "Running automated server backup every {0} seconds."},
									{"backupdelay", "Backup delayed ({0} of {1}) for next '{2}' seconds."},
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
			wasShutDown = false;
		}

		void OnServerInitialized()
        {
			currentIdentity = ConVar.Server.rootFolder.Replace("server/", "");
			if (Interface.Oxide.CommandLine.HasVariable("server.identity"))
				currentIdentity = Interface.Oxide.CommandLine.GetVariable("server.identity");
			backupFolders = BackupFolders();
			backupFoldersShutdown = BackupFoldersShutdown();
			if (includeOxideInBackups)
			{
				backupFoldersOxide = BackupFoldersOxide();
				backupFoldersShutdownOxide = BackupFoldersShutdownOxide();
			}
			currentRetry = 0;
			if (useTimer)
			{
				timer.Once(timerInterval, TimerCheck);
				Puts(string.Format(lang.GetMessage("backupautomatic", this), timerInterval));
			}
        }

		void OnPluginUnloaded(Plugin name)
		{
			if (Interface.Oxide.IsShuttingDown && !wasShutDown)
			{
				wasShutDown = true;
				try {
					DirectoryEx.Backup(BackupFoldersShutdown());
					if (includeOxideInBackups)
						DirectoryEx.Backup(BackupFoldersShutdownOxide());
					} catch {}
			}
		}

		IEnumerator BackupCreateI(bool manual = false)
		{
			DirectoryEx.Backup(BackupFolders());
			yield return new WaitForEndOfFrame();
			DirectoryEx.CopyAll(ConVar.Server.rootFolder, backupFolders[0]);
			yield return new WaitForEndOfFrame();
			if (includeOxideInBackups)
				DirectoryEx.CopyAll("oxide", backupFoldersOxide[0]);
			if (!manual)
				Puts(lang.GetMessage("backupfinish", this));
			yield return null;
		}

		void BackupCreate(bool manual = false)
		{
			DirectoryEx.Backup(BackupFolders());
			DirectoryEx.CopyAll(ConVar.Server.rootFolder, backupFolders[0]);
			if (includeOxideInBackups)
				DirectoryEx.CopyAll("oxide", backupFoldersOxide[0]);
			if (!manual)
				Puts(lang.GetMessage("backupfinish", this));
		}

		void TimerCheck()
		{
			if (SaveRestore.IsSaving)
			{
				timer.Once(1f, TimerCheck);
				return;
			}
			if (BasePlayer.activePlayerList.Count > maxPlayers && currentRetry < maxRetry)
			{
				currentRetry++;
				Puts(string.Format(lang.GetMessage("backupdelay", this), currentRetry, maxRetry, delayRetrySeconds));
				timer.Once(delayRetrySeconds, TimerCheck);
			}
			else
			{
				currentRetry = 0;
				ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), "extbackup");
				timer.Once(timerInterval, TimerCheck);
			}
		}

		[ConsoleCommand("extbackup")]
		void ccmdExtBackup(ConsoleSystem.Arg arg)
		{
			if(arg.Connection != null && arg.Connection.authLevel < 2) return;
			if (backupBroadcast)
			{
				if (useBroadcastDelay)
				{
					SendReply(arg, string.Format(lang.GetMessage("backupannounce", this, arg.Connection != null ? arg.Connection.userid.ToString() : null ), backupDelay));
					BroadcastChat(string.Format(lang.GetMessage("backupannounce", this), backupDelay));
					timer.Once(backupDelay, () => BackupRun(arg));
				}
				else
				{
					timer.Once(0f, () => BackupRun(arg));
				}
			}
			else
				timer.Once(0f, () => BackupRun(arg));
		}

		void BackupRun(ConsoleSystem.Arg arg)
		{
			if (backupBroadcast)
				BroadcastChat(lang.GetMessage("backuprunning", this));
			SendReply(arg, lang.GetMessage("backuprunning", this, arg.Connection != null ? arg.Connection.userid.ToString() : null ));
			NextFrame( () => ServerMgr.Instance.StartCoroutine(BackupCreateI(true)));
			SendReply(arg, lang.GetMessage("backupfinish", this, arg.Connection != null ? arg.Connection.userid.ToString() : null ));
			if (backupBroadcast)
				BroadcastChat(lang.GetMessage("backupfinish", this));
		}

		string [] BackupFolders()
		{
			string [] dp = new string[numberOfBackups];
			for (int i = 0; i < numberOfBackups; i++)
				dp[i] = $"backup/{i}/"+ currentIdentity;
			return dp;
		}

		string [] BackupFoldersOxide()
		{
			string [] dp = new string[numberOfBackups];
			for (int i = 0; i < numberOfBackups; i++)
				dp[i] = $"backup/{i}/oxide";
			return dp;
		}

		string [] BackupFoldersShutdown()
		{
			string [] dp = new string[numberOfBackups];
			for (int i = 3; i < numberOfBackups; i++)
				dp[i] = $"backup/{i}/" + currentIdentity;
			return dp;
		}

		string [] BackupFoldersShutdownOxide()
		{
			string [] dp = new string[numberOfBackups];
			for (int i = 3; i < numberOfBackups; i++)
				dp[i] = $"backup/{i}/oxide";
			return dp;
		}

		void BroadcastChat(string msg = null) => PrintToChat(msg == null ? prefix : "<color=" + prefixColor + ">" + prefix + "</color>: " + msg);
	}
}
