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
        private const double pattern_variety_scale = 12.0;
        private const double time_variety_scale = 4.0;
        private const double weight = 0.375;
        protected override double StrainValueOf(DifficultyHitObject current)
        {
            if (current.BaseObject is Spinner)
                return 0;

            var osuCurrent = (OsuDifficultyHitObject)current;

            double calculateDistance(OsuDifficultyHitObject obj) => obj.JumpDistance + obj.TravelDistance;
            double angleTransform(double angle) => 1.0 - Math.Sin(2.0 * Math.Pow(angle, 0.4));

            if (Previous.Count > 1) 
            {
                var osuPrevious = (OsuDifficultyHitObject)Previous[0];
                var osuPreviousPrevious = (OsuDifficultyHitObject)Previous[1];

                // Cursor velocity change calc
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

                // Distance change calc
                double distDiff = Math.Abs(calculateDistance(osuCurrent) - calculateDistance(osuPrevious));
                double prevDistDiff = Math.Abs(calculateDistance(osuPrevious) - calculateDistance(osuPreviousPrevious));

                double avgDist = (calculateDistance(osuCurrent) + calculateDistance(osuPrevious)) / 2.0;
                double prevAvgDist = (calculateDistance(osuPrevious) + calculateDistance(osuPreviousPrevious)) / 2.0;

                double currDistChange = avgDist != 0 ? distDiff / avgDist : 0;
                double prevDistChange = prevAvgDist != 0 ? prevDistDiff / prevAvgDist : 0;
                double totalDistChange = Math.Sqrt(currDistChange * prevDistChange);

                // Angle scale calc
                double angleScale = 0;
                double currAngle = 0;
                double prevAngle = 0;
                double prevPrevAngle = 0;
                double angleResult = 0;

                if (osuCurrent.Angle != null && osuPrevious.Angle != null)
                {
                    double angleStdDev;
                    double maxStdDev;
                    currAngle = osuCurrent.Angle.Value;
                    prevAngle = osuPrevious.Angle.Value;
                    if (osuPreviousPrevious.Angle != null)
                    {
                        prevPrevAngle = osuPreviousPrevious.Angle.Value;

                        double angleAvg = (currAngle + prevAngle + prevPrevAngle) / 3.0;
                        angleStdDev = Math.Sqrt((Math.Pow(currAngle - angleAvg, 2.0) + Math.Pow(prevAngle - angleAvg, 2.0) + Math.Pow(prevPrevAngle - angleAvg, 2.0)) / 2.0);
                        maxStdDev = Math.Sqrt((2.0 * Math.Pow(Math.PI / 3.0, 2.0) + Math.Pow(Math.PI - Math.PI / 3.0, 2.0)) / 2.0);

                        angleScale = angleTransform((currAngle + prevAngle + prevPrevAngle) / 3.0);
                    } else
                    {
                        double angleAvg = (currAngle + prevAngle) / 2.0;
                        angleStdDev = Math.Sqrt(Math.Pow(currAngle - angleAvg, 2.0) + Math.Pow(prevAngle - angleAvg, 2.0));
                        maxStdDev = Math.Sqrt(2.0 * Math.Pow(Math.PI / 3.0, 2.0) + Math.Pow(Math.PI - Math.PI / 3.0, 2.0));

                        angleScale = angleTransform((currAngle + prevAngle) / 2.0);
                    }
                    double angleWeight = Math.Pow(1.0 - angleStdDev / maxStdDev, 2.0);
                    angleResult = (1.0 - angleWeight) * (1.0 - 0.7 * Math.Pow(2.0 * angleWeight - 1.1, 2.0)) + angleWeight * angleScale;
                }

                // Time calc
                double currMinStrain = Math.Min(osuCurrent.StrainTime, osuPrevious.StrainTime);
                double currMaxStrain = Math.Max(osuCurrent.StrainTime, osuPrevious.StrainTime);
                double currMod = currMaxStrain % currMinStrain;
                double currRoot1 = currMaxStrain - currMod;
                double currRoot2 = currMaxStrain + currMinStrain - currMod;
                double currTimeVal = Math.Pow(- 4.0 * (currMaxStrain - currRoot1) * (currMaxStrain - currRoot2) / Math.Pow(currMinStrain, 2.0), 7.0);

                double prevMinStrain = Math.Min(osuPreviousPrevious.StrainTime, osuPrevious.StrainTime);
                double prevMaxStrain = Math.Max(osuPreviousPrevious.StrainTime, osuPrevious.StrainTime);
                double prevMod = prevMaxStrain % prevMinStrain;
                double prevRoot1 = prevMaxStrain - prevMod;
                double prevRoot2 = prevMaxStrain + prevMinStrain - prevMod;
                double prevTimeVal = Math.Pow(- 4.0 * (prevMaxStrain - prevRoot1) * (prevMaxStrain - prevRoot2) / Math.Pow(prevMinStrain, 2.0), 7.0);

                double timeDiff = Math.Abs(osuCurrent.StrainTime - osuPrevious.StrainTime);
                double prevtimeDiff = Math.Abs(osuPrevious.StrainTime - osuPreviousPrevious.StrainTime);
				
                double totalTimeChange = currTimeVal + prevTimeVal;

                // Slider calc
                double sliderChange = 0;

                if (current.BaseObject is Slider currentSlider && osuPrevious.BaseObject is Slider previousSlider)
                {
                    double sliderDiff = Math.Abs(currentSlider.Velocity - previousSlider.Velocity);
                    double sliderAvg = (currentSlider.Velocity + previousSlider.Velocity) / 2.0;
                    sliderChange = sliderAvg != 0 ? sliderDiff / sliderAvg : 0;
                }

                // Pattern Result Multipliers
                double spacingScale = Math.Min(1.0, Math.Min(Math.Pow((calculateDistance(osuCurrent)) / 100.0, 3.0), Math.Pow((calculateDistance(osuPrevious)) / 100.0, 3.0)));
                double timeScale = time_scale_factor / (time_scale_factor + (timeDiff + prevtimeDiff) / 2.0);

                // Final values
                double sliderResult = Math.Min(timeMultiplier(osuCurrent), timeMultiplier(osuPrevious)) * (1.0 - timeScale) * sliderChange;
                double patternResult = pattern_variety_scale * timeMultiplier(osuCurrent) * spacingScale * timeScale * angleResult * Math.Sqrt(totalVelChange * totalDistChange);
                double timeResult = time_variety_scale * Math.Pow(Math.Min(timeMultiplier(osuCurrent), timeMultiplier(osuPrevious)), 2.0) * totalTimeChange;

                /*Console.WriteLine("---");
                Console.WriteLine("Object placed: " + osuCurrent.BaseObject.StartTime);
                Console.WriteLine("currTimeVal: " + currTimeVal);
                Console.WriteLine("prevTimeVal: " + prevTimeVal);
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
                Console.WriteLine("patternResult: " + patternResult);
                Console.WriteLine("timeResult: " + timeResult);*/

                return (weight * patternResult + (1.0 - weight) * timeResult + sliderResult) / Math.Min(osuCurrent.StrainTime, osuPrevious.StrainTime);            
            } else return 0;
        }
    }
}