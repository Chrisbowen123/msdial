﻿using CompMs.Common.Components;
using CompMs.Common.DataObj;
using CompMs.Common.DataObj.Result;
using CompMs.Common.Extension;
using CompMs.Common.Parameter;
using CompMs.Common.Proteomics.DataObj;
using CompMs.MsdialCore.DataObj;
using CompMs.MsdialCore.MSDec;
using CompMs.MsdialCore.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CompMs.MsdialCore.Algorithm.Annotation
{
    public class AnnotationProcessOfProteoMetabolomics : IAnnotationProcess {
        public void RunAnnotation(
            IReadOnlyList<ChromatogramPeakFeature> chromPeakFeatures,
            IReadOnlyList<MSDecResult> msdecResults,
            IDataProvider provider,
            int numThreads = 1,
            CancellationToken token = default,
            Action<double> reportAction = null) {
            if (numThreads <= 1) {
                RunBySingleThread(chromPeakFeatures, msdecResults, provider, reportAction);
            }
            else {
                RunByMultiThread(chromPeakFeatures, msdecResults, provider, numThreads, token, reportAction);
            }
        }

        public async Task RunAnnotationAsync(
            IReadOnlyList<ChromatogramPeakFeature> chromPeakFeatures,
            IReadOnlyList<MSDecResult> msdecResults,
            IDataProvider provider,
            int numThreads = 1,
            CancellationToken token = default,
            Action<double> reportAction = null) {
            if (numThreads <= 1) {
                await RunBySingleThreadAsync(chromPeakFeatures, msdecResults, provider, token, reportAction);
            }
            else {
                await RunByMultiThreadAsync(chromPeakFeatures, msdecResults, provider, numThreads, token, reportAction);
            }
        }

        private readonly List<(IAnnotationQueryFactory<MsScanMatchResult> Factory, MsRefSearchParameterBase Parameter)> _moleculeContainerPairs;
        private readonly List<(IAnnotationQueryFactory<MsScanMatchResult> Factory, MsRefSearchParameterBase Parameter)> _peptideContainerPairs;
        private readonly IMatchResultEvaluator<MsScanMatchResult> _evaluator;
        private readonly IMatchResultRefer<MoleculeMsReference, MsScanMatchResult> _moleculeRefer;
        private readonly IMatchResultRefer<PeptideMsReference, MsScanMatchResult> _peptideRefer;

        public AnnotationProcessOfProteoMetabolomics(
            List<(IAnnotationQueryFactory<MsScanMatchResult>, MsRefSearchParameterBase)> moleculeContainerPairs,
            List<(IAnnotationQueryFactory<MsScanMatchResult>, MsRefSearchParameterBase)> peptideContainerPairs,
            IMatchResultEvaluator<MsScanMatchResult> evaluator,
            IMatchResultRefer<MoleculeMsReference, MsScanMatchResult> moleculeRefer, IMatchResultRefer<PeptideMsReference, MsScanMatchResult> peptideRefer) {

            _moleculeContainerPairs = moleculeContainerPairs ?? throw new ArgumentNullException(nameof(moleculeContainerPairs));
            _peptideContainerPairs = peptideContainerPairs ?? throw new ArgumentNullException(nameof(peptideContainerPairs));
            _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
            _moleculeRefer = moleculeRefer ?? throw new ArgumentNullException(nameof(moleculeRefer));
            _peptideRefer = peptideRefer ?? throw new ArgumentNullException(nameof(peptideRefer));
        }

        private Dictionary<int, List<int>> GetParentID2IsotopePeakIDs(IReadOnlyList<ChromatogramPeakFeature> chromPeakFeatures) {
            var isotopeGroupDict = new Dictionary<int, List<int>>();
            var peaks = chromPeakFeatures.OrderBy(n => n.PeakCharacter.IsotopeParentPeakID).ThenBy(n => n.PeakCharacter.IsotopeWeightNumber).ToList();
            for (int i = 0; i < peaks.Count; i++) {
                if (peaks[i].PeakCharacter.IsotopeWeightNumber == 0) {
                    isotopeGroupDict[peaks[i].PeakCharacter.IsotopeParentPeakID] = new List<int>();
                }
                else {
                    isotopeGroupDict[peaks[i].PeakCharacter.IsotopeParentPeakID].Add(peaks[i].MasterPeakID);
                }
            }
            return isotopeGroupDict;
        }


        private void RunBySingleThread(
            IReadOnlyList<ChromatogramPeakFeature> chromPeakFeatures, 
            IReadOnlyList<MSDecResult> msdecResults, 
            IDataProvider provider, 
            Action<double> reportAction) {
            var spectrums = provider.LoadMsSpectrums();
            var parentID2IsotopePeakIDs = GetParentID2IsotopePeakIDs(chromPeakFeatures);

            for (int i = 0; i < chromPeakFeatures.Count; i++) {
                var chromPeakFeature = chromPeakFeatures[i];
                if (chromPeakFeature.PeakCharacter.IsotopeWeightNumber == 0) {
                    var msdecResult = GetRepresentativeMSDecResult(chromPeakFeature, i, msdecResults, parentID2IsotopePeakIDs);
                    RunAnnotationCore(chromPeakFeature, msdecResult, spectrums);
                }
                reportAction?.Invoke((double)(i + 1) / chromPeakFeatures.Count);
            };
        }

        private MSDecResult GetRepresentativeMSDecResult(ChromatogramPeakFeature chromPeakFeature, int index, IReadOnlyList<MSDecResult> msdecResults, Dictionary<int, List<int>> parentID2IsotopePeakIDs) {
            var msdecResult = msdecResults[index];
            if (msdecResult.Spectrum.IsEmptyOrNull()) {
                var ids = parentID2IsotopePeakIDs[chromPeakFeature.PeakCharacter.IsotopeParentPeakID];
                foreach (var id in ids) {
                    if (!msdecResults[id].Spectrum.IsEmptyOrNull()) {
                        msdecResult = msdecResults[id];
                        chromPeakFeature.MSDecResultIdUsed = id;
                        break;
                    }
                }
            }
            else {
                chromPeakFeature.MSDecResultIdUsed = index;
            }
            return msdecResult;
        }

        private void RunByMultiThread(IReadOnlyList<ChromatogramPeakFeature> chromPeakFeatures, IReadOnlyList<MSDecResult> msdecResults, IDataProvider provider, int numThreads, CancellationToken token, Action<double> reportAction) {
            var spectrums = provider.LoadMsSpectrums();
            var parentID2IsotopePeakIDs = GetParentID2IsotopePeakIDs(chromPeakFeatures);
            var syncObj = new object();
            var counter = 0;
            var totalCount = chromPeakFeatures.Count(n => n.PeakCharacter.IsotopeWeightNumber == 0);
            Enumerable.Range(0, chromPeakFeatures.Count)
                .AsParallel()
                .WithCancellation(token)
                .WithDegreeOfParallelism(numThreads)
                .ForAll(i => {
                    var chromPeakFeature = chromPeakFeatures[i];
                    var msdecResult = GetRepresentativeMSDecResult(chromPeakFeature, i, msdecResults, parentID2IsotopePeakIDs);
                    if (chromPeakFeature.PeakCharacter.IsotopeWeightNumber == 0) {
                        RunAnnotationCore(chromPeakFeature, msdecResult, spectrums);
                        lock (syncObj) {
                            counter++;
                            reportAction?.Invoke((double)counter / (double)totalCount);
                        }
                    }
                });
        }

        private async Task RunBySingleThreadAsync(
             IReadOnlyList<ChromatogramPeakFeature> chromPeakFeatures,
             IReadOnlyList<MSDecResult> msdecResults,
             IDataProvider provider,
             CancellationToken token,
             Action<double> reportAction) {
            var spectrums = provider.LoadMsSpectrums();
            var parentID2IsotopePeakIDs = GetParentID2IsotopePeakIDs(chromPeakFeatures);
            for (int i = 0; i < chromPeakFeatures.Count; i++) {
                var chromPeakFeature = chromPeakFeatures[i];
                var msdecResult = GetRepresentativeMSDecResult(chromPeakFeature, i, msdecResults, parentID2IsotopePeakIDs);
                if (chromPeakFeature.PeakCharacter.IsotopeWeightNumber == 0) {
                    await RunAnnotationCoreAsync(chromPeakFeature, msdecResult, spectrums, token);
                }
                reportAction?.Invoke((double)(i + 1) / chromPeakFeatures.Count);
            };
        }

        private async Task RunByMultiThreadAsync(
            IReadOnlyList<ChromatogramPeakFeature> chromPeakFeatures,
            IReadOnlyList<MSDecResult> msdecResults,
            IDataProvider provider,
            int numThreads,
            CancellationToken token,
            Action<double> reportAction) {
            var spectrums = provider.LoadMsSpectrums();
            var parentID2IsotopePeakIDs = GetParentID2IsotopePeakIDs(chromPeakFeatures);
            using (var sem = new SemaphoreSlim(numThreads)) {
                var annotationTasks = new List<Task>();
                for (int i = 0; i < chromPeakFeatures.Count; i++) {
                    var v = Task.Run(async () => {
                        await sem.WaitAsync();

                        try {
                            var chromPeakFeature = chromPeakFeatures[i];
                            var msdecResult = GetRepresentativeMSDecResult(chromPeakFeature, i, msdecResults, parentID2IsotopePeakIDs);
                            if (chromPeakFeature.PeakCharacter.IsotopeWeightNumber == 0) {
                                await RunAnnotationCoreAsync(chromPeakFeature, msdecResult, spectrums, token);
                            }
                        }
                        finally {
                            sem.Release();
                            reportAction?.Invoke((double)(i + 1) / chromPeakFeatures.Count);
                        }
                    });
                    annotationTasks.Add(v);
                }
                await Task.WhenAll(annotationTasks);
            }
        }

        private void RunAnnotationCore(
             ChromatogramPeakFeature chromPeakFeature,
             MSDecResult msdecResult,
             IReadOnlyList<RawSpectrum> msSpectrums) {

            foreach (var (Factory, Parameter) in _peptideContainerPairs) {
                var pepQuery = Factory.Create(
                    chromPeakFeature,
                    msdecResult,
                    msSpectrums[chromPeakFeature.MS1RawSpectrumIdTop].Spectrum,
                    chromPeakFeature.PeakCharacter,
                    Parameter);
                SetPepAnnotationResult(chromPeakFeature, pepQuery);
            }
            foreach (var (Factory, Parameter) in _moleculeContainerPairs) {
                var query = Factory.Create(
                    chromPeakFeature,
                    msdecResult,
                    msSpectrums[chromPeakFeature.MS1RawSpectrumIdTop].Spectrum,
                    chromPeakFeature.PeakCharacter,
                    Parameter);
                SetAnnotationResult(chromPeakFeature, query, _evaluator);
            }
            
            SetRepresentativeProperty(chromPeakFeature);
        }

        private async Task RunAnnotationCoreAsync(
            ChromatogramPeakFeature chromPeakFeature,
            MSDecResult msdecResult,
            IReadOnlyList<RawSpectrum> msSpectrums,
            CancellationToken token = default) {

            foreach (var (Factory, Parameter) in _peptideContainerPairs) {
                var pepQuery = Factory.Create(
                    chromPeakFeature,
                    msdecResult,
                    msSpectrums[chromPeakFeature.MS1RawSpectrumIdTop].Spectrum,
                    chromPeakFeature.PeakCharacter,
                    Parameter);
                token.ThrowIfCancellationRequested();
                await Task.Run(() => SetPepAnnotationResult(chromPeakFeature, pepQuery), token);
            }
            token.ThrowIfCancellationRequested();

            foreach (var (Factory, Parameter) in _moleculeContainerPairs) {
                var query = Factory.Create(
                    chromPeakFeature,
                    msdecResult,
                    msSpectrums[chromPeakFeature.MS1RawSpectrumIdTop].Spectrum,
                    chromPeakFeature.PeakCharacter,
                    Parameter);
                token.ThrowIfCancellationRequested();
                await Task.Run(() => SetAnnotationResult(chromPeakFeature, query, _evaluator), token);
            }
            token.ThrowIfCancellationRequested();
            
            SetRepresentativeProperty(chromPeakFeature);
        }

        private void SetPepAnnotationResult(ChromatogramPeakFeature chromPeakFeature, IAnnotationQuery<MsScanMatchResult> query) {
            var candidates = query.FindCandidates().ToList();
            if (candidates.Count == 2) {

                candidates[0].IsReferenceMatched = true;
                candidates[1].IsReferenceMatched = true;
                chromPeakFeature.MatchResults.AddResult(candidates[0]); // peptide query
                chromPeakFeature.MatchResults.AddResult(candidates[1]); // decoy query
            }
        }

        private void SetAnnotationResult(ChromatogramPeakFeature chromPeakFeature, IAnnotationQuery<MsScanMatchResult> query, IMatchResultEvaluator<MsScanMatchResult> matchResultEvaluator) {
            var candidates = query.FindCandidates();
            var results = matchResultEvaluator.FilterByThreshold(candidates);
            var matches = matchResultEvaluator.SelectReferenceMatchResults(results);
            if (matches.Count > 0) {
                var best = matchResultEvaluator.SelectTopHit(matches);
                best.IsReferenceMatched = true;
                chromPeakFeature.MatchResults.AddResult(best);
            }
            else if (results.Count > 0) {
                var best = matchResultEvaluator.SelectTopHit(results);
                best.IsAnnotationSuggested = true;
                chromPeakFeature.MatchResults.AddResult(best);
            }
        }

        private void SetRepresentativeProperty(ChromatogramPeakFeature chromPeakFeature) {
            var representative = chromPeakFeature.MatchResults.Representative;
            
            if (representative.Source == SourceType.FastaDB) {
                if (_evaluator is null) {
                    return;
                }
                else if (_evaluator.IsReferenceMatched(representative)) {
                    DataAccess.SetPeptideMsProperty(chromPeakFeature, _peptideRefer.Refer(representative), representative);
                }
                else if (_evaluator.IsAnnotationSuggested(representative)) {
                    DataAccess.SetPeptideMsPropertyAsSuggested(chromPeakFeature, _peptideRefer.Refer(representative), representative);
                }
            }
            else {
                if (_evaluator is null) {
                    return;
                }
                else if (_evaluator.IsReferenceMatched(representative)) {
                    DataAccess.SetMoleculeMsProperty(chromPeakFeature, _moleculeRefer.Refer(representative), representative);
                }
                else if (_evaluator.IsAnnotationSuggested(representative)) {
                    DataAccess.SetMoleculeMsPropertyAsSuggested(chromPeakFeature, _moleculeRefer.Refer(representative), representative);
                }
            }
        }
    }
}
