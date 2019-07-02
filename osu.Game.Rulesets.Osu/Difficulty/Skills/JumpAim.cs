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
    /// Represents the skill required to correctly aim at every object in the map with a uniform CircleSize and normalized distances.
    /// </summary>
    public class JumpAim : OsuSkill
    {
        private double StrainDecay = 0.15;
        private const double angle_bonus_begin = Math.PI / 3.0;
        private const double angle_bonus_end = 5.0 * Math.PI / 6.0;

        private const double pi_over_2 = Math.PI / 2.0;

        protected override double SkillMultiplier => 25;
        protected override double StrainDecayBase => StrainDecay;

        protected override double StrainValueOf(DifficultyHitObject current)
        {
            StrainDecay = 0.15;
            if (current.BaseObject is Spinner)
                return 0;

            var osuCurrent = (OsuDifficultyHitObject)current;
            double angleBonus = 0;

            double currDistance = applyDiminishingExp(osuCurrent.JumpDistance);
            double currDistanceStrain = currDistance / osuCurrent.StrainTime;
            double currTravelStrain = osuCurrent.TravelTime != 0 ? osuCurrent.TravelDistance / osuCurrent.TravelTime : 0;

            if (Previous.Count > 0)
            {
                var osuPrevious = (OsuDifficultyHitObject)Previous[0];
                double prevDistance = applyDiminishingExp(osuPrevious.JumpDistance);
                double prevDistanceStrain = prevDistance / osuPrevious.StrainTime;
                double geoStrain = Math.Sqrt(prevDistanceStrain * currDistanceStrain);
                double minDist = Math.Min(currDistance, prevDistance);
                double jumpNorm = 0;

                if (minDist > 175)
                    jumpNorm = 1.0;
                else if (minDist > 125)
                    jumpNorm = Math.Pow(Math.Sin(Math.PI * minDist / 100 - 9.0 * Math.PI / 4.0), 2.0);

                if (osuCurrent.Angle != null)
                {
                    if (osuCurrent.Angle.Value > angle_bonus_end)
                        angleBonus = geoStrain;
                    else if (osuCurrent.Angle.Value > angle_bonus_begin)
                        angleBonus = geoStrain * Math.Pow(Math.Sin(osuCurrent.Angle.Value - angle_bonus_begin), 2.0);
                }

                angleBonus *= jumpNorm;
            }

            if (osuCurrent.BaseObject is Slider) StrainDecay = Math.Min(osuCurrent.TravelTime, osuCurrent.StrainTime - 30.0) / osuCurrent.StrainTime * 
                (1.0 - Math.Pow(1.0 - StrainDecay, Math.Pow(2.0 + osuCurrent.TravelDistance / Math.Max(osuCurrent.TravelTime, 30.0), 3.0))) + 
                Math.Max(30.0, osuCurrent.StrainTime - osuCurrent.TravelTime) / osuCurrent.StrainTime * StrainDecay;

            return currDistanceStrain + currTravelStrain + Math.Sqrt(currTravelStrain * currDistanceStrain) + angleBonus;
        }

        private double applyDiminishingExp(double val) => Math.Max(val - 45.0, 0.0);
    }
}