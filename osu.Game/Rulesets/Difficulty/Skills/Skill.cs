// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;

namespace osu.Game.Rulesets.Difficulty.Skills
{
    /// <summary>
    /// Used to processes strain values of <see cref="DifficultyHitObject"/>s, keep track of strain levels caused by the processed objects
    /// and to calculate a final difficulty value representing the difficulty of hitting all the processed objects.
    /// </summary>
    public abstract class Skill
    {
        /// <summary>
        /// The peak strain for each <see cref="DifficultyCalculator.SectionLength"/> section of the beatmap.
        /// </summary>
        public IList<double> StrainPeaks => strainPeaks;

        public double timeMultiplier(DifficultyHitObject current) => 0.5 - Math.Tanh((current.StrainTime - 100.0) / 50.0) / 2.0;

		public double sinusoid(double inputNumber)
        {
            double ePower, piMultiplier, offset;
            double changeValue = 2.0 / 3.0;
            if (inputNumber < changeValue)
            {
                ePower = -1.5;
                piMultiplier = 4.0;
                offset = -2.0;
            } else
            {
                ePower = -4.0 / 3.0;
                piMultiplier = 3.0;
                offset = -2.0 - 1.0 / 6.0;
            }

            double outputValue = 2.0 * Math.Exp(ePower * inputNumber) * Math.Pow(Math.Sin((piMultiplier * Math.PI) / (inputNumber + offset)), 2.0);

            return outputValue;
        }
		
        /// <summary>
        /// Strain values are multiplied by this number for the given skill. Used to balance the value of different skills between each other.
        /// </summary>
        protected abstract double SkillMultiplier { get; }

        /// <summary>
        /// Determines how quickly strain decays for the given skill.
        /// For example a value of 0.15 indicates that strain decays to 15% of its original value in one second.
        /// </summary>
        protected abstract double StrainDecayBase { get; }

        /// <summary>
        /// The weight by which each strain value decays.
        /// </summary>
        protected virtual double DecayWeight => 0.9;

        /// <summary>
        /// <see cref="DifficultyHitObject"/>s that were processed previously. They can affect the strain values of the following objects.
        /// </summary>
        protected readonly LimitedCapacityStack<DifficultyHitObject> Previous = new LimitedCapacityStack<DifficultyHitObject>(2); // Contained objects not used yet

        private double currentStrain = 1; // We keep track of the strain level at all times throughout the beatmap.
        private double currentSectionPeak = 1; // We also keep track of the peak strain level in the current section.

        private readonly List<double> strainPeaks = new List<double>();
        private readonly List<Tuple<DifficultyHitObject, double>> objPeaks = new List<Tuple<DifficultyHitObject, double>>();

        /// <summary>
        /// Process a <see cref="DifficultyHitObject"/> and update current strain values accordingly.
        /// </summary>
        public void Process(DifficultyHitObject current)
        {
            currentStrain *= strainDecay(current.DeltaTime);
            currentStrain += StrainValueOf(current) * SkillMultiplier;

            objPeaks.Add(new Tuple<DifficultyHitObject, double>(current, currentStrain));

            currentSectionPeak = Math.Max(currentStrain, currentSectionPeak);

            Previous.Push(current);
        }

        /// <summary>
        /// Saves the current peak strain level to the list of strain peaks, which will be used to calculate an overall difficulty.
        /// </summary>
        public void SaveCurrentPeak()
        {
            if (Previous.Count > 0)
                strainPeaks.Add(currentSectionPeak);
        }

        /// <summary>
        /// Sets the initial strain level for a new section.
        /// </summary>
        /// <param name="offset">The beginning of the new section in milliseconds.</param>
        public void StartNewSectionFrom(double offset)
        {
            // The maximum strain of the new section is not zero by default, strain decays as usual regardless of section boundaries.
            // This means we need to capture the strain level at the beginning of the new section, and use that as the initial peak level.
            if (Previous.Count > 0)
                currentSectionPeak = currentStrain * strainDecay(offset - Previous[0].BaseObject.StartTime);
        }

        /// <summary>
        /// Returns the calculated difficulty value representing all processed <see cref="DifficultyHitObject"/>s.
        /// </summary>
        public double DifficultyValue()
        {
            strainPeaks.Sort((a, b) => b.CompareTo(a)); // Sort from highest to lowest strain.
            objPeaks.Sort((a, b) => a.Item2.CompareTo(b.Item2)); // Sort from highest to lowest strain.

            double difficulty = 0;
            double weight = 1;

            // Difficulty is the weighted sum of the highest strains from every section.
            foreach (double strain in strainPeaks)
            {
                difficulty += strain * weight;
                weight *= DecayWeight;
            }

            /*foreach  (Tuple<OsuDifficultyHitObject, double> obj in objPeaks) 
            {
                Console.WriteLine("---");
                Console.WriteLine("Object placed: " + obj.Item1.BaseObject.StartTime);
                Console.WriteLine("Strain value: " + obj.Item2);
            }*/

            return difficulty;
        }

        /// <summary>
        /// Calculates the strain value of a <see cref="DifficultyHitObject"/>. This value is affected by previously processed objects.
        /// </summary>
        protected abstract double StrainValueOf(DifficultyHitObject current);

        private double strainDecay(double ms) => Math.Pow(StrainDecayBase, ms / 1000);
    }
}