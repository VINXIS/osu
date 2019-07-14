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
using MathNet.Numerics;

namespace osu.Game.Rulesets.Osu.Difficulty
{
    public class OsuPerformanceCalculator : PerformanceCalculator
    {
        public new OsuDifficultyAttributes Attributes => (OsuDifficultyAttributes)base.Attributes;

        private readonly int countHitCircles;
        private readonly int countSliders;
        private readonly int beatmapMaxCombo;

        private Mod[] mods;

        private double accuracy;
        private int scoreMaxCombo;
        private int countGreat;
        private int countGood;
        private int countMeh;
        private int countMiss;
        private const double combo_weight = 0.5;
        private const double aim_pp_factor = 1.25f;
        private const double speed_pp_factor = 2.5f;
        private const double total_factor = 1.1f;

        public OsuPerformanceCalculator(Ruleset ruleset, WorkingBeatmap beatmap, ScoreInfo score)
            : base(ruleset, beatmap, score)
        {
            countHitCircles = Beatmap.HitObjects.Count(h => h is HitCircle);
            countSliders = Beatmap.HitObjects.Count(h => h is Slider);

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
            double multiplier = 1.23f; // This is being adjusted to keep the final pp value scaled around what it used to be when changing things

            if (mods.Any(m => m is OsuModNoFail))
                multiplier *= 0.90f;

            if (mods.Any(m => m is OsuModSpunOut))
                multiplier *= 0.95f;

            double jumpAimValue = computeJumpAimValue(categoryRatings);
            double streamAimValue = computeStreamAimValue(categoryRatings);
            double staminaValue = computeStaminaValue(categoryRatings);
            double speedValue = computeSpeedValue(categoryRatings);
            double aimControlValue = computeAimControlValue(categoryRatings);
            double fingerControlValue = computeFingerControlValue(categoryRatings);
            double accuracyValue = computeAccuracyValue(categoryRatings);

            double totalAimValue = Math.Pow(
                Math.Pow(jumpAimValue, aim_pp_factor) + 
                Math.Pow(streamAimValue, aim_pp_factor) + 
                Math.Pow(aimControlValue, aim_pp_factor), 1.0f / aim_pp_factor);
            double totalSpeedValue = Math.Pow(
                Math.Pow(staminaValue, speed_pp_factor) + 
                Math.Pow(speedValue, speed_pp_factor) + 
                Math.Pow(fingerControlValue, speed_pp_factor), 1.0f / speed_pp_factor);
            double totalValue = multiplier * Math.Pow(
                Math.Pow(totalAimValue, total_factor) + 
                Math.Pow(totalSpeedValue, total_factor) +
                Math.Pow(accuracyValue, total_factor), 1.0f / total_factor);

            if (categoryRatings != null)
            {
                categoryRatings.Add("Jump Aim", jumpAimValue);
                categoryRatings.Add("Stream Aim", streamAimValue);
                categoryRatings.Add("Stamina", staminaValue);
                categoryRatings.Add("Speed", speedValue);
                categoryRatings.Add("Aim Control", aimControlValue);
                categoryRatings.Add("Finger Control", fingerControlValue);
                categoryRatings.Add("Accuracy", accuracyValue);
                categoryRatings.Add("Total Aim", totalAimValue);
                categoryRatings.Add("Total Speed", totalSpeedValue);
                categoryRatings.Add("OD", Attributes.OverallDifficulty);
                categoryRatings.Add("AR", Attributes.ApproachRate);
                categoryRatings.Add("Max Combo", beatmapMaxCombo);
            }

            return totalValue;
        }

        private double interpComboStarRating(IList<double> values, double scoreCombo, double mapCombo)
        {
            if (mapCombo == 0)
            {
                return values.Last();
            }

            double comboRatio = scoreCombo / mapCombo;
            double pos = Math.Min(comboRatio * (values.Count), values.Count);
            int i = (int)pos;

            if (i == values.Count)
            {
                return values.Last();
            }

            if (pos <= 0)
            {
                return 0;
            }

            double ub = values[i];
            double lb = i == 0 ? 0 : values[i - 1];

            double t = pos - i;
            double ret = lb * (1 - t) + ub * t;

            return ret;
        }

        private double interpMissCountStarRating(double sr, IList<double> values, int missCount)
        {
            double increment = Attributes.MissStarRatingIncrement;
            double t;

            if (missCount == 0)
            {
                // zero misses, return SR
                return sr;
            }

            if (missCount < values[0])
            {
                return sr - increment * missCount / values[0];
            }

            for (int i = 0; i < values.Count; ++i)
            {
                if (missCount == values[i])
                {
                    if (i < values.Count - 1 && missCount == values[i + 1])
                    {
                        // if there are duplicates, take the lowest SR that can achieve miss count
                        continue;
                    }

                    return sr - (i + 1) * increment;
                }

                if (i < values.Count - 1 && missCount < values[i + 1])
                {
                    t = (missCount - values[i]) / (values[i + 1] - values[i]);

                    return sr - (i + 1 + t) * increment;
                }
            }

            // more misses than max evaluated, interpolate to zero
            t = (missCount - values.Last()) / (beatmapMaxCombo - values.Last());
            return (sr - values.Count * increment) * (1 - t);
        }

        private double computeJumpAimValue(Dictionary<string, double> categoryRatings = null)
        {
            double jumpAimComboStarRating = interpComboStarRating(Attributes.JumpAimComboStarRatings, scoreMaxCombo, beatmapMaxCombo);
            double jumpAimMissCountStarRating = interpMissCountStarRating(Attributes.JumpAimComboStarRatings.Last(), Attributes.JumpAimMissCounts, countMiss);
            double rawJumpAim = Math.Pow(jumpAimComboStarRating, combo_weight) * Math.Pow(jumpAimMissCountStarRating, 1 - combo_weight);

            if (mods.Any(m => m is OsuModTouchDevice))
                rawJumpAim = Math.Pow(rawJumpAim, 0.8);

            double jumpAimValue = Math.Pow(5.0f * Math.Max(1.0f, rawJumpAim / 0.0675f) - 4.0f, 3.0f) / 100000.0f;

            // Penalize misses exponentially.
            jumpAimValue *= Math.Pow(0.95, countMiss);

            double approachRateFactor = 1.0f;

            if (Attributes.ApproachRate > 10.33f)
                approachRateFactor += 0.3f * (Attributes.ApproachRate - 10.33f);
            else if (Attributes.ApproachRate < 8.0f)
                approachRateFactor += 0.01f * (8.0f - Attributes.ApproachRate);

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

            if (categoryRatings != null)
            {
                categoryRatings.Add("Jump Aim Combo Stars", jumpAimComboStarRating);
                categoryRatings.Add("Jump Aim Miss Count Stars", jumpAimMissCountStarRating);
            }

            return jumpAimValue;
        }

        private double computeStreamAimValue(Dictionary<string, double> categoryRatings = null)
        {
            double streamAimComboStarRating = interpComboStarRating(Attributes.StreamAimComboStarRatings, scoreMaxCombo, beatmapMaxCombo);
            double streamAimMissCountStarRating = interpMissCountStarRating(Attributes.StreamAimComboStarRatings.Last(), Attributes.StreamAimMissCounts, countMiss);
            double rawStreamAim = Math.Pow(streamAimComboStarRating, combo_weight) * Math.Pow(streamAimMissCountStarRating, 1 - combo_weight);

            if (mods.Any(m => m is OsuModTouchDevice))
                rawStreamAim = Math.Pow(rawStreamAim, 1.25f);

            double streamAimValue = Math.Pow(5.0f * Math.Max(1.0f, rawStreamAim / 0.0675f) - 4.0f, 3.0f) / 100000.0f;
            
            // Penalize misses exponentially.
            streamAimValue *= Math.Pow(0.95f, countMiss);

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

            if (categoryRatings != null)
            {
                categoryRatings.Add("Stream Aim Combo Stars", streamAimComboStarRating);
                categoryRatings.Add("Stream Aim Miss Count Stars", streamAimMissCountStarRating);
            }

            return streamAimValue;
        }

        private double computeAimControlValue(Dictionary<string, double> categoryRatings = null)
        {
            double aimControlComboStarRating = interpComboStarRating(Attributes.AimControlComboStarRatings, scoreMaxCombo, beatmapMaxCombo);
            double aimControlMissCountStarRating = interpMissCountStarRating(Attributes.AimControlComboStarRatings.Last(), Attributes.AimControlMissCounts, countMiss);
            double rawAimControl = Math.Pow(aimControlComboStarRating, combo_weight) * Math.Pow(aimControlMissCountStarRating, 1 - combo_weight);

            if (mods.Any(m => m is OsuModTouchDevice))
                rawAimControl = Math.Pow(rawAimControl, 0.75f);
    
            double aimControlValue = Math.Pow(5.0f * Math.Max(1.0f, rawAimControl / 0.0675f) - 4.0f, 3.0f) / 100000.0f;

            // Penalize misses exponentially.
            aimControlValue *= Math.Pow(0.95f, countMiss);

            double approachRateFactor = 1.0f;
            if (Attributes.ApproachRate > 10.33f)
                approachRateFactor += 0.3f * (Attributes.ApproachRate - 10.33f);

            aimControlValue *= approachRateFactor;

            if (mods.Any(m => m is OsuModHidden))
                aimControlValue *= 1.0f + 0.08f * (12.0f - Attributes.ApproachRate);

            if (mods.Any(h => h is OsuModFlashlight))
            {
                // Apply object-based bonus for flashlight.
                aimControlValue *= 1.0f + 0.35f * Math.Min(1.0f, totalHits / 200.0f) +
                        (totalHits > 200 ? 0.3f * Math.Min(1.0f, (totalHits - 200) / 300.0f) +
                        (totalHits > 500 ? (totalHits - 500) / 1200.0f : 0.0f) : 0.0f);
            }

            // Scale the sim control value with accuracy
            aimControlValue *= 0.75f + accuracy / 4.0f;
            // It is important to also consider accuracy difficulty when doing that
            aimControlValue *= 0.98f + Math.Pow(Attributes.OverallDifficulty, 2) / 2500;

            if (categoryRatings != null)
            {
                categoryRatings.Add("Aim Control Combo Stars", aimControlComboStarRating);
                categoryRatings.Add("Aim Control Miss Count Stars", aimControlMissCountStarRating);
            }

            return aimControlValue;
        }

        private double computeStaminaValue(Dictionary<string, double> categoryRatings = null)
        {
            double staminaComboStarRating = interpComboStarRating(Attributes.StaminaComboStarRatings, scoreMaxCombo, beatmapMaxCombo);
            double staminaMissCountStarRating = interpMissCountStarRating(Attributes.StaminaComboStarRatings.Last(), Attributes.StaminaMissCounts, countMiss);
            double rawStamina = Math.Pow(staminaComboStarRating, combo_weight) * Math.Pow(staminaMissCountStarRating, 1 - combo_weight);
            double staminaValue = Math.Pow(5.0f * Math.Max(1.0f, rawStamina / 0.0675f) - 4.0f, 3.0f) / 100000.0f;

            // Penalize misses exponentially.
            staminaValue *= Math.Pow(0.99f, countMiss);

            // Scale with acc and OD
            double ODScale = (10.0f + Attributes.OverallDifficulty) / 20.0f;
            double accScale = Math.Pow(accuracy, 20.0f - Attributes.OverallDifficulty);
            staminaValue *= ODScale * accScale;

            if (categoryRatings != null)
            {
                categoryRatings.Add("Stamina Combo Stars", staminaComboStarRating);
                categoryRatings.Add("Stamina Miss Count Stars", staminaMissCountStarRating);
            }

            return staminaValue;
        }

        private double computeSpeedValue(Dictionary<string, double> categoryRatings = null)
        {
            double speedComboStarRating = interpComboStarRating(Attributes.SpeedComboStarRatings, scoreMaxCombo, beatmapMaxCombo);
            double speedMissCountStarRating = interpMissCountStarRating(Attributes.SpeedComboStarRatings.Last(), Attributes.SpeedMissCounts, countMiss);
            double rawSpeed = Math.Pow(speedComboStarRating, combo_weight) * Math.Pow(speedMissCountStarRating, 1 - combo_weight);

            if (mods.Any(m => m is OsuModTouchDevice))
                rawSpeed = Math.Pow(rawSpeed, 1.25f);

            double speedValue = Math.Pow(5.0f * Math.Max(1.0f, rawSpeed / 0.0675f) - 4.0f, 3.0f) / 100000.0f;

            // Penalize misses exponentially.
            speedValue *= Math.Pow(0.99f, countMiss);

            // Scale with acc and OD
            double ODScale = (10.0f + Attributes.OverallDifficulty) / 20.0f;
            double accScale = Math.Pow(accuracy, 20.0f - Attributes.OverallDifficulty);
            speedValue *= ODScale * accScale;

            if (categoryRatings != null)
            {
                categoryRatings.Add("Speed Combo Stars", speedComboStarRating);
                categoryRatings.Add("Speed Miss Count Stars", speedMissCountStarRating);
            }

            return speedValue;
        }

        private double computeFingerControlValue(Dictionary<string, double> categoryRatings = null)
        {
            double fingerControlComboStarRating = interpComboStarRating(Attributes.FingerControlComboStarRatings, scoreMaxCombo, beatmapMaxCombo);
            double fingerControlMissCountStarRating = interpMissCountStarRating(Attributes.FingerControlComboStarRatings.Last(), Attributes.FingerControlMissCounts, countMiss);
            double rawFingerControl = Math.Pow(fingerControlComboStarRating, combo_weight) * Math.Pow(fingerControlMissCountStarRating, 1 - combo_weight);

            if (mods.Any(m => m is OsuModTouchDevice))
                rawFingerControl = Math.Pow(rawFingerControl, 1.25f);

            double fingerControlValue = Math.Pow(5.0f * Math.Max(1.0f, rawFingerControl / 0.0675f) - 4.0f, 3.0f) / 100000.0f;

            double approachRateFactor = 1.0f;
            if (Attributes.ApproachRate > 10.33f)
                approachRateFactor += 0.3f * (Attributes.ApproachRate - 10.33f);

            fingerControlValue *= approachRateFactor;

            // Penalize misses exponentially.
            fingerControlValue *= Math.Pow(0.99f, countMiss);

            // Scale with acc and OD
            double ODScale = (10.0f + Attributes.OverallDifficulty) / 20.0f;
            double accScale = Math.Pow(accuracy, 20.0f - Attributes.OverallDifficulty);
            fingerControlValue *= ODScale * accScale;

            if (categoryRatings != null)
            {
                categoryRatings.Add("Finger Control Combo Stars", fingerControlComboStarRating);
                categoryRatings.Add("Finger Control Miss Count Stars", fingerControlMissCountStarRating);
            }

            return fingerControlValue;
        }

        private double computeAccuracyValue(Dictionary<string, double> categoryRatings = null)
        {
            double sigmaCircle = 0;
            double sigmaSlider = 0;
            double sigmaTotal = 0;

            double zScore = 2.58f;
            double sqrt2 = Math.Sqrt(2.0f);
            double accMultiplier = 800.0f;
            double accScale = 1.25f;

            double circleAccuracy = 0;
            if (countHitCircles > 0) circleAccuracy = Math.Max(0.0f, 1.0f - (1.0f - accuracy) * totalHits / countHitCircles);

            // Slider sigma calculations
            if (countSliders > 0)
            {
                double sliderConst = Math.Sqrt(2.0f / countSliders) * zScore;
                double sliderProbability = (2.0f * accuracy + Math.Pow(sliderConst, 2.0f) - sliderConst * Math.Sqrt(4.0f * accuracy + Math.Pow(sliderConst, 2.0f) - 4.0f * Math.Pow(accuracy, 2.0f))) / (2.0f  + 2.0f * Math.Pow(sliderConst, 2.0f));
                sigmaSlider = (199.5f - 10.0f * Attributes.OverallDifficulty) / (sqrt2 * SpecialFunctions.ErfInv(sliderProbability));
            }
            
            // Circle sigma calculations
            if (countHitCircles > 0)
            {
                double circleConst = Math.Sqrt(2.0f / countHitCircles) * zScore;
                double circleProbability = (2.0f * circleAccuracy + Math.Pow(circleConst, 2.0f) - circleConst * Math.Sqrt(4.0f * circleAccuracy + Math.Pow(circleConst, 2.0f) - 4.0f * Math.Pow(circleAccuracy, 2.0f))) / (2.0f  + 2.0f * Math.Pow(circleConst, 2.0f));
                sigmaCircle = (79.5f - 6.0f * Attributes.OverallDifficulty) / (sqrt2 * SpecialFunctions.ErfInv(circleProbability));
            }

            if (sigmaSlider == 0) return accMultiplier * Math.Pow(accScale, -sigmaCircle);
            if (sigmaCircle == 0) return accMultiplier * Math.Pow(accScale, -sigmaSlider);

            sigmaTotal = 1.0f / (1.0f / sigmaCircle + 1.0f / sigmaSlider);

            return accMultiplier * Math.Pow(accScale, -sigmaTotal);
        }

        private double totalHits => countGreat + countGood + countMeh + countMiss;
        private double totalSuccessfulHits => countGreat + countGood + countMeh;
    }
}
