using System;
using System.Collections.Generic;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Timed Execute", "PaiN", "0.7.3", ResourceId = 1937)]
    [Description("Execute commands every (x) seconds.")]
    class TimedExecute : CovalencePlugin
    {
        public static TimedExecute Plugin;

        public enum Types
        {
            RealTime,
            InGameTime,
            Repeater,
            TimerOnce
        };

        Timer ptimer;

        #region Classes
        class Timers
        {
            public static List<Timer> AllTimers = new List<Timer>();
            public static Timer InGame;
            public static Timer Real;
            public static Timer Repeat;
            public static Timer Once;

            public static void ResetTimer(Types type)
            {
                switch (type)
                {
                    case Types.InGameTime:
                        RunTimer(Types.InGameTime);
                        break;

                    case Types.RealTime:
                        RunTimer(Types.RealTime);
                        break;

                    case Types.Repeater:
                        RunTimer(Types.Repeater);
                        break;

                    case Types.TimerOnce:
                        RunTimer(Types.TimerOnce);
                        break;
                }
            }

            public static void RunTimer(Types type)
            {
				float timeinterval = 1f;
				#if RUST
                timeinterval = 4.5f;
				#endif
				
                switch (type)
                {
                    case Types.InGameTime:
                         if (InGame != null) InGame.Destroy();
                         Plugin.Puts("The InGame timer has started");
                         AllTimers.Add(InGame = Plugin.timer.Repeat(timeinterval, 0, () =>
                         {
                             foreach (var cmd in Plugin.Config["InGameTime-Timer"] as Dictionary<string, object>)
                                 if (Plugin.covalence.Server.Time.ToShortTimeString() == cmd.Key)
                                 {
                                     Plugin.covalence.Server.Command(cmd.Value.ToString());
                                     Plugin.Puts(string.Format("ran CMD: {0}", cmd.Value));
                                 }
                         }
                         ));
                         break;
                     
                    case Types.RealTime:
                        if (Real != null) Real.Destroy();
                        Plugin.Puts("The RealTime timer has started");
                        AllTimers.Add(Real = Plugin.timer.Repeat(1, 0, () =>
                        {
                            foreach (var cmd in Plugin.Config["RealTime-Timer"] as Dictionary<string, object>)
                                if (System.DateTime.Now.ToString("HH:mm:ss") == cmd.Key.ToString())
                                {
                                    Plugin.covalence.Server.Command(cmd.Value.ToString());
                                    Plugin.Puts(string.Format("ran CMD: {0}", cmd.Value));
                                }
                        }
                        ));
                        break;

                    case Types.Repeater:
                        if (Repeat != null) Repeat.Destroy();
                        Plugin.Puts("The Repeat timer has started");
                        foreach (var cmd in Plugin.Config["TimerRepeat"] as Dictionary<string, object>)
                        {
                            Repeat = Plugin.timer.Repeat(Convert.ToSingle(cmd.Value), 0, () => {
                                Plugin.covalence.Server.Command(cmd.Key);
                                Plugin.Puts(string.Format("ran CMD: {0}", cmd.Key));
                            });
                        }
                        AllTimers.Add(Repeat);
                        break;

                    case Types.TimerOnce:
                        if (Once != null) Once.Destroy();
                        Plugin.Puts("The Timer-Once timer has started");
                        foreach (var cmd in Plugin.Config["TimerOnce"] as Dictionary<string, object>)
                        {
                            Once = Plugin.timer.Once(Convert.ToSingle(cmd.Value), () => {
                                Plugin.covalence.Server.Command(cmd.Key);
                                Plugin.Puts(string.Format("ran CMD: {0}", cmd.Key));
                            });
                        }
                        AllTimers.Add(Once);
                        break;
                }
            }

            public static void DestroyAll()
            {
                foreach (Timer tim in AllTimers)
                    if (tim != null)
                        tim.Destroy();
            }

            public static void RunAll()
            {
                if (Convert.ToBoolean(Plugin.Config["EnableInGameTime-Timer"]) == true)
                    RunTimer(Types.InGameTime);

                if (Convert.ToBoolean(Plugin.Config["EnableRealTime-Timer"]) == true)
                    RunTimer(Types.RealTime);

                if (Convert.ToBoolean(Plugin.Config["EnableTimerRepeat"]) == true)
                    RunTimer(Types.Repeater);

                if (Convert.ToBoolean(Plugin.Config["EnableTimerOnce"]) == true)
                    RunTimer(Types.TimerOnce);
            }
        }
        #endregion

        void OnServerInitialized()
        {
            Plugin = this;
            Timers.RunAll();

        }

        void Unloaded()
        {
            Timers.DestroyAll();
        }

        Dictionary<string, object> repeatcmds = new Dictionary<string, object>();
        Dictionary<string, object> chaincmds = new Dictionary<string, object>();
        Dictionary<string, object> realtimecmds = new Dictionary<string, object>();
        Dictionary<string, object> ingamecmds = new Dictionary<string, object>();

        protected override void LoadDefaultConfig()
        {
            repeatcmds.Add("command1 arg", 300);
            repeatcmds.Add("command2 'msg'", 300);
            Puts("Creating a new configuration file!");
            if (Config["TimerRepeat"] == null) Config["TimerRepeat"] = repeatcmds;

            chaincmds.Add("command1 'msg'", 60);
            chaincmds.Add("command2 'msg'", 120);
            chaincmds.Add("command3 arg", 180);
            chaincmds.Add("reset.timeronce", 181);
            if (Config["TimerOnce"] == null) Config["TimerOnce"] = chaincmds;

            if (Config["EnableTimerRepeat"] == null) Config["EnableTimerRepeat"] = true;
            if (Config["EnableTimerOnce"] == null) Config["EnableTimerOnce"] = true;
            if (Config["EnableRealTime-Timer"] == null) Config["EnableRealTime-Timer"] = true;
            if (Config["EnableInGameTime-Timer"] == null) Config["EnableInGameTime-Timer"] = true;

            realtimecmds.Add("16:00:00", "command1 arg");
            realtimecmds.Add("16:30:00", "command2 arg");
            realtimecmds.Add("17:00:00", "command3 arg");
            realtimecmds.Add("18:00:00", "command4 arg");
            if (Config["RealTime-Timer"] == null) Config["RealTime-Timer"] = realtimecmds;

            ingamecmds.Add("01:00", "weather rain");
            ingamecmds.Add("12:00", "command 1");
            ingamecmds.Add("15:00", "command 2");
            if (Config["InGameTime-Timer"] == null) Config["InGameTime-Timer"] = ingamecmds;
        }

        [Command("reset.timeronce", "resettimeronce")]
        void cmdReset(IPlayer player, string cmd, string[] args)
        {
            if (player.IsAdmin)
                Timers.ResetTimer(Types.TimerOnce);
        }
    }
}