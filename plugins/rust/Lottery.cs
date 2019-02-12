using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;

using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Lottery", "Sami37", "1.2.5", ResourceId = 2145)]
    internal class Lottery : RustPlugin
    {
        #region Economy Support

        [PluginReference("Economics")]
        Plugin Economy;

        #endregion

        #region serverreward

        [PluginReference("ServerRewards")]
        Plugin ServerRewards;


        #endregion

        #region humanNPC

        [PluginReference("HumanNPC")]
        Plugin HumanNPC;


        #endregion

        #region generalClass/function
        internal class playerinfo
        {
            public int multiplicator { get; set; } = 1;
            public double currentbet { get; set; }
            public double totalbet { get; set; }

        }

        int[] GetIntArray(int num)
        {
            List<int> listOfInts = new List<int>();
            while(num > 0)
            {
                listOfInts.Add(num % 10);
                num = num / 10;
            }
            listOfInts.Reverse();
            return listOfInts.ToArray();
        }

        private object DoRay(Vector3 Pos, Vector3 Aim)
        {
            var hits = Physics.RaycastAll(Pos, Aim);
            float distance = 3f;
            object target = false;
            foreach (var hit in hits)
            {
                if (hit.distance < distance)
                {
                    distance = hit.distance;
                    target = hit.GetEntity();
                }
            }
            return target;
        }
        #endregion

        #region general_variable
        private bool newConfig, UseSR, UseNPC, AutoCloseAfterPlaceingBet;
        public Dictionary<ulong, playerinfo> Currentbet = new Dictionary<ulong, playerinfo>();
        private string container, containerwin, BackgroundUrl, BackgroundColor, WinBackgroundUrl, WinBackgroundColor, anchorMin, anchorMax;
        private DynamicConfigFile data;
        private List<object> NPCID = new List<object>();
        private double jackpot, SRMinBet, SRjackpot, MinBetjackpot, MinBetjackpotEco;
        private int JackpotNumber, SRJackpotNumber, DefaultMaxRange, DefaultMinRange;
        public Dictionary<string, object> IndividualRates { get; private set; }
        public Dictionary<string, int> SRRates { get; private set; }
        private Dictionary<string, object> DefaultWinRates = null;
        private Dictionary<string, object> SRWinRates = null;
        private List<object> DefaultBasePoint = null;
		private FieldInfo serverinput;
        private Vector3 eyesAdjust;
        private string MainContainer = "MainContainer";
        #endregion

        #region config
        private T GetConfigValue<T>(string category, string setting, T defaultValue)
        {
            var data = Config[category] as Dictionary<string, object>;
            object value;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[category] = data;
                newConfig = true;
            }
            if (data.TryGetValue(setting, out value)) return (T)Convert.ChangeType(value, typeof(T));
            value = defaultValue;
            data[setting] = value;
            newConfig = true;
            return (T)Convert.ChangeType(value, typeof(T));
        }

        private void SetConfigValue<T>(string category, string setting, T newValue)
        {
            var data = Config[category] as Dictionary<string, object>;
            object value;
            if (data != null && data.TryGetValue(setting, out value))
            {
                value = newValue;
                data[setting] = value;
                newConfig = true;
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
		{
			Config.Clear();
			LoadConfig();
		}

        void LoadConfig()
        {
            jackpot = GetConfigValue("Global", "Jackpot", 50000);
            DefaultWinRates = GetConfigValue("Global", "WinRate", DefaultPay());
            DefaultBasePoint = GetConfigValue("ServerRewards", "Match", DefaultSRPay());
            SRWinRates = GetConfigValue("ServerRewards", "WinPoint", DefautSRWinPay());
            SRjackpot = GetConfigValue("ServerRewards", "Jackpot", 10);
            SRMinBet = GetConfigValue("ServerRewards", "MinBet", 1000);
            MinBetjackpot = GetConfigValue("ServerRewards", "MinBetJackpot", 100000);
            MinBetjackpotEco = GetConfigValue("Global", "MinBetJackpot", 100000);
            SRJackpotNumber = GetConfigValue("ServerRewards", "JackpotMatch", 1869);
            JackpotNumber = GetConfigValue("Global", "JackpotMatch", 1058);
            DefaultMinRange = GetConfigValue("Global", "RollMinRange", 1000);
            DefaultMaxRange = GetConfigValue("Global", "RollMaxRange", 9999);
            UseSR = GetConfigValue("ServerRewards", "Enabled", false);
            AutoCloseAfterPlaceingBet = GetConfigValue("Global", "Place Bet Auto-close", false);
            UseNPC = GetConfigValue("HumanNPC", "Enabled", false);
            NPCID = GetConfigValue("HumanNPC", "npcID", new List<object>());
            BackgroundUrl = GetConfigValue("UI", "BackgroundMainURL",
                "http://wac.450f.edgecastcdn.net/80450F/kool1079.com/files/2016/05/RS2397_126989085.jpg");
            BackgroundColor = GetConfigValue("UI", "BackgroundMainColor",
                "0.1 0.1 0.1 1");
            WinBackgroundUrl = GetConfigValue("UI", "BackgroundWinURL",
                "http://wac.450f.edgecastcdn.net/80450F/kool1079.com/files/2016/05/RS2397_126989085.jpg");
            WinBackgroundColor = GetConfigValue("UI", "BackgroundWinColor",
                "0.1 0.1 0.1 1");
            anchorMin = GetConfigValue("CUI", "anchorMin", "0.8 0.2");
            anchorMax = GetConfigValue("CUI", "anchorMax", "1 0.8");
		    if (!newConfig) return;
		    SaveConfig();
		    newConfig = false;
        }

        static Dictionary<string, object> DefaultPay()
        {
            var d = new Dictionary<string, object>
            {
                { "111x", 1 },
                { "222x", 10 },
                { "333x", 50 },
                { "444x", 10 },
                { "555x", 75 },
                { "666x", 5 },
                { "777x", 75 },
                { "888x", 56 },
                { "999x", 42 },
                { "99x9", 52 },
                { "9x99", 57 },
                { "x999", 85 },
                { "99xx", 86 },
                { "9xxx", 86 }
            };
            return d;
        }

        static List<object> DefaultSRPay()
        {
            var d = new List<object>
            {
                "111x",
                "222x",
                "333x",
                "444x",
                "555x",
                "666x",
                "777x",
                "888x",
                "999x",
                "99x9",
                "9x99",
                "x999",
                "99xx",
                "9xxx"
            };
            return d;
        }

        static Dictionary<string, object> DefautSRWinPay()
        {
            var d = new Dictionary<string, object>
            {
                { "Match1Number", 1 },
                { "Match2Number", 2 },
                { "Match3Number", 3 },
                { "Match4Number", 4 }
            };
            return d;
        }

        #endregion

        #region data_init

        void LoadDefaultMessages()
        {
            var messages = new Dictionary<string, string>
            {
                {"NoPerm", "You don't have permission to do it."},
                {"NoWin", "You roll {0} but don't win anything."},
                {"NoPoint", "You don't have any point to use."},
                {"NoEconomy", "Economics isn't installed."},
                {"NoServerRewards", "Server Rewards isn't installed."},
                {"NotEnoughMoney", "You don't have enough money."},
                {"AddedNPC", "You successfully added the npc to the usable list."},
                {"NotInList", "The npc you are looking is not in the list."},
                {"RemovedNPC", "You successfully removed the npc to the usable list."},
                {"Win", "You roll {0} and won {1}$"},
                {"WinPoints", "You roll {0} and won {1} point(s)"},
                {"NoBet", "You must bet before."},
                {"NPCOnly", "You must find the NPC to use the lottery."},
                {"Balance", "Your current balance is {0}$"},
                {"BalanceSR", "Your current balance is {0} point(s)"},
                {"CurrentBet", "Your current bet is {0}$"},
                {"CurrentBetSR", "Your current bet is {0} point(s)"},
                {"Roll", "Roll {0} to win \nthe current jackpot:\n {1}$"},
                {"Jackpot", "You roll {0} and won the jackpot : {1}$ !!!!!!"},
                {"MiniSRBet", "You need to bet more to place bet. (Min: {0})"},
                {"BetMore", "If you had bet more you could win the jackpot. (Min: {0})"},
                {"MinimumSRBet", "Minimum bet of {0} to win the current jackpot: {1} point(s)"},
                {"CantOpen", "You must have Economics or ServerRewards installed and loaded."},
                {"WinRateText", "Win rate"},
                {"PointsText", "{0} : {1} point(s)"},
                {"MultiplierText", "Multiplier : x{0}"},
                {"BetmodifiersText", "Bet modifiers :"}
            };
            lang.RegisterMessages(messages, this);
            Puts("Messages loaded...");
        }

		void OnServerInitialized() {
            LoadConfig();
			LoadDefaultMessages();
            permission.RegisterPermission("Lottery.canuse", this);
            permission.RegisterPermission("Lottery.canconfig", this);
		    if (DefaultWinRates != null)
		    {
		        IndividualRates = new Dictionary<string, object>();
		        foreach (var entry in DefaultWinRates)
		        {
		            int rate;
		            if (!int.TryParse(entry.Value.ToString(), out rate)) continue;
		            IndividualRates.Add(entry.Key, rate);
		        }
		    }
		    if (SRWinRates != null)
		    {
		        var serverRewardsDict = SRWinRates;
		        SRRates = new Dictionary<string, int>();
		        if (serverRewardsDict != null)
		        {
		            foreach (var entry in serverRewardsDict)
		            {
		                int rate;
		                if (!int.TryParse(entry.Value.ToString(), out rate)) continue;
		                SRRates.Add(entry.Key, rate);
		            }
		        }
		    }

		    data = Interface.Oxide.DataFileSystem.GetFile(Name);
		    try
		    {
		        Currentbet = data.ReadObject<Dictionary<ulong, playerinfo>>();
		    }
		    catch (Exception e)
		    {
		        Currentbet = new Dictionary<ulong, playerinfo>();
		        Puts(e.Message);
		    }
		    data.WriteObject(Currentbet);
            serverinput = typeof(BasePlayer).GetField("serverInput", (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));
            eyesAdjust = new Vector3(0f, 1.5f, 0f);
		}

        void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                GUIDestroy(player);
                GUIDestroy(player);
            }
            Puts("Data saved.");
            if(Currentbet != null)
            SaveData(Currentbet);
        }
        #endregion
        
        #region save_data
        void SaveData(Dictionary<ulong, playerinfo> datas)
        {
            data.WriteObject(datas);
        }
        #endregion

        #region Lotery

        private CuiElement CreateImage(string panelName, bool win = false)
        {
            var element = new CuiElement();
            var url = !win ? BackgroundUrl : WinBackgroundUrl;
            var color = !win ? BackgroundColor : WinBackgroundColor;
            var image = new CuiRawImageComponent
            {
                Url = url,
                Color = color
            };

            var rectTransform = new CuiRectTransformComponent
            {
                AnchorMin = "0 0",
                AnchorMax = "1 1"
            };
            element.Components.Add(image);
            element.Components.Add(rectTransform);
            element.Name = CuiHelper.GetGuid();
            element.Parent = panelName;
            return element;
        }

        void RefreshUI(BasePlayer player, string[] args)
        {
            CuiHelper.DestroyUi(player, "containerLotery");
            CuiHelper.DestroyUi(player, "containerwinLotery");
            CuiHelper.DestroyUi(player, "ButtonBackLotery");
            CuiHelper.DestroyUi(player, "ButtonForwardLotery");

            if(Economy != null && Economy.IsLoaded && !UseSR)
                ShowLotery(player, args);
            else if(ServerRewards != null && ServerRewards.IsLoaded)
                ShowSrLotery(player,  args);
        }

        void GUIDestroy(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "MainContainer");
            CuiHelper.DestroyUi(player, "containerLotery");
            CuiHelper.DestroyUi(player, "containerwinLotery");
            CuiHelper.DestroyUi(player, "ButtonBackLotery");
            CuiHelper.DestroyUi(player, "ButtonForwardLotery");
        }
        
        void ShowLotery(BasePlayer player, string[] args)
        {
            if (Economy == null || !Economy.IsLoaded)
            {
                SendReply(player, lang.GetMessage("NoEconomy", this, player.UserIDString));
                return;
            }
            int from = 0;
            var currentBalance = Economy.Call("Balance", player.UserIDString);
            playerinfo playerbet;
            if(Currentbet == null)
                Currentbet = new Dictionary<ulong, playerinfo>();
            if (Currentbet.ContainsKey(player.userID))
            {
                Currentbet.TryGetValue(player.userID, out playerbet);
            }
            else
            {
                Currentbet.Add(player.userID, new playerinfo());
            }
            playerbet = Currentbet[player.userID];
            if (args != null && args.Length > 0 && playerbet != null)
            {
                if (args[0].Contains("less") || args[0].Contains("plus"))
                {
                    if (args[0].Contains("plus"))
                    {
                        if ((double)currentBalance >= playerbet.currentbet*(playerbet.multiplicator + 1))
                        {
                            int multiplier;
                            int.TryParse(args[1], out multiplier);
                            playerbet.multiplicator += multiplier;
                        }
                    }
                    if (args[0].Contains("less"))
                    {
                        if (playerbet.multiplicator > 1)
                            playerbet.multiplicator -= 1;
                    }
                }
                if (args[0].Contains("bet"))
                {
                    int bet;
                    int.TryParse(args[1], out bet);
                    if((double)currentBalance < (playerbet.currentbet+bet)*playerbet.multiplicator)
                        SendReply(player, lang.GetMessage("NotEnoughMoney", this, player.UserIDString));
                    else
                        playerbet.currentbet += bet;
                }
                if (args[0].Contains("page"))
                {
                    int.TryParse(args[1], out from);
                }
            }
            int i = 0;
            double jackpots = Math.Round(Currentbet.Sum(v => v.Value.totalbet));
            jackpots += jackpot;
            var win = new CuiElementContainer();
            var containerwin = win.Add(new CuiPanel
            {
                Image =
                {
                    Color = WinBackgroundColor
                },
                RectTransform =
                {
                    AnchorMin = anchorMin,
                    AnchorMax = anchorMax
                },
                CursorEnabled = true
            }, "Hud", "containerwinLotery");
            win.Add(new CuiLabel
            {
                Text =
                {
                    Text = lang.GetMessage("WinRateText", this, player.UserIDString),
                    FontSize = 18,
                    Align = TextAnchor.MiddleCenter
                },
                RectTransform =
                {
                    AnchorMin = "0.1 0.85",
                    AnchorMax = "0.9 1"
                }
            }, containerwin);
            var backgroundImageWin = CreateImage("containerwinLotery", true);
            win.Add(backgroundImageWin);
            foreach (var elem in IndividualRates)
            {
                if (i == 0)
                {
                    var pos = 0.81 - (i - from)/10.0;
                    var pos2 = 0.86 - (i - from)/20.0;
                    win.Add(new CuiLabel
                    {
                        Text =
                        {
                            Text = elem.Key + ": " + elem.Value + " %",
                            FontSize = 18,
                            Align = TextAnchor.MiddleCenter
                        },
                        RectTransform =
                        {
                            AnchorMin = $"{0.1} {pos}",
                            AnchorMax = $"{0.9} {pos2}"
                        }
                    }, containerwin);
                }
                else if (i >= from && i < from + 9)
                {
                    var pos = 0.81 - (i - from)/10.0;
                    var pos2 = 0.86 - (i - from)/20.0;
                    win.Add(new CuiLabel
                    {
                        Text =
                        {
                            Text = elem.Key + ": " + elem.Value + " %",
                            FontSize = 18,
                            Align = TextAnchor.MiddleCenter
                        },
                        RectTransform =
                        {
                            AnchorMin = $"{0.1} {pos}",
                            AnchorMax = $"{0.9} {pos2}"
                        }
                    }, containerwin);
                }
                i++;
            }
            var minfrom = from <= 10 ? 0 : from - 10;
            var maxfrom = from + 10 >= i ? from : from + 10;
            win.AddRange(ChangeBonusPage(minfrom, maxfrom));

            var elements = new CuiElementContainer();
        #region background
            var container = elements.Add(new CuiPanel
            {
                Image =
                {
                    Color = BackgroundColor
                },
                RectTransform =
                {
                    AnchorMin = "0 0.2",
                    AnchorMax = "0.8 0.8"
                },
                CursorEnabled = true
            }, "Hud", "containerLotery");
            var backgroundImage = CreateImage("containerLotery");

            elements.Add(backgroundImage);
        #endregion
        #region closebutton
            var closeButton = new CuiButton
            {
                Button =
                {
                    Command = "cmdDestroyUI",
                    Close = container,
                    Color = "0.8 0.8 0.8 0.2"
                },
                RectTransform =
                {
                    AnchorMin = "0.86 0.92",
                    AnchorMax = "0.97 0.98"
                },
                Text =
                {
                    Text = "X",
                    FontSize = 22,
                    Align = TextAnchor.MiddleCenter
                }
            };
            elements.Add(closeButton, container);
        #endregion
        #region currency
            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = string.Format(lang.GetMessage("Balance", this, player.UserIDString), currentBalance),
                    FontSize = 18,
                    Align = TextAnchor.MiddleCenter
                },
                RectTransform =
                {
                    AnchorMin = "0.1 0.91",
                    AnchorMax = "0.9 0.98"
                }
            }, container);
        #endregion
        #region multiplier

            if (playerbet != null)
            {
                elements.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = "Multiplier : x" + playerbet.multiplicator,
                        FontSize = 18,
                        Align = TextAnchor.MiddleCenter
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.05 0.81",
                        AnchorMax = "0.15 0.88"
                    }
                }, container);
                elements.Add(new CuiButton
                {
                    Button =
                    {
                        Command = "cmdLess Eco",
                        Close = container,
                        Color = "0.8 0.8 0.8 0.2"
                    },
                    Text =
                    {
                        Text = "-",
                        FontSize = 18,
                        Align = TextAnchor.MiddleCenter
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.2 0.81",
                        AnchorMax = "0.3 0.88"
                    }
                }, container);
                elements.Add(new CuiButton
                {
                    Button =
                    {
                        Command = "cmdPlus Eco",
                        Close = container,
                        Color = "0.8 0.8 0.8 0.2"
                    },
                    Text =
                    {
                        Text = "+",
                        FontSize = 18,
                        Align = TextAnchor.MiddleCenter
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.31 0.81",
                        AnchorMax = "0.41 0.88"
                    }
                }, container);
                #endregion
        #region bet
                elements.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = "Bet modifiers :",
                        FontSize = 18,
                        Align = TextAnchor.MiddleCenter
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.05 0.61",
                        AnchorMax = "0.15 0.68"
                    }
                }, container);
                elements.Add(new CuiButton
                {
                    Button =
                    {
                        Command = "cmdBet 1 Eco",
                        Close = container,
                        Color = "0.8 0.8 0.8 0.2"
                    },
                    Text =
                    {
                        Text = "+1",
                        FontSize = 18,
                        Align = TextAnchor.MiddleCenter
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.05 0.51",
                        AnchorMax = "0.15 0.58"
                    }
                }, container);
                elements.Add(new CuiButton
                {
                    Button =
                    {
                        Command = "cmdBet 5 Eco",
                        Close = container,
                        Color = "0.8 0.8 0.8 0.2"
                    },
                    Text =
                    {
                        Text = "+5",
                        FontSize = 18,
                        Align = TextAnchor.MiddleCenter
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.155 0.51",
                        AnchorMax = "0.255 0.58"
                    }
                }, container);
                elements.Add(new CuiButton
                {
                    Button =
                    {
                        Command = "cmdBet 10 Eco",
                        Close = container,
                        Color = "0.8 0.8 0.8 0.2"
                    },
                    Text =
                    {
                        Text = "+10",
                        FontSize = 18,
                        Align = TextAnchor.MiddleCenter
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.26 0.51",
                        AnchorMax = "0.36 0.58"
                    }
                }, container);
                elements.Add(new CuiButton
                {
                    Button =
                    {
                        Command = "cmdBet 100 Eco",
                        Close = container,
                        Color = "0.8 0.8 0.8 0.2"
                    },
                    Text =
                    {
                        Text = "+100",
                        FontSize = 18,
                        Align = TextAnchor.MiddleCenter
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.365 0.51",
                        AnchorMax = "0.485 0.58"
                    }
                }, container);
                elements.Add(new CuiButton
                {
                    Button =
                    {
                        Command = "cmdBet 1000 Eco",
                        Close = container,
                        Color = "0.8 0.8 0.8 0.2"
                    },
                    Text =
                    {
                        Text = "+1000",
                        FontSize = 18,
                        Align = TextAnchor.MiddleCenter
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.05 0.41",
                        AnchorMax = "0.15 0.48"
                    }
                }, container);
                elements.Add(new CuiButton
                {
                    Button =
                    {
                        Command = "cmdBet 10000 Eco",
                        Color = "0.8 0.8 0.8 0.2"
                    },
                    Text =
                    {
                        Text = "+10000",
                        FontSize = 18,
                        Align = TextAnchor.MiddleCenter
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.155 0.41",
                        AnchorMax = "0.255 0.48"
                    }
                }, container);
                elements.Add(new CuiButton
                {
                    Button =
                    {
                        Command = "cmdPlaceBet Eco",
                        Close = container,
                        Color = "0.8 0.8 0.8 0.2"
                    },
                    Text =
                    {
                        Text = "Place Bet",
                        FontSize = 18,
                        Align = TextAnchor.MiddleCenter
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.05 0.31",
                        AnchorMax = "0.255 0.38"
                    }
                }, container);

                elements.Add(new CuiButton
                {
                    Button =
                    {
                        Command = "cmdResetBet lot",
                        Color = "0.8 0.8 0.8 0.2"
                    },
                    Text =
                    {
                        Text = "Reset Bet",
                        FontSize = 18,
                        Align = TextAnchor.MiddleCenter
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.05 0.21",
                        AnchorMax = "0.255 0.28"
                    }
                }, container);

                #endregion
                #region winpart
                elements.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = string.Format(lang.GetMessage("CurrentBet", this, player.UserIDString), playerbet.currentbet*playerbet.multiplicator),
                        FontSize = 18,
                        Align = TextAnchor.MiddleCenter
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.71 0.71",
                        AnchorMax = "0.99 0.81"
                    }
                }, container);
                elements.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = string.Format(lang.GetMessage("Roll", this, player.UserIDString), JackpotNumber, jackpots),
                        FontSize = 18,
                        Align = TextAnchor.MiddleCenter
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.71 0.39",
                        AnchorMax = "0.99 0.59"
                    }
                }, container);
            }
            #endregion
            CuiHelper.AddUi(player, elements);
            CuiHelper.AddUi(player, win);
            Currentbet.Remove(player.userID);
            Currentbet.Add(player.userID, playerbet);
            SaveData(Currentbet);
        }
        
        void ShowSrLotery(BasePlayer player, string[] args)
        {
            if (ServerRewards == null || !ServerRewards.IsLoaded)
            {
                SendReply(player, lang.GetMessage("NoServerRewards", this, player.UserIDString));
                return;
            }
            int from = 0;
            var currentBalance = ServerRewards.Call("CheckPoints", player.userID);
            if (currentBalance != null)
            {
                playerinfo playerbet;
                if (Currentbet.ContainsKey(player.userID))
                {
                    Currentbet.TryGetValue(player.userID, out playerbet);
                }
                else
                {
                    Currentbet.Add(player.userID, new playerinfo());
                    Currentbet.TryGetValue(player.userID, out playerbet);
                }
                playerbet = Currentbet[player.userID];
                if (playerbet != null && args != null && args.Length > 0)
                {
                    if (args[0].Contains("less") || args[0].Contains("plus"))
                    {
                        if (args[0].Contains("plus"))
                        {
                            if ((int) currentBalance >= playerbet.currentbet*(playerbet.multiplicator + 1))
                            {
                                int multiplier;
                                int.TryParse(args[1], out multiplier);
                                playerbet.multiplicator += multiplier;
                            }
                        }
                        if (args[0].Contains("less"))
                        {
                            if (playerbet.multiplicator > 1)
                                playerbet.multiplicator -= 1;
                        }
                    }
                    if (args[0].Contains("bet"))
                    {
                        int bet;
                        int.TryParse(args[1], out bet);
                        if ((int) currentBalance < (playerbet.currentbet + bet)*playerbet.multiplicator)
                            SendReply(player, lang.GetMessage("NotEnoughMoney", this, player.UserIDString));
                        else playerbet.currentbet += bet;
                    }
                    if (args[0].Contains("page"))
                    {
                        int.TryParse(args[1], out @from);
                    }
                }
                var i = 0;
                double jackpots = Math.Round(Currentbet.Sum(v => v.Value.totalbet));
                jackpots += jackpot;
                var win = new CuiElementContainer();
                var containerwinlocal = win.Add(new CuiPanel
                {
                    Image =
                    {
                        Color = WinBackgroundColor
                    },
                    RectTransform =
                    {
                        AnchorMin = anchorMin,
                        AnchorMax = anchorMax
                    },
                    CursorEnabled = true
                }, "Hud", "containerwinLotery");
                win.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = lang.GetMessage("WinRateText", this, player.UserIDString),
                        FontSize = 18,
                        Align = TextAnchor.MiddleCenter
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.1 0.85",
                        AnchorMax = "0.9 1"
                    }
                }, containerwinlocal);
                var backgroundImageWin = CreateImage("containerwinLotery", true);
                win.Add(backgroundImageWin);
                foreach (var elem in SRRates)
                {
                    var pos = 0.86 - (i - @from)/10.0;
                    var pos2 = 0.91 - (i - @from)/20.0;
                    win.Add(new CuiLabel
                    {
                        Text =
                        {
                            Text = lang.GetMessage("PointsText", this, player.UserIDString).Replace("{0}", elem.Key).Replace("{1}", elem.Value.ToString()),
                            FontSize = 18,
                            Align = TextAnchor.MiddleCenter
                        },
                        RectTransform =
                        {
                            AnchorMin = $"{0.1} {pos}",
                            AnchorMax = $"{0.9} {pos2}"
                        }
                    }, containerwinlocal);
                    i++;
                }
                var elements = new CuiElementContainer();
                #region background
                var container = elements.Add(new CuiPanel
                {
                    Image =
                    {
                        Color = BackgroundColor
                    },
                    RectTransform =
                    {
                        AnchorMin = "0 0.2",
                        AnchorMax = "0.8 0.8"
                    },
                    CursorEnabled = true
                }, "Hud", "containerLotery");
                var backgroundImage = CreateImage("containerLotery");
                elements.Add(backgroundImage);
                #endregion
                #region closebutton
                var closeButton = new CuiButton
                {
                    Button =
                    {
                        Command = "cmdDestroyUI",
                        Close = container,
                        Color = "0.8 0.8 0.8 0.2"
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.86 0.92",
                        AnchorMax = "0.97 0.98"
                    },
                    Text =
                    {
                        Text = "X",
                        FontSize = 22,
                        Align = TextAnchor.MiddleCenter
                    }
                };
                elements.Add(closeButton, container);
                #endregion
                #region currency
                elements.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = string.Format(lang.GetMessage("BalanceSR", this, player.UserIDString), currentBalance),
                        FontSize = 18,
                        Align = TextAnchor.MiddleCenter
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.1 0.91",
                        AnchorMax = "0.9 0.98"
                    }
                }, container);
                #endregion
                #region multiplier
                elements.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = lang.GetMessage("MultiplierText", this, player.UserIDString).Replace("{0}", playerbet.multiplicator.ToString()),
                        FontSize = 18,
                        Align = TextAnchor.MiddleLeft
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.05 0.81",
                        AnchorMax = "0.30 0.88"
                    }
                }, container);
                elements.Add(new CuiButton
                {
                    Button =
                    {
                        Command = "cmdLess",
                        Close = container,
                        Color = "0.8 0.8 0.8 0.2"
                    },
                    Text =
                    {
                        Text = "-",
                        FontSize = 18,
                        Align = TextAnchor.MiddleCenter
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.2 0.81",
                        AnchorMax = "0.3 0.88"
                    }
                }, container);
                elements.Add(new CuiButton
                {
                    Button =
                    {
                        Command = "cmdPlus",
                        Close = container,
                        Color = "0.8 0.8 0.8 0.2"
                    },
                    Text =
                    {
                        Text = "+",
                        FontSize = 18,
                        Align = TextAnchor.MiddleCenter
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.31 0.81",
                        AnchorMax = "0.41 0.88"
                    }
                }, container);
                #endregion
                #region bet
                elements.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = lang.GetMessage("BetmodifiersText", this, player.UserIDString),
                        FontSize = 18,
                        Align = TextAnchor.MiddleCenter
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.05 0.61",
                        AnchorMax = "0.15 0.68"
                    }
                }, container);
                elements.Add(new CuiButton
                {
                    Button =
                    {
                        Command = "cmdBet 1",
                        Close = container,
                        Color = "0.8 0.8 0.8 0.2"
                    },
                    Text =
                    {
                        Text = "+1",
                        FontSize = 18,
                        Align = TextAnchor.MiddleCenter
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.05 0.51",
                        AnchorMax = "0.15 0.58"
                    }
                }, container);
                elements.Add(new CuiButton
                {
                    Button =
                    {
                        Command = "cmdBet 5",
                        Close = container,
                        Color = "0.8 0.8 0.8 0.2"
                    },
                    Text =
                    {
                        Text = "+5",
                        FontSize = 18,
                        Align = TextAnchor.MiddleCenter
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.155 0.51",
                        AnchorMax = "0.255 0.58"
                    }
                }, container);
                elements.Add(new CuiButton
                {
                    Button =
                    {
                        Command = "cmdBet 10",
                        Close = container,
                        Color = "0.8 0.8 0.8 0.2"
                    },
                    Text =
                    {
                        Text = "+10",
                        FontSize = 18,
                        Align = TextAnchor.MiddleCenter
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.26 0.51",
                        AnchorMax = "0.36 0.58"
                    }
                }, container);
                elements.Add(new CuiButton
                {
                    Button =
                    {
                        Command = "cmdBet 100",
                        Close = container,
                        Color = "0.8 0.8 0.8 0.2"
                    },
                    Text =
                    {
                        Text = "+100",
                        FontSize = 18,
                        Align = TextAnchor.MiddleCenter
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.365 0.51",
                        AnchorMax = "0.485 0.58"
                    }
                }, container);
                elements.Add(new CuiButton
                {
                    Button =
                    {
                        Command = "cmdBet 1000",
                        Close = container,
                        Color = "0.8 0.8 0.8 0.2"
                    },
                    Text =
                    {
                        Text = "+1000",
                        FontSize = 18,
                        Align = TextAnchor.MiddleCenter
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.05 0.41",
                        AnchorMax = "0.15 0.48"
                    }
                }, container);
                elements.Add(new CuiButton
                {
                    Button =
                    {
                        Command = "cmdBet 10000",
                        Close = container,
                        Color = "0.8 0.8 0.8 0.2"
                    },
                    Text =
                    {
                        Text = "+10000",
                        FontSize = 18,
                        Align = TextAnchor.MiddleCenter
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.155 0.41",
                        AnchorMax = "0.255 0.48"
                    }
                }, container);
                
                elements.Add(new CuiButton
                {
                    Button =
                    {
                        Command = "cmdPlaceBet",
                        Color = "0.8 0.8 0.8 0.2"
                    },
                    Text =
                    {
                        Text = "Place Bet",
                        FontSize = 18,
                        Align = TextAnchor.MiddleCenter
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.05 0.31",
                        AnchorMax = "0.255 0.38"
                    }
                }, container);

                elements.Add(new CuiButton
                {
                    Button =
                    {
                        Command = "cmdResetBet",
                        Color = "0.8 0.8 0.8 0.2"
                    },
                    Text =
                    {
                        Text = "Reset Bet",
                        FontSize = 18,
                        Align = TextAnchor.MiddleCenter
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.05 0.21",
                        AnchorMax = "0.255 0.28"
                    }
                }, container);

                #endregion
                #region winpart
                elements.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = string.Format(lang.GetMessage("CurrentBetSR", this, player.UserIDString), playerbet.currentbet*playerbet.multiplicator),
                        FontSize = 18,
                        Align = TextAnchor.MiddleCenter
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.71 0.71",
                        AnchorMax = "0.99 0.81"
                    }
                }, container);

                if (UseSR && ServerRewards.IsLoaded)
                {
                    var mini = string.Format(lang.GetMessage("MinimumSRBet", this, player.UserIDString), MinBetjackpot, SRjackpot);
                    elements.Add(new CuiLabel
                    {
                        Text =
                        {
                            Text = mini,
                            FontSize = 18,
                            Align = TextAnchor.MiddleCenter
                        },
                        RectTransform =
                        {
                            AnchorMin = "0.71 0.39",
                            AnchorMax = "0.99 0.59"
                        }
                    }, container);
                }
                else
                {
                    elements.Add(new CuiLabel
                    {
                        Text =
                        {
                            Text = string.Format(lang.GetMessage("Roll", this, player.UserIDString), JackpotNumber, jackpots),
                            FontSize = 18,
                            Align = TextAnchor.MiddleCenter
                        },
                        RectTransform =
                        {
                            AnchorMin = "0.71 0.39",
                            AnchorMax = "0.99 0.59"
                        }
                    }, container);
                }
                #endregion
                CuiHelper.AddUi(player, elements);
                CuiHelper.AddUi(player, win);
                Currentbet.Remove(player.userID);
                Currentbet.Add(player.userID, playerbet);
            }
            else
                SendReply(player, lang.GetMessage("NoPoint", this, player.UserIDString));
            SaveData(Currentbet);
        }
        
        private static CuiElementContainer ChangeBonusPage(int pageless, int pagemore)
        {
            return new CuiElementContainer
            {
                {
                    new CuiButton
                    {
                        Button = {Command = $"cmdPage page {pageless}", Color = "0.5 0.5 0.5 0.2"},
                        RectTransform = {AnchorMin = "0.83 0.25", AnchorMax = "0.91 0.3"},
                        Text = {Text = "<<", FontSize = 20, Align = TextAnchor.MiddleCenter}
                    },
                    "Hud",
                    "ButtonBackLotery"
                },
                {
                    new CuiButton
                    {
                        Button = {Command = $"cmdPage page {pagemore}", Color = "0.5 0.5 0.5 0.2"},
                        RectTransform = {AnchorMin = "0.92 0.25", AnchorMax = "1 0.30"},
                        Text = {Text = ">>", FontSize = 20, Align = TextAnchor.MiddleCenter}
                    },
                    "Hud",
                    "ButtonForwardLotery"
                }
            };
        }
        #endregion

        #region reward

        private object FindReward(BasePlayer player, int bet, int reference, int multiplicator = 1)
        {
            object findReward = 0;
            double reward = 0;
            int[] number = GetIntArray(reference);
            string newreference;
            if (UseSR && ServerRewards.IsLoaded)
            {
                #region jackpot

                if (reference == SRJackpotNumber)
                {
                    if (bet*multiplicator >= MinBetjackpot)
                    {
                        findReward = (int)findReward*bet*multiplicator + (int) SRjackpot;
                        return (int)findReward;
                    }
                    SendReply(player, string.Format(lang.GetMessage("BetMore", this, player.UserIDString), MinBetjackpot));
                }

                #endregion

                #region full_match
                if (DefaultBasePoint.Contains(reference.ToString()))
                {
                    SRWinRates.TryGetValue("Match4Number", out findReward);
                    return (int)findReward*bet*multiplicator;
                }

                #endregion

                #region three_match

                newreference = number[0].ToString() + number[1].ToString() + number[2].ToString() + "x";
                if (DefaultBasePoint.Contains(newreference))
                {
                    SRWinRates.TryGetValue("Match3Number", out findReward);
                    return (int)findReward*bet*multiplicator;
                }
                newreference = number[0].ToString() + number[1].ToString() + "x" + number[3].ToString();
                if (DefaultBasePoint.Contains(newreference))
                {
                    SRWinRates.TryGetValue("Match4Number", out findReward);
                    return (int)findReward*bet*multiplicator;
                }
                newreference = number[0].ToString() + "x" + number[2].ToString() + number[3].ToString();
                if (DefaultBasePoint.Contains(newreference))
                {
                    SRWinRates.TryGetValue("Match4Number", out findReward);
                    return (int)findReward*bet*multiplicator;
                }
                newreference = "x" + number[1].ToString() + number[2].ToString() + number[3].ToString();
                if (DefaultBasePoint.Contains(newreference))
                {
                    SRWinRates.TryGetValue("Match4Number", out findReward);
                    return (int)findReward*bet*multiplicator;
                }

                #endregion

                #region two_match

                newreference = number[0].ToString() + number[1].ToString() + "x" + "x";
                if (DefaultBasePoint.Contains(newreference))
                {
                    SRWinRates.TryGetValue("Match2Number", out findReward);
                    return (int)findReward*bet*multiplicator;
                }
                newreference = number[0].ToString() + "x" + "x" + number[3].ToString();
                if (DefaultBasePoint.Contains(newreference))
                {
                    SRWinRates.TryGetValue("Match2Number", out findReward);
                    return (int)findReward*bet*multiplicator;
                }
                newreference = "x" + "x" + number[2].ToString() + number[3].ToString();
                if (DefaultBasePoint.Contains(newreference))
                {
                    SRWinRates.TryGetValue("Match2Number", out findReward);
                    return (int)findReward*bet*multiplicator;
                }
                newreference = number[0].ToString() + "x" + number[2].ToString() + "x";
                if (DefaultBasePoint.Contains(newreference))
                {
                    SRWinRates.TryGetValue("Match2Number", out findReward);
                    return (int)findReward*bet*multiplicator;
                }
                newreference = "x" + number[1].ToString() + "x" + number[3].ToString();
                if (DefaultBasePoint.Contains(newreference))
                {
                    SRWinRates.TryGetValue("Match2Number", out findReward);
                    return (int)findReward*bet*multiplicator;
                }
                newreference = "x" + number[1].ToString() + number[2].ToString() + "x";
                if (DefaultBasePoint.Contains(newreference))
                {
                    SRWinRates.TryGetValue("Match2Number", out findReward);
                    return (int)findReward*bet*multiplicator;
                }

                #endregion

                #region one_match

                newreference = number[0].ToString() + "x" + "x" + "x";
                if (DefaultBasePoint.Contains(newreference))
                {
                    SRWinRates.TryGetValue("Match1Number", out findReward);
                    return (int)findReward*bet*multiplicator;
                }
                newreference = "x" + number[1].ToString() + "x" + "x";
                if (DefaultBasePoint.Contains(newreference))
                {
                    SRWinRates.TryGetValue("Match1Number", out findReward);
                    return (int)findReward*bet*multiplicator;
                }
                newreference = "x" + "x" + number[2].ToString() + "x";
                if (DefaultBasePoint.Contains(newreference))
                {
                    SRWinRates.TryGetValue("Match1Number", out findReward);
                    return (int)findReward*bet*multiplicator;
                }
                newreference = "x" + "x" + "x" + number[3].ToString();
                if (DefaultBasePoint.Contains(newreference))
                {
                    SRWinRates.TryGetValue("Match1Number", out findReward);
                    return (int)findReward*bet*multiplicator;
                }

                #endregion

                return findReward;
            }
            if(!UseSR && Economy.IsLoaded)
            {
                object rws = 0;
                #region jackpot
                if (reference == JackpotNumber)
                {
                    if (bet*multiplicator >= MinBetjackpotEco)
                    {
                        int jackpots = (int) Math.Round(Currentbet.Sum(v => v.Value.totalbet));
                        return bet + bet*multiplicator + jackpots + Convert.ToInt32(jackpot);
                    }
                    SendReply(player, string.Format(lang.GetMessage("BetMore", this, player.UserIDString), MinBetjackpotEco));
                }
                #endregion

                #region full_match

                if (IndividualRates.ContainsKey(reference.ToString()))
                {
                    IndividualRates.TryGetValue(reference.ToString(), out rws);
                    reward = (bet+bet*(Convert.ToInt32(rws)/100.0d)) * multiplicator;
                    return reward;
                }

                #endregion

                #region three_match
                newreference = number[0].ToString() + number[1].ToString() + number[2].ToString() + "x";
                if(IndividualRates.ContainsKey(newreference))
                {
                    IndividualRates.TryGetValue(newreference, out rws);
                    reward = (bet+bet*(Convert.ToInt32(rws)/100.0d)) * multiplicator;
                    return reward;
                }
                newreference = number[0].ToString() + number[1].ToString() + "x" + number[3].ToString();
                if(IndividualRates.ContainsKey(newreference))
                {
                    IndividualRates.TryGetValue(newreference, out rws);
                    reward = (bet+bet*(Convert.ToInt32(rws)/100.0d)) * multiplicator;
                    return reward;
                }
                newreference = number[0].ToString() + "x" + number[2].ToString() + number[3].ToString();
                if(IndividualRates.ContainsKey(newreference))
                {
                    IndividualRates.TryGetValue(newreference, out rws);
                    reward = (bet+bet*(Convert.ToInt32(rws)/100.0d)) * multiplicator;
                    return reward;
                }
                newreference =  "x" + number[1].ToString() + number[2].ToString() + number[3].ToString();
                if(IndividualRates.ContainsKey(newreference))
                {
                    IndividualRates.TryGetValue(newreference, out rws);
                    reward = (bet+bet*(Convert.ToInt32(rws)/100.0d)) * multiplicator;
                    return reward;
                }
                #endregion

                #region two_match
                newreference = number[0].ToString() + number[1].ToString() + "x" + "x";
                if(IndividualRates.ContainsKey(newreference))
                {
                    IndividualRates.TryGetValue(newreference, out rws);
                    reward = (bet+bet*(Convert.ToInt32(rws)/100.0d)) * multiplicator;
                    return reward;
                }
                newreference = number[0].ToString() + "x" + "x" + number[3].ToString();
                if(IndividualRates.ContainsKey(newreference))
                {
                    IndividualRates.TryGetValue(newreference, out rws);
                    reward = (bet+bet*(Convert.ToInt32(rws)/100.0d)) * multiplicator;
                    return reward;
                }
                newreference =  "x" + "x" + number[2].ToString() + number[3].ToString();
                if(IndividualRates.ContainsKey(newreference))
                {
                    IndividualRates.TryGetValue(newreference, out rws);
                    reward = (bet+bet*(Convert.ToInt32(rws)/100.0d)) * multiplicator;
                    return reward;
                }
                newreference = number[0].ToString() + "x" + number[2].ToString() + "x";
                if(IndividualRates.ContainsKey(newreference))
                {
                    IndividualRates.TryGetValue(newreference, out rws);
                    reward = (bet+bet*(Convert.ToInt32(rws)/100.0d)) * multiplicator;
                    return reward;
                }
                newreference = "x" + number[1].ToString() + "x" + number[3].ToString();
                if(IndividualRates.ContainsKey(newreference))
                {
                    IndividualRates.TryGetValue(newreference, out rws);
                    reward = (bet+bet*(Convert.ToInt32(rws)/100.0d)) * multiplicator;
                    return reward;
                }
                newreference =  "x" + number[1].ToString() + number[2].ToString() + "x";
                if(IndividualRates.ContainsKey(newreference))
                {
                    IndividualRates.TryGetValue(newreference, out rws);
                    reward = (bet+bet*(Convert.ToInt32(rws)/100.0d)) * multiplicator;
                    return reward;
                }

                #endregion

                #region one_match
                newreference = number[0].ToString() + "x" + "x" + "x";
                if(IndividualRates.ContainsKey(newreference))
                {
                    IndividualRates.TryGetValue(newreference, out rws);
                    reward = (bet+bet*(Convert.ToInt32(rws)/100.0d)) * multiplicator;
                    return reward;
                }
                newreference =  "x" + number[1].ToString() + "x" + "x";
                if(IndividualRates.ContainsKey(newreference))
                {
                    IndividualRates.TryGetValue(newreference, out rws);
                    reward = (bet+bet*(Convert.ToInt32(rws)/100.0d)) * multiplicator;
                    return reward;
                }
                newreference =  "x" + "x" + number[2].ToString() + "x";
                if(IndividualRates.ContainsKey(newreference))
                {
                    IndividualRates.TryGetValue(newreference, out rws);
                    reward = (bet+bet*(Convert.ToInt32(rws)/100.0d)) * multiplicator;
                    return reward;
                }
                newreference = "x" + "x" + "x" + number[3].ToString();
                if(IndividualRates.ContainsKey(newreference))
                {
                    IndividualRates.TryGetValue(newreference, out rws);
                    reward = (bet+bet*(Convert.ToInt32(rws)/100.0d)) * multiplicator;
                    return reward;
                }

                #endregion
                return rws;
            }
            return null;
        }
        #endregion

        #region Command
        [ChatCommand("lot")]
        private void cmdLotery(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "Lottery.canuse"))
            {
                SendReply(player, string.Format(lang.GetMessage("NoPerm", this, player.UserIDString)));
                return;
            }
            if (args.Length != 0)
            {
                if (args[0].ToLower() == "add")
                {
                    if (!permission.UserHasPermission(player.UserIDString, "Lottery.canconfig"))
                    {
                        SendReply(player, string.Format(lang.GetMessage("NoPerm", this, player.UserIDString)));
                        return;
                    }
                    var input = serverinput.GetValue(player) as InputState;
                    var currentRot = Quaternion.Euler(input.current.aimAngles) * Vector3.forward;
                    var target = DoRay(player.transform.position + eyesAdjust, currentRot);
                    if (!(target is bool) && target is BasePlayer)
                    {
                        var bases = target as BasePlayer;
                        NPCID.Add(bases.UserIDString);
                        SetConfigValue("HumanNPC", "npcID", NPCID);
                        SendReply(player, lang.GetMessage("AddedNPC", this, player.UserIDString));
                        return;
                    }
                }
                if (args[0].ToLower() == "remove")
                {
                    if (!permission.UserHasPermission(player.UserIDString, "Lottery.canconfig"))
                    {
                        SendReply(player, string.Format(lang.GetMessage("NoPerm", this, player.UserIDString)));
                        return;
                    }
                    var input = serverinput.GetValue(player) as InputState;
                    var currentRot = Quaternion.Euler(input.current.aimAngles) * Vector3.forward;
                    var target = DoRay(player.transform.position + eyesAdjust, currentRot);
                    if (!(target is bool) && target is BasePlayer)
                    {
                        var bases = target as BasePlayer;
                        if (NPCID.Contains(bases.UserIDString))
                        {
                            NPCID.Remove(bases.UserIDString);
                            SetConfigValue("HumanNPC", "npcID", NPCID);
                            SendReply(player, lang.GetMessage("RemovedNPC", this, player.UserIDString));
                        }
                        else
                            SendReply(player, lang.GetMessage("NotInList", this, player.UserIDString));
                        return;
                    }
                }
            }
            if (UseNPC)
            {
                SendReply(player, string.Format(lang.GetMessage("NPCOnly", this, player.UserIDString)));
                return;
            }
            if (Economy != null && Economy.IsLoaded && !UseSR)
            {
                var win = new CuiElementContainer();
                win.Add(new CuiPanel
                {
                    Image =
                    {
                        Color = WinBackgroundColor
                    },
                    RectTransform =
                    {
                        AnchorMin = anchorMin,
                        AnchorMax = anchorMax
                    },
                    CursorEnabled = true
                }, "Hud", MainContainer);
                CuiHelper.AddUi(player, win);
                ShowLotery(player, null);
            }
            else if (ServerRewards != null && ServerRewards.IsLoaded && UseSR)
            {
                var win = new CuiElementContainer();
                win.Add(new CuiPanel
                {
                    Image =
                    {
                        Color = WinBackgroundColor
                    },
                    RectTransform =
                    {
                        AnchorMin = anchorMin,
                        AnchorMax = anchorMax
                    },
                    CursorEnabled = true
                }, "Hud", MainContainer);
                CuiHelper.AddUi(player, win);
                ShowSrLotery(player, null);
            }
            else
            {
                SendReply(player, lang.GetMessage("CantOpen", this, player.UserIDString));
            }
        }

        void OnUseNPC(BasePlayer npc, BasePlayer player, Vector3 destination)
        {
            if (NPCID != null && NPCID.Contains(npc.UserIDString) && UseNPC)
            {
                RefreshUI(player, null);
            }
        }

        [ConsoleCommand("cmdDestroyUI")]
        void cmdDestroyUI(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null) return;
            GUIDestroy(arg.Player());
        }

        [ConsoleCommand("cmdLess")]
        void cmdLess(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null) return;
            RefreshUI(arg.Player(), new[] {"less", "-1"});
        }

        [ConsoleCommand("cmdBet")]
        void cmdBet(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null) return;
            RefreshUI(arg.Player(), new[] {"bet", arg.Args[0]});
        }

        [ConsoleCommand("cmdPlus")]
        void cmdPlus(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null) return;
            RefreshUI(arg.Player(), new[] {"plus", "1"});
        }

        [ConsoleCommand("cmdPage")]
        void cmdPage(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null) return;
            RefreshUI(arg.Player(), new[] {"page", arg.Args[1]});
        }

        [ConsoleCommand("cmdPlaceBet")]
        void cmdPlaceBet(ConsoleSystem.Arg arg)
        {
            Dictionary<ulong, playerinfo> playerinfos = new Dictionary<ulong, playerinfo>();
            if (arg.Player() == null) return;
            GUIDestroy(arg.Player());
            playerinfo playerbet = new playerinfo();
            if (Currentbet == null)
                return;
            if (!Currentbet.ContainsKey(arg.Player().userID))
            {
                Currentbet.Add(arg.Player().userID, new playerinfo());
            }
            else
            {
                Currentbet.TryGetValue(arg.Player().userID, out playerbet);
            }
            if (playerbet != null && Math.Abs(playerbet.currentbet) < 0.0000001)
            {
                SendReply(arg.Player(), lang.GetMessage("NoBet", this, arg.Player().UserIDString));
                return;
            }
            int random = UnityEngine.Random.Range(DefaultMinRange, DefaultMaxRange);
            if (UseSR && ServerRewards != null && ServerRewards.IsLoaded)
            {
                int rwd;
                if (playerbet != null)
                {
                    var reward = FindReward(arg.Player(), (int)playerbet.currentbet, random, playerbet.multiplicator);
                    if (SRMinBet <= playerbet.currentbet*playerbet.multiplicator)
                    {
                        if (reward != null && Convert.ToInt32(reward) != 0)
                        {
                            rwd = (int)reward;
                            if (playerbet.currentbet*playerbet.multiplicator >= MinBetjackpot)
                                if (random == SRJackpotNumber)
                                {
                                    foreach (var resetbet in Currentbet)
                                    {
                                        resetbet.Value.totalbet = 0;
                                        resetbet.Value.multiplicator = 1;
                                        playerinfos.Add(resetbet.Key, resetbet.Value);
                                    }
                                    Currentbet.Clear();
                                    Currentbet = playerinfos;
                                    ServerRewards?.Call("AddPoints", new object[] {arg.Player().userID, rwd});
                                    SendReply(arg.Player(),
                                        string.Format(lang.GetMessage("Jackpot", this, arg.Player().UserIDString), random,
                                            rwd));
                                    return;
                                }
                            if (Math.Abs(rwd) > 0 && random != SRJackpotNumber)
                            {
                                Currentbet.Remove(arg.Player().userID);
                                Currentbet.Add(arg.Player().userID, playerbet);
                                ServerRewards?.Call("AddPoints", new object[] {arg.Player().userID, rwd});
                                SendReply(arg.Player(),
                                    string.Format(lang.GetMessage("WinPoints", this, arg.Player().UserIDString), random,
                                                rwd));
                            }
                            else
                            {
                                ServerRewards?.Call("AddPoints", new object[] {arg.Player().userID, rwd});
                                SendReply(arg.Player(),
                                    string.Format(lang.GetMessage("WinPoints", this, arg.Player().UserIDString), random,
                                        rwd));
                            }

                            playerbet.totalbet += playerbet.currentbet*(10/100.0);
                            ServerRewards?.Call("TakePoints", arg.Player().userID, (int)playerbet.currentbet*playerbet.multiplicator);
                            playerbet.currentbet = 0;
                            playerbet.multiplicator = 1;
                        }
                        else
                        {
                            playerbet.totalbet += playerbet.currentbet*(10/100.0);
                            ServerRewards?.Call("TakePoints", arg.Player().userID, (int)playerbet.currentbet*playerbet.multiplicator);
                            playerbet.currentbet = 0;
                            playerbet.multiplicator = 1;
                            SendReply(arg.Player(), string.Format(lang.GetMessage("NoWin", this, arg.Player().UserIDString), random));
                        }
                    }
                    else
                    {
                        SendReply(arg.Player(), string.Format(lang.GetMessage("MiniSRBet", this, arg.Player().UserIDString), SRMinBet));
                    }
                }
            }
            else if(!UseSR && Economy != null && Economy.IsLoaded)
            {
                if (playerbet != null)
                {
                    var reward = FindReward(arg.Player(), (int)playerbet.currentbet, random, playerbet.multiplicator);
                    if (reward != null && Convert.ToInt32(reward) != 0)
                    {
                        var rwd = (double) reward;
                        if (playerbet.currentbet*playerbet.multiplicator >= MinBetjackpotEco)
                            if (random == JackpotNumber)
                            {
                                foreach (var resetbet in Currentbet)
                                {
                                    resetbet.Value.totalbet = 0;
                                    resetbet.Value.multiplicator = 1;
                                    playerinfos.Add(resetbet.Key, resetbet.Value);
                                }
                                Currentbet.Clear();
                                Currentbet = playerinfos;
                                Economy?.CallHook("Deposit", arg.Player().UserIDString, rwd);
                                SendReply(arg.Player(),
                                    string.Format(lang.GetMessage("Jackpot", this, arg.Player().UserIDString), random,
                                        rwd));
                                return;
                            }
                        if (Math.Abs(rwd) > 0 && random != JackpotNumber)
                        {
                            Currentbet.Remove(arg.Player().userID);
                            Currentbet.Add(arg.Player().userID, playerbet);
                            Economy?.CallHook("Deposit", arg.Player().UserIDString, rwd);
                            SendReply(arg.Player(),
                                string.Format(lang.GetMessage("Win", this, arg.Player().UserIDString), random, rwd));
                        }
                        else
                        {
                            Economy?.CallHook("Deposit", arg.Player().UserIDString, rwd);
                            SendReply(arg.Player(),
                                string.Format(lang.GetMessage("Win", this, arg.Player().UserIDString), random, rwd));
                        }
                        playerbet.totalbet += playerbet.currentbet*(10/100.0);
                        Economy?.CallHook("Withdraw", arg.Player().UserIDString, playerbet.currentbet*playerbet.multiplicator);
                        playerbet.currentbet = 0;
                        playerbet.multiplicator = 1;
                    }
                    else
                    {
                        playerbet.totalbet += playerbet.currentbet*(10/100.0);
                        Economy?.CallHook("Withdraw", arg.Player().UserIDString, playerbet.currentbet*playerbet.multiplicator);
                        playerbet.currentbet = 0;
                        playerbet.multiplicator = 1;
                        SendReply(arg.Player(),
                            string.Format(lang.GetMessage("NoWin", this, arg.Player().UserIDString), random));
                    }
                }
            }
            SaveData(Currentbet);
            if (!AutoCloseAfterPlaceingBet)
            {
                if (arg.Args != null && arg.Args.Length > 0)
                    ShowLotery(arg.Player(), new[] {"bet", arg.Args[0]});
                else
                    ShowSrLotery(arg.Player(), null);
            }
        }

        [ConsoleCommand("cmdResetBet")]
        void cmdResetBet(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null)
            {
                Puts(arg.FullString);
                BasePlayer player = arg.Player();
                GUIDestroy(player);
                playerinfo playerbet;
                Currentbet.TryGetValue(player.userID, out playerbet);
                if(playerbet != null)
                Currentbet[player.userID].currentbet = 0;
                if (arg.Args != null && arg.Args.Length > 0)
                    ShowLotery(arg.Player(), null);
                else
                    ShowSrLotery(arg.Player(), null);
            }
        }
        #endregion
    }
}