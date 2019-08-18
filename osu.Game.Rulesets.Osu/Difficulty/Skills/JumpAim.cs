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
        private const float prevMultiplier = 0.25f;
        private const double distThresh = 125;
        private const double strainThresh = 90;

        protected override double SkillMultiplier => 38.86;
        protected override double StrainDecayBase => StrainDecay;
        protected override double StarMultiplierPerRepeat => 1.07;

        protected override double StrainValueOf(DifficultyHitObject current)
        {
            StrainDecay = 0.15;

            if (current.BaseObject is Spinner)
                return 0;

            var osuCurrent = (OsuDifficultyHitObject)current;
            if (osuCurrent.BaseObject is Slider && osuCurrent.TravelTime < osuCurrent.StrainTime) StrainDecay = (osuCurrent.TravelTime - 50) / osuCurrent.StrainTime * 
                (1.0 - Math.Pow(1.0 - StrainDecay, Math.Pow(1.0 + osuCurrent.TravelDistance / Math.Max(osuCurrent.TravelTime, 30.0), 3.0))) + 
                (osuCurrent.StrainTime - (osuCurrent.TravelTime - 50)) / osuCurrent.StrainTime * StrainDecay;

            double strain = 0;
            double diffStrain = 0;
            double sliderStrain = osuCurrent.TravelDistance / osuCurrent.TravelTime;

            if (Previous.Count > 0 && osuCurrent.Angle != null)
            {
                var osuPrevious = (OsuDifficultyHitObject)Previous[0];

                double currTime = Math.Max(strainThresh, osuCurrent.StrainTime - osuCurrent.TravelTime + 50);
                double prevTime = Math.Max(strainThresh, osuPrevious.StrainTime - osuPrevious.TravelTime + 50);

                double currDistance = osuCurrent.JumpDistance + osuCurrent.TravelDistance;
                double prevDistance = osuPrevious.JumpDistance + osuPrevious.TravelDistance;
                diffStrain = Math.Min(Math.Min(currDistance, prevDistance) / distThresh, 1.0) * Math.Abs(Math.Min(currDistance, 250) - Math.Min(prevDistance, 250)) / (Math.Max(currTime, prevTime) - 20);

                if (osuCurrent.JumpDistance >= distThresh && osuPrevious.JumpDistance >= distThresh)
                    strain = Math.Abs((applyDiminishingDist(osuCurrent.DistanceVector) + prevMultiplier * applyDiminishingDist(osuPrevious.DistanceVector)).Length) / Math.Max(osuCurrent.StrainTime, osuPrevious.StrainTime);
            } else if (osuCurrent.JumpDistance >= distThresh)
                strain = applyDiminishingDist(osuCurrent.DistanceVector).Length / Math.Max(osuCurrent.StrainTime - osuCurrent.TravelTime + 50 - 20, strainThresh);

            return strain + diffStrain + sliderStrain;
        }

        private Vector2 applyDiminishingDist(Vector2 val) => val - (float)distThresh * val.Normalized();
    }
}