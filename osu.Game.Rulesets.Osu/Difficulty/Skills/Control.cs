// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// 
    /// </summary>
    public class Control : OsuSkill
    {
        protected override double SkillMultiplier => 40;
        protected override double StrainDecayBase => 0.35;

        protected override double StrainValueOf(DifficultyHitObject current)
        {
            if (current.BaseObject is Spinner)
                return 0;

            var osuCurrent = (OsuDifficultyHitObject)current;

            double currVel = osuCurrent.JumpDistance / osuCurrent.StrainTime;
            double sliderVel = osuCurrent.TravelDistance / osuCurrent.TravelTime;
            double jumpAwk = 0;
            double angleAwk = 0;
            double jumpNorm = 0;
            double angleBonus = 0;
            
            if (osuCurrent.JumpDistance > 125)
                jumpNorm = 1.0;
            else 
                jumpNorm = Math.Pow(Math.Sin((Math.PI / 2.0) * (osuCurrent.JumpDistance / 125)), 2.0);

            if (Previous.Count > 0)
            {
                var osuPrevious = (OsuDifficultyHitObject)Previous[0];
                
                double prevVel = osuPrevious.JumpDistance / osuPrevious.StrainTime;
                double geoVel = Math.Sqrt(prevVel * currVel);

                if (osuCurrent.Angle != null && osuPrevious.Angle != null)
                    angleAwk = (Math.Min(prevVel, currVel) / Math.Max(geoVel, 0.75)) * Math.Pow(Math.Sin((osuCurrent.Angle.Value - osuPrevious.Angle.Value) / 1.5), 2.0);
                
                if (osuCurrent.Angle != null && osuCurrent.Angle.Value > Math.PI / 4.0 && osuCurrent.Angle.Value < 3.0 * Math.PI / 4.0)
                    angleBonus = (Math.Min(prevVel, currVel) / Math.Max(geoVel, 0.75)) * Math.Pow(Math.Sin(osuCurrent.Angle.Value - (Math.PI / 4.0)), 2.0);

                if (Math.Abs(currVel - prevVel) > Math.Max(geoVel, 0.75))
                    jumpAwk = 1.0;
                else 
                    jumpAwk = Math.Pow(Math.Sin((Math.PI / 2.0) * (Math.Abs(currVel - prevVel) / Math.Max(geoVel, 0.75))), 2.0);
            }

            if (currVel > 1.0)
                return Math.Sqrt(currVel) * (Math.Max(angleAwk, jumpAwk) + angleBonus + sliderVel + angleAwk * jumpAwk) * jumpNorm;
            else
                return currVel * (Math.Max(angleAwk, jumpAwk) + angleBonus + sliderVel + angleAwk * jumpAwk) * jumpNorm;
        }
    }
}