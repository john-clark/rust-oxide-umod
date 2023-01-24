using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Magic Carpet", "redBDGR", "1.0.1")]
    [Description("Take a magic carpet ride")]
    class MagicCarpet : RustPlugin
    {
        private const string permissionName = "magiccarpet.use";
        public static LayerMask collLayers = LayerMask.GetMask("Construction", "Deployed", "Tree", "Terrain", "Resource", "World", "Water", "Default", "AI");
        private Dictionary<string, BaseEntity> users = new Dictionary<string, BaseEntity>();

        private void Init()
        {
            permission.RegisterPermission(permissionName, this);
            lang.RegisterMessages(new Dictionary<string, string>
            {
                //chat
                ["No Permission"] = "You are not allowed to use this command!",
                ["MC start"] = "You have summoned a magic carpet!",
                ["MC End"] = "Your magic carpet has disappeared"
            }, this);
        }

        private void Unload()
        {
            foreach(var entry in users)
                entry.Value.Kill();
        }

        private void OnEntityMounted(BaseMountable mountable, BasePlayer player)
        {
            _MagicCarpet mc = mountable.GetComponent<_MagicCarpet>();
            if (!mc)
                return;
            mc.player = player;
        }

        private void OnEntityDismounted(BaseMountable mountable, BasePlayer player)
        {
            _MagicCarpet mc = mountable.GetComponent<_MagicCarpet>();
            if (!mc)
                return;
            mc.player = null;
        }

        [ChatCommand("mc")]
        void attachCMD(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, permissionName))
            {
                player.ChatMessage(msg("No Permission", player.UserIDString));
                return;
            }

            if (users.ContainsKey(player.UserIDString))
            {
                player.ChatMessage(msg("MC End", player.UserIDString));
                Effect.server.Run("assets/prefabs/npc/patrol helicopter/effects/rocket_fire.prefab", player.transform.position);
                _MagicCarpet carpet = users[player.UserIDString].GetComponent<_MagicCarpet>();
                if (carpet)
                    carpet.Destroy();
                BaseEntity ent = users[player.UserIDString];
                if (ent)
                {
                    users.Remove(player.UserIDString);
                    ent.Kill();
                }
            }
            else
            {
                player.ChatMessage(msg("MC start", player.UserIDString));
                Vector3 pos = player.transform.position + -player.transform.forward * 3f + new Vector3(0, 1f, 0);
                Effect.server.Run("assets/prefabs/npc/patrol helicopter/effects/rocket_fire.prefab", pos);
                BaseEntity ent = GameManager.server.CreateEntity("assets/prefabs/deployable/chair/chair.deployed.prefab", pos);
                if (ent)
                {
                    ent.Spawn();
                    ent.gameObject.AddComponent<_MagicCarpet>().player = player;
                    users.Add(player.UserIDString, ent);
                }
            }
        }

        private class _MagicCarpet : MonoBehaviour
        {
            public BasePlayer player;
            private BaseEntity ent;
            private BaseEntity chair;
            private BaseMountable mountable;

            private void Awake()
            {
                chair = gameObject.GetComponent<BaseEntity>();
                ent = GameManager.server.CreateEntity("assets/prefabs/deployable/rug/rug.deployed.prefab", chair.transform.position);
                ent.Spawn();
                mountable = chair.GetComponent<BaseMountable>();
                mountable.isMobile = true;
                ent.SetParent(chair);
                ent.transform.localPosition = new Vector3(0, 0.4f, 0);
                ent.transform.localRotation = new Quaternion(0, 0, 0, 0);
                chair.transform.localRotation = new Quaternion(0, 0, 0, 0);
            }

            private void Update()
            {
                if (!mountable.IsMounted())
                    return;
                transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.LookRotation(player.eyes.HeadForward()), UnityEngine.Time.deltaTime);
                if (player.serverInput.IsDown(BUTTON.FORWARD))
                    transform.position = Vector3.MoveTowards(transform.position, player.eyes.HeadForward(), UnityEngine.Time.deltaTime * 0.1f);
                chair.SendNetworkUpdateImmediate();
                ent.SendNetworkUpdateImmediate();
            }

            private Vector3 GetMovementPosition()
            {
                Vector3 nextPos = transform.position;
                if (player.serverInput.IsDown(BUTTON.FIRE_PRIMARY))
                    return transform.position + transform.forward * 0.01f;
                if (player.serverInput.IsDown(BUTTON.LEFT))
                    return transform.position + -transform.right * 0.01f;
                if (player.serverInput.IsDown(BUTTON.RIGHT))
                    return transform.position + transform.right * 0.01f;
                if (player.serverInput.IsDown(BUTTON.BACKWARD))
                    return transform.position + -transform.forward * 0.01f;
                return nextPos;
            }

            public void Destroy()
            {
                Destroy(this);
            }
        }

        private string msg(string key, string id = null) => lang.GetMessage(key, this, id);
    }
}
