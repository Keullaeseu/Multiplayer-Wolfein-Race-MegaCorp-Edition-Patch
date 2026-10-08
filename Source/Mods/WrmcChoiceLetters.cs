using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace MultiplayerWolfeinRaceMegaCorpEditionPatch.Source.Mods;

/// <summary>
///     Syncs WRMegaCorp custom ChoiceLetters. Verified against the mod IL:
///     both ChoiceLetter_CSC and ChoiceLetter_DebtSlave build their accept/reject
///     DiaOption actions as instance methods directly on the letter
///     (CSC: '&lt;get_Choices&gt;b__5_0/b__5_1', DebtSlave: 'b__6_0/b__6_1', void()),
///     capturing only 'this' (signal tags). Their instance is the syncable letter
///     itself, so they use MP.RegisterSyncMethod - same pattern as VEE
///     (ChoiceLetter_AcceptCrashlanders) in Multiplayer-Compatibility.
///     The reject option is also registered as the default letter choice so
///     archived/expired letters resolve identically on all clients.
/// </summary>
public static class WrmcChoiceLetters
{
    private const string LogPrefix = "[Multiplayer Wolfein Race MegaCorp Edition Patch]";

    public static void Patch()
    {
        PatchLetter("WRMegaCorp.ChoiceLetter_CSC");
        PatchLetter("WRMegaCorp.ChoiceLetter_DebtSlave");
    }

    private static void PatchLetter(string typeName)
    {
        var type = AccessTools.TypeByName(typeName);

        if (type == null)
        {
            Log.Warning($"{LogPrefix} Could not find {typeName}.");
            return;
        }

        var synced = WrmcLambdaSync.SyncParentLambdas(type, "get_Choices", typeof(void));

        if (synced != 2)
        {
            Log.Warning($"{LogPrefix} Expected 2 choices for {typeName}, synced {synced}.");
            return;
        }

        try
        {
            var matches = WrmcLambdaSync.FindParentLambdas(type, "get_Choices", typeof(void));

            if (matches.Count == 2)
                MP.RegisterDefaultLetterChoice(matches[1], type);
        }
        catch (Exception exception)
        {
            Log.Warning($"{LogPrefix} RegisterDefaultLetterChoice failed for {typeName}: {exception.Message}");
        }

        Log.Message($"{LogPrefix} Patched {typeName}.Choices.");
    }
}