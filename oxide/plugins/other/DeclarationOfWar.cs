using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CodeHatch.Engine.Networking;
using CodeHatch.Common;
using Oxide.Core;
using CodeHatch.Blocks.Networking.Events;
using CodeHatch.Engine.Modules.SocialSystem;
using CodeHatch.Thrones.SocialSystem;
namespace Oxide.Plugins
{
    [Info("DeclarationOfWar", "juk3b0x & D-Kay", "2.0.0")]
    public class DeclarationOfWar : ReignOfKingsPlugin
    {
        #region Variables

        private TimeSpan WarTime { get; set; }
        private TimeSpan PrepTime { get; set; }

        private int WarReportInterval { get; set; }
        private const int WarTimerInterval = 1;

        private HashSet<War> WarList { get; set; } = new HashSet<War>();

        private class War
        {
            public Guild Declarer { get; set; }
            public Guild Defender { get; set; }
            public bool HasCommenced { get; set; }
            public TimeSpan RemainingTime { get; set; }
            public TimeSpan WarTime { get; set; }
            public TimeSpan PrepTime { get; set; }

            public War() { }

            public War(Guild declarer, Guild defender, double wartimeLength = 0, double preptimeLength = 0)
            {
                Declarer = declarer;
                Defender = defender;
                HasCommenced = false;
                WarTime = TimeSpan.FromSeconds(wartimeLength);
                PrepTime = TimeSpan.FromSeconds(preptimeLength);
                RemainingTime = WarTime + PrepTime;
            }

            public War(CodeHatch.Thrones.SocialSystem.Guild declarer, CodeHatch.Thrones.SocialSystem.Guild defender, double wartimeLength = 0, double preptimeLength = 0)
            {
                Declarer = new Guild(declarer);
                Defender = new Guild(defender);
                HasCommenced = false;
                WarTime = TimeSpan.FromSeconds(wartimeLength);
                PrepTime = TimeSpan.FromSeconds(preptimeLength);
                RemainingTime = WarTime + PrepTime;
            }

            public bool ContainsGuild(ulong id)
            {
                if (Declarer == null || Defender == null) return false;

                return Declarer.Id == id || Defender.Id == id;
            }

            public bool ContainsGuilds(ulong guild1, ulong guild2)
            {
                if (Declarer == null || Defender == null) return false;

                return Declarer.Id == guild1 && Defender.Id == guild2 ||
                       Declarer.Id == guild2 && Defender.Id == guild1;
            }

            public TimeSpan RemainingPrepTime()
            {
                return RemainingTime - WarTime;
            }

            public int GetState()
            {
                if (RemainingTime <= TimeSpan.Zero) return 0;
                if (RemainingTime <= WarTime)
                {
                    return HasCommenced ? 1 : 2;
                }
                return 3;
            }

            public void Commence()
            {
                HasCommenced = true;
            }

            public void Update(int time = 60)
            {
                RemainingTime = RemainingTime.Subtract(TimeSpan.FromSeconds(time));
            }
        }

        private class Guild
        {
            public ulong Id { get; set; }
            public string Name { get; set; }
            public string DisplayName { get; set; }

            public Guild() { }

            public Guild(ulong id, string name = null, string displayName = null)
            {
                Id = id;
                Name = name;
                DisplayName = displayName;
            }

            public Guild(CodeHatch.Thrones.SocialSystem.Guild guild)
            {
                Id = guild.BaseID;
                Name = guild.Name;
                DisplayName = guild.DisplayName;
            }

            public void UpdateName(string name)
            {
                Name = name;
            }

            public void UpdateDisplayName(string displayName)
            {
                DisplayName = displayName;
            }

            public override string ToString()
            {
                return this.DisplayName;
            }
        }

        #endregion

        #region Save and Load Data

        private void Loaded()
        {
            LoadConfigData();
            LoadWarData();
            LoadDefaultMessages();

            permission.RegisterPermission("DeclarationOfWar.Admin", this);

            timer.Repeat(WarReportInterval, 0, WarReport);
            timer.Repeat(WarTimerInterval, 0, WarUpdate);
        }

        private void LoadConfigData()
        {
            WarTime = TimeSpan.FromSeconds(GetConfig("WarTimeLength", 5400.0));
            PrepTime = TimeSpan.FromSeconds(GetConfig("WarPrepTime", 600.0));
            WarReportInterval = GetConfig("WarReportinterval", 300);
        }

        private void SaveConfigData()
        {
            Config["WarTimeLength"] = WarTime.TotalSeconds;
            Config["WarPrepTime"] = PrepTime.TotalSeconds;
            Config["WarReportInterval"] = WarReportInterval;

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            LoadConfigData();
            SaveConfigData();
        }

        private void SaveWarListData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("DeclarationOfWar", WarList);
        }

        private void LoadWarData()
        {
            WarList = Interface.Oxide.DataFileSystem.ReadObject<HashSet<War>>("DeclarationOfWar");
        }

        private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "NoActiveWars", "[FF0000] War Report [FFFFFF]  There are currently no active wars. The land is finally at peace once more.[-]" },
                { "NoAdmin", "Only a God(admin) can end wars.[-]" },
                { "EndAllWarsMessage", "[FF0000]Ended all Wars for : [00FF00]{0}[FFFFFF][-]" },
                { "WarReportPrefix", "[0000FF]WAR REPORT[FFFFFF][-]" },
                { "PlayerOffline", "The Player {0} seems to be offline.[-]" },
                { "IsOwnGuildPlayer", "[FF0000]War Squire[FFFFFF] : My Lord! {0} is your friend! A trusted Ally! You can't declare war on them! It would get... awkward...![-]" },
                { "AlreadyAtWar", "[FF0000]War Squire[FFFFFF] : We are already at war with {0}, my Lord.[-]" },
                { "DeclarationOfWar", " [FFFFFF] ([FF0000]Declaring War[FFFFFF]) : [00FF00]{0}[FFFFFF] has declared war on [00FF00]{1}[FFFFFF]! They have [00FF00]{2}[FFFFFF] to prepare for war![-]" },
                { "HelpText", "[FF0000]War Organiser[FFFFFF] : Use the following commands for Wars :[-]" },
                { "1HelpText", "[00FF00]/declarewar [FF00FF]<player_name> [FFFFFF] - Declare war on players guild[-]" },
                { "2HelpText", "[00FF00]/warreport [FFFFFF] - View all active wars[-]" },
                { "HTA", "[00FF00]/endwar [FF00FF]<player_name> [FFFFFF] - End current war on players guild[-]" },
                { "AHT1", "[00FF00]/endallwars [FFFFFF] - End current war on players guild[-]" },
                { "PreparingForWar", "[FF0000]War Report : [00FF00]{0}[FFFFFF] is preparing for war with [00FF00] {1} [FFFFFF].[-]" },
                { "IsAtWar", "[FF0000]War Report : [00FF00]{0}[FFFFFF] is at war with [00FF00]{1}.[-]" },
                { "TimePrepare", "[FFFFFF]There are [00FF00]{0}[FFFFFF] until this war begins![-]" },
                { "AtWarTime", "[00FF00]{0}[FFFFFF] remaining.[-]" },
                { "CommencedWar", "[FF0000]WAR BETWEEN [00FF00]{0}[FF0000] AND [00FF00]{1}[FF0000] HAS BEGUN![-]" },
                { "WarIsOver", "[FF0000] War Report [FFFFFF]([00FF00]WAR OVER![FFFFFF]) : The war between [00FF00]{0}[FFFFFF] and [00FF00]{1}[FFFFFF] has now ended![-]" },
                { "NotAtWar", "[FF0000]War General : [00FF00]{0}[FFFFFF]! You cannot attack this base when you are not at war with [00FF00]{1}[FFFFFF]!![-]" },
                { "SelfDeclare", "[FF0000]War Squire[FFFFFF] : You can't declare war upon thyself, my Lord! This is crazy talk![-]" },
                { "Instructions", "[FF0000]Declare War Instructions[FFFFFF] : Type /declarewar followed by the Player's name to declare war on that player's guild. THIS CANNOT BE UNDONE![-]" },
                { "GuildLess", "[FF0000]War Squire[FFFFFF] : My Lord, you have not yet formed a guild. You must do so before you can declare a war![-]"},
                { "EndAllWars", "Ending all guild wars...[-]"},
                { "SpecificEndWar", "Ending all wars for guild : [00FF00]{0}[-]"}
            }, this);
        }

        #endregion

        #region Commands

        [ChatCommand("declarewar")]
        private void CmdDeclareWar(Player player, string cmd, string[] args)
        {
            DeclareWar(player, args);
        }

        [ChatCommand("endwar")]
        private void CmdEndWar(Player player, string cmd, string[] args)
        {
            EndWar(player, args);
        }

        [ChatCommand("endallwars")]
        private void CmdEndAllWars(Player player, string cmd)
        {
            EndAllWars(player);
        }

        [ChatCommand("warreport")]
        private void CmdPlayerWarReport(Player player, string cmd)
        {
            PlayerWarReport(player);
        }

        [ChatCommand("warcommands")]
        private void ShowHelpPopup(Player player, string cmd)
        {
            SendHelpText(player);
        }

        #endregion

        #region Command Functions

        private void DeclareWar(Player player, string[] input)
        {
            if (!input.Any())
            {
                player.SendMessage(GetMessage("Instructions", player));
                return;
            }

            var targetName = input.JoinToString(" ");
            var target = Server.GetPlayerByName(targetName);

            if (target == null)
            {
                player.SendError(GetMessage("PlayerOffline", player), targetName);
                return;
            }

            if (Equals(player, target))
            {
                player.SendError(GetMessage("SelfDeclare", player));
                return;
            }

            var playerGuild = player.GetGuild();
            var targetGuild = target.GetGuild();

            if (targetGuild.DisplayName.IsNullOrEmpty())
            {
                player.SendError(GetMessage("GuildLess", player));
                return;
            }

            if (Equals(playerGuild, targetGuild))
            {
                player.SendError(GetMessage("IsOwnGuildPlayer", player), target.Name);
                return;
            }

            if (IsAtWar(playerGuild.BaseID, targetGuild.BaseID))
            {
                player.SendError(GetMessage("AlreadyAtWar", player), targetGuild.DisplayName);
                return;
            }

            PrintToChat(GetMessage("DeclarationOfWar", player), playerGuild.DisplayName, targetGuild.DisplayName, PrepTime);

            CommenceWar(new Guild(playerGuild), new Guild(targetGuild));

            SaveWarListData();
        }

        private void EndWar(Player player, string[] input)
        {
            if (!player.HasPermission("DeclarationOfWar.Admin"))
            {
                player.SendError(GetMessage("NoAdmin", player));
                return;
            }

            if (!input.Any())
            {
                return;
            }

            var targetName = input.JoinToString(" ");
            if (targetName == "")
            {
                player.SendError(GetMessage("EnterAName", player));
                return;
            }

            var target = Server.GetPlayerByName(targetName);
            if (target == null)
            {
                player.SendError(GetMessage("PlayerOffline", player), targetName);
                return;
            }

            var guild = target.GetGuild();
            player.SendMessage(GetMessage("SpecificEndWar", player), guild.DisplayName);

            foreach (var war in WarList.ToList())
            {
                if (war.ContainsGuild(guild.BaseID)) WarList.Remove(war);
            }
            PrintToChat(GetMessage("EndAllWarsMessage"), guild.DisplayName);

            SaveWarListData();
        }

        private void EndAllWars(Player player)
        {
            if (!player.HasPermission("DeclarationOfWar.Admin"))
            {
                player.SendError(GetMessage("NoAdmin", player));
                return;
            }

            PrintToChat(GetMessage("EndAllWars"));

            WarList.Clear();

            SaveWarListData();
        }

        private void PlayerWarReport(Player player)
        {
            if (WarList.Count <= 0) player.SendError(GetMessage("NoActiveWars", player));
            WarReport(player);
        }

        #endregion

        #region System Functions

        private void CommenceWar(Guild declarer, Guild defender)
        {
            WarList.Add(new War(declarer, defender, WarTime.TotalSeconds, PrepTime.TotalSeconds));
        }

        private void WarReport()
        {
            if (WarList.Count < 1) return;

            PrintToChat(GetMessage("WarReportPrefix"));

            foreach (var war in WarList)
            {
                switch (war.GetState())
                {
                    case 3:
                        PrintToChat(GetMessage("PreparingForWar"),
                            war.Declarer,
                            war.Defender);
                        
                        PrintToChat(GetMessage("TimePrepare"),
                            war.RemainingPrepTime());
                        break;
                    case 1:
                        PrintToChat(GetMessage("IsAtWar"),
                            war.Declarer,
                            war.Defender);

                        PrintToChat(GetMessage("AtWarTime"),
                            war.RemainingTime);
                        break;
                }
            }
        }

        private void WarReport(Player player)
        {
            if (WarList.Count < 1) return;

            player.SendMessage(GetMessage("WarReportPrefix", player));
            
            foreach (var war in WarList)
            {
                switch (war.GetState())
                {
                    case 3:
                        player.SendMessage(GetMessage("PreparingForWar", player),
                            war.Declarer,
                            war.Defender);
                        
                        player.SendMessage(GetMessage("TimePrepare", player),
                            war.RemainingPrepTime());
                        break;
                    case 1:
                        player.SendMessage(GetMessage("IsAtWar", player),
                            war.Declarer,
                            war.Defender);

                        player.SendMessage(GetMessage("AtWarTime", player),
                            war.RemainingTime);
                        break;
                }
            }
        }

        private void WarUpdate()
        {
            foreach (var war in WarList.ToList())
            {
                war.Update(WarTimerInterval);

                switch (war.GetState())
                {
                    case 3:
                        break;
                    case 2:
                        PrintToChat(GetMessage("CommencedWar"), war.Declarer, war.Defender);
                        war.Commence();
                        break;
                    case 1:
                        break;
                    case 0:
                        PrintToChat(GetMessage("WarIsOver"), war.Declarer, war.Defender);
                        WarList.Remove(war);
                        break;
                }
            }

            SaveWarListData();
        }

        private bool IsAtWar(ulong guildId)
        {
            return WarList.Any(war => war.ContainsGuild(guildId));
        }

        private bool IsAtWar(ulong guildId1, ulong guildId2)
        {
            return WarList.Any(war => war.ContainsGuilds(guildId1, guildId2));
        }

        #endregion

        #region Hooks

        private void OnCubeTakeDamage(CubeDamageEvent e)
        {
            #region Checks
            if (e == null) return;
            if (e.Cancelled) return;
            if (e.Damage == null) return;
            if (e.Damage.Amount <= 0f) return;
            if (e.Damage.Damager == null) return;
            if (e.Damage.DamageSource == null) return;
            if (!e.Damage.DamageSource.IsPlayer) return;
            #endregion

            bool trebuchet = e.Damage.Damager.name.Contains("Trebuchet");
            bool ballista = e.Damage.Damager.name.Contains("Ballista");
            if (!trebuchet && !ballista) return;

            var player = e.Damage.DamageSource.Owner;
            if (player == null) return;
            
            var worldCoordinate = e.Grid.LocalToWorldCoordinate(e.Position);
            var crestScheme = SocialAPI.Get<CrestScheme>();

            var crest = crestScheme.GetCrestAt(worldCoordinate);
            if (crest == null) return;

            var playerGuild = player.GetGuild().BaseID;
            var targetGuild = crest.SocialId;

            if (Equals(playerGuild, targetGuild)) return;

            if (IsAtWar(playerGuild, targetGuild)) return;

            e.Cancel();
            e.Damage.Amount = 0f;
            player.SendError(GetMessage("NotAtWar", player), player.Name, crest.GuildName);
        }

        private void SendHelpText(Player player)
        {
            player.SendMessage(GetMessage("HelpText", player));
            player.SendMessage(GetMessage("1HelpText", player));
            player.SendMessage(GetMessage("2HelpText", player));
            if (player.HasPermission("DeclarationOfWar.Admin"))
            {
                player.SendMessage(GetMessage("HTA", player));
                player.SendMessage(GetMessage("AHT1", player));
            }
        }

        #endregion

        #region Utility

        private string GetMessage(string key, Player player = null) => lang.GetMessage(key, this, player?.Id.ToString());

        T GetConfig<T>(string name, T defaultValue) => Config[name] == null ? defaultValue : (T)System.Convert.ChangeType(Config[name], typeof(T));

        #endregion

    }
}