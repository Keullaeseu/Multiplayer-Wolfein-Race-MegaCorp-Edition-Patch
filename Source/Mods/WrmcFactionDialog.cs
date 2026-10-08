using HarmonyLib;
using Verse;

namespace MultiplayerWolfeinRaceMegaCorpEditionPatch.Source.Mods;

/// <summary>
///     Syncs WRMegaCorp.FactionDialog DiaOption actions that mutate sim state.
///     Verified against the mod IL (Wolfein-Race-MegaCorp-Edition-IL/FactionDialog.il):
///     - WolfeinRevivalFoundationDialog b__0 (donate small) and b__2 (donate big)
///     on DisplayClass12_0 call VerifyAmount + LaunchThingsOfType +
///     TryAffectGoodwillWith + ReceiveLetter.
///     - MegaCorpOnlineStore b__3 (prototype heart: MakeThing + DropThingsNear +
///     LaunchThingsOfType), b__4/b__5/b__1 (military aid small/medium/big:
///     TryCallAid + LaunchThingsOfType). b__1 lives on DisplayClass16_1 via the
///     shared outer display class, the rest on DisplayClass16_0.
///     Navigation lambdas (linkLateBind returning DiaNode) are intentionally not
///     synced - they only rebuild local dialog pages.
///     Also syncs Wolfein.FactionDialog.RequestDataQuest b__0 (base Wolfein Race mod,
///     used as the data-quest option inside the online store): LaunchThingsOfType +
///     MakeThing + DropThingGroupsNear. The sibling Wolfein patch does not cover
///     dialogs, so there is no double registration.
///     IL-scan filtering by touched state methods keeps this robust against
///     ordinal / display-class layout shifts between mod builds.
/// </summary>
public static class WrmcFactionDialog
{
    private const string LogPrefix = "[Multiplayer Wolfein Race MegaCorp Edition Patch]";

    private const string MegaCorpFactionDialog = "WRMegaCorp.FactionDialog";
    private const string WolfeinFactionDialog = "Wolfein.FactionDialog";

    private static readonly string[] MutatingMethods =
    [
        "LaunchThingsOfType",
        "TryAffectGoodwillWith",
        "TryCallAid",
        "DropThingsNear",
        "DropThingGroupsNear",
        "MakeThing"
    ];

    public static void Patch()
    {
        PatchMegaCorpFoundation();
        PatchMegaCorpOnlineStore();
        PatchWolfeinRequestDataQuest();
    }

    private static void PatchMegaCorpFoundation()
    {
        var type = AccessTools.TypeByName(MegaCorpFactionDialog);

        if (type == null)
        {
            Log.Warning($"{LogPrefix} Could not find {MegaCorpFactionDialog}.");
            return;
        }

        var synced = WrmcLambdaSync.SyncStateChangingLambdas(
            MegaCorpFactionDialog,
            "WolfeinRevivalFoundationDialog",
            [],
            ["LaunchThingsOfType", "TryAffectGoodwillWith", "ReceiveLetter"]);

        if (synced != 2)
            Log.Warning($"{LogPrefix} Expected 2 donate actions, synced {synced}.");
        else
            Log.Message($"{LogPrefix} Patched {MegaCorpFactionDialog}.WolfeinRevivalFoundationDialog().");
    }

    private static void PatchMegaCorpOnlineStore()
    {
        var type = AccessTools.TypeByName(MegaCorpFactionDialog);

        if (type == null)
        {
            Log.Warning($"{LogPrefix} Could not find {MegaCorpFactionDialog}.");
            return;
        }

        var synced = WrmcLambdaSync.SyncStateChangingLambdas(
            MegaCorpFactionDialog,
            "MegaCorpOnlineStore",
            [],
            MutatingMethods);

        if (synced != 4)
            Log.Warning($"{LogPrefix} Expected 4 online store buy actions, synced {synced}.");
        else
            Log.Message($"{LogPrefix} Patched {MegaCorpFactionDialog}.MegaCorpOnlineStore().");
    }

    private static void PatchWolfeinRequestDataQuest()
    {
        var type = AccessTools.TypeByName(WolfeinFactionDialog);

        if (type == null)
        {
            Log.Warning($"{LogPrefix} Could not find {WolfeinFactionDialog} (base Wolfein Race mod).");
            return;
        }

        var synced = WrmcLambdaSync.SyncStateChangingLambdas(
            WolfeinFactionDialog,
            "RequestDataQuest",
            [],
            MutatingMethods);

        if (synced != 1)
            Log.Warning($"{LogPrefix} Expected 1 RequestDataQuest buy action, synced {synced}.");
        else
            Log.Message($"{LogPrefix} Patched {WolfeinFactionDialog}.RequestDataQuest().");
    }
}