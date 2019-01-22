// Requires: Discord

using System;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("DiscordReport", "Bobakanoosh", "0.8.0", ResourceId = 2323)]
    [Description("Send reports from players ingame to a Discord channel")]

    public class DiscordReport : CovalencePlugin
    {
        [PluginReference]
        Plugin Discord;

        #region Initialization

        // Defining permission variables
        private const string permUse = "discordreport.use";
        private const string permAdmin = "discordreport.admin";
        private const string permIsBlocked = "discordreport.isblocked";
        private const string permIsImmune = "discordreport.isimmune";

        // Defining config variables
        bool logReports;
        bool sendLink;

        int commandCooldown;

        string prefix;

        // Used for linking steam profiles in the report message
        string steam_URL = "http://steamcommunity.com/profiles/";

        // Defining lists for command cooldowns.
        List<String> recentUserReportList = new List<string>();
        List<long> recentTimeReportList = new List<long>();

        // Used for storing data.
        class StoredData
        {
            public HashSet<PlayerInfo> Players = new HashSet<PlayerInfo>();

            public StoredData()
            {
            }
        }

        // Used for formatting
        class PlayerInfo
        {
            public string message;

            public PlayerInfo(String message2)
            {
                message = message2;
            }

        }

        // Creating data variables
        StoredData storedData;

        // On initiliaize
        void Init()
        {
            // Load configs
            LoadDefaultConfig();
            LoadDefaultMessages();

            // Registering permissions
            permission.RegisterPermission(permUse, this);
            permission.RegisterPermission(permAdmin, this);
            permission.RegisterPermission(permIsBlocked, this);
            permission.RegisterPermission(permIsImmune, this);
        }

        // Setting up configs
        protected override void LoadDefaultConfig()
        {
            Config["Log reports? (true / false)"] = logReports = GetConfig("Log reports? (true / false)", true);
            Config["Send steam profile link with reports? (true / false)"] = sendLink = GetConfig("Send steam profile link with reports? (true / false)", true);
            Config["Cooldown between each report (seconds)"] = commandCooldown = GetConfig("Cooldown between each report (seconds)", 60);
            Config["Custom chat Prefix: "] = prefix = GetConfig("Custom chat Prefix: ", "<color=silver>[</color><color=orange>DiscordReport</color><color=silver>]</color> ");
            SaveConfig();
        }

        #endregion

        #region Localization
        void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                // Message used in           Message sent
                // the Lang() function
                ["NoPermission"]           = "{0}You don't have permission to use this command.",
                ["PlayerNotFound"]         = "{0}Player not found.",
                ["ReportConfirmation"]     = "{0}Your report has been sent!",
                ["ReportMessage"]          = "**Reporter:** {0} ( <{5}{1}> )\n**Reported:** {2} ( <{5}{3}> )\n**Reason:** {4}",
                ["ReportMessageConsole"]   = "\nReporter: {0} ( <{5}{1}> )\nReported: {2} ( <{5}{3}> )\nReason: {4}\n-------------------------------------------------------",
                ["DrSyntax"]               = "{0}Incorrect syntax! Use /dr block <user>, /dr unblock <user>, or /dr isblocked <user>",
                ["ReportSyntax"]           = "{0}Incorrect syntax! Use /report <user> <reason> or /dreport <user> <reason>!",
                ["UnblockSyntax"]          = "{0}Incorrect syntax! Use /dr unblock <user>",
                ["BlockSyntax"]            = "{0}Incorrect syntax! Use /dr block <user>",
                ["PlayerBlocked"]          = "{0}{1} ({2}) has been blocked from using the /report <user> command.",
                ["PlayerUnblocked"]        = "{0}{1} ({2}) has been unblocked from using the /report <user> command.",
                ["Blocked"]                = "{0}You were blocked by an admin from using that command!",
                ["Cooldown"]               = "{0}That command is on cooldown!"
            }, this);

            // French
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPermission"]           = "{0}Vous n'êtes pas autorisé à utiliser cette commande.",
                ["PlayerNotFound"]         = "{0}Joueur non trouvé.",
                ["ReportConfirmation"]     = "{0}Votre rapport a été envoyé!",
                ["ReportMessage"]          = "**Le Reporter:** {0} ( <{5}{1}> )\n**Signalé:** {2} ( <{5}{3}> )\n**Raison:** {4}",
                ["ReportMessageConsole"]   = "\nLe Reporter: {0} ( <{5}{1}> )\nSignalé: {2} ( <{5}{3}> )\nRaison: {4}\n-------------------------------------------------------",
                ["DrSyntax"]               = "{0}Syntaxe incorrecte! Utilisation /dr block <joueur>, /dr unblock <joueur>, ou /dr isblocked <joueur>",
                ["ReportSyntax"]           = "{0}Syntaxe incorrecte! Utilisation /report <joueur> <raison> ou /dreport <joueur> <raison>!",
                ["UnblockSyntax"]          = "{0}Syntaxe incorrecte! Utilisation /dr unblock <joueur>",
                ["BlockSyntax"]            = "{0}Syntaxe incorrecte! Utilisation /dr block <joueur>",
                ["PlayerBlocked"]          = "{0}{1} ({2}) a été empêché d'utiliser le /report <joueur> commander.",
                ["PlayerUnblocked"]        = "{0}{1} ({2}) A été débloqué de l'utilisation de /report <joueur> commander.",
                ["Blocked"]                = "{0}Vous avez été bloqué par un administrateur d'utiliser cette commande!",
                ["Cooldown"]               = "{0}Cette commande est en rétablissement!"

            }, this, "fr");

            // German
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPermission"]           = "{0}Sie haben keine Berechtigung, diesen Befehl zu verwenden.",
                ["PlayerNotFound"]         = "{0}Spieler nicht gefunden.",
                ["ReportConfirmation"]     = "{0}Ihr Bericht wurde gesendet!",
                ["ReportMessage"]          = "**Der Reporter:** {0} ( <{5}{1}> )\n**Berichtet:** {2} ( <{5}{3}> )\n**Grund:** {4}",
                ["ReportMessageConsole"]   = "\nDer Reporter: {0} ( <{5}{1}> )\nBerichtet: {2} ( <{5}{3}> )\nGrund: {4}\n-------------------------------------------------------",
                ["DrSyntax"]               = "{0}Falsche Syntax! Benutzen /dr block <benutzer>, /dr unblock <benutzer>, oder /dr isblocked <benutzer>",
                ["ReportSyntax"]           = "{0}Falsche Syntax! Benutzen /report <benutzer> <grund> oder /dreport <benutzer> <grund>!",
                ["UnblockSyntax"]          = "{0}Falsche syntax! Benutzen /dr unblock <benutzer>",
                ["BlockSyntax"]            = "{0}Falsche syntax! Benutzen /dr block <benutzer>",
                ["CommandExample"]         = "{0}Beispielsweise /dr user Bobakanoosh Er betrügt",
                ["PlayerBlocked"]          = "{0}{1} ({2}) Wurde mit dem Befehl /dr report <benutzer> blockiert.",
                ["PlayerUnblocked"]        = "{0}{1} ({2}) wurde von der Verwendung der /dr report <benutzer> blockiert.",
                ["Blocked"]                = "{0}Sie wurden von einem Admin von diesem Befehl blockiert!",
                ["Cooldown"]               = "{0}Dieser Befehl ist auf Abklingzeit!"

            }, this, "de");

            // Russian
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPermission"]           = "{0}У вас нет разрешения на использование этой команды.",
                ["PlayerNotFound"]         = "{0}Игрок не найден.",
                ["ReportConfirmation"]     = "{0}Ваше сообщение было отправлено!",
                ["ReportMessage"]          = "**репортер:** {0} ( <{5}{1}> )\n**сообщается:** {2} ( <{5}{3}> )\n**причина:** {4}",
                ["ReportMessageConsole"]   = "\nрепортер: {0} ( <{5}{1}> )\nсообщается: {2} ( <{5}{3}> )\nпричина: {4}\n-------------------------------------------------------",
                ["DrSyntax"]               = "{0}Неправильный синтаксис! использование /dr block <пользователь>, /dr unblock <пользователь>, or /dr isblocked <пользователь>",
                ["ReportSyntax"]           = "{0}Неправильный синтаксис! использование /report <пользователь> <причина> or /dreport <пользователь> <причина>!",
                ["UnblockSyntax"]          = "{0}Неправильный синтаксис! использование /dr unblock <пользователь>",
                ["BlockSyntax"]            = "{0}Неправильный синтаксис! использование /dr block <пользователь>",
                ["PlayerBlocked"]          = "{0}{1} ({3}) был заблокирован с помощью /dr report <пользователь> команда.",
                ["PlayerUnblocked"]        = "{0}{2} ({3}) разблокирован с помощью /dr report <пользователь> команда.",
                ["Blocked"]                = "{0}Вы были заблокированы администратором с помощью этой команды!",
                ["Cooldown"]               = "{0}Эта команда на кулдауне!"

            }, this, "ru");

            // Spanish
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPermission"]           = "{0}No tiene permiso para utilizar este comando.",
                ["PlayerNotFound"]         = "{0}Jugador no encontrado.",
                ["ReportConfirmation"]     = "{0}¡Tu reporte ha sido enviado!",
                ["ReportMessage"]          = "**Reportero:** {0} ( <{5}{1}> )\n**Informó:** {2} ( <{5}{3}> )\n**Razón:** {4}",
                ["ReportMessageConsole"]   = "\nReportero: {0} ( <{5}{1}> )\nInformó: {2} ( <{5}{3}> )\nRazón: {4}\n-------------------------------------------------------",
                ["DrSyntax"]               = "{0}¡Sintaxis incorrecta! Utilizar /dr block <usuario>, /dr unblock <usuario>, or /dr isblocked <usuario>",
                ["ReportSyntax"]           = "{0}¡Sintaxis incorrecta! Utilizar /report <usuario> <razón> or /dreport <usuario> <razón>!",
                ["UnblockSyntax"]          = "{0}¡Sintaxis incorrecta! Utilizar /dr unblock <usuario>",
                ["BlockSyntax"]            = "{0}¡Sintaxis incorrecta! Utilizar /dr block <usuario>",
                ["PlayerBlocked"]          = "{0}{1} ({2})ha sido bloqueado de usar el /report <usuario> mando.",
                ["PlayerUnblocked"]        = "{0}{1} ({2}) se ha desbloqueado el uso del /report <usuario> mando.",
                ["Blocked"]                = "{0}¡El administrador te ha bloqueado el uso de ese comando!",
                ["Cooldown"]               = "{0}¡Ese comando está en tiempo de reutilización!"

            }, this, "es");
        }
        #endregion
        
        #region Functions
        
        // Used for checking if a player is currently on cooldown.
        public bool onCooldown(IPlayer player)
        {
            // Gets the current time
            var currentTime = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;

            for (int i = 0; i < recentUserReportList.Count; i++)
            {
                // If the given player is in the current iteration of the user list
                if (player.Id == recentUserReportList[i])
                {
                    // If the current time is less than the expire time
                    if (currentTime < recentTimeReportList[i])
                    {
                        // Return true, they are on cooldown.
                        return true;
                    }
                    // Otherwise,
                    else
                    {
                        // If the user is not on cooldown, remove the user and time from the lists.
                        recentUserReportList.Remove(recentUserReportList[i]);
                        recentTimeReportList.Remove(recentTimeReportList[i]);
                    }

                    // Then, break out of the loop so that we don't end up going through dozens of players.
                    break;
                }
            }

            // Then, return false, the player is not on cooldown.
            return false;
        }
        
        public bool permissionCheck(IPlayer player, string permission)
        {
            // Checking permissions
            if (!player.IsAdmin)
            {
                if (!player.HasPermission(permission))
                {
                    player.Message(Lang("NoPermission", null, prefix), player.Id);

                    return false;
                }
                else
                    return true;

            }
            else
                return true;
        }

        #endregion

        #region CMD_report
        [Command("report", "dreport")]
        void reportCommand(IPlayer player, string command, string[] args)
        {
            // Checking permissions
            if (!permissionCheck(player, "discordreport.use"))
                return;

            // If the player is on cooldown
            
            if (onCooldown(player))
            {
                player.Message(Lang("Cooldown", null, prefix), player.Id);

                return;
            }
            

            // If they didn't give enough arguments
            if (args.Length < 2)
            {
                player.Message(Lang("ReportSyntax", null, prefix), player.Id);

                return;
            }

            // If the user isn't blocked from using the report command
            if (!player.HasPermission(permIsBlocked))
            {
                // Finding the user they gave
                var targetUser = players.FindPlayer(args[0]);

                // Finds the player given in arg[0] (first thing after /report), assigns it to targetUser. If that targetUser is null (doesn't exist), it will return an error
                if (targetUser == null)
                {
                    player.Message(Lang("PlayerNotFound", null, prefix), player.Id);

                    return;
                }
                // If the player exists
                else
                {
                    string message = null;

                    // Adding each arg to one string to make format look better.
                    for (int i = 1; i < args.Length; i++)
                    {
                        message += args[i];
                        message += " ";
                    }

                    // If the user doesn't want to send steam links with their reports, then set the link to nothing.
                    if (!sendLink)
                        steam_URL = "";

                    // Calling Discord's call function. This sends a message with steam id's of both users and their message, which are then sent using a bot script to
                    // the discord server that your bot is connected to. It also sends it to console.

                    Discord.Call("SendMessage", Lang("ReportMessage", null, player.Name, player.Id, targetUser.Name, targetUser.Id, message, steam_URL));
                    player.Message(Lang("ReportConfirmation", null, prefix));

                    // If the config wants logging
                    if (logReports)
                    {
                        // Logging the info given to discord to a file. 
                        Log(Lang("ReportMessageConsole", null, player.Name, player.Id, targetUser.Name, targetUser.Id, message, steam_URL));
                    }

                    // Setting expireTime to the current time
                    var expireTime = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;

                    // Adding the confiugured cooldown to the current time, which makes it the expired time.
                    expireTime += commandCooldown;

                    // If they don't have the discordreport.isimmune permission
                    if (!player.HasPermission(permIsImmune))
                    {
                        // Add the users id and the expireTime to a new dynamic list.
                        recentUserReportList.Add(targetUser.Id);
                        recentTimeReportList.Add(expireTime);

                     }

                    return;
                    }
                }

                else
                {
                    player.Message(Lang("Blocked", null, prefix), player.Id);

                    return;
                }
            
        }
        #endregion

        #region CMD_dr   
        [Command("dr")]
        void drCommand(IPlayer player, string command, string[] args)
        {
            #region Checks
            // If they just typed /dr
            if (args.Length == 0)
            {
                player.Message(Lang("DrSyntax", null, prefix), player.Id);
                
                return;
            }

            #endregion

            #region CMD_ti
            // Creating the toggleimmunity command
            if (args[0] == "ti")
            {
                if (!permissionCheck(player, "discordreport.admin"))
                {
                    player.Message(Lang("NoPermission", null, prefix), player.Id);

                    return;
                }
                   
                // If they didn't user proper syntax
                if (args.Length != 2)
                {
                    player.Message(prefix + " User /dr ti <user>");

                    return;
                }

                // Set targetUser variable to a matched player that they entered in the 2nd arguement (user).
                var targetUser = players.FindPlayer(args[1]);

                // If they have the immune permission, remove it
                if (targetUser.HasPermission(permIsImmune))
                {
                    targetUser.RevokePermission(permIsImmune);
                    player.Message($"{prefix}{targetUser.Name}'s immunity has been disabled.");
                }


                // If they don't, give it.
                else
                {
                    targetUser.GrantPermission(permIsImmune);
                    player.Message($"{prefix}{targetUser.Name}'s immunity has been enabled.");
                }

                return;
            }
            #endregion

            #region CMD_isblocked
            // Creating the /dr isblocked <user> com mand
            if (args[0] == "isblocked")
            {

                // If the permission check returns false, don't continue.
                if (!permissionCheck(player, "discordreport.isblocked"))
                    return;

                // If they didn't user proper syntax
                if (args.Length != 2)
                {
                    player.Message(prefix + " Use /dr isblocked <user>");       

                    return;
                }

                // Set targetUser variable to a matched player that they entered in the 2nd arguement (user).
                var targetUser = players.FindPlayer(args[1]);

                // If the targetUser has the permission discordreport.isblocked, then say that they're blocked
                if (targetUser.HasPermission(permIsBlocked))
                    player.Message(prefix + targetUser.Name + " is blocked!");

                // Otherwise, say that they're not blocked.
                else
                    player.Message(prefix + targetUser.Name + " is not blocked!");

                // Return to exit the rest of the code.
                return;

            }
            #endregion

            #region CMD_isimmune
            // Creating the /dr isimmune <user> command
            if (args[0] == "isimmune")
            {
                // If the permission check returns false, don't continue.
                if (!permissionCheck(player, "discordreport.isimmune"))
                    return;

                // If they didn't user proper syntax
                if (args.Length != 2)
                {
                    player.Message(prefix + " Use /dr isimmune <user>");

                    // Return to exit the rest of the code.
                    return;
                }

                // Set targetUser variable to a matched player that they entered in the 2nd arguement (user).
                var targetUser = players.FindPlayer(args[1]);

                // If the targetUser has the permission discordreport.isblocked, then say that they're blocked
                if (targetUser.HasPermission(permIsImmune))
                    player.Message(prefix + targetUser.Name + " is immune!");

                // Otherwise, say that they're not blocked.
                else
                    player.Message(prefix + targetUser.Name + " is not immune!");

                // Return to exit the rest of the code.
                return;

            }
            #endregion

            #region CMD_block
            // Creating command for /dr block
            if (args[0] == "block")
            {
                // If the permission check returns false, don't continue.
                if (!permissionCheck(player, "discordreport.admin"))
                {
                    player.Message(Lang("NoPermission", null, prefix), player.Id);

                    return;
                }

                // If they didn't give two arguements (block and the user)
                if (args.Length != 2)
                {
                    player.Message(Lang("BlockSyntax", null, prefix), player.Id);

                    return;
                }

                // Set targetUser variable to a matched player that they entered in the 2nd arguement (user).
                var targetUser = players.FindPlayer(args[1]);

                // Give the user they targeted the discordreport.isblocked permission
                targetUser.GrantPermission(permIsBlocked);
                player.Message(Lang("PlayerBlocked", null, prefix, targetUser.Name, targetUser.Id));
                    
                return;
            }
            #endregion

            #region CMD_unblock
            // Creating command for /dr unblock
            if (args[0] == "unblock")
            {
                // If the permission check returns false, don't continue.
                if (!permissionCheck(player, "discordreport.admin"))
                {
                    player.Message(Lang("NoPermission", null, prefix), player.Id);

                    return;
                }

                if (args.Length != 2)
                {
                    player.Message(Lang("UnblockSyntax", null, prefix), player.Id);
                    return;
                }

                // If the player has permission
                var targetUser = players.FindPlayer(args[1]);

                // Revoke the permission from targeted user
                targetUser.RevokePermission(permIsBlocked);
                player.Message(Lang("PlayerUnblocked", null, prefix, targetUser.Name, targetUser.Id));

                return;
            }

            #endregion
        }
        #endregion

        #region Helpers

        // Getting configs
        private T GetConfig<T>(string name, T defaultValue)
        {
            if (Config[name] == null) Config[name] = defaultValue;
            return (T)Convert.ChangeType(Config[name], typeof(T));
        }

        // Used for localization messages
        string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        // Used for logging files
        void Log(string filename, string text)
        {
            LogToFile(filename, $"[{DateTime.Now}] {text}", this);
        }
        #endregion
    }
}