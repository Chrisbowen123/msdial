﻿using CompMs.Common.Algorithm.ChromSmoothing;
using CompMs.Common.Components;
using CompMs.Common.Mathematics.Basic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CompMs.Common.Algorithm.PeakPick {

    public class PeakDetectionResult {
        public int PeakID { get; set; } = -1;
        public int ScanNumAtPeakTop { get; set; } = -1;
        public int ScanNumAtRightPeakEdge { get; set; } = -1;
        public int ScanNumAtLeftPeakEdge { get; set; } = -1;
        public float IntensityAtPeakTop { get; set; } = -1.0F;
        public float IntensityAtRightPeakEdge { get; set; } = -1.0F;
        public float IntensityAtLeftPeakEdge { get; set; } = -1.0F;
        public float RtAtPeakTop { get; set; } = -1.0F;
        public float RtAtRightPeakEdge { get; set; } = -1.0F;
        public float RtAtLeftPeakEdge { get; set; } = -1.0F;
        public float AmplitudeOrderValue { get; set; } = -1.0F;
        public float AmplitudeScoreValue { get; set; } = -1.0F;
        public float SymmetryValue { get; set; } = -1.0F;
        public float BasePeakValue { get; set; } = -1.0F;
        public float IdealSlopeValue { get; set; } = -1.0F;
        public float GaussianSimilarityValue { get; set; } = -1.0F;
        public float ShapnessValue { get; set; } = -1.0F;
        public float PeakPureValue { get; set; } = -1.0F;
        public float AreaAboveBaseline { get; set; } = -1.0F;
        public float AreaAboveZero { get; set; } = -1.0F;
        public float EstimatedNoise { get; set; } = -1.0F;
        public float SignalToNoise { get; set; } = -1.0F;
    }

    public sealed class PeakDetection {
        private PeakDetection() { }
        // below is a global peak detection method for gcms/lcms data preprocessing
        public static List<PeakDetectionResult> PeakDetectionVS1(double minimumDatapointCriteria, double minimumAmplitudeCriteria, List<ChromatogramPeak> peaklist) {
            var results = new List<PeakDetectionResult>();
            #region

            // global parameter
            var averagePeakWidth = 20.0;
            var amplitudeNoiseFoldCriteria = 4.0;
            var slopeNoiseFoldCriteria = 2.0;
            var peaktopNoiseFoldCriteria = 2.0;

            var smoother = 1;
            var baselineLevel = 30;
            var noiseEstimateBin = 50;
            var minNoiseWindowSize = 10;
            var minNoiseLevel = 50.0;
            var noiseFactor = 3.0;

            // 'chromatogram' properties
            double maxChromIntensity, minChromIntensity, baselineMedian, noise;
            List<ChromatogramPeak> ssPeaklist, baseline, baselineCorrectedPeaklist;
            bool isHighBaseline;
            findChromatogramGlobalProperties(peaklist, smoother, baselineLevel,
                noiseEstimateBin, minNoiseWindowSize, minNoiseLevel, noiseFactor,
                out maxChromIntensity, out minChromIntensity, out baselineMedian, out noise, out isHighBaseline,
                out ssPeaklist, out baseline, out baselineCorrectedPeaklist);

            // differential factors
            List<double> firstDiffPeaklist, secondDiffPeaklist;
            double maxFirstDiff, maxSecondDiff, maxAmplitudeDiff;
            generateDifferencialCoefficients(ssPeaklist, out firstDiffPeaklist, out secondDiffPeaklist, out maxAmplitudeDiff, out maxFirstDiff, out maxSecondDiff);

            // slope noises
            double amplitudeNoise, slopeNoise, peaktopNoise;
            calculateSlopeNoises(ssPeaklist, firstDiffPeaklist, secondDiffPeaklist, maxAmplitudeDiff, maxFirstDiff, maxSecondDiff,
                out amplitudeNoise, out slopeNoise, out peaktopNoise);

            var datapoints = new List<double[]>();
            var infinitLoopCheck = false;
            var infinitLoopID = 0;
            var nextPeakCheck = false;
            var nextPeakCheckReminder = 0;
            var margin = 5;
            if (minimumDatapointCriteria > margin) margin = (int)minimumDatapointCriteria;


            var peakCounter = 0;
            for (int i = margin; i < ssPeaklist.Count - margin; i++) {
                if (i > nextPeakCheckReminder + 2) nextPeakCheck = false;
                if (isPeakStarted(i, ssPeaklist, firstDiffPeaklist, slopeNoise, slopeNoiseFoldCriteria, results, nextPeakCheck)) {
                    datapoints = new List<double[]>();
                    datapoints.Add(new double[] { peaklist[i].ID, peaklist[i].Times.Value, peaklist[i].Mass, peaklist[i].Intensity,
                        firstDiffPeaklist[i], secondDiffPeaklist[i] });
                    searchRealLeftEdge(i, datapoints, peaklist, ssPeaklist, firstDiffPeaklist, secondDiffPeaklist);
                    i = searchRightEdgeCandidate(i, datapoints, peaklist, ssPeaklist, firstDiffPeaklist, secondDiffPeaklist, slopeNoise, slopeNoiseFoldCriteria, amplitudeNoise, peaktopNoise);
                    var isBreak = false;
                    i = searchRealRightEdge(i, datapoints, peaklist, ssPeaklist, firstDiffPeaklist, secondDiffPeaklist, ref infinitLoopCheck, ref infinitLoopID, out isBreak);
                    if (isBreak) break;
                    if (datapoints.Count < minimumDatapointCriteria) continue;
                    var peaktopID = 0;
                    curateDatapoints(datapoints, averagePeakWidth, out peaktopID);

                    var maxPeakHeight = 0.0;
                    var minPeakHeight = 0.0;
                    peakHeightFromBaseline(datapoints, peaktopID, out maxPeakHeight, out minPeakHeight);
                    if (maxPeakHeight < noise) continue;
                    if (minPeakHeight < minimumAmplitudeCriteria || minPeakHeight < amplitudeNoise * amplitudeNoiseFoldCriteria) continue;
                    if (isHighBaseline && Math.Min(datapoints[0][3], datapoints[datapoints.Count - 1][3]) < baselineMedian) continue;

                    var result = GetPeakDetectionResult(datapoints, peaktopID);
                    if (result == null) continue;
                    result.PeakID = peakCounter;
                    result.EstimatedNoise = (float)(noise / noiseFactor);
                    if (result.EstimatedNoise < 1.0) result.EstimatedNoise = 1.0F;
                    result.SignalToNoise = (float)(maxPeakHeight / result.EstimatedNoise);

                    results.Add(result);

                    peakCounter++;
                }
            }
            #endregion
            if (results.Count == 0) return null;
            return finalizePeakDetectionResults(results);
        }
        #region methods

        private static List<PeakDetectionResult> finalizePeakDetectionResults(List<PeakDetectionResult> results) {

            var sResults = results.OrderByDescending(n => n.IntensityAtPeakTop).ToList();
            float maxIntensity = sResults[0].IntensityAtPeakTop;
            for (int i = 0; i < sResults.Count; i++) {
                sResults[i].AmplitudeScoreValue = sResults[i].IntensityAtPeakTop / maxIntensity;
                sResults[i].AmplitudeOrderValue = i + 1;
            }
            sResults = sResults.OrderBy(n => n.PeakID).ToList();
            return new List<PeakDetectionResult>(sResults);
        }

        private static void peakHeightFromBaseline(List<double[]> datapoints, int peaktopID, out double maxPeakHeight, out double minPeakHeight) {
            var peaktopInt = datapoints[peaktopID][3];
            var peakleftInt = datapoints[0][3];
            var peakrightInt = datapoints[datapoints.Count - 1][3];


            maxPeakHeight = Math.Max(peaktopInt - peakleftInt, peaktopInt - peakrightInt);
            minPeakHeight = Math.Min(peaktopInt - peakleftInt, peaktopInt - peakrightInt);
        }

        private static void curateDatapoints(List<double[]> datapoints, double averagePeakWidth, out int peakTopId) {
            peakTopId = -1;

            var peakTopIntensity = double.MinValue;
            var excludedLeftCutPoint = 0;
            var excludedRightCutPoint = 0;

            for (int j = 0; j < datapoints.Count; j++) {
                if (peakTopIntensity < datapoints[j][3]) {
                    peakTopIntensity = datapoints[j][3];
                    peakTopId = j;
                }
            }
            if (peakTopId > averagePeakWidth) {
                excludedLeftCutPoint = 0;
                for (int j = peakTopId - (int)averagePeakWidth; j >= 0; j--) {
                    if (j - 1 <= 0) break;
                    if (datapoints[j][3] <= datapoints[j - 1][3]) {
                        excludedLeftCutPoint = j;
                        break;
                    }
                }
                if (excludedLeftCutPoint > 0) {
                    for (int j = 0; j < excludedLeftCutPoint; j++)
                        datapoints.RemoveAt(0);
                    peakTopId = peakTopId - excludedLeftCutPoint;
                }
            }
            if (datapoints.Count - 1 > peakTopId + averagePeakWidth) {
                excludedRightCutPoint = 0;
                for (int j = peakTopId + (int)averagePeakWidth; j < datapoints.Count; j++) {
                    if (j + 1 > datapoints.Count - 1) break;
                    if (datapoints[j][3] <= datapoints[j + 1][3]) { excludedRightCutPoint = datapoints.Count - 1 - j; break; }
                }
                if (excludedRightCutPoint > 0)
                    for (int j = 0; j < excludedRightCutPoint; j++)
                        datapoints.RemoveAt(datapoints.Count - 1);
            }
        }

        private static int searchRealRightEdge(int i, List<double[]> datapoints, List<ChromatogramPeak> peaklist, List<ChromatogramPeak> ssPeaklist,
            List<double> firstDiffPeaklist, List<double> secondDiffPeaklist, ref bool infinitLoopCheck, ref int infinitLoopID, out bool isBreak) {
            //Search real right edge within 5 data points
            var rightCheck = false;
            var trackcounter = 0;
            isBreak = false;

            //case: wrong edge is in right of real edge
            if (rightCheck == false) {
                for (int j = 0; j <= 5; j++) {
                    if (i - j - 1 < 0) break;
                    if (ssPeaklist[i - j].Intensity <= ssPeaklist[i - j - 1].Intensity) break;
                    if (ssPeaklist[i - j].Intensity > ssPeaklist[i - j - 1].Intensity) {
                        datapoints.RemoveAt(datapoints.Count - 1);
                        rightCheck = true;
                        trackcounter++;
                    }
                }
                if (trackcounter > 0) {
                    i -= trackcounter;
                    if (infinitLoopCheck == true && i == infinitLoopID && i > ssPeaklist.Count - 10) {
                        isBreak = true;
                        return i;
                    };
                    infinitLoopCheck = true; infinitLoopID = i;
                }
            }

            //case: wrong edge is in left of real edge
            if (rightCheck == false) {
                for (int j = 0; j <= 5; j++) {
                    if (i + j + 1 > ssPeaklist.Count - 1) break;
                    if (ssPeaklist[i + j].Intensity <= ssPeaklist[i + j + 1].Intensity) break;
                    if (ssPeaklist[i + j].Intensity > ssPeaklist[i + j + 1].Intensity) {
                        datapoints.Add(new double[] { peaklist[i + j + 1].ID, peaklist[i + j + 1].Times.Value, peaklist[i + j + 1].Mass,
                                    peaklist[i + j + 1].Intensity, firstDiffPeaklist[i + j + 1], secondDiffPeaklist[i + j + 1] });
                        rightCheck = true;
                        trackcounter++;
                    }
                }
                if (trackcounter > 0) i += trackcounter;
            }
            return i;
        }
        private static int searchRightEdgeCandidate(int i, List<double[]> datapoints,
            List<ChromatogramPeak> peaklist, List<ChromatogramPeak> ssPeaklist, List<double> firstDiffPeaklist, List<double> secondDiffPeaklist,
            double slopeNoise, double slopeNoiseFoldCriteria, double amplitudeNoise, double peaktopNoise) {
            var peaktopCheck = false;
            var peaktopCheckPoint = i;
            while (true) {
                if (i + 1 == ssPeaklist.Count - 1) break;

                i++;
                datapoints.Add(new double[] { peaklist[i].ID, peaklist[i].Times.Value, peaklist[i].Mass, peaklist[i].Intensity,
                            firstDiffPeaklist[i], secondDiffPeaklist[i] });
                if (peaktopCheck == false &&
                    (firstDiffPeaklist[i - 1] > 0 && firstDiffPeaklist[i] < 0) || (firstDiffPeaklist[i - 1] > 0 && firstDiffPeaklist[i + 1] < 0) &&
                    secondDiffPeaklist[i] < -1 * peaktopNoise) {
                    peaktopCheck = true; peaktopCheckPoint = i;
                }
                if (peaktopCheck == true && peaktopCheckPoint + 3 <= i - 1) {
                    if (firstDiffPeaklist[i] > -1 * slopeNoise * slopeNoiseFoldCriteria) break;
                    if (Math.Abs(ssPeaklist[i - 2].Intensity - ssPeaklist[i - 1].Intensity) < amplitudeNoise &&
                          Math.Abs(ssPeaklist[i - 1].Intensity - ssPeaklist[i].Intensity) < amplitudeNoise) break;
                }
            }
            return i;
        }
        private static void searchRealLeftEdge(int i, List<double[]> datapoints, 
            List<ChromatogramPeak> peaklist, List<ChromatogramPeak> ssPeaklist, List<double> firstDiffPeaklist, List<double> secondDiffPeaklist) {
            //search real left edge within 5 data points
            for (int j = 0; j <= 5; j++) {
                if (i - j - 1 < 0) break;
                if (ssPeaklist[i - j].Intensity <= ssPeaklist[i - j - 1].Intensity) break;
                if (ssPeaklist[i - j].Intensity > ssPeaklist[i - j - 1].Intensity)
                    datapoints.Insert(0, new double[] { peaklist[i - j - 1].ID, peaklist[i - j - 1].Times.Value,
                                peaklist[i - j - 1].Mass, peaklist[i - j - 1].Intensity, firstDiffPeaklist[i - j - 1], secondDiffPeaklist[i - j - 1] });
            }
        }

        private static bool isPeakStarted(int index, List<ChromatogramPeak> ssPeaklist, List<double> firstDiffPeaklist,
            double slopeNoise, double slopeNoiseFoldCriteria, List<PeakDetectionResult> peakDetectionResults, bool nextPeakCheck) {

            if (firstDiffPeaklist[index] > slopeNoise * slopeNoiseFoldCriteria &&
                firstDiffPeaklist[index + 1] > slopeNoise * slopeNoiseFoldCriteria ||
                (nextPeakCheck &&
                peakDetectionResults[peakDetectionResults.Count - 1].IntensityAtRightPeakEdge < ssPeaklist[index].Intensity &&
                ssPeaklist[index].Intensity < ssPeaklist[index + 1].Intensity && ssPeaklist[index + 1].Intensity < ssPeaklist[index + 2].Intensity)) {
                return true;
            }
            else {
                return false;
            }
        }

        private static void calculateSlopeNoises(List<ChromatogramPeak> ssPeaklist, List<double> firstDiffPeaklist, List<double> secondDiffPeaklist,
            double maxAmplitudeDiff, double maxFirstDiff, double maxSecondDiff, out double amplitudeNoise, out double slopeNoise, out double peaktopNoise) {

            var amplitudeNoiseCandidate = new List<double>();
            var slopeNoiseCandidate = new List<double>();
            var peaktopNoiseCandidate = new List<double>();
            double amplitudeNoiseThresh = maxAmplitudeDiff * 0.05, slopeNoiseThresh = maxFirstDiff * 0.05, peaktopNoiseThresh = maxSecondDiff * 0.05;
            for (int i = 2; i < ssPeaklist.Count - 2; i++) {
                if (Math.Abs(ssPeaklist[i + 1].Intensity - ssPeaklist[i].Intensity) < amplitudeNoiseThresh &&
                    Math.Abs(ssPeaklist[i + 1].Intensity - ssPeaklist[i].Intensity) > 0)
                    amplitudeNoiseCandidate.Add(Math.Abs(ssPeaklist[i + 1].Intensity - ssPeaklist[i].Intensity));
                if (Math.Abs(firstDiffPeaklist[i]) < slopeNoiseThresh && Math.Abs(firstDiffPeaklist[i]) > 0)
                    slopeNoiseCandidate.Add(Math.Abs(firstDiffPeaklist[i]));
                if (secondDiffPeaklist[i] < 0 && Math.Abs(secondDiffPeaklist[i]) < peaktopNoiseThresh &&
                    Math.Abs(secondDiffPeaklist[i]) > 0)
                    peaktopNoiseCandidate.Add(Math.Abs(secondDiffPeaklist[i]));
            }
            if (amplitudeNoiseCandidate.Count == 0) amplitudeNoise = 0.0001; else amplitudeNoise = BasicMathematics.Median(amplitudeNoiseCandidate.ToArray());
            if (slopeNoiseCandidate.Count == 0) slopeNoise = 0.0001; else slopeNoise = BasicMathematics.Median(slopeNoiseCandidate.ToArray());
            if (peaktopNoiseCandidate.Count == 0) peaktopNoise = 0.0001; else peaktopNoise = BasicMathematics.Median(peaktopNoiseCandidate.ToArray());
        }

        private static void generateDifferencialCoefficients(List<ChromatogramPeak> ssPeaklist, out List<double> firstDiffPeaklist, out List<double> secondDiffPeaklist,
            out double maxAmplitudeDiff, out double maxFirstDiff, out double maxSecondDiff) {

            firstDiffPeaklist = new List<double>();
            secondDiffPeaklist = new List<double>();

            maxFirstDiff = double.MinValue;
            maxSecondDiff = double.MinValue;
            maxAmplitudeDiff = double.MinValue;

            var firstDiffCoeff = new double[] { -0.2, -0.1, 0, 0.1, 0.2 };
            var secondDiffCoeff = new double[] { 0.14285714, -0.07142857, -0.1428571, -0.07142857, 0.14285714 };
            double firstDiff, secondDiff;
            int halfDatapoint = (int)(firstDiffCoeff.Length / 2);

            for (int i = 0; i < ssPeaklist.Count; i++) {
                if (i < halfDatapoint) {
                    firstDiffPeaklist.Add(0);
                    secondDiffPeaklist.Add(0);
                    continue;
                }
                if (i >= ssPeaklist.Count - halfDatapoint) {
                    firstDiffPeaklist.Add(0);
                    secondDiffPeaklist.Add(0);
                    continue;
                }

                firstDiff = secondDiff = 0;
                for (int j = 0; j < firstDiffCoeff.Length; j++) {
                    firstDiff += firstDiffCoeff[j] * ssPeaklist[i + j - halfDatapoint].Intensity;
                    secondDiff += secondDiffCoeff[j] * ssPeaklist[i + j - halfDatapoint].Intensity;
                }
                firstDiffPeaklist.Add(firstDiff);
                secondDiffPeaklist.Add(secondDiff);

                if (Math.Abs(firstDiff) > maxFirstDiff) maxFirstDiff = Math.Abs(firstDiff);
                if (secondDiff < 0 && maxSecondDiff < -1 * secondDiff) maxSecondDiff = -1 * secondDiff;
                if (Math.Abs(ssPeaklist[i].Intensity - ssPeaklist[i - 1].Intensity) > maxAmplitudeDiff)
                    maxAmplitudeDiff = Math.Abs(ssPeaklist[i].Intensity - ssPeaklist[i - 1].Intensity);
            }
        }

        private static void findChromatogramGlobalProperties(List<ChromatogramPeak> peaklist, int smoother, int baselineLevel,
            int noiseEstimateBin, int minNoiseWindowSize, double minNoiseLevel, double noiseFactor,
            out double maxChromIntensity, out double minChromIntensity, out double baselineMedian, out double noise, out bool isHighBaseline,
            out List<ChromatogramPeak> ssPeaklist, out List<ChromatogramPeak> baseline, out List<ChromatogramPeak> baselineCorrectedPeaklist) {

            // checking chromatogram properties
            maxChromIntensity = double.MinValue;
            minChromIntensity = double.MaxValue;
            ssPeaklist = Smoothing.LinearWeightedMovingAverage(Smoothing.LinearWeightedMovingAverage(peaklist, 1), 1);
            baseline = Smoothing.SimpleMovingAverage(Smoothing.SimpleMovingAverage(peaklist, 30), 30);
            baselineCorrectedPeaklist = new List<ChromatogramPeak>();

            var amplitudeDiffs = new List<double>();
            var counter = noiseEstimateBin;
            var ampMax = double.MinValue;
            var ampMin = double.MaxValue;
            var baseIntensites = new List<double>();
            for (int i = 0; i < peaklist.Count; i++) {
                baseIntensites.Add(baseline[i].Intensity);

                var intensity = ssPeaklist[i].Intensity - baseline[i].Intensity;
                if (intensity < 0) intensity = 0;
                baselineCorrectedPeaklist.Add(new ChromatogramPeak { 
                    ID = peaklist[i].ID, Times = peaklist[i].Times, Mass = peaklist[i].Mass, Intensity = intensity });

                if (peaklist[i].Intensity > maxChromIntensity) maxChromIntensity = peaklist[i].Intensity;
                if (peaklist[i].Intensity < minChromIntensity) minChromIntensity = peaklist[i].Intensity;

                if (counter < i) {
                    if (ampMax > ampMin) {
                        amplitudeDiffs.Add(ampMax - ampMin);
                    }

                    counter += noiseEstimateBin;
                    ampMax = double.MinValue;
                    ampMin = double.MaxValue;
                }
                else {
                    if (ampMax < intensity) ampMax = intensity;
                    if (ampMin > intensity) ampMin = intensity;
                }
            }
            baselineMedian = BasicMathematics.Median(baseIntensites.ToArray());
            isHighBaseline = baselineMedian > (maxChromIntensity - minChromIntensity) * 0.5 ? true : false;

            if (amplitudeDiffs.Count >= minNoiseWindowSize) {
                minNoiseLevel = BasicMathematics.Median(amplitudeDiffs.ToArray());
            }

            noise = minNoiseLevel * noiseFactor;
        }

        public static PeakDetectionResult GetPeakDetectionResult(List<double[]> datapoints, int peakTopId) {
            PeakDetectionResult detectedPeakInformation;
            double peakHwhm, peakHalfDiff, peakFivePercentDiff, leftShapenessValue, rightShapenessValue
                , gaussianSigma, gaussianNormalize, gaussianArea, gaussinaSimilarityValue, gaussianSimilarityLeftValue, gaussianSimilarityRightValue
                , realAreaAboveZero, realAreaAboveBaseline, leftPeakArea, rightPeakArea, idealSlopeValue, nonIdealSlopeValue, symmetryValue, basePeakValue, peakPureValue;
            int peakHalfId = -1, leftPeakFivePercentId = -1, rightPeakFivePercentId = -1, leftPeakHalfId = -1, rightPeakHalfId = -1;

            //1. Check HWHM criteria and calculate shapeness value, symmetry value, base peak value, ideal value, non ideal value
            #region
            if (datapoints.Count <= 3) return null;
            if (datapoints[peakTopId][3] - datapoints[0][3] < 0 && datapoints[peakTopId][3] - datapoints[datapoints.Count - 1][3] < 0) return null;
            idealSlopeValue = 0;
            nonIdealSlopeValue = 0;
            peakHalfDiff = double.MaxValue;
            peakFivePercentDiff = double.MaxValue;
            leftShapenessValue = double.MinValue;

            for (int j = peakTopId; j >= 0; j--) {
                if (peakHalfDiff > Math.Abs((datapoints[peakTopId][3] - datapoints[0][3]) / 2 - (datapoints[j][3] - datapoints[0][3]))) {
                    peakHalfDiff = Math.Abs((datapoints[peakTopId][3] - datapoints[0][3]) / 2 - (datapoints[j][3] - datapoints[0][3]));
                    leftPeakHalfId = j;
                }

                if (peakFivePercentDiff > Math.Abs((datapoints[peakTopId][3] - datapoints[0][3]) / 5 - (datapoints[j][3] - datapoints[0][3]))) {
                    peakFivePercentDiff = Math.Abs((datapoints[peakTopId][3] - datapoints[0][3]) / 5 - (datapoints[j][3] - datapoints[0][3]));
                    leftPeakFivePercentId = j;
                }

                if (j == peakTopId) continue;

                if (leftShapenessValue < (datapoints[peakTopId][3] - datapoints[j][3]) / (peakTopId - j) / Math.Sqrt(datapoints[peakTopId][3]))
                    leftShapenessValue = (datapoints[peakTopId][3] - datapoints[j][3]) / (peakTopId - j) / Math.Sqrt(datapoints[peakTopId][3]);

                if (datapoints[j + 1][3] - datapoints[j][3] >= 0)
                    idealSlopeValue += Math.Abs(datapoints[j + 1][3] - datapoints[j][3]);
                else
                    nonIdealSlopeValue += Math.Abs(datapoints[j + 1][3] - datapoints[j][3]);
            }
            peakHalfDiff = double.MaxValue;
            peakFivePercentDiff = double.MaxValue;
            rightShapenessValue = double.MinValue;
            for (int j = peakTopId; j <= datapoints.Count - 1; j++) {
                if (peakHalfDiff > Math.Abs((datapoints[peakTopId][3] - datapoints[datapoints.Count - 1][3]) / 2 - (datapoints[j][3] - datapoints[datapoints.Count - 1][3]))) {
                    peakHalfDiff = Math.Abs((datapoints[peakTopId][3] - datapoints[datapoints.Count - 1][3]) / 2 - (datapoints[j][3] - datapoints[datapoints.Count - 1][3]));
                    rightPeakHalfId = j;
                }

                if (peakFivePercentDiff > Math.Abs((datapoints[peakTopId][3] - datapoints[datapoints.Count - 1][3]) / 5 - (datapoints[j][3] - datapoints[datapoints.Count - 1][3]))) {
                    peakFivePercentDiff = Math.Abs((datapoints[peakTopId][3] - datapoints[datapoints.Count - 1][3]) / 5 - (datapoints[j][3] - datapoints[datapoints.Count - 1][3]));
                    rightPeakFivePercentId = j;
                }

                if (j == peakTopId) continue;

                if (rightShapenessValue < (datapoints[peakTopId][3] - datapoints[j][3]) / (j - peakTopId) / Math.Sqrt(datapoints[peakTopId][3]))
                    rightShapenessValue = (datapoints[peakTopId][3] - datapoints[j][3]) / (j - peakTopId) / Math.Sqrt(datapoints[peakTopId][3]);

                if (datapoints[j - 1][3] - datapoints[j][3] >= 0)
                    idealSlopeValue += Math.Abs(datapoints[j - 1][3] - datapoints[j][3]);
                else
                    nonIdealSlopeValue += Math.Abs(datapoints[j - 1][3] - datapoints[j][3]);
            }


            if (datapoints[0][3] <= datapoints[datapoints.Count - 1][3]) {
                gaussianNormalize = datapoints[peakTopId][3] - datapoints[0][3];
                peakHalfId = leftPeakHalfId;
                basePeakValue = Math.Abs((datapoints[peakTopId][3] - datapoints[datapoints.Count - 1][3]) / (datapoints[peakTopId][3] - datapoints[0][3]));
            }
            else {
                gaussianNormalize = datapoints[peakTopId][3] - datapoints[datapoints.Count - 1][3];
                peakHalfId = rightPeakHalfId;
                basePeakValue = Math.Abs((datapoints[peakTopId][3] - datapoints[0][3]) / (datapoints[peakTopId][3] - datapoints[datapoints.Count - 1][3]));
            }

            if (Math.Abs(datapoints[peakTopId][1] - datapoints[leftPeakFivePercentId][1]) <= Math.Abs(datapoints[peakTopId][1] - datapoints[rightPeakFivePercentId][1]))
                symmetryValue = Math.Abs(datapoints[peakTopId][1] - datapoints[leftPeakFivePercentId][1]) / Math.Abs(datapoints[peakTopId][1] - datapoints[rightPeakFivePercentId][1]);
            else
                symmetryValue = Math.Abs(datapoints[peakTopId][1] - datapoints[rightPeakFivePercentId][1]) / Math.Abs(datapoints[peakTopId][1] - datapoints[leftPeakFivePercentId][1]);

            peakHwhm = Math.Abs(datapoints[peakHalfId][1] - datapoints[peakTopId][1]);
            #endregion

            //2. Calculate peak pure value (from gaussian area and real area)
            #region
            gaussianSigma = peakHwhm / Math.Sqrt(2 * Math.Log(2));
            gaussianArea = gaussianNormalize * gaussianSigma * Math.Sqrt(2 * Math.PI) / 2;

            realAreaAboveZero = 0;
            leftPeakArea = 0;
            rightPeakArea = 0;
            for (int j = 0; j < datapoints.Count - 1; j++) {
                realAreaAboveZero += (datapoints[j][3] + datapoints[j + 1][3]) * (datapoints[j + 1][1] - datapoints[j][1]) * 0.5;
                if (j == peakTopId - 1)
                    leftPeakArea = realAreaAboveZero;
                else if (j == datapoints.Count - 2)
                    rightPeakArea = realAreaAboveZero - leftPeakArea;
            }

            realAreaAboveBaseline = realAreaAboveZero - (datapoints[0][3] + datapoints[datapoints.Count - 1][3]) * (datapoints[datapoints.Count - 1][1] - datapoints[0][1]) / 2;

            if (datapoints[0][3] <= datapoints[datapoints.Count - 1][3]) {
                leftPeakArea = leftPeakArea - datapoints[0][3] * (datapoints[peakTopId][1] - datapoints[0][1]);
                rightPeakArea = rightPeakArea - datapoints[0][3] * (datapoints[datapoints.Count - 1][1] - datapoints[peakTopId][1]);
            }
            else {
                leftPeakArea = leftPeakArea - datapoints[datapoints.Count - 1][3] * (datapoints[peakTopId][1] - datapoints[0][1]);
                rightPeakArea = rightPeakArea - datapoints[datapoints.Count - 1][3] * (datapoints[datapoints.Count - 1][1] - datapoints[peakTopId][1]);
            }

            if (gaussianArea >= leftPeakArea) gaussianSimilarityLeftValue = leftPeakArea / gaussianArea;
            else gaussianSimilarityLeftValue = gaussianArea / leftPeakArea;

            if (gaussianArea >= rightPeakArea) gaussianSimilarityRightValue = rightPeakArea / gaussianArea;
            else gaussianSimilarityRightValue = gaussianArea / rightPeakArea;

            gaussinaSimilarityValue = (gaussianSimilarityLeftValue + gaussianSimilarityRightValue) / 2;
            idealSlopeValue = (idealSlopeValue - nonIdealSlopeValue) / idealSlopeValue;

            if (idealSlopeValue < 0) idealSlopeValue = 0;

            peakPureValue = (gaussinaSimilarityValue + 1.2 * basePeakValue + 0.8 * symmetryValue + idealSlopeValue) / 4;
            if (peakPureValue > 1) peakPureValue = 1;
            if (peakPureValue < 0) peakPureValue = 0;
            #endregion

            //3. Set area information
            #region
            detectedPeakInformation = new PeakDetectionResult() {
                PeakID = -1,
                AmplitudeOrderValue = -1,
                AmplitudeScoreValue = -1,
                AreaAboveBaseline = (float)(realAreaAboveBaseline * 60),
                AreaAboveZero = (float)(realAreaAboveZero * 60),
                BasePeakValue = (float)basePeakValue,
                GaussianSimilarityValue = (float)gaussinaSimilarityValue,
                IdealSlopeValue = (float)idealSlopeValue,
                IntensityAtLeftPeakEdge = (float)datapoints[0][3],
                IntensityAtPeakTop = (float)datapoints[peakTopId][3],
                IntensityAtRightPeakEdge = (float)datapoints[datapoints.Count - 1][3],
                PeakPureValue = (float)peakPureValue,
                RtAtLeftPeakEdge = (float)datapoints[0][1],
                RtAtPeakTop = (float)datapoints[peakTopId][1],
                RtAtRightPeakEdge = (float)datapoints[datapoints.Count - 1][1],
                ScanNumAtLeftPeakEdge = (int)datapoints[0][0],
                ScanNumAtPeakTop = (int)datapoints[peakTopId][0],
                ScanNumAtRightPeakEdge = (int)datapoints[datapoints.Count - 1][0],
                ShapnessValue = (float)((leftShapenessValue + rightShapenessValue) / 2),
                SymmetryValue = (float)symmetryValue
            };
            #endregion
            return detectedPeakInformation;
        }

        #endregion method

    }


}
