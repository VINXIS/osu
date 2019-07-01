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
            double sliderVel = Math.Min(1.0, osuCurrent.TravelDistance / osuCurrent.TravelTime);
            double normedVel = 0;
            double jumpAwk = 0;
            double angleAwk = 0;
            double jumpNorm = 0;
            double angleBonus = 0;
            double flowBonus = 0;
            
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

                if (Math.Pow(Math.Abs(currVel - prevVel), 2.0) > Math.Max(1.0, Math.Min(currVel, prevVel)))
                    jumpAwk = 1.0;
                else 
                    jumpAwk = Math.Pow(Math.Sin((Math.PI / 2.0) * (Math.Pow(Math.Abs(currVel - prevVel), 2.0) / Math.Max(1.0, Math.Min(currVel, prevVel)))), 2.0);

                if (osuPrevious.NormedDet * osuCurrent.NormedDet < 0)
                    flowBonus = ((osuCurrent.DistanceVector + osuPrevious.DistanceVector).Length / Math.Max(osuCurrent.JumpDistance, osuPrevious.JumpDistance)) / 2.0;

                normedVel = Math.Min(currVel, prevVel) > 1.0 ? Math.Sqrt(Math.Min(currVel, prevVel)) : Math.Min(currVel, prevVel);
            }

            return normedVel * (Math.Max(angleAwk, jumpAwk) + angleBonus + flowBonus + sliderVel + angleAwk * jumpAwk) * jumpNorm;
        }
    }
}