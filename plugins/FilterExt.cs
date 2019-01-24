using System.Linq;

namespace Oxide.Plugins
{
    [Info("FilterExt", "Wulf/lukespragg", "0.2.0", ResourceId = 1396)]
    [Description("Extension to Oxide's filter for removing unwanted console messages")]

    class FilterExt : CovalencePlugin
    {
        void Init()
        {
            #region Get existing filter list

            #if HURTWORLD
            var filter = Game.Hurtworld.HurtworldExtension.Filter.ToList();
            #endif
            #if REIGNOFKINGS
            var filter = Game.ReignOfKings.ReignOfKingsExtension.Filter.ToList();
            #endif
            #if RUST
            var filter = Game.Rust.RustExtension.Filter.ToList();
            #endif
            #if RUSTLEGACY
            var filter = Game.RustLegacy.RustExtension.Filter.ToList();
            #endif
            #if THEFOREST
            var filter = Game.TheForest.TheForestExtension.Filter.ToList();
            #endif
            #if UNTURNED
            var filter = Game.Unturned.UnturnedExtension.Filter.ToList();
            #endif

            #endregion

            #region Add messages to filter

            filter.Add(", serialization");
            filter.Add("- deleting");
            filter.Add("[event] assets/");
            filter.Add("SteamServerConnectFailure: k_EResultServiceUnavailable");
            filter.Add("SteamServerConnectFailure: k_EResultNoConnection");
            filter.Add("SteamServerConnected");

            #endregion

            #region Update filter list

            #if HURTWORLD
            Game.Hurtworld.HurtworldExtension.Filter = filter.ToArray();
            #endif
            #if REIGNOFKINGS
            Game.ReignOfKings.ReignOfKingsExtension.Filter = filter.ToArray();
            #endif
            #if RUST
            Game.Rust.RustExtension.Filter = filter.ToArray();
            #endif
            #if RUSTLEGACY
            Game.RustLegacy.RustExtension.Filter = filter.ToArray();
            #endif
            #if THEFOREST
            Game.TheForest.TheForestExtension.Filter = filter.ToArray();
            #endif
            #if UNTURNED
            Game.Unturned.UnturnedExtension.Filter = filter.ToArray();
            #endif

            #endregion
        }
    }
}
