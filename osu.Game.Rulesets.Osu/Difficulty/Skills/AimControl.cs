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
        protected override double SkillMultiplier => 200;
        protected override double StrainDecayBase => StrainDecay;
        protected override double StarMultiplierPerRepeat => 1.07;
        private const double pi_over_2 = Math.PI / 2.0;
        private const double pi_over_4 = Math.PI / 4.0;

        private const double geoMeanThresh = 0.75;
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

            double currDistance = applyDiminishingExp(osuCurrent.JumpDistance + osuCurrent.TravelDistance);

            double strain = 0;
            double jumpNorm = 0;
            double jumpAwk = 0;
            double angleAwk = 0;
            double angleThresh = 0;
            double flowBonus = 1.0;
            double sliderVel = 1.0 + Math.Min(1.0, osuCurrent.TravelDistance / osuCurrent.TravelTime);

            if (Previous.Count > 1)
            {
                test.Add(Tuple.Create(current.BaseObject.StartTime, 0.0));
                var osuPrevious = (OsuDifficultyHitObject)Previous[0];
                var osuPrevPrevious = (OsuDifficultyHitObject)Previous[1];

                double prevDistance = applyDiminishingExp(osuPrevious.JumpDistance + osuPrevious.TravelDistance);
                double diffDist = Math.Pow(currDistance - prevDistance, 2.0);
                double minDist = Math.Min(currDistance, prevDistance);
                double geoDist = Math.Max(Math.Sqrt(currDistance * prevDistance), geoMeanThresh);
                
                if (minDist > 150)
                    jumpNorm = 1.0;
                else 
                    jumpNorm = applySinTransformation(minDist / 150); // Less value for stacked objects
                
                double flowChange = 0;
                if (osuCurrent.JumpDistance != 0 && osuPrevious.JumpDistance != 0)
                    flowChange = Projection(osuCurrent, osuPrevious).Length / osuCurrent.JumpDistance;

                if (diffDist < geoDist)
                    flowChange *= applySinTransformation(diffDist / geoDist); // Check for distance change

                jumpAwk = flowChange;

                if (osuCurrent.Angle != null && osuPrevious.Angle != null)
                {
                    double minAngle = Math.Min(osuCurrent.Angle.Value, osuPrevious.Angle.Value);
                    double averageAngle = (osuCurrent.Angle.Value + osuPrevious.Angle.Value) / 2.0;

                    angleAwk = Math.Sin(minAngle) * Math.Pow(Math.Sin((osuCurrent.Angle.Value - osuPrevious.Angle.Value) / 1.5), 2.0); // Higher value for changing angles

                    if (averageAngle > pi_over_4 && averageAngle < 3.0 * pi_over_4)
                        angleThresh = Math.Pow(Math.Sin(averageAngle - pi_over_4), 2.0);
                    else if (averageAngle >= 3.0 * pi_over_4)
                        angleThresh = 1.0;
                }
                
                double currArea = Det(osuCurrent, osuPrevious);
                double prevArea = Det(osuPrevious, osuPrevPrevious);
                if (currArea * prevArea < 0)
                    flowBonus += ((osuCurrent.DistanceVector + osuPrevious.DistanceVector).Length / Math.Max(osuCurrent.JumpDistance, osuPrevious.JumpDistance)) / 2.0; // Bonus for when jumps change direction

                strain = currDistance / Math.Max(osuCurrent.StrainTime, osuPrevious.StrainTime);
            }
            test.Add(Tuple.Create(current.BaseObject.StartTime, angleThresh));
            return strain * jumpNorm * jumpAwk * angleAwk * angleThresh * sliderVel;
        }
        private Vector2 Projection(OsuDifficultyHitObject curr, OsuDifficultyHitObject prev)	
        => Vector2.Multiply(prev.DistanceVector.Normalized(), Vector2.Dot(curr.DistanceVector, prev.DistanceVector.Normalized()));	

        private double Det(OsuDifficultyHitObject curr, OsuDifficultyHitObject prev)
        => curr.DistanceVector.X * prev.DistanceVector.Y - curr.DistanceVector.Y * prev.DistanceVector.X;

        private double applyDiminishingExp(double val) => Math.Max(val - radius, 0.0);

        private double applySinTransformation(double val) => Math.Pow(Math.Sin(pi_over_2 * val), 2.0);
    }
}