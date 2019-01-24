using UnityEngine;

namespace Oxide.Plugins
{
    [Info("EasyChopper", "open_maibox", "0.1")]
    [Description("Simplify dealing with attack helicopters similar to EasyAirdrop.")]
    class EasyChopper : RustPlugin
    {
        private const string PERMISSION = "easychopper.call";
        private const string PREFAB     = "assets/prefabs/npc/patrol helicopter/patrolhelicopter.prefab";

        #region setup
        void Loaded()
        {
            permission.RegisterPermission("easychopper.call", this);
        }
        #endregion

        #region commands
        [ConsoleCommand("chopper")]
        void CCommandChopper(ConsoleSystem.Arg arg)
        {
            if (arg == null) return;

            BasePlayer player = arg?.Connection?.player == null ? null : arg?.Connection?.player as BasePlayer;
            string cmd = arg.cmd?.Name ?? "unknown";
            string[] args = arg.HasArgs() ? arg.Args : new string[0];

            CommandChopper(player, cmd, args);
        }

        [ChatCommand("chopper")]
        void CommandChopper(BasePlayer player, string cmd, string[] args)
        {
            switch (args[0].ToLower())
            {
                case "random":
                    if (!HasPermission(player))
                    {
                        SendChatMessage(player, "You can't do that.");
                        return;
                    }

                    Vector3 vector    = GetRandomVector();
                    string playerName = player == null ? "Server" : player.IPlayer.Name;

                    SpawnChopper(vector);

                    if (player != null)
                    {
                        SendReply(player, "Attack chopper dispatched with initial random destination of " + vector);
                    }

                    Puts(playerName + " dispatched an attack chopper with initial random destination of " + vector);

                    break;
                default:
                    break;
            }
        }
        #endregion

        #region util functions
        Vector3 GetRandomVector()
        {
            float max = ConVar.Server.worldsize / 2;

            float x = Random.Range(-max, max);
            float y = Random.Range(150, 200);
            float z = Random.Range(-max, max);

            return new Vector3(x, y, z);
        }

        private bool HasPermission(BasePlayer player)
        {
            if (player == null) return true; // Called from rcon

            if (permission.UserHasPermission(player.UserIDString, PERMISSION)) return true;

            return false;
        }

        void SendChatMessage(BasePlayer player, string msg)
        {
            if (player != null)
                SendReply(player, msg);
            else
                Puts(msg);
        }

        private void SpawnChopper(Vector3 position)
        {
            BaseEntity entity = GameManager.server.CreateEntity(PREFAB, new Vector3(), new Quaternion());

            if (!entity) return;

            PatrolHelicopterAI helicopter = entity.GetComponent<PatrolHelicopterAI>();

            helicopter.SetInitialDestination(position);
            entity.Spawn();
        }
        #endregion
    }
}
