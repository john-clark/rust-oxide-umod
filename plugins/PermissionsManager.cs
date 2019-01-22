using System;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using UnityEngine;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("PermissionsManager", "Steenamaroo", "0.0.9", ResourceId = 2629)]
    class PermissionsManager : RustPlugin
    {
        #region VariablesAndStorage
        Dictionary<int, string> PlugList = new Dictionary<int, string>();
        Dictionary<int,  List<string>> GroupUsersList = new Dictionary<int, List<string>>();
        Dictionary<int, string> numberedPerms = new Dictionary<int, string>();
        List<ulong> MenuOpen = new List<ulong>();

        Dictionary<ulong, info> ActiveAdmins = new Dictionary<ulong, info>();
        
        public class info
        {
            public int plugNumber;
            public int noOfGroups;
            public int noOfPlugs;
            public bool exists = false;
            public int previousPage = 1;
            public string subjectGroup;
            public BasePlayer subject;
        }

        string ButtonColour1 = "0.7 0.32 0.17 1";
        string ButtonColour2 = "0.2 0.2 0.2 1";
        #endregion

        void OnPluginLoaded(Plugin plugin)
        {
            if (plugin is PermissionsManager)
            return;
            Wipe();
            OnServerInitialized();
        }
        void OnPluginUnloaded(Plugin plugin)
        {
            if (plugin is PermissionsManager)
            return;
            Wipe();
            OnServerInitialized();
        }
        void OnServerInitialized()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (MenuOpen.Contains(player.userID))
                {
                    CuiHelper.DestroyUi(player, "MenuGUI1");
                    CuiHelper.DestroyUi(player, "MenuGUI");
                    MenuOpen.Remove(player.userID);
                }
            }
            lang.RegisterMessages(messages, this);

            LoadConfigVariables();
            SaveConfig();
        }

        void Wipe()
        {
            PlugList.Clear();
            GroupUsersList.Clear();
            numberedPerms.Clear();
        }
        
        void GetPlugs(BasePlayer player)
        {
            var path = ActiveAdmins[player.userID];
            PlugList.Clear();
            path.noOfPlugs = 0;
            List<string> sortedPlugs = new List<string>();
            foreach (var entry in plugins.GetAll())
            {
                if (entry.IsCorePlugin)
                continue;

                var str = entry.ToString();
                var charsToRemove = new string[] { "Oxide.Plugins." };
                foreach (var c in charsToRemove)
                {
                    str = str.Replace(c, string.Empty).ToLower();
                }
                
                foreach (var perm in permission.GetPermissions().ToList())
                {
                    if (perm.Contains($"{str}") && !(BlockList.Split(',').ToList().Contains($"{str}")))
                    {
                    path.exists = false;

                        foreach (var livePlug in sortedPlugs)
                        if(livePlug == str)
                        path.exists = true; //prevent duplicates entries
                        if(!path.exists)
                        {
                        sortedPlugs.Add(str);// add to list for sorting
                        }
                    }
                }
            }
            sortedPlugs.Sort();
            foreach (var entry in sortedPlugs) //bring from sorted list to numbered dictionary
            {
                path.noOfPlugs++;
                PlugList.Add(path.noOfPlugs, entry);
            }
        }
        
        void GetGroups(BasePlayer player)
        {
        var path = ActiveAdmins[player.userID];
        GroupUsersList.Clear();
        path.noOfGroups = 0;
        
            foreach (var group in permission.GetGroups())
            {
                path.noOfGroups++;
                GroupUsersList.Add(path.noOfGroups, new List<string>());
                GroupUsersList[path.noOfGroups].Add(group);          // first entry is group name - players follow

                foreach (var useringroup in permission.GetUsersInGroup(group))
                {
                    GroupUsersList[path.noOfGroups].Add(useringroup);
                }
            }
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Creating a new config file");
            Config.Clear();
            LoadConfigVariables();
            SaveConfig();
        }

        void Unload()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (MenuOpen.Contains(player.userID))
                {
                CuiHelper.DestroyUi(player, "MenuGUI1");
                CuiHelper.DestroyUi(player, "MenuGUI");
                MenuOpen.Remove(player.userID);
                }
            }
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (MenuOpen.Contains(player.userID))
            {
            CuiHelper.DestroyUi(player, "MenuGUI1");
            CuiHelper.DestroyUi(player, "MenuGUI");
            MenuOpen.Remove(player.userID);
            }
        }

        bool isAuth(BasePlayer player)
        {
            if (player.net.connection != null)
                if (player.net.connection.authLevel < 2)
                    return false;
                    return true;
        }

        void MainUI(BasePlayer player, string msg, string isgroup, int page)
        {
            int pageNo = Convert.ToInt32(page);
            string group = isgroup;
            string guiString = String.Format("0.1 0.1 0.1 {0}", guitransparency);
            var elements = new CuiElementContainer();

            var mainName = elements.Add(new CuiPanel
            {
                Image = { Color = guiString },

                RectTransform = { AnchorMin = "0.3 0.1", AnchorMax = "0.7 0.9" },
                CursorEnabled = true
            }, "Overlay", "MenuGUI");

            elements.Add(new CuiElement
            {
                Parent = "MenuGUI",
                Components =
                {
                new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });

        double LeftAMin1 = 0.1;
        double AMin2 = 0.89;
        double LeftAMax1 = 0.3;
        double AMax2 = 0.91;
        double CenterAMin1 = 0.4;
        double CenterAMax1 = 0.6;
        double RightAMin1 = 0.7;
        double RightAMax1 = 0.9;
       
        int plugsTotal = 0;
        int pos1 = (60 - (page*60));
        int next = (page+1);
        int previous = (page-1);
    
   foreach (var plug in PlugList)
   {
        pos1++;
        plugsTotal++;
        var plugNo = plug.Key;
        
        if (plugNo < (-39 + (page*60)) && plugNo > (-60 + (page*60)))
        {
            elements.Add(new CuiButton
            { Button = { Command = $"permsList {plugNo} null null {group} null 1", Color = ButtonColour }, RectTransform = { AnchorMin = $"{LeftAMin1} {(AMin2 - (pos1*3f)/100f)}", AnchorMax = $"{LeftAMax1} {(AMax2 - (pos1*3f)/100f)}" }, Text = { Text = $"{PlugList[plugNo]}", FontSize = 10, Align = TextAnchor.MiddleCenter } }, mainName);
        }
        if (plugNo < (-19 + (page*60)) && plugNo > (-40 + (page*60)))
        {
            elements.Add(new CuiButton
            { Button = { Command = $"permsList {plugNo} null null {group} null 1", Color = ButtonColour }, RectTransform = { AnchorMin = $"{CenterAMin1} {(AMin2 - ((pos1-20)*3f)/100f)}", AnchorMax = $"{CenterAMax1} {(AMax2 - ((pos1-20)*3f)/100f)}" }, Text = { Text = $"{PlugList[plugNo]}", FontSize = 10, Align = TextAnchor.MiddleCenter } }, mainName);
        }
        if (plugNo < (1 + (page*60)) && plugNo > (-20 + (page*60)))
        {
            elements.Add(new CuiButton
            { Button = { Command = $"permsList {plugNo} null null {group} null 1", Color = ButtonColour }, RectTransform = { AnchorMin = $"{RightAMin1} {(AMin2 - ((pos1-40)*3f)/100f)}", AnchorMax = $"{RightAMax1} {(AMax2 - ((pos1-40)*3f)/100f)}" }, Text = { Text = $"{PlugList[plugNo]}", FontSize = 10, Align = TextAnchor.MiddleCenter } }, mainName);
        }
   }
            elements.Add(new CuiButton { Button = { Command = "ClosePM", Color = ButtonColour }, RectTransform = { AnchorMin = "0.55 0.02", AnchorMax = "0.7 0.06" }, Text = { Text = lang.GetMessage("Close", this), FontSize = 14, Align = TextAnchor.MiddleCenter} }, mainName);
            elements.Add(new CuiLabel { Text = { Text = msg, FontSize = 16, Align = TextAnchor.MiddleCenter }, RectTransform = { AnchorMin = "0 0.9", AnchorMax = "1 0.98" } }, mainName);
            
            if(isgroup == "false")
            elements.Add(new CuiButton { Button = { Command = "Groups 1", Color = ButtonColour }, RectTransform = { AnchorMin = "0.3 0.02", AnchorMax = "0.45 0.06" }, Text = { Text = lang.GetMessage("Groups", this), FontSize = 14, Align = TextAnchor.MiddleCenter} }, mainName);

            if (plugsTotal > (page*60))
            elements.Add(new CuiButton { Button = { Command = $"Navigate {group} {next}", Color = ButtonColour }, RectTransform = { AnchorMin = "0.8 0.02", AnchorMax = "0.9 0.06" }, Text = { Text = lang.GetMessage("->", this), FontSize = 14, Align = TextAnchor.MiddleCenter}, }, mainName);
            
            if (page > 1)
            elements.Add(new CuiButton { Button = { Command = $"Navigate {group} {previous}", Color = ButtonColour }, RectTransform = { AnchorMin = "0.1 0.02", AnchorMax = "0.2 0.06" }, Text = { Text = lang.GetMessage("<-", this), FontSize = 14, Align = TextAnchor.MiddleCenter}, }, mainName);
            
            if (group == "true") elements.Add(new CuiButton
            { Button = { Command = $"EmptyGroup", Color = ButtonColour }, RectTransform = { AnchorMin = "0.3 0.02", AnchorMax = "0.45 0.06" }, Text = { Text = lang.GetMessage("removePlayers", this), FontSize = 14, Align = TextAnchor.MiddleCenter} }, mainName);

            
            CuiHelper.AddUi(player, elements);
        }

        object PermsCheck(BasePlayer player, string group, string info)
        {
            var path = ActiveAdmins[player.userID];
            if (group == "true" && permission.GroupHasPermission(path.subjectGroup, info))
            return true;
            else if (group == "false" && permission.UserHasPermission(path.subject.userID.ToString(), info))
            return true;
            else return false;
        }

        object GroupCheck(int groupNo, string ID)
        {
            bool present = false;
                foreach (var user in GroupUsersList[groupNo])
                {
                if (user.Contains(ID))
                return true;
                }
            return false;
        }

        void PermsUI(BasePlayer player, string msg, int PlugNumber, string permSet, string group, int page)
        {
            var path = ActiveAdmins[player.userID];
            int total = 0;
            foreach (var item in numberedPerms)
            {
                total++;
            }

            string guiString = String.Format("0.1 0.1 0.1 {0}", guitransparency);
            var elements = new CuiElementContainer();

            var mainName = elements.Add(new CuiPanel
            {
                Image = { Color = guiString },

                RectTransform = { AnchorMin = "0.3 0.1", AnchorMax = "0.7 0.9" },
                CursorEnabled = true
            }, "Overlay", "MenuGUI");

            elements.Add(new CuiElement
            {
                Parent = "MenuGUI",
                Components =
                {
                new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });

//left column

    double AMin1 = 0.2;
    double AMin2 = 0.89;
    double AMax1 = 0.5;
    double AMax2 = 0.91;
    double BMin1 = 0.55;
    double BMax1 = 0.65;
    double CMin1 = 0.7;
    double CMax1 = 0.8;
    
    double RightAMin1 = 0.52;
    double RightAMax1 = 0.75;
    double RightBMin1 = 0.77;
    double RightBMax1 = 0.85;
 
    int permsTotal = 0;
    int pos1 = (20 - (page*20));
    int next = (page+1);
    int previous = (page-1);
    
   foreach (var perm in numberedPerms)
   {
        pos1++;
        permsTotal++;
        var permNo = perm.Key;
        string showName = numberedPerms[permNo].ToString();
        string output = showName.Substring(showName.IndexOf('.') + 1);
                 
        if (permNo < (1 + (page*20)) && permNo > (-20 + (page*20)))
        {
            if ((bool)PermsCheck(player, group, numberedPerms[permNo]))
            { ButtonColour1 = Colour1; ButtonColour2 = Colour2; } else{ ButtonColour1 =Colour2; ButtonColour2 = Colour1; }
            
            elements.Add(new CuiButton
            { Button = { Color = ButtonColour }, RectTransform = { AnchorMin = $"{AMin1} {(AMin2 - (pos1*3f)/100f)}", AnchorMax = $"{AMax1} {(AMax2 - (pos1*3f)/100f)}" }, Text = { Text = $"{output}", FontSize = 10, Align = TextAnchor.MiddleCenter } }, mainName);
        
            elements.Add(new CuiButton
            { Button = { Command = $"permsList {PlugNumber} grant {numberedPerms[permNo]} {group} null {page}", Color = ButtonColour1 }, RectTransform = { AnchorMin = $"{BMin1} {(AMin2 - (pos1*3f)/100f)}", AnchorMax = $"{BMax1} {(AMax2 - (pos1*3f)/100f)}" }, Text = { Text = lang.GetMessage("Granted", this), FontSize = 10, Align = TextAnchor.MiddleCenter } }, mainName);
            
            elements.Add(new CuiButton
            { Button = { Command = $"permsList {PlugNumber} revoke {numberedPerms[permNo]} {group} null {page}", Color = ButtonColour2 }, RectTransform = { AnchorMin = $"{CMin1} {(AMin2 - (pos1*3f)/100f)}", AnchorMax = $"{CMax1} {(AMax2 - (pos1*3f)/100f)}" }, Text = { Text = lang.GetMessage("Revoked", this), FontSize = 10, Align = TextAnchor.MiddleCenter } }, mainName);
        } 
    }
            elements.Add(new CuiButton { Button = { Command = $"Navigate {group} {path.previousPage}", Color = ButtonColour }, RectTransform = { AnchorMin = "0.4 0.02", AnchorMax = "0.6 0.06" }, Text = { Text = lang.GetMessage("Back", this), FontSize = 14, Align = TextAnchor.MiddleCenter} }, mainName);
            elements.Add(new CuiButton { Button = { Command = $"permsList {PlugNumber} grant null {group} all {page}", Color = ButtonColour }, RectTransform = { AnchorMin = "0.3 0.02", AnchorMax = "0.36 0.06" }, Text = { Text = lang.GetMessage("All", this), FontSize = 14, Align = TextAnchor.MiddleCenter} }, mainName);
            elements.Add(new CuiButton { Button = { Command = $"permsList {PlugNumber} revoke null {group} all {page}", Color = ButtonColour }, RectTransform = { AnchorMin = "0.64 0.02", AnchorMax = "0.7 0.06" }, Text = { Text = lang.GetMessage("None", this), FontSize = 14, Align = TextAnchor.MiddleCenter} }, mainName);
            elements.Add(new CuiLabel { Text = { Text = msg, FontSize = 16, Align = TextAnchor.MiddleCenter }, RectTransform = { AnchorMin = "0 0.9", AnchorMax = "1 0.98" } }, mainName);

            if (permsTotal > (page*20)) elements.Add(new CuiButton
            { Button = { Command = $"permsList {PlugNumber} null null {group} null {next}", Color = ButtonColour }, RectTransform = { AnchorMin = "0.72 0.02", AnchorMax = "0.8 0.06" }, Text = { Text = "->", FontSize = 14, Align = TextAnchor.MiddleCenter} }, mainName);
            if (page > 1) elements.Add(new CuiButton
            { Button = { Command = $"permsList {PlugNumber} null null {group} null {previous}", Color = ButtonColour }, RectTransform = { AnchorMin = "0.2 0.02", AnchorMax = "0.28 0.06" }, Text = { Text = "<-", FontSize = 14, Align = TextAnchor.MiddleCenter} }, mainName);
            
            CuiHelper.AddUi(player, elements);
        }
        
        void GroupsUI(BasePlayer player, string msg, int page)
        {
            var path = ActiveAdmins[player.userID];
            int groupTotal = 0;
            var outmsg = string.Format(lang.GetMessage("GUIGroupsFor", this), msg);
            string guiString = String.Format("0.1 0.1 0.1 {0}", guitransparency);
            var elements = new CuiElementContainer();
            var mainName = elements.Add(new CuiPanel
            {
                Image = { Color = guiString },

                RectTransform = { AnchorMin = "0.3 0.1", AnchorMax = "0.7 0.9" },
                CursorEnabled = true
            }, "Overlay", "MenuGUI");

            elements.Add(new CuiElement
            {
                Parent = "MenuGUI",
                Components =
                {
                new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });

            //left column
            double LeftAMin1 = 0.05;
            double AMin2 = 0.89;
            double LeftAMax1 = 0.2;
            double AMax2 = 0.91;
            double LeftBMin1 = 0.22;
            double LeftBMax1 = 0.3;
            double LeftCMin1 = 0.32;
            double LeftCMax1 = 0.4;
            
            double RightAMin1 = 0.55;
            double RightAMax1 = 0.7;
            double RightBMin1 = 0.72;
            double RightBMax1 = 0.8;
            double RightCMin1 = 0.82;
            double RightCMax1 = 0.9;
            int next = (page+1);
            int previous = (page-1);
            int pos1 = (40 - (page*40));  // gets 1,2,3,4,etc regardless of page
            foreach (var group in GroupUsersList)
            {
                pos1++;
                groupTotal++;
                 var groupNo = group.Key;

                 if (groupNo < (-19 + (page*40)) && groupNo > (-40 + (page*40)))
                 {
                     if ((bool)GroupCheck(groupNo, path.subject.userID.ToString()))
                     { ButtonColour1 = Colour1; ButtonColour2 = Colour2; } else{ ButtonColour1 =Colour2; ButtonColour2 = Colour1; }
                     
                     elements.Add(new CuiButton
                     { Button = { Color = ButtonColour }, RectTransform = { AnchorMin = $"{LeftAMin1} {(AMin2 - (pos1*3f)/100f)}", AnchorMax = $"{LeftAMax1} {(AMax2 - (pos1*3f)/100f)}" }, Text = { Text = $"{GroupUsersList[groupNo][0]}", FontSize = 10, Align = TextAnchor.MiddleCenter } }, mainName);
         
                     elements.Add(new CuiButton
                     { Button = { Command = $"GroupAddRemove add {GroupUsersList[groupNo][0]} {page}", Color = ButtonColour1 }, RectTransform = { AnchorMin = $"{LeftBMin1} {(AMin2 - (pos1*3f)/100f)}", AnchorMax = $"{LeftBMax1} {(AMax2 - (pos1*3f)/100f)}" }, Text = { Text = lang.GetMessage("Granted", this), FontSize = 10, Align = TextAnchor.MiddleCenter } }, mainName);
                     
                     elements.Add(new CuiButton
                     { Button = { Command = $"GroupAddRemove remove {GroupUsersList[groupNo][0]} {page}", Color = ButtonColour2 }, RectTransform = { AnchorMin = $"{LeftCMin1} {(AMin2 - (pos1*3f)/100f)}", AnchorMax = $"{LeftCMax1} {(AMax2 - (pos1*3f)/100f)}" }, Text = { Text = lang.GetMessage("Revoked", this), FontSize = 10, Align = TextAnchor.MiddleCenter } }, mainName);
                 }
                
                 if (groupNo < (1 + (page*40)) && groupNo > (-20 + (page*40)))
                 {
                     if ((bool)GroupCheck(groupNo, path.subject.userID.ToString()))
                     { ButtonColour1 = Colour1; ButtonColour2 = Colour2; } else{ ButtonColour1 =Colour2; ButtonColour2 = Colour1; }
                     
                     elements.Add(new CuiButton
                     { Button = { Color = ButtonColour }, RectTransform = { AnchorMin = $"{RightAMin1} {(AMin2 - ((pos1-20)*3f)/100f)}", AnchorMax = $"{RightAMax1} {(AMax2 - ((pos1-20)*3f)/100f)}" }, Text = { Text = $"{GroupUsersList[groupNo][0]}", FontSize = 10, Align = TextAnchor.MiddleCenter } }, mainName);
         
                     elements.Add(new CuiButton
                     { Button = { Command = $"GroupAddRemove add {GroupUsersList[groupNo][0]} {page}", Color = ButtonColour1 }, RectTransform = { AnchorMin = $"{RightBMin1} {(AMin2 - ((pos1-20)*3f)/100f)}", AnchorMax = $"{RightBMax1} {(AMax2 - ((pos1-20)*3f)/100f)}" }, Text = { Text = lang.GetMessage("Granted", this), FontSize = 10, Align = TextAnchor.MiddleCenter } }, mainName);
                     
                     elements.Add(new CuiButton
                     { Button = { Command = $"GroupAddRemove remove {GroupUsersList[groupNo][0]} {page}", Color = ButtonColour2 }, RectTransform = { AnchorMin = $"{RightCMin1} {(AMin2 - ((pos1-20)*3f)/100f)}", AnchorMax = $"{RightCMax1} {(AMax2 - ((pos1-20)*3f)/100f)}" }, Text = { Text = lang.GetMessage("Revoked", this), FontSize = 10, Align = TextAnchor.MiddleCenter } }, mainName);
                 }               
            }
            elements.Add (new CuiButton { Button = { Command = $"Navigate false {1}", Color = ButtonColour }, RectTransform = { AnchorMin = "0.4 0.02", AnchorMax = "0.6 0.06" }, Text = { Text = lang.GetMessage("Back", this), FontSize = 14, Align = TextAnchor.MiddleCenter} }, mainName);
            elements.Add(new CuiLabel { Text = { Text = outmsg, FontSize = 16, Align = TextAnchor.MiddleCenter }, RectTransform = { AnchorMin = "0 0.9", AnchorMax = "1 0.98" } }, mainName);
            
            if (groupTotal > (page*40)) elements.Add(new CuiButton
            { Button = { Command = $"Groups {next}", Color = ButtonColour }, RectTransform = { AnchorMin = "0.7 0.02", AnchorMax = "0.8 0.06" }, Text = { Text = lang.GetMessage("->", this), FontSize = 14, Align = TextAnchor.MiddleCenter} }, mainName);
            if (page > 1) elements.Add(new CuiButton
            { Button = { Command = $"Groups {previous}", Color = ButtonColour }, RectTransform = { AnchorMin = "0.2 0.02", AnchorMax = "0.3 0.06" }, Text = { Text = lang.GetMessage("<-", this), FontSize = 14, Align = TextAnchor.MiddleCenter} }, mainName);
                       
            CuiHelper.AddUi(player, elements);
        }

        #region console commands
        [ConsoleCommand("EmptyGroup")]
        private void EmptyGroup(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            
            var elements1 = new CuiElementContainer();
            var mainName1 = elements1.Add(new CuiPanel
            {
                Image = { Color = String.Format("0.1 0.1 0.1 1", guitransparency) },

                RectTransform = { AnchorMin = "0.4 0.42", AnchorMax = "0.6 0.48" },
                CursorEnabled = true
            }, "Overlay", "MenuGUI1");

            elements1.Add(new CuiElement
            {
                Parent = "MenuGUI1",
                Components =
                {
                new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });

            elements1.Add(new CuiButton { Button = { Command = $"Empty true", Color = ButtonColour }, RectTransform = { AnchorMin = "0.1 0.2", AnchorMax = "0.4 0.8" }, Text = { Text = lang.GetMessage("confirm", this), FontSize = 14, Align = TextAnchor.MiddleCenter}, }, mainName1);
            
            elements1.Add(new CuiButton { Button = { Command = $"Empty false", Color = ButtonColour }, RectTransform = { AnchorMin = "0.6 0.2", AnchorMax = "0.9 0.8" }, Text = { Text = lang.GetMessage("cancel", this), FontSize = 14, Align = TextAnchor.MiddleCenter}, }, mainName1);
            
            CuiHelper.AddUi(player, elements1);
 
        }
        
        [ConsoleCommand("Empty")]
        private void Empty(ConsoleSystem.Arg arg, string confirm)
        {
            var player = arg.Connection.player as BasePlayer;
            var path = ActiveAdmins[player.userID];
            string confirmation = arg.Args[0];
            if (confirmation == "true")
            {
                int count = 0;
                foreach (var user in permission.GetUsersInGroup(path.subjectGroup))
                {
                    count++;
                    string str = user.Substring(0,17);
                    permission.RemoveUserGroup(str, path.subjectGroup);
                    GetGroups(player);
                    CuiHelper.DestroyUi(player, "MenuGUI");
                    CuiHelper.DestroyUi(player, "MenuGUI1");
                    var argsOut = new string[] { "group", path.subjectGroup};
                    cmdPerms(player, null, argsOut);
                }
                if (count == 0)
                {
                    CuiHelper.DestroyUi(player, "MenuGUI1");
                }
            }
            else
            {
                CuiHelper.DestroyUi(player, "MenuGUI1");
            }           
        }
        
        [ConsoleCommand("GroupAddRemove")]
        private void GroupAddRemove(ConsoleSystem.Arg arg, string action, string group, int page)
        {
            var player = arg.Connection.player as BasePlayer;
            var path = ActiveAdmins[player.userID];
            if (player == null || arg.Args == null || arg.Args.Length < 3) return;
            string Pname = path.subject.userID.ToString();
            string userGroup = arg.Args[1];
            page = Convert.ToInt32(arg.Args[2]);
            if (arg.Args[0] == "add")
            permission.AddUserGroup(Pname, userGroup);
            if (arg.Args[0] == "remove")
            permission.RemoveUserGroup(Pname, userGroup);
            GetGroups(player);
            CuiHelper.DestroyUi(player, "MenuGUI");
            GroupsUI(player, $"{path.subject.displayName}", page);
        }

        [ConsoleCommand("Groups")]
        private void GroupsPM(ConsoleSystem.Arg arg, int page)
        {
            var player = arg.Connection.player as BasePlayer;
            var path = ActiveAdmins[player.userID];
            if (player == null || arg.Args == null || arg.Args.Length < 1) return;
            page = Convert.ToInt32(arg.Args[0]);
            CuiHelper.DestroyUi(player, "MenuGUI");
            GroupsUI(player, $"{path.subject.displayName}", page);
        }

        [ConsoleCommand("ClosePM")]
        private void ClosePM(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null) return;
            CuiHelper.DestroyUi(player, "MenuGUI");
            MenuOpen.Remove(player.userID);
            return;
        }

        [ConsoleCommand("Navigate")]
        private void Navigate(ConsoleSystem.Arg arg, string group, int page)
        {
            var player = arg.Connection.player as BasePlayer;
            var path = ActiveAdmins[player.userID];
            if (player == null || arg.Args == null || arg.Args.Length < 2) return;
            ActiveAdmins[player.userID].previousPage = Convert.ToInt32(arg.Args[1]);
            CuiHelper.DestroyUi(player, "MenuGUI");
            string[] argsOut = new string[]{};
            if (arg.Args[0] == "true")
            {
            argsOut = new string[] { "group", path.subjectGroup, path.previousPage.ToString()};
            cmdPerms(player, null, argsOut);
            }
            else
            {
            argsOut = new string[] { "player", path.subject.displayName, path.previousPage.ToString()};
            cmdPerms(player, null, argsOut);
            }
            return;
        }

        [ConsoleCommand("permsList")]
        private void permsList(ConsoleSystem.Arg arg, int plugNumber, string actiontype, string Perm, string isGroup, string all, int page)
        {
            var player = arg.Connection.player as BasePlayer;
            var path = ActiveAdmins[player.userID];
            if (player == null || arg.Args == null || arg.Args.Length < 6) return;
            int pageNo = Convert.ToInt32(arg.Args[5]);
            string Pname;
            string group = arg.Args[3];
            if (arg.Args[4] == "all")
            {
                if (arg.Args[2] != null)
                {
                    Pname = path.subject?.userID.ToString();
                    string action = arg.Args[1];
                    string PermInHand = arg.Args[2];
                    foreach (var perm in numberedPerms)
                    {
                        if (AllPerPage == true && perm.Key > (pageNo*20)-20 && perm.Key < ((pageNo*20)+1)) //All = page or All = all?! config option?
                        {
                            if (action == "grant" && group == "false")
                            permission.GrantUserPermission(Pname, perm.Value, null);
                            if (action == "revoke" && group == "false")
                            permission.RevokeUserPermission(Pname, perm.Value);
                            if (action == "grant" && group == "true")
                            permission.GrantGroupPermission(path.subjectGroup, perm.Value, null);
                            if (action == "revoke" && group == "true")
                            permission.RevokeGroupPermission(path.subjectGroup, perm.Value);
                        }
                        if (AllPerPage == false)
                        {
                            if (action == "grant" && group == "false")
                            permission.GrantUserPermission(Pname, perm.Value, null);
                            if (action == "revoke" && group == "false")
                            permission.RevokeUserPermission(Pname, perm.Value);
                            if (action == "grant" && group == "true")
                            permission.GrantGroupPermission(path.subjectGroup, perm.Value, null);
                            if (action == "revoke" && group == "true")
                            permission.RevokeGroupPermission(path.subjectGroup, perm.Value);
                        }
                    }
                }
            }
            else
            {
                Pname = path.subject?.userID.ToString();
                string action = arg.Args[1];
                string PermInHand = arg.Args[2];
                if (arg.Args[2] != null)
                {
                    if (action == "grant" && group == "false")
                    permission.GrantUserPermission(Pname, PermInHand, null);
                    if (action == "revoke" && group == "false")
                    permission.RevokeUserPermission(Pname, PermInHand);
                    if (action == "grant" && group == "true")
                    permission.GrantGroupPermission(path.subjectGroup, PermInHand, null);
                    if (action == "revoke" && group == "true")
                    permission.RevokeGroupPermission(path.subjectGroup, PermInHand);  
                }
            }

            plugNumber = Convert.ToInt32(arg.Args[0]);
            string plugName = "";
            foreach (var key in PlugList)
            {
                if (key.Key == plugNumber)
                plugName = key.Value;
            }

            numberedPerms.Clear();
            int numOfPerms = 0;
            foreach (var perm in permission.GetPermissions())
            {
                if (perm.Contains($"{plugName}."))
                {
                    numOfPerms++;
                    numberedPerms.Add(numOfPerms, perm);
                }
            }
            CuiHelper.DestroyUi(player, "MenuGUI");
            if (group == "false")
            PermsUI(player, $"{path.subject.displayName} - {plugName}", plugNumber, null, group, pageNo);
            else
            PermsUI(player, $"{path.subjectGroup} - {plugName}", plugNumber, null, group, pageNo);
            return;
        }
        #endregion

        #region chat commands

        [ChatCommand("perms")]
        void cmdPerms(BasePlayer player, string command, string[] args)
        {
        if (ActiveAdmins.ContainsKey(player.userID))
        ActiveAdmins.Remove(player.userID);
        ActiveAdmins.Add(player.userID, new info());
        var path = ActiveAdmins[player.userID];
        GetPlugs(player);
        GetGroups(player);
            
            int page = 1;
            if (args.Length == 3) 
            page = Convert.ToInt32(args[2]);
            if (isAuth(player))
            {
                if (args == null || args.Length < 2)
                {
                SendReply(player, TitleColour + lang.GetMessage("title", this) + "</color>" + MessageColour + lang.GetMessage("Syntax", this) + "</color>");
                return;
                }
                args[1] = args[1].ToLower();
                if (args[0] == "player")
                {
                    path.subject = FindPlayerByName(args[1]);
                    if (path.subject == null)
                    {
                    SendReply(player, TitleColour + lang.GetMessage("title", this) + "</color>" + MessageColour + lang.GetMessage("NoPlayer", this) + "</color>", args[1]);
                    return;
                    }
                    string msg = string.Format(lang.GetMessage("GUIName", this), path.subject.displayName);

                        if (MenuOpen.Contains(player.userID))
                        {
                            MenuOpen.Remove(player.userID);
                            CuiHelper.DestroyUi(player, "MenuGUI");
                        }
                        MainUI(player, msg.ToString(), "false", page);
                        MenuOpen.Add(player.userID);
                        return;
                }
                if (args[0] == "group")
                {
                    List<string> Groups = new List<string>();
                    foreach (var group in permission.GetGroups())
                        {
                           Groups.Add(group);
                        }
                    if (Groups.Contains($"{args[1]}")) 
                    {
                    string msg = string.Format(lang.GetMessage("GUIName", this), args[1]);
                    
                        ActiveAdmins[player.userID].subjectGroup = args[1];
                        if (MenuOpen.Contains(player.userID))
                        {
                            MenuOpen.Remove(player.userID);
                            CuiHelper.DestroyUi(player, "MenuGUI");
                        }
                        MainUI(player, msg.ToString(), "true", page);
                        MenuOpen.Add(player.userID);
                        return;
                    }
                    else
                    SendReply(player, TitleColour + lang.GetMessage("title", this) + "</color>" + MessageColour + lang.GetMessage("NoGroup", this) + "</color>", args[1]);
                }
            }
            else
            SendReply(player, TitleColour + lang.GetMessage("title", this) + "</color>" + MessageColour + lang.GetMessage("NotAdmin", this) + "</color>", args[1]);
        }

        static BasePlayer FindPlayerByName(string name)
        {
            BasePlayer result = null;
            foreach (BasePlayer current in BasePlayer.activePlayerList)
            {
                if (current.displayName.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    BasePlayer result2 = current;
                    return result2;
                }
                if (current.UserIDString.Contains(name, CompareOptions.OrdinalIgnoreCase))
                {
                    BasePlayer result2 = current;
                    return result2;
                }
                if (current.displayName.Contains(name, CompareOptions.OrdinalIgnoreCase))
                {
                    result = current;
                }
            }
            return result;
        }
        #endregion

        #region config
        static string TitleColour = "<color=orange>";
        static string MessageColour = "<color=white>";
        static double guitransparency = 0.5;
        static string BlockList = "playerranks,botspawn";
        string ButtonColour = "0.7 0.32 0.17 1";
        string Colour1 = "0.7 0.32 0.17 1";
        string Colour2 = "0.2 0.2 0.2 1";
        bool AllPerPage = false;

        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }

        private void LoadConfigVariables()
        {
            CheckCfg("Options - GUI Transparency 0-1", ref guitransparency);
            CheckCfg("Options - Plugin BlockList", ref BlockList);
            CheckCfg("Chat - Title colour", ref TitleColour);
            CheckCfg("Chat - Message colour", ref MessageColour);
            CheckCfg("GUI - Label colour", ref ButtonColour);
            CheckCfg("GUI - All = per page", ref AllPerPage);
            CheckCfg("GUI - On colour", ref Colour1);
            CheckCfg("GUI - Off colour", ref Colour2); 
        }

        private void CheckCfg<T>(string Key, ref T var)
        {
            if (Config[Key] is T)
                var = (T)Config[Key];
            else
                Config[Key] = var;
        }
        #endregion

        #region messages
        Dictionary<string, string> messages = new Dictionary<string, string>()
        {
            {"title", "Permissions Manager: " },
            {"NoGroup", "Group {0} was not found." },
            {"NoPlayer", "Player {0} was not found." },
            {"Syntax", "/perms player PlayerName or /perms group GroupName" },
            {"GUIAll", "All" },
            {"GUINone", "None" },
            {"GUIBack", "Back" },
            {"GUIClose", "Close" },
            {"GUIGroups", "Groups" },
            {"GUIGranted", "Granted" },
            {"GUIRevoked", "Revoked" },
            {"GUIName", "Permissions for {0}" },
            {"GUIGroupsFor", "Groups for {0}"},
            {"removePlayers", "Remove All Players"},
            {"confirm", "Confirm"},
            {"cancel", "Cancel"},
            {"NotAdmin", "This command is reserved for admin."}
            
        };
        #endregion
    }
}