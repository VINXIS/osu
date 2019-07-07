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
    /// 
    /// </summary>
    public class AimControl : OsuSkill
    {
        private double StrainDecay = 0.225;
        protected override double SkillMultiplier => 20;
        protected override double StrainDecayBase => StrainDecay;
        protected override double StarMultiplierPerRepeat => 1.07;

        private double pi_over_root_2 = Math.PI / Math.Sqrt(2.0);
        private const double pi_over_2 = Math.PI / 2.0;
        private const double pi_over_4 = Math.PI / 4.0;
        private double radius;

        protected override double StrainValueOf(DifficultyHitObject current)
        {
            StrainDecay = 0.225;

            if (current.BaseObject is Spinner)
                return 0;

            var osuCurrent = (OsuDifficultyHitObject)current;
            if (osuCurrent.BaseObject is Slider && osuCurrent.TravelTime < osuCurrent.StrainTime) StrainDecay = Math.Min(osuCurrent.TravelTime, osuCurrent.StrainTime - 30.0) / osuCurrent.StrainTime * 
                (1.0 - Math.Pow(1.0 - StrainDecay, Math.Pow(2.0 + osuCurrent.TravelDistance / Math.Max(osuCurrent.TravelTime, 30.0), 3.0))) + 
                Math.Max(30.0, osuCurrent.StrainTime - osuCurrent.TravelTime) / osuCurrent.StrainTime * StrainDecay;
            if (radius == 0) radius = ((OsuHitObject)osuCurrent.BaseObject).Radius;

            double strain = 0;
            double jumpNorm = 0;

            if (Previous.Count > 1 && osuCurrent.ArcLength != null)
            {
                var osuPrevious = (OsuDifficultyHitObject)Previous[0];
                
                double flowStrain = osuCurrent.JumpDistance / (osuCurrent.StrainTime - 35);
                double snapStrain = osuCurrent.ArcLength.Value / osuCurrent.StrainTime;

                double minDist = Math.Min(osuCurrent.JumpDistance, osuPrevious.JumpDistance);

                if (minDist > 150)
                    jumpNorm = 1.0;
                else 
                    jumpNorm = Math.Pow(Math.Sin(pi_over_2 * minDist / 150), 2.0);

                if (snapStrain < flowStrain)
                    strain = 1.5 * snapStrain;
                if (snapStrain > flowStrain)
                    strain = 1.5 * flowStrain;

                area.Add(Tuple.Create(osuCurrent.BaseObject.StartTime, flowStrain));
            }
            return jumpNorm * strain;
        }

        private Vector2 Projection(OsuDifficultyHitObject curr, OsuDifficultyHitObject prev)
        => Vector2.Multiply(prev.DistanceVector.Normalized(), Vector2.Dot(curr.DistanceVector, prev.DistanceVector.Normalized()));

        private double Det(OsuDifficultyHitObject curr, OsuDifficultyHitObject prev)
        => curr.DistanceVector.X * prev.DistanceVector.Y - curr.DistanceVector.Y * prev.DistanceVector.X;

        private double applyDiminishingExp(double val) => Math.Max(val - radius, 0.0);

        private double applySinTransformation(double val) => Math.Pow(Math.Sin(pi_over_2 * val), 2.0);
    }
}