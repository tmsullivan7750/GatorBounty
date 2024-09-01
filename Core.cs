using BepInEx.Logging;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using GatorBounty;
using ProjectM;
using ProjectM.Physics;
using ProjectM.Scripting;
using ProjectM.Terrain;
using Stunlock.Localization;
using System.Collections;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using Unity.Entities;
using UnityEngine;

namespace GatorBounty;

internal static class Core
{
	public static World Server { get; } = GetWorld("Server") ?? throw new System.Exception("There is no Server world (yet). Did you install a server mod on the client?");

	public static EntityManager EntityManager { get; } = Server.EntityManager;
    public static ChunkObjectManager ChunkObjectManager { get; } = Server.GetExistingSystemManaged<ChunkObjectManager>();
    public static PrefabCollectionSystem PrefabCollection { get; } = Server.GetExistingSystemManaged<PrefabCollectionSystem>();
    public static ServerGameSettingsSystem ServerGameSettingsSystem { get; internal set; }
    public static ServerScriptMapper ServerScriptMapper { get; internal set; }
    public static ServerGameManager ServerGameManager => ServerScriptMapper.GetServerGameManager();
    public static double ServerTime => ServerGameManager.ServerTime;
    static MonoBehaviour monoBehaviour;

    public static bool hasInitialized;

    public static void LogException(System.Exception e, [CallerMemberName] string caller = null)
    {
        Plugin.Logger.LogError($"Failure in {caller}\nMessage: {e.Message} Inner:{e.InnerException?.Message}\n\nStack: {e.StackTrace}\nInner Stack: {e.InnerException?.StackTrace}");
    }
    public static void Initialize()
    {
        if (hasInitialized) return;

        ServerGameSettingsSystem = Server.GetExistingSystemManaged<ServerGameSettingsSystem>();
        ServerScriptMapper = Server.GetExistingSystemManaged<ServerScriptMapper>();

        string serverIP = Dns.GetHostAddresses(Dns.GetHostName())
                     .FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork)?.ToString();
        Plugin.Logger.LogError("Server IP: " + serverIP);

        Plugin.Logger.LogInfo("GatorBounty initialized");

        hasInitialized = true;
    }

    static World GetWorld(string name)
    {
        foreach (var world in World.s_AllWorlds)
        {
            if (world.Name == name)
            {
                return world;
            }
        }
        return null;
    }
}
