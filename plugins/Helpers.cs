using System;

using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
	class Helpers : RustPlugin
	{
		void ConfigItem(string key, object defaultValue)
		{
			Config[key] = Config[key] ?? defaultValue;
		}

		void ConfigItem(string key1, string key2, object defaultValue)
		{
			Config[key1, key2] = Config[key1, key2] ?? defaultValue;
		}

		void ConfigItem(string key1, string key2, string key3, object defaultValue)
		{
			Config[key1, key2, key3] = Config[key1, key2, key3] ?? defaultValue;
		}

		bool IsPluginExists(string name)
		{
			return Interface.Oxide.GetLibrary<Core.Libraries.Plugins>().Exists(name);
		}

		bool StringContains(string source, string value, StringComparison comparison)
		{
			return source.IndexOf(value, comparison) >= 0;
		}

		string Lang(string key, BasePlayer player = null)
		{
			return lang.GetMessage(key, this, player?.UserIDString);
		}

		bool PlayerHasPermission(BasePlayer player, string permissionName)
		{
			return player.IsAdmin || permission.UserHasPermission(player.UserIDString, permissionName);
		}

		bool IsDigitsOnly(string str)
		{
			foreach (char c in str)
				if (!char.IsDigit(c))
					return false;
			return true;
		}

		string GenerateId()
		{
			byte[] bytes = Guid.NewGuid().ToByteArray();
			int number = Math.Abs(BitConverter.ToInt32(bytes, 0));
			return number.ToString();
		}

		static void DrawDebugLine(BasePlayer player, Vector3 from, Vector3 to, float duration = 1f)
		{
			player.SendConsoleCommand("ddraw.line", duration, Color.yellow, from, to);
		}

		static void DrawDebugSphere(BasePlayer player, Vector3 position, float radius = 0.5f, float duration = 1f)
		{
			player.SendConsoleCommand("ddraw.sphere", duration, Color.green, position, radius);
		}
	}
}
