using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;
using static HarmonyLib.Code;

namespace CustomizableSkillDecay;

[HarmonyPatch]
public static class SkillDecayPatch
{
    internal static readonly SimpleCurve defaultDecayCurve = new()
    {
        new (0,0),
        new (1,0),
        new (2,0),
        new (3,0),
        new (4,0),
        new (5,0),
        new (6,0),
        new (7,0),
        new (8,0),
        new (9,0),
        new (10,-0.1f),
        new (11,-0.2f),
        new (12,-0.4f),
        new (13,-0.6f),
        new (14,-1f),
        new (15,-1.8f),
        new (16,-2.8f),
        new (17,-4f),
        new (18,-6f),
        new (19,-8f),
        new (20,-12f)
    };

    [HarmonyPatch(typeof(SkillRecord), nameof(SkillRecord.LearningSaturatedToday), methodType: MethodType.Getter)]
    [HarmonyPostfix]
    public static void UnlockSoftLearnLock(SkillRecord __instance, ref bool __result)
    {
        __result = Main.ModSetting.thresholds.TryGetValue(__instance.def, out int threshold) ? __instance.xpSinceMidnight >= threshold : __result;
    }


    [HarmonyPatch(typeof(SkillUI), "GetSkillDescription")]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> LoadDefModExtThreshold(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        foreach (var inst in instructions)
        {
            if (inst.Is(OpCodes.Ldc_I4, SkillRecord.MaxFullRateXpPerDay))
            {
                yield return Ldarg_0;
                yield return Call[AccessTools.Method(typeof(SkillDecayPatch), nameof(GetThreshold))];
                continue;
            }
            yield return inst;
        }
    }

    private static int GetThreshold(SkillRecord record)
    {
        return Main.ModSetting.thresholds.TryGetValue(record.def, out int threshold) ? threshold : SkillRecord.MaxFullRateXpPerDay;
    }

    private static readonly FieldInfo skillDef = AccessTools.Field(typeof(SkillRecord), nameof(SkillRecord.def));
    private static readonly FieldInfo levelInt = AccessTools.Field(typeof(SkillRecord), nameof(SkillRecord.levelInt));
    [HarmonyPatch(typeof(SkillRecord), nameof(SkillRecord.Interval))]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        Label label;
        LocalBuilder local = generator.DeclareLocal(typeof(SimpleCurve));
        foreach (var inst in instructions)
        {
            if (inst.opcode == OpCodes.Ldloc_1)
            {
                label = generator.DefineLabel();
                inst.labels.Add(label);
                yield return Ldsfld[AccessTools.Field(typeof(Main), nameof(Main.ModSetting))];
                yield return Ldfld[AccessTools.Field(typeof(Settings), nameof(Settings.decayCurves))];
                yield return Ldarg_0;
                yield return Ldfld[skillDef];
                yield return Ldloca[local];
                yield return Callvirt[AccessTools.Method(typeof(Dictionary<SkillDef, SimpleCurve>), nameof(Dictionary<SkillDef, SimpleCurve>.TryGetValue), [typeof(SkillDef), typeof(SimpleCurve).MakeByRefType()])];
                // If cannot retrieve, use default code
                yield return Brfalse_S[label];
                // If retrieved, use custom code
                // local should be the curve now
                yield return Ldarg_0;
                yield return Ldloc[local];
                yield return Ldarg_0; // SkillRecord
                yield return Ldc_I4_0; // False
                yield return Callvirt[AccessTools.Method(typeof(SkillRecord), nameof(SkillRecord.GetLevel))]; // Clamped at (0,20)
                yield return Conv_R4; // Convert to float
                yield return Callvirt[AccessTools.Method(typeof(SimpleCurve), nameof(SimpleCurve.Evaluate))];
                yield return Ldloc_0; // multiplier (is 0.5f if it has GreatMemory)
                yield return Mul;
                yield return Ldc_I4_0;
                yield return Ldc_I4_0;
                yield return Call[AccessTools.Method(typeof(SkillRecord), nameof(SkillRecord.Learn))];
                yield return Ret;
            }
            yield return inst;
        }
    }
}
