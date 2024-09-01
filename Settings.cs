using BepInEx.Configuration;

namespace GatorBounty;

internal class Settings
{
	internal static bool AnnounceBounty { get; private set; }
	internal static string bountyCurrency { get; private set; }
	internal static string bountyCurrencyName { get; private set; }
	internal static string bountyAnnounceValueKill { get; private set; }
	internal static string bountyAnnounceValueReward { get; private set; }
	internal static string bountyMapValueKill { get; private set; }
	internal static string bountyMapValueReward { get; private set; }
	internal static string bountyKillValue { get; private set; }

	internal static void Initialize(ConfigFile config)
	{
		AnnounceBounty = config.Bind("General", "AnnounceBounty", true, "Announce bounties in chat").Value;
		bountyCurrency = config.Bind("General", "bountyCurrency", "576389135", "Prefab GUID for the currency for bounty system").Value;
		bountyCurrencyName = config.Bind("General", "bountyCurrencyName", "Greater Stygian Shards", "Name of currency for bounty system").Value;
		bountyKillValue = config.Bind("General", "bountyKillValue", "50", "How much each kill is worth towards bounty").Value;
		bountyAnnounceValueKill = config.Bind("General", "bountyAnnounceValueKill", "7", "Killstreak size for bounty to start").Value;
		bountyMapValueKill = config.Bind("General", "bountyMapValueKill", "12", "Killstreak size for bounty map icon to start").Value;
		bountyAnnounceValueReward = config.Bind("General", "bountyAnnounceValueReward", "500", "Bounty price amount for bounty to start").Value;
		bountyMapValueReward = config.Bind("General", "bountyMapValueReward", "1000", "Bounty price amount for bounty map icon to start").Value;
	}
}
