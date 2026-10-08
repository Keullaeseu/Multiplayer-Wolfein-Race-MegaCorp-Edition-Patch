using HarmonyLib;
using Multiplayer.Compat;
using Verse;

namespace MultiplayerWolfeinRaceMegaCorpEditionPatch.Source.Mods;

/// <summary>
///     Isolates synced Verse.Rand usage in incident execution and cosmetic VFX.
///     - IncidentWorker_BlackMarketTradeShip.TryExecuteWorker generates a faction
///     (FactionGenerator), spawns a trade ship, and calls GenerateThings (Rand
///     inside). Surrounding it with Rand.PushState/PopState keeps the incident's
///     Rand sequence from leaking into (or reading a diverged) global Rand state.
///     Same standard pattern as WolfeinIncident (DroneRaid) in the sibling
///     Wolfein patch and AlphaBiomes in Multiplayer-Compatibility.
///     - Verb_DragonBreath.SpawnDragonBreathVfx consumes synced Rand for purely
///     visual spread / range / mote lifespan while spawning the IncineratorSpray
///     effect thing. Isolating it keeps cosmetic variation from shifting the sim
///     Rand stream for subsequent combat ticks.
/// </summary>
public static class WrmcIncidentAndVfx
{
    private const string LogPrefix = "[Multiplayer Wolfein Race MegaCorp Edition Patch]";

    public static void Patch()
    {
        PatchBlackMarketIncident();
        PatchDragonBreathVfx();
    }

    private static void PatchBlackMarketIncident()
    {
        const string method = "WRMegaCorp.IncidentWorker_BlackMarketTradeShip:TryExecuteWorker";

        var target = AccessTools.DeclaredMethod(method) ?? AccessTools.Method(method);

        if (target == null)
        {
            Log.Warning($"{LogPrefix} Could not find {method}.");
            return;
        }

        PatchingUtilities.PatchPushPopRand(target);
        Log.Message($"{LogPrefix} Patched IncidentWorker_BlackMarketTradeShip.TryExecuteWorker().");
    }

    private static void PatchDragonBreathVfx()
    {
        var type = AccessTools.TypeByName("WRMegaCorp.Verb_DragonBreath");

        if (type == null)
        {
            Log.Warning($"{LogPrefix} Could not find WRMegaCorp.Verb_DragonBreath.");
            return;
        }

        var target = AccessTools.DeclaredMethod(type, "SpawnDragonBreathVfx");

        if (target == null)
        {
            Log.Warning($"{LogPrefix} Could not find WRMegaCorp.Verb_DragonBreath.SpawnDragonBreathVfx.");
            return;
        }

        PatchingUtilities.PatchPushPopRand(target);
        Log.Message($"{LogPrefix} Patched Verb_DragonBreath.SpawnDragonBreathVfx().");
    }
}