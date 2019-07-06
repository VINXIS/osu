// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// 
    /// </summary>
    public class AimControl : OsuSkill
    {
        private double StrainDecay = 0.9;
        protected override double SkillMultiplier => 3;
        protected override double StrainDecayBase => StrainDecay;
        protected override double StarMultiplierPerRepeat => 1.03;

        private const double pi_over_2 = Math.PI / 2.0;
        private const double pi_over_4 = Math.PI / 4.0;

        protected override double StrainValueOf(DifficultyHitObject current)
        {
            StrainDecay = 0.9;

            if (current.BaseObject is Spinner)
                return 0;

            var osuCurrent = (OsuDifficultyHitObject)current;
            if (osuCurrent.BaseObject is Slider && osuCurrent.TravelTime < osuCurrent.StrainTime) StrainDecay = Math.Min(osuCurrent.TravelTime, osuCurrent.StrainTime - 30.0) / osuCurrent.StrainTime * 
                (1.0 - Math.Pow(1.0 - StrainDecay, Math.Pow(2.0 + osuCurrent.TravelDistance / Math.Max(osuCurrent.TravelTime, 30.0), 3.0))) + 
                Math.Max(30.0, osuCurrent.StrainTime - osuCurrent.TravelTime) / osuCurrent.StrainTime * StrainDecay;

            double currVel = osuCurrent.JumpDistance / osuCurrent.StrainTime;
            double currLazyVel = osuCurrent.JumpDistance / Math.Max(50, osuCurrent.StrainTime - osuCurrent.TravelTime);
            double currEndVel = osuCurrent.EndJumpDistance / Math.Max(50, osuCurrent.StrainTime - osuCurrent.TravelDuration);
            double sliderVel = osuCurrent.TravelTime != 0 ? Math.Min(1.0, osuCurrent.TravelDistance / osuCurrent.TravelTime) : 0;

            double minVel = currLazyVel != 0 ? Math.Min(currVel, Math.Min(currLazyVel, currEndVel)) : Math.Min(currVel, currEndVel);

            double jumpAwk = 0;
            double jumpNorm = 0;
            double angleBonus = 0;
            double angleAwk = 0;
            double flowBonus = 0;

            if (Previous.Count > 0)
            {
                var osuPrevious = (OsuDifficultyHitObject)Previous[0];
                
                double prevVel = osuPrevious.JumpDistance / osuPrevious.StrainTime;
                double prevLazyVel = osuPrevious.JumpDistance / Math.Max(50, osuPrevious.StrainTime - osuPrevious.TravelTime);
                double prevEndVel = osuPrevious.EndJumpDistance / Math.Max(50, osuPrevious.StrainTime - osuPrevious.TravelDuration);

                double minDist = Math.Min(osuCurrent.TravelDistance + osuCurrent.JumpDistance, osuPrevious.TravelDistance + osuPrevious.JumpDistance);

                double geoVel = Math.Sqrt(currVel * prevVel);
                double geoLazyVel = Math.Sqrt(currLazyVel * prevLazyVel);
                double geoEndVel = Math.Sqrt(currEndVel * prevEndVel);

                double jumpAwk1 = 0;
                double jumpAwk2 = 0;
                double jumpAwk3 = 0;

                if (minDist > 150)
                    jumpNorm = 1.0;
                else 
                    jumpNorm = Math.Pow(Math.Sin(pi_over_2 * minDist / 150), 2.0);

                if (Math.Pow(currVel - prevVel, 2.0) >= Math.Max(geoVel, 0.75))
                    jumpAwk1 = 1.0;
                else 
                    jumpAwk1 = Math.Pow(Math.Sin(pi_over_2 * (Math.Pow(currVel - prevVel, 2.0) / Math.Max(geoVel, 0.75))), 2.0);

                if (Math.Pow(currEndVel - prevEndVel, 2.0) >= Math.Max(geoEndVel, 0.75))
                    jumpAwk2 = 1.0;
                else 
                    jumpAwk2 = Math.Pow(Math.Sin(pi_over_2 * (Math.Pow(currEndVel - prevEndVel, 2.0) / Math.Max(geoEndVel, 0.75))), 2.0);

                if (currLazyVel > 0 || prevLazyVel > 0)
                {
                    if (Math.Pow(currLazyVel - prevLazyVel, 2.0) >= Math.Max(geoLazyVel, 0.75))
                        jumpAwk3 = 1.0;
                    else 
                        jumpAwk3 = Math.Pow(Math.Sin(pi_over_2 * (Math.Pow(currLazyVel - prevLazyVel, 2.0) / Math.Max(geoLazyVel, 0.75))), 2.0);
                } else
                    jumpAwk3 = jumpAwk2;

                jumpAwk = Math.Min(jumpAwk1, Math.Min(jumpAwk2, jumpAwk3));

                if (osuCurrent.Angle != null && osuPrevious.Angle != null)
                {
                    angleAwk = Math.Pow(Math.Sin((osuCurrent.Angle.Value - osuPrevious.Angle.Value) / 1.5), 2.0);

                    double averageAngle = (osuCurrent.Angle.Value + osuPrevious.Angle.Value) / 2.0;

                    if (averageAngle > pi_over_4 && averageAngle < 3.0 * pi_over_4)
                        angleBonus = Math.Pow(Math.Sin(averageAngle - pi_over_4), 2.0);
                    else if (averageAngle >= 3.0 * pi_over_4)
                        angleBonus = 1.0;
                }

                if (osuPrevious.NormedDet * osuCurrent.NormedDet < 0)
                    flowBonus = ((osuCurrent.DistanceVector + osuPrevious.DistanceVector).Length / Math.Max(osuCurrent.JumpDistance, osuPrevious.JumpDistance)) / 2.0;
            }

            return minVel * jumpNorm * (jumpAwk + angleAwk + flowBonus + sliderVel + jumpAwk * (angleAwk + flowBonus + angleBonus));
        }
    }
}