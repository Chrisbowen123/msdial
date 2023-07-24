﻿using CompMs.Common.Parser;
using CompMs.MsdialCore.DataObj;

namespace CompareAnnotationResult
{
    internal class Program
    {
        static void Main(string[] args) {
            var data = CommandLineParser.Parse<CommandLineData>(args);

            MatchedSpotCandidateCalculator candidateCalculator = new MatchedSpotCandidateCalculator(data.MzTolerance, data.RtTolerance, data.AmplitudeThreshold);
            var finder = new CompoundTargetFinder(data, candidateCalculator);
            var candidate = finder.Find(data.LoadSpots().AlignmentSpotProperties);
            
        }
    }
}