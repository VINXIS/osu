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
    /// Represents the skill required to adjust your movement and tapping to varying spacing and rhythms in short periods of time.
    /// </summary>
    public class Control : Skill
    {
        protected override double SkillMultiplier => 5000;
        protected override double StrainDecayBase => 0.3;
        private const double time_scale_factor = 20.0;
        private const double pattern_variety_scale = 8.0;
        private const double time_variety_scale = 0.8;
        private const double weight = 0.375;
        protected override double StrainValueOf(DifficultyHitObject current)
        {
            if (current.BaseObject is Spinner)
                return 0;

            var osuCurrent = (OsuDifficultyHitObject)current;

            double calculateDistance(OsuDifficultyHitObject obj) => obj.JumpDistance + obj.TravelDistance;
            double angleTransform(double angle) => Math.Pow((Math.Sin(3.0 * angle / 2.0 + Math.PI) + 3.0) / 4.0, 2.5);
            double rhythmStep(double interval) => Math.Ceiling(2.0 * interval) / 2.0;

            if (Previous.Count > 1) 
            {
                var osuPrevious = (OsuDifficultyHitObject)Previous[0];
                var osuPreviousPrevious = (OsuDifficultyHitObject)Previous[1];

                // Cursor velocity calc
                double currCursorVel = calculateDistance(osuCurrent) / osuCurrent.StrainTime;
                double prevCursorVel = calculateDistance(osuPrevious) / osuPrevious.StrainTime;
                double prevprevCursorVel = calculateDistance(osuPreviousPrevious) / osuPreviousPrevious.StrainTime;

                double diffVel = Math.Abs(currCursorVel - prevCursorVel);
                double prevDiffVel = Math.Abs(prevCursorVel - prevprevCursorVel);

                double avgVel = (currCursorVel + prevCursorVel) / 2.0;
                double prevAvgVel = (prevprevCursorVel + prevCursorVel) / 2.0;

                double currVelChange = avgVel != 0 ? diffVel / avgVel : 0;
                double prevVelChange = prevAvgVel != 0 ? prevDiffVel / prevAvgVel : 0;
                double totalVelChange = Math.Sqrt(currVelChange * prevVelChange);

                // Distance calc
                double distDiff = Math.Abs(calculateDistance(osuCurrent) - calculateDistance(osuPrevious));
                double prevDistDiff = Math.Abs(calculateDistance(osuPrevious) - calculateDistance(osuPreviousPrevious));

                double avgDist = (calculateDistance(osuCurrent) + calculateDistance(osuPrevious)) / 2.0;
                double prevAvgDist = (calculateDistance(osuPrevious) + calculateDistance(osuPreviousPrevious)) / 2.0;

                double currDistChange = avgDist != 0 ? distDiff / avgDist : 0;
                double prevDistChange = prevAvgDist != 0 ? prevDistDiff / prevAvgDist : 0;
                double totalDistChange = Math.Sqrt(currDistChange * prevDistChange);

                // Angle calc
                double angleScale = 0;
                double currAngle = 0;
                double prevAngle = 0;
                double prevPrevAngle = 0;

                if (osuCurrent.Angle != null && osuPrevious.Angle != null)
                {
                    currAngle = angleTransform(osuCurrent.Angle.Value);
                    prevAngle = angleTransform(osuPrevious.Angle.Value);
                    if (osuPreviousPrevious.Angle != null)
                    {
                        prevPrevAngle = angleTransform(osuPreviousPrevious.Angle.Value);
                        angleScale = (currAngle + prevAngle + prevPrevAngle) / 3.0;
                    } else
                    {
                        angleScale = (currAngle + prevAngle) / 2.0;
                    }
                }

                // Time calc
                double timeDiff = Math.Abs(osuCurrent.StrainTime - osuPrevious.StrainTime);
                double prevtimeDiff = Math.Abs(osuPrevious.StrainTime - osuPreviousPrevious.StrainTime);

                double timeRatio = rhythmStep(osuCurrent.StrainTime > osuPrevious.StrainTime ? osuCurrent.StrainTime / osuPrevious.StrainTime : osuPrevious.StrainTime / osuCurrent.StrainTime);
                double prevTimeRatio = rhythmStep(osuPreviousPrevious.StrainTime > osuPrevious.StrainTime ? osuPreviousPrevious.StrainTime / osuPrevious.StrainTime : osuPrevious.StrainTime / osuPreviousPrevious.StrainTime);

                if (timeRatio % 1 != 0) timeRatio = 1;
                else timeRatio = 0;

                if (prevTimeRatio % 1 != 0) prevTimeRatio = 1;
                else prevTimeRatio = 0;
				
                double totalTimeChange = timeRatio + prevTimeRatio;

                // Slider calc
                double sliderChange = 0;

                if (current.BaseObject is Slider currentSlider && osuPrevious.BaseObject is Slider previousSlider)
                {
                    double sliderDiff = Math.Abs(currentSlider.Velocity - previousSlider.Velocity);
                    double sliderAvg = (currentSlider.Velocity + previousSlider.Velocity) / 2.0;
                    sliderChange = sliderAvg != 0 ? 1.5 * sliderDiff / sliderAvg : 0;
                }

                // Apply dec. multipliers to values for non-constant rhythm/stacks
                double stackScale = Math.Min(1.0, Math.Min(Math.Pow((calculateDistance(osuCurrent)) / 100.0, 2.0), Math.Pow((calculateDistance(osuPrevious)) / 100.0, 2.0)));
                double timeScale = time_scale_factor / (time_scale_factor + (timeDiff + prevtimeDiff) / 2.0);

                // Final values
                double sliderResult = timeMultiplier(osuCurrent) * (1.0 - timeScale) * sliderChange;
                double patternResult = pattern_variety_scale * timeMultiplier(osuCurrent) * stackScale * timeScale * angleScale * Math.Sqrt(totalVelChange * totalDistChange);
                double timeResult = Math.Max(timeMultiplier(osuCurrent), timeMultiplier(osuPrevious)) * Math.Pow(totalTimeChange, time_variety_scale);

                /*Console.WriteLine("---");
                Console.WriteLine("Object placed: " + osuCurrent.BaseObject.StartTime);
                Console.WriteLine("timeRatio: " + timeRatio);
                Console.WriteLine("prevTimeRatio: " + prevTimeRatio);
                Console.WriteLine("timeResult: " + timeResult);
                Console.WriteLine("totalVelChange: " + totalVelChange);
                Console.WriteLine("totalDistChange: " + totalDistChange);
                Console.WriteLine("pattern_variety_scale: " + pattern_variety_scale);
                Console.WriteLine("timeMultiplier(osuCurrent): " + timeMultiplier(osuCurrent));
                Console.WriteLine("stackScale: " + stackScale);
                Console.WriteLine("timeScale: " + timeScale);
                Console.WriteLine("angleScale: " + angleScale);
                Console.WriteLine("patternResult: " + patternResult);
                Console.WriteLine("timeDiff: " + timeDiff);
                Console.WriteLine("prevtimeDiff: "+ prevtimeDiff);
                Console.WriteLine("timeAvg: " + timeAvg);
                Console.WriteLine("prevTimeAvg: "+ prevTimeAvg);
                Console.WriteLine("currTimeChange: " + currTimeChange);
                Console.WriteLine("prevTimeChange: "+ prevTimeChange);
                Console.WriteLine("totalTimeChange: " + totalTimeChange);
                Console.WriteLine("timeResult: " + timeResult);*/

                return (weight * patternResult + (1.0 - weight) * timeResult + sliderResult) / Math.Min(osuCurrent.StrainTime, osuPrevious.StrainTime);            
            } else return 0;
        }
    }
}