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

        protected override double SkillMultiplier => 5;
        protected override double StrainDecayBase => Math.Pow(0.99, 20.0);

        protected override double StrainValueOf(DifficultyHitObject current)
        {
            if (current.BaseObject is Spinner)
                return 0;

            var osuCurrent = (OsuDifficultyHitObject)current;

            double changeBonus = 1.0;
            if (Previous.Count > 0)
            {
                var osuPrevious = (OsuDifficultyHitObject)Previous[0];
                changeBonus = Math.Min(Math.Min(osuPrevious.StrainTime / osuCurrent.StrainTime, osuCurrent.StrainTime / osuPrevious.StrainTime), 2.0);
            }
            return changeBonus * Math.Pow(75.0 / osuCurrent.StrainTime, 1.75);
        }
    }
}