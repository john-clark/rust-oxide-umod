using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Map Block", "Jacob", "1.0.0")]
    internal class MapBlock : RustPlugin
    {
        private MapMarkerGenericRadius _marker;

        private void OnPlayerInit(BasePlayer player) => UpdateMarker();

        private void OnServerInitialized()
        {
            _marker = GameManager.server.CreateEntity("assets/prefabs/tools/map/genericradiusmarker.prefab", new Vector3(0, 100, 0)) as MapMarkerGenericRadius;
            _marker.alpha = 1;
            _marker.color1 = Color.black;
            _marker.color2 = Color.black;
            _marker.radius = ConVar.Server.worldsize;
            _marker.Spawn();
            _marker.SendUpdate();
        }

        private void Unload() => _marker?.Kill();

        private void UpdateMarker()
        {
            _marker.Kill();
            _marker.Spawn();
            _marker.SendUpdate();
        }
    }
}