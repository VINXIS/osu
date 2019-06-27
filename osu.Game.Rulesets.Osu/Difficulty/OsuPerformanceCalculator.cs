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
            double multiplier = 1.12f; // This is being adjusted to keep the final pp value scaled around what it used to be when changing things

            if (mods.Any(m => m is OsuModNoFail))
                multiplier *= 0.90f;

            if (mods.Any(m => m is OsuModSpunOut))
                multiplier *= 0.95f;

            double jumpAimValue = computeJumpAimValue();
            double streamAimValue = computeStreamAimValue();
            double staminaValue = computeStaminaValue();
            double speedValue = computeSpeedValue();
            double controlValue = computeControlValue();
            double accuracyValue = computeAccuracyValue();
            double totalValue =
                Math.Pow(
                    Math.Pow(jumpAimValue, 1.1f) +
                    Math.Pow(streamAimValue, 1.1f) +
                    Math.Pow(staminaValue, 1.1f) +
                    Math.Pow(speedValue, 1.1f) +
                    Math.Pow(controlValue, 1.1f) +
                    Math.Pow(accuracyValue, 1.1f), 1.0f / 1.2f
                ) * multiplier;

            if (categoryRatings != null)
            {
                categoryRatings.Add("Jump Aim", jumpAimValue);
                categoryRatings.Add("Stream Aim", streamAimValue);
                categoryRatings.Add("Stamina", staminaValue);
                categoryRatings.Add("Speed", speedValue);
                categoryRatings.Add("Control", controlValue);
                categoryRatings.Add("Accuracy", accuracyValue);
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
                rawAim = Math.Pow(rawAim, 0.8);

            double jumpAimValue = Math.Pow(5.0f * Math.Max(1.0f, rawAim / 0.0675f) - 4.0f, 3.0f) / 100000.0f;

            // Longer maps are worth more
            jumpAimValue *= 0.95f + 0.4f * Math.Min(1.0f, totalHits / 2000.0f) +
                (totalHits > 2000 ? Math.Log10(totalHits / 2000.0f) * 0.5f : 0.0f);

            // Penalize misses exponentially. This mainly fixes tag4 maps and the likes until a per-hitobject solution is available
            jumpAimValue *= Math.Pow(0.95f, countMiss);

            // Combo scaling
            if (beatmapMaxCombo > 0)
                jumpAimValue *= Math.Min(Math.Pow(scoreMaxCombo, 0.8f) / Math.Pow(beatmapMaxCombo, 0.8f), 1.0f);

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
                jumpAimValue *= 1.0f + 0.02f * (12.0f - Attributes.ApproachRate);

            if (mods.Any(h => h is OsuModFlashlight))
            {
                // Apply object-based bonus for flashlight.
                jumpAimValue *= 1.0f + 0.35f * Math.Min(1.0f, totalHits / 200.0f) +
                        (totalHits > 200 ? 0.3f * Math.Min(1.0f, (totalHits - 200) / 300.0f) +
                        (totalHits > 500 ? (totalHits - 500) / 1200.0f : 0.0f) : 0.0f);
            }

            // Scale the aim value with accuracy _slightly_
            jumpAimValue *= 0.5f + accuracy / 2.0f;
            // It is important to also consider accuracy difficulty when doing that
            jumpAimValue *= 0.98f + Math.Pow(Attributes.OverallDifficulty, 2) / 2500;

            return jumpAimValue;
        }

        private double computeStreamAimValue()
        {
            double rawStreamAim = Attributes.StreamAimStrain;

            if (mods.Any(m => m is OsuModTouchDevice))
                rawStreamAim = Math.Pow(rawStreamAim, 1.1);

            double streamAimValue = Math.Pow(5.0f * Math.Max(1.0f, rawStreamAim / 0.0675f) - 4.0f, 3.0f) / 100000.0f;

            // Longer maps are worth more
            streamAimValue *= 0.95f + 0.4f * Math.Min(1.0f, totalHits / 2000.0f) +
                (totalHits > 2000 ? Math.Log10(totalHits / 2000.0f) * 0.5f : 0.0f);
            
            // Penalize misses exponentially. This mainly fixes tag4 maps and the likes until a per-hitobject solution is available
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

            // Scale the aim value with accuracy _slightly_
            streamAimValue *= 0.5f + accuracy / 2.0f;
            // It is important to also consider accuracy difficulty when doing that
            streamAimValue *= 0.98f + Math.Pow(Attributes.OverallDifficulty, 2) / 2500;

            return streamAimValue;
        }

        private double computeControlValue()
        {
            double rawControl = Attributes.ControlStrain;

            if (mods.Any(m => m is OsuModTouchDevice))
                rawControl = Math.Pow(rawControl, 0.8);
    
            double controlValue = Math.Pow(5.0f * Math.Max(1.0f, rawControl / 0.0675f) - 4.0f, 3.0f) / 100000.0f;

            // Longer maps are worth more
            controlValue *= 0.95f + 0.4f * Math.Min(1.0f, totalHits / 2000.0f) +
                (totalHits > 2000 ? Math.Log10(totalHits / 2000.0f) * 0.5f : 0.0f);

            // Penalize misses exponentially. This mainly fixes tag4 maps and the likes until a per-hitobject solution is available
            controlValue *= Math.Pow(0.95f, countMiss);

            // Combo scaling
            if (beatmapMaxCombo > 0)
                controlValue *= Math.Min(Math.Pow(scoreMaxCombo, 0.8f) / Math.Pow(beatmapMaxCombo, 0.8f), 1.0f);

            double approachRateFactor = 1.0f;
            if (Attributes.ApproachRate > 10.33f)
                approachRateFactor += 0.3f * (Attributes.ApproachRate - 10.33f);

            controlValue *= approachRateFactor;

            if (mods.Any(m => m is OsuModHidden))
                controlValue *= 1.0f + 0.08f * (12.0f - Attributes.ApproachRate);

            // Scale the aim value with accuracy _slightly_
            controlValue *= 0.5f + accuracy / 2.0f;
            // It is important to also consider accuracy difficulty when doing that
            controlValue *= 0.98f + Math.Pow(Attributes.OverallDifficulty, 2) / 2500;

            return controlValue;
        }

        private double computeStaminaValue()
        {
            double staminaValue = Math.Pow(5.0f * Math.Max(1.0f, Attributes.StaminaStrain / 0.0675f) - 4.0f, 3.0f) / 100000.0f;

            // Penalize misses exponentially. This mainly fixes tag4 maps and the likes until a per-hitobject solution is available
            staminaValue *= Math.Pow(0.97f, countMiss);

            // Combo scaling
            if (beatmapMaxCombo > 0)
                staminaValue *= Math.Min(Math.Pow(scoreMaxCombo, 0.8f) / Math.Pow(beatmapMaxCombo, 0.8f), 1.0f);

            double approachRateFactor = 1.0f;
            if (Attributes.ApproachRate > 10.33f)
                approachRateFactor += 0.3f * (Attributes.ApproachRate - 10.33f);

            staminaValue *= approachRateFactor;

            if (mods.Any(m => m is OsuModHidden))
                staminaValue *= 1.0f + 0.08f * (12.0f - Attributes.ApproachRate);

            // Scale the aim value with accuracy _slightly_
            staminaValue *= 0.5f + accuracy / 2.0f;
            // It is important to also consider accuracy difficulty when doing that
            staminaValue *= 1.0f + Math.Pow(Attributes.OverallDifficulty, 2) / 2000;

            return staminaValue;
        }

        private double computeSpeedValue()
        {
            double speedValue = Math.Pow(5.0f * Math.Max(1.0f, Attributes.SpeedStrain / 0.0675f) - 4.0f, 3.0f) / 100000.0f;

            // Penalize misses exponentially. This mainly fixes tag4 maps and the likes until a per-hitobject solution is available
            speedValue *= Math.Pow(0.97f, countMiss);

            // Combo scaling
            if (beatmapMaxCombo > 0)
                speedValue *= Math.Min(Math.Pow(scoreMaxCombo, 0.8f) / Math.Pow(beatmapMaxCombo, 0.8f), 1.0f);

            double approachRateFactor = 1.0f;
            if (Attributes.ApproachRate > 10.33f)
                approachRateFactor += 0.3f * (Attributes.ApproachRate - 10.33f);

            speedValue *= approachRateFactor;

            if (mods.Any(m => m is OsuModHidden))
                speedValue *= 1.0f + 0.08f * (12.0f - Attributes.ApproachRate);

            // Scale the aim value with accuracy _slightly_
            speedValue *= 0.5f + accuracy / 2.0f;
            // It is important to also consider accuracy difficulty when doing that
            speedValue *= 1.0f + Math.Pow(Attributes.OverallDifficulty, 2) / 2000;

            return speedValue;
        }

        private double computeAccuracyValue()
        {
            double rhythmValue = Math.Pow(5.0f * Math.Max(1.0f, Attributes.RhythmStrain / 0.0675f) - 4.0f, 2.0f) / 100000.0f;

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
            double accuracyValue = rhythmValue * Math.Pow(1.52163f, Attributes.OverallDifficulty) * Math.Pow(betterAccuracyPercentage, 24) * 2.83f;

            // Bonus for many hitcircles - it's harder to keep good accuracy up for longer
            accuracyValue *= Math.Min(1.15f, Math.Pow(amountHitObjectsWithAccuracy / 1000.0f, 0.3f));

            if (mods.Any(m => m is OsuModHidden))
                accuracyValue *= 1.08f;
            if (mods.Any(m => m is OsuModFlashlight))
                accuracyValue *= 1.02f;

            return accuracyValue;
        }

        private double totalHits => countGreat + countGood + countMeh + countMiss;
        private double totalSuccessfulHits => countGreat + countGood + countMeh;
    }
}
