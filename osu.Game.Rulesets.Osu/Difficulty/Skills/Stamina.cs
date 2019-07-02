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
    /// Represents the difficulty required to keep up with the note density and speed at which objects are needed to be tapped along with
    /// </summary>
    public class Stamina : OsuSkill
    {
        private double StrainDecay = 1.0;
        protected override double SkillMultiplier => 3;
        protected override double StrainDecayBase => StrainDecay;

        protected override double StrainValueOf(DifficultyHitObject current)
        {
            if (current.BaseObject is Spinner)
                return 0;

            var osuCurrent = (OsuDifficultyHitObject)current;

            StrainDecay = Math.Pow(63.0 / 64.0, 1000.0 / Math.Min(osuCurrent.StrainTime, 200.0));

            if (osuCurrent.StrainTime < 75) return Math.Pow(75.0 / osuCurrent.StrainTime, 1.5);
            else return 75.0 / osuCurrent.StrainTime;
        }
    }
}