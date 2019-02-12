using System.Collections.Generic;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("CloudflareUpdater", "PsychoTea", "1.0.0")]
    [Description("Updates your Cloudflare domains with your server's IP")]

    class CloudflareUpdater : CovalencePlugin
    {
        Timer _timer;
        const float timeout = 200f;
        Dictionary<string, string> header;
        Dictionary<string, int> fails = new Dictionary<string, int>();

        //URLs
        const string ZoneIDUrl = "https://api.cloudflare.com/client/v4/zones?name={0}";
        const string RecordIDUrl = "https://api.cloudflare.com/client/v4/zones/{0}/dns_records?name={1}&type=A";
        const string IPUrl = "http://canihazip.com/s";
        const string UpdateIPUrl = "https://api.cloudflare.com/client/v4/zones/{0}/dns_records/{1}";

        //Config
        string ZoneName;
        Dictionary<string, bool> DomainNames = new Dictionary<string, bool>();

        class Request
        {
            [JsonProperty("result")]
            public ResultObj[] Result { get; set; }

            public class ResultObj
            {
                [JsonProperty("id")]
                public string ID { get; set; }
            }
        }

        #region Oxide Hooks

        void Init()
        {
            ZoneName = GetConfig<string>("Zone Name");
            foreach (var kvp in GetConfig<Dictionary<string, object>>("Domain Names - Use Proxy"))
                DomainNames.Add(kvp.Key, (bool)kvp.Value);

            header = new Dictionary<string, string>()
            {
                { "X-Auth-Email", GetConfig<string>("Login Email") },
                { "X-Auth-Key", GetConfig<string>("API Key") },
                { "Content-Type", "application/json" }
            };

            if (header["X-Auth-Email"] == "me@example.org" || string.IsNullOrEmpty(header["X-Auth-Email"]) || 
                header["X-Auth-Key"] == "<Insert API key here>" || string.IsNullOrEmpty(header["X-Auth-Key"]))
                Puts("Please update your config file with your Cloudflare email and API key.");

            _timer = timer.Repeat(GetConfig<int>("Seconds Between Updates"), 0, () => UpdateIPs());
            UpdateIPs();
        }

        void Unload() => _timer.Destroy();

        protected override void LoadDefaultConfig()
        {
            Puts("Generating new config...");

            Config["Seconds Between Updates"] = 300;
            Config["API Key"] = "<Insert API key here>";
            Config["Login Email"] = "me@example.org";
            Config["Zone Name"] = "example.org";
            Config["Domain Names - Use Proxy"] = new Dictionary<string, bool>
            {
                { "play.example.org", false },
                { "website.example.org", true }
            };

            Puts("New config file generated.");
        }

        #endregion

        #region Custom Functions

        void UpdateIPs()
        {
            foreach (var domainName in DomainNames.Keys)
            {
                Puts($"Updating {domainName}...");
                DoUpdate(domainName);
            }
        }

        void DoUpdate(string domainName, string zoneID = null, string recordID = null, string newIP = null, bool done = false)
        {
            if (!fails.ContainsKey(domainName))
                fails.Add(domainName, 0);

            if (fails[domainName] >= 3)
            {
                Puts($"Failed to update domain {domainName}. Update cancelled.");
                fails.Remove(domainName);
                return;
            }

            if (zoneID == null)
            {
                GetZoneID(domainName);
                return;
            }

            if (recordID == null)
            {
                GetRecordID(domainName, zoneID);
                return;
            }

            if (newIP == null)
            {
                GetIP(domainName, zoneID, recordID);
                return;
            }

            if (!done) UpdateIPTo(domainName, zoneID, recordID, newIP);
            else Puts($"Updated {domainName} successfully.");
        }

        #endregion

        #region Web Requests

        void GetZoneID(string domainName)
        {
            webrequest.EnqueueGet(string.Format(ZoneIDUrl, ZoneName), (code, response) =>
            {
                if (code == 200 && response != null)
                {
                    var zoneIDResponse = JsonConvert.DeserializeObject<Request>(response);
                    string zoneID = zoneIDResponse.Result[0].ID;
                    DoUpdate(domainName, zoneID);
                }
                else
                {
                    Puts($"Failed to get zone ID for domain {domainName}. Code: {code}\nRetrying in 3s...");
                    timer.Once(3f, () => DoUpdate(domainName));
                    fails[domainName]++;
                }

            }, this, header, timeout);
        }
        
        void GetRecordID(string domainName, string zoneID)
        {
            webrequest.EnqueueGet(string.Format(RecordIDUrl, zoneID, domainName), (code, response) =>
            {
                if (code == 200 && response != null)
                {
                    var recordIDResponse = JsonConvert.DeserializeObject<Request>(response);
                    string recordID = recordIDResponse.Result[0].ID;
                    DoUpdate(domainName, zoneID, recordID);
                }
                else
                {
                    Puts($"Failed to get record ID for domain {domainName}. Code: {code}\nRetrying in 3s...");
                    timer.Once(3f, () => DoUpdate(domainName, zoneID));
                    fails[domainName]++;
                }

            }, this, header, timeout);
        }

        void GetIP(string domainName, string zoneID, string recordID)
        {
            webrequest.EnqueueGet(IPUrl, (code, response) =>
            {
                if (code == 200 && response != null)
                {
                    DoUpdate(domainName, zoneID, recordID, response);
                }
                else
                {
                    Puts($"Failed to get IP. Code: {code}\nRetrying in 3s...");
                    timer.Once(3f, () => DoUpdate(domainName, zoneID, recordID));
                    fails[domainName]++;
                }

            }, this, null, timeout);
        }

        void UpdateIPTo(string domainName, string zoneID, string recordID, string newIP)
        {
            Dictionary<string, object> data = new Dictionary<string, object>()
            {
                { "type", "A" },
                { "name", domainName },
                { "content", newIP },
                { "proxied", UseProxy(domainName) }
            };

            webrequest.EnqueuePut(string.Format(UpdateIPUrl, zoneID, recordID), JsonConvert.SerializeObject(data), (code, response) =>
            {
                if (code == 200 && response != null)
                {
                    DoUpdate(domainName, zoneID, recordID, newIP, true);
                }
                else
                {
                    Puts($"Failed to push IP update. Code: {code}\nRetrying in 3s...");
                    timer.Once(3f, () => DoUpdate(domainName, zoneID, recordID));
                    fails[domainName]++;
                }
            }, this, header, timeout);
        }

        #endregion

        #region Helpers

        T GetConfig<T>(string key) => (T)Config[key];

        bool UseProxy(string domainName) => DomainNames[domainName];

        #endregion
    }
}