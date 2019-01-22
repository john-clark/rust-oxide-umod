/*
TODO:
- Add configuration and localization support
- Add cooldown option for taunting
- Add option for picking which taunts are allowed?
- Add option to only taunt prop's effect(s)
- Add option to show gibs for props or not
- Figure out why Hurt() isn't working for damage passing
- Fix player kick with error on Unload()
- Fix OnPlayerInput checks not allowing players to be props sometimes (dictionary issue)
- Move taunt GUI button to better position
- Unselect active item if selected (make sure to restore fully)
- Update configuration to have usable defaults
- Update configuration automatically
- Whitelist objects to block bad prefabs
*/

using System.Collections.Generic;
using System.Linq;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Hide and Seek", "Wulf/lukespragg", "0.1.3", ResourceId = 1421)]
    [Description("The classic game(mode) of hide and seek, as props")]
    public class HideAndSeek : CovalencePlugin
    {
        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                ["AlreadyHidden"] = "You are already hidden",
                ["Hiding"] = "You are hiding... shhh!",
                ["NotAllowed"] = "You are not allowed to use the '{0}' command",
                ["NotHiding"] = "You are no longer hiding, run!"
            }, this);
        }

        #endregion
 
        #region Initialization

        private static System.Random random = new System.Random();
        private static readonly string[] animalTaunts = new[]
        {
            "animals/bear/attack1",
            "animals/bear/attack2",
            "animals/bear/bite",
            "animals/bear/breathe-1",
            "animals/bear/breathing",
            "animals/bear/death",
            "animals/bear/roar1",
            "animals/bear/roar2",
            "animals/bear/roar3",
            "animals/boar/attack1",
            "animals/boar/attack2",
            "animals/boar/flinch1",
            "animals/boar/flinch2",
            "animals/boar/scream",
            "animals/chicken/attack1",
            "animals/chicken/attack2",
            "animals/chicken/attack3",
            "animals/chicken/cluck1",
            "animals/chicken/cluck2",
            "animals/chicken/cluck3",
            "animals/horse/attack",
            "animals/horse/flinch1",
            "animals/horse/flinch2",
            "animals/horse/heavy_breath",
            "animals/horse/snort",
            "animals/horse/whinny",
            "animals/horse/whinny_large",
            "animals/rabbit/attack1",
            "animals/rabbit/attack2",
            "animals/rabbit/run",
            "animals/rabbit/walk",
            "animals/stag/attack1",
            "animals/stag/attack2",
            "animals/stag/death1",
            "animals/stag/death2",
            "animals/stag/flinch1",
            "animals/stag/scream",
            "animals/wolf/attack1",
            "animals/wolf/attack2",
            "animals/wolf/bark",
            "animals/wolf/breathe",
            "animals/wolf/howl1",
            "animals/wolf/howl2",
            "animals/wolf/run_attack",
        };
        private static readonly string[] buildingTaunts = new[]
        {
            "barricades/damage",
            "beartrap/arm",
            "beartrap/fire",
            //"bucket_drop_debris",
            "build/frame_place",
            //"build/promote_metal",
            //"build/promote_stone",
            //"build/promote_toptier",
            //"build/promote_wood",
            "build/repair",
            "build/repair_failed",
            "build/repair_full",
            "building/fort_metal_gib",
            "building/metal_sheet_gib",
            "building/stone_gib",
            "building/thatch_gib",
            "building/wood_gib",
            "door/door-metal-impact",
            "door/door-metal-knock",
            "door/door-wood-impact",
            "door/door-wood-knock",
            "door/lock.code.denied",
            "door/lock.code.lock",
            "door/lock.code.unlock",
            "door/lock.code.updated",
        };
        private static readonly string[] otherTaunts = new[]
        {
            //"entities/helicopter/heli_explosion",
            //"entities/helicopter/rocket_airburst_explosion",
            //"entities/helicopter/rocket_explosion",
            //"entities/helicopter/rocket_fire",
            "entities/loot_barrel/gib",
            "entities/loot_barrel/impact",
            "entities/tree/tree-impact",
            //"fire/fire_v2",
            //"fire/fire_v3",
            //"fire_explosion",
            //"gas_explosion_small",
            "gestures/cameratakescreenshot",
            "headshot",
            "headshot_2d",
            "hit_notify",
            /*"impacts/additive/explosion",
            "impacts/blunt/clothflesh/clothflesh1",
            "impacts/blunt/concrete/concrete1",
            "impacts/blunt/metal/metal1",
            "impacts/blunt/wood/wood1",
            "impacts/bullet/clothflesh/clothflesh1",
            "impacts/bullet/concrete/concrete1",
            "impacts/bullet/dirt/dirt1",
            "impacts/bullet/forest/forest1",
            "impacts/bullet/metal/metal1",
            "impacts/bullet/metalore/bullet_impact_metalore",
            "impacts/bullet/path/path1",
            "impacts/bullet/rock/bullet_impact_rock",
            "impacts/bullet/sand/sand1",
            "impacts/bullet/snow/snow1",
            "impacts/bullet/tundra/bullet_impact_tundra",
            "impacts/bullet/wood/wood1",
            "impacts/slash/concrete/slash_concrete_01",
            "impacts/slash/metal/metal1",
            "impacts/slash/metal/metal2",
            "impacts/slash/metalore/slash_metalore_01",
            "impacts/slash/rock/slash_rock_01",
            "impacts/slash/wood/wood1",*/
            "item_break",
            "player/beartrap_clothing_rustle",
            "player/beartrap_scream",
            "player/groundfall",
            "player/howl",
            //"player/onfire",
            "repairbench/itemrepair",
        };
        private static readonly string[] weaponTaunts = new[]
        {
            "ricochet/ricochet1",
            "ricochet/ricochet2",
            "ricochet/ricochet3",
            "ricochet/ricochet4",
            //"survey_explosion",
            //"weapons/c4/c4_explosion",
            "weapons/rifle_jingle1",
            "weapons/survey_charge/survey_charge_stick",
            "weapons/vm_machete/attack-1",
            "weapons/vm_machete/attack-2",
            "weapons/vm_machete/attack-3",
            "weapons/vm_machete/deploy",
            "weapons/vm_machete/hit"
        };

        private Dictionary<IPlayer, BaseEntity> props = new Dictionary<IPlayer, BaseEntity>();

        private const string permAllow = "hideandseek.allow";

        private void Init()
        {
            permission.RegisterPermission(permAllow, this);

            //foreach (var player in props.Values) TauntButton(player, null);
        }

        #endregion

        #region Player Restoring

        private void OnUserConnected(IPlayer player)
        {
            if (!props.ContainsKey(player)) return;

            var basePlayer = player.Object as BasePlayer;
            if (basePlayer.IsSleeping()) basePlayer.EndSleeping();
            SetPropFlags(basePlayer);
        }

        #endregion

        #region Prop Flags

        private void SetPropFlags(BasePlayer player)
        {
            // Remove admin/developer flags
            if (player.IsAdmin) player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
            if (player.IsDeveloper) player.SetPlayerFlag(BasePlayer.PlayerFlags.IsDeveloper, false);

            // Change to third-person view
            player.SetPlayerFlag(BasePlayer.PlayerFlags.ThirdPersonViewmode, true);
            player.SetPlayerFlag(BasePlayer.PlayerFlags.EyesViewmode, false);
        }

        private void UnsetPropFlags(BasePlayer player)
        {
            // Change to normal view
            player.SetPlayerFlag(BasePlayer.PlayerFlags.ThirdPersonViewmode, false);
            player.SetPlayerFlag(BasePlayer.PlayerFlags.EyesViewmode, false);

            // Restore admin/developer flags
            if (player.net.connection.authLevel > 0) player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
            if (DeveloperList.IsDeveloper(player)) player.SetPlayerFlag(BasePlayer.PlayerFlags.IsDeveloper, true);
        }

        #endregion

        #region Player Hiding

        private void HidePlayer(IPlayer player)
        {
            if (props.ContainsKey(player)) props.Remove(player);

            var basePlayer = player.Object as BasePlayer;
            var ray = new Ray(basePlayer.eyes.position, basePlayer.eyes.HeadForward());
            var entity = FindObject(ray, 3); // TODO: Make distance configurable
            if (entity == null || props.ContainsKey(player)) return;

            // Hide active item
            if (basePlayer.GetActiveItem() != null)
            {
                var heldEntity = basePlayer.GetActiveItem().GetHeldEntity() as HeldEntity;
                heldEntity?.SetHeld(false);
            }

            // Create the prop entity
            var prop = GameManager.server.CreateEntity(entity.name, basePlayer.transform.position, basePlayer.transform.rotation);
            prop.SendMessage("SetDeployedBy", basePlayer, SendMessageOptions.DontRequireReceiver);
            prop.SendMessage("InitializeItem", entity, SendMessageOptions.DontRequireReceiver);
            prop.Spawn();
            props.Add(player, prop);

            // Make the player invisible
            basePlayer.SetPlayerFlag(BasePlayer.PlayerFlags.Spectating, true);
            basePlayer.gameObject.SetLayerRecursive(10);
            basePlayer.CancelInvoke("MetabolismUpdate");
            basePlayer.CancelInvoke("InventoryUpdate");
            SetPropFlags(basePlayer);

            //TauntButton(basePlayer, null);
            player.Reply(Lang("Hiding", player.Id));
        }

        private void UnhidePlayer(IPlayer player)
        {
            if (!props.ContainsKey(player)) return;

            // Remove the prop entity
            var prop = props[player];
            if (!prop.IsDestroyed) prop.Kill(BaseNetworkable.DestroyMode.Gib);
            props.Remove(player);

            // Make the player visible
            var basePlayer = player.Object as BasePlayer;
            basePlayer.metabolism.Reset();
            basePlayer.InvokeRepeating("InventoryUpdate", 1f, 0.1f * Random.Range(0.99f, 1.01f));
            basePlayer.SetPlayerFlag(BasePlayer.PlayerFlags.Spectating, false);
            basePlayer.gameObject.SetLayerRecursive(17);
            UnsetPropFlags(basePlayer);

            //CuiHelper.DestroyUi(basePlayer, tauntPanel); // TODO
            player.Reply(Lang("NotHiding", player.Id));
        }

        #endregion

        #region Chat Commands

        [Command("hide")]
        private void HideCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permAllow))
            {
                player.Reply(Lang("NotAllowed", player.Id, command));
                return;
            }

            var basePlayer = player.Object as BasePlayer;
            if (props.ContainsKey(player) && basePlayer.HasPlayerFlag(BasePlayer.PlayerFlags.Spectating))
            {
                player.Reply(Lang("AlreadyHidden", player.Id));
                return;
            }

            HidePlayer(player);
        }

        [Command("unhide")]
        private void UnhideCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permAllow))
            {
                player.Reply(Lang("NotAllowed", player.Id, command));
                return;
            }

            var basePlayer = player.Object as BasePlayer;
            if (!props.ContainsKey(player) && !basePlayer.HasPlayerFlag(BasePlayer.PlayerFlags.Spectating))
            {
                player.Reply(Lang("AlreadyUnhidden", player.Id));
                return;
            }

            UnhidePlayer(player);
        }

        #endregion

        #region Prop Taunting

        [Command("taunt")]
        private void TauntCommand(IPlayer player, string command, string[] args)
        {
            var basePlayer = player.Object as BasePlayer;
            if (!props.ContainsKey(player) && !basePlayer.HasPlayerFlag(BasePlayer.PlayerFlags.Spectating))
            {
                player.Reply("You're not a prop!");
                return;
            }

            var taunt = otherTaunts[random.Next(otherTaunts.Length)];
            Effect.server.Run($"assets/bundled/prefabs/fx/{taunt}.prefab", basePlayer.transform.position, Vector3.zero);
        }

        #endregion

        #region Damage Passing

        private object OnEntityTakeDamage(BaseEntity entity, HitInfo info)
        {
            if (entity is BasePlayer) return null;

            if (!props.ContainsValue(entity))
            {
                var attacker = info.Initiator as BasePlayer;
                attacker?.Hurt(info.damageTypes.Total());
                return true;
            };

            /*var propPlayer = props[entity].Object as BasePlayer;
            if (propPlayer.health <= 1)
            {
                propPlayer.Die();
                return null;
            }

            propPlayer.InitializeHealth(propPlayer.health - info.damageTypes.Total(), 100f);*/
            return true;
        }

        #endregion

        #region Death Handling

        private void OnEntityDeath(BaseEntity entity)
        {
            // Check for prop entity/player
            var basePlayer = entity.ToPlayer();
            if (basePlayer == null) return;

            var player = players.FindPlayerById(basePlayer.UserIDString);
            if (!props.ContainsKey(player)) return;

            // Get the prop entity
            UnhidePlayer(player);
            props.Remove(player);
            basePlayer.RespawnAt(basePlayer.transform.position, basePlayer.transform.rotation);

            // Remove the prop entity
            var prop = props[player];
            if (!prop.IsDestroyed) prop.Kill(BaseNetworkable.DestroyMode.Gib);
        }

        private void OnEntitySpawned(BaseNetworkable entity)
        {
            // Remove all corpses
            if (entity.ShortPrefabName.Equals("player_corpse")) entity.KillMessage();
        }

        #endregion

        #region Spectate Blocking

        private object OnServerCommand(ConsoleSystem.Arg arg)
        {
            if (arg?.Connection != null && arg.cmd.Name == "spectate") return true;
            return null;
        }

        private object OnPlayerInput(BasePlayer player, InputState input)
        {
            var iplayer = players.FindPlayerById(player.UserIDString);
            if (iplayer == null) return null;

            if (!props.ContainsKey(iplayer) && !player.IsSpectating() && input.WasJustPressed(BUTTON.FIRE_PRIMARY))
                HidePlayer(iplayer);
            else if (props.ContainsKey(iplayer) && player.IsSpectating() && input.WasJustPressed(BUTTON.FIRE_SECONDARY))
                UnhidePlayer(iplayer);
            else if (props.ContainsKey(iplayer) && player.IsSpectating() && input.WasJustPressed(BUTTON.JUMP) || input.WasJustPressed(BUTTON.DUCK))
                return true;

            return null;
        }

        #endregion

        #region GUI Button

        string tauntPanel;

        private void TauntButton(BasePlayer player, string text)
        {
            var elements = new CuiElementContainer();
            tauntPanel = elements.Add(new CuiPanel
            {
                Image = { Color = "0.0 0.0 0.0 0.0" },
                RectTransform = { AnchorMin = "0.026 0.037", AnchorMax = "0.075 0.10" }
            }, "Hud", "taunt");
            elements.Add(new CuiElement
            {
                Parent = tauntPanel,
                Components =
                {
                    new CuiRawImageComponent { Url = "http://i.imgur.com/28fdPww.png" },
                    new CuiRectTransformComponent { AnchorMin = "0.0 0.0", AnchorMax = "1.0 1.0" }
                }
            });
            elements.Add(new CuiButton
            {
                Button = { Command = $"taunt", Color = "0.0 0.0 0.0 0.0" },
                RectTransform = { AnchorMin = "0.026 0.037", AnchorMax = "0.075 0.10" },
                Text = { Text = "" }
            });
            CuiHelper.DestroyUi(player, tauntPanel);
            CuiHelper.AddUi(player, elements);
        }

        #endregion

        #region Cleanup Props

        private void Unload()
        {
            var propList = props.Keys.ToList();
            foreach (var prop in propList) UnhidePlayer(prop);
            //foreach (var player in BasePlayer.activePlayerList) CuiHelper.DestroyUi(player, tauntPanel);
        }

        #endregion

        #region Helper Methods

        private static BaseEntity FindObject(Ray ray, float distance)
        {
            RaycastHit hit;
            return !Physics.Raycast(ray, out hit, distance) ? null : hit.GetEntity();
        }

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        #endregion
    }
}