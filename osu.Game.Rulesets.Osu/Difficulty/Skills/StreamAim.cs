// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// duhhh
    /// </summary>
    public class StreamAim : Skill
    {
        private const double angle_bonus_begin = 5.0 * Math.PI / 6.0;
        private const double angle_bonus_end = Math.PI / 3.0;
        private const double pi_over_2 = Math.PI / 2.0;

        protected override double SkillMultiplier => 3000;
        protected override double StrainDecayBase => 0.3;

        protected override double StrainValueOf(DifficultyHitObject current)
        {
            if (current.BaseObject is Spinner)
                return 0;

            var osuCurrent = (OsuDifficultyHitObject)current;

            double distance = osuCurrent.TravelDistance + osuCurrent.JumpDistance + Math.Sqrt(osuCurrent.TravelDistance * osuCurrent.JumpDistance);
            double angleBonus = 1.0;
            double strain = 0;

            if (distance > 150)
                strain = 1.0;
            else
                strain = Math.Pow(Math.Sin(pi_over_2 * (distance / 150)), 2.0);

            if (osuCurrent.Angle != null)
            {
                if (osuCurrent.Angle.Value < angle_bonus_end)
                    angleBonus = 1.25;
                else if (osuCurrent.Angle.Value < angle_bonus_begin)
                    angleBonus = 1.0 + (Math.Pow(Math.Sin(angle_bonus_begin - osuCurrent.Angle.Value), 2.0) / 4.0);
            }

            return strain * angleBonus / osuCurrent.StrainTime;
        }
    }
}
