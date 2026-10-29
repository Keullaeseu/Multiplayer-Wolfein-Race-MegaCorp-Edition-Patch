using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace MultiplayerWolfeinRaceMegaCorpEditionPatch.Source.Mods;

/// <summary>
///     Syncs the weapon-mode switch gizmo on the MegaCorp switch-mode comps
///     (IPawnWeaponGizmoProvider.GetWeaponGizmos).
///     The gizmo action cycles CurrentModeIndex, rebuilds ranged verbs, and plays
///     interaction sounds - all sim-side state that must run on every client.
///     Two comp classes exist: WRMegaCorp.Comp_SwitchModeRangedWeapon in the base
///     assembly, and WRMegaCorp.Comp_SwitchModeRangedWeapon_VEF in the VEF optional
///     module (LoadFolders.xml swaps compClass on both switchable weapons when VEF
///     Core is active - the VEF class is a separate ThingComp, not a subclass).
///     Both are patched here, and either type may be absent at runtime.
///     Actions are located by IL scan (writes CurrentModeIndex / calls
///     ApplyCurrentRangedMode) instead of ordinal, so the dev-only "Print Verb in
///     Log" gizmo that sits next to it is never registered.
///     Additionally syncs ApplyCurrentRangedMode itself: GetWeaponGizmos calls it
///     during gizmo enumeration whenever a verb rebuild is pending, which mutates
///     the shared verb list from UI context on whichever client happens to have the
///     weapon selected. Syncing the named method converges all clients immediately;
///     calls from already-synced contexts (Notify_Equipped, the synced gizmo action,
///     the long-event queue) execute directly without rebroadcast. Same safety-net
///     convention as CancelLoad in the sibling Wolfein repair-unit patch (not
///     modified here).
/// </summary>
public static class WrmcWeaponGizmo
{
    private const string LogPrefix = "[Multiplayer Wolfein Race MegaCorp Edition Patch]";

    private const string BaseCompTypeName = "WRMegaCorp.Comp_SwitchModeRangedWeapon";

    private const string VefCompTypeName = "WRMegaCorp.Comp_SwitchModeRangedWeapon_VEF";

    private static readonly string[] ModeStateFields = { "CurrentModeIndex" };

    private static readonly string[] ModeStateMethods = { "ApplyCurrentRangedMode" };

    public static void Patch()
    {
        PatchComp(BaseCompTypeName);
        PatchComp(VefCompTypeName);
    }

    private static void PatchComp(string compTypeName)
    {
        var compType = AccessTools.TypeByName(compTypeName);

        if (compType == null)
        {
            Log.Message($"{LogPrefix} {compTypeName} not loaded; skipping.");
            return;
        }

        var synced = WrmcLambdaSync.SyncStateChangingParentLambdas(
            compType, "GetWeaponGizmos", ModeStateFields, ModeStateMethods);

        if (synced != 1)
        {
            Log.Warning($"{LogPrefix} Expected 1 weapon mode switch action in {compTypeName}, synced {synced}.");
            return;
        }

        Log.Message($"{LogPrefix} Patched {compTypeName}.GetWeaponGizmos().");

        SyncVerbRebuild(compType, compTypeName);
    }

    private static void SyncVerbRebuild(Type compType, string compTypeName)
    {
        var method = AccessTools.DeclaredMethod(compType, "ApplyCurrentRangedMode");

        if (method == null)
        {
            Log.Warning($"{LogPrefix} Could not find {compTypeName}.ApplyCurrentRangedMode.");
            return;
        }

        try
        {
            MP.RegisterSyncMethod(method);
            Log.Message($"{LogPrefix} Synced {compTypeName}.ApplyCurrentRangedMode.");
        }
        catch (Exception exception)
        {
            Log.Warning($"{LogPrefix} Could not sync {compTypeName}.ApplyCurrentRangedMode: {exception.Message}");
        }
    }
}
