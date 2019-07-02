// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
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
        protected override double SkillMultiplier => 20.0;
        protected override double StrainDecayBase => StrainDecay;

        protected override double StrainValueOf(DifficultyHitObject current)
        {
            if (current.BaseObject is Spinner)
                return 0;

            var osuCurrent = (OsuDifficultyHitObject)current;
            double strainTime = Math.Max(osuCurrent.DeltaTime, 37.5);

            StrainDecay = Math.Pow(7.0 / 8.0, 1000.0 / Math.Min(strainTime, 200.0));

            if (strainTime < 75.0)
                return Math.Pow(75.0 / strainTime, 1.75);
            else 
                return 75.0 / strainTime;
        }
    }
}