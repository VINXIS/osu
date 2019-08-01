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
        private double StrainDecay = 0.25;
        protected override double SkillMultiplier => 525;
        protected override double StrainDecayBase => StrainDecay;
        protected override double StarMultiplierPerRepeat => 1.1;

        private const double pi_over_2 = Math.PI / 2.0;
        private const double pi_over_4 = Math.PI / 4.0;
        private const double angle_stretch = 3.0 / 4.0;
        private const double valThresh = 150;
        private const double angleWeight = 0.1;
        private double radius;

        protected override double StrainValueOf(DifficultyHitObject current)
        {
            StrainDecay = 0.25;

            if (current.BaseObject is Spinner)
                return 0;

            var osuCurrent = (OsuDifficultyHitObject)current;
            if (osuCurrent.BaseObject is Slider && osuCurrent.TravelTime < osuCurrent.StrainTime) StrainDecay = Math.Min(osuCurrent.TravelTime, osuCurrent.StrainTime - 30.0) / osuCurrent.StrainTime * 
                (1.0 - Math.Pow(1.0 - StrainDecay, Math.Pow(1.0 + osuCurrent.TravelDistance / Math.Max(osuCurrent.TravelTime, 30.0), 3.0))) + 
                Math.Max(30.0, osuCurrent.StrainTime - osuCurrent.TravelTime) / osuCurrent.StrainTime * StrainDecay;
            if (radius == 0) radius = ((OsuHitObject)osuCurrent.BaseObject).Radius;

            test.Add(Tuple.Create(current.BaseObject.StartTime, 0.0));

            double strain = 0;
            double velScale = 0;
            double sliderVel = 1.0 + osuCurrent.TravelDistance / osuCurrent.TravelTime + Math.Sqrt(osuCurrent.JumpDistance * osuCurrent.TravelDistance) / osuCurrent.StrainTime;

            if (Previous.Count > 0)
            {
                var osuPrevious = (OsuDifficultyHitObject)Previous[0];
                double awkVal = 0;
                double angleScale = angleWeight;
                double strainScale = 0;

                double maxTime = Math.Max(osuCurrent.StrainTime, osuPrevious.StrainTime);
                double minTime = Math.Min(osuCurrent.StrainTime, osuPrevious.StrainTime);

                if (osuCurrent.Angle != null)
                {
                    double currDistance = applyDiminishingExp(osuCurrent.JumpDistance + osuCurrent.TravelDistance);
                    double prevDistance = applyDiminishingExp(osuPrevious.JumpDistance + osuPrevious.TravelDistance);

                    double currVel = currDistance / osuCurrent.StrainTime;
                    double prevVel = prevDistance / osuPrevious.StrainTime;
                    
                    double diffDist = Math.Abs(currDistance - prevDistance);
                    double maxDist = Math.Max(Math.Max(currDistance, prevDistance), valThresh);
                    double minDist = Math.Max(Math.Min(currDistance, prevDistance), valThresh);

                    velScale = Math.Min(currVel, prevVel);

                    awkVal = diffDist / maxDist;
                    angleScale += (1.0 - angleWeight) * applySinTransformation(angle_stretch * (osuCurrent.Angle.Value - pi_over_4) / pi_over_2);
                    strainScale = minTime / maxTime;
                }
                strain = applySinTransformation(Math.Min(1.0, 111.0 * awkVal * angleScale / maxTime));

                test.Add(Tuple.Create(current.BaseObject.StartTime, awkVal));
            }
            return strain * velScale * sliderVel;
        }

        private double applyDiminishingExp(double val) => Math.Max(val - radius * 2.0, 0.0);

        private double applySinTransformation(double val) => Math.Pow(Math.Sin(pi_over_2 * val), 2.0);
    }
}