using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using Bloody.Core;
using Bloody.Core.API.v1;
using HarmonyLib;
using ProjectM;
using ProjectM.Network;
using Unity.Entities;
using VampireCommandFramework;
using System.Net.Http;
using System.Net;

namespace GatorBounty;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("gg.deca.VampireCommandFramework")]
[BepInDependency("gg.deca.Bloodstone")]
[BepInDependency("trodi.Bloody.Core")]
[BepInDependency("gg.deca.Killfeed")]
[Bloodstone.API.Reloadable]
public class Plugin : BasePlugin
{
    Harmony _harmony;
    internal static Plugin Instance { get; private set; }
    public static ManualLogSource Logger;
    private static readonly HttpClient httpClient = new HttpClient();
    public override void Load()
    {
        Logger = Log;
        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} version {MyPluginInfo.PLUGIN_VERSION} is loaded!");
        Instance = this;
        // Harmony patching
        _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        _harmony.PatchAll(System.Reflection.Assembly.GetExecutingAssembly());

        // Register all commands in the assembly with VCF
        CommandRegistry.RegisterAll();
        Settings.Initialize(Config);

        DataStore.LoadFromDisk();

        EventsHandlerSystem.OnInitialize += GameDataOnInitialize;
    }

    public override bool Unload()
    {
        EventsHandlerSystem.OnInitialize -= GameDataOnInitialize;
        CommandRegistry.UnregisterAssembly();
        _harmony?.UnpatchSelf();
        return true;
    }
    private static void GameDataOnInitialize(World world)
    {
        Core.Initialize();
    }

    private static World GetWorld(string name)
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
