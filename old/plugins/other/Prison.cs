using CodeHatch.Blocks.Networking.Events;
using CodeHatch.Common;
using CodeHatch.Engine.Networking;
using CodeHatch.Networking.Events;
using CodeHatch.Networking.Events.Entities;
using CodeHatch.Networking.Events.Players;
using CodeHatch.Thrones.AncientThrone;
using Oxide.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using CodeHatch.Common.Collections;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Prison", "D-Kay", "1.0.0")]
    public class Prison : ReignOfKingsPlugin
    {
        #region Variables

        private bool UsePublicNotifications { get; set; } = true;
        private static int RollCall { get; set; } = 15;
        private static int Interval { get; set; } = 1;

        private Jail _Jail { get; set; } = new Jail();

        private enum Metric
        {
            Seconds,
            Minutes,
            Hours,
            Days
        }

        private class Jail
        {
            public SortedDictionary<int, Cell> Cells { get; set; } = new SortedDictionary<int, Cell>();
            public Position Release { get; set; }

            public Jail()
            {

            }

            public void Reset()
            {
                Cells.Clear();
            }

            public int[] AddCell(Vector3 position, int cellNumber = 0)
            {
                if (cellNumber == 0)
                {
                    do cellNumber++;
                    while (Cells.ContainsKey(cellNumber) && Cells[cellNumber].Area.GetPositions() == 2);
                }

                if (Cells.ContainsKey(cellNumber))
                {
                    switch (Cells[cellNumber].Area.GetPositions())
                    {
                        case 0:
                            Cells[cellNumber].Area.SetPosition(1, position);
                            return new[] {1, cellNumber};
                        case 1:
                            Cells[cellNumber].Area.SetPosition(2, position);
                            return new[] {2, cellNumber};
                        case 2:
                            return new[] {0, cellNumber};
                    }
                }

                Cells.Add(cellNumber, new Cell(position));
                return new[] {1, cellNumber};
            }

            public int AddRelease(Vector3 position)
            {
                var overwritten = Release != null;
                Release = new Position(position);
                return overwritten ? 2 : 1;
            }

            public void RemoveRelease()
            {
                Release = null;
            }

            public int RemoveCell(int cellNumber)
            {
                if (!Cells.ContainsKey(cellNumber)) return -1;
                Cells.Remove(cellNumber);
                return 1;
            }

            public int[] Imprison(Player player, int time, Metric type, int cellNumber)
            {
                if (cellNumber == 0)
                {
                    cellNumber = Cells.Keys.First();
                    foreach (var cell in Cells) if (cell.Value.PrisonerCount < Cells[cellNumber].PrisonerCount) cellNumber = cell.Key;
                }

                if (!Cells.ContainsKey(cellNumber)) return new[] {-2, cellNumber};
                if (Cells.Values.Any(cell => cell.HasPrisoner(player))) return new[] {-1, cellNumber};

                return new[] {Cells[cellNumber].AddPrisoner(player, time, type, player.Entity.Position) == 1 ? 1 : 0, cellNumber};
            }

            public int Free(string player)
            {
                foreach (var cell in Cells.Values)
                {
                    if (!cell.HasPrisoner(player)) continue;
                    return cell.RemovePrisoner(player) == 1 ? 1 : 0;
                }
                return -1;
            }

            public int Free(Player player)
            {
                foreach (var cell in Cells.Values)
                {
                    if (!cell.HasPrisoner(player)) continue;
                    if (cell.RemovePrisoner(player, Release) != 1) return 0;
                    return 1;
                }
                return -1;
            }

            public int MovePrisoner(Player player, int cellNumber)
            {
                if (!Cells.ContainsKey(cellNumber) || Cells[cellNumber].Area.GetPositions() != 2) return -1;
                Prisoner prisoner = null;
                foreach (var cell in Cells)
                {
                    if (!cell.Value.HasPrisoner(player)) continue;
                    prisoner = cell.Value.Prisoners[player.Id];
                    cell.Value.RemovePrisoner(player, null, false);
                    break;
                }
                if (prisoner == null) return -2;
                return Cells[cellNumber].AddPrisoner(player, prisoner) == 1 ? 1 : 0;
            }

            public string GetSentenceTime(Player player)
            {
                var time = "0";
                foreach (var cell in Cells)
                {
                    if (cell.Value.HasPrisoner(player)) time = cell.Value.Prisoners[player.Id].Time.ToString();
                }
                return time;
            }

            public HashSet<string> GetPrisoners(int cellNumber)
            {
                if (!Cells.ContainsKey(cellNumber)) return null;

                var prisoners = Cells[cellNumber].GetPrisoners();

                return prisoners;
            }

            public bool CheckPrisoner(Player player)
            {
                foreach (var cell in Cells.Values)
                {
                    if (cell.CheckPrisoner(player) != 0) continue;
                    cell.SendToCell(player);
                    return false;
                }
                return true;
            }

            public bool ExpendTime(Player player)
            {
                foreach (var cell in Cells.Values)
                {
                    if (!cell.HasPrisoner(player)) continue;
                    if (cell.Prisoners[player.Id].ExpendTime(Interval) != 1) continue;
                    return true;
                }
                return false;
            }
        }

        private class Cell
        {
            public Area Area { get; set; } = new Area();

            public Dictionary<ulong, Prisoner> Prisoners { get; set; } = new Dictionary<ulong, Prisoner>();

            public int PrisonerCount => Prisoners.Count;

            public Cell()
            {

            }

            public Cell(Vector3 position1)
            {
                Area = new Area(position1);
                Prisoners = new Dictionary<ulong, Prisoner>();
            }

            public Cell(Vector3 position1, Vector3 position2)
            {
                Area = new Area(position1, position2);
                Prisoners = new Dictionary<ulong, Prisoner>();
            }

            private bool IsInArea(Vector3 position)
            {
                return Area.IsInArea(position);
            }

            public bool HasPrisoner(string player)
            {
                return Prisoners.Values.Any(
                    prisoner => string.Equals(prisoner.Name, player, StringComparison.OrdinalIgnoreCase));
            }

            public bool HasPrisoner(Player player)
            {
                return Prisoners.ContainsKey(player.Id);
            }

            public int AddPrisoner(Player player, Prisoner prisoner, bool teleport = true)
            {
                if (Prisoners.ContainsKey(player.Id)) return -1;
                try
                {
                    Prisoners.Add(player.Id, prisoner);
                    if (teleport) SendToCell(player);
                    return 1;
                }
                catch
                {
                    return 0;
                }
            }

            public int AddPrisoner(Player player, int time, Metric type, Vector3 release, bool teleport = true)
            {
                if (Prisoners.ContainsKey(player.Id)) return -1;
                try
                {
                    Prisoners.Add(player.Id, new Prisoner(player.Name, time, type, release));
                    if (teleport) SendToCell(player);
                    return 1;
                }
                catch
                {
                    return 0;
                }
            }

            public int RemovePrisoner(string player)
            {
                if (!HasPrisoner(player)) return -1;

                foreach (var prisoner in Prisoners)
                    if (prisoner.Value.Name == player)
                    {
                        Prisoners.Remove(prisoner.Key);
                        return 1;
                    }
                return 0;
            }

            public int RemovePrisoner(Player player, Position release, bool teleport = true)
            {
                if (!HasPrisoner(player)) return -1;
                try
                {
                    if (teleport) RemoveFromCell(player, release);
                    Prisoners.Remove(player.Id);
                    return 1;
                }
                catch
                {
                    return 0;
                }
            }

            public int SendToCell(Player player)
            {
                try
                {
                    EventManager.CallEvent(new TeleportEvent(player.Entity, Area.Center));
                    return 1;
                }
                catch
                {
                    return -1;
                }
            }

            public int RemoveFromCell(Player player, Position release)
            {
                try
                {
                    if (release == null) release = Prisoners[player.Id].Position;
                    EventManager.CallEvent(new TeleportEvent(player.Entity, release.Point));
                    return 1;
                }
                catch
                {
                    return -1;
                }
            }

            public HashSet<string> GetPrisoners()
            {
                return Prisoners.Values.Select(prisoner => prisoner.Name).ToHashSet();
            }

            public int CheckPrisoner(Player player)
            {
                if (!Prisoners.ContainsKey(player.Id)) return -1;
                return !IsInArea(player.Entity.Position) ? 0 : 1;
            }
        }

        private class Prisoner
        {
            public string Name { get; set; }
            public TimeSpan Time { get; set; }
            public Position Position { get; set; }

            public Prisoner()
            {

            }

            public Prisoner(string name, int time, Metric type, Vector3 position)
            {
                Name = name;
                switch (type)
                {
                    case Metric.Seconds:
                        Time = TimeSpan.FromSeconds(time);
                        break;
                    case Metric.Minutes:
                        Time = TimeSpan.FromMinutes(time);
                        break;
                    case Metric.Hours:
                        Time = TimeSpan.FromHours(time);
                        break;
                    case Metric.Days:
                        Time = TimeSpan.FromDays(time);
                        break;
                }
                Position = new Position(position);
            }

            public int ExpendTime(int time = 60)
            {
                Time = Time.Subtract(TimeSpan.FromSeconds(time));
                return Time <= TimeSpan.Zero ? 1 : 0;
            }
        }

        private class Area
        {
            public Position FirstPosition { get; set; }
            public Position SecondPosition { get; set; }

            public Vector3 Center => Vector3.Lerp(FirstPosition.Point, SecondPosition.Point, 0.5f);

            public Area() { }

            public Area(Vector3 position)
            {
                FirstPosition = new Position(position);
            }

            public Area(Vector3 position1, Vector3 position2)
            {
                FirstPosition = new Position(position1);
                SecondPosition = new Position(position2);
            }

            public int GetPositions()
            {
                if (FirstPosition == null) return 0;
                if (SecondPosition == null) return 1;
                return 2;
            }

            public int SetPosition(int type, Vector3 position)
            {
                if (GetPositions() == 2) return -2;
                switch (type)
                {
                    case 1:
                        if (GetPositions() == 1) return -1;
                        FirstPosition = new Position(position);
                        break;
                    case 2:
                        SecondPosition = new Position(position);
                        break;
                }
                return 1;
            }

            public bool IsInArea(Vector3 position)
            {
                if ((position.x < FirstPosition.X && position.x > SecondPosition.X) && (position.z > FirstPosition.Z && position.z < SecondPosition.Z) && (position.y > FirstPosition.Y && position.y < SecondPosition.Y)) return true;
                if ((position.x < FirstPosition.X && position.x > SecondPosition.X) && (position.z > FirstPosition.Z && position.z < SecondPosition.Z) && (position.y < FirstPosition.Y && position.y > SecondPosition.Y)) return true;
                if ((position.x < FirstPosition.X && position.x > SecondPosition.X) && (position.z < FirstPosition.Z && position.z > SecondPosition.Z) && (position.y > FirstPosition.Y && position.y < SecondPosition.Y)) return true;
                if ((position.x < FirstPosition.X && position.x > SecondPosition.X) && (position.z < FirstPosition.Z && position.z > SecondPosition.Z) && (position.y < FirstPosition.Y && position.y > SecondPosition.Y)) return true;
                if ((position.x > FirstPosition.X && position.x < SecondPosition.X) && (position.z < FirstPosition.Z && position.z > SecondPosition.Z) && (position.y > FirstPosition.Y && position.y < SecondPosition.Y)) return true;
                if ((position.x > FirstPosition.X && position.x < SecondPosition.X) && (position.z < FirstPosition.Z && position.z > SecondPosition.Z) && (position.y < FirstPosition.Y && position.y > SecondPosition.Y)) return true;
                if ((position.x > FirstPosition.X && position.x < SecondPosition.X) && (position.z > FirstPosition.Z && position.z < SecondPosition.Z) && (position.y > FirstPosition.Y && position.y < SecondPosition.Y)) return true;
                if ((position.x > FirstPosition.X && position.x < SecondPosition.X) && (position.z > FirstPosition.Z && position.z < SecondPosition.Z) && (position.y < FirstPosition.Y && position.y > SecondPosition.Y)) return true;

                return false;
            }
        }

        private class Position
        {
            public float X { get; set; } = 0f;
            public float Y { get; set; } = 0f;
            public float Z { get; set; } = 0f;

            public Position() { }

            public Position(Vector3 position)
            {
                X = position.x;
                Y = position.y;
                Z = position.z;
            }

            public Vector3 Point => new Vector3(X, Y, Z);
        }

        #endregion

        #region Save and Load Data

        private void Loaded()
        {
            LoadDefaultMessages();
            LoadConfigData();
            LoadPrisonData();

            permission.RegisterPermission("Prison.Imprison", this);
            permission.RegisterPermission("Prison.Free", this);
            permission.RegisterPermission("Prison.Modify", this);

            timer.Repeat(RollCall, 0, CheckJail);
            timer.Repeat(Interval, 0, ExpendTime);
        }

        private void LoadConfigData()
        {
            UsePublicNotifications = GetConfig("Use Public Notifications", true);
            RollCall = GetConfig("Roll Call", 15);
            Interval = GetConfig("Timer Inverval", 1);
        }

        private void SaveConfigData()
        {
            Config["Use Public Notifications"] = UsePublicNotifications;
            Config["Roll Call"] = RollCall;
            Config["Timer Inverval"] = Interval;
        }

        protected override void LoadDefaultConfig()
        {
            LoadConfigData();
            SaveConfigData();
        }

        private void LoadPrisonData()
        {
            _Jail = Interface.Oxide.DataFileSystem.ReadObject<Jail>("Prison");
        }

        private void SavePrisonData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("Prison", _Jail);
        }

        private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "No Permission", "You do not have permission to use this command." },
                { "Invalid Args", "Something went wrong. Please use /phelp to see if you used the correct format." },
                { "Invalid Player", "We could not find that player in any of our cells. Did you misspell the name?" },
                { "Invalid Online Player", "That player does not appear to be online right now." },
                { "Invalid Cell Number", "That cell is currently not available." },
                { "Sentence Remaining", "Your remaining sentence time is {0}." },
                { "Release Set", "The position for released players was set to {0}." },
                { "Release Remove", "The position for released players was removed." },
                { "No Cell", "That cell is currently unavailable." },
                { "Cell Exists", "That cell already exists." },
                { "Cell First", "The first position for cell {0} was set to {1}." },
                { "Cell Second", "The second position for cell {0} was set to {1}." },
                { "Cell Created", "Cell {0} was created." },
                { "Cell Removed", "Cell {0} was removed." },
                { "Cell Contains", "Cell {0} contains:" },
                { "Cell Contains Empty", "Cell {0} contains no Prisoners." },
                { "Prison Wiped", "All cells were removed." },
                { "Already Imprisoned", "That player already is in a cell." },
                { "No Prisoner", "You are not imprisoned." },
                { "Not Imprisoned", "That player does not appear to be in any of our cells." },
                { "Moved Player", "{0} was moved to a new cell." },
                { "Moved", "You were moved to a new cell." },
                { "Imprisoned Player", "{0} has been imprisoned for {1} {2} in cell {3}." },
                { "Imprisoned", "You have been imprisoned for {0} {1}." },
                { "Imprisoned Notification", "{0} has been imprisoned!" },
                { "Freed Player", "{0} was released from imprisonment." },
                { "Freed", "You have been released from imprisonment." },
                { "Freed Notification", "{0} was released from imprisonment!" },
                { "Escape Caught", "The guards caught you and brought you back to your cell." },
                { "Escape Caught Noticifaction", "{0} tried to escape but was caught and brought back to prison!" },
                { "Help Title", "[0000FF]Prison Commands[FFFFFF]" },
                { "Help Sentence", "[00FF00]/sentence[FFFFFF] - Show how long before your release from imprisonment." },
                { "Help Imprison", "[00FF00]/imprison (playername) (time) (optional: sec/min/hour/day) (optional: cell number)[FFFFFF] - Imprison a player for a said amount of time. Time is in minutes by default." },
                { "Help Move Player", "[00FF00]/moveprisoner (playername) (cell)[FFFFFF] - Move a prisoner to a different cell." },
                { "Help Free", "[00FF00]/free (playername)[FFFFFF] - Release a player from imprisonment." },
                { "Help Cell", "[00FF00]/cell (number)[FFFFFF] - List the prisoners in this cell." },
                { "Help Add Mark", "[00FF00]/p.addmark (optional: cell)[FFFFFF] - Set a mark for a cell." },
                { "Help Remove Mark", "[00FF00]/p.removemark (cell)[FFFFFF] - Remove the marks for a cell." },
                { "Help Remove All Marks", "[00FF00]/p.removeallmarks[FFFFFF] - Remove the marks of all cells. WARNING - Will remove all prisoners!" },
                { "Help Set Release", "[00FF00]/p.setrelease[FFFFFF] - Set the release point for new prisoners." },
                { "Help Remove Release", "[00FF00]/p.removerelease[FFFFFF] - Remove the release point for new prisoners." }
            }, this);
        }

        #endregion

        #region Commands

        [ChatCommand("sentence")]
        private void CmdShowSentenceTime(Player player, string cmd, string[] args)
        {
            ShowSentenceTime(player);
        }

        [ChatCommand("imprison")]
        private void CmdImprisonPlayer(Player player, string cmd, string[] args)
        {
            ImprisonPlayer(player, args);
        }

        [ChatCommand("free")]
        private void CmdFreePlayer(Player player, string cmd, string[] args)
        {
            FreePlayer(player, args);
        }

        [ChatCommand("cell")]
        private void CmdListCellPrisoners(Player player, string cmd, string[] args)
        {
            ListCellPrisoners(player, args);
        }

        [ChatCommand("moveprisoner")]
        private void CmdMovePrisoner(Player player, string cmd, string[] args)
        {
            MovePrisoner(player, args);
        }

        [ChatCommand("p.addmark")]
        private void CmdAddMark(Player player, string cmd, string[] args)
        {
            AddMark(player, args);
        }

        [ChatCommand("p.removemark")]
        private void CmdRemoveMark(Player player, string cmd, string[] args)
        {
            RemoveMark(player, args);
        }

        [ChatCommand("p.removeallmarks")]
        private void CmdRemoveAllMarks(Player player, string cmd, string[] args)
        {
            RemoveAllMarks(player);
        }

        [ChatCommand("p.setrelease")]
        private void CmdSetRelease(Player player, string cmd, string[] args)
        {
            SetRelease(player);
        }

        [ChatCommand("p.removerelease")]
        private void CmdRemoveRelease(Player player, string cmd, string[] args)
        {
            RemoveRelease(player);
        }

        [ChatCommand("phelp")]
        private void CmdSendHelpText(Player player, string cmd, string[] args)
        {
            SendHelpText(player);
        }

        #endregion

        #region Command Functions

        private void ShowSentenceTime(Player player)
        {
            var time = _Jail.GetSentenceTime(player);
            if (time == "0")
                player.SendMessage(GetMessage("No Prisoner", player));
            else
                player.ShowPopup("Prison", string.Format(GetMessage("Sentence Remaining", player), time));
        }

        private void ImprisonPlayer(Player player, string[] input)
        {
            if (!player.HasPermission("Prison.Imprison")) { player.SendError(GetMessage("No Permission", player)); return; }

            if (input.Length < 2) { player.SendError(GetMessage("Invalid Args", player)); return; }

            ulong targetId;
            var target = ulong.TryParse(input[0], out targetId) ? Server.GetPlayerById(targetId) : Server.GetPlayerByName(input[0]);
            if (target == null) { player.SendError(GetMessage("Invalid Online Player", player)); return; }

            int time;
            if (!int.TryParse(input[1], out time)) { player.SendError(GetMessage("Invalid Args", player)); return; }
            var type = Metric.Minutes;
            var cellNumber = 0;
            if (input.Length > 2)
            {
                switch (input[2].ToLower())
                {
                    case "s":
                    case "sec":
                    case "second":
                    case "seconds":
                        type = Metric.Seconds;
                        break;
                    case "m":
                    case "min":
                    case "minute":
                    case "minutes":
                        type = Metric.Minutes;
                        break;
                    case "h":
                    case "hour":
                    case "hours":
                        type = Metric.Hours;
                        break;
                    case "d":
                    case "day":
                    case "days":
                        type = Metric.Days;
                        break;
                    default:
                        if (!int.TryParse(input[2], out cellNumber)) { player.SendError(GetMessage("Invalid Args", player)); return; }
                        break;
                }
                if (input.Length > 3)
                    if (!int.TryParse(input[3], out cellNumber)) { player.SendError(GetMessage("Invalid Args", player)); return; }
            }
            var result = _Jail.Imprison(target, time, type, cellNumber);
            switch (result[0])
            {
                case -3:
                    player.SendError(GetMessage("No Release Position", player));
                    return;
                case -2:
                    player.SendError(GetMessage("Invalid Cell Number", player));
                    return;
                case -1:
                    player.SendError(GetMessage("Already Imprisoned", player));
                    return;
                case 0:
                    player.SendError(GetMessage("Invalid Args", player));
                    return;
                case 1:
                    player.SendMessage(GetMessage("Imprisoned Player"), target.Name, time, Enum.GetName(typeof(Metric), type), result[1]);
                    target.ShowPopup("Prison", string.Format(GetMessage("Imprisoned", target), time, Enum.GetName(typeof(Metric), type)));
                    if (UsePublicNotifications) PrintToChat(GetMessage("Imprisoned Notification"), target.Name);
                    break;
            }

            SavePrisonData();
        }

        private void FreePlayer(Player player, string[] input)
        {
            if (!player.HasPermission("Prison.Free")) { player.SendError(GetMessage("No Permission", player)); return; }

            if (input.Length < 1) { player.SendError(GetMessage("Invalid Args", player)); return; }

            var target = Server.GetPlayerByName(input.JoinToString(" "));
            if (target == null)
                switch (_Jail.Free(input.JoinToString(" ")))
                {
                    case -1:
                        player.SendError(GetMessage("Not Imprisoned", player));
                        return;
                    case 0:
                        return;
                    case 1:
                        player.SendMessage(GetMessage("Freed Player", player), input.JoinToString(" "));
                        break;
                }
            else
                switch (_Jail.Free(target))
                {
                    case -1:
                        player.SendError(GetMessage("Not Imprisoned", player));
                        return;
                    case 0:
                        return;
                    case 1:
                        player.SendMessage(GetMessage("Freed Player", player), input.JoinToString(" "));
                        target.ShowPopup("Prison", GetMessage("Freed", target));
                        if (UsePublicNotifications) PrintToChat(GetMessage("Freed Notification"), target.Name);
                        break;
                }

            SavePrisonData();
        }

        private void ListCellPrisoners(Player player, string[] input)
        {
            if (!player.HasPermission("Prison.Free")) { player.SendError(GetMessage("No Permission", player)); return; }

            if (input.Length < 1) { player.SendError(GetMessage("Invalid Args", player)); return; }

            int cellNumber;
            if (!int.TryParse(input[0], out cellNumber)) { player.SendError(GetMessage("Invalid Args", player)); return; }

            var result = _Jail.GetPrisoners(cellNumber);
            var msg = string.Format(GetMessage("Cell Contains", player), cellNumber);
            if (result.Count == 0) msg = string.Format(GetMessage("Cell Contains Empty", player), cellNumber);
            msg = result.Aggregate(msg, (current, m) => current + $"{m}\n\r");

            player.SendMessage(msg);
        }

        private void MovePrisoner(Player player, string[] input)
        {
            if (!player.HasPermission("Prison.imprison")) { player.SendError(GetMessage("No Permission", player)); return; }

            if (input.Length < 2) { player.SendError(GetMessage("Invalid Args", player)); return; }

            var name = input[0];
            var target = Server.GetPlayerByName(name);

            int cellNumber;
            if (!int.TryParse(input[1], out cellNumber)) { player.SendError(GetMessage("Invalid Args", player)); return; }

            var result = _Jail.MovePrisoner(target, cellNumber);
            switch (result)
            {
                case -2:
                    player.SendError(GetMessage("Not Imprisoned", player));
                    break;
                case -1:
                    player.SendError(GetMessage("No Cell", player));
                    break;
                case 0:
                    player.SendError(GetMessage("No Cell", player));
                    break;
                case 1:
                    player.SendMessage(GetMessage("Moved Player", player), target.Name);
                    target.ShowPopup("Prison", GetMessage("Moved", target));
                    break;
            }

            SavePrisonData();
        }

        private void AddMark(Player player, string[] input)
        {
            if (!player.HasPermission("Prison.Modify")) { player.SendError(GetMessage("No Permission", player)); return; }

            var cellNumber = 0;
            if (input.Any())
                if (!int.TryParse(input[0], out cellNumber)) { player.SendError(GetMessage("Invalid Args", player)); return; }

            var responce = _Jail.AddCell(player.Entity.Position, cellNumber);
            switch (responce[0])
            {
                case 0:
                    player.SendError(GetMessage("Cell Exists", player));
                    return;
                case 1:
                    player.SendMessage(GetMessage("Cell First", player), responce[1], player.Entity.Position);
                    break;
                case 2:
                    player.SendMessage(GetMessage("Cell Second", player), responce[1], player.Entity.Position);
                    player.SendMessage(GetMessage("Cell Created", player), responce[1]);
                    break;
            }

            SavePrisonData();
        }

        private void RemoveMark(Player player, string[] input)
        {
            if (!player.HasPermission("Prison.Modify")) { player.SendError(GetMessage("No Permission", player)); return; }

            int cellNumber;
            if (!int.TryParse(input[0], out cellNumber)) { player.SendError(GetMessage("Invalid Args", player)); return; }

            switch (_Jail.RemoveCell(cellNumber))
            {
                case -1:
                    player.SendError(GetMessage("Invalid Cell Number", player));
                    return;
                case 1:
                    player.SendMessage(GetMessage("Cell Removed", player), cellNumber);
                    break;
            }

            SavePrisonData();
        }

        private void RemoveAllMarks(Player player)
        {
            if (!player.HasPermission("Prison.Modify")) { player.SendError(GetMessage("No Permission", player)); return; }

            _Jail.Reset();
            player.SendMessage(GetMessage("Prison Wiped", player));

            SavePrisonData();
        }

        private void SetRelease(Player player)
        {
            if (!player.HasPermission("Prison.Modify")) { player.SendError(GetMessage("No Permission", player)); return; }

            _Jail.AddRelease(player.Entity.Position);
            player.SendMessage(GetMessage("Release Set", player), player.Entity.Position);

            SavePrisonData();
        }

        private void RemoveRelease(Player player)
        {
            if (!player.HasPermission("Prison.Modify")) { player.SendError(GetMessage("No Permission", player)); return; }

            _Jail.RemoveRelease();
            player.SendMessage(GetMessage("Release Remove", player));

            SavePrisonData();
        }

        #endregion

        #region System Functions

        private void CheckJail()
        {
            try
            {
                foreach (var player in Server.ClientPlayers)
                {
                    if (player == null) continue;
                    if (player.Entity == null) continue;
                    if (_Jail.CheckPrisoner(player)) continue;
                    player.ShowPopup("Prison", GetMessage("Escape Caught", player));
                    if (UsePublicNotifications) PrintToChat(GetMessage("Escape Caught Noticifaction"), player.Name);
                }
            }
            catch
            {
                Puts("Check prisoners broke;");
            }
        }

        private void ExpendTime()
        {
            try
            {
                foreach (var player in Server.ClientPlayers)
                {
                    if (!_Jail.ExpendTime(player)) continue;
                    Free(player);
                }
            }
            catch
            {
                Puts("Expending time broke.");
            }

            SavePrisonData();
        }

        private int Imprison(Player player, int time, string metric = "minute", int cell = 0)
        {
            if (player == null) return -1;
            Metric type;
            switch (metric.ToLower())
            {
                case "s":
                case "sec":
                case "second":
                case "seconds":
                    type = Metric.Seconds;
                    break;
                case "m":
                case "min":
                case "minute":
                case "minutes":
                    type = Metric.Minutes;
                    break;
                case "h":
                case "hour":
                case "hours":
                    type = Metric.Hours;
                    break;
                case "d":
                case "day":
                case "days":
                    type = Metric.Days;
                    break;
                default:
                    return -2;
            }
            var result = _Jail.Imprison(player, time, type, cell);
            switch (result[0])
            {
                case 1:
                    player.ShowPopup("Prison", string.Format(GetMessage("Imprisoned", player), time, Enum.GetName(typeof(Metric), type)));
                    if (UsePublicNotifications) PrintToChat(GetMessage("Imprisoned Notification"), player.Name);
                    return 1;
                default:
                    return 0;
            }
        }

        private int Free(Player player)
        {
            if (player == null) return -1;
            switch (_Jail.Free(player))
            {
                case 1:
                    player.ShowPopup("Prison", GetMessage("Freed", player));
                    if (UsePublicNotifications) PrintToChat(GetMessage("Freed Notification"), player.Name);
                    return 1;
                default:
                    return 0;
            }
        }

        #endregion

        #region Hooks

        private void SendHelpText(Player player)
        {
            player.SendMessage(GetMessage("Help Title", player));
            player.SendMessage(GetMessage("Help Sentence", player));
            if (player.HasPermission("prison.imprison"))
            {
                player.SendMessage(GetMessage("Help Imprison", player));
                player.SendMessage(GetMessage("Help Move Player", player));
            }
            if (player.HasPermission("prison.free"))
            {
                player.SendMessage(GetMessage("Help Free", player));
                player.SendMessage(GetMessage("Help Cell", player));
            }
            if (player.HasPermission("prison.modify"))
            {
                player.SendMessage(GetMessage("Help Add Mark", player));
                player.SendMessage(GetMessage("Help Remove Mark", player));
                player.SendMessage(GetMessage("Help Remove All Marks", player));
                player.SendMessage(GetMessage("Help Set Release", player));
                player.SendMessage(GetMessage("Help Remove Release", player));
            }
        }

        #endregion

        #region Utility

        private T GetConfig<T>(string name, T defaultValue)
        {
            if (Config[name] == null) return defaultValue;
            return (T)Convert.ChangeType(Config[name], typeof(T));
        }

        private string GetMessage(string key, Player player = null) => lang.GetMessage(key, this, player?.Id.ToString());

        #endregion
    }
}