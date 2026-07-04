using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;

using HarmonyLib;

namespace DynamicTeleportAreas;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public class DynamicTeleportAreasPlugin : BaseUnityPlugin
{
    private const string PluginGuid = "com.arifogel.dynamicteleportareas";
    private const string PluginName = "DynamicTeleportAreas";
    private const string PluginVersion = "1.0.0";

    private static DynamicTeleportAreasPlugin s_instance;

    // Control flag to cleanly intercept and drop on-screen HUD popups
    public static bool SuppressNotifications;

    // Configuration Entries
    internal static ConfigEntry<bool> Enabled;
    private static ConfigEntry<bool> s_enableMessageHudNotifications;
    private static ConfigEntry<int> s_normalLoadedArea;
    private static ConfigEntry<int> s_normalGeneratedArea;
    internal static ConfigEntry<int> TeleportLoadedArea;
    internal static ConfigEntry<int> TeleportGeneratedArea;
    private static ConfigEntry<int> s_frameDelayCount;

    private void Awake()
    {
        s_instance = this;
        InitConfiguration();

        Harmony harmony = new(PluginGuid);
        harmony.PatchAll();

        Logger.LogInfo("DynamicTeleportAreas initialized and configs bound.");
    }

    private void InitConfiguration()
    {
        Enabled = Config.Bind(
            "1 - Global Settings", "Enabled", true,
            "Whether mod should be enabled or disabled.");

        s_enableMessageHudNotifications = Config.Bind(
            "1 - Global Settings", "MessageHudNotifications", false,
            "When Enabled, on-screen notifications are shown at top-left when this mod changes Loaded/Generated areas during portal transitions.");

        s_normalLoadedArea = Config.Bind(
            "2 - Standard Gameplay Environment", "NormalLoadedArea", 4,
            "The 'Loaded zones' value restored after portal stabilization completes.");

        s_normalGeneratedArea = Config.Bind(
            "2 - Standard Gameplay Environment", "NormalGeneratedArea", 6,
            "The 'Generated zones' value restored after portal stabilization completes.");

        TeleportLoadedArea = Config.Bind(
            "3 - Portal Transition Environment", "PortalLoadedArea", 2,
            "The temporary 'Loaded zones' value enforced during portal loading screens.");

        TeleportGeneratedArea = Config.Bind(
            "3 - Portal Transition Environment", "PortalGeneratedArea", 4,
            "The temporary 'Generated zones' value enforced during portal loading screens.");

        s_frameDelayCount = Config.Bind(
            "4 - Engine Timing Controls", "FrameDelayCount", 30,
            "The number of engine frames to wait after exiting a portal before restoring standard render limits. Higher values delay full world detail but prevent post-portal stutter. The optimal value is the lowest setting where your framerate fully stabilizes before the environment expands.");
    }

    public static void SetRenderLimits(int loaded, int generated)
    {
        if (s_instance is null)
        {
            return;
        }

        // Trigger HUD silence if screen logging is disabled by the user
        if (!s_enableMessageHudNotifications.Value)
        {
            SuppressNotifications = true;
        }

        try
        {
            // Execute Commands (Always run this so Render Limits flushes its active cache arrays)
            if (Console.instance != null)
            {
                Console.instance.TryRunCommand($"render_config loaded_zones {loaded}");
                Console.instance.TryRunCommand($"render_config loaded_area {loaded}");
                Console.instance.TryRunCommand($"render_config generated_zones {generated}");
                Console.instance.TryRunCommand($"render_config generated_area {generated}");
                Console.instance.TryRunCommand($"render_config distant_area {generated}");
            }
        }
        finally
        {
            // Instantly lower the gate to let standard gameplay notifications process normally
            SuppressNotifications = false;
        }

        // Synchronize config files directly in memory as a background fallback
        if (!Chainloader.PluginInfos.TryGetValue("valheim.jerekuusela.render_limits",
                out PluginInfo renderLimitsPlugin))
        {
            return;
        }

        ConfigFile config = renderLimitsPlugin.Instance.Config;
        IDictionary<ConfigDefinition, ConfigEntryBase> dict = config;

        ConfigDefinition loadedDefNew = new("1. Zones", "Loaded zones");
        ConfigDefinition loadedDefOld = new("General", "loaded_area");
        ConfigDefinition genDefNew = new("1. Zones", "Generated zones");
        ConfigDefinition genDefOld = new("General", "distant_area");

        if (dict.TryGetValue(loadedDefNew, out ConfigEntryBase entryLNew))
        {
            entryLNew.BoxedValue = loaded;
        }
        else if (dict.TryGetValue(loadedDefOld, out ConfigEntryBase entryLOld))
        {
            entryLOld.BoxedValue = loaded;
        }

        if (dict.TryGetValue(genDefNew, out ConfigEntryBase entryGNew))
        {
            entryGNew.BoxedValue = generated;
        }
        else if (dict.TryGetValue(genDefOld, out ConfigEntryBase entryGOld))
        {
            entryGOld.BoxedValue = generated;
        }
    }

    public static void TriggerExpansion()
    {
        if (s_instance != null)
        {
            s_instance.StartCoroutine(StreamDelayedZonesRoutine());
        }
    }

    private static IEnumerator StreamDelayedZonesRoutine()
    {
        int targetDelay = s_frameDelayCount.Value;

        for (int i = 0; i < targetDelay; i++)
        {
            yield return null;
        }

        SetRenderLimits(s_normalLoadedArea.Value, s_normalGeneratedArea.Value);
    }
}

[HarmonyPatch(typeof(Player), nameof(Player.TeleportTo))]
internal static class PatchPortalEnter
{
    internal static void Prefix()
    {
        if (!DynamicTeleportAreasPlugin.Enabled.Value)
        {
            return;
        }

        DynamicTeleportAreasPlugin.SetRenderLimits(DynamicTeleportAreasPlugin.TeleportLoadedArea.Value,
            DynamicTeleportAreasPlugin.TeleportGeneratedArea.Value);
    }
}

[HarmonyPatch(typeof(Player), "UpdateTeleport")]
internal static class PatchPortalExit
{
    private static bool s_wasTeleporting;

    [SuppressMessage("ReSharper", "InconsistentNaming",
        Justification = "The double-underscore prefix is an absolute syntactic requirement for Harmony runtime instance injection.")]
    internal static void Postfix(Player __instance)
    {
        if (!DynamicTeleportAreasPlugin.Enabled.Value)
        {
            return;
        }

        if (__instance.IsTeleporting())
        {
            s_wasTeleporting = true;
        }
        else if (s_wasTeleporting)
        {
            s_wasTeleporting = false;
            DynamicTeleportAreasPlugin.TriggerExpansion();
        }
    }
}

// Intercepts the game HUD notification pipeline to block on-screen text popups
[HarmonyPatch(typeof(MessageHud), "ShowMessage")]
internal static class PatchMessageHudSilence
{
    internal static bool Prefix()
    {
        return !DynamicTeleportAreasPlugin.SuppressNotifications;
    }
}

// Intercepts the Chat log frame in case notifications are routed locally
[HarmonyPatch(typeof(Chat), "AddString")]
internal static class PatchChatSilence
{
    internal static bool Prefix()
    {
        return !DynamicTeleportAreasPlugin.SuppressNotifications;
    }
}