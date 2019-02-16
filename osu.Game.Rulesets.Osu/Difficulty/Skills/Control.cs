// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// Represents the skill required to stay in control of your movement and tapping.
    /// </summary>
    public class Control : Skill
    {
        protected override double SkillMultiplier => 5000;
        protected override double StrainDecayBase => 0.35;
        private const double time_scale_factor = 50;
        private const double pattern_variety_scale = 2.0;
        private const double time_variety_scale = 0.8;
        protected override double StrainValueOf(OsuDifficultyHitObject current)
        {
            double calculateDistance(OsuDifficultyHitObject obj) => obj.JumpDistance + obj.TravelDistance;

            if (Previous.Count > 1) 
            {
                // Cursor velocity calc
                double currCursorVel = calculateDistance(current) / current.DeltaTime;
                double prevCursorVel = calculateDistance(Previous[0]) / Previous[0].DeltaTime;
                double prevprevCursorVel = calculateDistance(Previous[1]) / Previous[1].DeltaTime;

                double diffVel = Math.Abs(currCursorVel - prevCursorVel);
                double prevDiffVel = Math.Abs(prevCursorVel - prevprevCursorVel);

                double avgVel = (currCursorVel + prevCursorVel) / 2.0;
                double prevAvgVel = (prevprevCursorVel + prevCursorVel) / 2.0;

                double currVelChange = avgVel != 0 ? diffVel / avgVel : 0;
                double prevVelChange = prevAvgVel != 0 ? prevDiffVel / prevAvgVel : 0;
                double totalVelChange = currVelChange * prevVelChange;

                // Distance calc
                double distDiff = Math.Abs(calculateDistance(current) - calculateDistance(Previous[0]));
                double prevDistDiff = Math.Abs(calculateDistance(Previous[0]) - calculateDistance(Previous[1]));

                double avgDist = (calculateDistance(current) + calculateDistance(Previous[0])) / 2.0;
                double prevAvgDist = (calculateDistance(Previous[0]) + calculateDistance(Previous[1])) / 2.0;

                double currDistChange = avgDist != 0 ? distDiff / avgDist : 0;
                double prevDistChange = prevAvgDist != 0 ? prevDistDiff / prevAvgDist : 0;
                double totalDistChange = currDistChange * prevDistChange;

                // Angle calc
                double angleScale = 0;
                double angleStdDev = 0;

                if (current.Angle != null && Previous[0].Angle != null)
                {
                    double maxStdDev;
                    if (Previous[1].Angle != null)
                    {
                        double angleAvg = (current.Angle.Value + Previous[0].Angle.Value + Previous[1].Angle.Value) / 3.0;
                        angleStdDev = Math.Sqrt((Math.Pow(current.Angle.Value - angleAvg, 2.0) + Math.Pow(Previous[0].Angle.Value - angleAvg, 2.0) + Math.Pow(Previous[1].Angle.Value - angleAvg, 2.0)) / 2.0);
                        maxStdDev = Math.Sqrt((2 * Math.Pow(Math.PI / 3.0, 2.0) + Math.Pow(Math.PI - Math.PI / 3.0, 2.0)) / 2.0);
                    } else
                    {
                        double angleAvg = (current.Angle.Value + Previous[0].Angle.Value) / 2.0;
                        angleStdDev = Math.Sqrt(Math.Pow(current.Angle.Value - angleAvg, 2.0) + Math.Pow(Previous[0].Angle.Value - angleAvg, 2.0));
                        maxStdDev = Math.Sqrt(2 * Math.Pow(Math.PI / 3.0, 2.0) + Math.Pow(Math.PI - Math.PI / 3.0, 2.0));
                    }
                    angleScale = Math.Pow(angleStdDev / maxStdDev, 2.0);
                }

                // Slider calc
                double sliderChange = 0;

                if (current.BaseObject is Slider currentSlider && Previous[0].BaseObject is Slider previousSlider)
                {
                    double sliderDiff = Math.Abs(currentSlider.Velocity - previousSlider.Velocity);
                    double sliderAvg = (currentSlider.Velocity + previousSlider.Velocity) / 2.0;
                    sliderChange = sliderAvg != 0 ? 1.5 * sliderDiff / sliderAvg : 0;
                }

                // Time calc
                double timeDiff = Math.Abs(current.DeltaTime - Previous[0].DeltaTime);
                double prevtimeDiff = Math.Abs(Previous[0].DeltaTime - Previous[1].DeltaTime);

                double timeAvg = (current.DeltaTime + Previous[0].DeltaTime) / 2.0;
                double prevTimeAvg = (Previous[0].DeltaTime + Previous[1].DeltaTime) / 2.0;

                double currTimeChange = timeAvg != 0 ? timeDiff / timeAvg : 0;
                double prevTimeChange = prevTimeAvg != 0 ? prevtimeDiff / prevTimeAvg : 0;

                // Unorthodox rhythm gives higher values
                currTimeChange = 2.0 * Math.Exp(-1.5 * currTimeChange) * Math.Pow(Math.Sin(4.0 * Math.PI / (currTimeChange - 2.0)), 2.0);
                prevTimeChange = 2.0 * Math.Exp(-1.5 * prevTimeChange) * Math.Pow(Math.Sin(4.0 * Math.PI / (prevTimeChange - 2.0)), 2.0);

                double totalTimeChange = Math.Max(currTimeChange, prevTimeChange);

                // Apply dec. multipliers to values for non-constant rhythm/stacks
                double stackScale = Math.Min(1.0, Math.Min(Math.Pow((calculateDistance(current)) / 100, 2.0), Math.Pow((calculateDistance(Previous[0])) / 100, 2.0)));
                double timeScale = time_scale_factor / (time_scale_factor + (timeDiff + prevtimeDiff) / 2.0);

                // Final values
                double patternResult = timeMultiplier(current) * stackScale * pattern_variety_scale * timeScale * angleScale * Math.Sqrt(totalVelChange * totalDistChange);
                double timeResult = Math.Max(timeMultiplier(current), timeMultiplier(Previous[0])) * Math.Pow(totalTimeChange, time_variety_scale);

                /*Console.WriteLine("---");
                Console.WriteLine("Object placed: " + current.BaseObject.StartTime);
                Console.WriteLine("timeDiff: " + timeDiff);
                Console.WriteLine("prevtimeDiff: "+ prevtimeDiff);
                Console.WriteLine("timeAvg: " + timeAvg);
                Console.WriteLine("prevTimeAvg: "+ prevTimeAvg);
                Console.WriteLine("currTimeChange: " + currTimeChange);
                Console.WriteLine("prevTimeChange: "+ prevTimeChange);
                Console.WriteLine("totalTimeChange: " + totalTimeChange);
                Console.WriteLine("stackScale: " + stackScale);
                Console.WriteLine("pattern_variety_scale: " + pattern_variety_scale);
                Console.WriteLine("timeScale: " + timeScale);
                Console.WriteLine("angleScale: " + angleScale);
                Console.WriteLine("totalVelChange: " + totalVelChange);
                Console.WriteLine("totalDistChange: " + totalDistChange);
                Console.WriteLine("patternResult: " + patternResult);
                Console.WriteLine("timeResult: " + timeResult);*/

                /*var document = new Document(
                    new Span("---"),
                    '\n',
                    new Span("Object placed".PadRight(15) + $": {current.BaseObject.StartTime}"),
                    '\n',
                    new Span("Cursor Velocity Change".PadRight(15) + $": {totalVelChange}"),
                    '\n',
                    new Span("Distance Change".PadRight(15) + $": {totalDistChange}"),
                    '\n',
                    new Span("Angle Change".PadRight(15) + $": {angleStdDev}"),
                    '\n',
                    new Span("Time Change".PadRight(15) + $": {totalTimeChange}"),
                    '\n',
                    new Span("Slider Change".PadRight(15) + $": {sliderChange}"),
                    '\n',
                    new Span("Total Control value".PadRight(15) + $": {timeMultiplier(current) * (stackMultiplier * Math.Pow(totalVelChange * totalDistChange * angleStdDev, 0.25)) / Math.Min(current.StrainTime, Previous[0].StrainTime) + Math.Sqrt(totalTimeChange) / Math.Max(current.StrainTime, Previous[0].StrainTime) + sliderChange / Math.Min(current.StrainTime, Previous[0].StrainTime)}"),
                    '\n',
                    '\n'
                );
                using (var writer = new StringWriter())
                {
                    ConsoleRenderer.RenderDocumentToText(document, new TextRenderTarget(writer));
                    var str = writer.GetStringBuilder().ToString();
                    var lines = str.Split('\n');
                    for (int i = 0; i < lines.Length; i++)
                        lines[i] = lines[i].TrimEnd();
                    str = string.Join('\n'.ToString(), lines);
                    Console.Write(str);
                    File.AppendAllText(@"A:\Users\oykxf\Documents\osu-tools\objects.txt", str);
                }*/

                return (patternResult + timeResult + sliderChange) / Math.Min(current.StrainTime, Previous[0].StrainTime);            
            } else return 0;
        }
    }
}
