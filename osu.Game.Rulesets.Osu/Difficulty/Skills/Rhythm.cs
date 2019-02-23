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
    /// Represents the skill required to adjust your movement to varying spacing.
    /// </summary>
    public class Rhythm : Skill
    {
        protected override double SkillMultiplier => 10000;
        protected override double StrainDecayBase => 0.3;
        protected override double StrainValueOf(DifficultyHitObject current)
        {
            if (current.BaseObject is Spinner)
                return 0;

            if (Previous.Count > 0) 
            {
                var osuCurrent = (OsuDifficultyHitObject)current;
                var osuPrevious = (OsuDifficultyHitObject)Previous[0];

                // Time calc
                double currMinStrain = Math.Min(osuCurrent.StrainTime, osuPrevious.StrainTime);
                double currMaxStrain = Math.Max(osuCurrent.StrainTime, osuPrevious.StrainTime);
                double currMod = currMaxStrain % currMinStrain;
                double currRoot1 = currMaxStrain - currMod;
                double currRoot2 = currMaxStrain + currMinStrain - currMod;
                double timeChange = Math.Pow(- 4.0 * (currMaxStrain - currRoot1) * (currMaxStrain - currRoot2) / Math.Pow(currMinStrain, 2.0), 7.0);

                double timeResult = Math.Pow(Math.Min(timeMultiplier(osuCurrent), timeMultiplier(osuPrevious)), 2.0) * timeChange;

                return timeResult / Math.Min(osuCurrent.StrainTime, osuPrevious.StrainTime);            
            } else return 0;
        }
    }
}