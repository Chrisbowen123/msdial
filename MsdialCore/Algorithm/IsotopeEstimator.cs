﻿using CompMs.Common.Algorithm.IsotopeCalc;
using CompMs.Common.Components;
using CompMs.Common.DataObj.Database;
using CompMs.Common.DataObj.Property;
using CompMs.Common.FormulaGenerator.DataObj;
using CompMs.Common.FormulaGenerator.Function;
using CompMs.Common.Utility;
using CompMs.MsdialCore.DataObj;
using CompMs.MsdialCore.Parameter;
using CompMs.MsdialCore.Utility;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace CompMs.MsdialCore.Algorithm {

    public class IsotopeTemp {
        public int WeightNumber { get; set; }
        public double Mz { get; set; }
        public double Intensity { get; set; }
        public int PeakID { get; set; }
    }

    public sealed class IsotopeEstimator
    {
        private IsotopeEstimator() { }

        /// <summary>
        /// This method tries to decide if the detected peak is the isotopic ion or not.
        /// The peaks less than the abundance of the mono isotopic ion will be assigned to the isotopic ions within the same data point.
        /// </summary>
        /// <param name="peakAreaBeanCollection"></param>
        /// <param name="analysisParametersBean"></param>
        public static void Process(
            List<ChromatogramPeakFeature> peakFeatures,
            ParameterBase param, 
            IupacDatabase iupac)
        {
            peakFeatures = peakFeatures.OrderBy(n => n.PrecursorMz).ToList();

            //var spectrumMargin = 2;
            var rtMargin = 0.25F;
            var isotopeMax = 8.1;

            foreach (var peak in peakFeatures) {
                var peakCharacter = peak.PeakCharacter;
                if (peakCharacter.IsotopeWeightNumber >= 0) continue;

                // var focusedScan = peak.ScanNumberAtPeakTop;
                var focusedMass = peak.PrecursorMz;
                var focusedRt = peak.ChromXsTop.RT.Value;

                var startScanIndex = SearchCollection.LowerBound(peakFeatures, new ChromatogramPeakFeature() { Mass = focusedMass - param.CentroidMs1Tolerance }, (a, b) => a.Mass.CompareTo(b.Mass));
                //DataAccess.GetScanStartIndexByMz((float)focusedMass - param.CentroidMs1Tolerance, peakFeatures);
                var isotopeCandidates = new List<ChromatogramPeakFeature>() { peak };

                for (int j = startScanIndex; j < peakFeatures.Count; j++) {

                    if (peakFeatures[j].PeakID == peak.PeakID) continue;
                    if (peakFeatures[j].ChromXsTop.RT.Value < focusedRt - rtMargin) continue;
                    if (peakFeatures[j].ChromXsTop.RT.Value > focusedRt + rtMargin) continue;
                    if (peakFeatures[j].PeakCharacter.IsotopeWeightNumber >= 0) continue;
                    if (peakFeatures[j].PrecursorMz <= focusedMass) continue;
                    if (peakFeatures[j].PrecursorMz > focusedMass + isotopeMax) break;

                    isotopeCandidates.Add(peakFeatures[j]);
                }
                EstimateIsotopes(isotopeCandidates, param, iupac);
            }
        }
     
        public static void EstimateIsotopes(
            List<ChromatogramPeakFeature> peakFeatures,
            ParameterBase param,
            IupacDatabase iupac) {
            var c13_c12Diff = MassDiffDictionary.C13_C12;  //1.003355F;
            var tolerance = param.CentroidMs1Tolerance;
            var monoIsoPeak = peakFeatures[0];
            var ppm = MolecularFormulaUtility.PpmCalculator(200.0, 200.0 + param.CentroidMs1Tolerance); //based on m/z 200
            var accuracy = MolecularFormulaUtility.ConvertPpmToMassAccuracy(monoIsoPeak.PrecursorMz, ppm);
            var rtMargin = 0.06F;

            tolerance = (float)accuracy;
            var isFinished = false;

            monoIsoPeak.PeakCharacter.IsotopeWeightNumber = 0;
            monoIsoPeak.PeakCharacter.IsotopeParentPeakID = monoIsoPeak.PeakID;

            //if (Math.Abs(monoIsoPeak.AccurateMass - 762.5087) < 0.001) {
            //    Console.WriteLine();
            //}
            var rtMonoisotope = monoIsoPeak.ChromXsTop.RT.Value;
            var rtFocused = monoIsoPeak.ChromXsTop.RT.Value;
            //charge number check at M + 1
            var predChargeNumber = 1;
            for (int j = 1; j < peakFeatures.Count; j++) {
                var isotopePeak = peakFeatures[j];
                if (isotopePeak.PrecursorMz > monoIsoPeak.PrecursorMz + c13_c12Diff + tolerance) break;
                var isotopeRt = isotopePeak.ChromXsTop.RT.Value;

                for (int k = param.MaxChargeNumber; k >= 1; k--) {
                    var predIsotopeMass = (double)monoIsoPeak.PrecursorMz + (double)c13_c12Diff / (double)k;
                    var diff = Math.Abs(predIsotopeMass - isotopePeak.PrecursorMz);
                    var diffRt = Math.Abs(rtMonoisotope - isotopeRt);
                    if (diff < tolerance && diffRt < rtMargin) {
                        predChargeNumber = k;
                        if (k <= 3) {
                            break;
                        } else if (k == 4 || k == 5) {
                            var predNextIsotopeMass = (double)monoIsoPeak.PrecursorMz + (double)c13_c12Diff / (double)(k - 1);
                            var nextDiff = Math.Abs(predNextIsotopeMass - isotopePeak.PrecursorMz);
                            if (diff > nextDiff) predChargeNumber = k - 1;
                            break;
                        } else if (k >= 6) {
                            var predNextIsotopeMass = (double)monoIsoPeak.PrecursorMz + (double)c13_c12Diff / (double)(k - 1);
                            var nextDiff = Math.Abs(predNextIsotopeMass - isotopePeak.PrecursorMz);
                            if (diff > nextDiff) {
                                predChargeNumber = k - 1;
                                diff = nextDiff;

                                predNextIsotopeMass = (double)monoIsoPeak.PrecursorMz + (double)c13_c12Diff / (double)(k - 2);
                                nextDiff = Math.Abs(predNextIsotopeMass - isotopePeak.PrecursorMz);

                                if (diff > nextDiff) {
                                    predChargeNumber = k - 2;
                                    diff = nextDiff;
                                }
                            }
                            break;
                        }
                    }
                }
                if (predChargeNumber != 1) break;
            }

            monoIsoPeak.PeakCharacter.Charge = predChargeNumber;

            var maxTraceNumber =15;
            var isotopeTemps = new IsotopeTemp[maxTraceNumber + 1];
            isotopeTemps[0] = new IsotopeTemp() {
                WeightNumber = 0, Mz = monoIsoPeak.PrecursorMz,
                Intensity = monoIsoPeak.PeakHeightTop, PeakID = monoIsoPeak.PeakID
            };
            for (int i = 1; i < isotopeTemps.Length; i++) {
                isotopeTemps[i] = new IsotopeTemp() {
                    WeightNumber = i, Mz = monoIsoPeak.PrecursorMz + (double)i * c13_c12Diff / (double)predChargeNumber,
                    Intensity = 0, PeakID = -1
                };
            }

            var reminderIndex = 1;
            var mzFocused = (double)monoIsoPeak.PrecursorMz;
            for (int i = 1; i <= maxTraceNumber; i++) {

                //var predIsotopicMass = (double)monoIsoPeak.PrecursorMz + (double)i * c13_c12Diff / (double)predChargeNumber;
                var predIsotopicMass = mzFocused + (double)c13_c12Diff / (double)predChargeNumber;
                for (int j = reminderIndex; j < peakFeatures.Count; j++) {

                    var isotopePeak = peakFeatures[j];
                    var isotopeRt = isotopePeak.ChromXsTop.RT.Value;
                    var isotopeMz = isotopePeak.PrecursorMz;
                    var diffMz = Math.Abs(predIsotopicMass - isotopeMz);
                    var diffRt = Math.Abs(rtFocused - isotopeRt);

                    if (diffMz < tolerance && diffRt < rtMargin) {

                        if (isotopeTemps[i].PeakID == -1) {
                            isotopeTemps[i] = new IsotopeTemp() {
                                WeightNumber = i, Mz = isotopeMz,
                                Intensity = isotopePeak.PeakHeightTop, PeakID = j
                            };
                            rtFocused = isotopeRt;
                            mzFocused = isotopeMz;
                        }
                        else {
                            if (Math.Abs(isotopeTemps[i].Mz - predIsotopicMass) > Math.Abs(isotopeMz - predIsotopicMass)) {
                                isotopeTemps[i].Mz = isotopeMz;
                                isotopeTemps[i].Intensity = isotopePeak.PeakHeightTop;
                                isotopeTemps[i].PeakID = j;

                                rtFocused = isotopeRt;
                                mzFocused = isotopeMz;
                            }
                        }
                    }
                    else if (isotopePeak.PrecursorMz >= predIsotopicMass + tolerance) {
                        if (j == peakFeatures.Count - 1) break;
                        reminderIndex = j;
                        if (isotopeTemps[i - 1].PeakID == -1 && isotopeTemps[i].PeakID == -1) {
                            isFinished = true;
                        }
                        else if (isotopeTemps[i].PeakID == -1) {
                            mzFocused += (double)c13_c12Diff / (double)predChargeNumber;
                        }
                        break;
                    }
                }
                if (isFinished)
                    break;
            }

            var monoisotopicMass = (double)monoIsoPeak.PrecursorMz * (double)predChargeNumber;
            var simulatedFormulaByAlkane = getSimulatedFormulaByAlkane(monoisotopicMass);

            //from here, simple decreasing will be expected for <= 800 Da
            //simulated profiles by alkane formula will be projected to the real abundances for the peaks of more than 800 Da
            IsotopeProperty simulatedIsotopicPeaks = null;
            var isIsotopeDetected = false;
            if (monoisotopicMass > 800)
                simulatedIsotopicPeaks = IsotopeCalculator.GetNominalIsotopeProperty(simulatedFormulaByAlkane, maxTraceNumber + 1, iupac);
            for (int i = 1; i <= maxTraceNumber; i++) {
                if (isotopeTemps[i].PeakID == -1) continue;
                if (isotopeTemps[i - 1].PeakID == -1 && isotopeTemps[i].PeakID == -1) break;

                if (monoisotopicMass <= 800) {
                    if (isotopeTemps[i - 1].Intensity > isotopeTemps[i].Intensity && param.IsBrClConsideredForIsotopes == false) {
                        peakFeatures[isotopeTemps[i].PeakID].PeakCharacter.IsotopeParentPeakID = monoIsoPeak.PeakID;
                        peakFeatures[isotopeTemps[i].PeakID].PeakCharacter.IsotopeWeightNumber = i;
                        peakFeatures[isotopeTemps[i].PeakID].PeakCharacter.Charge = monoIsoPeak.PeakCharacter.Charge;
                        isIsotopeDetected = true;
                    }
                    else if (param.IsBrClConsideredForIsotopes == true) {
                        peakFeatures[isotopeTemps[i].PeakID].PeakCharacter.IsotopeParentPeakID = monoIsoPeak.PeakID;
                        peakFeatures[isotopeTemps[i].PeakID].PeakCharacter.IsotopeWeightNumber = i;
                        peakFeatures[isotopeTemps[i].PeakID].PeakCharacter.Charge = monoIsoPeak.PeakCharacter.Charge;
                        isIsotopeDetected = true;
                    }
                    else {
                        break;
                    }
                }
                else {
                    if (isotopeTemps[i - 1].Intensity <= 0) break;
                    var expRatio = isotopeTemps[i].Intensity / isotopeTemps[i - 1].Intensity;
                    var simRatio = simulatedIsotopicPeaks.IsotopeProfile[i].RelativeAbundance / simulatedIsotopicPeaks.IsotopeProfile[i - 1].RelativeAbundance;

                    if (Math.Abs(expRatio - simRatio) < 5.0) {
                        peakFeatures[isotopeTemps[i].PeakID].PeakCharacter.IsotopeParentPeakID = monoIsoPeak.PeakID;
                        peakFeatures[isotopeTemps[i].PeakID].PeakCharacter.IsotopeWeightNumber = i;
                        peakFeatures[isotopeTemps[i].PeakID].PeakCharacter.Charge = monoIsoPeak.PeakCharacter.Charge;
                        isIsotopeDetected = true;
                    }
                    else {
                        break;
                    }
                }
            }
            if (!isIsotopeDetected) {
                monoIsoPeak.PeakCharacter.Charge = 1;
            }
        }

        /// <summary>
        /// peak list must be sorted by m/z (ordering)
        /// peak should be initialized by new Peak() { Mz = spec[0], Intensity = spec[1], Charge = 1, IsotopeFrag = false, Comment = "NA" }
        /// </summary>
        public static void MsmsIsotopeRecognition(List<SpectrumPeak> peaks, 
            int maxTraceNumber, int maxChargeNumber, double tolerance,
            IupacDatabase iupac) {
            var c13_c12Diff = MassDiffDictionary.C13_C12;  //1.003355F;
            for (int i = 0; i < peaks.Count; i++) {
                var peak = peaks[i];
                if (peak.Comment != "NA") continue;
                peak.IsotopeFrag = false;
                peak.Comment = i.ToString();

                // charge state checking at M + 1
                var predChargeNumber = 1;
                for (int j = i + 1; j < peaks.Count; j++) {
                    var isotopePeak = peaks[j];
                    if (isotopePeak.Mass > peak.Mass + c13_c12Diff + tolerance) break;
                    if (isotopePeak.Comment != "NA") continue;

                    for (int k = maxChargeNumber; k >= 1; k--) {
                        var predIsotopeMass = (double)peak.Mass + (double)c13_c12Diff / (double)k;
                        var diff = Math.Abs(predIsotopeMass - isotopePeak.Mass);
                        if (diff < tolerance) {
                            predChargeNumber = k;
                            if (k <= 3) {
                                break;
                            }
                            else if (k == 4 || k == 5) {
                                var predNextIsotopeMass = (double)peak.Mass + (double)c13_c12Diff / (double)(k - 1);
                                var nextDiff = Math.Abs(predNextIsotopeMass - isotopePeak.Mass);
                                if (diff > nextDiff) predChargeNumber = k - 1;
                                break;
                            }
                            else if (k >= 6) {
                                var predNextIsotopeMass = (double)peak.Mass + (double)c13_c12Diff / (double)(k - 1);
                                var nextDiff = Math.Abs(predNextIsotopeMass - isotopePeak.Mass);
                                if (diff > nextDiff) {
                                    predChargeNumber = k - 1;
                                    diff = nextDiff;

                                    predNextIsotopeMass = (double)peak.Mass + (double)c13_c12Diff / (double)(k - 2);
                                    nextDiff = Math.Abs(predNextIsotopeMass - isotopePeak.Mass);

                                    if (diff > nextDiff) {
                                        predChargeNumber = k - 2;
                                        diff = nextDiff;
                                    }
                                }
                                break;
                            }
                        }
                    }
                    if (predChargeNumber != 1) break;
                }
                peak.Charge = predChargeNumber;

                // isotope grouping till M + 8
                var isotopeTemps = new IsotopeTemp[maxTraceNumber + 1];
                isotopeTemps[0] = new IsotopeTemp() { WeightNumber = 0, Mz = peak.Mass, Intensity = peak.Intensity, PeakID = i };

                var reminderIndex = i + 1;
                var isFinished = false;
                for (int j = 1; j <= maxTraceNumber; j++) {
                    var predIsotopicMass = (double)peak.Mass + (double)j * c13_c12Diff / (double)predChargeNumber;

                    for (int k = reminderIndex; k < peaks.Count; k++) {
                        var isotopePeak = peaks[k];
                        if (isotopePeak.Comment != "NA") continue;

                        if (predIsotopicMass - tolerance < isotopePeak.Mass && isotopePeak.Mass < predIsotopicMass + tolerance) {
                            if (isotopeTemps[j] == null) {
                                isotopeTemps[j] = new IsotopeTemp() {
                                    WeightNumber = j,
                                    Mz = isotopePeak.Mass,
                                    Intensity = isotopePeak.Intensity,
                                    PeakID = k
                                };
                            } else {
                                if (Math.Abs(isotopeTemps[j].Mz - predIsotopicMass) > Math.Abs(isotopePeak.Mass - predIsotopicMass)) {
                                    isotopeTemps[j].Mz = isotopePeak.Mass;
                                    isotopeTemps[j].Intensity = isotopePeak.Intensity;
                                    isotopeTemps[j].PeakID = k;
                                }
                            }
                        }
                        else if (isotopePeak.Mass >= predIsotopicMass + tolerance) {
                            reminderIndex = k;
                            if (isotopeTemps[j] == null) isFinished = true;
                            break;
                        }
                    }
                    if (isFinished)
                        break;
                }

                // finalize and store
                var reminderIntensity = peak.Intensity;
                var monoisotopicMass = (double)peak.Mass * (double)predChargeNumber;
                var simulatedFormulaByAlkane = getSimulatedFormulaByAlkane(monoisotopicMass);

                //from here, simple decreasing will be expected for <= 800 Da
                //simulated profiles by alkane formula will be projected to the real abundances for the peaks of more than 800 Da
                IsotopeProperty simulatedIsotopicPeaks = null;
                if (monoisotopicMass > 800)
                    simulatedIsotopicPeaks = IsotopeCalculator.GetNominalIsotopeProperty(simulatedFormulaByAlkane, 9, iupac);

                for (int j = 1; j <= maxTraceNumber; j++) {
                    if (isotopeTemps[j] == null) break;
                    if (isotopeTemps[j].Intensity <= 0) break;

                    if (monoisotopicMass <= 800) {
                        if (isotopeTemps[j - 1].Intensity > isotopeTemps[j].Intensity) {
                            peaks[isotopeTemps[j].PeakID].IsotopeFrag = true;
                            peaks[isotopeTemps[j].PeakID].Charge = peak.Charge;
                            peaks[isotopeTemps[j].PeakID].Comment = i.ToString();
                        }
                        else {
                            break;
                        }
                    }
                    else {
                        var expRatio = isotopeTemps[j].Intensity / isotopeTemps[j - 1].Intensity;
                        var simRatio = simulatedIsotopicPeaks.IsotopeProfile[j].RelativeAbundance / simulatedIsotopicPeaks.IsotopeProfile[j - 1].RelativeAbundance;

                        if (Math.Abs(expRatio - simRatio) < 5.0) {
                            peaks[isotopeTemps[j].PeakID].IsotopeFrag = true;
                            peaks[isotopeTemps[j].PeakID].Charge = peak.Charge;
                            peaks[isotopeTemps[j].PeakID].Comment = i.ToString();
                        }
                        else {
                            break;
                        }
                    }
                }
            }
        }

        public static void Process(IEnumerable<AlignmentSpotProperty> alignmentSpots, ParameterBase param, IupacDatabase iupac) {
            var rtMargin = 0.06F;
            var isotopeMax = 8.1;
            var spots = alignmentSpots.OrderBy(spot => spot.MassCenter).ToList();
            var dummy = new AlignmentSpotProperty(); // used for binary search

            foreach (var target in spots) {
                if (target.PeakCharacter.IsotopeWeightNumber > 0) continue;

                var spotRt = target.TimesCenter;
                var spotMz = target.MassCenter;

                dummy.MassCenter = spotMz - 0.0001f;
                var idx = SearchCollection.LowerBound(spots, dummy, (x, y) => x.MassCenter.CompareTo(y.MassCenter));

                var isotopeCandidates = new List<AlignmentSpotProperty> { target };

                for (int i = idx; i < spots.Count; i++) {
                    var spot = spots[i];
                    if (spot.MasterAlignmentID == target.MasterAlignmentID) continue;
                    if (!spot.IsUnknown) continue;
                    if (spot.TimesCenter.Value < spotRt.Value - rtMargin) continue;
                    if (spot.TimesCenter.Value > spotRt.Value + rtMargin) continue;
                    if (spot.PeakCharacter.IsotopeWeightNumber >= 0) continue;
                    if (spot.MassCenter <= spotMz) continue;
                    if (spot.MassCenter > spotMz + isotopeMax) continue;

                    isotopeCandidates.Add(spot);
                }
                EstimateIsotopes(isotopeCandidates, param, iupac);
            }
        }

        public static void EstimateIsotopes(List<AlignmentSpotProperty> spots, ParameterBase param, IupacDatabase iupac) {
            EstimateIsotopes(spots, iupac, param.CentroidMs1Tolerance, param.IsBrClConsideredForIsotopes);
        }

        public static void EstimateIsotopes(List<AlignmentSpotProperty> spots, IupacDatabase iupac, double ms1Tolerance, bool isBrClConsidreredForIsotopes = false) {

            var maxTraceNumber = 15;
            var c13_c12Diff = MassDiffDictionary.C13_C12;  //1.003355F;

            var monoIsoPeak = spots[0];
            monoIsoPeak.PeakCharacter.IsotopeWeightNumber = 0;
            monoIsoPeak.PeakCharacter.IsotopeParentPeakID = monoIsoPeak.AlignmentID;

            var ppm = MolecularFormulaUtility.PpmCalculator(200.0, 200.0 + ms1Tolerance); //based on m/z 400
            var tolerance = MolecularFormulaUtility.ConvertPpmToMassAccuracy(monoIsoPeak.MassCenter, ppm);
            var predChargeNumber = monoIsoPeak.PeakCharacter.Charge;

            var isotopeTemps = GetIsotopeCandidates(spots, monoIsoPeak, c13_c12Diff / predChargeNumber, tolerance, maxTraceNumber);
            SetParentToIsotopes(spots, monoIsoPeak, isotopeTemps, maxTraceNumber, iupac, isBrClConsidreredForIsotopes);
        }

        private static IsotopeTemp[] GetIsotopeCandidates(IReadOnlyList<AlignmentSpotProperty> spots,
            AlignmentSpotProperty monoIsoPeak, double isotopeUnit, double tolerance, int maxTraceNumber) {

            var isotopeTemps = new IsotopeTemp[maxTraceNumber + 1];

            var mzFocused = monoIsoPeak.MassCenter;
            isotopeTemps[0] = new IsotopeTemp() {
                WeightNumber = 0, Mz = mzFocused,
                Intensity = monoIsoPeak.HeightAverage, PeakID = monoIsoPeak.AlignmentID
            };
            for (int i = 1; i < isotopeTemps.Length; i++) {
                isotopeTemps[i] = new IsotopeTemp() {
                    WeightNumber = i, Mz = mzFocused + i * isotopeUnit,
                    Intensity = 0, PeakID = -1
                };
            }
            
            var j = 1;
            for (int i = 1; i <= maxTraceNumber; i++) {
                var predIsotopicMass = mzFocused + isotopeUnit;
                for (; j < spots.Count; j++) {

                    var isotopePeak = spots[j];
                    var isotopeMz = isotopePeak.MassCenter;

                    if (Math.Abs(predIsotopicMass - isotopeMz) < tolerance) {

                        if (isotopeTemps[i].PeakID == -1 || Math.Abs(isotopeTemps[i].Mz - predIsotopicMass) > Math.Abs(isotopeMz - predIsotopicMass)) {
                            isotopeTemps[i].Mz = isotopeMz;
                            isotopeTemps[i].Intensity = isotopePeak.HeightAverage;
                            isotopeTemps[i].PeakID = j;

                            mzFocused = isotopeMz;
                        }
                    }
                    else if (isotopeMz >= predIsotopicMass + tolerance) {
                        if (isotopeTemps[i].PeakID == -1) {
                            if (isotopeTemps[i - 1].PeakID == -1)
                                return isotopeTemps;
                            mzFocused += isotopeUnit;
                        }
                        break;
                    }
                }
            }

            return isotopeTemps;
        }

        private static void SetParentToIsotopes(IList<AlignmentSpotProperty> spots, AlignmentSpotProperty monoIsoPeak,
            IReadOnlyList<IsotopeTemp> isotopeTemps, int maxTraceNumber, IupacDatabase iupac, bool isBrClConsidered) {

            Func<int, bool> predicate = i => isBrClConsidered || isotopeTemps[i - 1].Intensity > isotopeTemps[i].Intensity;
            var monoisotopicMass = monoIsoPeak.MassCenter * monoIsoPeak.PeakCharacter.Charge;

            //from here, simple decreasing will be expected for <= 800 Da
            //simulated profiles by alkane formula will be projected to the real abundances for the peaks of more than 800 Da
            if (monoisotopicMass > 800) {
                var simulatedFormulaByAlkane = getSimulatedFormulaByAlkane(monoisotopicMass);
                var simulatedIsotopicPeaks = IsotopeCalculator.GetNominalIsotopeProperty(simulatedFormulaByAlkane, maxTraceNumber + 1, iupac);
                predicate = i => {
                    if (isotopeTemps[i - 1].Intensity <= 0) return false;
                    var expRatio = isotopeTemps[i].Intensity / isotopeTemps[i - 1].Intensity;
                    var simRatio = simulatedIsotopicPeaks.IsotopeProfile[i].RelativeAbundance / simulatedIsotopicPeaks.IsotopeProfile[i - 1].RelativeAbundance;

                    return Math.Abs(expRatio - simRatio) < 5.0;
                };
            }

            for (int i = 1; i <= maxTraceNumber; i++) {
                if (isotopeTemps[i].PeakID == -1) {
                    if (isotopeTemps[i - 1].PeakID == -1) break;
                    continue;
                }
                if (!predicate(i)) break;

                spots[isotopeTemps[i].PeakID].PeakCharacter.IsotopeParentPeakID = monoIsoPeak.AlignmentID;
                spots[isotopeTemps[i].PeakID].PeakCharacter.IsotopeWeightNumber = i;
                spots[isotopeTemps[i].PeakID].PeakCharacter.Charge = monoIsoPeak.PeakCharacter.Charge;
            }
        }

        private static string getSimulatedFormulaByAlkane(double mass) {

            var ch2Mass = 14.0;
            var carbonCount = (int)(mass / ch2Mass);
            var hCount = (int)(carbonCount * 2);

            if (carbonCount == 0 || carbonCount == 1)
                return "CH2";
            else {
                return "C" + carbonCount.ToString() + "H" + hCount.ToString();
            }

        }
    }
}
