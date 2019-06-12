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
    public class Accuracy : Skill
    {
        protected override double SkillMultiplier => 25.0;
        protected override double StrainDecayBase => 0.3;

        private double prevAvg = 0;
        private double prevAvg2 = 0;

        protected override double StrainValueOf(DifficultyHitObject current)
        {
            if (current.BaseObject is Spinner)
                return 0;
            
            var osuCurrent = (OsuDifficultyHitObject)current;

            double rhythmConstant = 4.0 * Math.Pow(0.1, -osuCurrent.StrainTime / 1000);

            if (osuCurrent.StrainTime > 1500)
            {
                prevAvg = osuCurrent.StrainTime;
                prevAvg2 = Math.Pow(osuCurrent.StrainTime, 2.0);
            }

            double avg = prevAvg * (1.0 / rhythmConstant) + osuCurrent.StrainTime * (rhythmConstant - 1.0) / (rhythmConstant);
            double avg2 = prevAvg2 * (1.0 / rhythmConstant) + Math.Pow(osuCurrent.StrainTime, 2.0) * (rhythmConstant - 1.0) / (rhythmConstant);
            
            double coeffVarCurr = Math.Sqrt(avg2 - Math.Pow(avg, 2.0)) / avg;

            prevAvg = avg;
            prevAvg2 = avg2;

            return Math.Cos(Math.PI / 2.0 * Math.Min(osuCurrent.StrainTime, avg) / Math.Max(osuCurrent.StrainTime, avg));
        }
    }
}