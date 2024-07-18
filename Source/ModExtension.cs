using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace CustomizableSkillDecay;
public class SkillDecayExtension : DefModExtension
{
    public int xpThresholdPerDay = SkillRecord.MaxFullRateXpPerDay;
    public SimpleCurve decayCurve;
    public override IEnumerable<string> ConfigErrors()
    {
        for (int i = 0; i <= 20; i++)
        {
            if (!decayCurve.Points.Exists(point => point.x == i))
            {
                decayCurve.Add(SkillDecayPatch.defaultDecayCurve[i]);
            }
            else if (decayCurve.Points[i].y > 0f)
            {
                yield return "Skill decay curve cannot have positive values";
            }
        }
    }
}
