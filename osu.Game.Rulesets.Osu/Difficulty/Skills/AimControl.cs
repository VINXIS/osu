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
        private double StrainDecay = 0.45;
        protected override double SkillMultiplier => 10500;
        protected override double StrainDecayBase => StrainDecay;
        protected override double StarMultiplierPerRepeat => 1.03;

        private const double pi_over_2 = Math.PI / 2.0;
        private const double pi_over_4 = Math.PI / 4.0;
        private const double valThresh = 0.1;
        private double radius;

        protected override double StrainValueOf(DifficultyHitObject current)
        {
            StrainDecay = 0.45;

            if (current.BaseObject is Spinner)
                return 0;

            var osuCurrent = (OsuDifficultyHitObject)current;
            if (osuCurrent.BaseObject is Slider && osuCurrent.TravelTime < osuCurrent.StrainTime) StrainDecay = Math.Min(osuCurrent.TravelTime, osuCurrent.StrainTime - 30.0) / osuCurrent.StrainTime * 
                (1.0 - Math.Pow(1.0 - StrainDecay, Math.Pow(2.0 + osuCurrent.TravelDistance / Math.Max(osuCurrent.TravelTime, 30.0), 3.0))) + 
                Math.Max(30.0, osuCurrent.StrainTime - osuCurrent.TravelTime) / osuCurrent.StrainTime * StrainDecay;
            if (radius == 0) radius = ((OsuHitObject)osuCurrent.BaseObject).Radius;

            test.Add(Tuple.Create(current.BaseObject.StartTime, 0.0));

            double strain = 0;
            double sliderVel = 1.0 + Math.Min(1.0, osuCurrent.TravelDistance / osuCurrent.TravelTime);

            if (Previous.Count > 1)
            {
                var osuPrevious = (OsuDifficultyHitObject)Previous[0];
                var osuPrevPrevious = (OsuDifficultyHitObject)Previous[1];
                double distScale = 1.0;
                double jumpAwk = 0;
                double angleBonus = 1.0;
                double flowBonus = 1.0;

                double currDistance = applyDiminishingExp(osuCurrent.JumpDistance + osuCurrent.TravelDistance);
                double prevDistance = applyDiminishingExp(osuPrevious.JumpDistance + osuPrevious.TravelDistance);
                
                double diffDist = Math.Abs(currDistance - prevDistance);
                double maxDist = Math.Max(Math.Max(currDistance, prevDistance), valThresh);
                double minDist = Math.Max(Math.Min(currDistance, prevDistance), valThresh);
                
                if (minDist < 150) distScale = applySinTransformation(minDist / 150);
                angleBonus += Math.Pow(Math.Sin(osuCurrent.Angle.Value / 2.0), 2.0);
                jumpAwk = diffDist / maxDist;
                if (Det(osuCurrent, osuPrevious) * Det(osuPrevious, osuPrevPrevious) < 0)
                    flowBonus += ((osuCurrent.DistanceVector + osuPrevious.DistanceVector).Length / Math.Max(osuCurrent.JumpDistance, osuPrevious.JumpDistance)) / 2.0;

                test.Add(Tuple.Create(current.BaseObject.StartTime, jumpAwk));
                strain = distScale * jumpAwk * flowBonus / Math.Max(osuCurrent.StrainTime, osuPrevious.StrainTime);
            }
            return strain * sliderVel;
        }

        private Vector2 Projection(OsuDifficultyHitObject curr, OsuDifficultyHitObject prev)	
        => Vector2.Multiply(prev.DistanceVector.Normalized(), Vector2.Dot(curr.DistanceVector, prev.DistanceVector.Normalized()));

        private double Det(OsuDifficultyHitObject curr, OsuDifficultyHitObject prev)
        => curr.DistanceVector.X * prev.DistanceVector.Y - curr.DistanceVector.Y * prev.DistanceVector.X;

        private double applyDiminishingExp(double val) => Math.Max(val - radius, 0.0);

        private double applySinTransformation(double val) => Math.Pow(Math.Sin(pi_over_2 * val), 2.0);
    }
}