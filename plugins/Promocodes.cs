using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Promocodes", "LaserHydra", "3.0.0", ResourceId = 1471)]
    [Description("Create promotion codes which run commands when redeemed by players")]
    public sealed class Promocodes : CovalencePlugin
    {
        private static Promocodes _instance;
        private List<PromocodeGroup> _promocodeGroups;

        #region Initialization

        private void Init()
        {
            _instance = this;
            _promocodeGroups.ForEach(p => p.FillCodes());
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Invalid Code"] = "The code you entered seems to be either invalid or already redeemed.",
                ["Code Redeemed"] = "You redeemed a code with and received {amount} reward packages."
            }, this);
        }

        #endregion

        #region Config Handling

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _promocodeGroups = Config.ReadObject<List<PromocodeGroup>>();
            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(_promocodeGroups);

        protected override void LoadDefaultConfig() => _promocodeGroups = new List<PromocodeGroup> { PromocodeGroup.GetDefaultGroup() };

        #endregion

        #region Commands

        [Command("redeem")]
        private void RedeemCommand(IPlayer player, string cmd, string[] args)
        {
            if (args.Length != 1)
            {
                player.Reply("Syntax: redeem <code>");
                return;
            }

            string code = args[0];

            int i = 0;
            foreach (var promocodeGroup in _promocodeGroups)
            {
                if (promocodeGroup.IsValidCode(code))
                {
                    promocodeGroup.RedeemCode(player, code);
                    i++;
                }
            }

            player.Reply(i == 0
                ? lang.GetMessage("Invalid Code", this, player.Id)
                : lang.GetMessage("Code Redeemed", this, player.Id).Replace("{amount}", i.ToString()));
        }

        #endregion

        #region Classes

        private sealed class PromocodeGroup
        {
            [JsonProperty("Automatically Fill To (Amount)")]
            private int _fillAmount = 5;

            [JsonProperty("Codes")]
            private List<string> _codes = new List<string>();

            [JsonProperty("Commands")]
            private CommandCall[] _commands = new CommandCall[0];

            public bool IsValidCode(string code) => _codes.Contains(code);

            public void RedeemCode(IPlayer player, string code)
            {
                if (!IsValidCode(code))
                    return;

                foreach (var command in _commands)
                    _instance.server.Command(command.Command, command.GetParameters(player));

                _codes.Remove(code);

                FillCodes();

                _instance?.SaveConfig();
            }

            public void FillCodes()
            {
                if (_codes == null)
                    return;

                while (_codes.Count < _fillAmount)
                    _codes.Add(GenerateCode());

                _instance?.SaveConfig();
            }

            private static string GenerateCode() => Guid.NewGuid().ToString();

            public static PromocodeGroup GetDefaultGroup() => new PromocodeGroup
            {
                _commands = new[]
                {
                    new CommandCall("say", new[] {"{username} just redeemed a code!"})
                }
            };
        }

        public sealed class CommandCall
        {
            [JsonProperty("Parameters")]
            private string[] _parameters;

            [JsonProperty("Command")]
            public string Command { get; private set; }

            public string[] GetParameters(IPlayer player)
            {
                return _parameters.Select(parameter =>
                    parameter.Replace("{id}", player.Id).Replace("{username}", player.Name)
                ).ToArray();
            }

            public CommandCall()
            {   
            }

            public CommandCall(string command, string[] parameters)
            {
                Command = command;
                _parameters = parameters;
            }
        }

        #endregion
    }
}