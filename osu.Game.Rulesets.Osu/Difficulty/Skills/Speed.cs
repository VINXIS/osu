// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// idk something lol
    /// </summary>
    public class Speed : OsuSkill
    {
        private double StrainDecay = 1.0;
        protected override double SkillMultiplier => 54;
        protected override double StrainDecayBase => StrainDecay;
        protected override double StarMultiplierPerRepeat => 1.04;

        private const double quarter240 = 60000 / (4 * 240);
        private int repeatStrainCount = 1;

        protected override double StrainValueOf(DifficultyHitObject current)
        {
            if (current.BaseObject is Spinner)
                return 0;

            var osuCurrent = (OsuDifficultyHitObject)current;
            double strainTime = Math.Max(osuCurrent.DeltaTime, 37.5);

            StrainDecay = Math.Pow(5.0 / 6.0, 1000.0 / Math.Min(strainTime, 200.0));

            double strain = Math.Pow(quarter240 / strainTime, 2.0);
            if (osuCurrent.BaseObject is Slider) strain /= 4;

            if (Previous.Count > 0)
            {
                var osuPrevious = (OsuDifficultyHitObject)Previous[0];

                if (Math.Abs(osuCurrent.StrainTime - osuPrevious.StrainTime) > 4.0) repeatStrainCount = 1;
                else repeatStrainCount++;
            }

            return strain / Math.Pow(repeatStrainCount, 0.25);
        }
    }
}