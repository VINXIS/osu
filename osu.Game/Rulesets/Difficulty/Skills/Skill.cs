﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
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

        private List<Tuple<double, double>> grapher = new List<Tuple<double, double>>();

        public List<Tuple<double, double>> jumpAwkVals = new List<Tuple<double, double>>();
        public List<Tuple<double, double>> angleAwkVals = new List<Tuple<double, double>>();
        public List<Tuple<double, double>> angleBonusVals = new List<Tuple<double, double>>();
        public List<Tuple<double, double>> sliderVelVals = new List<Tuple<double, double>>();
        public List<Tuple<double, double>> flowBonusVals = new List<Tuple<double, double>>();
        public List<Tuple<double, double>> jumpNormVals = new List<Tuple<double, double>>();
        public List<Tuple<double, double>> velocities = new List<Tuple<double, double>>();

        /// <summary>
        /// Process a <see cref="DifficultyHitObject"/> and update current strain values accordingly.
        /// </summary>
        public void Process(DifficultyHitObject current)
        {
            currentStrain *= strainDecay(current.DeltaTime);
            grapher.Add(Tuple.Create(current.BaseObject.StartTime, currentStrain));
            currentStrain += StrainValueOf(current) * SkillMultiplier;
            grapher.Add(Tuple.Create(current.BaseObject.StartTime, currentStrain));

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
            using (StreamWriter outputFile = new StreamWriter(this.GetType().Name.ToLower() + ".txt"))
            {
                foreach (Tuple<double, double> point in grapher)
                    outputFile.WriteLine(point);
            }

            if (this.GetType().Name == "Control")
            {
                using (StreamWriter outputFile = new StreamWriter("jumpAwkVals.txt"))
                {
                    foreach (Tuple<double, double> point in jumpAwkVals)
                        outputFile.WriteLine(point);
                }
                using (StreamWriter outputFile = new StreamWriter("angleAwkVals.txt"))
                {
                    foreach (Tuple<double, double> point in angleAwkVals)
                        outputFile.WriteLine(point);
                }
                using (StreamWriter outputFile = new StreamWriter("angleBonusVals.txt"))
                {
                    foreach (Tuple<double, double> point in angleBonusVals)
                        outputFile.WriteLine(point);
                }
                using (StreamWriter outputFile = new StreamWriter("sliderVelVals.txt"))
                {
                    foreach (Tuple<double, double> point in sliderVelVals)
                        outputFile.WriteLine(point);
                }
                using (StreamWriter outputFile = new StreamWriter("flowBonusVals.txt"))
                {
                    foreach (Tuple<double, double> point in flowBonusVals)
                        outputFile.WriteLine(point);
                }
                using (StreamWriter outputFile = new StreamWriter("jumpNormVals.txt"))
                {
                    foreach (Tuple<double, double> point in jumpNormVals)
                        outputFile.WriteLine(point);
                }
                using (StreamWriter outputFile = new StreamWriter("velocities.txt"))
                {
                    foreach (Tuple<double, double> point in velocities)
                        outputFile.WriteLine(point);
                }
            }
            strainPeaks.Sort((a, b) => b.CompareTo(a)); // Sort from highest to lowest strain.

            double difficulty = 0;
            double weight = 1;

            // Difficulty is the weighted sum of the highest strains from every section.
            foreach (double strain in strainPeaks)
            {
                difficulty += strain * weight;
                weight *= DecayWeight;
            }

            return difficulty;
        }

        /// <summary>
        /// Calculates the strain value of a <see cref="DifficultyHitObject"/>. This value is affected by previously processed objects.
        /// </summary>
        protected abstract double StrainValueOf(DifficultyHitObject current);

        private double strainDecay(double ms) => Math.Pow(StrainDecayBase, ms / 1000);
    }
}
