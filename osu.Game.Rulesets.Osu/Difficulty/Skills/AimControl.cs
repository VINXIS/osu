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
        protected override double SkillMultiplier => 30000;
        protected override double StrainDecayBase => StrainDecay;
        protected override double StarMultiplierPerRepeat => 1.04;

        private const double pi_over_2 = Math.PI / 2.0;
        private const double valThresh = 1.0;
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
            double sliderVel = 1.0 + Math.Min(1.0, osuCurrent.TravelDistance / osuCurrent.TravelTime);

            if (Previous.Count > 0)
            {
                var osuPrevious = (OsuDifficultyHitObject)Previous[0];
                double jumpAwk = 0;
                double distScale = 1.0;
                double angleScale = 0.25;
                double strainScale = 0;

                double minTime = Math.Min(osuCurrent.StrainTime, osuPrevious.StrainTime);
                double maxTime = Math.Max(osuCurrent.StrainTime, osuPrevious.StrainTime);

                if (osuCurrent.Angle != null)
                {
                    double currDistance = applyDiminishingExp(osuCurrent.JumpDistance + osuCurrent.TravelDistance);
                    double prevDistance = applyDiminishingExp(osuPrevious.JumpDistance + osuPrevious.TravelDistance);
                    
                    double diffDist = Math.Abs(currDistance - prevDistance);
                    double maxDist = Math.Max(Math.Max(currDistance, prevDistance), valThresh);
                    double minDist = Math.Max(Math.Min(currDistance, prevDistance), valThresh);

                    double diffVel = Math.Abs(currDistance / osuCurrent.StrainTime - prevDistance / osuPrevious.StrainTime);
                    double maxVel = Math.Max(Math.Max(currDistance / osuCurrent.StrainTime, prevDistance / osuPrevious.StrainTime), valThresh);

                    jumpAwk = diffVel / maxVel;
                    if (minDist < 150) distScale = Math.Pow(Math.Sin(pi_over_2 * minDist / 150), 2.0);
                    angleScale += 3.0 * Math.Pow(Math.Sin(osuCurrent.Angle.Value / 2.0), 2.0) / 4.0;
                    strainScale = minTime / maxTime;
                }

                test.Add(Tuple.Create(current.BaseObject.StartTime, jumpAwk));
                strain = jumpAwk * distScale * angleScale * strainScale / maxTime;
            }
            return strain * sliderVel;
        }

        private double applyDiminishingExp(double val) => Math.Max(val - radius, 0.0);
    }
}