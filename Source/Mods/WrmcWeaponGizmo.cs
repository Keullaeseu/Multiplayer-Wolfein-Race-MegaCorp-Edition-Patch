using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace MultiplayerWolfeinRaceMegaCorpEditionPatch.Source.Mods;

/// <summary>
///     Syncs the weapon-mode switch gizmo on WRMegaCorp.Comp_SwitchModeRangedWeapon
///     (IPawnWeaponGizmoProvider.GetWeaponGizmos).
///     The gizmo action cycles CurrentModeIndex, rebuilds ranged verbs, and plays
///     interaction sounds - all sim-side state that must run on every client.
///     The mod IL shows it as an instance method directly on the comp
///     (Comp_SwitchModeRangedWeapon.'&lt;GetWeaponGizmos&gt;b__14_0', void()),
///     synced by signature so ordinals cannot drift. Same pattern as
///     Wolfein.CompToolSwitcher in the sibling Wolfein patch (not modified here).
///     Additionally syncs ApplyCurrentRangedMode itself: GetWeaponGizmos calls it
///     during gizmo enumeration (Comp_SwitchModeRangedWeapon.cs:58-59) whenever a
///     verb rebuild is pending, which mutates the shared verb list from UI context
///     on whichever client happens to have the weapon selected. Syncing the named
///     method converges all clients immediately; calls from already-synced contexts
///     (Notify_Equipped, the synced gizmo action, the long-event queue) execute
///     directly without rebroadcast. Same safety-net convention as CancelLoad in
///     the sibling Wolfein repair-unit patch (not modified here).
/// </summary>
public static class WrmcWeaponGizmo
{
    private const string LogPrefix = "[Multiplayer Wolfein Race MegaCorp Edition Patch]";

    private const string CompTypeName = "WRMegaCorp.Comp_SwitchModeRangedWeapon";

    public static void Patch()
    {
        var compType = AccessTools.TypeByName(CompTypeName);

        if (compType == null)
        {
            Log.Warning($"{LogPrefix} Could not find {CompTypeName}.");
            return;
        }

        var synced = WrmcLambdaSync.SyncParentLambdas(compType, "GetWeaponGizmos", typeof(void));

        if (synced != 1)
        {
            Log.Warning($"{LogPrefix} Expected 1 weapon mode switch action, synced {synced}.");
            return;
        }

        Log.Message($"{LogPrefix} Patched {CompTypeName}.GetWeaponGizmos().");

        SyncVerbRebuild(compType);
    }

    private static void SyncVerbRebuild(Type compType)
    {
        var method = AccessTools.DeclaredMethod(compType, "ApplyCurrentRangedMode");

        if (method == null)
        {
            Log.Warning($"{LogPrefix} Could not find {CompTypeName}.ApplyCurrentRangedMode.");
            return;
        }

        try
        {
            MP.RegisterSyncMethod(method);
            Log.Message($"{LogPrefix} Synced {CompTypeName}.ApplyCurrentRangedMode.");
        }
        catch (Exception exception)
        {
            Log.Warning($"{LogPrefix} Could not sync {CompTypeName}.ApplyCurrentRangedMode: {exception.Message}");
        }
    }
}