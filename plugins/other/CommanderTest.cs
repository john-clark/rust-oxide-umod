using System;
using System.Collections.Generic;
using System.Linq;

/*
	TODO:
			- (?) Make code smaller
			- Make required/not required parameters
			- Command aliases?
*/

/*
		cmdr.AddAlias("ol", "options list");
		cmdr.AddAlias("fad", "add 10 fixed lul");
*/

namespace Oxide.Plugins
{
	[Info("Testing Commander", "deer_SWAG", "1.0.0", ResourceId = 0)]
	[Description("Tests stuff")]
	class CommanderTest : RustPlugin
	{
		Commander cmdr;

		void Init()
		{
			cmdr = new Commander();
			cmdr.Add(null, null, (player, args) =>
			{
				Puts("ROOT yey");
			});
			cmdr.Add("add", null, (player, args) =>
			{
				foreach (var item in args)
				{
					Puts("{0}: {1}", item.Key, item.Value);
				}
			}).AddParam("name", Commander.ParamType.String);

			var group = cmdr.AddGroup("find");
				group.Add(null, null, (player, args) =>
				{
					Puts("a group help text");
				});
			var group2 = group.AddGroup("player");
				group2.Add("name", null, (p, a) =>
				{
					Puts("Hello from group2");
				});
		}

		[ChatCommand("commander")]
		void cmdChat(BasePlayer player, string command, string[] args)
		{
			cmdr.Run(player, args);
		}

		[ConsoleCommand("test.commander")]
		void conCmd(ConsoleSystem.Arg args)
		{
			cmdr.Run(null, args.Args);
		}

		/// <summary>
		/// Helper class for chat and console commands
		/// </summary>
		class Commander
		{
			#region Definitions

			public delegate bool PermissionCheck(BasePlayer player, string permission);

			/// <summary>Type of parameter for auto convertion</summary>
			public enum ParamType
			{
				/// <summary>Use this if you want a raw string</summary>
				String,
				Int, Float,	Bool,
				/// <summary>Returns a BasePlayer object</summary>
				Player
			}

			public struct Param
			{
				public string Name    { get; set; }
				public ParamType Type { get; set; }
				public bool Greedy    { get; set; }
			}

			public class ParamValue
			{
				/// <summary>Raw value</summary>
				public object Value { get; set; }
				/// <summary>If argument wasn't parse properly</summary>
				public bool IsInvalid { get; set; }

				public string String => Get<string>();
				public int Integer => Get<int>();
				public float Single => Get<float>();
				public BasePlayer Player => Get<BasePlayer>();

				/// <exception cref="InvalidCastException"></exception>
				public T Get<T>()
				{
					return (T)Value;
				}
			}

			public class ValueCollection : Dictionary<string, ParamValue>
			{
				public new ParamValue this[string key]
				{
					get
					{
						ParamValue value;
						this.TryGetValue(key, out value);
						return value;
					}
				}
			}

			public class BaseCommand
			{
				public string Name { get; set; }
				public string Permission { get; set; }

				public virtual void Run(BasePlayer player, string[] args, PermissionCheck check) { }
			}

			public class Command : BaseCommand
			{
				public List<Param> Params => new List<Param>();
				public Action<BasePlayer, ValueCollection> Callback { get; set; }

				public Command AddParam(string name, ParamType type = ParamType.String, bool greedy = false)
				{
					if (name == null)
						throw new NullReferenceException("Name of the parameter can't be null");
					if (greedy && type != ParamType.String)
						throw new InvalidOperationException("Only string-type parameters can be greedy");

					Params.Add(new Param
					{
						Name = name,
						Type = type,
						Greedy =  greedy
					});

					return this;
				}

				public override void Run(BasePlayer player, string[] args, PermissionCheck check)
				{
					if (check != null && !check(player, Permission))
						return;

					var collection = new ValueCollection();

					if (Params.Count > 0)
						CheckParams(args, ref collection);

					Callback?.Invoke(player, collection);
				}

				private void CheckParams(string[] args, ref ValueCollection collection)
				{
					for(int i = 0; i < args.Length; i++)
					{
						if (i >= Params.Count)
							break;

						var arg = args[i];
						var param = Params[i];

						if (param.Greedy)
						{
							collection.Add(param.Name, new ParamValue { Value = string.Join(" ", args, i, args.Length - i) });
							break;
						}

						object value;
						bool result = CheckAndConvert(arg, param.Type, out value);
						collection.Add(param.Name, new ParamValue { Value = value, IsInvalid = !result });
					}
				}

				private bool CheckAndConvert(string arg, ParamType type, out object result)
				{
					switch (type)
					{
						case ParamType.String:
							result = arg;
							return true;
						case ParamType.Int:
							{
								int ret;
								if (int.TryParse(arg, out ret))
								{
									result = ret;
									return true;
								}
								result = ret;
								return false;
							}
						case ParamType.Float:
							{
								float ret;
								if (float.TryParse(arg, out ret))
								{
									result = ret;
									return true;
								}
								result = ret;
								return false;
							}
						case ParamType.Bool:
							{
								bool ret;
								if (ParseBool(arg, out ret))
								{
									result = ret;
									return true;
								}
								result = ret;
								return false;
							}
						case ParamType.Player:
							result = BasePlayer.Find(arg);
							return true;
						default:
							result = null;
							return false;
					}
				}
			}
			
			public class CommandGroup : BaseCommand
			{
				private List<BaseCommand> _children = new List<BaseCommand>();
				private Command _empty;

				public Command Add(string name, string permission, Action<BasePlayer, ValueCollection> callback)
				{
					var cmd = new Command
					{
						Name = name,
						Permission = permission,
						Callback = callback
					};

					if (name == null)
						return _empty = cmd;

					_children.Add(cmd);
					return cmd;
				}

				public CommandGroup AddGroup(string name, string permission = null)
				{
					var group = new CommandGroup
					{
						Name = name,
						Permission = permission
					};

					_children.Add(group);
					return group;
				}

				public override void Run(BasePlayer player, string[] args, PermissionCheck check)
				{
					if (check != null && !check(player, Permission))
						return;

					Commander.Run(_children, _empty, player, args);
				}
			}

			#endregion Definitions

			private List<BaseCommand> _commands = new List<BaseCommand>();
			private Command _empty;
			private PermissionCheck _permCheck;

			public Commander(PermissionCheck permissionCheck = null)
			{
				_permCheck = permissionCheck;
			}

			public Command Add(string name, string permission, Action<BasePlayer, ValueCollection> callback)
			{
				var cmd = new Command
				{
					Name = name,
					Permission = permission,
					Callback = callback
				};

				if (name == null || name.Length == 0)
					return _empty = cmd;

				_commands.Add(cmd);
				return cmd;
			}

			public CommandGroup AddGroup(string name, string permission = null)
			{
				var group = new CommandGroup
				{
					Name = name,
					Permission = permission
				};

				_commands.Add(group);
				return group;
			}

			static protected void Run(List<BaseCommand> cmds, BaseCommand empty, BasePlayer player, string[] args, PermissionCheck check = null)
			{
				if (args == null || args.Length == 0)
				{
					empty?.Run(player, args, check);
					return;
				}

				cmds.Find((cmd) => cmd.Name.Equals(args[0], StringComparison.CurrentCultureIgnoreCase))
				   ?.Run(player, args.Skip(1).ToArray(), check);
			}

			public void Run(BasePlayer player, string[] args)
			{
				Run(_commands, _empty, player, args, _permCheck);
			}

			#region Custom Boolean Parser

			private static string[] _trueBoolValues  = { "true", "yes", "on", "enable", "1" };
			private static string[] _falseBoolValues = { "false", "no", "off", "disable", "0" };

			protected static bool ParseBool(string input, out bool result)
			{
				for (int i = 0; i < _trueBoolValues.Length; i++)
				{
					if (input.Equals(_trueBoolValues[i], StringComparison.CurrentCultureIgnoreCase))
					{
						return result = true;
					}
					else if (input.Equals(_falseBoolValues[i], StringComparison.CurrentCultureIgnoreCase))
					{
						result = false;
						return true;
					}
				}
				
				return result = false;
			}

			#endregion Custom Boolean Parser
		}


	}
}
