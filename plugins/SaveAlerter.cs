using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("SaveAlerter", "PsychoTea", "1.0.0")]
    [Description("A small save icon when the server saves.")]

    class SaveAlerter : RustPlugin
    {
        string ImageURL;
        string ImagePosMin;
        string ImagePosMax;
        int TimeToShow;

        void Init()
        {
            ImageURL = (string)Config["Image URL"];
            ImagePosMin = (string)Config["Image Position - Anchor Min"];
            ImagePosMax = (string)Config["Image Position - Anchor Max"];
            TimeToShow = (int)Config["Time to Show"];
        }

        void OnServerSave()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                SaveGUI(player);
                timer.Once(TimeToShow, () => { 
                    if (player != null)
                        HideGUI(player);
                });
            }
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Generating new config...");

            Config["Image URL"] = "https://cdn3.iconfinder.com/data/icons/dev-basic/512/save-512.png";
            Config["Image Position - Anchor Min"] = "0.01 0.01";
            Config["Image Position - Anchor Max"] = "0.065 0.1";
            Config["Time to Show"] = 3;

            Puts("New config generated!");
        }

        void SaveGUI(BasePlayer player)
        {
            HideGUI(player);

            var GUIElement = new CuiElementContainer();

            GUIElement.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = ImagePosMin,
                    AnchorMax = ImagePosMax
                },
                Image =
                {
                    Color = "0 0 0 0"
                }
            }, "Hud", "SaveAlertGUI");

            GUIElement.Add(new CuiElement
            {
                Parent = "SaveAlertGUI",
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Url = ImageURL,
                        Sprite = "assets/content/textures/generic/fulltransparent.tga"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1"
                    }
                }
            });

            CuiHelper.AddUi(player, GUIElement);
        }

        void HideGUI(BasePlayer player) => CuiHelper.DestroyUi(player, "SaveAlertGUI");
    }
}