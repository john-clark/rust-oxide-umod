using Oxide.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Plugins;
using System.Collections;
using System.Globalization;

namespace Oxide.Plugins
{
    [Info("LocalTimeDamageControl", "CARNY666", "1.0.1", ResourceId = 2720)]

    class LocalTimeDamageControl : RustPlugin
    {
        const string adminPriv = "LocalTimeDamageControl.admin";

        void Init()
        {
            try
            { 
                PrintToChat($"{this.Title} {this.Version} Initialized @ {DateTime.Now.ToLongTimeString()}...");
            }
            catch (Exception ex)
            {
                PrintError($"Error Init: {ex.StackTrace}");
            }

        }

        void Loaded()
        {
            try
            {
                lang.RegisterMessages(new Dictionary<string, string>
                {
                    { "localtime", "Local time is {localtime}." },
                    { "nodamage", "You cannot damage buildings during this time {starttime} and {endtime}." },
                    { "starts", "LocalTimeDamageControl starts at {starts}." },
                    { "remains", "LocalTimeDamageControl remains on for {remains}." },
                    { "status", "LocalTimeDamageControl is {status}." },
                    { "duration", "LocalTimeDamageControl duration is {duration} minutes." },
                    { "errorstart", "Error, please 24 hour time format: i.e 08:00 for 8 am." },
                    { "errormin", "Error, please enter an integer i.e: 60 for 60 minutes." },
                    { "errorhour", "Error, please enter an integer i.e: 2 for 180 minutes." },
                    { "help1", "/lset start 08:00 ~ Set start time for damage control." },
                    { "help2", "/lset minutes 60  ~ Set duration in minutes for damage control."},
                    { "help3", "/lset hours 12    ~ Set duration in hours for damage control."},
                    { "help4", "/lset off         ~ Turn off damage control."},
                    { "help5", "/lset on          ~ Turn on damage control during set times. "},
                    { "help6", "- starts at {starttime} ends at {endtime}."}
            }, this, "en");


                if ((bool)Config["LocalTimeDamageControlOn"])
                {
                    PrintWarning("LocalTimeDamageControl starts at " + Config["LocalTimeDamageControlStart"]);
                    PrintWarning("LocalTimeDamageControl remains on for " + Config["LocalTimeDamageControlDuratationMinutes"] + " minutes.");                
                }
                else
                {
                    PrintWarning("LocalTimeDamageControl is off.");
                }
                permission.RegisterPermission(adminPriv, this);
                PrintWarning(adminPriv + " privilidge is registered.");

            }
            catch (Exception ex)
            {
                PrintError($"Error Loaded: {ex.StackTrace}");
            }

        }

        void Unload()
        {
            try
            { 
            }
            catch (Exception ex)
            {
                PrintError($"Error Unload: {ex.StackTrace}");
            }


        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file.");

            Config.Clear();

            Config["LocalTimeDamageControlStart"] = "8:30";               // 8:30 am
            Config["LocalTimeDamageControlDuratationMinutes"] = (8 * 60); // 8 hrs

            Config["LocalTimeDamageControlOn"] = true;

            SaveConfig();

        }

        public bool IsDamageControlOn()
        {
            if ((bool)Config["LocalTimeDamageControlOn"] == false) return false;

            DateTime startTime = DateTime.ParseExact(Config["LocalTimeDamageControlStart"].ToString(), "hh:mm tt", CultureInfo.InvariantCulture);
            DateTime endTime = DateTime.ParseExact(Config["LocalTimeDamageControlStart"].ToString(), "hh:mm tt", CultureInfo.InvariantCulture).AddMinutes(int.Parse(Config["LocalTimeDamageControlDuratationMinutes"].ToString())) ;

            if ((DateTime.Now.ToLocalTime() >= startTime) && (DateTime.Now.ToLocalTime() <= endTime))
                return true;

            return false;
        }

        public DateTime getStartTime()
        {
            return DateTime.Parse(Config["LocalTimeDamageControlStart"].ToString());
        }

        public DateTime getEndTime()
        {
            return DateTime.Parse(Config["LocalTimeDamageControlStart"].ToString()).AddMinutes(int.Parse(Config["LocalTimeDamageControlDuratationMinutes"].ToString()));
        }

        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            try
            {               
                if (IsDamageControlOn() == false) return null;                  // check if on or off
                if (entity is BasePlayer) return null;                          // damage to players ok!!
                if (entity.OwnerID == info.InitiatorPlayer.userID) return null; // owner can damage own stuff

                if (info.InitiatorPlayer != null)
                    PrintToChat(info.InitiatorPlayer, lang.GetMessage("nodamage", this, info.InitiatorPlayer.UserIDString).Replace("{starttime}", getStartTime().ToLongTimeString()).Replace("{endtime}", getEndTime().ToLongTimeString()));

                info.damageTypes.ScaleAll(0.0f);                                // no damage
                return false;
            }
            catch (Exception ex)
            {
                PrintError("Error OnEntityTakeDamage: " + ex.Message);
                
            }
            return null;
        }

        [ChatCommand("lset")]
        private void lset(BasePlayer player, string command, string[] args)
        {
            PrintToChat(player, lang.GetMessage("localtime", this, player.UserIDString).Replace("{localtime}", DateTime.Now.ToLocalTime().ToLongTimeString()));

            if (!permission.UserHasPermission(player.UserIDString, adminPriv))
                return;

            if (args.Count() > 0)
            {
                #region toggle
                if (args[0].ToLower() == "off")
                {
                    Config["LocalTimeDamageControlOn"] = false;
                    PrintToChat(lang.GetMessage("status", this, player.UserIDString).Replace("{status}", ((bool)Config["LocalTimeDamageControlOn"] ? "ON" : "OFF")));
                    SaveConfig();
                    return;
                }

                if (args[0].ToLower() == "on")
                {
                    Config["LocalTimeDamageControlOn"] = true;
                    PrintToChat(lang.GetMessage("status", this, player.UserIDString).Replace("{status}", ((bool)Config["LocalTimeDamageControlOn"] ? "ON" : "OFF")));
                    SaveConfig();
                    return;
                }
                #endregion

                if (args[0].ToLower() == "start")
                {
                    try
                    {
                        DateTime dateTime = DateTime.ParseExact(args[1].ToUpper(), "HH:mm", CultureInfo.InvariantCulture);
                        Config["LocalTimeDamageControlStart"] = dateTime.ToString("hh:mm tt" , CultureInfo.CurrentCulture);

                        SaveConfig();
                        PrintToChat(player, lang.GetMessage("starts", this, player.UserIDString).Replace("{starts}", Config["LocalTimeDamageControlStart"].ToString()));
                    }
                    catch (Exception)
                    {
                        PrintToChat(player, lang.GetMessage("errorstart", this, player.UserIDString));
                        
                    }
                    return;
                }
                if (args[0].ToLower() == "minutes")
                {
                    try
                    {
                        Config["LocalTimeDamageControlDuratationMinutes"] = int.Parse(args[1]);
                        SaveConfig();
                        PrintToChat(lang.GetMessage("duration", this, player.UserIDString).Replace("{duration}", Config["LocalTimeDamageControlDuratationMinutes"].ToString()));
                    }
                    catch (Exception)
                    {
                        PrintToChat(player, lang.GetMessage("errormin", this, player.UserIDString));

                    }
                    return;
                }
                if (args[0].ToLower() == "hours")
                {
                    try
                    {
                        Config["LocalTimeDamageControlDuratationMinutes"] = int.Parse(args[1]) * 60;
                        SaveConfig();
                        PrintToChat(lang.GetMessage("duration", this, player.UserIDString).Replace("{duration}", Config["LocalTimeDamageControlDuratationMinutes"].ToString()));
                    }
                    catch (Exception)
                    {
                        PrintToChat(player, lang.GetMessage("errorhour", this, player.UserIDString));
                    }
                    return;
                }

            }
            else
            {
                for (int ii = 1; ii <= 6; ii++)
                    PrintToChat(player, lang.GetMessage("help" + ii, this, player.UserIDString).Replace("{starttime}", getStartTime().ToLongTimeString()).Replace("{endtime}", getEndTime().ToLongTimeString()));
            }


        }

    }
}