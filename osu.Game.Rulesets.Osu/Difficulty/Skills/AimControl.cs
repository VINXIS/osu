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
        protected override double SkillMultiplier => 100;
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

            double currDistance = applyDiminishingExp(osuCurrent.JumpDistance + osuCurrent.TravelDistance);
            double currVel = currDistance / osuCurrent.StrainTime;
            double flowChange = 0;
            if (Previous.Count > 1)
            {
                var osuPrevious = (OsuDifficultyHitObject)Previous[0];
                var osuPrevPrevious = (OsuDifficultyHitObject)Previous[1];

                if (osuCurrent.JumpDistance != 0 && osuPrevPrevious.JumpDistance != 0 && osuPrevious.JumpDistance != 0)
                {
                    double currFlow = Projection(osuCurrent, osuPrevious).Length / osuCurrent.JumpDistance;
                    double prevFlow = Projection(osuPrevious, osuPrevPrevious).Length / osuPrevious.JumpDistance;

                    flowChange = Math.Pow(currFlow - prevFlow, 2.0);
                    area.Add(Tuple.Create(osuCurrent.BaseObject.StartTime, flowChange));
                }
            }
            return currVel * flowChange;
        }

        private Vector2 Projection(OsuDifficultyHitObject curr, OsuDifficultyHitObject prev)
        => Vector2.Multiply(prev.DistanceVector.Normalized(), Vector2.Dot(curr.DistanceVector, prev.DistanceVector.Normalized()));

        private double Det(OsuDifficultyHitObject curr, OsuDifficultyHitObject prev)
        => curr.DistanceVector.X * prev.DistanceVector.Y - curr.DistanceVector.Y * prev.DistanceVector.X;

        private double applyDiminishingExp(double val) => Math.Max(val - radius, 0.0);

        private double applySinTransformation(double val) => Math.Pow(Math.Sin(pi_over_2 * val), 2.0);
    }
}