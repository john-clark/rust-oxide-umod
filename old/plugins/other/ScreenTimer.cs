using System.Collections.Generic;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using System;

namespace Oxide.Plugins
{
    [Info("ScreenTimer", "DylanSMR", "2.0.1", ResourceId = 1918)]
    [Description("A timer enhanced system for OxideMod Rust")]
    class ScreenTimer : RustPlugin
    {  
		#region DataFile 
			public void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(this.Title, storage);
			public class TimeHandler{
				private ScreenTimer instance;
				public bool isRunning;
				public string type_time;
				public string type_text;
				public string type_size = "16";
				public string type_color = "0 0 0";
				public string type_tsize = "12" ;
				public string type_tcolor = "0 0 0";
				public string type_command = "NoCommandRandomText=jf89ew4u0=03-485=3-24059=3-4059=0-23195=";
				public int key;

				public void Load(ScreenTimer instance){
					this.instance = instance;
					this.StartPrintCycle();
					instance.PrintWarning("Initalized Timer: {0}", this.key);
					if(this.key == storage.screen) instance.timer.Once(1.5f, () => this.Show());
				}

				public ReturnType Create(ScreenTimer instance, string text, string time){
					this.type_time = time;
					if(this.IsDateTime() == "InvalidTime") return ReturnType.TimeIssue;
					this.type_text = text;
					this.isRunning = true;
					storage.handlers.Add(++storage.count, this);
					this.key = storage.count;
					this.Load(instance);
					instance.SaveData();
					return ReturnType.Created;
				}

				public string IsDateTime(){
					DateTime t;
					int s;				
					if(DateTime.TryParse(type_time, out t)) return "DateTime";
					if(int.TryParse(type_time, out s)) return "SecondTimer";
					return "InvalidTime";
				}

				public void Show() => instance.CreateBasicUI();

				public void HandleEnd(){
					if(this.type_command != "NoCommandRandomText=jf89ew4u0=03-485=3-24059=3-4059=0-23195=") ConsoleSystem.Run(ConsoleSystem.Option.Server, this.type_command);
					this.type_text = "Timer Ended";
					if(this.key == storage.screen){
						instance.UpdatePlayerUI();
						instance.timer.Once(3f, () => instance.DestroyUI());
					}
					this.isRunning = false;
					storage.handlers.Remove(this.key);
					instance.SaveData();
				}

				public void StartPrintCycle(){
					if(!this.isRunning){ return; }
					var temp = 0;
					if(this.IsDateTime() == "SecondTimer") temp = int.Parse(this.type_time) - 1;
					if(temp == 0 && this.IsDateTime() == "SecondTimer"){
						this.SetValue(ValueType.Type_Time, (temp).ToString());
						this.HandleEnd();
						return;
					} else if (this.IsDateTime() == "DateTime" && DateTime.Now > Convert.ToDateTime(this.type_time)){
						this.HandleEnd();
						return;
					}
					if(this.IsDateTime() == "SecondTimer") this.SetValue(ValueType.Type_Time, (temp).ToString());
					if(storage.screen == this.key) instance.UpdatePlayerUI();
					instance.timer.Once(1f, () => this.StartPrintCycle());
				}

				public string GetUpdated(){
					DateTime t;
					int seconds;
					if(this.IsDateTime() == "DateTime"){
						t = Convert.ToDateTime(this.type_time);
						var span = t - DateTime.Now;
						return string.Format(instance.GetMsg("TimeFormat"), span.Days, span.Hours, span.Minutes, span.Seconds);
					}else if(this.IsDateTime() == "SecondTimer"){
						seconds = Convert.ToInt32(this.type_time);
						var span = TimeSpan.FromSeconds(seconds);
						return string.Format(instance.GetMsg("TimeFormat"), span.Days, span.Hours, span.Minutes, span.Seconds);
					}
					return "null";
				}
				
				public void SetValue(ValueType key, string val){
					switch(key){
						case ValueType.Type_Time: this.type_time = val; break;
						case ValueType.Type_Text: this.type_text = val; break;
						case ValueType.Type_Size: this.type_size = val; break;
						case ValueType.Type_Color: this.type_color = val; break;
						case ValueType.Type_TSize: this.type_tsize = val; break;
						case ValueType.Type_TColor: this.type_tcolor = val; break;
						case ValueType.Type_Command: this.type_command = val; break;
					}
				}
			}
			static Storage storage;
			public class Storage{
				public int screen = -1;
				public int count;
				public Dictionary<int, TimeHandler> handlers = new Dictionary<int, TimeHandler>();
			}
		#endregion

		#region OxideMod
			void Unload(){
				DestroyUI();
				SaveData();
			}
			void OnPlayerInit(BasePlayer player){
				players.Add(player);
				CreateBasicUI();
			}

			void OnPlayerDisconnected(BasePlayer player, string reason){
				if(players.Contains(player)) players.Remove(player);
			}
			private string GetMsg(string msg, string id = "") => lang.GetMessage(msg, this, id);
			void Init(){
				permission.RegisterPermission("screentimer.admin", this);
				storage = Interface.Oxide.DataFileSystem.ReadObject<Storage>(this.Title);
				foreach(var e in storage.handlers){
					var h = storage.handlers[e.Key];
					h.Load(this);
				}
				lang.RegisterMessages(new Dictionary<string, string>(){
					{"NoPerm", "You do not have the correct permissions to preform this command"},
					{"TimeFormat","{0:D2}d:{1:D2}h:{2:D2}m:{3:D2}s"},
					{"valuetable","Values: Text, Size, Color, TimeTextSize, TimeTextColor, Command"},
					{"help","Syntax:\n/stm types - Shows all available types\n/stm edit (id) (type) (value) - Edits a timers type.\n/stm toggle - Toggles your GUI\n/stm wipe - Wipes the data\n/stm create (text) (time) - Creates a timer\n/stm end (id) - Ends a timer\n/stm show (id) - Shows a timer on the screen\n/stm list - Lists all timers"},
					{"noexist","Timer does not exist in the storage system. Please refer to /stm list"},
					{"hudtoggle","Hud Status: {0}"},
					{"InvalidFormat","Invalid Time Format: Examples - 120 = two minutes, '3/25/2017 15:30' in quotation marks."},
					{"Created","Timer Created With Variables:\nText={0}\nTime={1}\nID={2}"},
					{"TimerSet","Timer {0} set to be on the screen."},
					{"TypeSet","Setting Timer {0} {1} to {2}"},
				}, this);
				foreach(var player in BasePlayer.activePlayerList){
					players.Add(player);
				}
			}
		#endregion

		#region References
			static string stuff = "screentimer.lowman";
			static string stufftwo = "screentimer.twoman";
			List<BasePlayer> players = new List<BasePlayer>();
			public enum ValueType{
				Type_Time,
				Type_Text,
				Type_Size,
				Type_Color,
				Type_TSize,
				Type_TColor,
				Type_Command,
			}
			public enum ReturnType{
				TimeIssue,
				Created,
			}
		#endregion

		#region Command 
			[ConsoleCommand("stm")]
			private void STM_CCommands(ConsoleSystem.Arg arg){
				var args = arg.Args;
				var player = arg;
				var pp = arg.Player();
				if(pp != null){
					if(!permission.UserHasPermission(pp.UserIDString, "screentimer.admin")){
						SendReply(player, lang.GetMessage("NoPerm", this));
						return;	
					}
				}
				if(!(args.Length > 0)){
					SendReply(player, lang.GetMessage("help", this));
					return;
				}
				switch(args[0]){
					case "wipe":
						storage.screen = -1;
						storage.count = 0;
						storage.handlers.Clear();
						SaveData();
						SendReply(arg, "Screen Timer Data Wiped");
					break;
					case "create":
						if(args.Length > 3 || args.Length < 3){
							SendReply(arg, lang.GetMessage("help", this));
							return;
						}
						var nt = new TimeHandler();
						if(nt.Create(this, args[1], args[2]) == ReturnType.TimeIssue){
							SendReply(player, GetMsg("InvalidFormat"));
							return;
						}
						SendReply(player, string.Format(GetMsg("Created"), args[1], args[2], storage.count));
						SaveData();
					break;
					case "end":
						if(args.Length > 3 || args.Length < 3){
							SendReply(player, lang.GetMessage("help", this));
							return;
						}
						var id = Convert.ToInt32(args[1]);
						if(!storage.handlers.ContainsKey(id)){
							SendReply(player, GetMsg("noexist"));
							return;
						}
						var t = storage.handlers[id];
						t.HandleEnd();
						SendReply(player, "Stopping Timer");
					break;	
					case "show":
						if(args.Length > 2 || args.Length < 2){
							SendReply(player, lang.GetMessage("help", this));
							return;	
						}
						var idn = Convert.ToInt32(args[1]);
						var ti = storage.handlers[idn];
						if(!storage.handlers.ContainsKey(idn)){
							SendReply(player, GetMsg("noexist"));
							return;
						}
						storage.screen = idn;
						foreach(var p in BasePlayer.activePlayerList){
							Destroy(p);
						}
						ti.Show();
						SaveData();
						SendReply(player, string.Format(GetMsg("TimerSet"), idn));
					break;
					case "edit":
						if(args.Length > 4 || args.Length < 4){
							SendReply(player, lang.GetMessage("help", this));
							return;
						}	
						TimeHandler timer;
						int key;
						key = Convert.ToInt32(args[1]);
						if(!storage.handlers.ContainsKey(key)){
							SendReply(player, GetMsg("noexist"));
							return;
						}
						timer = storage.handlers[key];
						switch(args[2]){		
							case "Text":	
								SendReply(player, string.Format(GetMsg("TypeSet"), args[2], "text", args[3]));
								timer.SetValue(ValueType.Type_Text, args[3]);
								SaveData();
							break;
							case "Size":
								SendReply(player, string.Format(GetMsg("TypeSet"), args[2], "size", args[3]));
								timer.SetValue(ValueType.Type_Size, args[3]);
								SaveData();		
							break;
							case "Color":
								SendReply(player, string.Format(GetMsg("TypeSet"), args[2], "color", args[3]));
								timer.SetValue(ValueType.Type_Color, args[3]);
								SaveData();		
							break;
							case "TimeTextSize":
								SendReply(player, string.Format(GetMsg("TypeSet"), args[2], "time size", args[3]));
								timer.SetValue(ValueType.Type_TSize, args[3]);
								SaveData();	
							break;
							case "TimeTextColor":	
								SendReply(player, string.Format(GetMsg("TypeSet"), args[2], "time color", args[3]));
								timer.SetValue(ValueType.Type_TColor, args[3]);
								SaveData();	
							break;
							case "Command":
								SendReply(player, string.Format(GetMsg("TypeSet"), args[2], "command", args[3]));
								timer.SetValue(ValueType.Type_Command, args[3]);
								SaveData();	
							break;
							default:
								SendReply(player, lang.GetMessage("valuetable", this));
								return;
							break;
						}
					break;
					case "types":
						SendReply(player, lang.GetMessage("valuetable", this));
					break;
					case "list":
						if(storage.handlers.Count == 0){
							SendReply(player, "No timers in storage.");
							return;
						}
						foreach(var e in storage.handlers){
							SendReply(player, $"ID:{e.Key} - Text:{storage.handlers[e.Key].type_text}");
						}
					break;
					default:
						SendReply(player, lang.GetMessage("help", this));
					break;	
				}	
			}
			[ChatCommand("stm")]
			private void STM_Commands(BasePlayer player, string command, string[] args){
				if(args[0] == "toggle"){
					if(players.Contains(player)){  SendReply(player, string.Format(GetMsg("hudtoggle", player.UserIDString), "Off")); players.Remove(player); Destroy(player); }
					else { SendReply(player, string.Format(GetMsg("hudtoggle", player.UserIDString), "On")); players.Add(player); CreateBasicUI();  }
					return;
				}
				if(!permission.UserHasPermission(player.UserIDString, "screentimer.admin")){
					SendReply(player, lang.GetMessage("NoPerm", this, player.UserIDString));
					return;	
				}
				if(!(args.Length > 0)){
					SendReply(player, lang.GetMessage("help", this, player.UserIDString));
					return;
				}
				switch(args[0]){
					case "wipe":
						storage.screen = -1;
						storage.count = 0;
						storage.handlers.Clear();
						SaveData();
						SendReply(player, "Screen Timer Data Wiped");
					break;
					case "create":
						if(args.Length > 3 || args.Length < 2){
							SendReply(player, lang.GetMessage("help", this, player.UserIDString));
							return;
						}
						var nt = new TimeHandler();
						if(nt.Create(this, args[1], args[2]) == ReturnType.TimeIssue){
							SendReply(player, GetMsg("InvalidFormat", player.UserIDString));
							return;
						}
						SendReply(player, string.Format(GetMsg("Created", player.UserIDString), args[1], args[2], storage.count));
						SaveData();
					break;
					case "end":
						if(args.Length > 3 || args.Length < 2){
							SendReply(player, lang.GetMessage("help", this, player.UserIDString));
							return;
						}
						var id = Convert.ToInt32(args[1]);
						if(!storage.handlers.ContainsKey(id)){
							SendReply(player, GetMsg("noexist", player.UserIDString));
							return;
						}
						var t = storage.handlers[id];
						t.HandleEnd();
						SendReply(player, "Stopping Timer");
					break;	
					case "show":
						if(args.Length > 2 || args.Length < 1){
							SendReply(player, lang.GetMessage("help", this, player.UserIDString));
							return;	
						}
						var idn = Convert.ToInt32(args[1]);
						var ti = storage.handlers[idn];
						if(!storage.handlers.ContainsKey(idn)){
							SendReply(player, GetMsg("noexist", player.UserIDString));
							return;
						}
						storage.screen = idn;
						foreach(var p in BasePlayer.activePlayerList) Destroy(p);
						ti.Show();
						SaveData();
						SendReply(player, string.Format(GetMsg("TimerSet", player.UserIDString), idn));
					break;
					case "edit":
						if(args.Length > 4 || args.Length < 3){
							SendReply(player, lang.GetMessage("help", this, player.UserIDString));
							return;
						}	
						TimeHandler timer;
						int key;
						key = Convert.ToInt32(args[1]);
						if(!storage.handlers.ContainsKey(key)){
							SendReply(player, GetMsg("noexist", player.UserIDString));
							return;
						}
						timer = storage.handlers[key];
						switch(args[2]){		
							case "Text":	
								SendReply(player, string.Format(GetMsg("TypeSet", player.UserIDString), args[2], "text", args[3]));
								timer.SetValue(ValueType.Type_Text, args[3]);
								SaveData();
							break;
							case "Size":
								SendReply(player, string.Format(GetMsg("TypeSet", player.UserIDString), args[2], "size", args[3]));
								timer.SetValue(ValueType.Type_Size, args[3]);
								SaveData();		
							break;
							case "Color":
								SendReply(player, string.Format(GetMsg("TypeSet", player.UserIDString), args[2], "color", args[3]));
								timer.SetValue(ValueType.Type_Color, args[3]);
								SaveData();		
							break;
							case "TimeTextSize":
								SendReply(player, string.Format(GetMsg("TypeSet", player.UserIDString), args[2], "time size", args[3]));
								timer.SetValue(ValueType.Type_TSize, args[3]);
								SaveData();	
							break;
							case "TimeTextColor":	
								SendReply(player, string.Format(GetMsg("TypeSet", player.UserIDString), args[2], "time color", args[3]));
								timer.SetValue(ValueType.Type_TColor, args[3]);
								SaveData();	
							break;
							case "Command":
								SendReply(player, string.Format(GetMsg("TypeSet", player.UserIDString), args[2], "command", args[3]));
								timer.SetValue(ValueType.Type_Command, args[3]);
								SaveData();	
							break;
							default:
								SendReply(player, lang.GetMessage("valuetable", this, player.UserIDString));
								return;
							break;
						}
					break;
					case "types":
						SendReply(player, lang.GetMessage("valuetable", this, player.UserIDString));
					break;
					case "list":
						if(storage.handlers.Count == 0){
							SendReply(player, "No timers in storage.");
							return;
						}
						foreach(var e in storage.handlers) SendReply(player, $"ID:{e.Key} - Text:{storage.handlers[e.Key].type_text}");
					break;
					default:
						SendReply(player, lang.GetMessage("help", this, player.UserIDString));
					break;
				}	
			}
		#endregion

		#region UI
			
            private Dictionary<string, string> UIColors = new Dictionary<string, string>
            {
                {"dark", "0 0 0 0.94" },
                {"grey", "0.85 0.85 0.85 1.0" },
                {"invis", "0 0 0 0.0"}
            };
			void DestroyUI(){
				foreach(var player in BasePlayer.activePlayerList){
					CuiHelper.DestroyUi(player, stuff);
					CuiHelper.DestroyUi(player, stufftwo);
				}		
			}
			void Destroy(BasePlayer player){
				CuiHelper.DestroyUi(player, stuff);
				CuiHelper.DestroyUi(player, stufftwo);	
			}
			void CreateBasicUI(){
				foreach(var player in players){
					CuiHelper.DestroyUi(player, stuff);
					if(!storage.handlers[storage.screen].isRunning) return;
					var background = UI.CreateElementContainer(stuff, UIColors["dark"], "0.850 0.400", "0.99 0.500", false);
					UI.CreatePanel(ref background, stuff, UIColors["grey"],"0.01 0.03", "0.98 0.95", false);
					CuiHelper.AddUi(player, background);
				}
			}
			void UpdatePlayerUI(){
				foreach(var player in players){
					var s = storage.handlers[storage.screen];
					if(!s.isRunning) return;
					CuiHelper.DestroyUi(player, stufftwo);
					var content = UI.CreateElementContainer(stufftwo, UIColors["invis"], "0.843 0.380", "0.99 0.800", false);
					UI.CreateLabel(ref content, stufftwo, s.type_tcolor, s.GetUpdated(), Convert.ToInt32(s.type_tsize), "0.0200 0.070", "1 0.150", TextAnchor.MiddleCenter);
					UI.CreateLabel(ref content, stufftwo, s.type_color, s.type_text, Convert.ToInt32(s.type_size), "0.0200 0.170", "1 0.250", TextAnchor.MiddleCenter);
					CuiHelper.AddUi(player, content);
				}
			}
			public class UI
			{
				static public CuiElementContainer CreateElementContainer(string panelName, string color, string aMin, string aMax, bool useCursor = false, string parent = "Overall")
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
		#endregion 
    }
}