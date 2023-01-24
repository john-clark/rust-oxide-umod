//Reference: Oxide.Core.MySql
using System;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
	[Info("VorEconomy", "Braanflakes", version, ResourceId = 0)]
	[Description("Universal Economy plugin using a custom user-defined currency.")]

	class VorEconomy : CovalencePlugin
	{
		#region Vars
		private readonly Core.MySql.Libraries.MySql _mySql = Interface.Oxide.GetLibrary<Core.MySql.Libraries.MySql>();
		private Core.Database.Connection _mySqlConnection;
		private string InsertPlayerData = "";
		private string SelectSteam64 = "";
		private string SelectAmount = "";
		private string UpdateAmount = "";
		private const string version = "1.1.0";
		#endregion

		#region Data File
		// create data file
		class StoredData
		{
			public Dictionary<string, string> PlayerAmounts = new Dictionary<string, string>();

			public StoredData()
			{
			}
		}

		StoredData storedData;
		#endregion

		#region Config File
		protected override void LoadDefaultConfig()
		{
			PrintWarning("Creating a new configuration file");
			Config.Clear();
			Config["UpdateDataTimer"] = 300f;
			Config["DatabaseHostName"] = "HostName";
			Config["DatabasePort"] = 3306;
			Config["DatabaseName"] = "Name";
			Config["DatabaseTableName"] = "Name.TableName";
			Config["DatabaseUsername"] = "root";
			Config["DatabasePassword"] = "P@ssw0rd!";
			Config["CurrencyName"] = "VorCash";
			SaveConfig();
		}
		#endregion
		
		#region Server Hooks
		void Init()
		{
			// load data
			storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>("VorEconomy");

			// register messages with lang file
			lang.RegisterMessages(Messages, this);

			// assign SQL statements
			InsertPlayerData = "INSERT INTO " + Config["DatabaseTableName"] + " (`steam64`, `amount`) VALUES (@0, @1);";
			SelectSteam64 = "SELECT steam64 FROM " + Config["DatabaseTableName"] + " WHERE steam64=@0;";
			SelectAmount = "SELECT amount FROM " + Config["DatabaseTableName"] + " WHERE steam64=@0;";
			UpdateAmount = "UPDATE " + Config["DatabaseTableName"] + " SET amount=@0 WHERE steam64=@1;";
		}

		void Unload()
		{
			// close the database connection on unload
			_mySql.CloseDb(_mySqlConnection);
		}

		private void OnServerInitialized()
		{
			// Open a connection with the database
			_mySqlConnection = _mySql.OpenDb(Config["DatabaseHostName"].ToString(), int.Parse(Config["DatabasePort"].ToString()), "onrust1_economy", Config["DatabaseUsername"].ToString(), Config["DatabasePassword"].ToString(), this);

			// Queries the database every X seconds and updates the local data file accordingly
			timer.Repeat(float.Parse(Config["UpdateDataTimer"].ToString()), 0, () =>
			{
				UpdateDataFile();
			});
		}
		#endregion

		#region Player Hooks
		private void OnUserConnected(IPlayer player)
		{
			string PlayerId = player.Id;
			string Name = player.Name;
			string amount = "0";

			var sql = Core.Database.Sql.Builder.Append(SelectSteam64, PlayerId);

			_mySql.Query(sql, _mySqlConnection, list =>
				{
					// if the database already contains the user
					if (list.Count > 0)
					{
						// if the local data file doesn't contain the user
						if (!storedData.PlayerAmounts.ContainsKey(PlayerId))
						{
							Puts(string.Format("User {0} ({1}) exists in the database but not in the local data file.", Name, PlayerId));

							// get the players currency from the database
							sql = Core.Database.Sql.Builder.Append(SelectAmount, PlayerId);

							_mySql.Query(sql, _mySqlConnection, list1 =>
								{
									amount = list1[0]["amount"].ToString();
								});

							// add the player to the local data file
							timer.Once(2f, () =>
							{
								storedData.PlayerAmounts.Add(PlayerId, amount);
								Interface.Oxide.DataFileSystem.WriteObject("VorEconomy", storedData);

								Puts(string.Format("User {0} ({1}) was added to the local data file with the amount of: {2}", Name, PlayerId, amount));
							});							
						}
						return;
					}

					// if the database doesn't contain the user, add them with a currency value of 0
					storedData.PlayerAmounts.Add(PlayerId, "0");
					Interface.Oxide.DataFileSystem.WriteObject("VorEconomy", storedData);

					sql = Core.Database.Sql.Builder.Append(InsertPlayerData, PlayerId, 0);
					_mySql.Insert(sql, _mySqlConnection);
					Puts(string.Format("User did not exist in the database. Successfully added {1} ({2}) with an amount of: 0", Name, PlayerId));
				});
		}
		#endregion

		#region Helper Functions

		#region UpdateDataFile()
		// update data file from database values
		private void UpdateDataFile()
		{
			// loop through data file, for each steam64, select that steam64 from database and update amount
			foreach(KeyValuePair<string, string> entry in storedData.PlayerAmounts)
			{
				var sql = Core.Database.Sql.Builder.Append(SelectAmount, entry.Key.ToString());

				_mySql.Query(sql, _mySqlConnection, list =>
					{
						if (list.Count == 0)
						{
							PrintWarning("Warning: Database is empty. Verify integrity of your local data file and database table.");
						}
						if (list.Count > 0)
						{
							foreach (var ent in list)
							{
								storedData.PlayerAmounts[entry.Key] = ent["amount"].ToString();
								Interface.Oxide.DataFileSystem.WriteObject("VorEconomy", storedData);
							}
						}
					});
			}

			Puts("Database sucessfully queried and local data file is up to date.");
		}

		#endregion

		#region DoesPlayerExist()
		// Function to check if player exists in the local data file.
		private bool DoesPlayerExist(string PlayerId)
		{
			foreach(KeyValuePair<string, string> entry in storedData.PlayerAmounts)
			{
				if (PlayerId == entry.Key)
				{
					return true;
				}
			}

			return false;
		}
		#endregion

		#endregion

		#region Commands
		// Chat Command for VorEconomy
		[Command("economy")]
		private void EconomyCommand(IPlayer player, string command, string[] args)
		{
			#region Vars
			string PlayerId = player.Id;
			string Name = player.Name;
			string TargetId = "";
			string TargetName = "";
			IPlayer targetPlayer;
			#endregion

			#region Default Cases
			// default case for admin
			if (player.IsAdmin && args == null || player.IsAdmin && args.Length == 0)
			{
				player.Reply(MsgFormat("infoplayer", PlayerId, version));
				return;
			}

			// default case for player
			if (!player.IsAdmin && args == null || !player.IsAdmin && args.Length == 0)
			{
				player.Reply(MsgFormat("infoadmin", PlayerId, version));
				return;
			}
			#endregion

			#region AddCurrencyCommand
			// add currency
			if (!player.IsAdmin && args[0] == "add")
			{
				player.Reply(Msg("noaccesscommand", PlayerId));
				return;
			}

			if (player.IsAdmin && args[0] == "add")
			{
				int value;
				int amount = 0;

				// error checking
				if (args.Length < 3 || args.Length > 3)
				{
					player.Reply(Msg("syntaxadd", PlayerId));
					return;
				}

				// if 2 args, first being the target player's name and second being the amount
				if (args.Length == 3 && Int32.TryParse(args[2], out value))
				{
					targetPlayer = covalence.Players.FindPlayer(args[1]);

					if (targetPlayer != null && DoesPlayerExist(targetPlayer.Id))
					{
						TargetId = targetPlayer.Id;
						TargetName = targetPlayer.Name;

						// set amount
						amount = int.Parse(args[2]);

						// add amount to target's currency
						AddCurrency(player, TargetName, TargetId, amount);
					}
					else
					{
						player.Reply(Msg("playernotfound", PlayerId));
						return;
					}				
				}
			}
			#endregion

			#region SubtractCurrencyCommand
			// subtract currency
			if (!player.IsAdmin && args[0] == "sub")
			{
				player.Reply(Msg("noaccesscommand", PlayerId));
				return;
			}

			if (player.IsAdmin && args[0] == "sub")
			{
				int value;
				int amount = 0;

				// error checking
				if (args.Length < 3 || args.Length > 3)
				{
					player.Reply(Msg("syntaxsub", PlayerId));
					return;
				}

				// if 2 args, first being the target player's name and second being the amount
				if (args.Length == 3 && Int32.TryParse(args[2], out value))
				{
					targetPlayer = covalence.Players.FindPlayer(args[1]);

					if (targetPlayer != null && DoesPlayerExist(targetPlayer.Id))
					{
						TargetId = targetPlayer.Id;
						TargetName = targetPlayer.Name;

						// set amount
						amount = int.Parse(args[2]);

						// subtract amount from target player's currency
						SubtractCurrency(player, TargetName, TargetId, amount);
					}
					else
					{
						player.Reply(Msg("playernotfound", PlayerId));
						return;
					}
				}
			}

			#endregion

			#region SetCurrencyCommand
			// set currency
			if (!player.IsAdmin && args[0] == "set")
			{
				player.Reply(Msg("noaccesscommand", PlayerId));
				return;
			}

			if (player.IsAdmin && args[0] == "set")
			{
				int value;
				int amount = 0;

				// error checking
				if (args.Length < 3 || args.Length > 3)
				{
					player.Reply(Msg("syntaxset", PlayerId));
					return;
				}

				// if 2 args, first being the target tplayer's name and second being the amount
				if (args.Length == 3 && Int32.TryParse(args[2], out value))
				{
					targetPlayer = covalence.Players.FindPlayer(args[1]);

					if (targetPlayer != null && DoesPlayerExist(targetPlayer.Id))
					{
						TargetId = targetPlayer.Id;
						TargetName = targetPlayer.Name;

						// set amount
						amount = int.Parse(args[2]);

						// set currency command
						SetCurrency(player, TargetName, TargetId, amount);
					}
					else
					{
						player.Reply(Msg("playernotfound", PlayerId));
						return;
					}
				}
			}
			#endregion

			#region CheckAmountPlayerCommand
			// set currency
			if (!player.IsAdmin && args[0] == "checkamount")
			{
				// error checking
				if (args.Length > 1)
				{
					player.Reply(Msg("syntaxcheckamountplayer", PlayerId));
					return;
				}

				// check amount of player
				CheckAmount(player, Name, PlayerId);
			}
			#endregion

			#region CheckAmountAdminCommand
			if (player.IsAdmin && args[0] == "checkamount")
			{
				// error checking
				if (args.Length > 2)
				{
					player.Reply(Msg("syntaxcheckamountadmin", PlayerId));
					return;
				}

				// checking own amount
				if (args.Length == 1)
				{
					// Check amount of player
					CheckAmount(player, Name, PlayerId);
				}

				// checking another players' amount
				if (args.Length == 2)
				{
					targetPlayer = covalence.Players.FindPlayer(args[1]);

					if (targetPlayer != null && DoesPlayerExist(targetPlayer.Id))
					{
						TargetId = targetPlayer.Id;
						TargetName = targetPlayer.Name;

						// Check amount of target player
						CheckAmount(player, TargetName, TargetId);
					}
					else
					{
						player.Reply(Msg("playernotfound", PlayerId));
						return;
					}
				}
			}
			#endregion

			#region ForceUpdateCommand
			// force the local data file to update to the current database values
			if (!player.IsAdmin && args[0] == "forceupdate")
			{
				player.Reply(Msg("noaccesscommand", PlayerId));
				return;
			}

			if (player.IsAdmin && args[0] == "forceupdate")
			{
				if (args.Length > 1)
				{
					player.Reply(Msg("syntaxforceupdate", PlayerId));
					return;
				}

				UpdateDataFile();
				player.Reply(Msg("updatedatasuccess", PlayerId));
				return;
			}
			#endregion
		}
		#endregion

		#region API

		#region AddCurrency
		void AddCurrency(IPlayer player, string TargetName, string TargetId, int amount)
		{
			int originalamount = amount;
			int entamount = 0;

			// append SQL statement to SELECT amount from database
			var sql = Core.Database.Sql.Builder.Append(SelectAmount, TargetId);

			// query the database and add amount to player
			_mySql.Query(sql, _mySqlConnection, list =>
			{
				if (list.Count > 0)
				{
					foreach (var ent in list)
					{
						entamount = int.Parse(ent["amount"].ToString());
						amount = amount + entamount;
						sql = Core.Database.Sql.Builder.Append(UpdateAmount, amount, TargetId);
						_mySql.Update(sql, _mySqlConnection);

						// update local data file to reflect changes made on database
						timer.Once(2f, () =>
						{
							UpdateDataFile();

							// display output to console and chat for completion of this command
							player.Reply(MsgFormat("successadd", player.Id, originalamount, Config["CurrencyName"], TargetName, TargetId));
							Puts(string.Format("User {0} ({1}) has successfully added {2} {3} to {4} ({5}).", player.Name, player.Id, originalamount, Config["CurrencyName"], TargetName, TargetId));
						});
					}
				}
			});
		}
		#endregion

		#region SubtractCurrency
		void SubtractCurrency(IPlayer player, string TargetName, string TargetId, int amount)
		{
			int originalamount = amount;
			int entamount = 0;

			// append SQL statement to SELECT amount from database
			var sql = Core.Database.Sql.Builder.Append(SelectAmount, TargetId);

			// query the database and add amount to player
			_mySql.Query(sql, _mySqlConnection, list =>
			{
				if (list.Count > 0)
				{
					foreach (var ent in list)
					{
						entamount = int.Parse(ent["amount"].ToString());
						amount = entamount - amount;
						if (amount < 0)
						{
							player.Reply(Msg("errorsub", player.Id));
							return;
						}

						sql = Core.Database.Sql.Builder.Append(UpdateAmount, amount, TargetId);
						_mySql.Update(sql, _mySqlConnection);

						// update local data file to reflect changes made on database
						timer.Once(2f, () =>
						{
							UpdateDataFile();

							// display output to console and chat for completion of this command
							player.Reply(MsgFormat("successsub", player.Id, originalamount, Config["CurrencyName"], TargetName, TargetId));
							Puts(string.Format("User {0} ({1}) has successfully subtracted {2} {3} from {4} ({5}).", player.Name, player.Id, originalamount, Config["CurrencyName"], TargetName, TargetId));
						});
					}
				}
			});
		}
		#endregion

		#region SetCurrency
		void SetCurrency(IPlayer player, string TargetName, string TargetId, int amount)
		{
			int originalamount = amount;

			// append SQL statement to SELECT amount from database
			var sql = Core.Database.Sql.Builder.Append(SelectAmount, TargetId);

			// query the database and add amount to player
			_mySql.Query(sql, _mySqlConnection, list =>
			{
				if (list.Count > 0)
				{
					foreach (var ent in list)
					{
						if (amount < 0)
						{
							player.Reply(Msg("errorset", player.Id));
							return;
						}
						sql = Core.Database.Sql.Builder.Append(UpdateAmount, amount, TargetId);
						_mySql.Update(sql, _mySqlConnection);
					}
				}

				// update local data file to reflect changes made on database
				timer.Once(2f, () =>
				{
					UpdateDataFile();
				});

				// display output to console and chat for completion of this command
				player.Reply(MsgFormat("successset", player.Id, Config["CurrencyName"], TargetName, TargetId, originalamount));
				Puts(string.Format("User {0} ({1}) has successfully set {2} of {3} ({4}) to {5}.", player.Name, player.Id, Config["CurrencyName"], TargetName, TargetId, originalamount));
			});
		}
		#endregion

		#region CheckAmount
		void CheckAmount(IPlayer player, string TargetName, string TargetId)
		{
			int count = 0;

			// find player's id in data file and output amount
			foreach (KeyValuePair<string, string> entry in storedData.PlayerAmounts)
			{
				if (entry.Key == TargetId)
				{
					player.Reply(MsgFormat("checkamountadmin", player.Id, Config["CurrencyName"], TargetName, TargetId, entry.Value));
					return;
				}

				count++;
				// if player is not found in data file
				if (count == storedData.PlayerAmounts.Count)
				{
					player.Reply(Msg("playernotfound", player.Id));
					return;
				}
			}
		}
		#endregion

		#endregion
		
		#region Localization
		string Msg(string key, string playerid = null) => lang.GetMessage(key, this, playerid);
		string MsgFormat(string key, string playerid = null, params object[] args) => string.Format(lang.GetMessage(key, this, playerid), args);
		
		Dictionary<string, string> Messages = new Dictionary<string, string>
		{
			{"infoplayer", "<size=20>VorEconomy</size> {0} by <color=#95f442>Braanflakes</color>\nSyntax: /economy add | sub | set | checkamount | forceupdate"},
			{"infoadmin", "<size=20>VorEconomy</size> {0} by <color=#95f442>Braanflakes</color>\nSyntax: /economy checkamount"},
			{"noaccesscommand", "You do not have access to that command."},
			{"playernotfound", "Could not find player."},
			{"syntaxadd", "Incorrect syntax. Try /economy add \"PlayerName\" <amount>"},
			{"syntaxsub", "Incorrect syntax. Try /economy sub \"PlayerName\" <amount>"},
			{"syntaxset", "Incorrect syntax. Try /economy set \"PlayerName\" <amount>"},
			{"syntaxcheckamountplayer", "Incorrect syntax. Try /economy checkamount"},
			{"syntaxcheckamountadmin", "Incorrect syntax. Try /economy checkamount or /economy checkamount \"PlayerName\""},
			{"syntaxforceupdate", "Incorrect syntax. Try /economy forceupdate"},
			{"updatedatasuccess", "Local data file successfully updated to match database."},
			{"errorsub", "You can not subtract more than the player's total."},
			{"errorset", "Can't set player's amount less than 0."},
			{"successadd", "You have successfully added {0} {1} to {2} ({3})."},
			{"successsub", "You have successfully subtracted {0} {1} from {2} ({3})."},
			{"successset", "You have successfully set {0} of {1} ({2}) to {3}."},
			{"checkamountplayer", "Your current amount of {0} is: {1}"},
			{"checkamountadmin", "The current amount of {0} for {1} ({2}) is: {3}"}
		};
		#endregion
	}
}