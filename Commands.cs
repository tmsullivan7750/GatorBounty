using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cpp2IL.Core.Extensions;
using ProjectM;
using ProjectM.Network;
using VampireCommandFramework;
using System;
using Unity.Collections;
using UnityEngine.Jobs;
using Bloody.Core.GameData.v1;
using Bloody.Core.Models.v1;
using Unity.Entities;
using static Il2CppMono.Security.X509.X520;

namespace GatorBounty;

public class Commands
{
	[Command("gatorbounty list", shortHand: "gb list")]
	public void bountyListCommand(ChatCommandContext ctx)
	{
		int num = 10;
		int offset = 5;
		var killstreakBounties = DataStore.PlayerDatas.Values.Where(k => k.CurrentStreak >= 7).OrderByDescending(k => k.CurrentStreak).ToArray();
		var playerBounties = DataStore.PlayerBounties.Values.OrderByDescending(b => b.bountyPrice).ToArray();

		List<BountyList> ksbountyArray = new List<BountyList>();
		List<BountyList> pbountyArray = new List<BountyList>();
		for (int i = 0; i < killstreakBounties.Length; i++)
		{
			ksbountyArray.Add(new BountyList(killstreakBounties[i].SteamId, killstreakBounties[i].LastName, killstreakBounties[i].CurrentStreak, 0));
		}
		for (int i = 0; i < playerBounties.Length; i++)
		{
			pbountyArray.Add(new BountyList(playerBounties[i].SteamId, playerBounties[i].LastName, 0, playerBounties[i].bountyPrice));
		}


		var bountyListFinal = ksbountyArray
		.Concat(pbountyArray)
		.GroupBy(p => p.SteamId)
		.Select(g => new BountyList(
			g.Key,
			g.First().LastName,
			g.Sum(p => p.CurrentStreak),
			g.Sum(p => p.BountyPrice)))
		.OrderByDescending(g => (g.CurrentStreak * Int32.Parse(Settings.bountyKillValue)) + g.BountyPrice)
		.ToArray();




		offset = offset > bountyListFinal.Length ? bountyListFinal.Length : offset;
		num = num > bountyListFinal.Length ? bountyListFinal.Length : num;

		var sbTitlee = new StringBuilder();
		sbTitlee.AppendLine($"{Markup.Prefix} <size=18><color=blue><u>Bounty List</u></color></size>");

		var message = (BountyList k) => $"{Markup.Highlight(k.LastName)} \t<color={Markup.SecondaryColor}> Price: {(k.CurrentStreak * 50) + k.BountyPrice}</color>";

        for (var i = 0; i < bountyListFinal.Length; i += 5)
		{
			var sb = new StringBuilder();
			sb.AppendLine(" ");
            // Process the current chunk of 5 items
            for (var j = i; j < i + 5 && j < bountyListFinal.Length; j++)
            {
                var k = bountyListFinal[j];
                sb.AppendLine($"{j + 1}. {message(k)}");
            }

            // Send the current StringBuilder's content as a message
            ctx.Reply(sb.ToString());
        }
	}

	[Command("gatorbounty add", shortHand: "gb add")]
	public static void bountyAddCommand(ChatCommandContext ctx, string playerName, int amount)
	{
        UserModel playerModel = GameData.Users.GetUserByCharacterName(playerName);
        UserModel userModel = GameData.Users.GetUserByCharacterName(ctx.User.CharacterName.Value);
		if (playerModel == null)
		{
			Plugin.Logger.LogDebug("Player was not found");
			ctx.Reply($"Player does not exist.");
		}
		else if (amount < Int32.Parse(Settings.bountyAnnounceValueReward))
		{
			ctx.Reply($"Bounty value must be atleast {Settings.bountyAnnounceValueReward}");
		}
		else
		{
			int temp = DataStore.handleBountyAddition(userModel, playerModel, amount);
			if (temp == -1)
			{
				ctx.Reply($"You don't have enough {Settings.bountyCurrencyName}!");
			}
		}
		int bounty = DataStore.getBounty(playerModel.PlatformId);

        if (bounty >= Int32.Parse(Settings.bountyMapValueReward))
		{
			VampireDownedHook.AddIcon(playerModel.Entity);
		}

    }

    [Command("gatorbounty remove", shortHand: "gb remove", adminOnly: true)]
    public static void bountyRemoveIcons(ChatCommandContext ctx)
    {
		VampireDownedHook.RemoveAllIcons();
    }
}

public class BountyList
{
	public ulong SteamId;
	public string LastName;
	public int CurrentStreak;
	public int BountyPrice;

	public BountyList(ulong SteamId, string LastName, int CurrentStreak, int bountyPrice)
	{
		this.SteamId = SteamId;
		this.LastName = LastName;
		this.CurrentStreak = CurrentStreak;
		this.BountyPrice = bountyPrice;
	}

	public int getTotalBounty()
	{
		var a = this.CurrentStreak * Int32.Parse(Settings.bountyKillValue);
		return a + this.BountyPrice;
	}
}
