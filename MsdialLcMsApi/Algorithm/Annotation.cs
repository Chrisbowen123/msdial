﻿using CompMs.Common.Algorithm.Scoring;
using CompMs.Common.Components;
using CompMs.Common.DataObj;
using CompMs.Common.DataObj.Result;
using CompMs.Common.Extension;
using CompMs.Common.FormulaGenerator.Function;
using CompMs.Common.Utility;
using CompMs.MsdialCore.Algorithm.Annotation;
using CompMs.MsdialCore.DataObj;
using CompMs.MsdialCore.MSDec;
using CompMs.MsdialCore.Utility;
using CompMs.MsdialLcmsApi.Parameter;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CompMs.MsdialLcMsApi.Algorithm.Annotation {
    public class Annotation {

        public double InitialProgress { get; set; } = 60.0;
        public double ProgressMax { get; set; } = 30.0;

        public Annotation(double InitialProgress, double ProgressMax) {
            this.InitialProgress = InitialProgress;
            this.ProgressMax = ProgressMax;
        }

        // mspDB must be sorted by precursor mz
        // textDB must be sorted by precursor mz
        public void MainProcess(List<RawSpectrum> spectrumList,
            List<ChromatogramPeakFeature> chromPeakFeatures, List<MSDecResult> msdecResults,
            IReadOnlyCollection<ISerializableAnnotatorContainer> annotatorContainers,
            List<MoleculeMsReference> mspDB, List<MoleculeMsReference> textDB,
            MsdialLcmsParameter param, Action<int> reportAction) {

            for (int i = 0; i < chromPeakFeatures.Count; i++) {
                // count of chrompeakfeatures and msdecresults should be same
                var chromPeak = chromPeakFeatures[i];
                var msdecResult = msdecResults[i];
                if (chromPeak.PeakCharacter.IsotopeWeightNumber == 0) {
                    LcMsMsMatchMethod(chromPeak, msdecResult, spectrumList, annotatorContainers, mspDB, textDB, param);
                }
                //Console.WriteLine("Done {0}/{1}", i, chromPeakFeatures.Count);
                ReportProgress.Show(InitialProgress, ProgressMax, i, chromPeakFeatures.Count(), reportAction);
            }
        }

        public void LcMsMsMatchMethod(ChromatogramPeakFeature chromPeak, MSDecResult msdecResult,
            List<RawSpectrum> spectrumList, IReadOnlyCollection<ISerializableAnnotatorContainer> annotatorContainers, List<MoleculeMsReference> mspDB, List<MoleculeMsReference> textDB, MsdialLcmsParameter param) {

            if (mspDB.IsEmptyOrNull() && textDB.IsEmptyOrNull() && annotatorContainers.IsEmptyOrNull()) return;

            var isotopes = DataAccess.GetIsotopicPeaks(spectrumList, chromPeak.MS1RawSpectrumIdTop, (float)chromPeak.Mass, param.CentroidMs1Tolerance);
            var normMSScanProp = DataAccess.GetNormalizedMSScanProperty(chromPeak, msdecResult, param);

            var mz = chromPeak.Mass;
            var ms1Tol = param.MspSearchParam.Ms1Tolerance;
            var ppm = Math.Abs(MolecularFormulaUtility.PpmCalculator(500.00, 500.00 + ms1Tol));
            if (mz > 500) {
                ms1Tol = (float)MolecularFormulaUtility.ConvertPpmToMassAccuracy(mz, ppm);
            }

            if (!mspDB.IsEmptyOrNull()) {
                var startID = SearchCollection.LowerBound(mspDB,
                    new MoleculeMsReference() { PrecursorMz = chromPeak.Mass - ms1Tol },
                    (a, b) => a.PrecursorMz.CompareTo(b.PrecursorMz));
                var candidates = new List<MsScanMatchResult>();
                for (int i = startID; i < mspDB.Count; i++) {
                    var refSpec = mspDB[i];
                    if (refSpec.PrecursorMz > mz + ms1Tol) break;
                    if (refSpec.PrecursorMz < mz - ms1Tol) continue;

                    MsScanMatchResult result = MsScanMatching.CompareMS2ScanProperties(msdecResult, refSpec, param.MspSearchParam, 
                        param.TargetOmics, -1.0, isotopes, refSpec.IsotopicPeaks, param.AndromedaDelta, param.AndromedaMaxPeaks);
                    //if (param.TargetOmics == Common.Enum.TargetOmics.Metabolomics) {
                    //    result = MsScanMatching.CompareMS2ScanProperties(msdecResult, refSpec, param.MspSearchParam, isotopes, refSpec.IsotopicPeaks);
                    //}
                    //else if (param.TargetOmics == Common.Enum.TargetOmics.Lipidomics) {
                    //    result = MsScanMatching.CompareMS2LipidomicsScanProperties(msdecResult, refSpec, param.MspSearchParam, isotopes, refSpec.IsotopicPeaks);
                    //}
                    if (result != null && (result.IsSpectrumMatch || result.IsPrecursorMzMatch)) {
                        result.Source = SourceType.MspDB;
                        result.LibraryIDWhenOrdered = i;
                        candidates.Add(result);
                    }
                }

                foreach (var (result, index) in candidates.OrEmptyIfNull().OrderByDescending(n => n.TotalScore).WithIndex()) {
                    if (index == 0) {
                        chromPeak.MSRawID2MspBasedMatchResult[msdecResult.RawSpectrumID] = result;
                        chromPeak.MatchResults.AddMspResult(msdecResult.RawSpectrumID, result);
                        DataAccess.SetMoleculeMsProperty(chromPeak, mspDB[result.LibraryIDWhenOrdered], result);
                        chromPeak.MSRawID2MspIDs[msdecResult.RawSpectrumID] = new List<int>();
                    }
                    chromPeak.MSRawID2MspIDs[msdecResult.RawSpectrumID].Add(result.LibraryID);
                }
            }

            if (!textDB.IsEmptyOrNull()) {
                var startID = SearchCollection.LowerBound(textDB,
                    new MoleculeMsReference() { PrecursorMz = chromPeak.Mass - ms1Tol },
                    (a, b) => a.PrecursorMz.CompareTo(b.PrecursorMz));
                var candidates = new List<MsScanMatchResult>();
                for (int i = startID; i < textDB.Count; i++) {
                    var refSpec = textDB[i];
                    if (refSpec.PrecursorMz > mz + ms1Tol) break;
                    if (refSpec.PrecursorMz < mz - ms1Tol) continue;

                    var result = MsScanMatching.CompareMS2ScanProperties(msdecResult, refSpec, param.MspSearchParam);
                    result.Source = SourceType.TextDB;
                    if (result.IsPrecursorMzMatch) {
                        result.LibraryIDWhenOrdered = i;
                        candidates.Add(result);
                    }
                }

                foreach (var (result, index) in candidates.OrEmptyIfNull().OrderByDescending(n => n.TotalScore).WithIndex()) {
                    if (index == 0) {
                        chromPeak.TextDbBasedMatchResult = result;
                        DataAccess.SetMoleculeMsProperty(chromPeak, textDB[result.LibraryIDWhenOrdered], result, true);
                    }
                    chromPeak.TextDbIDs.Add(result.LibraryID);
                    chromPeak.MatchResults.AddTextDbResult(result);
                }
            }

            foreach (var annotatorContainer in annotatorContainers) {
                var annotator = annotatorContainer.Annotator;

                var candidates = annotator.FindCandidates(chromPeak, msdecResult, isotopes, annotatorContainer.Parameter);
                var results = annotator.FilterByThreshold(candidates, annotatorContainer.Parameter);
                var matches = annotator.SelectReferenceMatchResults(results, annotatorContainer.Parameter);
                chromPeak.MatchResults.AddResults(results);
                if (matches.Count > 0) {
                    var best = annotator.SelectTopHit(matches, annotatorContainer.Parameter);
                    DataAccess.SetMoleculeMsProperty(chromPeak, annotator.Refer(best), best);
                }               
                else if (results.Count > 0) {
                    var best = annotator.SelectTopHit(results, annotatorContainer.Parameter);
                    DataAccess.SetMoleculeMsPropertyAsSuggested(chromPeak, annotator.Refer(best), best);
                }
            }
        }
    }
}
