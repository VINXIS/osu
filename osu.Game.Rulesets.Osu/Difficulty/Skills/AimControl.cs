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
        private double StrainDecay = 0.3;
        protected override double SkillMultiplier => 5000;
        protected override double StrainDecayBase => StrainDecay;
        protected override double StarMultiplierPerRepeat => 1.07;

        private const double pi_over_2 = Math.PI / 2.0;
        private const double pi_over_4 = Math.PI / 4.0;
        private const double distThresh = 150;
        private const double strainThresh = 90;
        private double decayConst = Math.Pow(0.3, strainThresh / 1000.0);

        protected override double StrainValueOf(DifficultyHitObject current)
        {
            StrainDecay = 0.3;
            if (current.BaseObject is Spinner)
                return 0;

            var osuCurrent = (OsuDifficultyHitObject)current;
            StrainDecay = Math.Pow(decayConst, 1000.0 / Math.Min(osuCurrent.StrainTime, strainThresh));
            if (osuCurrent.BaseObject is Slider && osuCurrent.TravelTime < osuCurrent.StrainTime) StrainDecay = (osuCurrent.TravelTime - 50) / osuCurrent.StrainTime * 
                (1.0 - Math.Pow(1.0 - StrainDecay, Math.Pow(2.0 + osuCurrent.TravelDistance / Math.Max(osuCurrent.TravelTime, 30.0), 3.0))) + 
                (osuCurrent.StrainTime - (osuCurrent.TravelTime - 50)) / osuCurrent.StrainTime * StrainDecay;

            test.Add(Tuple.Create(current.BaseObject.StartTime, 0.0));

            double strain = 0;

            if (Previous.Count > 0 && osuCurrent.Angle != null)
            {
                var osuPrevious = (OsuDifficultyHitObject)Previous[0];
                double currDistance = osuCurrent.TravelDistance + osuCurrent.JumpDistance;
                double prevDistance = osuPrevious.TravelDistance + osuPrevious.JumpDistance;

                double currStrain = applySinTransformation(Math.Min(1.0, currDistance / distThresh));

                double currTime = Math.Max(strainThresh, osuCurrent.StrainTime - osuCurrent.TravelTime + 50);
                double prevTime = Math.Max(strainThresh, osuPrevious.StrainTime - osuPrevious.TravelTime + 50);

                double strainDiff = applySinTransformation(Math.Min(1.0, Math.Max(Math.Min(distThresh, prevDistance) - Math.Min(distThresh, currDistance), 0.0) / distThresh));

                double angleVal = applySinTransformation(osuCurrent.Angle.Value / pi_over_2);

                test.Add(Tuple.Create(current.BaseObject.StartTime, strain));

                strain = (currStrain * (angleVal)) / Math.Max(currTime, prevTime);
            }
            return strain;
        }

        private double applySinTransformation(double val) => Math.Pow(Math.Sin(pi_over_2 * val), 2.0);
    }
}