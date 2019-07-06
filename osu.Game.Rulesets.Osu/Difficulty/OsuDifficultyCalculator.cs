// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Skills;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty
{
    public class OsuDifficultyCalculator : DifficultyCalculator
    {
        private const double difficulty_multiplier = 0.0675;
        private const double star_rating_scale_factor = 1.3;
        private const double star_factor = 0.5;
        private const double total_star_factor = 4.0;

        public OsuDifficultyCalculator(Ruleset ruleset, WorkingBeatmap beatmap)
            : base(ruleset, beatmap)
        {
        }

        public double PointsTransformation(double skillRating) => Math.Pow(5.0f * Math.Max(1.0f, skillRating / difficulty_multiplier) - 4.0f, 3.0f) / 100000.0f;
        public double StarTransformation(double pointsRating) => difficulty_multiplier * (Math.Pow(100000.0f * pointsRating, 1.0f / 3.0f) + 4.0f) / 5.0f;

        protected override DifficultyAttributes CreateDifficultyAttributes(IBeatmap beatmap, Mod[] mods, Skill[] skills, double clockRate)
        {
            var jumpAim = (OsuSkill)skills[0];
            var streamAim = (OsuSkill)skills[1];
            var stamina = (OsuSkill)skills[2];
            var speed = (OsuSkill)skills[3];
            var aimControl = (OsuSkill)skills[4];
            var fingerControl = (OsuSkill)skills[5];

            if (beatmap.HitObjects.Count == 0)
                return new OsuDifficultyAttributes { Mods = mods };

            IList<double> jumpAimComboSr = jumpAim.ComboStarRatings;
            IList<double> jumpAimMissCounts = jumpAim.MissCounts;

            IList<double> streamAimComboSr = streamAim.ComboStarRatings;
            IList<double> streamAimMissCounts = streamAim.MissCounts;

            IList<double> staminaComboSr = stamina.ComboStarRatings;
            IList<double> staminaMissCounts = stamina.MissCounts;

            IList<double> speedComboSr = speed.ComboStarRatings;
            IList<double> speedMissCounts = speed.MissCounts;
            
            IList<double> aimControlComboSr = aimControl.ComboStarRatings;
            IList<double> aimControlMissCounts = aimControl.MissCounts;

            IList<double> fingerControlComboSr = fingerControl.ComboStarRatings;
            IList<double> fingerControlMissCounts = fingerControl.MissCounts;

            const double miss_sr_increment = OsuSkill.MISS_STAR_RATING_INCREMENT;

            double jumpAimRating = jumpAimComboSr.Last();
            double streamAimRating = streamAimComboSr.Last();
            double staminaRating = staminaComboSr.Last();
            double speedRating = speedComboSr.Last();
            double aimControlRating = aimControlComboSr.Last();
            double fingerControlRating = fingerControlComboSr.Last();
            
            double totalAimRating = Math.Pow(
                Math.Pow(PointsTransformation(jumpAimRating), star_factor) + 
                Math.Pow(PointsTransformation(streamAimRating), star_factor) +
                Math.Pow(PointsTransformation(aimControlRating), star_factor), 1.0 / star_factor);
            double totalSpeedRating = Math.Pow(
                Math.Pow(PointsTransformation(staminaRating), star_factor) +
                Math.Pow(PointsTransformation(speedRating), star_factor) +
                Math.Pow(PointsTransformation(fingerControlRating), star_factor), 1.0 / star_factor);
            double starRating = StarTransformation(star_rating_scale_factor * Math.Pow(
                Math.Pow(totalAimRating, total_star_factor) + 
                Math.Pow(totalSpeedRating, total_star_factor), 1.0 / total_star_factor));

            string values = "Jump Aim: " + Math.Round(jumpAimRating, 2) +
            "\nStream Aim: " + Math.Round(streamAimRating, 2) + 
            "\nStamina: " + Math.Round(staminaRating, 2) + 
            "\nSpeed: " + Math.Round(speedRating, 2) + 
            "\nAim Control: " + Math.Round(aimControlRating, 2) + 
            "\nFinger Control: " + Math.Round(fingerControlRating, 2) +
            "\n---" +
            "\nAim SR: " + Math.Round(totalAimRating, 2) +
            "\nSpeed SR: " + Math.Round(totalSpeedRating, 2) +
            "\nSR: " + Math.Round(starRating, 2);

            using (StreamWriter outputFile = new StreamWriter(beatmap.BeatmapInfo.OnlineBeatmapID + "values.txt"))
                outputFile.WriteLine(values);

            // Todo: These int casts are temporary to achieve 1:1 results with osu!stable, and should be removed in the future
            double hitWindowGreat = (int)(beatmap.HitObjects.First().HitWindows.Great / 2) / clockRate;
            double preempt = (int)BeatmapDifficulty.DifficultyRange(beatmap.BeatmapInfo.BaseDifficulty.ApproachRate, 1800, 1200, 450) / clockRate;

            int maxCombo = beatmap.HitObjects.Count;
            // Add the ticks + tail of the slider. 1 is subtracted because the head circle would be counted twice (once for the slider itself in the line above)
            maxCombo += beatmap.HitObjects.OfType<Slider>().Sum(s => s.NestedHitObjects.Count - 1);

            return new OsuDifficultyAttributes
            {
                StarRating = starRating,
                AimRating = StarTransformation(totalAimRating),
                SpeedRating = StarTransformation(totalSpeedRating),
                Mods = mods,
                MissStarRatingIncrement = miss_sr_increment,
                ApproachRate = preempt > 1200 ? (1800 - preempt) / 120 : (1200 - preempt) / 150 + 5,
                OverallDifficulty = (80 - hitWindowGreat) / 6,
                MaxCombo = maxCombo,

                JumpAimStrain = jumpAimRating,
                JumpAimComboStarRatings = jumpAimComboSr,
                JumpAimMissCounts = jumpAimMissCounts,

                StreamAimStrain = streamAimRating,
                StreamAimComboStarRatings = streamAimComboSr,
                StreamAimMissCounts = streamAimMissCounts,

                StaminaStrain = staminaRating,
                StaminaComboStarRatings = staminaComboSr,
                StaminaMissCounts = staminaMissCounts,

                SpeedStrain = speedRating,
                SpeedComboStarRatings = speedComboSr,
                SpeedMissCounts = speedMissCounts,

                AimControlStrain = aimControlRating,
                AimControlComboStarRatings = aimControlComboSr,
                AimControlMissCounts = aimControlMissCounts,

                FingerControlStrain = fingerControlRating,
                FingerControlComboStarRatings = fingerControlComboSr,
                FingerControlMissCounts = fingerControlMissCounts
            };
        }

        protected override IEnumerable<DifficultyHitObject> CreateDifficultyHitObjects(IBeatmap beatmap, double clockRate)
        {
            // The first jump is formed by the first two hitobjects of the map.
            // If the map has less than two OsuHitObjects, the enumerator will not return anything.
            for (int i = 1; i < beatmap.HitObjects.Count; i++)
            {
                var lastLast = i > 1 ? beatmap.HitObjects[i - 2] : null;
                var last = beatmap.HitObjects[i - 1];
                var current = beatmap.HitObjects[i];

                yield return new OsuDifficultyHitObject(current, lastLast, last, clockRate);
            }
        }

        protected override Skill[] CreateSkills(IBeatmap beatmap) => new Skill[]
        {
            new JumpAim(),
            new StreamAim(),
            new Stamina(),
            new Speed(),
            new AimControl(),
            new FingerControl()
        };

        protected override Mod[] DifficultyAdjustmentMods => new Mod[]
        {
            new OsuModDoubleTime(),
            new OsuModHalfTime(),
            new OsuModEasy(),
            new OsuModHardRock(),
        };
    }
}
