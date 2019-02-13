using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Supply Signal Alerts", "LaserHydra", "3.0.1", ResourceId = 933)]
    internal class SupplySignalAlerts : RustPlugin
    {
        private void Init()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Supply Signal Thrown"] = "<color=orange>{player}</color> has thrown a supply signal at <color=orange>{position}</color>",
                ["Position Format"] = "( X: {x}, Y: {y}, Z: {z} )"
            }, this);
        }

        private void OnExplosiveDropped(BasePlayer player, BaseEntity entity) => OnExplosiveThrown(player, entity);

        private void OnExplosiveThrown(BasePlayer player, BaseEntity entity)
        {
            if (!(entity is SupplySignal))
                return;

            timer.Once(2.8f, () =>
            {
                var position = lang.GetMessage("Position Format", this, player.UserIDString)
                    .Replace("{x}", entity.transform.position.x.ToString("##.0"))
                    .Replace("{y}", entity.transform.position.y.ToString("##.0"))
                    .Replace("{z}", entity.transform.position.z.ToString("##.0"));

                var message = lang.GetMessage("Supply Signal Thrown", this, player.UserIDString)
                    .Replace("{player}", player.displayName)
                    .Replace("{position}", position);

                PrintToChat(message);
                Puts(message);
            });
        }
    }
}