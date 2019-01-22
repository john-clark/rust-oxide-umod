/*
 * TODO:
 * Add command to list blocked words by partial match
 * Add command to search for blocked words
 * Add default colors to chat names
 * Add option to pull list(s) from remote URL
 * Add option to show uncensored message to admin
 * Add option to specific replace words with another word
 * Add separate list for misc blocked words
 * Add support for converting CAPS to Sentence case
 * Add support for jail plugins?
 * Allow multiple actions if desired
 */

using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Oxide.Plugins
{
    [Info("UFilter", "Wulf/lukespragg", "5.0.5")]
    [Description("Prevents advertising and/or profanity and optionally punishes player")]
    public class UFilter : CovalencePlugin
    {
        #region Configuration

        private Configuration config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Check for advertising (true/false)")]
            public bool CheckForAdvertising { get; set; } = true;

            [JsonProperty(PropertyName = "Check for profanity (true/false)")]
            public bool CheckForProfanity { get; set; } = true;

            //[JsonProperty(PropertyName = "Check for all capital letters (true/false)")]
            //public bool CheckForAllCaps { get; set; } = true;

            [JsonProperty(PropertyName = "Check player chat (true/false)")]
            public bool CheckChat { get; set; } = true;

            [JsonProperty(PropertyName = "Check player names (true/false)")]
            public bool CheckNames { get; set; } = true;

            //[JsonProperty(PropertyName = "Convert all caps to normal (true/false)")]
            //public bool ConvertAllCaps { get; set; } = true;

            [JsonProperty(PropertyName = "Log advertising (true/false)")]
            public bool LogAdvertising { get; set; } = false;

            [JsonProperty(PropertyName = "Log profanity (true/false)")]
            public bool LogProfanity { get; set; } = false;

            [JsonProperty(PropertyName = "Log to console (true/false)")]
            public bool LogToConsole { get; set; } = false;

            [JsonProperty(PropertyName = "Log to file (true/false)")]
            public bool LogToFile { get; set; } = false;

            //[JsonProperty(PropertyName = "Rotate logs daily (true/false)")]
            //public bool RotateLogs { get; set; } = true; // TODO: Finish implementing

            [JsonProperty(PropertyName = "Warn player in chat (true/false)")]
            public bool WarnInChat { get; set; } = true;

            [JsonProperty(PropertyName = "Action for advertising")]
            public string ActionForAdvertising { get; set; } = "block";

            [JsonProperty(PropertyName = "Action for profanity")]
            public string ActionForProfanity { get; set; } = "censor";

            [JsonProperty(PropertyName = "Word or symbol to use for censoring")]
            public string CensorText { get; set; } = "*";

            //[JsonProperty(PropertyName = "Color to use for admin chat")]
            //public string ColorAdmin { get; set; } = "*"; // TODO: Finish implementing

            //[JsonProperty(PropertyName = "Color to use for player chat")]
            //public string ColorPlayers { get; set; } = "*"; // TODO: Finish implementing

            [JsonProperty(PropertyName = "Allowed advertisements")]
            public List<string> AllowedAds { get; set; } = new List<string>();

            [JsonProperty(PropertyName = "Allowed profanity")]
            public List<string> AllowedProfanity { get; set; } = new List<string>();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            string configPath = $"{Interface.Oxide.ConfigDirectory}{Path.DirectorySeparatorChar}{Name}.json";
            LogWarning($"Could not load a valid configuration file, creating a new configuration file at {configPath}");
            config = new Configuration();
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion Configuration

        #region Localization

        private new void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandUsage"] = "Usage: {0} <add/remove> <word or phrase>",
                ["NoAdvertising"] = "Advertising is not allowed on this server",
                ["NoProfanity"] = "Profanity is not allowed on this server",
                ["NotAllowed"] = "You are not allowed to use the '{0}' command",
                ["WordAdded"] = "Word '{0}' was added to the profanity list",
                ["WordListed"] = "Word '{0}' is already in the profanity list",
                ["WordNotListed"] = "Word '{0}' is not in the profanity list",
                ["WordRemoved"] = "Word '{0}' was removed from the profanity list"
            }, this);
        }

        #endregion Localization

        #region Initialization

        [PluginReference]
        private Plugin BetterChat, Slap;

        private const string permAdmin = "ufilter.admin";
        private const string permBypass = "ufilter.bypass";

        private static readonly Regex ipRegex = new Regex(@"(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}(:\d{2,5})?)");
        private static readonly Regex domainRegex = new Regex(@"(\w{2,}\.\w{2,3}\.\w{2,3}|\w{2,}\.\w{2,3}(:\d{2,5})?)$");

        #region Blocked Words

        private StoredData storedData;

        private class StoredData
        {
            public readonly HashSet<string> Profanities = new HashSet<string>
            {
                "4r5e", "5h1t", "5hit", "a55", "a_s_s", "anal", "anus", "ar5e", "arrse", "arse", "ass", "ass-fucker", "asses", "assface", "assfaces", "assfucker",
                "assfukka", "asshole", "assholes", "asswhole", "b!tch", "b00bs", "b17ch", "b1tch", "ballbag", "balls", "ballsack", "bastard", "bastards", "beastial",
                "beastiality", "bellend", "bestial", "bestiality", "bi+ch", "biatch", "bitch", "bitcher", "bitchers", "bitches", "bitchin", "bitching", "bitchy",
                "bloody", "blow job", "blowjob", "blowjobs", "boiolas", "bollock", "bollok", "boner", "boob", "boobs", "booobs", "boooobs", "booooobs", "booooooobs",
                "breasts", "buceta", "bugger", "bullshit", "bum", "bunny fucker", "butt", "butthole", "buttmuch", "buttplug", "c0ck", "c0cksucker", "carpet muncher",
                "cawk", "chink", "cipa", "cl1t", "clit", "clitoris", "clits", "cnut", "cock", "cock-sucker", "cockface", "cockhead", "cockmunch", "cockmuncher", "cocks",
                "cocksuck", "cocksucked", "cocksucker", "cocksuckers", "cocksucking", "cocksucks", "cocksuka", "cocksukka", "cok", "cokmuncher", "coksucka", "coon",
                "cox", "crap", "cum", "cummer", "cumming", "cums", "cumshot", "cunilingus", "cunillingus", "cunnilingus", "cunt", "cuntlick", "cuntlicker", "cuntlicking",
                "cunts", "cyalis", "cyberfuc", "cyberfuck", "cyberfucked", "cyberfucker", "cyberfuckers", "cyberfucking", "d1ck", "damn", "dick", "dickhead", "dickheads",
                "dildo", "dildos", "dink", "dinks", "dirsa", "dlck", "dog-fucker", "doggin", "dogging", "donkeyribber", "doosh", "duche", "dyke", "ejaculate", "ejaculated",
                "ejaculates", "ejaculating", "ejaculatings", "ejaculation", "ejakulate", "f u c k", "f u c k e r", "f4nny", "f_u_c_k", "fag", "fagging", "faggitt", "faggot",
                "faggots", "faggs", "fagot", "fagots", "fags", "fanny", "fannyflaps", "fannyfucker", "fanyy", "fatass", "fcuk", "fcuker", "fcuking", "feck", "fecker",
                "felching", "fellate", "fellatio", "fingerfuck", "fingerfucked", "fingerfucker", "fingerfuckers", "fingerfucking", "fingerfucks", "fistfuck", "fistfucked",
                "fistfucker", "fistfuckers", "fistfucking", "fistfuckings", "fistfucks", "flange", "fook", "fooker", "fuc", "fuck", "fucka", "fucked", "fuckedup", "fucker",
                "fuckers", "fuckhead", "fuckheads", "fuckin", "fucking", "fuckings", "fuckingshitmotherfucker", "fuckme", "fuckoff", "fucks", "fuckup", "fuckwhit", "fuckwit",
                "fudge packer", "fudgepacker", "fuk", "fuker", "fukker", "fukkers", "fukkin", "fuks", "fukwhit", "fukwit", "fuq", "fux", "fux0r", "gangbang", "gangbanged",
                "gangbangs", "gaylord", "gaysex", "goatse", "god-dam", "god-damned", "goddamn", "goddamned", "goddamnit", "hardcoresex", "hell", "heshe", "hoar", "hoare",
                "hoer", "homo", "hore", "horniest", "horny", "hotsex", "jack-off", "jackass", "jackasses", "jackoff", "jap", "jerk", "jerk-off", "jism", "jiz", "jizm",
                "jizz", "kawk", "knob", "knob end", "knobead", "knobed", "knobend", "knobhead", "knobjocky", "knobjokey", "kock", "kondum", "kondums", "kum", "kummer",
                "kumming", "kums", "kunilingus", "l3i+ch", "l3itch", "labia", "lmao", "lmfao", "lust", "lusting", "m0f0", "m0fo", "m45terbate", "ma5terb8", "ma5terbate",
                "masochist", "master-bate", "masterb8", "masterbat*", "masterbat3", "masterbate", "masterbation", "masterbations", "masturbate", "mo-fo", "mof0", "mofo",
                "mothafuck", "mothafucka", "mothafuckas", "mothafuckaz", "mothafucked", "mothafucker", "mothafuckers", "mothafuckin", "mothafucking", "mothafuckings",
                "mothafucks", "mother fucker", "motherfuck", "motherfucked", "motherfucker", "motherfuckers", "motherfuckin", "motherfucking", "motherfuckings",
                "motherfuckka", "motherfucks", "muff", "mutha", "muthafecker", "muthafuckker", "muther", "mutherfucker", "n1gga", "n1gger", "nazi", "nigg3r", "nigg4h",
                "nigga", "niggah", "niggas", "niggaz", "nigger", "niggers", "nob", "nob jokey", "nobhead", "nobjocky", "nobjokey", "numbnuts", "nutsack", "omg", "orgasim",
                "orgasims", "orgasm", "orgasms", "p0rn", "pawn", "pecker", "penis", "penisfucker", "phonesex", "phuck", "phuk", "phuked", "phuking", "phukked", "phukking",
                "phuks", "phuq", "pigfucker", "pimpis", "piss", "pissed", "pisser", "pissers", "pisses", "pissflaps", "pissin", "pissing", "pissoff", "poop", "porn", "porno",
                "pornography", "pornos", "prick", "pricks", "pron", "pube", "pusse", "pussi", "pussies", "pussy", "pussys", "queer", "rectum", "retard", "rimjaw", "rimming",
                "s hit", "s.o.b.", "s_h_i_t", "sadist", "schlong", "screwing", "scroat", "scrote", "scrotum", "semen", "sex", "sh!+", "sh!t", "sh1t", "shag", "shagger",
                "shaggin", "shagging", "shemale", "shi+", "shit", "shitdick", "shite", "shited", "shitey", "shitfuck", "shitfull", "shithead", "shitheads", "shiting",
                "shitings", "shits", "shitted", "shitter", "shitters", "shittier", "shittiest", "shitting", "shittings", "shitty", "skank", "slut", "sluts", "smartass",
                "smartasses", "smegma", "smut", "snatch", "son-of-a-bitch", "spac", "spunk", "t1tt1e5", "t1tties", "teets", "teez", "testical", "testicle", "tit", "titfuck",
                "tities", "tits", "titt", "tittie5", "tittiefucker", "titties", "tittyfuck", "tittywank", "titwank", "tosser", "turd", "tw4t", "twat", "twathead", "twatty",
                "twunt", "twunter", "v14gra", "v1gra", "vagina", "viagra", "vulva", "w00se", "wang", "wank", "wanker", "wanky", "whoar", "whore", "willies", "willy",
                "wiseass", "wiseasses", "wtf", "xrated", "xxx"
            };
        }

        #endregion Blocked Words

        private void Init()
        {
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);

            permission.RegisterPermission(permAdmin, this);
            permission.RegisterPermission(permBypass, this);

            if (!config.CheckChat)
            {
                Unsubscribe(nameof(OnBetterChat));
                Unsubscribe(nameof(OnUserChat));
            }

            if (!config.CheckNames)
            {
                Unsubscribe(nameof(OnUserRespawned));
                Unsubscribe(nameof(OnUserSpawned));
            }
        }

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);

        #endregion Initialization

        #region Filter Matching

        private string[] Advertisements(string text)
        {
            string[] ips = ipRegex.Matches(text).OfType<Match>().Select(m => m.Value).ToArray();
            string[] domains = domainRegex.Matches(text.ToLower()).OfType<Match>().Select(m => m.Value.ToLower()).ToArray();
            return ips.Concat(domains).Where(a => !config.AllowedAds.Contains(a)).ToArray();
        }

        private string[] Profanities(string text)
        {
            return Regex.Split(text, @"\W").Where(w => storedData.Profanities.Contains(w.ToLower()) && !config.AllowedProfanity.Contains(w.ToLower())).ToArray();
        }

        #endregion Filter Matching

        #region Text Processing

        private string ProcessText(string text, IPlayer player)
        {
            if (player.HasPermission(permBypass))
            {
                return text;
            }

            string[] profanities = Profanities(text);
            string[] advertisements = Advertisements(text);

            if (config.CheckForProfanity && profanities.Length > 0)
            {
                if (config.WarnInChat)
                {
                    Message(player, "NoProfanity", player.Id);
                }

                if (config.LogProfanity)
                {
                    foreach (string profanity in profanities)
                    {
                        Log($"{player.Name} ({player.Id}) {DateTime.Now}: {profanity}", "profanity");
                    }
                }

                return TakeAction(player, text, profanities, config.ActionForProfanity, Lang("NoProfanity", player.Id));
            }

            if (config.CheckForAdvertising && advertisements.Length > 0)
            {
                if (config.WarnInChat)
                {
                    Message(player, "NoAdvertising", player.Id);
                }

                if (config.LogAdvertising)
                {
                    foreach (string advertisement in advertisements)
                    {
                        Log($"{player.Name} ({player.Id}) {DateTime.Now}: {advertisement}", "ads");
                    }
                }

                return TakeAction(player, text, advertisements, config.ActionForAdvertising, Lang("NoAdvertising", player.Id));
            }

            return text;
        }

        #endregion Text Processing

        #region Action Processing

        private string TakeAction(IPlayer player, string text, string[] list, string action, string reason)
        {
            if (string.IsNullOrEmpty(action))
            {
                return string.Empty;
            }

            switch (action.ToLower().Trim())
            {
                case "ban":
                    player.Ban(reason);
                    return string.Empty;

                case "censor":
                    foreach (string word in list)
                    {
                        text = text.Replace(word, config.CensorText.Length == 1 ? new string(config.CensorText[0], word.Length) : config.CensorText);
                    }
                    return text;

                case "kick":
                    player.Kick(reason);
                    return string.Empty;

                case "kill":
                    player.Kill();
                    return string.Empty;

                case "slap":
                    if (Slap)
                    {
                        Slap.Call("SlapPlayer", player);
                    }
                    else
                    {
                        LogWarning("Slap plugin is not installed; slap action will not work");
                    }
                    return string.Empty;

                default:
                    return string.Empty;
            }
        }

        #endregion Action Processing

        #region Chat Handling

        private string HandleChat(IPlayer player, string message)
        {
            if (!string.IsNullOrEmpty(message))
            {
                string processed = ProcessText(message, player);

                if (string.IsNullOrEmpty(processed))
                {
                    return string.Empty;
                }

                if (!string.Equals(message, processed))
                {
                    if (BetterChat != null)
                    {
                        return processed;
                    }

                    string prefixColor = player.IsAdmin ? "#aaff55" : "#55aaff";
#if RUST
                    prefixColor = (player.Object as BasePlayer).IsDeveloper ? "#ffaa55" : prefixColor;
#endif
                    processed = covalence.FormatText($"[{prefixColor}]{player.Name}[/#]: {processed}");
#if RUST
                    foreach (IPlayer target in players.Connected)
                    {
                        target.Command("chat.add", player.Id, processed);
                    }
#else
                    server.Broadcast(processed);
#endif
                    return string.Empty;
                }
            }

            return null;
        }

        private object OnBetterChat(Dictionary<string, object> data)
        {
            string processed = HandleChat(data["Player"] as IPlayer, data["Text"] as string);
            if (string.IsNullOrEmpty(processed))
            {
                return null;
            }

            data["Text"] = processed;
            return data;
        }

        private object OnUserChat(IPlayer player, string message)
        {
            return BetterChat == null ? HandleChat(player, message) : null;
        }

        #endregion Chat Handling

        #region Name Handling

        private void ProcessName(IPlayer player)
        {
            string processed = ProcessText(player.Name, player);

            if (player.Name != processed)
            {
                player.Rename(processed);
            }
            else if (string.IsNullOrEmpty(processed))
            {
                player.Rename("Unnamed" + new System.Random()); // TODO: Config option
            }
        }

        private void OnUserRespawned(IPlayer player) => ProcessName(player);

        private void OnUserSpawned(IPlayer player) => ProcessName(player);

        #endregion Name Handling

        #region Commands

        [Command("ufilter")] // TODO: Localization
        private void FilterCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permAdmin))
            {
                Message(player, "NotAllowed", command);
                return;
            }

            if (args.Length < 1 || args.Length < 2 && args[0].ToLower() != "list")
            {
                Message(player, "CommandUsage", command);
                return;
            }

            string argList = string.Join(" ", args.Skip(1).Select(v => v).ToArray());
            switch (args[0].ToLower())
            {
                case "+":
                case "add":
                    if (storedData.Profanities.Contains(argList))
                    {
                        Message(player, "WordListed", argList);
                        break;
                    }

                    storedData.Profanities.Add(argList);
                    SaveData();

                    Message(player, "WordAdded", argList);
                    break;

                case "-":
                case "del":
                case "delete":
                case "remove":
                    if (!storedData.Profanities.Contains(argList))
                    {
                        Message(player, "WordNotListed", argList);
                        break;
                    }

                    storedData.Profanities.Remove(argList);
                    SaveData();

                    Message(player, "WordRemoved", argList);
                    break;

                /*case "list":
                    Message(player, string.Join(", ", storedData.Profanities.Cast<string>().ToArray());
                    break;*/

                default:
                    Message(player, "CommandUsage", command);
                    break;
            }
        }

        #endregion Commands

        #region Helpers

        private string Lang(string key, string id = null, params object[] args)
        {
            return string.Format(lang.GetMessage(key, this, id), args);
        }

        private void AddLocalizedCommand(string key, string command)
        {
            foreach (string language in lang.GetLanguages(this))
            {
                Dictionary<string, string> messages = lang.GetMessages(language, this);
                foreach (KeyValuePair<string, string> message in messages.Where(m => m.Key.Equals(key)))
                {
                    if (!string.IsNullOrEmpty(message.Value))
                    {
                        AddCovalenceCommand(message.Value, command);
                    }
                }
            }
        }

        private void Log(string text, string filename)
        {
            if (config.LogToConsole)
            {
                Puts(text);
            }

            if (config.LogToFile)
            {
                LogToFile(filename, $"[{DateTime.Now}] {text}", this);
            }
        }

        private void Message(IPlayer player, string key, params object[] args)
        {
            player.Reply(Lang(key, player.Id, args));
        }

        #endregion Helpers
    }
}
