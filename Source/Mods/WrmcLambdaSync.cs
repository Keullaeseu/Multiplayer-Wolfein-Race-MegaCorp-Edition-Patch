using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace MultiplayerWolfeinRaceMegaCorpEditionPatch.Source.Mods;

/// <summary>
///     Locates compiler-generated lambdas by name prefix plus signature (parent-type
///     instance lambdas) and by IL scan (display-class lambdas that mutate sim state).
///     Ordinals and generated type layouts shift between mod builds (display class vs
///     methods directly on the type), while the <c>&lt;ParentMethod&gt;b__</c> naming
///     plus the signature / touched state stay put. Verified against the mod IL
///     (Wolfein-Race-MegaCorp-Edition-IL).
///     Parent-type instance lambdas are synced with <c>MP.RegisterSyncMethod</c>:
///     their instance is the syncable comp / letter itself, so no delegate field sync
///     is needed. Display-class lambdas use <c>MP.RegisterSyncDelegate</c>, which syncs
///     captured Map / Faction / Pawn / Caravan fields via MP sync workers.
/// </summary>
internal static class WrmcLambdaSync
{
    private const string LogPrefix = "[Multiplayer Wolfein Race MegaCorp Edition Patch]";

    public static List<MethodInfo> FindParentLambdas(Type parentType, string parentMethod, Type returnType,
        params Type[] argTypes)
    {
        var matches = new List<MethodInfo>();
        var prefix = "<" + parentMethod + ">b__";

        foreach (var candidate in parentType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                                                        BindingFlags.Instance | BindingFlags.Static |
                                                        BindingFlags.DeclaredOnly))
        {
            if (!candidate.Name.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            if (candidate.ReturnType != returnType)
                continue;

            var parms = candidate.GetParameters();

            if (parms.Length != argTypes.Length)
                continue;

            var shapeMatches = true;

            for (var index = 0; index < parms.Length; index++)
                if (parms[index].ParameterType != argTypes[index])
                {
                    shapeMatches = false;
                    break;
                }

            if (shapeMatches)
                matches.Add(candidate);
        }

        return matches;
    }

    public static int SyncParentLambdas(Type parentType, string parentMethod, Type returnType, params Type[] argTypes)
    {
        var matches = FindParentLambdas(parentType, parentMethod, returnType, argTypes);

        foreach (var match in matches)
        {
            MP.RegisterSyncMethod(match);
            Log.Message($"{LogPrefix} Synced {parentType.Name}.{parentMethod} action {match.Name}.");
        }

        if (matches.Count == 0)
            Log.Warning($"{LogPrefix} No matching actions found in {parentType.Name}.{parentMethod}.");

        return matches.Count;
    }

    public static int SyncStateChangingLambdas(
        string typeName,
        string parentMethodName,
        string[] stateFieldNames,
        string[] stateMethodNames,
        SyncContext? context = null)
    {
        var found = FindStateLambdas(typeName, parentMethodName, stateFieldNames, stateMethodNames);
        var parentType = AccessTools.TypeByName(typeName);
        var synced = 0;

        foreach (var method in found)
            try
            {
                if (method.DeclaringType == parentType)
                {
                    var sync = MP.RegisterSyncMethod(method);

                    if (context.HasValue)
                        sync.SetContext(context.Value);
                }
                else
                {
                    var sync = MP.RegisterSyncDelegate(parentType, method.DeclaringType!.Name, method.Name, null);

                    if (context.HasValue)
                        sync.SetContext(context.Value);
                }

                synced++;
                Log.Message(
                    $"{LogPrefix} Synced {typeName}.{parentMethodName} lambda {method.DeclaringType!.Name}.{method.Name}.");
            }
            catch (Exception exception)
            {
                Log.Warning(
                    $"{LogPrefix} Could not sync {typeName}.{parentMethodName} lambda {method.DeclaringType!.Name}.{method.Name}: {exception.Message}");
            }

        if (synced == 0)
            Log.Warning($"{LogPrefix} No state-changing lambdas found in {typeName}.{parentMethodName}.");

        return synced;
    }

    public static List<MethodInfo> FindStateLambdas(
        string typeName,
        string parentMethodName,
        string[] stateFieldNames,
        string[] stateMethodNames)
    {
        var found = new List<MethodInfo>();
        var parentType = AccessTools.TypeByName(typeName);

        if (parentType == null)
        {
            Log.Warning($"{LogPrefix} Type not found: {typeName}.");
            return found;
        }

        var prefix = $"<{parentMethodName}>b__";

        CollectMatching(parentType, parentType, prefix, stateFieldNames, stateMethodNames, found);

        foreach (var nested in AllNestedTypes(parentType))
            CollectMatching(parentType, nested, prefix, stateFieldNames, stateMethodNames, found);

        return found;
    }

    private static void CollectMatching(
        Type parentType,
        Type declaringType,
        string prefix,
        string[] stateFieldNames,
        string[] stateMethodNames,
        List<MethodInfo> found)
    {
        List<MethodInfo> methods;

        try
        {
            methods = AccessTools.GetDeclaredMethods(declaringType);
        }
        catch (Exception exception)
        {
            Log.Warning($"{LogPrefix} Could not list methods of {declaringType.FullName}: {exception.Message}");
            return;
        }

        foreach (var method in methods)
        {
            if (!method.Name.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            if (TouchesState(method, stateFieldNames, stateMethodNames))
                found.Add(method);
        }
    }

    private static bool TouchesState(MethodInfo method, string[] stateFieldNames, string[] stateMethodNames)
    {
        List<CodeInstruction> instructions;

        try
        {
            instructions = PatchProcessor.GetOriginalInstructions(method);
        }
        catch
        {
            return false;
        }

        foreach (var code in instructions)
        {
            if ((code.opcode == OpCodes.Stfld || code.opcode == OpCodes.Stsfld)
                && code.operand is FieldInfo field
                && Array.IndexOf(stateFieldNames, field.Name) >= 0)
                return true;

            if (code.operand is MethodInfo called && Array.IndexOf(stateMethodNames, called.Name) >= 0)
                return true;
        }

        return false;
    }

    private static IEnumerable<Type> AllNestedTypes(Type type)
    {
        foreach (var nested in type.GetNestedTypes(AccessTools.all))
        {
            yield return nested;

            foreach (var deeper in AllNestedTypes(nested))
                yield return deeper;
        }
    }
}