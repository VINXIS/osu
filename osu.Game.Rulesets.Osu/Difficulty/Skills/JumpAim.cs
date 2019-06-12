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
    public class JumpAim : Skill
    {
        private const double angle_bonus_begin = Math.PI / 3.0;
        private const double angle_bonus_end = 5.0 * Math.PI / 6.0;
        private const double square_bonus_begin = Math.PI / 3.0;
        private const double square_bonus_end = 2.0 * Math.PI / 3.0;
        private const double almostRadius = 90;

        protected override double SkillMultiplier => 30;
        protected override double StrainDecayBase => 0.15;

        protected override double StrainValueOf(DifficultyHitObject current)
        {
            if (current.BaseObject is Spinner)
                return 0;

            var osuCurrent = (OsuDifficultyHitObject)current;
            double angleBonus = 0;
            double squareBonus = 0;
            double decelBonus = 1;

            double currDistance = applyDiminishingExp(osuCurrent.JumpDistance);
            double currTravelStrain = osuCurrent.TravelDistance / Math.Max(osuCurrent.TravelTime, 50);
            double prevTravelStrain = 0;

            if (Previous.Count > 0)
            {
                var osuPrevious = (OsuDifficultyHitObject)Previous[0];
                prevTravelStrain = osuPrevious.TravelDistance / Math.Max(osuPrevious.TravelTime, 50);
                double prevDistance = applyDiminishingExp(osuPrevious.JumpDistance);

                double geoStrain = Math.Sqrt((prevDistance / osuPrevious.StrainTime) *
                            (currDistance / osuCurrent.StrainTime));

                if (osuPrevious.Angle != null)
                {
                    
                    if (osuPrevious.Angle.Value > angle_bonus_end)
                        angleBonus = geoStrain;
                    else if (osuPrevious.Angle.Value > angle_bonus_begin)
                        angleBonus = geoStrain * Math.Pow(Math.Sin(osuPrevious.Angle.Value - angle_bonus_begin), 2.0);
                    if (osuCurrent.Angle != null && osuCurrent.Angle.Value < square_bonus_begin && osuCurrent.Angle.Value > square_bonus_end && osuPrevious.Angle.Value < square_bonus_begin && osuPrevious.Angle.Value > square_bonus_end)
                        squareBonus = geoStrain * Math.Pow(Math.Sin(angle_bonus_begin - osuCurrent.Angle.Value), 2.0) * Math.Pow(Math.Sin(angle_bonus_begin - osuPrevious.Angle.Value), 2.0);
                }
                if (osuCurrent.JumpDistance / osuCurrent.StrainTime < osuPrevious.JumpDistance / osuPrevious.StrainTime)
                    decelBonus = Math.Min((osuPrevious.JumpDistance / osuPrevious.StrainTime) / (osuCurrent.JumpDistance / osuCurrent.StrainTime), 4);
            }

            double currDistanceStrain = decelBonus * (currDistance / osuCurrent.StrainTime);

            return currDistanceStrain + prevTravelStrain + Math.Sqrt(prevTravelStrain * currDistanceStrain) + angleBonus + squareBonus;
        }

        private double applyDiminishingExp(double val) => Math.Max(val - almostRadius / 2.0, 0.0);
    }
}