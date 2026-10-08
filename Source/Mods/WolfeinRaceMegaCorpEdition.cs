using Multiplayer.Compat;
using Verse;

namespace MultiplayerWolfeinRaceMegaCorpEditionPatch.Source.Mods;

/// <summary>
///     Multiplayer Patch for Wolfein Race MegaCorp Edition by Zoshsgahdnkc,
///     Last Update: 18 Sep @ 9:31am 2026
///     <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3687841204" />
///     Entry point: schedules <see cref="LatePatch" /> once mods are loaded,
///     which delegates to one patch class per MegaCorp feature.
/// </summary>
[MpCompatFor("com.zoshsgahdnkc.WRMegaCorpEdition")]
public class WolfeinRaceMegaCorpEdition
{
    private const string LogPrefix = "[Multiplayer Wolfein Race MegaCorp Edition Patch]";

    public WolfeinRaceMegaCorpEdition(ModContentPack content)
    {
        LongEventHandler.ExecuteWhenFinished(LatePatch);
    }

    private static void LatePatch()
    {
        Log.Message($"{LogPrefix} Initializing...");

        // Each block is isolated: a bad lambda match in one MegaCorp feature
        // must not prevent the remaining patches from registering.
        SafePatch(WrmcWeaponGizmo.Patch);
        SafePatch(WrmcFactionDialog.Patch);
        SafePatch(WrmcChoiceLetters.Patch);
        SafePatch(WrmcArmisticeDay.Patch);
        SafePatch(WrmcIncidentAndVfx.Patch);

        Log.Message($"{LogPrefix} Initialized.");
    }

    private static void SafePatch(Action patch)
    {
        try
        {
            patch();
        }
        catch (Exception exception)
        {
            Log.Error($"{LogPrefix} Patch {patch.Method.Name} failed: {exception}");
        }
    }
}