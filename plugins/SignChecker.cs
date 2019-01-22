using System;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Core.Configuration;
using Facepunch.Unity;
using Facepunch;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info( "Sign Checker", "Scarylaggy", "1.1.5" )]
    [Description( "Provides a way to check the last editor of signs" )]
    class SignChecker : RustPlugin
    {
        private static readonly string PERMISSION_USE = "signchecker.use";
        private static readonly string PERMISSION_CLEAN = "signchecker.clean";
        StoredData storedData;

        void Init()
        {
            registerPermissions();
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>( "SignChecker" );
        }

        /// <summary>
        /// Registers the Permissions on Plugin initilation
        /// </summary>
        private void registerPermissions()
        {
            permission.RegisterPermission( PERMISSION_USE, this );
            permission.RegisterPermission( PERMISSION_CLEAN, this );
        }

        /// <summary>
        /// Server hook wich gets called on every edit of an Signage
        /// </summary>
        /// <param name="sign"></param>
        /// <param name="player"></param>
        /// <param name="text"></param>
        void OnSignUpdated(Signage sign, BasePlayer player, string text)
        {   
            //Quick check if the Called sign is already in the Datafile
            if (!isAlreadyInStorageData( sign ))
            {
                //If not, it gets added
                storedData.signCheckerDatas.Add( new SignCheckerData( player, sign ) );
            }
            else
            {
                //If it is, we get it from the list, and Edit it to the new "Last editor"-Data
                foreach (SignCheckerData data in FindSignById( sign.ToString() ))
                {
                    data.userId = player.UserIDString;
                    data.name = player.UserIDString;
                }
            }
            Interface.Oxide.DataFileSystem.WriteObject( this.Name, storedData );
        }

        /// <summary>
        /// Gets an IEnumerable<SignCheckerData> wich will only contain the needed Part of our stored Data
        /// </summary>
        /// <param name="id"></param> The ID is specified by the Signage#ToString() Method
        /// <returns>IEnumerable<SignCheckerData></returns>
        private IEnumerable<SignCheckerData> FindSignById(string id)
        {
            return storedData.signCheckerDatas.Where( item => item.signId.Equals( id ) );
        }
        private bool hasPermissionUse(BasePlayer player, string perm)
        {
            return permission.UserHasPermission( player.UserIDString, perm );
        }

        /// <summary>
        /// Gets the Last editor for a given Signage
        /// </summary>
        /// <param name="sign"></param>
        /// <param name="caller"></param>
        /// <returns></returns>
        private string getPlayerDataForSign(Signage sign, string caller)
        {
            if (sign != null)
            {
                string playerData = "";
                foreach (SignCheckerData data in FindSignById( sign.ToString() ))
                {
                    playerData = data.ToString();
                }
                if (!string.IsNullOrEmpty(playerData))
                {
                    return playerData;
                }
            }
            return string.Format( GetLangValue( "NoOne", caller ) );
        }

        /// <summary>
        /// Chatcommand to clean the File, as it is apropriate to clean it after a Wipe
        /// </summary>
        /// <param name="player"></param>
        /// <param name="command"></param>
        /// <param name="args"></param>
        [ChatCommand( "signwipe" )]
        private void wipeDataChatCommand(BasePlayer player, string command, string[] args)
        {
            if (!hasPermissionUse(player, PERMISSION_CLEAN))
            {
                player.ChatMessage( string.Format( GetLangValue( "NoPermission", player.UserIDString ) ) );
                return;
            }
            storedData.signCheckerDatas.Clear();
            Interface.Oxide.DataFileSystem.WriteObject( this.Name, storedData );
            player.ChatMessage( string.Format( GetLangValue( "Wipe", player.UserIDString ) ) );
        }

        /// <summary>
        /// Chat Command for Players with the signchecker.use Permission
        /// Messages the user with an chatmessage about the last editor of an Signage
        /// => Checks if the player has the needed Permission
        /// => Checks if the player is looking at a Signage
        /// </summary>
        /// <param name="player"></param>
        /// <param name="command"></param>
        /// <param name="args"></param>
        [ChatCommand( "check" )]
        private void CheckChatCommand(BasePlayer player, string command, string[] args)
        {
            //Check for the Use Permission
            if (!hasPermissionUse( player, PERMISSION_USE ))
            {
                player.ChatMessage( string.Format( GetLangValue( "NoPermission", player.UserIDString ) ) );
                return;
            }
            Signage sign = null;
            RaycastHit hit;
            // Get the object that is in front of the player within the maximum distance set in the config.
            if (Physics.Raycast( player.eyes.HeadRay(), out hit, 3 ))
            {
                // Attempt to grab the Signage entity, if there is none this will set the sign to null, 
                // otherwise this will set it to the sign the player is looking at.
                sign = hit.GetEntity() as Signage;
            }
            else
            {
                player.ChatMessage( string.Format( GetLangValue( "NothingFound", player.UserIDString ) ) );
                return;
            }
            //If everything is ok, here we actually get the needed Values
            player.ChatMessage( string.Format( GetLangValue( "LastEdit", player.UserIDString ), getPlayerDataForSign( sign, player.UserIDString ) ) );
        }

        private string GetLangValue(string key, string userId) => lang.GetMessage( key, this, userId );

        bool isAlreadyInStorageData(Signage sign) => storedData.signCheckerDatas.Any( item => item.signId.Equals( sign.ToString() ) );

        protected override void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages( new Dictionary<string, string>
            {
                { "NoPermission", "You don't have the Permission to use this Command." },
                { "LastEdit", "Last editor was {0}" },
                { "NothingFound", "You are not looking at a Sign" },
                { "NoOne", "nobody" },
                { "Wipe", "Datafile has been wiped"}

            }, this );
        }

        class StoredData
        {
            public HashSet<SignCheckerData> signCheckerDatas = new HashSet<SignCheckerData>();

            public StoredData()
            {
            }
        }
        class SignCheckerData
        {
            public string userId;
            public string name;
            public string signId;

            public SignCheckerData()
            {

            }

            public SignCheckerData(BasePlayer player, Signage sign)
            {
                signId = sign.ToString();
                userId = player.UserIDString;
                name = player.displayName;
            }

            public override string ToString()
            {
                return name + " - " + userId;
            }
        }
    }
}
