using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Backtrace.Unity.Common;
using Bloodstone.API;
using Il2CppSystem.Linq;
using Il2CppInterop.Runtime;
using ProjectM;
using ProjectM.Network;
using ProjectM.Scripting;
using ProjectM.Shared;
using Steamworks;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.Jobs;
using Random = System.Random;
using Bloody.Core.GameData.v1;
using Bloody.Core.Models.v1;
using ProjectM.UI;
using System.Linq;

namespace GatorBounty;
public class DataStore
{
	public static int lostStreakBounty;

    public record struct PlayerStatistics(ulong SteamId, string LastName, int Kills, int Deaths, int CurrentStreak,
		int HighestStreak, string LastClanName, int CurrentLevel, int MaxLevel)
	{
		// lol yikes
		private static string SafeCSVName(string s) => s.Replace(",", "");

		public string ToCsv() => $"{SteamId},{SafeCSVName(LastName)},{Kills},{Deaths},{CurrentStreak},{HighestStreak},{SafeCSVName(LastClanName)},{CurrentLevel},{MaxLevel}";

		public static PlayerStatistics Parse(string csv)
		{
			// intentionally naieve and going to blow up so I'll catch it and not get an object for that player and log it
			var split = csv.Split(',');
			return new PlayerStatistics()
			{
				SteamId = ulong.Parse(split[0]),
				LastName = split[1],
				Kills = int.Parse(split[2]),
				Deaths = int.Parse(split[3]),
				CurrentStreak = int.Parse(split[4]),
				HighestStreak = int.Parse(split[5]),
				LastClanName = split.Length > 6 ? split[6] : "",
				CurrentLevel = split.Length > 7 ? int.Parse(split[7]) : -1,
				MaxLevel = split.Length > 8 ? int.Parse(split[8]) : -1
			};
		}

		public string FormattedName
		{
			get
			{
				var name = Markup.Highlight(LastName);
				return $"{name}";
			}
		}
	}

	public record struct PlayerBounty(ulong SteamId, string LastName, int bountyPrice)
	{
		private static string SafeCSVNameBounty(string s) => s.Replace(",", "");

		public string ToCsv() => $"{SteamId},{SafeCSVNameBounty(LastName)},{bountyPrice}";

		public static PlayerBounty Parse(string csv)
		{
			// intentionally naieve and going to blow up so I'll catch it and not get an object for that player and log it
			var split = csv.Split(',');
			return new PlayerBounty()
			{
				SteamId = ulong.Parse(split[0]),
				LastName = split[1],
				bountyPrice = int.Parse(split[2])
			};
		}

		public string FormattedNameBounty
		{
			get
			{
				var name = Markup.Highlight(LastName);
				return $"{name}";
			}
		}
	}

	public static Dictionary<ulong, PlayerBounty> PlayerBounties = new();
	public static Dictionary<ulong, PlayerStatistics> PlayerDatas = new();


	private const string STATS_FILE_NAME = "stats.v1.csv";
	private const string STATS_FILE_PATH = $"BepInEx/config/Killfeed/{STATS_FILE_NAME}";

	private const string BOUNTY_FILE_NAME = "bounty.v1.csv";
	private const string BOUNTY_FILE_PATH = $"BepInEx/config/GatorBounty/{BOUNTY_FILE_NAME}";

	public static void WriteToDisk()
	{
        var dir = Path.GetDirectoryName(BOUNTY_FILE_PATH);
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        using StreamWriter bountyFile = new StreamWriter(BOUNTY_FILE_PATH, append: false);
		foreach (var bountyData in PlayerBounties.Values)
		{
			bountyFile.WriteLine(bountyData.ToCsv());
		}
	}

	public static void LoadFromDisk()
	{
		LoadPlayerBountyData();
        LoadPlayerData();

    }

	private static void LoadPlayerBountyData()
	{
		if (!File.Exists(BOUNTY_FILE_PATH)) return;
		using StreamReader bountyFile = new StreamReader(BOUNTY_FILE_PATH);
		while (!bountyFile.EndOfStream)
		{
			var line = bountyFile.ReadLine();
			if (string.IsNullOrWhiteSpace(line)) continue;
			try
			{
				var bountyData = PlayerBounty.Parse(line);
				if (PlayerBounties.TryGetValue(bountyData.SteamId, out PlayerBounty data))
				{
					Plugin.Logger.LogWarning($"Duplicate player bounty data found, overwriting {data} with {bountyData}");
				}
				PlayerBounties[bountyData.SteamId] = bountyData;
			}
			catch (Exception)
			{
				Plugin.Logger.LogError($"Failed to parse player bounty line: \"{line}\"");
			}
		}
	}

    private static void LoadPlayerData()
    {
        if (!File.Exists(STATS_FILE_PATH)) return;
        using StreamReader statsFile = new StreamReader(STATS_FILE_PATH);
        while (!statsFile.EndOfStream)
        {
            var line = statsFile.ReadLine();
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                var playerData = PlayerStatistics.Parse(line);
                PlayerDatas[playerData.SteamId] = playerData;
            }
            catch (Exception)
            {
                Plugin.Logger.LogError($"Failed to parse player line: \"{line}\"");
            }
        }
    }

    public static int handleBountyAddition(UserModel user, UserModel player, int bountyPrice)
	{
		var playerEntity = player.Entity.Read<User>();
		searchTotalPrefabsInInventory(user, new PrefabGUID(Int32.Parse(Settings.bountyCurrency)), out int userTotalItemsCheck);
		Plugin.Logger.LogError($"{userTotalItemsCheck}");
        if (userTotalItemsCheck >= bountyPrice)
		{
			getPrefabFromInventory(user, new PrefabGUID(Int32.Parse(Settings.bountyCurrency)), bountyPrice);
            PlayerBounty UpsertName(ulong steamID, string name, int bountyPrice)
            {
                if (PlayerBounties.TryGetValue(steamID, out PlayerBounty bounty))
                {
                    bounty.SteamId = steamID;
                    bounty.LastName = name;
                    bounty.bountyPrice += bountyPrice;
                    PlayerBounties[steamID] = bounty;
                }
                else
                {
                    PlayerBounties[steamID] = new PlayerBounty() { SteamId = steamID, LastName = name, bountyPrice = bountyPrice };
                }
                return PlayerBounties[steamID];
            }
            var bountyData = UpsertName(playerEntity.PlatformId, playerEntity.CharacterName.ToString(), bountyPrice);
            AnnounceBounty(bountyData);
            WriteToDisk();
            return 0;

        } else
		{
			return -1;

        }
	}

    public static void DeleteLineFromBountyFile(ulong steamId)
    {
        if (!File.Exists(BOUNTY_FILE_PATH))
        {
            Plugin.Logger.LogError($"Bounty file not found: {BOUNTY_FILE_PATH}");
            return;
        }

        // Read all lines from the file
        var lines = File.ReadAllLines(BOUNTY_FILE_PATH).ToList();

        // Find the line to delete
        var lineToRemove = lines.FirstOrDefault(line =>
        {
            var split = line.Split(',');
            return split.Length > 0 && ulong.TryParse(split[0], out var id) && id == steamId;
        });

        // If a line was found, remove it
        if (lineToRemove != null)
        {
            lines.Remove(lineToRemove);
            Plugin.Logger.LogInfo($"Removed line: {lineToRemove}");

            // Write the remaining lines back to the file
            File.WriteAllLines(BOUNTY_FILE_PATH, lines);
            Plugin.Logger.LogInfo($"Updated file written to: {BOUNTY_FILE_PATH}");
        }
        else
        {
            Plugin.Logger.LogWarning($"No line found with SteamId: {steamId}");
        }
    }

    public static void handleBountyDeath(ProjectM.Network.User player)
    {
		PlayerDatas.TryGetValue(player.PlatformId, out PlayerStatistics temp);
		lostStreakBounty = temp.CurrentStreak;
		DeleteLineFromBountyFile(player.PlatformId);
        PlayerBounties.Remove(player.PlatformId);
        LoadFromDisk();
    }

    private static void AnnounceBounty(PlayerBounty bounty)
    {
		if (!Settings.AnnounceBounty) return;

		var userName = bounty.FormattedNameBounty;

		var message = $"Someone has put a bounty of <color=red>{bounty.bountyPrice} {Settings.bountyCurrencyName}</color> on <color=white>{userName}'s</color> head!";
		ServerChatUtils.SendSystemMessageToAllClients(VWorld.Server.EntityManager, Markup.Prefix + message);
	}

	public static int getStreak(ulong steamId)
	{
		if (PlayerDatas.TryGetValue(steamId, out var player))
		{
			return player.CurrentStreak;
		}
		else
		{
			return 0;
		}
	}

	public static int getBounty(ulong steamId)
	{
		if (PlayerBounties.TryGetValue(steamId, out var player))
		{
			return player.bountyPrice;
		}
		else
		{
			return 0;
		}
	}

    public static bool searchTotalPrefabsInInventory(UserModel player, PrefabGUID prefabCurrencyGUID, out int total)
    {
        total = 0;
        try
        {
            var characterEntity = player.Character.Entity;
            total = InventoryUtilities.GetItemAmount(Core.EntityManager, characterEntity, prefabCurrencyGUID);
            if (total >= 0)
            {
                return true;
            }
            else
            {
                return false;
            }

        }
        catch (Exception error)
        {
            Plugin.Logger.LogError($"Error: {error.Message}");
            return false;
        }
    }

    public static bool getPrefabFromInventory(UserModel player, PrefabGUID prefabGUID, int quantity)
    {

        try
        {

            var prefabGameData = GameData.Items.GetPrefabById(prefabGUID);
            var userEntity = player.Character.Entity;

            if (InventoryUtilitiesServer.TryRemoveItem(Core.EntityManager, userEntity, prefabGameData.PrefabGUID, quantity))
            {
                return true;
            }
            else
            {
                return false;
            }

        }
        catch (Exception error)
        {
            Plugin.Logger.LogError($"Error {error.Message}");
            return false;
        }
    }
}
