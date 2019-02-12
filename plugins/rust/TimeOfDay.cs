using Oxide.Core.Plugins;
using Oxide.Core;
using System;
using System.Text;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("TimeOfDay", "FuJiCuRa", "2.3.4")]
    [Description("Does alter day and night duration.")]
    public class TimeOfDay : RustPlugin
    {
		bool Changed;
		bool Initialized;
		int componentSearchAttempts;
		TOD_Time timeComponent;
		bool activatedDay;

		int authLevelCmds;
		int authLevelFreeze;
		int dayLength;
		int nightLength;
		int presetDay;
		int presetMonth;
		int presetYear;
		bool setPresetDate;
		bool freezeDate;
		bool autoSkipNight;
		bool autoSkipDay;
		bool logAutoSkipConsole;
		bool freezeTimeOnload;
		float timeToFreeze;
		
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
			dayLength =  System.Convert.ToInt32(GetConfig("Settings", "dayLength", 30));
			nightLength =  System.Convert.ToInt32(GetConfig("Settings", "nightLength", 30));
			freezeDate = System.Convert.ToBoolean(GetConfig("Settings", "freezeDate", false));
			authLevelCmds = System.Convert.ToInt32(GetConfig("Settings", "authLevelCmds", 1));
			authLevelFreeze = System.Convert.ToInt32(GetConfig("Settings", "authLevelFreeze", 2));
			autoSkipNight = System.Convert.ToBoolean(GetConfig("Settings", "autoSkipNight", false));
			autoSkipDay = System.Convert.ToBoolean(GetConfig("Settings", "autoSkipDay", false));
			logAutoSkipConsole = System.Convert.ToBoolean(GetConfig("Settings", "logAutoSkipConsole", true));
			
			presetDay =  System.Convert.ToInt32(GetConfig("DatePreset", "presetDay", 1));
			presetMonth =  System.Convert.ToInt32(GetConfig("DatePreset", "presetMonth", 1));
			presetYear =  System.Convert.ToInt32(GetConfig("DatePreset", "presetYear", 2020));
			setPresetDate = System.Convert.ToBoolean(GetConfig("DatePreset", "setPresetDate", false));
			
			freezeTimeOnload = System.Convert.ToBoolean(GetConfig("TimeFreeze", "freezeTimeOnload", false));
			timeToFreeze = System.Convert.ToSingle(GetConfig("TimeFreeze", "timeToFreeze", 12.0));

			if (!Changed) return;
			SaveConfig();
			Changed = false;
		}

		protected override void LoadDefaultConfig()
		{
			Config.Clear();
			LoadVariables();
		}

		void Loaded()
		{
			LoadVariables();
			Initialized = false;
		}

		void Unload()
		{
			if (timeComponent == null || !Initialized) return;
			timeComponent.OnSunrise -= OnSunrise;
            timeComponent.OnSunset -= OnSunset;
			timeComponent.OnDay -= OnDay;
			timeComponent.OnHour -= OnHour;
		}

		void OnServerInitialized()
		{
			if (TOD_Sky.Instance == null)
            {
				componentSearchAttempts++;
                if (componentSearchAttempts < 10)
                    timer.Once(1, OnServerInitialized);
                else
                    PrintWarning("Could not find required component after 10 attempts. Plugin disabled");
                return;
            }
            timeComponent = TOD_Sky.Instance.Components.Time;
            if (timeComponent == null)
            {
                PrintWarning("Could not fetch time component. Plugin disabled");
                return;
            }
			if (setPresetDate)
			{
				TOD_Sky.Instance.Cycle.Day = presetDay;
				TOD_Sky.Instance.Cycle.Month = presetMonth;
				TOD_Sky.Instance.Cycle.Year = presetYear;
			}
			SetTimeComponent();
			if (freezeTimeOnload)
				StartupFreeze();
		}

        void SetTimeComponent()
        {
            timeComponent.ProgressTime = true;
            timeComponent.UseTimeCurve = false;
            timeComponent.OnSunrise += OnSunrise;
			timeComponent.OnSunset += OnSunset;
			timeComponent.OnDay += OnDay;
			timeComponent.OnHour += OnHour;
			Initialized = true;
            if (TOD_Sky.Instance.Cycle.Hour > TOD_Sky.Instance.SunriseTime && TOD_Sky.Instance.Cycle.Hour < TOD_Sky.Instance.SunsetTime)
                OnSunrise();
            else
                OnSunset();
        }
		
		void StartupFreeze()
		{
			if (!Initialized) return;
			timeComponent.ProgressTime = false;
			ConVar.Env.time = timeToFreeze;
		}

        void OnDay()
        {
			if (Initialized && freezeDate)
				--TOD_Sky.Instance.Cycle.Day;
		}

        void OnHour()
        {
			if (!Initialized) return;
			if (TOD_Sky.Instance.Cycle.Hour > TOD_Sky.Instance.SunriseTime && TOD_Sky.Instance.Cycle.Hour < TOD_Sky.Instance.SunsetTime && !activatedDay)
			{
				OnSunrise();
				return;
			}
			if ((TOD_Sky.Instance.Cycle.Hour > TOD_Sky.Instance.SunsetTime || TOD_Sky.Instance.Cycle.Hour < TOD_Sky.Instance.SunriseTime) && activatedDay)
			{
				OnSunset();
				return;
			}
		}

        void OnSunrise()
        {
			if (!Initialized) return;
			if (autoSkipDay && !autoSkipNight)
			{
				TOD_Sky.Instance.Cycle.Hour = TOD_Sky.Instance.SunsetTime;
				if (logAutoSkipConsole)
					Puts("Daytime autoskipped");
				OnSunset();
				return;
			}
			timeComponent.DayLengthInMinutes = dayLength * (24.0f / (TOD_Sky.Instance.SunsetTime - TOD_Sky.Instance.SunriseTime));
			if (!activatedDay)
				Interface.CallHook("OnTimeSunrise");
			activatedDay = true;
        }

        void OnSunset()
        {
			if (!Initialized) return;
			if (autoSkipNight)
			{
				float timeToAdd = (24 - TOD_Sky.Instance.Cycle.Hour) + TOD_Sky.Instance.SunriseTime;
				TOD_Sky.Instance.Cycle.Hour += timeToAdd;
				if (logAutoSkipConsole)
					Puts("Nighttime autoskipped");
				OnSunrise();
				return;
			}
			timeComponent.DayLengthInMinutes = nightLength * (24.0f / (24.0f - (TOD_Sky.Instance.SunsetTime - TOD_Sky.Instance.SunriseTime)));
			if (activatedDay)
				Interface.CallHook("OnTimeSunset");
			activatedDay = false;
        }
		
		[ConsoleCommand("tod.daylength")]
        void ConsoleDayLength(ConsoleSystem.Arg arg)
        {
            if (!Initialized) return;
			if (arg.Connection != null && arg.Connection.authLevel < authLevelCmds) return;
			if (arg.Args == null || arg.Args.Length < 1)
            {
                SendReply(arg, $"Current 'dayLength' setting is '{dayLength}'");
                return;
            }
			
			int newDaylength = 0;
			if (int.TryParse(arg.Args[0], out newDaylength))
			{
				if (newDaylength < 1)
				{
					SendReply(arg, $"The new daylength must be greater zero");
					return;
				}
			}
			dayLength = newDaylength;
			SendReply(arg, $"The 'dayLength' has been set to '{dayLength}'");
			
			if (TOD_Sky.Instance.IsDay)
				OnSunrise();
			else
				OnSunset();
			Config["Settings", "dayLength"] = dayLength;
			SaveConfig();
		}
		
		[ConsoleCommand("tod.nightlength")]
        void ConsoleNightLength(ConsoleSystem.Arg arg)
        {
            if (!Initialized) return;
			if (arg.Connection != null && arg.Connection.authLevel < authLevelCmds) return;
			if (arg.Args == null || arg.Args.Length < 1)
            {
                SendReply(arg, $"Current 'nightLength' setting is '{nightLength}'");
                return;
            }
			
			int newNightlength = 0;
			if (int.TryParse(arg.Args[0], out newNightlength))
			{
				if (newNightlength < 1)
				{
					SendReply(arg, $"The new nightlength must be greater zero");
					return;
				}
			}
			nightLength = newNightlength;
			SendReply(arg, $"The 'nightLength' has been set to '{nightLength}'");
			if (TOD_Sky.Instance.IsDay)
				OnSunrise();
			else
				OnSunset();
			Config["Settings", "nightLength"] = nightLength;
			SaveConfig();
		}
		
		[ConsoleCommand("tod.freezetime")]
        void ConsoleFreezeTime(ConsoleSystem.Arg arg)
        {
            if (!Initialized) return;
			if (arg.Connection != null && arg.Connection.authLevel < authLevelFreeze) return;

			timeComponent.ProgressTime = !timeComponent.ProgressTime;
			
			if (timeComponent.ProgressTime)
				SendReply(arg, $"The game time was unfreezed");
			else
				SendReply(arg, $"The game time was freezed");
		}

		[ConsoleCommand("tod.skipday")]
        void ConsoleSkipDay(ConsoleSystem.Arg arg)
        {
            if (!Initialized) return;
			if (arg.Connection != null && arg.Connection.authLevel < authLevelCmds) return;
			if (TOD_Sky.Instance.IsNight)
			{
				SendReply(arg, $"Night is already active");
				return;
			}
			OnSunset();
			TOD_Sky.Instance.Cycle.Hour = TOD_Sky.Instance.SunsetTime;
			SendReply(arg, $"Current daytime skipped");
		}

		[ConsoleCommand("tod.skipnight")]
        void ConsoleSkipNight(ConsoleSystem.Arg arg)
        {
            if (!Initialized) return;
			if (arg.Connection != null && arg.Connection.authLevel < authLevelCmds) return;
			if (TOD_Sky.Instance.IsDay)
			{
				SendReply(arg, $"Day is already active");
				return;
			}
			OnSunrise();
			TOD_Sky.Instance.Cycle.Hour = TOD_Sky.Instance.SunriseTime;
			SendReply(arg, $"Current nighttime skipped");
		}			

        [ChatCommand("tod")]
        private void TodCommand(BasePlayer player, string command, string[] args)
        {
			if (!Initialized)
				return;
			TimeSpan ts1= TimeSpan.FromHours(TOD_Sky.Instance.SunriseTime);
			TimeSpan ts2= TimeSpan.FromHours(TOD_Sky.Instance.SunsetTime);
			
            StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine(FormNeutralMessage("-------- Settings --------"));
			stringBuilder.AppendLine("Current Time".PadRight(15) + TOD_Sky.Instance.Cycle.DateTime.ToString("HH:mm:ss"));
			stringBuilder.AppendLine("Sunrise Hour".PadRight(15) + string.Format("{0}:{1}", System.Math.Truncate(ts1.TotalHours).ToString(), ts1.Minutes.ToString()));
			stringBuilder.AppendLine("Sunset Hour".PadRight(15) + string.Format("{0}:{1}", System.Math.Truncate(ts2.TotalHours).ToString(), ts2.Minutes.ToString()));
			stringBuilder.AppendLine("Daylength".PadRight(15) + dayLength.ToString() + " minutes");
			stringBuilder.Append("Nightlength".PadRight(15) + nightLength.ToString() + " minutes");
			PrintPluginMessageToChat(player, stringBuilder.ToString().TrimEnd());
        }

        [HookMethod("SendHelpText")]
        private void SendHelpText(BasePlayer player)
        {
            if (!Initialized) return;
			StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append(FormNeutralMessage("-------------------- Available Commands --------------------\n"));
			stringBuilder.Append(FormNeutralMessage("/tod") + " - Shows current Time Of Day.\n");
            PrintPluginMessageToChat(player, stringBuilder.ToString());
        }

        private void PrintPluginMessageToChat(BasePlayer player, string message)
        {
            PrintToChat(player, "<b><size=16>[<color=#ffa500ff>" + this.Name + "</color>]</size></b>\n" + message);
        }

        private void PrintPluginMessageToChat(string message)
        {
            PrintToChat("<b><size=16>[<color=#ffa500ff>" + this.Name + "</color>]</size></b>\n" + message);
        }

        private string FormNeutralMessage(string message)
        {
            return "<color=#c0c0c0ff>" + message + "</color>";
        }

    }
}