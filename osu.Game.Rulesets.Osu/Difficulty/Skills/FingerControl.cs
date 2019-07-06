// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    ///
    /// </summary>
    public class FingerControl : OsuSkill
    {
        private double StrainDecay = 1.0;
        protected override double SkillMultiplier => 25.0;
        protected override double StrainDecayBase => StrainDecay;
        protected override double StarMultiplierPerRepeat => 1.02;

        private int repeatStrainCount = 0;

        private int prevRepeatStrainCount = 0;

        protected override double StrainValueOf(DifficultyHitObject current)
        {
            if (current.BaseObject is Spinner)
                return 0;
            
            var osuCurrent = (OsuDifficultyHitObject)current;
            StrainDecay = Math.Pow(8.0 / 9.0, 1000.0 / Math.Min(osuCurrent.StrainTime, 100.0));
            double strain = Math.Pow(100.0 / osuCurrent.StrainTime, 0.45);
            double repeatVal = 0;

            if (osuCurrent.BaseObject is Slider)
                strain /= 4;

            if (Previous.Count <= 1)
                return strain;

            if (Previous.Count > 1)
            {
                var osuPrevious = (OsuDifficultyHitObject)Previous[0];

                if (Math.Abs(osuCurrent.StrainTime - osuPrevious.StrainTime) > 5.0)
                {
                    if (Math.Abs(Math.Max(50, osuPrevious.StrainTime - osuPrevious.TravelDuration) - Math.Max(50, osuCurrent.StrainTime - osuCurrent.TravelDuration)) > 5.0)
                    {
                        prevRepeatStrainCount = repeatStrainCount;
                        repeatStrainCount = 0;
                    }
                    else
                    {
                        prevRepeatStrainCount = repeatStrainCount + 2;
                        repeatStrainCount = 2;
                    }
                } else
                    repeatStrainCount++;
            }

            repeatVal = 2.0 * Math.Pow(0.6, repeatStrainCount);

            if (repeatStrainCount % 2 == 1)
                return 0;
            else if (repeatStrainCount == prevRepeatStrainCount)
                return strain * repeatVal / 2.0;
            else
                return strain * repeatVal;
        }
    }
}