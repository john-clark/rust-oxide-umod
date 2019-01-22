using System.Text.RegularExpressions;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using System.Linq;
using System;

namespace Oxide.Plugins
{
    [Info("Advert Messages", "LaserHydra", "3.0.1", ResourceId = 1510)]
    [Description("Timed chat messages to work as informational messages")]
    internal class AdvertMessages : CovalencePlugin
    {
        private readonly Random random = new Random(DateTime.Now.Millisecond);
        private int lastAdvert = 0;

        #region Hooks and other Methods

        private void Loaded()
        {
            LoadConfig();
            
            Puts($"AdvertMessages is showing adverts each {Configuration.AdvertInterval} minutes.");
            timer.Repeat(Configuration.AdvertInterval * 60, 0, Advert);
        }

        private void Advert()
        {
            if (Configuration.Messages.Count == 0)
                return;

            int advert;

            if (Configuration.Messages.Count > 1)
            {
                do advert = random.Next(0, Configuration.Messages.Count - 1);
                while (advert == lastAdvert);
            }
            else
                advert = 0;

            lastAdvert = advert;

            server.Broadcast(SimpleColorFormat((string)Configuration.Messages[advert]));

            if (Configuration.BroadcastToConsole)
                Puts(SimpleColorFormat((string)Configuration.Messages[advert], true));
        }

        #endregion

        #region Configuration

        private struct Configuration
        {
            public static List<object> Messages = new List<object>
            {
                "Welcome to our server, have fun!",
                "<orange>Need help?<end> Try calling for an <cyan>admins<end> in the chat.",
                "Please treat everybody respectfully.",
                "Cheating will result in a <red>permanent<end> ban.",
                "This server is running <orange>Oxide 2<end>."
            };

            public static float AdvertInterval = 10;
            public static bool BroadcastToConsole = true;
        }

        private new void LoadConfig()
        {
            GetConfig(ref Configuration.Messages, "Messages");
            GetConfig(ref Configuration.AdvertInterval, "Settings", "Adverts Interval (In Minutes)");
            GetConfig(ref Configuration.BroadcastToConsole, "Settings", "Broadcast To Console");

            SaveConfig();
        }

        protected override void LoadDefaultConfig() => PrintWarning("Generating new config file...");

        #endregion

        #region Helpers

        public string SimpleColorFormat(string text, bool removeTags = false)
        {
            /*  Simple Color Format ( v3.0 ) by SkinN - Modified by LaserHydra
                Formats simple color tags to game dependant color codes */

            // All patterns
            Regex end = new Regex(@"\<(end?)\>"); // End tags
            Regex clr = new Regex(@"\<(\w+?)\>"); // Names
            Regex hex = new Regex(@"\<#(\w+?)\>"); // Hex codes

            // Replace tags
            text = end.Replace(text, "[/#]");
            text = clr.Replace(text, "[#$1]");
            text = hex.Replace(text, "[#$1]");

            return removeTags ? Formatter.ToPlaintext(text) : covalence.FormatText(text);
        }

        private void GetConfig<T>(ref T variable, params string[] path)
        {
            if (path.Length == 0)
                return;

            if (Config.Get(path) == null)
            {
                Config.Set(path.Concat(new object[] { variable }).ToArray());
                PrintWarning($"Added field to config: {string.Join("/", path)}");
            }

            variable = (T)Convert.ChangeType(Config.Get(path), typeof(T));
        }

        #endregion
    }
}