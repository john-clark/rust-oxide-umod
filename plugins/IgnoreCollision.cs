using System;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("IgnoreCollision", "Vlad-00003", "1.0.0")]
    [Description("This plugin removes collisions between droped items")]
    /*
     * Author info:
     *   E-mail: Vlad-00003@mail.ru
     *   Vk: vk.com/vlad_00003
     */
    class IgnoreCollision : RustPlugin
    {
        private bool Ignore;
        protected override void LoadDefaultConfig()
        {
            Config["Ignore dropped items collision"] = true;
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                Ignore = (bool)Config["Ignore dropped items collision"];
            }
            catch (Exception ex)
            {
                Ignore = true;
                RaiseError($"Failed to load config file (is the config file corrupt?) ({ex.Message})");
            }
        }
        void OnServerInitialized()
        {
            Physics.IgnoreLayerCollision(26, 26, Ignore);
            if (Ignore)
                Puts("Dropped items collisions disabled! Keep in mind that droped items would not stack anymore.");
            else
                Puts("Dropped items collisions active");
        }
    }
}