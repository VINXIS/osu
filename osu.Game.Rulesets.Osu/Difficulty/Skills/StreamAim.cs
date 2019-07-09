// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// duhhh
    /// </summary>
    public class StreamAim : OsuSkill
    {
        private double StrainDecay = 0.3;
        private const double angle_bonus_begin = 5.0 * Math.PI / 6.0;
        private const double angle_bonus_end = Math.PI / 3.0;
        private const double pi_over_2 = Math.PI / 2.0;

        protected override double SkillMultiplier => 2250;
        protected override double StrainDecayBase => StrainDecay;

        protected override double StrainValueOf(DifficultyHitObject current)
        {
            StrainDecay = 0.3;

            if (current.BaseObject is Spinner)
                return 0;

            var osuCurrent = (OsuDifficultyHitObject)current;

            if (osuCurrent.BaseObject is Slider && osuCurrent.TravelTime < osuCurrent.StrainTime) StrainDecay = Math.Min(osuCurrent.TravelTime, osuCurrent.StrainTime - 30.0) / osuCurrent.StrainTime * 
                (1.0 - Math.Pow(1.0 - StrainDecay, Math.Pow(2.0 + osuCurrent.TravelDistance / Math.Max(osuCurrent.TravelTime, 30.0), 3.0))) + 
                Math.Max(30.0, osuCurrent.StrainTime - osuCurrent.TravelTime) / osuCurrent.StrainTime * StrainDecay;

            double distance = osuCurrent.TravelDistance + osuCurrent.JumpDistance + Math.Sqrt(osuCurrent.TravelDistance * osuCurrent.JumpDistance);
            double angleBonus = 1.0;
            double strain = 0;

            double total1 = 0;
            double total2 = 0;

            if (distance < 150)
                strain = Math.Pow(Math.Sin(pi_over_2 * (distance / 150)), 2.0);
            else
                strain = 1.0;
                

            if (osuCurrent.Angle != null)
            {
                if (osuCurrent.Angle.Value < angle_bonus_end)
                    angleBonus = 1.25;
                else if (osuCurrent.Angle.Value < angle_bonus_begin)
                    angleBonus = 1.0 + (Math.Pow(Math.Sin(angle_bonus_begin - osuCurrent.Angle.Value), 2.0) / 4.0);
            }

            total1 = strain * angleBonus / osuCurrent.StrainTime;

            if (Previous.Count > 0)
            {
                var osuPrevious = (OsuDifficultyHitObject)Previous[0];
                double prevDistance = osuPrevious.TravelDistance + osuPrevious.JumpDistance + Math.Sqrt(osuPrevious.TravelDistance * osuPrevious.JumpDistance);
                double prevStrain = 0;

                if (prevDistance < 150)
                    prevStrain = Math.Pow(Math.Sin(pi_over_2 * (prevDistance / 150)), 2.0);
                else
                    prevStrain = 1.0;

                double strainDiff = 2.0 * Math.Abs(strain - prevStrain);

                total2 = strainDiff * strain * angleBonus / Math.Max(osuCurrent.StrainTime, osuPrevious.StrainTime);
            }

            return Math.Max(total1, total2);
        }
    }
}
