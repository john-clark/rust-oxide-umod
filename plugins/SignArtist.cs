// Reference: System.Drawing

using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Plugins.SignArtistClasses;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Sign Artist", "Mughisi", "1.1.2", ResourceId = 992)]
    [Description("Allows players with the appropriate permission to import images from the internet on paintable objects")]

    /*********************************************************************************
     * This plugin was originally created by Bombardir and then maintained by Nogrod.
     * It was rewritten from scratch by Mughisi on January 12th, 2018.
     *********************************************************************************/

    internal class SignArtist : RustPlugin
    {
        private Dictionary<ulong, float> cooldowns = new Dictionary<ulong, float>();

        private GameObject imageDownloaderGameObject;

        private ImageDownloader imageDownloader;

        public SignArtistConfig Settings { get; private set; }

        public Dictionary<string, ImageSize> ImageSizePerAsset { get; private set; }

        /// <summary>
        /// Plugin configuration
        /// </summary>
        public class SignArtistConfig
        {
            [JsonProperty(PropertyName = "Time in seconds between download requests (0 to disable)")]
            public int Cooldown { get; set; }

            [JsonProperty(PropertyName = "Maximum concurrent downloads")]
            public int MaxActiveDownloads { get; set; }

            [JsonProperty(PropertyName = "Maximum distance from the sign")]
            public int MaxDistance { get; set; }

            [JsonProperty(PropertyName = "Maximum filesize in MB")]
            public float MaxSize { get; set; }

            [JsonProperty(PropertyName = "Enforce JPG file format")]
            public bool EnforceJpeg { get; set; }

            [JsonProperty(PropertyName = "JPG image quality (0-100)")]
            public int Quality
            {
                get
                {
                    return quality;
                }
                set
                {
                    // Validate the value, it can't be less than 0 and not more than 100.
                    if (value >= 0 && value <= 100)
                    {
                        quality = value;
                    }
                    else
                    {
                        // Set the quality to a default value of 85% when an invalid value was specified.
                        quality = value > 100 ? 100 : 85;
                    }
                }
            }

            [JsonProperty("Enable logging file")]
            public bool FileLogging { get; set; }

            [JsonProperty("Enable logging console")]
            public bool ConsoleLogging { get; set; }

            [JsonIgnore]
            public float MaxFileSizeInBytes
            {
                get
                {
                    return MaxSize * 1024 * 1024;
                }
            }

            private int quality = 85;

            /// <summary>
            /// Creates a default configuration file
            /// </summary>
            /// <returns>Default config</returns>
            public static SignArtistConfig DefaultConfig()
            {
                return new SignArtistConfig
                {
                    Cooldown = 0,
                    MaxSize = 1,
                    MaxDistance = 3,
                    MaxActiveDownloads = 5,
                    EnforceJpeg = false,
                    Quality = 85,
                    FileLogging = false,
                    ConsoleLogging = false
                };
            }
        }

        /// <summary>
        /// A type used to request new images to download.
        /// </summary>
        private class DownloadRequest
        {
            public BasePlayer Sender { get; }

            public Signage Sign { get; }

            public string Url { get; }

            public bool Raw { get; }

            /// <summary>
            /// Initializes a new instance of the <see cref="DownloadRequest" /> class.
            /// </summary>
            /// <param name="url">The URL to download the image from. </param>
            /// <param name="player">The player that requested the download. </param>
            /// <param name="sign">The sign to add the image to. </param>
            /// <param name="raw">Should the image be stored with or without conversion to jpeg. </param>
            public DownloadRequest(string url, BasePlayer player, Signage sign, bool raw)
            {
                Url = url;
                Sender = player;
                Sign = sign;
                Raw = raw;
            }
        }

        /// <summary>
        /// A type used to request new images to be restored.
        /// </summary>
        private class RestoreRequest
        {
            public BasePlayer Sender { get; }

            public Signage Sign { get; }

            public bool Raw { get; }

            /// <summary>
            /// Initializes a new instance of the <see cref="RestoreRequest" /> class.
            /// </summary>
            /// <param name="player">The player that requested the restore. </param>
            /// <param name="sign">The sign to restore the image from. </param>
            /// <param name="raw">Should the image be stored with or without conversion to jpeg. </param>
            public RestoreRequest(BasePlayer player, Signage sign, bool raw)
            {
                Sender = player;
                Sign = sign;
                Raw = raw;
            }
        }

        /// <summary>
        /// A type used to determine the size of the image for a sign
        /// </summary>
        public class ImageSize
        {
            public int Width { get; }

            public int Height { get; }

            public int ImageWidth { get; }

            public int ImageHeight { get; }

            /// <summary>
            /// Initializes a new instance of the <see cref="ImageSize" /> class.
            /// </summary>
            /// <param name="width">The width of the canvas and the image. </param>
            /// <param name="height">The height of the canvas and the image. </param>
            public ImageSize(int width, int height) : this(width, height, width, height)
            {
            }

            /// <summary>
            /// Initializes a new instance of the <see cref="ImageSize" /> class.
            /// </summary>
            /// <param name="width">The width of the canvas. </param>
            /// <param name="height">The height of the canvas. </param>
            /// <param name="imageWidth">The width of the image. </param>
            /// <param name="imageHeight">The height of the image. </param>
            public ImageSize(int width, int height, int imageWidth, int imageHeight)
            {
                Width = width;
                Height = height;
                ImageWidth = imageWidth;
                ImageHeight = imageHeight;
            }
        }

        /// <summary>
        /// UnityEngine script to be attached to a GameObject to download images and apply them to signs.
        /// </summary>
        private class ImageDownloader : MonoBehaviour
        {
            private byte activeDownloads;

            private byte activeRestores;

            private readonly SignArtist signArtist = (SignArtist)Interface.Oxide.RootPluginManager.GetPlugin(nameof(SignArtist));

            private readonly Queue<DownloadRequest> downloadQueue = new Queue<DownloadRequest>();

            private readonly Queue<RestoreRequest> restoreQueue = new Queue<RestoreRequest>();

            /// <summary>
            /// Queue a new image to download and add to a sign
            /// </summary>
            /// <param name="url">The URL to download the image from. </param>
            /// <param name="player">The player that requested the download. </param>
            /// <param name="sign">The sign to add the image to. </param>
            /// <param name="raw">Should the image be stored with or without conversion to jpeg. </param>
            public void QueueDownload(string url, BasePlayer player, Signage sign, bool raw)
            {
                // Check if there is already a request for this sign and show an error if there is.
                bool existingRequest = downloadQueue.Any(request => request.Sign == sign) || restoreQueue.Any(request => request.Sign == sign);
                if (existingRequest)
                {
                    signArtist.SendMessage(player, "ActionQueuedAlready");

                    return;
                }

                // Instantiate a new DownloadRequest and add it to the queue.
                downloadQueue.Enqueue(new DownloadRequest(url, player, sign, raw));

                // Attempt to start the next download.
                StartNextDownload();
            }

            /// <summary>
            /// Attempts to restore a sign.
            /// </summary>
            /// <param name="player">The player that requested the restore. </param>
            /// <param name="sign">The sign to restore the image from. </param>
            /// <param name="raw">Should the image be stored with or without conversion to jpeg. </param>
            public void QueueRestore(BasePlayer player, Signage sign, bool raw)
            {
                // Check if there is already a request for this sign and show an error if there is.
                bool existingRequest = downloadQueue.Any(request => request.Sign == sign) || restoreQueue.Any(request => request.Sign == sign);
                if (existingRequest)
                {
                    signArtist.SendMessage(player, "ActionQueuedAlready");

                    return;
                }

                // Instantiate a new RestoreRequest and add it to the queue.
                restoreQueue.Enqueue(new RestoreRequest(player, sign, raw));

                // Attempt to start the next restore.
                StartNextRestore();
            }

            /// <summary>
            /// Starts the next download if available.
            /// </summary>
            /// <param name="reduceCount"></param>
            private void StartNextDownload(bool reduceCount = false)
            {
                // Check if we need to reduce the active downloads counter after a succesful or failed download.
                if (reduceCount)
                {
                    activeDownloads--;
                }

                // Check if we don't have the maximum configured amount of downloads running already.
                if (activeDownloads >= signArtist.Settings.MaxActiveDownloads)
                {
                    return;
                }

                // Check if there is still an image in the queue.
                if (downloadQueue.Count <= 0)
                {
                    return;
                }

                // Increment the active downloads by 1 and start the download process.
                activeDownloads++;
                StartCoroutine(DownloadImage(downloadQueue.Dequeue()));
            }

            /// <summary>
            /// Starts the next restore if available.
            /// </summary>
            /// <param name="reduceCount"></param>
            private void StartNextRestore(bool reduceCount = false)
            {
                // Check if we need to reduce the active restores counter after a succesful or failed restore.
                if (reduceCount)
                {
                    activeRestores--;
                }

                // Check if we don't have the maximum configured amount of restores running already.
                if (activeRestores >= signArtist.Settings.MaxActiveDownloads)
                {
                    return;
                }

                // Check if there is still an image in the queue.
                if (restoreQueue.Count <= 0)
                {
                    return;
                }

                // Increment the active restores by 1 and start the restore process.
                activeRestores++;
                StartCoroutine(RestoreImage(restoreQueue.Dequeue()));
            }

            /// <summary>
            /// Downloads the image and adds it to the sign.
            /// </summary>
            /// <param name="request">The requested <see cref="DownloadRequest"/> instance. </param>
            private IEnumerator DownloadImage(DownloadRequest request)
            {
                using (WWW www = new WWW(request.Url))
                {
                    // Wait for the webrequest to complete
                    yield return www;

                    // Verify that there is a valid reference to the plugin from this class.
                    if (signArtist == null)
                    {
                        throw new NullReferenceException("signArtist");
                    }

                    // Verify that the webrequest was succesful.
                    if (www.error != null)
                    {
                        // The webrequest wasn't succesful, show a message to the player and attempt to start the next download.
                        signArtist.SendMessage(request.Sender, "WebErrorOccurred", www.error);
                        StartNextDownload(true);

                        yield break;
                    }

                    // Verify that the file doesn't exceed the maximum configured filesize.
                    if (www.bytesDownloaded > signArtist.Settings.MaxFileSizeInBytes)
                    {
                        // The file is too large, show a message to the player and attempt to start the next download.
                        signArtist.SendMessage(request.Sender, "FileTooLarge", signArtist.Settings.MaxSize);
                        StartNextDownload(true);

                        yield break;
                    }

                    // Get the bytes array for the image from the webrequest and lookup the target image size for the targetted sign.
                    byte[] imageBytes;

                    if (request.Raw)
                    {
                        imageBytes = www.bytes;
                    }
                    else
                    {
                        imageBytes = GetImageBytes(www);
                    }

                    ImageSize size = GetImageSizeFor(request.Sign);

                    // Verify that we have image size data for the targetted sign.
                    if (size == null)
                    {
                        // No data was found, show a message to the player and print a detailed message to the server console and attempt to start the next download.
                        signArtist.SendMessage(request.Sender, "ErrorOccurred");
                        signArtist.PrintWarning($"Couldn't find the required image size for {request.Sign.PrefabName}, please report this in the plugin's thread.");
                        StartNextDownload(true);

                        yield break;
                    }

                    // Get the bytes array for the resized image for the targetted sign.
                    byte[] resizedImageBytes = imageBytes.ResizeImage(size.Width, size.Height, size.ImageWidth, size.ImageHeight, signArtist.Settings.EnforceJpeg && !request.Raw);

                    // Verify that the resized file doesn't exceed the maximum configured filesize.
                    if (resizedImageBytes.Length > signArtist.Settings.MaxFileSizeInBytes)
                    {
                        // The file is too large, show a message to the player and attempt to start the next download.
                        signArtist.SendMessage(request.Sender, "FileTooLarge", signArtist.Settings.MaxSize);
                        StartNextDownload(true);

                        yield break;
                    }

                    // Check if the sign already has a texture assigned to it.
                    if (request.Sign.textureID > 0)
                    {
                        // A texture was already assigned, remove this file to make room for the new one.
                        FileStorage.server.Remove(request.Sign.textureID, FileStorage.Type.png, request.Sign.net.ID);
                    }

                    // Create the image on the filestorage and send out a network update for the sign.
                    request.Sign.textureID = FileStorage.server.Store(resizedImageBytes, FileStorage.Type.png, request.Sign.net.ID);
                    request.Sign.SendNetworkUpdate();

                    // Notify the player that the image was loaded.
                    signArtist.SendMessage(request.Sender, "ImageLoaded");

                    // Call the Oxide hook 'OnSignUpdated' to notify other plugins of the update event.
                    Interface.Oxide.CallHook("OnSignUpdated", request.Sign, request.Sender);

                    // Check if logging to console is enabled.
                    if (signArtist.Settings.ConsoleLogging)
                    {
                        // Console logging is enabled, show a message in the server console.
                        signArtist.Puts(signArtist.GetTranslation("LogEntry"), request.Sender.displayName,
                            request.Sender.userID, request.Sign.textureID, request.Sign.ShortPrefabName, request.Url);
                    }

                    // Check if logging to file is enabled.
                    if (signArtist.Settings.FileLogging)
                    {
                        // File logging is enabled, add an entry to the logfile.
                        signArtist.LogToFile("log",
                            string.Format(signArtist.GetTranslation("LogEntry"), request.Sender.displayName,
                                request.Sender.userID, request.Sign.textureID, request.Sign.ShortPrefabName,
                                request.Url), signArtist);
                    }

                    // Attempt to start the next download.
                    StartNextDownload(true);
                }
            }

            /// <summary>
            /// Restores the image and adds it to the sign again.
            /// </summary>
            /// <param name="request">The requested <see cref="RestoreRequest"/> instance. </param>
            /// <returns></returns>
            private IEnumerator RestoreImage(RestoreRequest request)
            {
                // Verify that there is a valid reference to the plugin from this class.
                if (signArtist == null)
                {
                    throw new NullReferenceException("signArtist");
                }

                byte[] imageBytes;

                // Check if the sign already has a texture assigned to it.
                if (request.Sign.textureID == 0)
                {
                    // No texture was previously assigned, show a message to the player.
                    signArtist.SendMessage(request.Sender, "RestoreErrorOccurred");
                    StartNextRestore(true);

                    yield break;
                }

                // Cache the byte array of the currently stored file.
                imageBytes = FileStorage.server.Get(request.Sign.textureID, FileStorage.Type.png, request.Sign.net.ID);
                ImageSize size = GetImageSizeFor(request.Sign);

                // Verify that we have image size data for the targetted sign.
                if (size == null)
                {
                    // No data was found, show a message to the player and print a detailed message to the server console and attempt to start the next download.
                    signArtist.SendMessage(request.Sender, "ErrorOccurred");
                    signArtist.PrintWarning($"Couldn't find the required image size for {request.Sign.PrefabName}, please report this in the plugin's thread.");
                    StartNextRestore(true);

                    yield break;
                }

                // Remove the texture from the FileStorage.
                FileStorage.server.Remove(request.Sign.textureID, FileStorage.Type.png, request.Sign.net.ID);

                // Get the bytes array for the resized image for the targetted sign.
                byte[] resizedImageBytes = imageBytes.ResizeImage(size.Width, size.Height, size.ImageWidth, size.ImageHeight, signArtist.Settings.EnforceJpeg && !request.Raw);

                // Create the image on the filestorage and send out a network update for the sign.
                request.Sign.textureID = FileStorage.server.Store(resizedImageBytes, FileStorage.Type.png, request.Sign.net.ID);
                request.Sign.SendNetworkUpdate();

                // Notify the player that the image was loaded.
                signArtist.SendMessage(request.Sender, "ImageRestored");

                // Call the Oxide hook 'OnSignUpdated' to notify other plugins of the update event.
                Interface.Oxide.CallHook("OnSignUpdated", request.Sign, request.Sender);

                // Attempt to start the next download.
                StartNextRestore(true);
            }

            /// <summary>
            /// Gets the target image size for a <see cref="Signage"/>.
            /// </summary>
            /// <param name="signage"></param>
            private ImageSize GetImageSizeFor(Signage signage)
            {
                if (signArtist.ImageSizePerAsset.ContainsKey(signage.PrefabName))
                {
                    return signArtist.ImageSizePerAsset[signage.PrefabName];
                }

                return null;
            }

            /// <summary>
            /// Converts the <see cref="Texture2D"/> from the webrequest to a <see cref="byte"/> array.
            /// </summary>
            /// <param name="www">The completed webrequest. </param>
            private byte[] GetImageBytes(WWW www)
            {
                Texture2D texture = www.texture;
                byte[] image;

                if (texture.format == TextureFormat.ARGB32 && !signArtist.Settings.EnforceJpeg)
                {
                    image = texture.EncodeToPNG();
                }
                else
                {
                    image = texture.EncodeToJPG(signArtist.Settings.Quality);
                }

                DestroyImmediate(texture);

                return image;
            }
        }

        /// <summary>
        /// Oxide hook that is triggered when the plugin is loaded.
        /// </summary>
        private void Init()
        {
            // Register all the permissions used by the plugin
            permission.RegisterPermission("signartist.file", this);
            permission.RegisterPermission("signartist.ignorecd", this);
            permission.RegisterPermission("signartist.ignoreowner", this);
            permission.RegisterPermission("signartist.raw", this);
            permission.RegisterPermission("signartist.restore", this);
            permission.RegisterPermission("signartist.restoreall", this);
            permission.RegisterPermission("signartist.text", this);
            permission.RegisterPermission("signartist.url", this);

            // Initialize the dictionary with all paintable object assets and their target sizes
            ImageSizePerAsset = new Dictionary<string, ImageSize>()
            {
                // Picture Frames
                ["assets/prefabs/deployable/signs/sign.pictureframe.landscape.prefab"] = new ImageSize(256, 128), // Landscape Picture Frame
                ["assets/prefabs/deployable/signs/sign.pictureframe.portrait.prefab"] = new ImageSize(128, 256),  // Portrait Picture Frame
                ["assets/prefabs/deployable/signs/sign.pictureframe.tall.prefab"] = new ImageSize(128, 512),      // Tall Picture Frame
                ["assets/prefabs/deployable/signs/sign.pictureframe.xl.prefab"] = new ImageSize(512, 512),        // XL Picture Frame
                ["assets/prefabs/deployable/signs/sign.pictureframe.xxl.prefab"] = new ImageSize(1024, 512),      // XXL Picture Frame

                // Wooden Signs
                ["assets/prefabs/deployable/signs/sign.small.wood.prefab"] = new ImageSize(128, 64),              // Small Wooden Sign
                ["assets/prefabs/deployable/signs/sign.medium.wood.prefab"] = new ImageSize(256, 128),            // Wooden Sign
                ["assets/prefabs/deployable/signs/sign.large.wood.prefab"] = new ImageSize(256, 128),             // Large Wooden Sign
                ["assets/prefabs/deployable/signs/sign.huge.wood.prefab"] = new ImageSize(512, 128),              // Huge Wooden Sign

                // Banners
                ["assets/prefabs/deployable/signs/sign.hanging.banner.large.prefab"] = new ImageSize(64, 256),    // Large Banner Hanging
                ["assets/prefabs/deployable/signs/sign.pole.banner.large.prefab"] = new ImageSize(64, 256),       // Large Banner on Pole

                // Hanging Signs
                ["assets/prefabs/deployable/signs/sign.hanging.prefab"] = new ImageSize(128, 256),                // Two Sided Hanging Sign
                ["assets/prefabs/deployable/signs/sign.hanging.ornate.prefab"] = new ImageSize(256, 128),         // Two Sided Ornate Hanging Sign

                // Town Signs
                ["assets/prefabs/deployable/signs/sign.post.single.prefab"] = new ImageSize(128, 64),             // Single Sign Post
                ["assets/prefabs/deployable/signs/sign.post.double.prefab"] = new ImageSize(256, 256),            // Double Sign Post
                ["assets/prefabs/deployable/signs/sign.post.town.prefab"] = new ImageSize(256, 128),              // One Sided Town Sign Post
                ["assets/prefabs/deployable/signs/sign.post.town.roof.prefab"] = new ImageSize(256, 128),         // Two Sided Town Sign Post

                // Other paintable assets
                ["assets/prefabs/deployable/spinner_wheel/spinner.wheel.deployed.prefab"] = new ImageSize(512, 512, 285, 285), // Spinning Wheel
            };
        }

        /// <summary>
        /// Oxide hook that is triggered automatically after it has been loaded to initialize the messages for the Lang API.
        /// </summary>
        protected override void LoadDefaultMessages()
        {
            // Register all messages used by the plugin in the Lang API.
            lang.RegisterMessages(new Dictionary<string, string>
            {
                // Messages used throughout the plugin.
                ["WebErrorOccurred"] = "Failed to download the image! Error {0}.",
                ["FileTooLarge"] = "The file exceeds the maximum file size of {0}Mb.",
                ["ErrorOccurred"] = "An unknown error has occured, if this error keeps occuring please notify the server admin.",
                ["RestoreErrorOccurred"] = "Can't restore the sign because no texture is assigned to it.",
                ["DownloadQueued"] = "Your image was added to the download queue!",
                ["RestoreQueued"] = "Your sign was added to the restore queue!",
                ["RestoreBatchQueued"] = "You added all {0} signs to the restore queue!",
                ["ImageLoaded"] = "The image was succesfully loaded to the sign!",
                ["ImageRestored"] = "The image was succesfully restored for the sign!",
                ["LogEntry"] = "Player `{0}` (SteamId: {1}) loaded {2} into {3} from {4}",
                ["NoSignFound"] = "Unable to find a sign! Make sure you are looking at one and that you are not too far away from it.",
                ["Cooldown"] = "You can't use the command yet! Remaining cooldown: {0}.",
                ["SignNotOwned"] = "You can't change this sign as it is protected by a tool cupboard.",
                ["ActionQueuedAlready"] = "An action has already been queued for this sign, please wait for this action to complete.",
                ["SyntaxSilCommand"] = "Syntax error!\nSyntax: /sil <url> [raw]",
                ["SyntaxSiltCommand"] = "Syntax error!\nSyntax: /silt <message> [<fontsize:number>] [<color:hex value>] [<bgcolor:hex value>] [raw]",
                ["NoPermission"] = "You don't have permission to use this command.",
                ["NoPermissionFile"] = "You don't have permission to use images from the server's filesystem.",
                ["NoPermissionRaw"] = "You don't have permission to use raw images, loading normally instead.",
                ["NoPermissionRestoreAll"] = "You don't have permission to use restore all signs at once.",

                // Cooldown formatting 'translations'.
                ["day"] = "day",
                ["days"] = "days",
                ["hour"] = "hour",
                ["hours"] = "hours",
                ["minute"] = "minute",
                ["minutes"] = "minutes",
                ["second"] = "second",
                ["seconds"] = "seconds",
                ["and"] = "and"
            }, this);
        }

        /// <summary>
        /// Oxide hook that is triggered to automatically load the configuration file.
        /// </summary>
        protected override void LoadConfig()
        {
            base.LoadConfig();
            Settings = Config.ReadObject<SignArtistConfig>();
            SaveConfig();
        }

        /// <summary>
        /// Oxide hook that is triggered to automatically load the default configuration file when no file exists.
        /// </summary>
        protected override void LoadDefaultConfig()
        {
            Settings = SignArtistConfig.DefaultConfig();
        }

        /// <summary>
        /// Oxide hook that is triggered to save the configuration file.
        /// </summary>
        protected override void SaveConfig()
        {
            Config.WriteObject(Settings);
        }

        /// <summary>
        /// Oxide hook that is triggered when the server has fully initialized.
        /// </summary>
        private void OnServerInitialized()
        {
            // Create a new GameObject and attach the UnityEngine script to it for handling the image downloads.
            imageDownloaderGameObject = new GameObject("ImageDownloader");
            imageDownloader = imageDownloaderGameObject.AddComponent<ImageDownloader>();
        }

        /// <summary>
        /// Oxide hook that is triggered when the plugin is unloaded.
        /// </summary>
        private void Unload()
        {
            // Destroy the created GameObject and cleanup.
            UnityEngine.Object.Destroy(imageDownloaderGameObject);
            imageDownloader = null;
            cooldowns = null;
        }

        /// <summary>
        /// Handles the /sil chat command.
        /// </summary>
        /// <param name="player">The player that has executed the command. </param>
        /// <param name="command">The name of the command that was executed. </param>
        /// <param name="args">All arguments that were passed with the command. </param>
        [ChatCommand("sil")]
        private void SilChatCommand(BasePlayer player, string command, string[] args)
        {
            // Verify if the correct syntax is used.
            if (args.Length < 1)
            {
                // Invalid syntax was used, show an error message to the player.
                SendMessage(player, "SyntaxSilCommand");

                return;
            }

            // Verify if the player has permission to use this command.
            if (!HasPermission(player, "signartist.url"))
            {
                // The player doesn't have permission to use this command, show an error message.
                SendMessage(player, "NoPermission");

                return;
            }

            // Verify that the command isn't on cooldown for the user.
            if (HasCooldown(player))
            {
                // The command is still on cooldown for the player, show an error message.
                SendMessage(player, "Cooldown", FormatCooldown(GetCooldown(player)));

                return;
            }

            // Check if the player is looking at a sign.
            Signage sign;
            if (!IsLookingAtSign(player, out sign))
            {
                // The player isn't looking at a sign or is too far away from it, show an error message.
                SendMessage(player, "NoSignFound");

                return;
            }

            // Check if the player is able to update the sign.
            if (!CanChangeSign(player, sign))
            {
                // The player isn't able to update the sign, show an error message.
                SendMessage(player, "SignNotOwned");

                return;
            }

            // Check if the player wants to add the image from the server's filesystem and has the permission to do so.
            if (args[0].StartsWith("file://") && !HasPermission(player, "signartist.file"))
            {
                // The player doesn't have permission for this, show an error message.
                SendMessage(player, "NoPermissionFile");

                return;
            }

            // Check if the player wants to add the image as a raw image and has the permission to do so.
            bool raw = args.Length > 1 && args[1].Equals("raw", StringComparison.OrdinalIgnoreCase);
            if (raw && !HasPermission(player, "signartist.raw"))
            {
                // The player doesn't have permission for this, show a message and disable raw.
                SendMessage(player, "NoPermissionRaw");
                raw = false;
            }

            // Notify the player that it is added to the queue.
            SendMessage(player, "DownloadQueued");

            // Queue the download of the specified image.
            imageDownloader.QueueDownload(args[0], player, sign, raw);

            // Set the cooldown on the command for the player if the cooldown setting is enabled.
            SetCooldown(player);
        }

        /// <summary>
        /// Handles the sil console command
        /// </summary>
        /// <param name="arg"><see cref="ConsoleSystem.Arg"/> running the command. </param>
        [ConsoleCommand("sil")]
        private void SilConsoleCommand(ConsoleSystem.Arg arg)
        {
            // Verify that the command was run from an ingame console.
            if (arg.Player() == null)
            {
                // It wasn't run from an ingame console, do nothing.
                return;
            }

            // Manually trigger the chat command with the console command args.
            SilChatCommand(arg.Player(), "sil", arg.Args ?? new string[0]);
        }

        /// <summary>
        /// Handles the /silt chat command
        /// </summary>
        /// <param name="player">The player that has executed the command. </param>
        /// <param name="command">The name of the command that was executed. </param>
        /// <param name="args">All arguments that were passed with the command. </param>
        [ChatCommand("silt")]
        private void SiltChatCommand(BasePlayer player, string command, string[] args)
        {
            // Verify if the correct syntax is used.
            if (args.Length < 1)
            {
                // Invalid syntax was used, show an error message to the player.
                SendMessage(player, "SyntaxSiltCommand");

                return;
            }

            // Verify if the player has permission to use this command.
            if (!HasPermission(player, "signartist.text"))
            {
                // The player doesn't have permission to use this command, show an error message.
                SendMessage(player, "NoPermission");

                return;
            }

            // Verify that the command isn't on cooldown for the user.
            if (HasCooldown(player))
            {
                // The command is still on cooldown for the player, show an error message.
                SendMessage(player, "Cooldown", FormatCooldown(GetCooldown(player)));

                return;
            }

            // Check if the player is looking at a sign.
            Signage sign;
            if (!IsLookingAtSign(player, out sign))
            {
                // The player isn't looking at a sign or is too far away from it, show an error message.
                SendMessage(player, "NoSignFound");

                return;
            }

            // Check if the player is able to update the sign.
            if (!CanChangeSign(player, sign))
            {
                // The player isn't able to update the sign, show an error message.
                SendMessage(player, "SignNotOwned");

                return;
            }

            // Build the URL for the /silt command
            string message = args[0].EscapeForUrl();
            int fontsize = 80;
            string color = "000";
            string bgcolor = "0FFF";
            string format = "png32";

            // Replace the default fontsize if the player specified one.
            if (args.Length > 1)
            {
                int.TryParse(args[1], out fontsize);
            }

            // Replace the default color if the player specified one.
            if (args.Length > 2)
            {
                color = args[2].Trim(' ', '#');
            }

            // Replace the default color if the player specified one.
            if (args.Length > 3)
            {
                bgcolor = args[3].Trim(' ', '#');
            }

            // Check if the player wants to add the image as a raw image and has the permission to do so.
            bool raw = args.Length > 4 && args[4].Equals("raw", StringComparison.OrdinalIgnoreCase);
            if (raw && !HasPermission(player, "signartist.raw"))
            {
                // The player doesn't have permission for this, show a message and disable raw.
                SendMessage(player, "NoPermissionRaw");
                raw = false;
            }

            // Correct the format if required
            if (Settings.EnforceJpeg)
            {
                format = "jpg";
            }

            // Get the size for the image
            ImageSize size = null;
            if (ImageSizePerAsset.ContainsKey(sign.PrefabName))
            {
                size = ImageSizePerAsset[sign.PrefabName];
            }

            // Verify that we have image size data for the targetted sign.
            if (size == null)
            {
                // No data was found, show a message to the player and print a detailed message to the server console and attempt to start the next download.
                SendMessage(player, "ErrorOccurred");
                PrintWarning($"Couldn't find the required image size for {sign.PrefabName}, please report this in the plugin's thread.");

                return;
            }

            // Combine all the values into the url;
            string url = $"http://placeholdit.imgix.net/~text?fm={format}&txtsize={fontsize}&txt={message}&w={size.ImageWidth}&h={size.ImageHeight}&txtclr={color}&bg={bgcolor}";

            // Notify the player that it is added to the queue.
            SendMessage(player, "DownloadQueued");

            // Queue the download of the specified image.
            imageDownloader.QueueDownload(url, player, sign, raw);

            // Set the cooldown on the command for the player if the cooldown setting is enabled.
            SetCooldown(player);
        }

        /// <summary>
        /// Handles the sil console command
        /// </summary>
        /// <param name="arg"><see cref="ConsoleSystem.Arg"/> running the command. </param>
        [ConsoleCommand("silt")]
        private void SiltConsoleCommand(ConsoleSystem.Arg arg)
        {
            // Verify that the command was run from an ingame console.
            if (arg.Player() == null)
            {
                // It wasn't run from an ingame console, do nothing.
                return;
            }

            // Manually trigger the chat command with the console command args.
            SiltChatCommand(arg.Player(), "silt", arg.Args ?? new string[0]);
        }

        [ChatCommand("silrestore")]
        private void RestoreCommand(BasePlayer player, string command, string[] args)
        {
            // Verify if the player has permission to use this command.
            if (!HasPermission(player, "signartist.restore"))
            {
                // The player doesn't have permission to use this command, show an error message.
                SendMessage(player, "NoPermission");

                return;
            }

            // Check if the user wants to restore the sign or signs as raw images and has the permission to do so
            bool raw = string.IsNullOrEmpty(args.FirstOrDefault(s => s.Equals("raw", StringComparison.OrdinalIgnoreCase)));
            if (raw && !HasPermission(player, "signartist.raw"))
            {
                // The player doesn't have permission for this, show a message and disable raw.
                SendMessage(player, "NoPermissionRaw");
                raw = false;
            }

            // Check if the user wants to restore all signs and has the permission to do so.
            bool all = args.Any(s => s.Equals("all", StringComparison.OrdinalIgnoreCase));
            if (all && !HasPermission(player, "signartist.restoreall"))
            {
                // The player doesn't have permission for this, show a message and disable raw.
                SendMessage(player, "NoPermissionRestoreAll");

                return;
            }

            // Check if the player is looking at a sign if not all signs should be restored.
            if (!all)
            {
                Signage sign;
                if (!IsLookingAtSign(player, out sign))
                {
                    // The player isn't looking at a sign or is too far away from it, show an error message.
                    SendMessage(player, "NoSignFound");

                    return;
                }

                // Notify the player that it is added to the queue.
                SendMessage(player, "RestoreQueued");

                // Queue the restore of the image on the specified sign.
                imageDownloader.QueueRestore(player, sign, raw);

                return;
            }

            // The player wants to restore all signs.
            Signage[] allSigns = UnityEngine.Object.FindObjectsOfType<Signage>();

            // Notify the player that they were added to the queue
            SendMessage(player, "RestoreBatchQueued", allSigns.Length);

            // Queue every sign to be restored.
            foreach (Signage sign in allSigns)
            {
                imageDownloader.QueueRestore(player, sign, raw);
            }
        }

        /// <summary>
        /// Check if the given <see cref="BasePlayer"/> is able to use the command.
        /// </summary>
        /// <param name="player">The player to check. </param>
        private bool HasCooldown(BasePlayer player)
        {
            // Check if cooldown is enabled.
            if (Settings.Cooldown <= 0)
            {
                return false;
            }

            // Check if cooldown is ignored for the player.
            if (HasPermission(player, "signartist.ignorecd"))
            {
                return false;
            }

            // Make sure there is an entry for the player in the dictionary.
            if (!cooldowns.ContainsKey(player.userID))
            {
                cooldowns.Add(player.userID, 0);
            }

            // Check if the command is on cooldown or not.
            return Time.realtimeSinceStartup - cooldowns[player.userID] < Settings.Cooldown;
        }

        /// <summary>
        /// Returns the cooldown in seconds for the given <see cref="BasePlayer"/>.
        /// </summary>
        /// <param name="player">The player to obtain the cooldown of. </param>
        private float GetCooldown(BasePlayer player)
        {
            return Time.realtimeSinceStartup - cooldowns[player.userID];
        }

        /// <summary>
        /// Sets the last use for the cooldown handling of the command for the given <see cref="BasePlayer"/>.
        /// </summary>
        /// <param name="player">The player to put the command on cooldown for. </param>
        private void SetCooldown(BasePlayer player)
        {
            // Check if cooldown is enabled.
            if (Settings.Cooldown <= 0)
            {
                return;
            }

            // Check if cooldown is ignored for the player.
            if (HasPermission(player, "signartist.ignorecd"))
            {
                return;
            }

            // Make sure there is an entry for the player in the dictionary.
            if (!cooldowns.ContainsKey(player.userID))
            {
                cooldowns.Add(player.userID, 0);
            }

            // Set the last use
            cooldowns[player.userID] = Time.realtimeSinceStartup;
        }

        /// <summary>
        /// Returns a formatted string for the given cooldown.
        /// </summary>
        /// <param name="seconds">The cooldown in seconds. </param>
        private string FormatCooldown(float seconds)
        {
            // Create a new TimeSpan from the remaining cooldown.
            TimeSpan t = TimeSpan.FromSeconds(seconds);
            List<string> output = new List<string>();

            // Check if it is more than a single day and add it to the result.
            if (t.Days >= 1)
            {
                output.Add($"{t.Days} {(t.Days > 1 ? "days" : "day")}");
            }

            // Check if it is more than an hour and add it to the result.
            if (t.Hours >= 1)
            {
                output.Add($"{t.Hours} {(t.Hours > 1 ? "hours" : "hour")}");
            }

            // Check if it is more than a minute and add it to the result.
            if (t.Minutes >= 1)
            {
                output.Add($"{t.Minutes} {(t.Minutes > 1 ? "minutes" : "minute")}");
            }

            // Check if there is more than a second and add it to the result.
            if (t.Seconds >= 1)
            {
                output.Add($"{t.Seconds} {(t.Seconds > 1 ? "seconds" : "second")}");
            }

            // Format the result and return it.
            return output.Count >= 3 ? output.ToSentence().Replace(" and", ", and") : output.ToSentence();
        }

        /// <summary>
        /// Checks if the <see cref="BasePlayer"/> is looking at a valid <see cref="Signage"/> object.
        /// </summary>
        /// <param name="player">The player to check. </param>
        /// <param name="sign">When this method returns, contains the <see cref="Signage"/> the player contained in <paramref name="player" /> is looking at, or null if the player isn't looking at a sign. </param>
        private bool IsLookingAtSign(BasePlayer player, out Signage sign)
        {
            RaycastHit hit;
            sign = null;

            // Get the object that is in front of the player within the maximum distance set in the config.
            if (Physics.Raycast(player.eyes.HeadRay(), out hit, Settings.MaxDistance))
            {
                // Attempt to grab the Signage entity, if there is none this will set the sign to null, 
                // otherwise this will set it to the sign the player is looking at.
                sign = hit.GetEntity() as Signage;
            }

            // Return true or false depending on if we found a sign.
            return sign != null;
        }

        /// <summary>
        /// Checks if the <see cref="BasePlayer"/> is allowed to change the drawing on the <see cref="Signage"/> object.
        /// </summary>
        /// <param name="player">The player to check. </param>
        /// <param name="sign">The sign to check. </param>
        /// <returns></returns>
        private bool CanChangeSign(BasePlayer player, Signage sign)
        {
            return sign.CanUpdateSign(player) || HasPermission(player, "signartist.ignoreowner");
        }

        /// <summary>
        /// Checks if the given <see cref="BasePlayer"/> has the specified permission.
        /// </summary>
        /// <param name="player">The player to check a permission on. </param>
        /// <param name="perm">The permission to check for. </param>
        private bool HasPermission(BasePlayer player, string perm)
        {
            return permission.UserHasPermission(player.UserIDString, perm);
        }

        /// <summary>
        /// Send a formatted message to a single player.
        /// </summary>
        /// <param name="player">The player to send the message to. </param>
        /// <param name="key">The key of the message from the Lang API to get the message for. </param>
        /// <param name="args">Any amount of arguments to add to the message. </param>
        private void SendMessage(BasePlayer player, string key, params object[] args)
        {
            player.ChatMessage(string.Format(GetTranslation(key, player), args));
        }

        /// <summary>
        /// Gets the message for a specific player from the Lang API.
        /// </summary>
        /// <param name="key">The key of the message from the Lang API to get the message for. </param>
        /// <param name="player">The player to get the message for. </param>
        /// <returns></returns>
        private string GetTranslation(string key, BasePlayer player = null)
        {
            return lang.GetMessage(key, this, player?.UserIDString);
        }
    }

    namespace SignArtistClasses
    {
        /// <summary>
        /// Extension class with extension methods used by the <see cref="SignArtist"/> plugin.
        /// </summary>
        public static class Extensions
        {
            /// <summary>
            /// Resizes an image from the <see cref="byte"/> array to a new image with a specific width and height.
            /// </summary>
            /// <param name="bytes">Source image. </param>
            /// <param name="width">New image canvas width. </param>
            /// <param name="height">New image canvas height. </param>
            /// <param name="targetWidth">New image width. </param>
            /// <param name="targetHeight">New image height. </param>
            /// <param name="enforceJpeg"><see cref="bool"/> value, true to save the images as JPG, false for PNG. </param>
            public static byte[] ResizeImage(this byte[] bytes, int width, int height, int targetWidth, int targetHeight, bool enforceJpeg)
            {
                byte[] resizedImageBytes;

                using (MemoryStream originalBytesStream = new MemoryStream(), resizedBytesStream = new MemoryStream())
                {
                    // Write the downloaded image bytes array to the memorystream and create a new Bitmap from it.
                    originalBytesStream.Write(bytes, 0, bytes.Length);
                    Bitmap image = new Bitmap(originalBytesStream);

                    // Check if the width and height match, if they don't we will have to resize this image.
                    if (image.Width != width || image.Height != height)
                    {
                        // Create a new Bitmap with the target size.
                        Bitmap resizedImage = new Bitmap(width, height);

                        // Draw the original image onto the new image and resize it accordingly.
                        using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(resizedImage))
                        {
                            graphics.DrawImage(image, new Rectangle(0, 0, targetWidth, targetHeight));
                        }

                        // Save the bitmap to a MemoryStream as either Jpeg or Png.
                        if (enforceJpeg)
                        {
                            resizedImage.Save(resizedBytesStream, ImageFormat.Jpeg);
                        }
                        else
                        {
                            resizedImage.Save(resizedBytesStream, ImageFormat.Png);
                        }

                        // Grab the bytes array from the new image's MemoryStream and dispose of the resized image Bitmap.
                        resizedImageBytes = resizedBytesStream.ToArray();
                        resizedImage.Dispose();
                    }
                    else
                    {
                        // The image has the correct size so we can just return the original bytes without doing any resizing.
                        resizedImageBytes = bytes;
                    }

                    // Dispose of the original image Bitmap.
                    image.Dispose();
                }

                // Return the bytes array.
                return resizedImageBytes;
            }

            /// <summary>
            /// Converts a string to its escaped representation for the image placeholder text value.
            /// </summary>
            /// <param name="stringToEscape">The string to escape.</param>
            public static string EscapeForUrl(this string stringToEscape)
            {
                // Escape initial values.
                stringToEscape = Uri.EscapeDataString(stringToEscape);

                // Convert \r\n, \r and \n into linebreaks.
                stringToEscape = stringToEscape.Replace("%5Cr%5Cn", "%5Cn").Replace("%5Cr", "%5Cn").Replace("%5Cn", "%0A");

                // Return the converted message
                return stringToEscape;
            }
        }
    }
}