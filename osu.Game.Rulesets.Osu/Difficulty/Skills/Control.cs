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
        private const double pattern_variety_scale = 10.0;
        private const double time_variety_scale = 12.0;
        private const double weight = 0.45;
        protected override double StrainValueOf(DifficultyHitObject current)
        {
            if (current.BaseObject is Spinner)
                return 0;

            if (Previous.Count > 0) 
            {
                double calculateDistance(OsuDifficultyHitObject obj) => obj.JumpDistance + obj.TravelDistance;
                double angleTransform(double angle) => 1.0 - Math.Sin(2.0 * Math.Pow(angle, 0.4));

                var osuCurrent = (OsuDifficultyHitObject)current;
                var osuPrevious = (OsuDifficultyHitObject)Previous[0];

                // Cursor velocity change calc
                double currCursorVel = calculateDistance(osuCurrent) / osuCurrent.StrainTime;
                double prevCursorVel = calculateDistance(osuPrevious) / osuPrevious.StrainTime;

                double diffVel = Math.Abs(currCursorVel - prevCursorVel);
                double avgVel = (currCursorVel + prevCursorVel) / 2.0;

                double velChange = avgVel != 0 ? diffVel / avgVel : 0;

                // Distance change calc
                double distDiff = Math.Abs(calculateDistance(osuCurrent) - calculateDistance(osuPrevious));
                double avgDist = (calculateDistance(osuCurrent) + calculateDistance(osuPrevious)) / 2.0;

                double distChange = avgDist != 0 ? distDiff / avgDist : 0;

                // Angle scale calc
                double angleResult = 0;

                if (osuCurrent.Angle != null && osuPrevious.Angle != null)
                {
                    double currAngle = osuCurrent.Angle.Value;
                    double prevAngle = osuPrevious.Angle.Value;

                    double angleAvg = (currAngle + prevAngle) / 2.0;
                    double angleStdDev = Math.Sqrt(Math.Pow(currAngle - angleAvg, 2.0) + Math.Pow(prevAngle - angleAvg, 2.0));
                    double maxStdDev = Math.Sqrt(2.0 * Math.Pow(Math.PI / 3.0, 2.0) + Math.Pow(Math.PI - Math.PI / 3.0, 2.0));
                    double angleWeight = Math.Pow(1.0 - angleStdDev / maxStdDev, 2.0);

                    double angleScale = angleTransform((currAngle + prevAngle) / 2.0);
                    angleResult = (1.0 - angleWeight) * (1.0 - 0.7 * Math.Pow(2.0 * angleWeight - 1.1, 2.0)) + angleWeight * angleScale;
                }

                // Time calc
                double currMinStrain = Math.Min(osuCurrent.StrainTime, osuPrevious.StrainTime);
                double currMaxStrain = Math.Max(osuCurrent.StrainTime, osuPrevious.StrainTime);
                double currMod = currMaxStrain % currMinStrain;
                double currRoot1 = currMaxStrain - currMod;
                double currRoot2 = currMaxStrain + currMinStrain - currMod;
                double timeChange = Math.Pow(- 4.0 * (currMaxStrain - currRoot1) * (currMaxStrain - currRoot2) / Math.Pow(currMinStrain, 2.0), 7.0);

                double timeDiff = Math.Abs(osuCurrent.StrainTime - osuPrevious.StrainTime);

                // Slider calc
                double sliderChange = 0;

                if (current.BaseObject is Slider currentSlider && osuPrevious.BaseObject is Slider previousSlider)
                {
                    double sliderDiff = Math.Abs(currentSlider.Velocity - previousSlider.Velocity);
                    double sliderAvg = (currentSlider.Velocity + previousSlider.Velocity) / 2.0;
                    sliderChange = sliderAvg != 0 ? sliderDiff / sliderAvg : 0;
                }

                // Pattern Result Multipliers
                double spacingScale = Math.Min(1.0, Math.Min(Math.Pow((calculateDistance(osuCurrent)) / 100.0, 2.0), Math.Pow((calculateDistance(osuPrevious)) / 100.0, 2.0)));
                double timeScale = time_scale_factor / (time_scale_factor + timeDiff);

                // Final values
                double sliderResult = Math.Min(timeMultiplier(osuCurrent), timeMultiplier(osuPrevious)) * (1.0 - timeScale) * sliderChange;
                double patternResult = pattern_variety_scale * timeMultiplier(osuCurrent) * spacingScale * timeScale * angleResult * Math.Sqrt(velChange * distChange);
                double timeResult = time_variety_scale * Math.Pow(Math.Min(timeMultiplier(osuCurrent), timeMultiplier(osuPrevious)), 2.0) * timeChange;

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