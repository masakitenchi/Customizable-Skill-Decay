using System;
using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace CustomizableSkillDecay;

public class Main : Mod
{
    public static Settings ModSetting;
    private SkillDef skillDef;
    private string thresholdStringBuffer;

    public Main(ModContentPack content) : base(content)
    {
        Harmony harmony = new Harmony("regex.customizableskilldecay");
        harmony.PatchAll();
        // GetSettings should have already inited the dictionaries, but the very first time needs this
        LongEventHandler.QueueLongEvent(delegate
        {
            ModSetting = GetSettings<Settings>();
            foreach (var skill in DefDatabase<SkillDef>.AllDefs)
            {
                if (!ModSetting.decayCurves.ContainsKey(skill))
                {
                    ModSetting.decayCurves.Add(skill, SkillDecayPatch.defaultDecayCurve);
                }
                if (!ModSetting.thresholds.ContainsKey(skill))
                {
                    ModSetting.thresholds.Add(skill, SkillRecord.MaxFullRateXpPerDay);
                }
            }
            this.skillDef = DefDatabase<SkillDef>.AllDefs.First();
        }, "Init skill dict", false, null);
        LongEventHandler.QueueLongEvent(delegate
        {
            if (ModLister.GetActiveModWithIdentifier("ratys.madskills", true) != null)
            {
                Log.Error("Customizable Skill Decay is not compatible with Mad Skills. Please disable one of the mods.");
            }
            if (ModLister.GetActiveModWithIdentifier("slimesenpai.endlessgrowth") != null)
            {
                Log.Error("Customizable Skill Decay is not compatible with Endless Growth. Please disable one of the mods.");
            }
        }, "Check Incompatibilities", false, null);
    }

    public override string SettingsCategory()
    {
        return "Customizable Skill Decay";
    }

    private static int threshold;
    public override void DoSettingsWindowContents(Rect inRect)
    {
        Listing_Standard listing = new Listing_Standard();
        listing.Begin(inRect);
        listing.Gap(5f);
        if (listing.ButtonText(skillDef.LabelCap))
        {
            FloatMenuUtility.MakeMenu(DefDatabase<SkillDef>.AllDefs, skill => skill.LabelCap, skill => () => skillDef = skill);
        }
        threshold = ModSetting.thresholds[skillDef];
        listing.Label_NewTemp("DailyExpThresholdLabel".Translate());
        listing.IntEntry(ref threshold, ref thresholdStringBuffer);
        ModSetting.thresholds[skillDef] = threshold;
        Rect CurveRect = listing.GetRect(300f);
        SimpleCurve curve = ModSetting.decayCurves[skillDef];
        DoCurveEditor(CurveRect, curve);
        listing.End();
    }

    // Borrowed from Simple Sidearms
    private void DoCurveEditor(Rect CurveRect, SimpleCurve curve)
    {
        SimpleCurveDrawer.DrawCurves(CurveRect, new List<SimpleCurveDrawInfo>
            {
                new () { curve = curve},
                new () { curve = SkillDecayPatch.defaultDecayCurve, color = Color.gray }
            }, null, null, default(Rect));
        Vector2 mousePosition = Event.current.mousePosition - CurveRect.position;
        Vector2 mouseCurveCoords = SimpleCurveDrawer.ScreenToCurveCoords(CurveRect, curve.View.rect, mousePosition);
        if (Mouse.IsOver(CurveRect))
        {
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                var clampedCoords = mouseCurveCoords;
                clampedCoords.x = Mathf.Clamp(Mathf.Round(clampedCoords.x), 0, 20);
                clampedCoords.y = Mathf.Clamp((float)Math.Round(clampedCoords.y, 2), -20, 0);
                List<FloatMenuOption> list2 = new List<FloatMenuOption>();
                if (!curve.Any(point => point.x == clampedCoords.x))
                {
                    list2.Add(new FloatMenuOption($"AddPointAt".Translate(clampedCoords.x.ToString("0.#"), clampedCoords.y.ToString("0.#")), () =>
                    {
                        curve.Add(new CurvePoint(clampedCoords), true);
                    }, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
                }
                else
                {
                    var existingPoint = curve.First(point => point.x == clampedCoords.x);

                    list2.Add(new FloatMenuOption($"MovePointTo".Translate(existingPoint.x.ToString("0.#"), existingPoint.y.ToString("0.#"), clampedCoords.x.ToString("0.#"), clampedCoords.y.ToString("0.#")), () =>
                    {
                        curve.RemovePointNear(existingPoint);
                        curve.Add(new CurvePoint(clampedCoords), true);
                    }, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));

                    if (Mathf.RoundToInt(existingPoint.x) != 0 && Mathf.RoundToInt(existingPoint.x) != 20)
                    {
                        list2.Add(new FloatMenuOption($"RemovePointAt".Translate(existingPoint.x.ToString("0.#"), existingPoint.y.ToString("0.#")), () =>
                        {
                            curve.RemovePointNear(existingPoint);
                        }, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
                    }
                }
                Find.WindowStack.Add(new FloatMenu(list2));
                Event.current.Use();
            }
        }
    }
}


public class Settings : ModSettings
{
    internal Dictionary<SkillDef, SimpleCurve> decayCurves;
    internal Dictionary<SkillDef, int> thresholds;

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(ref decayCurves, "decayCurves", LookMode.Def, LookMode.Deep);
        Scribe_Collections.Look(ref thresholds, "thresholds", LookMode.Def, LookMode.Value);
    }

}