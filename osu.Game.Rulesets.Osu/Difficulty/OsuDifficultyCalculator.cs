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
        private const double difficulty_multiplier = 0.06;

        private const double aim_increase_percentage = 0.15;
        private const double speed_increase_percentage = 0.05;

        public OsuDifficultyCalculator(Ruleset ruleset, WorkingBeatmap beatmap)
            : base(ruleset, beatmap)
        {
        }

        protected override DifficultyAttributes CreateDifficultyAttributes(IBeatmap beatmap, Mod[] mods, Skill[] skills, double clockRate)
        {
            if (beatmap.HitObjects.Count == 0)
                return new OsuDifficultyAttributes { Mods = mods };

            double jumpAimRating = Math.Sqrt(skills[0].DifficultyValue()) * difficulty_multiplier;
            double streamAimRating = Math.Sqrt(skills[1].DifficultyValue()) * difficulty_multiplier;
            double staminaRating = Math.Sqrt(skills[2].DifficultyValue()) * difficulty_multiplier;
            double speedRating = Math.Sqrt(skills[3].DifficultyValue()) * difficulty_multiplier;
            double controlRating = Math.Sqrt(skills[4].DifficultyValue()) * difficulty_multiplier;
            double rhythmRating = Math.Sqrt(skills[5].DifficultyValue()) * difficulty_multiplier;

            double skill_factor_aim = Math.Log(2.0) / (aim_increase_percentage * (jumpAimRating + controlRating) / 2.0);
            double skill_factor_speed = Math.Log(2.0) / (speed_increase_percentage * (speedRating + streamAimRating + staminaRating) / 3.0);

            double totalAim = Math.Log(Math.Pow(Math.E, skill_factor_aim * jumpAimRating) + Math.Pow(Math.E, skill_factor_aim * controlRating)) / skill_factor_aim;
            double totalSpeed = Math.Log(Math.Pow(Math.E, skill_factor_speed * staminaRating) + Math.Pow(Math.E, skill_factor_speed * streamAimRating) + Math.Pow(Math.E, skill_factor_speed * speedRating)) / skill_factor_speed;
            double starRating = totalAim + totalSpeed;

            string values = "Jump Aim: " + Math.Round(jumpAimRating, 2) +
            "\nStream Aim: " + Math.Round(streamAimRating, 2) + 
            "\nStamina: " + Math.Round(staminaRating, 2) + 
            "\nSpeed: " + Math.Round(speedRating, 2) + 
            "\nControl: " + Math.Round(controlRating, 2) + 
            "\nRhythm: " + Math.Round(rhythmRating, 2) +
            "\nAim SR: " + Math.Round(totalAim, 2) +
            "\nSpeed SR: " + Math.Round(totalSpeed, 2) +
            "\nSR: " + Math.Round(starRating, 2);

            using (StreamWriter outputFile = new StreamWriter("values.txt"))
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
                Mods = mods,
                JumpAimStrain = jumpAimRating,
                StreamAimStrain = streamAimRating,
                StaminaStrain = staminaRating,
                SpeedStrain = speedRating,
                ControlStrain = controlRating,
                RhythmStrain = rhythmRating,
                ApproachRate = preempt > 1200 ? (1800 - preempt) / 120 : (1200 - preempt) / 150 + 5,
                OverallDifficulty = (80 - hitWindowGreat) / 6,
                MaxCombo = maxCombo
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
            new Control(),
            new Rhythm(),
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
