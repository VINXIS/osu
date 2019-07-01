// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Osu.Difficulty
{
    public class OsuPerformanceCalculator : PerformanceCalculator
    {
        public new OsuDifficultyAttributes Attributes => (OsuDifficultyAttributes)base.Attributes;

        private readonly int countHitCircles;
        private readonly int beatmapMaxCombo;

        private Mod[] mods;

        private double accuracy;
        private int scoreMaxCombo;
        private int countGreat;
        private int countGood;
        private int countMeh;
        private int countMiss;

        private const double pp_factor = 5.0f;

        public OsuPerformanceCalculator(Ruleset ruleset, WorkingBeatmap beatmap, ScoreInfo score)
            : base(ruleset, beatmap, score)
        {
            countHitCircles = Beatmap.HitObjects.Count(h => h is HitCircle);

            beatmapMaxCombo = Beatmap.HitObjects.Count;
            // Add the ticks + tail of the slider. 1 is subtracted because the "headcircle" would be counted twice (once for the slider itself in the line above)
            beatmapMaxCombo += Beatmap.HitObjects.OfType<Slider>().Sum(s => s.NestedHitObjects.Count - 1);
        }

        public override double Calculate(Dictionary<string, double> categoryRatings = null)
        {
            mods = Score.Mods;
            accuracy = Score.Accuracy;
            scoreMaxCombo = Score.MaxCombo;
            countGreat = Convert.ToInt32(Score.Statistics[HitResult.Great]);
            countGood = Convert.ToInt32(Score.Statistics[HitResult.Good]);
            countMeh = Convert.ToInt32(Score.Statistics[HitResult.Meh]);
            countMiss = Convert.ToInt32(Score.Statistics[HitResult.Miss]);

            // Don't count scores made with supposedly unranked mods
            if (mods.Any(m => !m.Ranked))
                return 0;

            // Custom multipliers for NoFail and SpunOut.
            double multiplier = 1.0f; // This is being adjusted to keep the final pp value scaled around what it used to be when changing things

            if (mods.Any(m => m is OsuModNoFail))
                multiplier *= 0.90f;

            if (mods.Any(m => m is OsuModSpunOut))
                multiplier *= 0.95f;

            double jumpAimValue = computeJumpAimValue();
            double streamAimValue = computeStreamAimValue();
            double staminaValue = computeStaminaValue();
            double speedValue = computeSpeedValue();
            double controlValue = computeControlValue();
            double rhythmValue = computeRhythmValue();

            double totalValue =
                Math.Pow(
                    Math.Pow(jumpAimValue, pp_factor) +
                    Math.Pow(streamAimValue, pp_factor) +
                    Math.Pow(staminaValue, pp_factor) +
                    Math.Pow(speedValue, pp_factor) +
                    Math.Pow(controlValue, pp_factor) +
                    Math.Pow(rhythmValue, pp_factor), 1.0f / pp_factor
                ) * multiplier;

            if (categoryRatings != null)
            {
                categoryRatings.Add("Jump Aim", jumpAimValue);
                categoryRatings.Add("Stream Aim", streamAimValue);
                categoryRatings.Add("Stamina", staminaValue);
                categoryRatings.Add("Speed", speedValue);
                categoryRatings.Add("Control", controlValue);
                categoryRatings.Add("Rhythm", rhythmValue);
                categoryRatings.Add("OD", Attributes.OverallDifficulty);
                categoryRatings.Add("AR", Attributes.ApproachRate);
                categoryRatings.Add("Max Combo", beatmapMaxCombo);
            }

            return totalValue;
        }

        private double computeJumpAimValue()
        {
            double rawAim = Attributes.JumpAimStrain;

            if (mods.Any(m => m is OsuModTouchDevice))
                rawAim = Math.Pow(rawAim, 0.75f);

            double jumpAimValue = Math.Pow(5.0f * Math.Max(1.0f, rawAim / 0.0575f) - 4.0f, 3.0f) / 100000.0f;

            // Longer maps are worth more
            jumpAimValue *= 0.95f + 0.4f * Math.Min(1.0f, totalHits / 2000.0f) +
                (totalHits > 2000 ? Math.Log10(totalHits / 2000.0f) * 0.5f : 0.0f);

            // Penalize misses exponentially.
            jumpAimValue *= Math.Pow(0.95f, countMiss);

            // HARSH Combo scaling
            if (beatmapMaxCombo > 0)
                jumpAimValue *= Math.Min(Math.Pow(scoreMaxCombo, 1.1f) / Math.Pow(beatmapMaxCombo, 1.1f), 1.0f);

            double approachRateFactor = 1.0f;
            if (Attributes.ApproachRate > 10.33f)
                approachRateFactor += 0.3f * (Attributes.ApproachRate - 10.33f);
            else if (Attributes.ApproachRate < 8.0f)
            {
                    approachRateFactor += 0.01f * (8.0f - Attributes.ApproachRate);
            }

            jumpAimValue *= approachRateFactor;

            // We want to give more reward for lower AR when it comes to aim and HD. This nerfs high AR and buffs lower AR.
            if (mods.Any(h => h is OsuModHidden))
                jumpAimValue *= Math.Max(1.0f, 1.0f + 0.04f * (9.0f - Attributes.ApproachRate));

            if (mods.Any(h => h is OsuModFlashlight))
            {
                // Apply object-based bonus for flashlight.
                jumpAimValue *= 1.0f + 0.35f * Math.Min(1.0f, totalHits / 200.0f) +
                        (totalHits > 200 ? 0.3f * Math.Min(1.0f, (totalHits - 200) / 300.0f) +
                        (totalHits > 500 ? (totalHits - 500) / 1200.0f : 0.0f) : 0.0f);
            }

            // Scale the jumpaim value with accuracy
            jumpAimValue *= 0.75f + accuracy / 4.0f;
            // It is important to also consider accuracy difficulty when doing that
            jumpAimValue *= 0.98f + Math.Pow(Attributes.OverallDifficulty, 2) / 2500;

            return jumpAimValue;
        }

        private double computeStreamAimValue()
        {
            double rawStreamAim = Attributes.StreamAimStrain;

            if (mods.Any(m => m is OsuModTouchDevice))
                rawStreamAim = Math.Pow(rawStreamAim, 1.25f);

            double streamAimValue = Math.Pow(5.0f * Math.Max(1.0f, rawStreamAim / 0.0575f) - 4.0f, 3.0f) / 100000.0f;

            // Longer maps are worth more
            streamAimValue *= 0.95f + 0.4f * Math.Min(1.0f, totalHits / 2000.0f) +
                (totalHits > 2000 ? Math.Log10(totalHits / 2000.0f) * 0.5f : 0.0f);
            
            // Penalize misses exponentially.
            streamAimValue *= Math.Pow(0.95f, countMiss);

            // Combo scaling
            if (beatmapMaxCombo > 0)
                streamAimValue *= Math.Min(Math.Pow(scoreMaxCombo, 0.8f) / Math.Pow(beatmapMaxCombo, 0.8f), 1.0f);

            double approachRateFactor = 1.0f;
            if (Attributes.ApproachRate > 10.33f)
                approachRateFactor += 0.3f * (Attributes.ApproachRate - 10.33f);

            streamAimValue *= approachRateFactor;

            if (mods.Any(m => m is OsuModHidden))
                streamAimValue *= 1.0f + 0.08f * (12.0f - Attributes.ApproachRate);
            
            if (mods.Any(h => h is OsuModFlashlight))
            {
                // Apply object-based bonus for flashlight.
                streamAimValue *= 1.0f + (0.35f * Math.Min(1.0f, totalHits / 200.0f) +
                        (totalHits > 200 ? 0.3f * Math.Min(1.0f, (totalHits - 200) / 300.0f) +
                        (totalHits > 500 ? (totalHits - 500) / 1200.0f : 0.0f) : 0.0f)) / 2.0f;
            }

            // Scale the streamaim value with accuracy
            streamAimValue *= 0.75f + accuracy / 4.0f;
            // It is important to also consider accuracy difficulty when doing that
            streamAimValue *= 0.98f + Math.Pow(Attributes.OverallDifficulty, 2) / 2500;

            return streamAimValue;
        }

        private double computeControlValue()
        {
            double rawControl = Attributes.ControlStrain;

            if (mods.Any(m => m is OsuModTouchDevice))
                rawControl = Math.Pow(rawControl, 0.75f);
    
            double controlValue = Math.Pow(5.0f * Math.Max(1.0f, rawControl / 0.0575f) - 4.0f, 3.0f) / 100000.0f;

            // Longer maps are worth more
            controlValue *= 0.95f + 0.4f * Math.Min(1.0f, totalHits / 2000.0f) +
                (totalHits > 2000 ? Math.Log10(totalHits / 2000.0f) * 0.5f : 0.0f);

            // Penalize misses exponentially.
            controlValue *= Math.Pow(0.95f, countMiss);

            // HARSH Combo scaling
            if (beatmapMaxCombo > 0)
                controlValue *= Math.Min(Math.Pow(scoreMaxCombo, 1.1f) / Math.Pow(beatmapMaxCombo, 1.1f), 1.0f);

            double approachRateFactor = 1.0f;
            if (Attributes.ApproachRate > 10.33f)
                approachRateFactor += 0.3f * (Attributes.ApproachRate - 10.33f);

            controlValue *= approachRateFactor;

            if (mods.Any(m => m is OsuModHidden))
                controlValue *= 1.0f + 0.08f * (12.0f - Attributes.ApproachRate);

            if (mods.Any(h => h is OsuModFlashlight))
            {
                // Apply object-based bonus for flashlight.
                controlValue *= 1.0f + 0.35f * Math.Min(1.0f, totalHits / 200.0f) +
                        (totalHits > 200 ? 0.3f * Math.Min(1.0f, (totalHits - 200) / 300.0f) +
                        (totalHits > 500 ? (totalHits - 500) / 1200.0f : 0.0f) : 0.0f);
            }

            // Scale the control value with accuracy
            controlValue *= 0.75f + accuracy / 4.0f;
            // It is important to also consider accuracy difficulty when doing that
            controlValue *= 0.98f + Math.Pow(Attributes.OverallDifficulty, 2) / 2500;

            return controlValue;
        }

        private double computeStaminaValue()
        {
            double staminaValue = Math.Pow(5.0f * Math.Max(1.0f, Attributes.StaminaStrain / 0.0575f) - 4.0f, 3.0f) / 100000.0f;

            // Penalize misses exponentially.
            staminaValue *= Math.Pow(0.99f, countMiss);

            // SLIGHT Combo scaling
            if (beatmapMaxCombo > 0)
                staminaValue *= Math.Min(Math.Pow(scoreMaxCombo, 0.6f) / Math.Pow(beatmapMaxCombo, 0.6f), 1.0f);

            // Scale the aim value with accuracy
            staminaValue *= accuracy + 0.02f / 0.98f;
            // It is important to also consider accuracy difficulty when doing that
            staminaValue *= 1.0f + Math.Pow(Attributes.OverallDifficulty, 2) / 2500;

            return staminaValue;
        }

        private double computeSpeedValue()
        {
            double rawSpeed = Attributes.SpeedStrain;

            if (mods.Any(m => m is OsuModTouchDevice))
                rawSpeed = Math.Pow(rawSpeed, 1.25f);

            double speedValue = Math.Pow(5.0f * Math.Max(1.0f, rawSpeed / 0.0575f) - 4.0f, 3.0f) / 100000.0f;

            // Penalize misses exponentially.
            speedValue *= Math.Pow(0.99f, countMiss);

            // SLIGHT Combo scaling
            if (beatmapMaxCombo > 0)
                speedValue *= Math.Min(Math.Pow(scoreMaxCombo, 0.6f) / Math.Pow(beatmapMaxCombo, 0.6f), 1.0f);

            // Scale the aim value with accuracy
            speedValue *= accuracy + 0.02f / 0.98f;
            // It is important to also consider accuracy difficulty when doing that
            speedValue *= 1.0f + Math.Pow(Attributes.OverallDifficulty, 2) / 2500;

            return speedValue;
        }

        private double computeRhythmValue()
        {
            double rhythmValue = Math.Pow(5.0f * Math.Max(1.0f, Attributes.RhythmStrain / 0.0575f) - 4.0f, 3.0f) / 1000000.0f;

            double approachRateFactor = 1.0f;
            if (Attributes.ApproachRate > 10.33f)
                approachRateFactor *= 1.0f + 0.3f * (Attributes.ApproachRate - 10.33f);

            rhythmValue *= approachRateFactor;

            // This percentage only considers HitCircles of any value - in this part of the calculation we focus on hitting the timing hit window
            double betterAccuracyPercentage;
            int amountHitObjectsWithAccuracy = countHitCircles;

            if (amountHitObjectsWithAccuracy > 0)
                betterAccuracyPercentage = ((countGreat - (totalHits - amountHitObjectsWithAccuracy)) * 6 + countGood * 2 + countMeh) / (amountHitObjectsWithAccuracy * 6);
            else
                betterAccuracyPercentage = 0;

            // It is possible to reach a negative accuracy with this formula. Cap it at zero - zero points
            if (betterAccuracyPercentage < 0)
                betterAccuracyPercentage = 0;

            // Lots of arbitrary values from testing.
            // Considering to use derivation from perfect accuracy in a probabilistic manner - assume normal distribution
            rhythmValue = rhythmValue * Math.Pow(1.52163f, Attributes.OverallDifficulty) * Math.Pow(betterAccuracyPercentage, 24);

            // Bonus for many hitcircles - it's harder to keep good accuracy up for longer
            rhythmValue *= Math.Min(1.15f, Math.Pow(amountHitObjectsWithAccuracy / 1000.0f, 0.3f));

            if (mods.Any(m => m is OsuModHidden))
                rhythmValue *= 1.08f;
            if (mods.Any(m => m is OsuModFlashlight))
                rhythmValue *= 1.02f;

            return rhythmValue;
        }

        private double totalHits => countGreat + countGood + countMeh + countMiss;
        private double totalSuccessfulHits => countGreat + countGood + countMeh;
    }
}
