using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace MultiplayerWolfeinRaceMegaCorpEditionPatch.Source.Mods;

/// <summary>
///     Syncs the Armistice Day caravan visit. Verified against the mod IL:
///     WorldObject_ArmisticeDay.Notify_CaravanArrived generates a pawn
///     (Verse.Rand + PawnGenerator), opens a Dialog_NodeTree for the join offer,
///     then immediately grants food / mood / goodwill and destroys itself.
///     The immediate rewards must run on all clients (synced method), the join
///     dialog must open on all clients (sync dialog node tree), and the accept
///     lambda (DisplayClass4_0.'&lt;Notify_CaravanArrived&gt;b__0': AddPawn +
///     PassToWorld) must run on all clients (synced delegate).
///     CaravanArrivalAction_ADay.Arrived just forwards to Notify_CaravanArrived
///     and is synced as well so arrival from any path stays deterministic.
///     Same pattern as MoreFactionInteraction (RegisterSyncDialogNodeTree for
///     Notify_CaravanArrived + RegisterSyncMethod for the arrival comp).
/// </summary>
public static class WrmcArmisticeDay
{
    private const string LogPrefix = "[Multiplayer Wolfein Race MegaCorp Edition Patch]";

    private const string ArmisticeDayType = "WRMegaCorp.WorldObject_ArmisticeDay";
    private const string ArrivalActionType = "WRMegaCorp.CaravanArrivalAction_ADay";

    public static void Patch()
    {
        PatchNotifyCaravanArrived();
        PatchArrivalAction();
        PatchAcceptLambda();
    }

    private static void PatchNotifyCaravanArrived()
    {
        var type = AccessTools.TypeByName(ArmisticeDayType);

        if (type == null)
        {
            Log.Warning($"{LogPrefix} Could not find {ArmisticeDayType}.");
            return;
        }

        var method = AccessTools.DeclaredMethod(type, "Notify_CaravanArrived");

        if (method == null)
        {
            Log.Warning($"{LogPrefix} Could not find {ArmisticeDayType}.Notify_CaravanArrived.");
            return;
        }

        try
        {
            MP.RegisterSyncMethod(method);
            Log.Message($"{LogPrefix} Synced {ArmisticeDayType}.Notify_CaravanArrived.");
        }
        catch (Exception exception)
        {
            Log.Warning($"{LogPrefix} Could not sync {ArmisticeDayType}.Notify_CaravanArrived: {exception.Message}");
        }

        try
        {
            MP.RegisterSyncDialogNodeTree(type, "Notify_CaravanArrived");
            Log.Message($"{LogPrefix} Synced {ArmisticeDayType}.Notify_CaravanArrived dialog.");
        }
        catch (Exception exception)
        {
            Log.Warning(
                $"{LogPrefix} Could not sync dialog for {ArmisticeDayType}.Notify_CaravanArrived: {exception.Message}");
        }
    }

    private static void PatchArrivalAction()
    {
        var type = AccessTools.TypeByName(ArrivalActionType);

        if (type == null)
        {
            Log.Warning($"{LogPrefix} Could not find {ArrivalActionType}.");
            return;
        }

        var method = AccessTools.DeclaredMethod(type, "Arrived");

        if (method == null)
        {
            Log.Warning($"{LogPrefix} Could not find {ArrivalActionType}.Arrived.");
            return;
        }

        try
        {
            MP.RegisterSyncMethod(method);
            Log.Message($"{LogPrefix} Synced {ArrivalActionType}.Arrived.");
        }
        catch (Exception exception)
        {
            Log.Warning($"{LogPrefix} Could not sync {ArrivalActionType}.Arrived: {exception.Message}");
        }
    }

    private static void PatchAcceptLambda()
    {
        var synced = WrmcLambdaSync.SyncStateChangingLambdas(
            ArmisticeDayType,
            "Notify_CaravanArrived",
            [],
            ["AddPawn", "PassToWorld"]);

        if (synced != 1)
            Log.Warning($"{LogPrefix} Expected 1 caravan join accept action, synced {synced}.");
        else
            Log.Message($"{LogPrefix} Patched {ArmisticeDayType}.Notify_CaravanArrived accept action.");
    }
}