// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;
using MathNet.Numerics;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    ///
    /// </summary>
    public class Accuracy : OsuSkill
    {
        protected override double SkillMultiplier => 3.0;
        protected override double StrainDecayBase => 0.5;

        private double prevBaseStrain = 0;

        private List<OsuDifficultyHitObject> objectIntervals = new List<OsuDifficultyHitObject>();

        protected override double StrainValueOf(DifficultyHitObject current)
        {
            if (current.BaseObject is Spinner)
                return 0;
            
            var osuCurrent = (OsuDifficultyHitObject)current;

            while (objectIntervals.Sum(x => x.StrainTime) + osuCurrent.StrainTime > 2000 && objectIntervals.Count > 0)
            {
                objectIntervals.RemoveAt(0);
            }
            double baseChangeStrain = 0;
            double complexitySum = 0;
            int count = 1;

            if (objectIntervals.Count > 0)
            {
                count = objectIntervals.Count;

                double rhythmAvg = 0;
                foreach (OsuDifficultyHitObject obj in objectIntervals)
                    rhythmAvg += obj.StrainTime;

                rhythmAvg = rhythmAvg / objectIntervals.Count;
                double baseStrain = double.PositiveInfinity;

                foreach (OsuDifficultyHitObject obj in objectIntervals)
                    baseStrain = Math.Abs(baseStrain) < Math.Abs(obj.StrainTime - rhythmAvg) ? baseStrain : obj.StrainTime - rhythmAvg;
                baseStrain = baseStrain + rhythmAvg;

                double prevStrain = baseStrain;
                foreach (OsuDifficultyHitObject obj in objectIntervals)
                {
                    if (obj.BaseObject is Slider)
                    {
                        if (Math.Abs(obj.StrainTime - baseStrain) % baseStrain / baseStrain > 0.05)
                            complexitySum += 2;
                    }
                    else
                    {
                        if (Math.Abs(obj.StrainTime - baseStrain) % baseStrain / baseStrain > 0.05)
                            complexitySum += 4;
                        if (Math.Abs(prevStrain - obj.StrainTime) > 2)
                            complexitySum += 2;
                    
                        prevStrain = obj.StrainTime;
                    }
                }

                if (Math.Abs(baseStrain - prevBaseStrain) % baseStrain / baseStrain > 0.05)
                    baseChangeStrain = 4;
                
                prevBaseStrain = baseStrain;
            }

            objectIntervals.Add(osuCurrent);

            return baseChangeStrain + complexitySum / count;
        }
    }
}