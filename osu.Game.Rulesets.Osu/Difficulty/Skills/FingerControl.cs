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
        protected override double SkillMultiplier => 75.0;
        protected override double StrainDecayBase => 0.1;

        private int switchCheck = 1;
        private double switchStrain;

        protected override double StrainValueOf(DifficultyHitObject current)
        {
            if (current.BaseObject is Spinner)
                return 0;
            
            var osuCurrent = (OsuDifficultyHitObject)current;
            double switchVal = 0;
            double strainScale = 0;
            
            // Assign value for first iteration/object
            if (switchStrain == 0)
                switchStrain = osuCurrent.StrainTime;

            if (Previous.Count > 0)
            {
                var osuPrevious = (OsuDifficultyHitObject)Previous[0];

                // Get the max straintime as well as create a scale for decreasing the value of slower objects such as in normal difficulties
                double maxStrain = Math.Max(osuPrevious.StrainTime, osuCurrent.StrainTime);
                strainScale = Math.Pow(Math.Sin((Math.PI / 2.0) * Math.Min(1.0, 96.0 / maxStrain)), 2.0);

                // Switch sum assignment, checks for how many times the specific straintime has appeared in the map and scales accordingly
                if (Math.Abs(osuCurrent.StrainTime - switchStrain) < 4.0)
                    switchCheck++;
                else
                {
                    switchVal = (1.0 + (switchCheck % 2)) / Math.Sqrt(switchCheck);
                    if (osuCurrent.BaseObject is Slider || osuPrevious.BaseObject is Slider) switchVal /= 5.0;
                    if (switchCheck == 1) switchCheck += 3;
                    else switchCheck = switchCheck % 2 == 1 ? 1 + switchCheck : 1; // To combat repetitive rhythms
                    switchStrain = osuCurrent.StrainTime;
                }
            }

            return strainScale * switchVal;
        }
    }
}