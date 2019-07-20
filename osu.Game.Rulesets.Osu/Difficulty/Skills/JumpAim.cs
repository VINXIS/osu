// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

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

        protected override double SkillMultiplier => 35;
        protected override double StrainDecayBase => StrainDecay;
        protected override double StarMultiplierPerRepeat => 1.04;

        private double radius;

        protected override double StrainValueOf(DifficultyHitObject current)
        {
            StrainDecay = 0.15;

            if (current.BaseObject is Spinner)
                return 0;

            var osuCurrent = (OsuDifficultyHitObject)current;
            if (osuCurrent.BaseObject is Slider && osuCurrent.TravelTime < osuCurrent.StrainTime) StrainDecay = Math.Min(osuCurrent.TravelTime, osuCurrent.StrainTime - 30.0) / osuCurrent.StrainTime * 
                (1.0 - Math.Pow(1.0 - StrainDecay, Math.Pow(1.0 + osuCurrent.TravelDistance / Math.Max(osuCurrent.TravelTime, 30.0), 3.0))) + 
                Math.Max(30.0, osuCurrent.StrainTime - osuCurrent.TravelTime) / osuCurrent.StrainTime * StrainDecay;
            if (radius == 0) radius = ((OsuHitObject)osuCurrent.BaseObject).Radius;

            double strain = 0;
            if (osuCurrent.JumpDistance >= 90)
                strain = (applyDiminishingDist(osuCurrent.DistanceVector).Length + osuCurrent.TravelDistance) / osuCurrent.StrainTime;

            if (Previous.Count > 0)
            {
                var osuPrevious = (OsuDifficultyHitObject)Previous[0];
                if (osuCurrent.JumpDistance >= 90 && osuPrevious.JumpDistance >= 90)
                    strain = ((applyDiminishingDist(osuCurrent.DistanceVector) + 0.5f * applyDiminishingDist(osuPrevious.DistanceVector)).Length + osuCurrent.TravelDistance) / Math.Max(osuCurrent.StrainTime, osuPrevious.StrainTime);
            }

            return strain;
        }

        private Vector2 applyDiminishingDist(Vector2 val) => val - 90.0f * val.Normalized();
    }
}