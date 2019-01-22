using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("Memory Cache", "austinv900", "1.0.1")]
    [Description("Provides api for in-memory storage")]
    internal class MemoryCache : CovalencePlugin
    {
        /// <summary>
        /// Defines a set of options used to control the behavior of cached items
        /// </summary>
        public sealed class CacheItemOptions
        {

            /// <summary>
            /// Gets or sets an absolute expiration date for the cache entry.
            /// </summary>
            public DateTimeOffset? AbsoluteExpiration { get; set; }

            /// <summary>
            /// Gets or sets an absolute expiration time, relative to now.
            /// </summary>
            public TimeSpan? AbsoluteExpirationRelativeToNow { get; set; }

            /// <summary>
            /// Gets or sets the callback that is fired when a cache object expires
            /// </summary>
            public Action<object> ExpirationCallback { get; set; }

            /// <summary>
            /// Gets or sets how long a cache entry can be inactive (e.g. not accessed) before it will be removed. This will not extend the entry lifetime beyond the absolute expiration (if set).
            /// </summary>
            public TimeSpan? SlidingExpiration { get; set; }

            /// <summary>
            /// Gets or sets the registering plugin so cached items can be removed
            /// </summary>
            public Plugin Plugin { get; set; }
        }

        /// <summary>
        /// Defines a object being stored in memory
        /// </summary>
        private class CacheItem
        {
            /// <summary>
            /// Gets or sets the cached object
            /// </summary>
            public object StoredObject { get; set; }

            /// <summary>
            /// Gets or sets the behavior of the cached object
            /// </summary>
            public CacheItemOptions Options { get; set; }

            /// <summary>
            /// Gets or sets the last access time for this object
            /// </summary>
            public DateTimeOffset LastAccess { get; set; }
        }

        /// <summary>
        /// Contains all currently stored object
        /// </summary>
        private Dictionary<string, CacheItem> _memoryCache = new Dictionary<string, CacheItem>();

        #region Oxide Hooks

        private void Init()
        {
            Unsubscribe(nameof(RunCacheCleanup));
            Unsubscribe(nameof(ProcessCacheCleanupItem));
            Unsubscribe(nameof(_memoryCache));
            Unsubscribe(nameof(ExpireItem));
            Unsubscribe(nameof(IsExpired));
        }

        private void OnPluginUnloaded(Plugin name)
        {
            if (name == this)
            {
                return;
            }

            foreach (var cacheItem in _memoryCache.ToArray())
            {
                var options = cacheItem.Value.Options;
                if (options == null)
                {
                    continue;
                }

                if (!options.AbsoluteExpiration.HasValue && !options.SlidingExpiration.HasValue && options.Plugin != null)
                {
                    if (options.Plugin == name)
                    {
                        ExpireItem(cacheItem.Key, cacheItem.Value);
                    }
                }
            }
        }

        private void Unload()
        {
            foreach (var cacheItem in _memoryCache.ToArray())
            {
                ExpireItem(cacheItem.Key, cacheItem.Value);
            }
        }

        private void OnServerSave() => RunCacheCleanup();

        #endregion



        private void RunCacheCleanup()
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var kv in _memoryCache.ToArray())
            {
                ProcessCacheCleanupItem(kv.Key, kv.Value, kv.Value.Options, now);
            }
        }

        private bool IsExpired(CacheItem item, CacheItemOptions options, DateTimeOffset current)
        {
            if (options.AbsoluteExpiration.HasValue)
            {
                if (options.AbsoluteExpiration >= current)
                {
                    return true;
                }
            }

            if (options.SlidingExpiration.HasValue)
            {
                if ((current - item.LastAccess) >= options.SlidingExpiration.Value)
                {
                    return true;
                }
            }

            return false;
        }

        private void ProcessCacheCleanupItem(string key, CacheItem item, CacheItemOptions options, DateTimeOffset currentTimestamp)
        {
            if (IsExpired(item, options, currentTimestamp))
            {
                ExpireItem(key, item);
            }
        }

        private void ExpireItem(string key, CacheItem item)
        {
            if (item.Options != null && item.Options.ExpirationCallback != null)
            {
                try
                {
                    item.Options.ExpirationCallback.Invoke(item.StoredObject);
                }
                catch (Exception e)
                {
#if DEBUG
                    PrintWarning($"Expiration callback for key '{key}' resulted in error | {e.Message}");
#endif
                }
            }
            else
            {
                if (item.StoredObject is IDisposable)
                {
                    var d = item.StoredObject as IDisposable;

                    try
                    {
                        d.Dispose();
                    }
                    catch(Exception e)
                    {
#if DEBUG
                        PrintWarning($"Failed to dispose IDisposable with key '{key}' | {e.Message}");
#endif
                    }
                }
            }

            _memoryCache.Remove(key);
#if DEBUG
            PrintWarning($"Removed cached item with key {key}");
#endif
        }

        public bool Add(string key, object item, CacheItemOptions options = null)
        {
            if (string.IsNullOrEmpty(key) || item == null)
            {
                return false;
            }

            if (options == null)
            {
                options = new CacheItemOptions();
            }

            if (options.Plugin == null && !options.AbsoluteExpiration.HasValue && !options.SlidingExpiration.HasValue && !options.AbsoluteExpirationRelativeToNow.HasValue)
            {
                options.SlidingExpiration = TimeSpan.FromHours(1);
            }

            var now = DateTimeOffset.UtcNow;
            if (options.AbsoluteExpirationRelativeToNow.HasValue && !options.AbsoluteExpiration.HasValue)
            {
                options.AbsoluteExpiration = now + options.AbsoluteExpirationRelativeToNow.Value;
                options.AbsoluteExpirationRelativeToNow = null;
            }

            if (_memoryCache.ContainsKey(key))
            {
                ExpireItem(key, _memoryCache[key]);
            }

            _memoryCache[key] = new CacheItem()
            {
                Options = options,
                StoredObject = item,
                LastAccess = now
            };

#if DEBUG
            PrintWarning($"Cached new item with key {key} with value {item}");
#endif
            return true;
        }
        
        private bool Add(string key, object item, Action<object> expireCallback, DateTimeOffset? absoluteExpire, TimeSpan? slidingExpire, Plugin plugin)
        {
            if (string.IsNullOrEmpty(key) || item == null)
            {
                return false;
            }

            var options = new CacheItemOptions();

            if (absoluteExpire.HasValue)
            {
                options.AbsoluteExpiration = absoluteExpire;
                if (absoluteExpire >= DateTimeOffset.UtcNow)
                {
                    return false;
                }
            }

            if (slidingExpire.HasValue)
            {
                options.SlidingExpiration = slidingExpire;
            }

            if (expireCallback != null)
            {
                options.ExpirationCallback = expireCallback;
            }

            if (plugin != null)
            {
                options.Plugin = plugin;
            }

            return Add(key, item, options);
        }

        private bool Add(string key, object item, Action<object> expireCallback, DateTimeOffset? absoluteExpire, TimeSpan? slidingExpire) => Add(key, item, expireCallback, absoluteExpire, slidingExpire, (Plugin)null);

        private bool Add(string key, object item, Action<object> expireCallback, TimeSpan? slidingExpire, Plugin plugin) => Add(key, item, expireCallback, (DateTimeOffset?)null, slidingExpire, plugin);

        private bool Add(string key, object item, Action<object> expireCallback, TimeSpan? slidingExpire) => Add(key, item, expireCallback, slidingExpire, (Plugin)null);

        private bool Add(string key, object item, Action<object> expireCallback, DateTimeOffset? absoluteExpire, Plugin plugin) => Add(key, item, expireCallback, absoluteExpire, (TimeSpan?)null, plugin);

        private bool Add(string key, object item, Action<object> expireCallback, DateTimeOffset? absoluteExpire) => Add(key, item, expireCallback, absoluteExpire, (Plugin)null);

        private bool Add(string key, object item, TimeSpan? slidingExpire, Plugin plugin) => Add(key, item, (Action<object>)null, (DateTimeOffset?)null, slidingExpire, plugin);

        private bool Add(string key, object item, TimeSpan? slidingExpire) => Add(key, item, slidingExpire, (Plugin)null);

        private bool Add(string key, object item, DateTimeOffset? absoluteExpire, Plugin plugin) => Add(key, item, null, absoluteExpire, null, plugin);

        private bool Add(string key, object item, DateTimeOffset? absoluteExpire) => Add(key, item, absoluteExpire, (Plugin)null);

        private bool Add(string key, object item, Plugin plugin) => Add(key, item, (Action<object>)null, (DateTimeOffset?)null, (TimeSpan?)null, plugin);

        private bool Add(string key, object item) => Add(key, item, (Plugin)null);

        private object Get(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            var obj = (CacheItem)null;
            if (_memoryCache.TryGetValue(key, out obj))
            {
                if (IsExpired(obj, obj.Options, DateTimeOffset.UtcNow))
                {
                    ExpireItem(key, obj);
                    return null;
                }

                obj.LastAccess = DateTimeOffset.UtcNow;
                return obj.StoredObject;
            }

            return null;
        }

        public TCacheItem Get<TCacheItem>(string key) => (TCacheItem)Get(key);

        private bool Remove(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            if (_memoryCache.ContainsKey(key))
            {
                ExpireItem(key, _memoryCache[key]);
                return true;
            }

            return false;
        }

        private void Remove(Plugin plugin)
        {
            if (plugin == null)
            {
                return;
            }

            OnPluginUnloaded(plugin);
        }
    }
}
