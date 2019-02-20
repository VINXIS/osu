// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
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

            if (Previous.Count > 1) 
            {
                var osuPrevious = (OsuDifficultyHitObject)Previous[0];
                var osuPreviousPrevious = (OsuDifficultyHitObject)Previous[1];

                // Cursor velocity calc
                double currCursorVel = calculateDistance(current) / current.StrainTime;
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
                double distDiff = Math.Abs(calculateDistance(current) - calculateDistance(osuPrevious));
                double prevDistDiff = Math.Abs(calculateDistance(osuPrevious) - calculateDistance(osuPreviousPrevious));

                double avgDist = (calculateDistance(current) + calculateDistance(osuPrevious)) / 2.0;
                double prevAvgDist = (calculateDistance(osuPrevious) + calculateDistance(osuPreviousPrevious)) / 2.0;

                double currDistChange = avgDist != 0 ? distDiff / avgDist : 0;
                double prevDistChange = prevAvgDist != 0 ? prevDistDiff / prevAvgDist : 0;
                double totalDistChange = Math.Sqrt(currDistChange * prevDistChange);

                // Angle calc
                double angleScale = 0;

                if (current.Angle != null && osuPrevious.Angle != null)
                {
                    double angleStdDev;
                    double maxStdDev;
                    if (osuPreviousPrevious.Angle != null)
                    {
                        double angleAvg = (current.Angle.Value + osuPrevious.Angle.Value + osuPreviousPrevious.Angle.Value) / 3.0;
                        angleStdDev = Math.Sqrt((Math.Pow(current.Angle.Value - angleAvg, 2.0) + Math.Pow(osuPrevious.Angle.Value - angleAvg, 2.0) + Math.Pow(osuPreviousPrevious.Angle.Value - angleAvg, 2.0)) / 2.0);
                        maxStdDev = Math.Sqrt((2.0 * Math.Pow(Math.PI / 3.0, 2.0) + Math.Pow(Math.PI - Math.PI / 3.0, 2.0)) / 2.0);
                    } else
                    {
                        double angleAvg = (current.Angle.Value + osuPrevious.Angle.Value) / 2.0;
                        angleStdDev = Math.Sqrt(Math.Pow(current.Angle.Value - angleAvg, 2.0) + Math.Pow(osuPrevious.Angle.Value - angleAvg, 2.0));
                        maxStdDev = Math.Sqrt(2.0 * Math.Pow(Math.PI / 3.0, 2.0) + Math.Pow(Math.PI - Math.PI / 3.0, 2.0));
                    }
                    angleScale = Math.Pow(1.0 - angleStdDev / maxStdDev, 2.0);
                    angleScale = 1.0 - 0.7 * Math.Pow(2.0 * angleScale - 1.1, 2.0);
                }

                // Time calc
                double timeDiff = Math.Abs(current.StrainTime - osuPrevious.StrainTime);
                double prevtimeDiff = Math.Abs(osuPrevious.StrainTime - osuPreviousPrevious.StrainTime);

                double timeAvg = (current.StrainTime + osuPrevious.StrainTime) / 2.0;
                double prevTimeAvg = (osuPrevious.StrainTime + osuPreviousPrevious.StrainTime) / 2.0;
                
                double currTimeChange = timeAvg != 0 ? timeDiff / timeAvg : 0;
                double prevTimeChange = prevTimeAvg != 0 ? prevtimeDiff / prevTimeAvg : 0;

                // Unorthodox rhythm gives higher values
                currTimeChange = sinusoid(currTimeChange);
                prevTimeChange = sinusoid(prevTimeChange);
				
                double totalTimeChange = Math.Abs(currTimeChange - prevTimeChange);

                // Slider calc
                double sliderChange = 0;

                if (current.BaseObject is Slider currentSlider && osuPrevious.BaseObject is Slider previousSlider)
                {
                    double sliderDiff = Math.Abs(currentSlider.Velocity - previousSlider.Velocity);
                    double sliderAvg = (currentSlider.Velocity + previousSlider.Velocity) / 2.0;
                    sliderChange = sliderAvg != 0 ? 1.5 * sliderDiff / sliderAvg : 0;
                }

                // Apply dec. multipliers to values for non-constant rhythm/stacks
                double stackScale = Math.Min(1.0, Math.Min(Math.Pow((calculateDistance(current)) / 100.0, 2.0), Math.Pow((calculateDistance(osuPrevious)) / 100.0, 2.0)));
                double timeScale = time_scale_factor / (time_scale_factor + (timeDiff + prevtimeDiff) / 2.0);

                // Final values
                double sliderResult = timeMultiplier(current) * (1.0 - timeScale) * sliderChange;
                double patternResult = pattern_variety_scale * timeMultiplier(current) * stackScale * timeScale * angleScale * Math.Sqrt(totalVelChange * totalDistChange);
                double timeResult = Math.Max(timeMultiplier(current), timeMultiplier(osuPrevious)) * Math.Pow(totalTimeChange, time_variety_scale);

                /*Console.WriteLine("---");
                Console.WriteLine("Object placed: " + current.BaseObject.StartTime);
                Console.WriteLine("totalVelChange: " + totalVelChange);
                Console.WriteLine("totalDistChange: " + totalDistChange);
                Console.WriteLine("pattern_variety_scale: " + pattern_variety_scale);
                Console.WriteLine("timeMultiplier(current): " + timeMultiplier(current));
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
                Console.WriteLine("timeResult: " + timeResult);
                Console.WriteLine("stackScale: " + stackScale);
                Console.WriteLine("angleScale: " + angleScale);
                Console.WriteLine("totalVelChange: " + totalVelChange);
                Console.WriteLine("totalDistChange: " + totalDistChange);
                Console.WriteLine("patternResult: " + patternResult);
                Console.WriteLine("timeResult: " + timeResult);*/

                return (weight * patternResult + (1.0 - weight) * timeResult + sliderResult) / Math.Min(current.StrainTime, osuPrevious.StrainTime);            
            } else return 0;
        }
    }
}