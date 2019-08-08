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
        private double StrainDecay = 0.25;
        private double radius;
        private const double angle_bonus_begin = 5.0 * Math.PI / 6.0;
        private const double angle_bonus_end = Math.PI / 2.0;
        private const double pi_over_2 = Math.PI / 2.0;
        private const double distThresh = 90;
        private const double strainThresh = 50;

        protected override double SkillMultiplier => 3000;
        protected override double StrainDecayBase => StrainDecay;
        protected override double StarMultiplierPerRepeat => 1.04;

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

            double distance = osuCurrent.JumpDistance + osuCurrent.TravelDistance;
            double angleBonus = 1.0;
            double currStrain = Math.Pow(Math.Sin(pi_over_2 * (Math.Min(1.0, Math.Max(distance - 30.0, 0) / distThresh))), 2.0);

            if (osuCurrent.Angle != null)
                angleBonus += Math.Pow(Math.Sin((osuCurrent.Angle.Value - Math.PI) / 2.0), 2.0) / 4.0;

            return angleBonus * currStrain / Math.Max(strainThresh, osuCurrent.StrainTime - osuCurrent.TravelTime + 50);
        }
    }
}
