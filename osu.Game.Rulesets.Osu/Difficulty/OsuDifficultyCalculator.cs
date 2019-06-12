// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
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
        private const double star_rating_scale_factor = 0.975;

        public OsuDifficultyCalculator(Ruleset ruleset, WorkingBeatmap beatmap)
            : base(ruleset, beatmap)
        {
        }

        protected override DifficultyAttributes CreateDifficultyAttributes(IBeatmap beatmap, Mod[] mods, Skill[] skills, double clockRate)
        {
            var oldaim = (OsuSkill)skills[0];
            var oldspeed = (OsuSkill)skills[1];
            var jumpaim = (OsuSkill)skills[2];
            var streamaim = (OsuSkill)skills[3];
            var stamina = (OsuSkill)skills[4];
            var speed = (OsuSkill)skills[5];
            var control = (OsuSkill)skills[6];
            var accuracy = (OsuSkill)skills[7];

            if (beatmap.HitObjects.Count == 0)
                return new OsuDifficultyAttributes { Mods = mods };

            IList<double> aimComboSr = oldaim.ComboStarRatings;
            IList<double> aimMissCounts = oldaim.MissCounts;

            IList<double> speedComboSr = oldspeed.ComboStarRatings;
            IList<double> speedMissCounts = oldspeed.MissCounts;

            const double miss_sr_increment = OsuSkill.MISS_STAR_RATING_INCREMENT;

            double oldaimRating = oldaim.Difficulty;
            double oldspeedRating = oldspeed.Difficulty;
            double jumpaimRating = jumpaim.Difficulty;
            double streamaimRating = streamaim.Difficulty;
            double staminaRating = stamina.Difficulty;
            double speedRating = speed.Difficulty;
            double controlRating = control.Difficulty;
            double accuracyRating = accuracy.Difficulty;
            double starRating = star_rating_scale_factor * (oldaimRating + oldspeedRating + Math.Abs(oldaimRating - oldspeedRating) / 2);

            // Todo: These int casts are temporary to achieve 1:1 results with osu!stable, and should be removed in the future
            double hitWindowGreat = (int)(beatmap.HitObjects.First().HitWindows.Great / 2) / clockRate;
            double preempt = (int)BeatmapDifficulty.DifficultyRange(beatmap.BeatmapInfo.BaseDifficulty.ApproachRate, 1800, 1200, 450) / clockRate;

            int maxCombo = beatmap.HitObjects.Count;
            // Add the ticks + tail of the slider. 1 is subtracted because the head circle would be counted twice (once for the slider itself in the line above)
            maxCombo += beatmap.HitObjects.OfType<Slider>().Sum(s => s.NestedHitObjects.Count - 1);

            return new OsuDifficultyAttributes
            {
                StarRating = starRating,
                Mods = mods,
                MissStarRatingIncrement = miss_sr_increment,
                OldAimStrain = oldaimRating,
                OldAimComboStarRatings = aimComboSr,
                OldAimMissCounts = aimMissCounts,
                OldSpeedStrain = oldspeedRating,
                OldSpeedComboStarRatings = speedComboSr,
                OldSpeedMissCounts = speedMissCounts,
                ApproachRate = preempt > 1200 ? (1800 - preempt) / 120 : (1200 - preempt) / 150 + 5,
                OverallDifficulty = (80 - hitWindowGreat) / 6,
                MaxCombo = maxCombo,
                JumpAimStrain = jumpaimRating,
                StreamAimStrain = streamaimRating,
                StaminaStrain = staminaRating,
                SpeedStrain = speedRating,
                ControlStrain = controlRating,
                AccuracyStrain = accuracyRating,
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
            new OldAim(),
            new OldSpeed(),
            new JumpAim(),
            new StreamAim(),
            new Stamina(),
            new Speed(),
            new Control(),
            new Accuracy()
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
