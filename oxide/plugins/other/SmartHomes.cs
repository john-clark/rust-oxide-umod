using System.Collections.Generic;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using System.Linq;
using System.Reflection;
using Facepunch;
using System;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;
 
namespace Oxide.Plugins
{
    [Info("Smart Homes", "DylanSMR", 0.5)]
    [Description("A pretty cool plugin ;)")]
 
    class SmartHomes : RustPlugin
    {
        static int constructionColl = UnityEngine.LayerMask.GetMask(new string[] { "Construction", "Deployable", "Prevent Building", "Deployed" });
        static FieldInfo serverinput;
        public InputState inputState;

        Dictionary<string, string> messages = new Dictionary<string, string>(){
            {"TitleMenu","Smart Menu"},
            {"HomeBtn","Home Menu"},
            {"ObjectBtn", "Object Menu"},
            {"SetupBtn","Setup Menu"},
            {"CloseBtn","Close Menu"},
            {"created", "<color=#1586db>Smart Home Created!!!</color>"},
            {"createhome", "Create S-Home"},
            {"createfurnace","Create Furnace"},
            {"createdoor","Create Door"},
            {"createlight","Create Light"},
            {"createturret","Create Turret"},
            {"awaitname","Please type the name you wish to grant your object."},
            {"createdobject","You have created a new object! Please check your object menu ;)"},
            {"alreadycreated","That object has already been added ;)"},
            {"notafurnace","The object you have tried to add is not of type [Furnace]."},
            {"notadoor","The object you have tried to add is not of type [Door]."},
            {"notinrange","You are not within range of your smart home!"},
            {"notaturret","The object you have tried to add is not of type [Turret]"},
            {"notalight","The object you have tried to add is not of type [Light]"},
            {"furnacelist","Your Furnaces"},
            {"lightlist","Your Lights"},
            {"doorlist","Your Doors"},
            {"turretlist","Your Turrets"},
            {"invalidobject","You have pointed at no object? Please try aiming a object you wish to create!"},
            {"objdestoryed","One of your objects has been destroyed!"},
            {"cannotcreatehome","You cannot create a home here!"},
            {"noperm","You do not have permission to view this page!"},
            {"newhome","Create New Home"},
            {"OpenAll","Open All Doors"},
            {"CloseAll","Close All Doors"},
            {"TurnAllOff","Turn Off Turrets"},
            {"TurnOnAll","Turn On Turrets"},
            {"TurnOffAllF","Turn Off Furnaces"},
            {"TurnOnAllF","Turn On Furnaces"},
            {"TurnLightsOn","Turn On Lights"},
            {"TurnLightsOff","Turn Off Lights"},
        };

        private string msg(string msg, BasePlayer player){
            return lang.GetMessage(msg, this, player.UserIDString);
        }

        static string MainGUIVar = "SmartGUI";
        static string MainNotVar = "fk9a80sfues52893nf";
        static List<BasePlayer> openedUI = new List<BasePlayer>();

        void Unload(){
            foreach(BasePlayer player in BasePlayer.activePlayerList){
                SmartManager.DestroyUI(player);
            }    
            Interface.Oxide.DataFileSystem.WriteObject("SmartData", smartData);  
        }

        void Loaded(){
            LoadVariables();
            serverinput = typeof(BasePlayer).GetField("serverInput", (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));
            lang.RegisterMessages(messages, this);
            smartData = Interface.Oxide.DataFileSystem.ReadObject<SmartData>("SmartData");
            Interface.Oxide.DataFileSystem.WriteObject("SmartData", smartData);  
            LoadAllSmart();
            permission.RegisterPermission(configData.Variables.turretPagePerm, this);
            permission.RegisterPermission(configData.Variables.furnacePagePerm, this);
            permission.RegisterPermission(configData.Variables.lightPagePerm, this);
            permission.RegisterPermission(configData.Variables.doorPagePerm, this);
        }

        void OnServerSave(){
            Interface.Oxide.DataFileSystem.WriteObject("SmartData", smartData);  
        }

        #region Main
        
            static ConfigData configData;
            class Variables
            {
                public int ActivationDistance {get; set;}
                public string turretPagePerm {get; set;}
                public string furnacePagePerm {get; set;}
                public string lightPagePerm {get; set;}
                public string doorPagePerm {get; set;}
            }
            class ConfigData
            {          
                public Variables Variables { get; set; }     
            }
            private void LoadVariables()
            {
                LoadConfigVariables();
                SaveConfig();            
            }
            private void LoadConfigVariables()
            {
                configData = Config.ReadObject<ConfigData>();
            }
            protected override void LoadDefaultConfig()
            {
                Puts("Creating a new config file");
                ConfigData config = new ConfigData
                {
                    Variables = new Variables
                    {
                        ActivationDistance = 30,
                        turretPagePerm = "smarthomes.turret",
                        furnacePagePerm = "smarthomes.furnace",
                        lightPagePerm = "smarthomes.light",
                        doorPagePerm = "smarthomes.door",
                    },
                };
                SaveConfig(config);            
            }
            void SaveConfig(ConfigData config)
            {
                Config.WriteObject(config, true);
            }

            public Dictionary<Vector3, BaseEntity> ents = new Dictionary<Vector3, BaseEntity>();

            void LoadAllSmart(){
                var i = 0;
                var time = DateTime.Now;
                if(smartData == null) return;
                if(smartData.data == null) return;
                if(smartData.data.Count == 0) return;
                PrintWarning("Loading all smart home entities/objects");
                foreach(var d in smartData.data){
                    if(smartData.data[d.Key].turrets.Count >= 1){
                        foreach(var s in smartData.data[d.Key].turrets){
                            var ss = smartData.data[d.Key].turrets[s.Key];
                            List<BaseEntity> x = new List<BaseEntity>();
                            Vis.Entities(new Vector3(ss.x, ss.y, ss.z), 0.6f, x);
                            foreach(var z in x){
                                if(z.ToString().Contains("turret")){
                                    if(!ents.ContainsKey(z.transform.position)) ents.Add(z.transform.position, z); i++;
                                    break;
                                }
                            }
                        }
                    }
                    if(smartData.data[d.Key].doors.Count >= 1){
                        foreach(var s in smartData.data[d.Key].doors){
                            var ss = smartData.data[d.Key].doors[s.Key];
                            List<BaseEntity> x = new List<BaseEntity>();
                            Vis.Entities(new Vector3(ss.x, ss.y, ss.z), 0.6f, x);
                            foreach(var z in x){
                                if(z.ToString().Contains("hing") || z.ToString().Contains("hatch")){
                                    if(!ents.ContainsKey(z.transform.position)) ents.Add(z.transform.position, z); i++;
                                    break;
                                }
                            }
                        }
                    }
                    if(smartData.data[d.Key].furnaces.Count >= 1){
                        foreach(var s in smartData.data[d.Key].furnaces){
                            var ss = smartData.data[d.Key].furnaces[s.Key];
                            List<BaseEntity> x = new List<BaseEntity>();
                            Vis.Entities(new Vector3(ss.x, ss.y, ss.z), 0.6f, x);
                            foreach(var z in x){
                                if(z.ToString().Contains("furnace") || z.ToString().Contains("camp")){
                                    if(!ents.ContainsKey(z.transform.position)) ents.Add(z.transform.position, z); i++;
                                    break;
                                }
                            }
                        }
                    } 
                    if(smartData.data[d.Key].lights.Count >= 1){
                        foreach(var s in smartData.data[d.Key].lights){
                            var ss = smartData.data[d.Key].lights[s.Key];
                            List<BaseEntity> x = new List<BaseEntity>();
                            Vis.Entities(new Vector3(ss.x, ss.y, ss.z), 0.6f, x);
                            foreach(var z in x){
                                if(z.ToString().Contains("lantern") || z.ToString().Contains("tuna") || z.ToString().Contains("ceiling")){
                                    if(!ents.ContainsKey(z.transform.position)) ents.Add(z.transform.position, z); i++;
                                    break;
                                }
                            }
                        }
                    }      
                }
                var now = DateTime.Now;
                System.TimeSpan timer = now - time;
                PrintWarning("Entities loaded : Total Amount {0} : Time Taken {1}", i, timer);
            }

            SmartData smartData;
            public class SmartData{
                public Dictionary<ulong, SmartHome> data = new Dictionary<ulong, SmartHome>();
            }

            public class SmartHome{
                public bool hashome;
                public float x;
                public float y;
                public float z;
                public Dictionary<int, SmartTurret>    turrets    = new Dictionary<int, SmartTurret>();
                public Dictionary<int, SmartLight>     lights     = new Dictionary<int, SmartLight>();
                public Dictionary<int, SmartFurnace>   furnaces   = new Dictionary<int, SmartFurnace>();
                public Dictionary<int, SmartDoor>      doors      = new Dictionary<int, SmartDoor>();

                public Vector3 GetPosition(){
                    return new Vector3(x, y, z);
                }
            }

            public class SmartTurret{
                public string name;
                public float x;
                public float y;
                public float z;
            }

            public class SmartDoor{
                public string name;
                public float x;
                public float y;
                public float z;
            }

            public class SmartLight{
                public string name;
                public float x;
                public float y;
                public float z;
            }

            public class SmartFurnace{
                public string name;
                public float x;
                public float y;
                public float z;
            }

			public Vector3 GetVector(float x, float y, float z) => new Vector3(x, y, z);
			
			
        #endregion 


        #region MainElement

            static BaseEntity GetEntity(Ray ray, float distance)
            {
                RaycastHit hit;
                if (!UnityEngine.Physics.Raycast(ray, out hit, distance, constructionColl))
                    return null;
                return hit.GetEntity();
            }

            bool InRange(BasePlayer player){
                List<BaseEntity> entities = new List<BaseEntity>();
                var d = smartData.data[player.userID];
                Vis.Entities(new Vector3(d.x, d.y, d.z), configData.Variables.ActivationDistance, entities);
                if(entities.Contains(player)){
                    return true;
                }
                return false;
            }

            [ConsoleCommand("SUI_OpenElement")]
            private void SwitchElement(ConsoleSystem.Arg arg){
                var d = smartData.data[arg.Player().userID];
                var player = arg.Player();
                switch(arg.Args[0]){
                    case "Close":
                        SmartManager.DestroyUI(player);
                    break;
                    case "Home":
                        CreateHomeMenu(player);
                    break;
                    case "Create":
                        CreateHome(player);
                    break;
                    case "CreateNew":
                        CreateHome(player);
                    break;
                    case "Setup":
                        CreateSetup(player);
                    break;
                    case "Object":
                        CreateObjects(player);
                    break;
                    case "showfurnaces":
                        ShowFurnaceMenu(player);
                    break;
                    case "showdoors":
                        ShowDoorMenu(player);
                    break;
                    case "showlights":
                        ShowLightMenu(player);
                    break;
                    case "showturrets":
                        ShowTurretMenu(player);
                    break;
                    case "createfurnace":
                        inputState = serverinput.GetValue(player) as InputState;
                        Ray ray = new Ray(player.eyes.position, Quaternion.Euler(inputState.current.aimAngles) * Vector3.forward);
                        BaseEntity entity = GetEntity(ray, 3f);
                        if(entity == null){
                            SendReply(player, msg("invalidobject", player));
                            return;                
                        }
                        if(!InRange(player)){
                            SendReply(player, msg("notinrange", player));
                            return;
                        }
                        if(!entity.ToString().Contains("furnace") && !entity.ToString().Contains("camp")){
                            SendReply(player, msg("notafurnace", player));
                            return;
                        }
                        if(ents.ContainsKey(entity.transform.position)){
                            SendReply(player, msg("alreadycreated", player));
                            return;
                        }
                        var n = new SmartFurnace(){
                            x = entity.transform.position.x,
                            y = entity.transform.position.y,
                            z = entity.transform.position.z,
                            name = "",
                        };
                        awaitingNamef.Add(player.userID, n);
                        ents.Add(new Vector3(n.x, n.y, n.z), entity);
                        SendReply(player, msg("awaitname", player));
                        SmartManager.DestroyUI(player);
                    break;
                    case "createdoor":
                        inputState = serverinput.GetValue(player) as InputState;
                        Ray ray2 = new Ray(player.eyes.position, Quaternion.Euler(inputState.current.aimAngles) * Vector3.forward);
                        BaseEntity entity2 = GetEntity(ray2, 3f);
                        if(entity2 == null){
                            SendReply(player, msg("invalidobject", player));
                            return;                
                        }
                        if(!InRange(player)){
                            SendReply(player, msg("notinrange", player));
                            return;
                        }
                        if(!entity2.ToString().Contains("hing") && !entity2.ToString().Contains("hatch")){
                            SendReply(player, msg("notadoor", player));
                            return;
                        }
                        if(ents.ContainsKey(entity2.transform.position)){
                            SendReply(player, msg("alreadycreated", player));
                            return;
                        }
                        var f = new SmartDoor(){
                            x = entity2.transform.position.x,
                            y = entity2.transform.position.y,
                            z = entity2.transform.position.z,
                            name = "",
                        };
                        awaitingNamed.Add(player.userID, f);
                        ents.Add(new Vector3(f.x, f.y, f.z), entity2);
                        SendReply(player, msg("awaitname", player));
                        SmartManager.DestroyUI(player);
                    break;
                    case "createturret":
                        inputState = serverinput.GetValue(player) as InputState;
                        Ray ray3 = new Ray(player.eyes.position, Quaternion.Euler(inputState.current.aimAngles) * Vector3.forward);
                        BaseEntity entity3 = GetEntity(ray3, 3f);
                        if(entity3 == null){
                            SendReply(player, msg("invalidobject", player));
                            return;                
                        }     
                        if(!InRange(player)){
                            SendReply(player, msg("notinrange", player));
                            return;
                        }
                        if(!entity3.ToString().Contains("turret")){
                            SendReply(player, msg("notaturret", player));
                            return;
                        }
                        if(ents.ContainsKey(entity3.transform.position)){
                            SendReply(player, msg("alreadycreated", player));
                            return;
                        }
                        var g = new SmartTurret(){
                            x = entity3.transform.position.x,
                            y = entity3.transform.position.y,
                            z = entity3.transform.position.z,
                            name = "",
                        };
                        awaitingNamet.Add(player.userID, g);
                        ents.Add(new Vector3(g.x, g.y, g.z), entity3);
                        SendReply(player, msg("awaitname", player));
                        SmartManager.DestroyUI(player);
                    break; 
                    case "createlight":
                        inputState = serverinput.GetValue(player) as InputState;
                        Ray ray4 = new Ray(player.eyes.position, Quaternion.Euler(inputState.current.aimAngles) * Vector3.forward);
                        BaseEntity entity4 = GetEntity(ray4, 3f);
                        if(entity4 == null){
                            SendReply(player, msg("invalidobject", player));
                            return;                
                        }
                        if(!InRange(player)){
                            SendReply(player, msg("notinrange", player));
                            return;
                        }
                        if(!entity4.ToString().Contains("lantern") && !entity4.ToString().Contains("ceiling") && !entity4.ToString().Contains("tuna")){
                            SendReply(player, msg("notalight", player));
                            return;
                        }
                        if(ents.ContainsKey(entity4.transform.position)){
                            SendReply(player, msg("alreadycreated", player));
                            return;
                        }
                        var r = new SmartLight(){
                            x = entity4.transform.position.x,
                            y = entity4.transform.position.y,
                            z = entity4.transform.position.z,
                            name = "",
                        };
                        awaitingNamel.Add(player.userID, r);
                        ents.Add(new Vector3(r.x, r.y, r.z), entity4);
                        SendReply(player, msg("awaitname", player));
                        SmartManager.DestroyUI(player);
                    break;  
                    case "togglefurnace":
                        var fid = Convert.ToInt32(arg.Args[1]);
                        var fidd = d.furnaces[fid];
                        var furn = ents[new Vector3(fidd.x, fidd.y, fidd.z)] as BaseOven;
                        if(!furn.IsOn()){
                            furn.CancelInvoke("Cook");
                            furn.InvokeRepeating("Cook", 0.5f, 0.5f);
                            furn.SetFlag(BaseEntity.Flags.On, true);
                        }else{
                            furn.CancelInvoke("Cook");
                            furn.SetFlag(BaseEntity.Flags.On, false);
                        }
                        ShowFurnaceMenu(player);
                    break;
                    case "toggledoor":
                        var did = Convert.ToInt32(arg.Args[1]);
                        var didd = d.doors[did];
                        var door = ents[new Vector3(didd.x, didd.y, didd.z)];
                        if(!door.IsOpen()){
                            door.SetFlag(BaseEntity.Flags.Open, true);
                            door.SendNetworkUpdateImmediate();
                        }else{
                            door.SetFlag(BaseEntity.Flags.Open, false);
                            door.SendNetworkUpdateImmediate();
                        }
                        ShowDoorMenu(player);
                    break;  
                    case "togglelight":
                        var lid = Convert.ToInt32(arg.Args[1]);
                        var lidd = d.lights[lid];
                        var light = ents[new Vector3(lidd.x, lidd.y, lidd.z)];
                        if(!light.IsOn()){
                            light.SetFlag(BaseEntity.Flags.On, true);
                        }else{
                            light.SetFlag(BaseEntity.Flags.On, false);
                        }
                        ShowLightMenu(player);
                    break; 
                    case "toggleturret":
                        var tid = Convert.ToInt32(arg.Args[1]);
                        var tidd = d.turrets[tid];
                        var turret = ents[new Vector3(tidd.x, tidd.y, tidd.z)];
                        if(!turret.IsOn()){
                            turret.SetFlag(BaseEntity.Flags.On, true);
                            turret.GetComponent<AutoTurret>().target = null;
                            turret.SendNetworkUpdateImmediate();
                        }else{
                            turret.SetFlag(BaseEntity.Flags.On, false);
                            turret.GetComponent<AutoTurret>().target = null;
                            turret.SendNetworkUpdateImmediate();
                        }
                        ShowTurretMenu(player);
                    break;  
                    case "ToggleAllLights":
                        switch(arg.Args[1]){
                            case "1":
                                foreach(var da5 in d.lights){
                                    var didd11 = d.lights[da5.Key];
                                    var door11 = ents[new Vector3(didd11.x, didd11.y, didd11.z)];
                                    door11.SetFlag(BaseEntity.Flags.On, true);
                                }
                            break;
                            case "2":
                                foreach(var da6 in d.lights){
                                    var didd99 = d.lights[da6.Key];
                                    var door99 = ents[new Vector3(didd99.x, didd99.y, didd99.z)];
                                    door99.SetFlag(BaseEntity.Flags.On, false);
                                }
                            break;
                        }
                    break;
                    case "ToggleAllTurrets":
                        switch(arg.Args[1]){
                            case "1":
                                foreach(var da3 in d.turrets){
                                    var didd4 = d.turrets[da3.Key];
                                    var door4 = ents[new Vector3(didd4.x, didd4.y, didd4.z)];
                                    door4.SetFlag(BaseEntity.Flags.On, true);
                                    door4.GetComponent<AutoTurret>().target = null;
                                    door4.SendNetworkUpdateImmediate();
                                }
                            break;
                            case "2":
                                foreach(var da4 in d.turrets){
                                    var didd15 = d.turrets[da4.Key];
                                    var door15 = ents[new Vector3(didd15.x, didd15.y, didd15.z)];
                                    door15.SetFlag(BaseEntity.Flags.On, false);
                                    door15.GetComponent<AutoTurret>().target = null;
                                    door15.SendNetworkUpdateImmediate();
                                }
                            break;
                        }
                    break;
                    case "ToggleAllFurnaces":
                        switch(arg.Args[1]){
                            case "1":
                                foreach(var da2 in d.furnaces){
                                    var didd3 = d.furnaces[da2.Key];
                                    var door3 = ents[new Vector3(didd3.x, didd3.y, didd3.z)];
                                    door3.CancelInvoke("Cook");
                                    door3.InvokeRepeating("Cook", 0.5f, 0.5f);
                                    door3.SetFlag(BaseEntity.Flags.On, true);
                                }
                            break;
                            case "2":
                                foreach(var da3 in d.doors){
                                    var didd13 = d.furnaces[da3.Key];
                                    var door13 = ents[new Vector3(didd13.x, didd13.y, didd13.z)];
                                    door13.CancelInvoke("Cook");
                                    door13.SetFlag(BaseEntity.Flags.On, false);
                                }
                            break;
                        }
                    break;
                    case "ToggleAllDoors":
                        switch(arg.Args[1]){
                            case "1":
                                foreach(var da in d.doors){
                                    var didd1 = d.doors[da.Key];
                                    var door1 = ents[new Vector3(didd1.x, didd1.y, didd1.z)];
                                    door1.SetFlag(BaseEntity.Flags.Open, true);
                                    door1.SendNetworkUpdateImmediate();
                                }
                            break;
                            case "2":
                                foreach(var da1 in d.doors){
                                    var didd12 = d.doors[da1.Key];
                                    var door12 = ents[new Vector3(didd12.x, didd12.y, didd12.z)];
                                    door12.SetFlag(BaseEntity.Flags.Open, false);
                                    door12.SendNetworkUpdateImmediate();
                                }
                            break;
                        }
                    break;
                    case "nop":
                        SendReply(player, msg("noperm", player));
                        return;
                    break;
                }
            }

            Dictionary<ulong, SmartFurnace> awaitingNamef = new Dictionary<ulong, SmartFurnace>();
            Dictionary<ulong, SmartDoor> awaitingNamed = new Dictionary<ulong, SmartDoor>();
            Dictionary<ulong, SmartTurret> awaitingNamet = new Dictionary<ulong, SmartTurret>();
            Dictionary<ulong, SmartLight> awaitingNamel = new Dictionary<ulong, SmartLight>();

            ulong GetOwner(BaseEntity e){
                var p = e.transform.position;
                foreach(var x in smartData.data){
                    if(e.ToString().Contains("turret")){
                        foreach(var y in smartData.data[x.Key].turrets){
                            var t = smartData.data[x.Key].turrets[y.Key];
                            if(t.x == p.x && t.y == p.y && t.z == p.z){
                                smartData.data[x.Key].turrets.Remove(y.Key);
                                var xt = x.Key;
                                return xt;
                            }
                        }
                    }
                    if(e.ToString().Contains("ceiling") || e.ToString().Contains("tuna") || e.ToString().Contains("lantern")){
                        foreach(var y in smartData.data[x.Key].lights){
                            var t = smartData.data[x.Key].lights[y.Key];
                            if(t.x == p.x && t.y == p.y && t.z == p.z){
                                smartData.data[x.Key].lights.Remove(y.Key);
                                var xt = x.Key;
                                return xt;
                            }                 
                        }
                    }
                    if(e.ToString().Contains("hing") || e.ToString().Contains("hatch")){
                        foreach(var y in smartData.data[x.Key].doors){
                            var t = smartData.data[x.Key].doors[y.Key];
                            if(t.x == p.x && t.y == p.y && t.z == p.z){
                                smartData.data[x.Key].doors.Remove(y.Key);
                                var xt = x.Key;
                                return xt;
                            }                 
                        }
                    }  
                    if(e.ToString().Contains("furnace") || e.ToString().Contains("camp")){
                        foreach(var y in smartData.data[x.Key].furnaces){
                            var t = smartData.data[x.Key].furnaces[y.Key];
                            if(t.x == p.x && t.y == p.y && t.z == p.z){
                                smartData.data[x.Key].furnaces.Remove(y.Key);
                                var xt = x.Key;
                                return xt;
                            }                 
                        }
                    }      
                }
                return 0;
            }

            void OnEntityDeath(BaseEntity entity){
                if(!ents.ContainsValue(entity)) return;
                var owner = GetOwner(entity);
                if(owner == 0) return;
                if(BasePlayer.FindByID(owner) == null){
                    return;
                }
                BasePlayer player = BasePlayer.FindByID(owner);
                SendReply(player, msg("objdestoryed", player));
                ents.Remove(entity.transform.position);
            }

            object OnPlayerChat(ConsoleSystem.Arg arg){
                var player = arg.Player();
                var message = arg.Args[0];
                if(awaitingNamef.ContainsKey(player.userID)){
                    SendReply(player, msg("createdobject", player));
                    var n = awaitingNamef[player.userID];
                    n.name = message;
                    var newid = 0;
                    if(smartData.data[player.userID].furnaces.Count == 0){
                        newid = 1;
                    }else{
                        var id = smartData.data[player.userID].furnaces.Last();
                        newid = id.Key + 1;
                    }
                    smartData.data[player.userID].furnaces.Add(newid, n);
                    awaitingNamef.Remove(player.userID);
                    Interface.Oxide.DataFileSystem.WriteObject("SmartData", smartData); 
                    return true;
                }
                if(awaitingNamed.ContainsKey(player.userID)){
                    SendReply(player, msg("createdobject", player));
                    var n = awaitingNamed[player.userID];
                    n.name = message;
                    var newid = 0;
                    if(smartData.data[player.userID].doors.Count == 0){
                        newid = 1;
                    }else{
                        var id = smartData.data[player.userID].doors.Last();
                        newid = id.Key + 1;
                    }
                    smartData.data[player.userID].doors.Add(newid, n);
                    awaitingNamed.Remove(player.userID);
                    Interface.Oxide.DataFileSystem.WriteObject("SmartData", smartData); 
                    return true;
                }
                if(awaitingNamet.ContainsKey(player.userID)){
                    SendReply(player, msg("createdobject", player));
                    var n = awaitingNamet[player.userID];
                    n.name = message;
                    var newid = 0;
                    if(smartData.data[player.userID].turrets.Count == 0){
                        newid = 1;
                    }else{
                        var id = smartData.data[player.userID].turrets.Last();
                        newid = id.Key + 1;
                    }
                    smartData.data[player.userID].turrets.Add(newid, n);
                    awaitingNamet.Remove(player.userID);
                    Interface.Oxide.DataFileSystem.WriteObject("SmartData", smartData); 
                    return true;
                }
                if(awaitingNamel.ContainsKey(player.userID)){
                    SendReply(player, msg("createdobject", player));
                    var n = awaitingNamel[player.userID];
                    n.name = message;
                    var newid = 0;
                    if(smartData.data[player.userID].lights.Count == 0){
                        newid = 1;
                    }else{
                        var id = smartData.data[player.userID].lights.Last();
                        newid = id.Key + 1;
                    }
                    smartData.data[player.userID].lights.Add(newid, n);
                    awaitingNamel.Remove(player.userID);
                    Interface.Oxide.DataFileSystem.WriteObject("SmartData", smartData); 
                    return true;
                }
                return null;   
            }

            void CheckData(BasePlayer player){
                if(smartData.data.ContainsKey(player.userID)) return;
                var n = new SmartHome(){
                    x = 0,
                    y = 0,
                    z = 0,
                    turrets    = new Dictionary<int, SmartTurret>(),
                    lights     = new Dictionary<int, SmartLight>(),
                    furnaces   = new Dictionary<int, SmartFurnace>(),
                    doors      = new Dictionary<int, SmartDoor>(),
                    hashome = false,
                };  
                smartData.data.Add(player.userID, n);
                Interface.Oxide.DataFileSystem.WriteObject("SmartData", smartData); 
            }

        #endregion

        #region UI

            [ChatCommand("rem")]
            void CreateMain(BasePlayer player){  
                CreateHomeMenu(player);
            }

            bool hasAuth(BasePlayer player){
                List<BaseEntity> list = new List<BaseEntity>();
                Vis.Entities(player.transform.position, 1f, list);
                foreach (var cupboard in list)
                {
                    if (!cupboard.ToString().Contains("cupboard")) continue;
                    if (cupboard.GetComponent<BuildingPrivlidge>() != null)
                    {
                        var priv = cupboard.GetComponent<BuildingPrivlidge>();
                        foreach (var ply in priv.authorizedPlayers)
                        {
                            if (ply.userid == player.userID)
                            {
                                return true;
                            }
                            else
                            {
                                continue;
                            }
                        }
                    }
                    continue;
                }
                return false;
            }

            bool canCreateHome(BasePlayer player){
                foreach(var h in smartData.data){
                    var v = new Vector3(smartData.data[h.Key].x, smartData.data[h.Key].y, smartData.data[h.Key].z);
                    List<BaseEntity> entities = new List<BaseEntity>();
                    Vis.Entities(v, configData.Variables.ActivationDistance, entities);
                    if(entities.Contains(player) && !(h.Key == player.userID)){
                        return false;
                    }
                }
                if(!hasAuth(player)) return false;
                return true;
            }

            [ChatCommand("remconfirm")]
            void ConfirmChat(BasePlayer player){
                if(!confirm.Contains(player)) return;
                var t1 = player.transform.position;
                var d = smartData.data[player.userID];
				foreach(var t in d.turrets){
					var p = GetVector(d.turrets[t.Key].x, d.turrets[t.Key].y, d.turrets[t.Key].z);
					if(ents.ContainsKey(p)) ents.Remove(p);
				}
				foreach(var t in d.lights){
					var p = GetVector(d.lights[t.Key].x, d.lights[t.Key].y, d.lights[t.Key].z);
					if(ents.ContainsKey(p)) ents.Remove(p);
				}
				foreach(var t in d.furnaces){
					var p = GetVector(d.furnaces[t.Key].x, d.furnaces[t.Key].y, d.furnaces[t.Key].z);
					if(ents.ContainsKey(p)) ents.Remove(p);
				}
				foreach(var t in d.doors){
					var p = GetVector(d.doors[t.Key].x, d.doors[t.Key].y, d.doors[t.Key].z);
					if(ents.ContainsKey(p)) ents.Remove(p);
				} 
                d.turrets.Clear();
                d.lights.Clear();
                d.furnaces.Clear();
                d.doors.Clear();
                d.x = t1.x;
                d.y = t1.y;
                d.z = t1.z;
                d.hashome = true;  
                CreateHomeMenu(player);
				Interface.Oxide.DataFileSystem.WriteObject("SmartData", smartData);      
            }

            List<BasePlayer> confirm = new List<BasePlayer>();
            void CreateHome(BasePlayer player){
                if(!canCreateHome(player)){
                    SendReply(player, msg("cannotcreatehome", player));
                    return;
                }
                var d = smartData.data[player.userID];
                if(d.hashome){
                    player.SendConsoleCommand("bind l", "chat.say /remconfirm");
                    confirm.Add(player);
                    SmartManager.DestroyUI(player);
                    SendReply(player, "Simply press L if you wish to create a new home. - You have five seconds to preform this action!");
                    timer.Once(5f, () => player.SendConsoleCommand("bind l l"));
                    return;
                }
                var t1 = player.transform.position;
				foreach(var t in d.turrets){
					var p = GetVector(d.turrets[t.Key].x, d.turrets[t.Key].y, d.turrets[t.Key].z);
					if(ents.ContainsKey(p)) ents.Remove(p);
				}
				foreach(var t in d.lights){
					var p = GetVector(d.lights[t.Key].x, d.lights[t.Key].y, d.lights[t.Key].z);
					if(ents.ContainsKey(p)) ents.Remove(p);
				}
				foreach(var t in d.furnaces){
					var p = GetVector(d.furnaces[t.Key].x, d.furnaces[t.Key].y, d.furnaces[t.Key].z);
					if(ents.ContainsKey(p)) ents.Remove(p);
				}
				foreach(var t in d.doors){
					var p = GetVector(d.doors[t.Key].x, d.doors[t.Key].y, d.doors[t.Key].z);
					if(ents.ContainsKey(p)) ents.Remove(p);
				} 
                d.turrets.Clear();
                d.lights.Clear();
                d.furnaces.Clear();
                d.doors.Clear();
                d.x = t1.x;
                d.y = t1.y;
                d.z = t1.z;
                d.hashome = true;  
                CreateHomeMenu(player);
				Interface.Oxide.DataFileSystem.WriteObject("SmartData", smartData);
SendReply(player, msg("created", player));				
            }

            [ConsoleCommand("sh.wipe")]
            void InitWipe(ConsoleSystem.Arg arg){
                if(arg.Player() != null) return;
                smartData.data.Clear();
                Interface.Oxide.DataFileSystem.WriteObject("SmartData", smartData); 
                PrintWarning("Smart Home Data WIPED!!!");
            }

            [ChatCommand("sh_remove")]
            void Player(BasePlayer player){
                foreach(var p in BasePlayer.activePlayerList){
                    if(p == player) continue;
                    var worldEntity = p as BaseEntity;
                    Rigidbody component = worldEntity.GetComponent<Rigidbody>();
                    component.constraints = RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezePositionZ;
                }
            }

            void CreateSetup(BasePlayer player){
                CheckData(player);
                var d = smartData.data[player.userID];
                if(!d.hashome)
                    CreateHomeMenu(player);

                SmartManager.DestroyUI(player);
                openedUI.Add(player);
                var S = SmartManager.AUI.CreateElementContainer(MainGUIVar, UIColors["dark"], "0 0", "1 1", true);
                SmartManager.AUI.CreatePanel(ref S,  MainGUIVar,  UIColors["grey1"],    "0.01 0.92", "0.99 0.99", true);
                SmartManager.AUI.CreateButton(ref S, MainGUIVar,  UIColors["buttonbg"], $"<b><color=#1586db>{msg("HomeBtn", player)}</color></b>", 16, "0.13 0.935", "0.23 0.975", "SUI_OpenElement Home", TextAnchor.MiddleCenter);
                SmartManager.AUI.CreateButton(ref S, MainGUIVar,  UIColors["buttonbg"], $"<b><color=#1586db>{msg("ObjectBtn", player)}</color></b>", 16, "0.26 0.935", "0.36 0.975", "SUI_OpenElement Object", TextAnchor.MiddleCenter);
                SmartManager.AUI.CreateButton(ref S, MainGUIVar,  UIColors["buttonbg"], $"<b><color=red>{msg("SetupBtn", player)}</color></b>", 16, "0.39 0.935", "0.49 0.975", "SUI_OpenElement Setup", TextAnchor.MiddleCenter);
                SmartManager.AUI.CreateButton(ref S, MainGUIVar,  UIColors["buttonbg"], $"<b><color=#1586db>{msg("CloseBtn", player)}</color></b>", 16, "0.52 0.935", "0.62 0.975", "SUI_OpenElement Close", TextAnchor.MiddleCenter);
                SmartManager.AUI.CreateLabel(ref S,  MainGUIVar,  UIColors["dark"],     $"<b><color=#1586db>{msg("TitleMenu", player)}</color></b>", 20, "0.0210 0.930", "0.1 0.980", TextAnchor.MiddleCenter);
                //Server Statistics
                SmartManager.AUI.CreatePanel(ref S,  MainGUIVar,  UIColors["grey1"],    "0.01 0.01", "0.99 0.910", true);
                SmartManager.AUI.CreateButton(ref S, MainGUIVar,  UIColors["buttonbg"], $"<b><color=#1586db>{msg("createfurnace", player)}</color></b>", 16, "0.88 0.845", "0.98 0.885", $"SUI_OpenElement createfurnace", TextAnchor.MiddleCenter);
                SmartManager.AUI.CreateButton(ref S, MainGUIVar,  UIColors["buttonbg"], $"<b><color=#1586db>{msg("createturret", player)}</color></b>", 16, "0.88 0.780", "0.98 0.820", $"SUI_OpenElement createturret", TextAnchor.MiddleCenter);
                SmartManager.AUI.CreateButton(ref S, MainGUIVar,  UIColors["buttonbg"], $"<b><color=#1586db>{msg("createlight", player)}</color></b>", 16, "0.88 0.715", "0.98 0.755", $"SUI_OpenElement createlight", TextAnchor.MiddleCenter);
                SmartManager.AUI.CreateButton(ref S, MainGUIVar,  UIColors["buttonbg"], $"<b><color=#1586db>{msg("createdoor", player)}</color></b>", 16, "0.88 0.650", "0.98 0.690", $"SUI_OpenElement createdoor", TextAnchor.MiddleCenter);
                CuiHelper.AddUi(player, S); 
            }

            void ShowTurretMenu(BasePlayer player){
                CheckData(player);
                var h = smartData.data[player.userID];
                if(!h.hashome){
                    CreateHomeMenu(player);
                }
                if(h.turrets.Count == 0){
                    SendReply(player, "You do not have any turrets!");  
                    return;
                }

                SmartManager.DestroyUI(player);
                openedUI.Add(player);
                var S = SmartManager.AUI.CreateElementContainer(MainGUIVar, UIColors["dark"], "0 0", "1 1", true);
                SmartManager.AUI.CreatePanel(ref S,  MainGUIVar,  UIColors["grey1"],    "0.01 0.92", "0.99 0.99", true);
                SmartManager.AUI.CreateButton(ref S, MainGUIVar,  UIColors["buttonbg"], $"<b><color=#1586db>{msg("HomeBtn", player)}</color></b>", 16, "0.13 0.935", "0.23 0.975", "SUI_OpenElement Home", TextAnchor.MiddleCenter);
                SmartManager.AUI.CreateButton(ref S, MainGUIVar,  UIColors["buttonbg"], $"<b><color=red>{msg("ObjectBtn", player)}</color></b>", 16, "0.26 0.935", "0.36 0.975", "SUI_OpenElement Object", TextAnchor.MiddleCenter);
                SmartManager.AUI.CreateButton(ref S, MainGUIVar,  UIColors["buttonbg"], $"<b><color=#1586db>{msg("SetupBtn", player)}</color></b>", 16, "0.39 0.935", "0.49 0.975", "SUI_OpenElement Setup", TextAnchor.MiddleCenter);
                SmartManager.AUI.CreateButton(ref S, MainGUIVar,  UIColors["buttonbg"], $"<b><color=#1586db>{msg("CloseBtn", player)}</color></b>", 16, "0.52 0.935", "0.62 0.975", "SUI_OpenElement Close", TextAnchor.MiddleCenter);
                SmartManager.AUI.CreateLabel(ref S,  MainGUIVar,  UIColors["dark"],     $"<b><color=#1586db>{msg("TitleMenu", player)}</color></b>", 20, "0.0210 0.930", "0.1 0.980", TextAnchor.MiddleCenter);
                //Stuff
                SmartManager.AUI.CreatePanel(ref S,  MainGUIVar,  UIColors["grey1"],    "0.01 0.01", "0.99 0.910", true);
                var i = 0;
                foreach(var d in smartData.data[player.userID].turrets){
                    i++;
                    var nonePos = SmartManager.CalcBttnPos(i - 1);
                    var data = smartData.data[player.userID].turrets[d.Key];
                    var name = data.name;
                    var pos = data;
                    var color = "";
                    if(ents[new Vector3(pos.x, pos.y, pos.z)].IsOn()){
                        color = "green";
                    }else{
                        color = "red";
                    }
                    SmartManager.AUI.CreateButton(ref S, MainGUIVar, UIColors["buttonbg"], $"<b><color={color}>{name}</color></b>", 18, $"{nonePos[0]} {nonePos[1]}", $"{nonePos[2]} {nonePos[3]}", $"SUI_OpenElement toggleturret {d.Key}", TextAnchor.MiddleCenter);
                }
                SmartManager.AUI.CreateButton(ref S, MainGUIVar,  UIColors["buttonbg"], $"<b><color=#1586db>{msg("TurnOnAll", player)}</color></b>", 16, "0.41 0.075", "0.55 0.115", "SUI_OpenElement ToggleAllTurrets 1", TextAnchor.MiddleCenter);
                SmartManager.AUI.CreateButton(ref S, MainGUIVar,  UIColors["buttonbg"], $"<b><color=#1586db>{msg("TurnOffAll", player)}</color></b>", 16, "0.60 0.075", "0.74 0.115", "SUI_OpenElement ToggleAllTurrets 2", TextAnchor.MiddleCenter);
                CuiHelper.AddUi(player, S);   
            }
            
            void ShowLightMenu(BasePlayer player){
                CheckData(player);
                var d = smartData.data[player.userID];
                if(!d.hashome){
                    CreateHomeMenu(player);
                }
                if(d.lights.Count == 0){
                    SendReply(player, "You do not have any lights!");  
                    return;
                }

                SmartManager.DestroyUI(player);
                openedUI.Add(player);
                var S = SmartManager.AUI.CreateElementContainer(MainGUIVar, UIColors["dark"], "0 0", "1 1", true);
                SmartManager.AUI.CreatePanel(ref S,  MainGUIVar,  UIColors["grey1"],    "0.01 0.92", "0.99 0.99", true);
                SmartManager.AUI.CreateButton(ref S, MainGUIVar,  UIColors["buttonbg"], $"<b><color=#1586db>{msg("HomeBtn", player)}</color></b>", 16, "0.13 0.935", "0.23 0.975", "SUI_OpenElement Home", TextAnchor.MiddleCenter);
                SmartManager.AUI.CreateButton(ref S, MainGUIVar,  UIColors["buttonbg"], $"<b><color=red>{msg("ObjectBtn", player)}</color></b>", 16, "0.26 0.935", "0.36 0.975", "SUI_OpenElement Object", TextAnchor.MiddleCenter);
                SmartManager.AUI.CreateButton(ref S, MainGUIVar,  UIColors["buttonbg"], $"<b><color=#1586db>{msg("SetupBtn", player)}</color></b>", 16, "0.39 0.935", "0.49 0.975", "SUI_OpenElement Setup", TextAnchor.MiddleCenter);
                SmartManager.AUI.CreateButton(ref S, MainGUIVar,  UIColors["buttonbg"], $"<b><color=#1586db>{msg("CloseBtn", player)}</color></b>", 16, "0.52 0.935", "0.62 0.975", "SUI_OpenElement Close", TextAnchor.MiddleCenter);
                SmartManager.AUI.CreateLabel(ref S,  MainGUIVar,  UIColors["dark"],     $"<b><color=#1586db>{msg("TitleMenu", player)}</color></b>", 20, "0.0210 0.930", "0.1 0.980", TextAnchor.MiddleCenter);
                //Stuff
                SmartManager.AUI.CreatePanel(ref S,  MainGUIVar,  UIColors["grey1"],    "0.01 0.01", "0.99 0.910", true);
                var i = 0;
                foreach(var entry in smartData.data[player.userID].lights){
                    i++;
                    var nonePos = SmartManager.CalcBttnPos(i - 1);
                    var data = smartData.data[player.userID].lights[entry.Key];
                    var name = data.name;
                    var pos = data;
                    var color = "";
                    if(ents[new Vector3(pos.x, pos.y, pos.z)].IsOn()){
                        color = "green";
                    }else{
                        color = "red";
                    }
                    SmartManager.AUI.CreateButton(ref S, MainGUIVar, UIColors["buttonbg"], $"<b><color={color}>{name}</color></b>", 18, $"{nonePos[0]} {nonePos[1]}", $"{nonePos[2]} {nonePos[3]}", $"SUI_OpenElement togglelight {entry.Key}", TextAnchor.MiddleCenter);
                }
                SmartManager.AUI.CreateButton(ref S, MainGUIVar,  UIColors["buttonbg"], $"<b><color=#1586db>{msg("TurnLightsOn", player)}</color></b>", 16, "0.41 0.075", "0.55 0.115", "SUI_OpenElement ToggleAllLights 1", TextAnchor.MiddleCenter);
                SmartManager.AUI.CreateButton(ref S, MainGUIVar,  UIColors["buttonbg"], $"<b><color=#1586db>{msg("TurnLightsOff", player)}</color></b>", 16, "0.60 0.075", "0.74 0.115", "SUI_OpenElement ToggleAllLights 2", TextAnchor.MiddleCenter);              
                CuiHelper.AddUi(player, S);   
            }

            void ShowDoorMenu(BasePlayer player){
                CheckData(player);
                var h = smartData.data[player.userID];
                if(!h.hashome){
                    CreateHomeMenu(player);
                }
                if(h.doors.Count == 0){
                    SendReply(player, "You do not have any doors!");  
                    return;
                }

                SmartManager.DestroyUI(player);
                openedUI.Add(player);
                var S = SmartManager.AUI.CreateElementContainer(MainGUIVar, UIColors["dark"], "0 0", "1 1", true);
                SmartManager.AUI.CreatePanel(ref S,  MainGUIVar,  UIColors["grey1"],    "0.01 0.92", "0.99 0.99", true);
                SmartManager.AUI.CreateButton(ref S, MainGUIVar,  UIColors["buttonbg"], $"<b><color=#1586db>{msg("HomeBtn", player)}</color></b>", 16, "0.13 0.935", "0.23 0.975", "SUI_OpenElement Home", TextAnchor.MiddleCenter);
                SmartManager.AUI.CreateButton(ref S, MainGUIVar,  UIColors["buttonbg"], $"<b><color=red>{msg("ObjectBtn", player)}</color></b>", 16, "0.26 0.935", "0.36 0.975", "SUI_OpenElement Object", TextAnchor.MiddleCenter);
                SmartManager.AUI.CreateButton(ref S, MainGUIVar,  UIColors["buttonbg"], $"<b><color=#1586db>{msg("SetupBtn", player)}</color></b>", 16, "0.39 0.935", "0.49 0.975", "SUI_OpenElement Setup", TextAnchor.MiddleCenter);
                SmartManager.AUI.CreateButton(ref S, MainGUIVar,  UIColors["buttonbg"], $"<b><color=#1586db>{msg("CloseBtn", player)}</color></b>", 16, "0.52 0.935", "0.62 0.975", "SUI_OpenElement Close", TextAnchor.MiddleCenter);
                SmartManager.AUI.CreateLabel(ref S,  MainGUIVar,  UIColors["dark"],     $"<b><color=#1586db>{msg("TitleMenu", player)}</color></b>", 20, "0.0210 0.930", "0.1 0.980", TextAnchor.MiddleCenter);
                //Stuff
                SmartManager.AUI.CreatePanel(ref S,  MainGUIVar,  UIColors["grey1"],    "0.01 0.01", "0.99 0.910", true);
                var i = 0;
                foreach(var d in smartData.data[player.userID].doors){
                    i++;
                    var nonePos = SmartManager.CalcBttnPos(i - 1);
                    var data = smartData.data[player.userID].doors[d.Key];
                    var name = data.name;
                    var pos = data;
                    var color = "";
                    if(ents[new Vector3(pos.x, pos.y, pos.z)].IsOpen()){
                        color = "green";
                    }else{
                        color = "red";
                    }
                    SmartManager.AUI.CreateButton(ref S, MainGUIVar, UIColors["buttonbg"], $"<b><color={color}>{name}</color></b>", 18, $"{nonePos[0]} {nonePos[1]}", $"{nonePos[2]} {nonePos[3]}", $"SUI_OpenElement toggledoor {d.Key}", TextAnchor.MiddleCenter);
                }
                SmartManager.AUI.CreateButton(ref S, MainGUIVar,  UIColors["buttonbg"], $"<b><color=#1586db>{msg("OpenAll", player)}</color></b>", 16, "0.41 0.075", "0.55 0.115", "SUI_OpenElement ToggleAllDoors 1", TextAnchor.MiddleCenter);
                SmartManager.AUI.CreateButton(ref S, MainGUIVar,  UIColors["buttonbg"], $"<b><color=#1586db>{msg("CloseAll", player)}</color></b>", 16, "0.60 0.075", "0.74 0.115", "SUI_OpenElement ToggleAllDoors 2", TextAnchor.MiddleCenter);
                CuiHelper.AddUi(player, S);   
            }
            void ShowFurnaceMenu(BasePlayer player){
                CheckData(player);
                var d = smartData.data[player.userID];
                if(!d.hashome){
                    CreateHomeMenu(player);
                }
                if(d.furnaces.Count == 0){
                    SendReply(player, "You do not have any furnaces!");  
                    return;
                }

                SmartManager.DestroyUI(player);
                openedUI.Add(player);
                var S = SmartManager.AUI.CreateElementContainer(MainGUIVar, UIColors["dark"], "0 0", "1 1", true);
                SmartManager.AUI.CreatePanel(ref S,  MainGUIVar,  UIColors["grey1"],    "0.01 0.92", "0.99 0.99", true);
                SmartManager.AUI.CreateButton(ref S, MainGUIVar,  UIColors["buttonbg"], $"<b><color=#1586db>{msg("HomeBtn", player)}</color></b>", 16, "0.13 0.935", "0.23 0.975", "SUI_OpenElement Home", TextAnchor.MiddleCenter);
                SmartManager.AUI.CreateButton(ref S, MainGUIVar,  UIColors["buttonbg"], $"<b><color=red>{msg("ObjectBtn", player)}</color></b>", 16, "0.26 0.935", "0.36 0.975", "SUI_OpenElement Object", TextAnchor.MiddleCenter);
                SmartManager.AUI.CreateButton(ref S, MainGUIVar,  UIColors["buttonbg"], $"<b><color=#1586db>{msg("SetupBtn", player)}</color></b>", 16, "0.39 0.935", "0.49 0.975", "SUI_OpenElement Setup", TextAnchor.MiddleCenter);
                SmartManager.AUI.CreateButton(ref S, MainGUIVar,  UIColors["buttonbg"], $"<b><color=#1586db>{msg("CloseBtn", player)}</color></b>", 16, "0.52 0.935", "0.62 0.975", "SUI_OpenElement Close", TextAnchor.MiddleCenter);
                SmartManager.AUI.CreateLabel(ref S,  MainGUIVar,  UIColors["dark"],     $"<b><color=#1586db>{msg("TitleMenu", player)}</color></b>", 20, "0.0210 0.930", "0.1 0.980", TextAnchor.MiddleCenter);
                //Stuff
                SmartManager.AUI.CreatePanel(ref S,  MainGUIVar,  UIColors["grey1"],    "0.01 0.01", "0.99 0.910", true);
                var i= 0;
                foreach(var e in smartData.data[player.userID].furnaces){
                    i++;
                    var nonePos = SmartManager.CalcBttnPos(i - 1);
                    var data = smartData.data[player.userID].furnaces[e.Key];
                    var name = data.name;
                    var pos = data;
                    var color = "";
                    if(ents[new Vector3(pos.x, pos.y, pos.z)].IsOn()){
                        color = "green";
                    }else{
                        color = "red";
                    }
                    SmartManager.AUI.CreateButton(ref S, MainGUIVar, UIColors["buttonbg"], $"<b><color={color}>{name}</color></b>", 18, $"{nonePos[0]} {nonePos[1]}", $"{nonePos[2]} {nonePos[3]}", $"SUI_OpenElement togglefurnace {e.Key}", TextAnchor.MiddleCenter);
                }
                SmartManager.AUI.CreateButton(ref S, MainGUIVar,  UIColors["buttonbg"], $"<b><color=#1586db>{msg("TurnOnAllF", player)}</color></b>", 16, "0.41 0.075", "0.55 0.115", "SUI_OpenElement ToggleAllFurnaces 1", TextAnchor.MiddleCenter);
                SmartManager.AUI.CreateButton(ref S, MainGUIVar,  UIColors["buttonbg"], $"<b><color=#1586db>{msg("TurnOffAllF", player)}</color></b>", 16, "0.60 0.075", "0.74 0.115", "SUI_OpenElement ToggleAllFurnaces 2", TextAnchor.MiddleCenter);
                CuiHelper.AddUi(player, S);   
            }

            void CreateObjects(BasePlayer player){
                CheckData(player);
                var d = smartData.data[player.userID];
                if(!d.hashome)
                    CreateHomeMenu(player);

                SmartManager.DestroyUI(player);
                openedUI.Add(player);
                var S = SmartManager.AUI.CreateElementContainer(MainGUIVar, UIColors["dark"], "0 0", "1 1", true);
                SmartManager.AUI.CreatePanel(ref S,  MainGUIVar,  UIColors["grey1"],    "0.01 0.92", "0.99 0.99", true);
                SmartManager.AUI.CreateButton(ref S, MainGUIVar,  UIColors["buttonbg"], $"<b><color=#1586db>{msg("HomeBtn", player)}</color></b>", 16, "0.13 0.935", "0.23 0.975", "SUI_OpenElement Home", TextAnchor.MiddleCenter);
                SmartManager.AUI.CreateButton(ref S, MainGUIVar,  UIColors["buttonbg"], $"<b><color=red>{msg("ObjectBtn", player)}</color></b>", 16, "0.26 0.935", "0.36 0.975", "SUI_OpenElement Object", TextAnchor.MiddleCenter);
                SmartManager.AUI.CreateButton(ref S, MainGUIVar,  UIColors["buttonbg"], $"<b><color=#1586db>{msg("SetupBtn", player)}</color></b>", 16, "0.39 0.935", "0.49 0.975", "SUI_OpenElement Setup", TextAnchor.MiddleCenter);
                SmartManager.AUI.CreateButton(ref S, MainGUIVar,  UIColors["buttonbg"], $"<b><color=#1586db>{msg("CloseBtn", player)}</color></b>", 16, "0.52 0.935", "0.62 0.975", "SUI_OpenElement Close", TextAnchor.MiddleCenter);
                SmartManager.AUI.CreateLabel(ref S,  MainGUIVar,  UIColors["dark"],     $"<b><color=#1586db>{msg("TitleMenu", player)}</color></b>", 20, "0.0210 0.930", "0.1 0.980", TextAnchor.MiddleCenter);
                //Stuff
                var sf = permission.UserHasPermission(player.UserIDString, configData.Variables.furnacePagePerm   )  ? "showfurnaces"  :   "nop";
                var st = permission.UserHasPermission(player.UserIDString, configData.Variables.turretPagePerm    )  ? "showturrets"   :   "nop";
                var sl = permission.UserHasPermission(player.UserIDString, configData.Variables.turretPagePerm    )  ? "showlights"    :   "nop";
                var sd = permission.UserHasPermission(player.UserIDString, configData.Variables.turretPagePerm    )  ? "showdoors"     :   "nop";
                SmartManager.AUI.CreatePanel(ref S,  MainGUIVar,  UIColors["grey1"],    "0.01 0.01", "0.99 0.910", true);
                SmartManager.AUI.CreateButton(ref S, MainGUIVar,  UIColors["buttonbg"], $"<b><color=#1586db>{msg("furnacelist", player)}</color></b>", 16, "0.88 0.845", "0.98 0.885", $"SUI_OpenElement {sf}", TextAnchor.MiddleCenter);
                SmartManager.AUI.CreateButton(ref S, MainGUIVar,  UIColors["buttonbg"], $"<b><color=#1586db>{msg("turretlist", player)}</color></b>", 16, "0.88 0.780", "0.98 0.820", $"SUI_OpenElement {st}", TextAnchor.MiddleCenter);
                SmartManager.AUI.CreateButton(ref S, MainGUIVar,  UIColors["buttonbg"], $"<b><color=#1586db>{msg("lightlist", player)}</color></b>", 16, "0.88 0.715", "0.98 0.755", $"SUI_OpenElement {sl}", TextAnchor.MiddleCenter);
                SmartManager.AUI.CreateButton(ref S, MainGUIVar,  UIColors["buttonbg"], $"<b><color=#1586db>{msg("doorlist", player)}</color></b>", 16, "0.88 0.650", "0.98 0.690", $"SUI_OpenElement {sd}", TextAnchor.MiddleCenter);
                CuiHelper.AddUi(player, S);         
            }

            void CreateHomeMenu(BasePlayer player){
                CheckData(player);
                SmartManager.DestroyUI(player);
                openedUI.Add(player);
                var S = SmartManager.AUI.CreateElementContainer(MainGUIVar, UIColors["dark"], "0 0", "1 1", true);
                SmartManager.AUI.CreatePanel(ref S,  MainGUIVar,  UIColors["grey1"],    "0.01 0.92", "0.99 0.99", true);
                SmartManager.AUI.CreateButton(ref S, MainGUIVar,  UIColors["buttonbg"], $"<b><color=red>{msg("HomeBtn", player)}</color></b>", 16, "0.13 0.935", "0.23 0.975", "SUI_OpenElement Home", TextAnchor.MiddleCenter);
                SmartManager.AUI.CreateButton(ref S, MainGUIVar,  UIColors["buttonbg"], $"<b><color=#1586db>{msg("ObjectBtn", player)}</color></b>", 16, "0.26 0.935", "0.36 0.975", "SUI_OpenElement Object", TextAnchor.MiddleCenter);
                SmartManager.AUI.CreateButton(ref S, MainGUIVar,  UIColors["buttonbg"], $"<b><color=#1586db>{msg("SetupBtn", player)}</color></b>", 16, "0.39 0.935", "0.49 0.975", "SUI_OpenElement Setup", TextAnchor.MiddleCenter);
                SmartManager.AUI.CreateButton(ref S, MainGUIVar,  UIColors["buttonbg"], $"<b><color=#1586db>{msg("CloseBtn", player)}</color></b>", 16, "0.52 0.935", "0.62 0.975", "SUI_OpenElement Close", TextAnchor.MiddleCenter);
                SmartManager.AUI.CreateLabel(ref S,  MainGUIVar,  UIColors["dark"],     $"<b><color=#1586db>{msg("TitleMenu", player)}</color></b>", 20, "0.0210 0.930", "0.1 0.980", TextAnchor.MiddleCenter);
                //Server Statistics
                SmartManager.AUI.CreatePanel(ref S,  MainGUIVar,  UIColors["grey1"],    "0.01 0.01", "0.99 0.910", true);
                var d = smartData.data[player.userID];
                if(!d.hashome){
                    SmartManager.AUI.CreateButton(ref S, MainGUIVar,  UIColors["buttonbg"], $"<b><color=red>{msg("createhome", player)}</color></b>", 16, "0.025 0.845", "0.125 0.885", $"SUI_OpenElement Create", TextAnchor.MiddleCenter);
                    CuiHelper.AddUi(player, S);
                    return;
                }
                SmartManager.AUI.CreateLabel(ref S,  MainGUIVar,  UIColors["dark"],     $"<b><color=#1586db>Smart-Home XYZ: {"(" + d.x + " , " + d.y + " , " + d.z + ")"}</color></b>", 20, "0.0210 0.800", "0.500 0.950", TextAnchor.MiddleLeft);
                SmartManager.AUI.CreateLabel(ref S,  MainGUIVar,  UIColors["dark"],     $"<b><color=#1586db>Smart Turrets: {d.turrets.Count}</color></b>", 20, "0.0210 0.600", "0.150 0.950", TextAnchor.MiddleLeft);
                SmartManager.AUI.CreateLabel(ref S,  MainGUIVar,  UIColors["dark"],     $"<b><color=#1586db>Smart Lights: {d.lights.Count}</color></b>", 20, "0.0210 0.500", "0.150 0.950", TextAnchor.MiddleLeft);
                SmartManager.AUI.CreateLabel(ref S,  MainGUIVar,  UIColors["dark"],     $"<b><color=#1586db>Smart Doors: {d.doors.Count}</color></b>", 20, "0.0210 0.400", "0.150 0.950", TextAnchor.MiddleLeft);
                SmartManager.AUI.CreateLabel(ref S,  MainGUIVar,  UIColors["dark"],     $"<b><color=#1586db>Smart Furnaces: {d.furnaces.Count}</color></b>", 20, "0.0210 0.700", "0.150 0.950", TextAnchor.MiddleLeft);
                SmartManager.AUI.CreateButton(ref S, MainGUIVar,  UIColors["buttonbg"], $"<b><color=#1586db>{msg("newhome", player)}</color></b>", 16, "0.88 0.845", "0.98 0.885", $"SUI_OpenElement CreateNew", TextAnchor.MiddleCenter);
                CuiHelper.AddUi(player, S);   
            }

            private Dictionary<string, string> UIColors = new Dictionary<string, string>
            {
                {"dark", "0 0 0 0.94" },
                {"header", "0 0 0 0.6" },
                {"light", ".85 .85 .85 0.3" },
                {"grey1", "0 0 0 0.8" },
                {"brown", "0.3 0.16 0.0 1.0" },
                {"yellow", "0.9 0.9 0.0 1.0" },
                {"#1586db", "1.0 0.65 0.0 1.0" },
                {"blue", "0.2 0.6 1.0 1.0" },
                {"red", "1.0 0.1 0.1 1.0" },
                {"green", "0.28 0.82 0.28 1.0" },
                {"grey", "0.85 0.85 0.85 1.0" },
                {"lightblue", "0.6 0.86 1.0 1.0" },
                {"buttonbg", "0.2 0.2 0.2 0.7" },
                {"buttongreen", "0.133 0.965 0.133 0.9" },
                {"buttonred", "0.964 0.133 0.133 0.9" },
                {"buttongrey", "0.8 0.8 0.8 0.9" },
                {"invis", "0 0 0 0.0"}
            };

        #endregion

        #region MainClass

            public class SmartManager
            {
                static public void DestroyUI(BasePlayer player){
                    CuiHelper.DestroyUi(player, MainGUIVar); 
                    if(openedUI.ToList().Contains(player)){openedUI.Remove(player);}
                }

                static public float[] CalcBttnPos(int number)
                {
                    Vector2 position = new Vector2(0.0200f, 0.8250f);
                    Vector2 dimensions = new Vector2(0.100f, 0.07f);
                    float offsetY = 0;
                    float offsetX = 0;
                    if (number >= 0 && number < 9)
                    {
                        offsetX = (0.0080f + dimensions.x) * number;
                    }
                    if (number > 8 && number < 18)
                    {
                        offsetX = (0.008f + dimensions.x) * (number - 9);
                        offsetY = (-0.0120f - dimensions.y) * 1;
                    }
                    if (number > 17 && number < 27)
                    {
                        offsetX = (0.008f + dimensions.x) * (number - 18);
                        offsetY = (-0.0120f - dimensions.y) * 2;
                    } 
                    if (number > 26 && number < 36)
                    {
                        offsetX = (0.008f + dimensions.x) * (number - 27);
                        offsetY = (-0.0120f - dimensions.y) * 3;
                    } 
                    if (number > 35 && number < 45)
                    {
                        offsetX = (0.008f + dimensions.x) * (number - 36);
                        offsetY = (-0.0120f - dimensions.y) * 4;
                    } 
                    if (number > 44 && number < 54)
                    {
                        offsetX = (0.008f + dimensions.x) * (number - 45);
                        offsetY = (-0.0120f - dimensions.y) * 5;
                    } 
                    if (number > 53 && number < 63)
                    {
                        offsetX = (0.008f + dimensions.x) * (number - 54);
                        offsetY = (-0.0120f - dimensions.y) * 6;
                    } 
                    if (number > 62 && number < 72)
                    {
                        offsetX = (0.008f + dimensions.x) * (number - 63);
                        offsetY = (-0.0120f - dimensions.y) * 7;
                    }        
                    Vector2 offset = new Vector2(offsetX, offsetY);
                    Vector2 posMin = position + offset;
                    Vector2 posMax = posMin + dimensions;
                    return new float[] { posMin.x, posMin.y, posMax.x, posMax.y };
                }

                public class AUI
                {
                    static public CuiElementContainer CreateElementContainer(string panelName, string color, string aMin, string aMax, bool useCursor = false, string parent = "Overlay")
                    {
                        var NewElement = new CuiElementContainer()
                        {
                            {
                                new CuiPanel
                                {
                                    Image = {Color = color},
                                    RectTransform = {AnchorMin = aMin, AnchorMax = aMax},
                                    CursorEnabled = useCursor
                                },
                                new CuiElement().Parent = parent,
                                panelName
                            }
                        };
                        return NewElement;
                    }
                    static public void CreatePanel(ref CuiElementContainer container, string panel, string color, string aMin, string aMax, bool cursor = false)
                    {
                        container.Add(new CuiPanel
                        {
                            Image = { Color = color },
                            RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                            CursorEnabled = cursor
                        },
                        panel);
                    }
                    static public void CreateLabel(ref CuiElementContainer container, string panel, string color, string text, int size, string aMin, string aMax, TextAnchor align = TextAnchor.MiddleCenter)
                    {
                        container.Add(new CuiLabel
                        {
                            Text = { Color = color, FontSize = size, Align = align, Text = text },
                            RectTransform = { AnchorMin = aMin, AnchorMax = aMax }
                        },
                        panel);

                    }
                    static public void CreateClose(ref CuiElementContainer container, string panel, string color, string text, int size, string aMin, string aMax, string command, TextAnchor align = TextAnchor.MiddleCenter)
                    {
                        container.Add(new CuiButton
                        {
                            Button = { Color = color, Command = command, FadeIn = 1.0f },
                            RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                            Text = { Text = text, FontSize = size, Align = align }
                        },
                        panel);
                    }
                    static public void CreateButton(ref CuiElementContainer container, string panel, string color, string text, int size, string aMin, string aMax, string command, TextAnchor align = TextAnchor.MiddleLeft)
                    {
                        container.Add(new CuiButton
                        {
                            Button = { Color = color, Command = command, FadeIn = 1.0f },
                            RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                            Text = { Text = text, FontSize = size, Align = align }
                        },
                        panel);
                    }
                }
            }   
        #endregion
    }
}