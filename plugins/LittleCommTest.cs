using System;

namespace Oxide.Plugins
{
	[Info("Testing Commander", "deer_SWAG", "1.0.0", ResourceId = 0)]
	[Description("Tests stuff")]
	class LittleCommTest : RustPlugin
	{
		LittleComm cmdr = new LittleComm();

		[ConsoleCommand("test.littlecomm")]
		void conCmd(ConsoleSystem.Arg args)
		{
			cmdr.OnCommand("find", args.Args, () =>
			{
				Puts("works");
			});
		}

		class LittleComm
		{
			private string[] _args;
			private int _index;

			public void OnCommand(string command, string[] args, Action action)
			{
				if (args == null || args.Length == 0)
				{
					if (command == null)
						action.Invoke();
					return;
				}

				_index = 0;
				_args = args;

				if (command.Equals(args[0], StringComparison.CurrentCultureIgnoreCase))
					action.Invoke();
			}

			public string Current() => _args[_index];
			public string Next() => _args[++_index];
			public string Prev() => _args[--_index];

			public bool NextExists(int num = 1) => _args.Length >= (_index + num + 1);
		}
	}
}
