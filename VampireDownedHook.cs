using System;
using Bloodstone.API;
using HarmonyLib;
using ProjectM;
using Steamworks;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.IO;
using Il2CppSystem.Linq;
using ProjectM.Network;
using Unity.Mathematics;
using UnityEngine.Jobs;
using static GatorBounty.DataStore;
using static RootMotion.FinalIK.Grounding;
using static VCF.Core.Basics.RoleCommands;
using Stunlock.Core;
using Bloody.Core.API.v1;
using Bloody.Core.Helper.v1;
using Bloody.Core.GameData.v1;
using Bloody.Core.Patch.Server;
using Bloody.Core.Methods;
using Bloody.Core.Models.v1;
using Bloody.Core;
using UnityEngine.TextCore.Text;
using Bloody.Core.Utils.v1;
using UnityEngine.UIElements;

namespace GatorBounty;

[HarmonyPatch(typeof(VampireDownedServerEventSystem), nameof(VampireDownedServerEventSystem.OnUpdate))]
public class VampireDownedHook
{
	private Entity icontEntity;
	private static Entity mapIconProxyPrefab;
	public static PrefabGUID mapIconPrefab = new PrefabGUID(1501929529);
	public static EntityQuery mapIconProxyQuery;

		public static void Prefix(VampireDownedServerEventSystem __instance)
	{
		var downedEvents = __instance.__query_1174204813_0.ToEntityArray(Allocator.Temp);
		foreach (var entity in downedEvents)
		{
			ProcessVampireDowned(entity);
		}
	}

	private static void ProcessVampireDowned(Entity entity)
	{
		if (!VampireDownedServerEventSystem.TryFindRootOwner(entity, 1, VWorld.Server.EntityManager, out var victimEntity))
		{
			Plugin.Logger.LogMessage("Couldn't get victim entity");
			return;
		}

		var downBuff = entity.Read<VampireDownedBuff>();


		if (!VampireDownedServerEventSystem.TryFindRootOwner(downBuff.Source, 1, VWorld.Server.EntityManager, out var killerEntity))
		{
			Plugin.Logger.LogMessage("Couldn't get victim entity");
			return;
		}

		var victim = victimEntity.Read<PlayerCharacter>();

		Plugin.Logger.LogMessage($"{victim.Name} is victim");
		var unitKiller = killerEntity.Has<UnitLevel>();

		if (unitKiller)
		{
			Plugin.Logger.LogInfo($"{victim.Name} was killed by a unit. [Not currently tracked]");
			return;
		}

		var playerKiller = killerEntity.Has<PlayerCharacter>();

		if (!playerKiller)
		{
			Plugin.Logger.LogWarning($"Killer could not be identified for {victim.Name}, if you know how to reproduce this please contact deca on discord or report on github");
			return;
		}

		var killer = killerEntity.Read<PlayerCharacter>();

		if (killer.UserEntity == victim.UserEntity)
		{
			Plugin.Logger.LogInfo($"{victim.Name} killed themselves. [Not currently tracked]");
			return;
		}

		var killerChar = killerEntity.Read<PlayerCharacter>();
		var killerUser = killerChar.UserEntity.Read<ProjectM.Network.User>();
		var victimChar = victimEntity.Read<PlayerCharacter>();
		var victimUser = victimChar.UserEntity.Read<ProjectM.Network.User>();

		//If 10+ create bounty
		int killStreak = DataStore.getStreak(killerUser.PlatformId);
		int lostStreak = DataStore.getStreak(victimUser.PlatformId);
		Plugin.Logger.LogInfo($"LostStreak: {lostStreak}");
        int bountyCheck = DataStore.getBounty(victimUser.PlatformId);
		int bountyPrice = lostStreak * Int32.Parse(Settings.bountyKillValue);
		int bountyPriceDisplay = killStreak * Int32.Parse(Settings.bountyKillValue);
		bountyPrice += bountyCheck;
		bountyPriceDisplay += bountyCheck;

		DataStore.LoadFromDisk();

        //Announce Bounty
        if (DataStore.getStreak(killerUser.PlatformId) == Int32.Parse(Settings.bountyAnnounceValueKill))
		{
			ServerChatUtils.SendSystemMessageToAllClients(VWorld.Server.EntityManager, $"<color=red>{killer.Name}</color> now has a killstreak bounty on their head! Current bounty price: <color=white>{bountyPriceDisplay} {Settings.bountyCurrencyName}</color>");
		}

		//Announce Bounty Map
        if (DataStore.getStreak(killerUser.PlatformId) == Int32.Parse(Settings.bountyMapValueKill))
        {
            ServerChatUtils.SendSystemMessageToAllClients(VWorld.Server.EntityManager, $"<color=red>{killer.Name}</color> is now marked on the map. Happy hunting!");
            AddIcon(killerEntity);
        }

        //If death of bounty
        if (lostStreak >= Int32.Parse(Settings.bountyAnnounceValueKill) || bountyPrice >= Int32.Parse(Settings.bountyAnnounceValueReward))
		{
			ServerChatUtils.SendSystemMessageToAllClients(VWorld.Server.EntityManager, $"<color=green>{killer.Name}</color> has claimed the <color=white>{bountyPrice} {Settings.bountyCurrencyName}</color> bounty on <color=red>{victim.Name}</color>'s head!");
            UserModel player = GameData.Users.GetUserByCharacterName(killer.Name.ToString());
			var itemGuid = new PrefabGUID(Int32.Parse(Settings.bountyCurrency));

			player.DropItemNearby(itemGuid, bountyPrice);
			DataStore.handleBountyDeath(victimUser);
		}
        RemoveIcon(victimEntity);
    }

    public static void AddIcon(Entity player)
    {
        CleanIcons(player);

        if (!Core.PrefabCollection._PrefabGuidToEntityMap.TryGetValue(mapIconPrefab, out mapIconProxyPrefab))
            Plugin.Logger.LogError("Failed to find MapIcon_ProxyObject_POI_Unknown Prefab entity");

        var mapIconProxy = Core.EntityManager.Instantiate(mapIconProxyPrefab);
        mapIconProxy.Add<MapIconTargetEntity>();
        mapIconProxy.Add<AttachMapIconsToEntity>();
        var mapIconTargetEntity = new MapIconTargetEntity();
        mapIconTargetEntity.TargetEntity = player.Read<Entity>();
        mapIconTargetEntity.TargetNetworkId = player.Read<NetworkId>();
        mapIconProxy.Write(mapIconTargetEntity);

        mapIconProxy.Remove<SyncToUserBitMask>();
        mapIconProxy.Remove<SyncToUserBuffer>();
        mapIconProxy.Remove<OnlySyncToUsersTag>();

        var attachMapIconsToEntity = Core.EntityManager.GetBuffer<AttachMapIconsToEntity>(mapIconProxy);
        Plugin.Logger.LogInfo($"AttachMITE: {attachMapIconsToEntity}");
        attachMapIconsToEntity.Clear();
        attachMapIconsToEntity.Add(new() { Prefab = mapIconPrefab });
    }
    public static bool RemoveIcon(Entity player)
    {
        Plugin.Logger.LogInfo($"REMOVING ICONS");
        if (!Core.PrefabCollection._PrefabGuidToEntityMap.TryGetValue(mapIconPrefab, out mapIconProxyPrefab))
            Plugin.Logger.LogError("Failed to find MapIcon_ProxyObject_POI_Unknown Prefab entity");

        mapIconProxyQuery = Core.EntityManager.CreateEntityQuery(new EntityQueryDesc()
        {
            All = new ComponentType[] {
                ComponentType.ReadOnly<AttachMapIconsToEntity>()
            },
            Options = EntityQueryOptions.IncludeDisabled
        });

        var userModel = GameData.Users.All.FirstOrDefault();
        var user = userModel.Entity;

        var mapIconProxies = mapIconProxyQuery.ToEntityArray(Allocator.Temp);
        foreach ( var mapIconProxy in mapIconProxies)
        {
            if (mapIconProxy.Read<PrefabGUID>().Equals(mapIconPrefab))
            {
                Plugin.Logger.LogInfo($"NetworkID: {mapIconProxy.Read<MapIconTargetEntity>().TargetNetworkId}");
                Plugin.Logger.LogInfo($"Players: {player.Read<NetworkId>()}");
                Plugin.Logger.LogInfo($"NetworkID: {mapIconProxy.Read<MapIconTargetEntity>().TargetNetworkId == player.Read<NetworkId>()}");
                Plugin.Logger.LogInfo($"TargetEntity: {mapIconProxy.Read<MapIconTargetEntity>().TargetEntity._Entity}");
                Plugin.Logger.LogInfo($"Players: {player.Read<Entity>()}");
                Plugin.Logger.LogInfo($"TargetEntity: {mapIconProxy.Read<MapIconTargetEntity>().TargetEntity._Entity == player.Read<PlayerCharacter>().UserEntity}");
            }
        }
        var iconToDestroyArray = mapIconProxies.ToArray().Where(x => x.Has<PrefabGUID>() && x.Read<PrefabGUID>().Equals(mapIconPrefab) && x.Read<MapIconTargetEntity>().TargetEntity._Entity == player.Read<PlayerCharacter>().UserEntity);
        Plugin.Logger.LogInfo($"Array: {iconToDestroyArray}");
        mapIconProxies.Dispose();
        foreach (var iconToDestroy in iconToDestroyArray)
        {
            Plugin.Logger.LogInfo($"icontodestroy exists: {iconToDestroy.Exists()}");
            Plugin.Logger.LogInfo($"icontodestroy targetEntity: {iconToDestroy.Read<MapIconTargetEntity>().TargetEntity}");
            Plugin.Logger.LogInfo($"icontodestroy network ID: {iconToDestroy.Read<MapIconTargetEntity>().TargetNetworkId}");

            if (iconToDestroy == Entity.Null)
            {
                Plugin.Logger.LogInfo("IconToDestroy is null");
                return false;
            }

            if (iconToDestroy.Has<AttachedBuffer>())
            {
                Plugin.Logger.LogInfo("Has Attached Buffer");
                var attachedBuffer = Core.EntityManager.GetBuffer<AttachedBuffer>(iconToDestroy);
                for (var i = 0; i < attachedBuffer.Length; i++)
                {
                    var attachedEntity = attachedBuffer[i].Entity;
                    Plugin.Logger.LogInfo($"{attachedEntity}");
                    if (attachedEntity == Entity.Null) continue;
                    attachedEntity.Remove<Attached>();
                    //attachedEntity.Remove<AttachedBuffer>();
                    attachedEntity.Remove<MapIconTargetEntity>();
                    attachedEntity.Remove<AttachMapIconsToEntity>();
                    Plugin.Logger.LogInfo($"{attachedEntity.Has<MapIconTargetEntity>()}");
                    StatChangeUtility.KillOrDestroyEntity(Core.EntityManager, attachedEntity, user, user, 0, StatChangeReason.Any, true);
                }
            }
            if (iconToDestroy.Has<MapIconTargetEntity>()) { iconToDestroy.Remove<MapIconTargetEntity>(); }
            if (iconToDestroy.Has<AttachMapIconsToEntity>()) { iconToDestroy.Remove<AttachMapIconsToEntity>(); }
            Plugin.Logger.LogInfo("Destroying icon...");
            StatChangeUtility.KillOrDestroyEntity(Core.EntityManager, iconToDestroy, user, user, 0, StatChangeReason.Any, true);
            Plugin.Logger.LogInfo("Icon Destroyed");
        }
        CleanIcons(player);
        return true;
    }

    public static bool RemoveAllIcons()
    {
        Plugin.Logger.LogInfo($"REMOVING ICONS");
        if (!Core.PrefabCollection._PrefabGuidToEntityMap.TryGetValue(mapIconPrefab, out mapIconProxyPrefab))
            Plugin.Logger.LogError("Failed to find MapIcon_ProxyObject_POI_Unknown Prefab entity");

        mapIconProxyQuery = Core.EntityManager.CreateEntityQuery(new EntityQueryDesc()
        {
            All = new ComponentType[] {
                ComponentType.ReadOnly<AttachMapIconsToEntity>()
            },
            Options = EntityQueryOptions.IncludeDisabled
        });

        var userModel = GameData.Users.All.FirstOrDefault();
        var user = userModel.Entity;

        var mapIconProxies = mapIconProxyQuery.ToEntityArray(Allocator.Temp);
        var iconToDestroyArray = mapIconProxies.ToArray().Where(x => x.Has<PrefabGUID>() && x.Read<PrefabGUID>().Equals(mapIconPrefab)); //&& x.Read<MapIconTargetEntity>().TargetEntity._Entity == player.Read<Entity>() && x.Read<MapIconTargetEntity>().TargetNetworkId == player.Read<NetworkId>());
        Plugin.Logger.LogInfo($"Array: {iconToDestroyArray}");
        mapIconProxies.Dispose();
        foreach (var iconToDestroy in iconToDestroyArray)
        {
            Plugin.Logger.LogInfo($"icontodestroy targetEntity: {iconToDestroy.Read<MapIconTargetEntity>().TargetEntity}");
            Plugin.Logger.LogInfo($"icontodestroy network ID: {iconToDestroy.Read<MapIconTargetEntity>().TargetNetworkId}");

            if (iconToDestroy == Entity.Null)
            {
                Plugin.Logger.LogInfo("IconToDestroy is null");
                return false;
            }

            if (iconToDestroy.Has<AttachedBuffer>())
            {
                Plugin.Logger.LogInfo("Has Attached Buffer");
                var attachedBuffer = Core.EntityManager.GetBuffer<AttachedBuffer>(iconToDestroy);
                for (var i = 0; i < attachedBuffer.Length; i++)
                {
                    var attachedEntity = attachedBuffer[i].Entity;
                    Plugin.Logger.LogInfo($"{attachedEntity}");
                    if (attachedEntity == Entity.Null) continue;
                    attachedEntity.Remove<Attached>();
                    //attachedEntity.Remove<AttachedBuffer>();
                    attachedEntity.Remove<MapIconTargetEntity>();
                    attachedEntity.Remove<AttachMapIconsToEntity>();
                    Plugin.Logger.LogInfo($"{attachedEntity.Has<MapIconTargetEntity>()}");
                    StatChangeUtility.KillOrDestroyEntity(Core.EntityManager, attachedEntity, user, user, 0, StatChangeReason.Any, true);
                }
            }
            if (iconToDestroy.Has<MapIconTargetEntity>()) { iconToDestroy.Remove<MapIconTargetEntity>(); }
            if (iconToDestroy.Has<AttachMapIconsToEntity>()) { iconToDestroy.Remove<AttachMapIconsToEntity>(); }
            Plugin.Logger.LogInfo("Destroying icon...");
            StatChangeUtility.KillOrDestroyEntity(Core.EntityManager, iconToDestroy, user, user, 0, StatChangeReason.Any, true);
            Plugin.Logger.LogInfo("Icon Destroyed");
        }
        return true;
    }

    public static void CleanIcons(Entity player)
    {
        Plugin.Logger.LogInfo("CLEANING ICONS");
        var userModel = GameData.Users.All.FirstOrDefault();
        var user = userModel.Entity;
        var entities = QueryComponents.GetEntitiesByComponentTypes<MapIconData>(EntityQueryOptions.IncludeDisabledEntities);
        foreach (var entity in entities)
        {
            var prefab = entity.Read<PrefabGUID>();
            if (prefab == mapIconPrefab && ((!entity.Has<MapIconTargetEntity>()) || entity.Read<MapIconTargetEntity>().TargetEntity._Entity == player.Read<Entity>()))
            {
                Plugin.Logger.LogInfo("Icon Destroyed CLEAN ICONS");
                StatChangeUtility.KillOrDestroyEntity(Core.EntityManager, entity, user, user, 0, StatChangeReason.Any, true);
            }
        }
        entities.Dispose();
    }

}
