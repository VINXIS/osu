// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// 
    /// </summary>
    public class Control : Skill
    {
        protected override double SkillMultiplier => 40;
        protected override double StrainDecayBase => 0.45;

        protected override double StrainValueOf(DifficultyHitObject current)
        {
            if (current.BaseObject is Spinner)
                return 0;

            var osuCurrent = (OsuDifficultyHitObject)current;

            double currVel = osuCurrent.JumpDistance / osuCurrent.StrainTime;
            double sliderVel = osuCurrent.TravelTime == 0 ? 0 : osuCurrent.TravelDistance / osuCurrent.TravelTime;
            double jumpAwk = 0;
            double angleAwk = 0;
            double jumpNorm = 0;
            double angleBonus = 0;

            if (Previous.Count > 0)
            {
                var osuPrevious = (OsuDifficultyHitObject)Previous[0];
                
                double prevVel = osuPrevious.JumpDistance / osuPrevious.StrainTime;
                double geoVel = Math.Sqrt(prevVel * currVel);

                if (osuCurrent.Angle != null && osuPrevious.Angle != null)
                    angleAwk = (Math.Min(prevVel, currVel) / Math.Max(geoVel, 0.75)) * Math.Pow(Math.Sin((osuCurrent.Angle.Value - osuPrevious.Angle.Value) / 1.5), 2.0);
                
                if (osuCurrent.Angle != null && osuCurrent.Angle.Value > Math.PI / 4.0 && osuCurrent.Angle.Value < 3.0 * Math.PI / 4.0)
                    angleBonus = (Math.Min(prevVel, currVel) / Math.Max(geoVel, 0.75)) * Math.Pow(Math.Sin(osuCurrent.Angle.Value - (Math.PI / 4.0)), 2.0);

                if (Math.Abs(currVel - prevVel) > Math.Max(geoVel, 0.75))
                    jumpAwk = 1.0;
                else 
                    jumpAwk = Math.Pow(Math.Sin((Math.PI / 2.0) * (Math.Abs(currVel - prevVel) / Math.Max(geoVel, 0.75))), 2.0);
            }

            if (osuCurrent.JumpDistance > 125)
                jumpNorm = 1.0;
            else 
                jumpNorm = Math.Pow(Math.Sin((Math.PI / 2.0) * (osuCurrent.JumpDistance / 125)), 2.0);

            if (currVel > 1.0)
                return Math.Sqrt(currVel) * (Math.Max(angleAwk, jumpAwk) + angleBonus + Math.Sqrt(sliderVel) + angleAwk * jumpAwk) * jumpNorm;
            else
                return currVel * (Math.Max(angleAwk, jumpAwk) + angleBonus + Math.Sqrt(sliderVel) + angleAwk * jumpAwk) * jumpNorm;

            /*
            double angleBuff = 1;
            double angleDev = 1;
            double velocityDev = 0;
            double velocityDiffDev = 0;

            if (Previous.Count == 0)
            {
                avgCursorPos = ((OsuHitObject)osuCurrent.BaseObject).StackedPosition;
                prevAvgCursorPos = avgCursorPos;
            }
            else
            {
                prevAvgCursorPos = avgCursorPos;
                if (osuCurrent.StrainTime > 1000)
                    avgCursorPos = ((OsuHitObject)osuCurrent.BaseObject).StackedPosition;
                else
                    avgCursorPos = avgCursorPos * (float)(1.0 - osuCurrent.StrainTime / 1000) + (float)(osuCurrent.StrainTime / 1000) * ((OsuHitObject)osuCurrent.BaseObject).StackedPosition;
            }
            double avgCursorStrain = (avgCursorPos - prevAvgCursorPos).Length / osuCurrent.StrainTime;

            if (Previous.Count > 1)
            {
                var osuPrevious = (OsuDifficultyHitObject)Previous[0];

                double velocitySum = 0;
                double velocityDiffSum = 0;
                double angleSum = 0;

                double velocityAvg = 0;
                double velocityDiffAvg = 0;
                double angleAvg = 0;

                Vector2 currVelocityVector = osuCurrent.DistanceVector / (float)osuCurrent.StrainTime;
                Vector2 prevVelocityVector = osuPrevious.DistanceVector / (float)osuPrevious.StrainTime;
                
                if (prevVelocityVector.Length == 0 && currVelocityVector.Length == 0)
                    angleBuff = 0;
                else
                    angleBuff = Math.Min(currVelocityVector.Length, prevVelocityVector.Length) * (currVelocityVector + prevVelocityVector).Length / Math.Max(prevVelocityVector.Length, currVelocityVector.Length);

                for (int i = 0; i < Previous.Count - 1; i++)
                {
                    var osuObject = (OsuDifficultyHitObject)Previous[i];
                    var osuPrevObject = (OsuDifficultyHitObject)Previous[i+1];

                    Vector2 osuObjectVelocityVector = osuObject.DistanceVector / (float)osuObject.StrainTime;
                    Vector2 osuPrevObjectVelocityVector = osuPrevObject.DistanceVector / (float)osuPrevObject.StrainTime;

                    velocitySum += osuObjectVelocityVector.Length;
                    velocityDiffSum += Math.Abs(osuObjectVelocityVector.Length - osuPrevObjectVelocityVector.Length);
                    angleSum += osuObject.Angle.Value;
                }
                velocityAvg = velocitySum / (Previous.Count - 1);
                velocityDiffAvg = velocityDiffSum / (Previous.Count - 1);
                angleAvg = angleSum / (Previous.Count - 1);

                velocityDev = Math.Abs(currVelocityVector.Length - velocityAvg);
                velocityDiffDev = Math.Abs(velocityDiffAvg - (currVelocityVector.Length - prevVelocityVector.Length));
                angleDev = 2.0 * Math.Sin(Math.Abs(angleAvg - osuCurrent.Angle.Value) / 2.0);
            }

            Console.WriteLine("Time: " + current.BaseObject.StartTime);
            Console.WriteLine("jumpNorm: " + jumpNorm);
            Console.WriteLine("angleBuff: " + angleBuff);
            Console.WriteLine("angleDev: " + angleDev);
            Console.WriteLine("Math.Sqrt(velocityDev * velocityDiffDev): " + Math.Sqrt(velocityDev * velocityDiffDev));

            return jumpNorm * angleBuff * angleDev * Math.Sqrt(velocityDev * velocityDiffDev);*/
        }
    }
}