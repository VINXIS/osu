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
        protected override double SkillMultiplier => 15000;
        protected override double StrainDecayBase => StrainDecay;
        protected override double StarMultiplierPerRepeat => 1.04;

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
                (1.0 - Math.Pow(1.0 - StrainDecay, Math.Pow(1.0 + osuCurrent.TravelDistance / Math.Max(osuCurrent.TravelTime, 30.0), 3.0))) + 
                Math.Max(30.0, osuCurrent.StrainTime - osuCurrent.TravelTime) / osuCurrent.StrainTime * StrainDecay;
            if (radius == 0) radius = ((OsuHitObject)osuCurrent.BaseObject).Radius;

            test.Add(Tuple.Create(current.BaseObject.StartTime, 0.0));

            double strain = 0;
            double sliderVel = 1.0 + Math.Min(1.0, osuCurrent.TravelDistance / osuCurrent.TravelTime);
            double currVel = osuCurrent.JumpDistance / osuCurrent.StrainTime;

            if (Previous.Count > 0)
            {
                var osuPrevious = (OsuDifficultyHitObject)Previous[0];
                double prevVel = osuPrevious.JumpDistance / osuPrevious.StrainTime;

                if (osuCurrent.Angle != null && osuPrevious.Angle != null)
                {
                    double currTrueVel = currVel * Math.Sin(osuCurrent.Angle.Value / 2.0);
                    double prevTrueVel = prevVel * Math.Sin(osuPrevious.Angle.Value / 2.0);
                    double trueVelDiff = currTrueVel - prevTrueVel;
                    
                    double arcLength = 0;

                    double distanceComparison = osuCurrent.JumpDistance - prevTrueVel * osuCurrent.StrainTime;

                    double constant1 = 6.0 * (trueVelDiff * osuCurrent.StrainTime / 2.0 - distanceComparison) / Math.Pow(osuCurrent.StrainTime, 3.0);
                    double constant2 = 6.0 * (-trueVelDiff * Math.Pow(osuCurrent.StrainTime, 2.0) / 3.0 + distanceComparison * osuCurrent.StrainTime) / Math.Pow(osuCurrent.StrainTime, 3.0);
                    double constantRatio = -constant2 / (2.0 * constant1);

                    double timeFunction = constant1 * Math.Pow(constantRatio, 2.0) + constant2 * constantRatio + prevTrueVel;
                    double chordLength = Math.Sqrt(Math.Pow(osuCurrent.StrainTime, 2.0) + Math.Pow(trueVelDiff, 2.0));
                    double chordDistance = Math.Abs(trueVelDiff * constantRatio - osuCurrent.StrainTime * (timeFunction - prevTrueVel)) / chordLength;
                    
                    if (constant1 != 0)
                    {
                        double pythagorean = Math.Sqrt(Math.Pow(chordLength, 2.0) + 16.0 * Math.Pow(chordDistance, 2.0));
                        arcLength = pythagorean / 2.0 + Math.Pow(chordLength, 2.0) * Math.Log(4.0 + pythagorean / chordDistance) / (8.0 * chordDistance);
                    } else 
                        arcLength = chordLength;

                    test.Add(Tuple.Create(current.BaseObject.StartTime, Math.Log(arcLength / osuCurrent.StrainTime)));
                    strain = trueVelDiff * arcLength / osuCurrent.StrainTime;
                }
            }
            return strain * sliderVel;
        }

        private Vector2 Projection(OsuDifficultyHitObject curr, OsuDifficultyHitObject prev)	
        => Vector2.Multiply(prev.DistanceVector.Normalized(), Vector2.Dot(curr.DistanceVector, prev.DistanceVector.Normalized()));

        private double Det(OsuDifficultyHitObject curr, OsuDifficultyHitObject prev)
        => curr.DistanceVector.X * prev.DistanceVector.Y - curr.DistanceVector.Y * prev.DistanceVector.X;

        private double applyDiminishingExp(double val) => Math.Max(val - radius, 0.0);
    }
}