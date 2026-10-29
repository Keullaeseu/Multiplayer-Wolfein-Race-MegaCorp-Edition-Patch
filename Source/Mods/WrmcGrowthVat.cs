using System.Reflection;
using HarmonyLib;
using Multiplayer.API;
using Multiplayer.Compat;
using Verse;

namespace MultiplayerWolfeinRaceMegaCorpEditionPatch.Source.Mods;

/// <summary>
///     Syncs WRMegaCorp.WolfeinGrowthVat (declared in the Biotech optional module,
///     OptionalLoaded/Biotech, gated on Ludeon.RimWorld.Biotech). The mod ships its own
///     growth vat building (WRMC_WolfeinGrowthVat), and WGrowthVat_VNPE only overrides
///     pipe drawing, so everything here applies to the VNPE variant too.
///     Multiplayer core only covers Building_Enterable.SelectPawn (already synced, which
///     handles the "insert person" gizmo and float-menu entry) and the vanilla
///     Building_GrowthVat gizmos. WolfeinGrowthVat derives from Building_Enterable, not
///     from Building_GrowthVat, so its UI entry points are unsynced. This mirrors the
///     vanilla Growth Vat block in MP's SyncMethods.cs:
///     - Cancel load (destroys the embryo / drops the pawn) is a player action and is
///       synced normally.
///     - Cancel growth opens a confirmation dialog, so the outer lambda is UI-only and
///       is not synced; the local function it calls on confirmation (DoCancel, which
///       runs Finish + TryDropAll) is the mutation and is synced instead.
///     - SelectEmbryo is called from the implant-embryo float menu (UI context), so the
///       named method is synced; the enclosing display-class lambdas are left alone
///       because a synced delegate would have to serialize the captured embryo list.
///     - The dev-tool actions (advance a year, advance gestation, birth now, fill / empty
///       nutrition) and the DEV learning hediff's Learn (invoked as a method group, so
///       there is no lambda to match) are registered as debug-only, matching vanilla.
///     Lambdas are located by IL scan rather than ordinal: only actions declared directly
///     on the vat type (no captured locals) that write one of the vat's state fields or
///     call one of its state methods are registered, so the dialog opener, the insert
///     person float menu, and the "inspect genes" window are never picked up.
///     Tick-driven growth, starvation, ejection and growth learning are deterministic and
///     are not touched.
/// </summary>
public static class WrmcGrowthVat
{
    private const string LogPrefix = "[Multiplayer Wolfein Race MegaCorp Edition Patch]";

    private const string VatTypeName = "WRMegaCorp.WolfeinGrowthVat";

    private const string LearningHediffTypeName = "WRMegaCorp.Hediff_WolfeinVatLearning";

    private static readonly string[] PlayerGizmoStateFields =
    {
        "selectedPawn", "selectedEmbryo", "embryoStarvation"
    };

    private static readonly string[] PlayerGizmoStateMethods =
    {
        "DestroyEmbryo", "TryDropAll", "OnStop", "Fail", "EndCurrentJob",
        "Notify_PawnRemoved", "FinishPawn", "FinishEmbryo", "TryGrowEmbryo"
    };

    private static readonly string[] DebugGizmoStateFields =
    {
        "startTick", "containedNutrition"
    };

    private static readonly string[] DebugGizmoStateMethods =
    {
        "Finish", "Notify_TickedInGrowthVat"
    };

    public static void Patch()
    {
        var vatType = AccessTools.TypeByName(VatTypeName);

        if (vatType == null)
        {
            Log.Message($"{LogPrefix} {VatTypeName} not loaded (Biotech optional module inactive); skipping.");
            return;
        }

        var playerActions = WrmcLambdaSync.SyncStateChangingParentLambdas(
            vatType, "GetGizmos", PlayerGizmoStateFields, PlayerGizmoStateMethods);

        var debugActions = WrmcLambdaSync.SyncStateChangingParentLambdas(
            vatType, "GetGizmos", DebugGizmoStateFields, DebugGizmoStateMethods, true);

        SyncNamedMethod(vatType, "SelectEmbryo");
        SyncLocalFunction(vatType, "GetGizmos", "DoCancel");

        var hediffType = AccessTools.TypeByName(LearningHediffTypeName);

        if (hediffType != null)
            SyncNamedMethod(hediffType, "Learn", true);
        else
            Log.Warning($"{LogPrefix} Could not find {LearningHediffTypeName}.");

        Log.Message(
            $"{LogPrefix} Patched {VatTypeName} ({playerActions} player actions, {debugActions} debug actions).");
    }

    private static void SyncNamedMethod(Type type, string methodName, bool debugOnly = false)
    {
        var method = AccessTools.DeclaredMethod(type, methodName);

        if (method == null)
        {
            Log.Warning($"{LogPrefix} Could not find {type.Name}.{methodName}.");
            return;
        }

        try
        {
            var sync = MP.RegisterSyncMethod(method);

            if (debugOnly)
                sync.SetDebugOnly();

            Log.Message($"{LogPrefix} Synced {type.Name}.{methodName}.");
        }
        catch (Exception exception)
        {
            Log.Warning($"{LogPrefix} Could not sync {type.Name}.{methodName}: {exception.Message}");
        }
    }

    private static void SyncLocalFunction(Type type, string parentMethodName, string localFunctionName)
    {
        MethodInfo method;

        try
        {
            method = MpMethodUtil.GetLocalFunc(type, parentMethodName, MethodType.Normal, null, localFunctionName);
        }
        catch (Exception exception)
        {
            Log.Warning(
                $"{LogPrefix} Could not find local function {type.Name}.{parentMethodName}::{localFunctionName}: {exception.Message}");
            return;
        }

        try
        {
            MP.RegisterSyncMethod(method);
            Log.Message($"{LogPrefix} Synced {type.Name}.{parentMethodName}::{localFunctionName}.");
        }
        catch (Exception exception)
        {
            Log.Warning(
                $"{LogPrefix} Could not sync {type.Name}.{parentMethodName}::{localFunctionName}: {exception.Message}");
        }
    }
}
