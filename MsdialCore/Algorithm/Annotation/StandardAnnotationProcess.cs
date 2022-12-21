﻿using CompMs.Common.Components;
using CompMs.Common.DataObj;
using CompMs.Common.DataObj.Result;
using CompMs.Common.Extension;
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
    public sealed class StandardAnnotationProcess : IAnnotationProcess
    {
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

        public StandardAnnotationProcess(
            IAnnotationQueryFactory<MsScanMatchResult> queryFactory,
            IMatchResultEvaluator<MsScanMatchResult> evaluator,
            IMatchResultRefer<MoleculeMsReference, MsScanMatchResult> refer) {
            _queryFactories = new List<IAnnotationQueryFactory<MsScanMatchResult>>
            {
                queryFactory
            };
            _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
            _refer = refer ?? throw new ArgumentNullException(nameof(refer));
        }

        public StandardAnnotationProcess(IReadOnlyList<IAnnotationQueryFactory<MsScanMatchResult>> queryFactories, IMatchResultEvaluator<MsScanMatchResult> evaluator, IMatchResultRefer<MoleculeMsReference, MsScanMatchResult> refer) {
            _queryFactories = queryFactories;
            _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
            _refer = refer ?? throw new ArgumentNullException(nameof(refer));
        }

        private readonly IReadOnlyList<IAnnotationQueryFactory<MsScanMatchResult>> _queryFactories;
        private readonly IMatchResultEvaluator<MsScanMatchResult> _evaluator;
        private readonly IMatchResultRefer<MoleculeMsReference, MsScanMatchResult> _refer;

        private void RunBySingleThread(
            IReadOnlyList<ChromatogramPeakFeature> chromPeakFeatures,
            IReadOnlyList<MSDecResult> msdecResults,
            IDataProvider provider,
            Action<double> reportAction) {
            for (int i = 0; i < chromPeakFeatures.Count; i++) {
                var chromPeakFeature = chromPeakFeatures[i];
                var msdecResult = msdecResults[i];
                RunAnnotationCore(chromPeakFeature, msdecResult, provider);
                reportAction?.Invoke((double)(i + 1) / chromPeakFeatures.Count);
            };
        }

        private void RunByMultiThread(
            IReadOnlyList<ChromatogramPeakFeature> chromPeakFeatures,
            IReadOnlyList<MSDecResult> msdecResults,
            IDataProvider provider,
            int numThreads,
            CancellationToken token,
            Action<double> reportAction) {
            var syncObj = new object();
            var counter = 0;
            var totalCount = chromPeakFeatures.Count;
            Enumerable.Range(0, chromPeakFeatures.Count)
                .AsParallel()
                .WithCancellation(token)
                .WithDegreeOfParallelism(numThreads)
                .ForAll(i => {
                    var chromPeakFeature = chromPeakFeatures[i];
                    var msdecResult = msdecResults[i];
                    RunAnnotationCore(chromPeakFeature, msdecResult, provider);
                    lock (syncObj) {
                        counter++;
                        reportAction?.Invoke((double)counter / (double)totalCount);
                    }
                });
        }

        private async Task RunBySingleThreadAsync(
            IReadOnlyList<ChromatogramPeakFeature> chromPeakFeatures,
            IReadOnlyList<MSDecResult> msdecResults,
            IDataProvider provider,
            CancellationToken token,
            Action<double> reportAction) {
            var spectrums = provider.LoadMs1Spectrums();
            for (int i = 0; i < chromPeakFeatures.Count; i++) {
                var chromPeakFeature = chromPeakFeatures[i];
                var msdecResult = msdecResults[i];
                await RunAnnotationCoreAsync(chromPeakFeature, msdecResult, spectrums, token);
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
            var spectrums = provider.LoadMs1Spectrums();
            using (var sem = new SemaphoreSlim(numThreads)) {
                var annotationTasks = chromPeakFeatures.Zip(msdecResults, Tuple.Create)
                    .Select(async (pair, i) => {
                        await sem.WaitAsync();

                        try {
                            var chromPeakFeature = pair.Item1;
                            var msdecResult = pair.Item2;
                            await RunAnnotationCoreAsync(chromPeakFeature, msdecResult, spectrums, token);
                        }
                        finally {
                            sem.Release();
                            reportAction?.Invoke((double)(i + 1) / chromPeakFeatures.Count);
                        }
                    });
                await Task.WhenAll(annotationTasks);
            }
        }

        private void RunAnnotationCore(
            ChromatogramPeakFeature chromPeakFeature,
            MSDecResult msdecResult,
            IDataProvider provider) {

            if (!msdecResult.Spectrum.IsEmptyOrNull()) {
                chromPeakFeature.MSDecResultIdUsed = chromPeakFeature.MasterPeakID;
            }
            foreach (var factory in _queryFactories) {
                var query = factory.Create(
                    chromPeakFeature,
                    msdecResult,
                    provider.LoadMsSpectrumFromIndex(chromPeakFeature.MS1RawSpectrumIdTop).Spectrum,
                    chromPeakFeature.PeakCharacter,
                    factory.PrepareParameter());
                SetAnnotationResult(chromPeakFeature, query);
            }
            SetRepresentativeProperty(chromPeakFeature);
        }

        private async Task RunAnnotationCoreAsync(
            ChromatogramPeakFeature chromPeakFeature,
            MSDecResult msdecResult,
            IReadOnlyList<RawSpectrum> msSpectrums,
            CancellationToken token = default) {

            if (!msdecResult.Spectrum.IsEmptyOrNull()) {
                chromPeakFeature.MSDecResultIdUsed = chromPeakFeature.MasterPeakID;
            }
            foreach (var factory in _queryFactories) {
                var query = factory.Create(
                    chromPeakFeature,
                    msdecResult,
                    msSpectrums[chromPeakFeature.MS1RawSpectrumIdTop].Spectrum,
                    chromPeakFeature.PeakCharacter,
                    factory.PrepareParameter());
                token.ThrowIfCancellationRequested();
                await Task.Run(() => SetAnnotationResult(chromPeakFeature, query), token);
            }
            token.ThrowIfCancellationRequested();
            SetRepresentativeProperty(chromPeakFeature);
        }

        private void SetAnnotationResult(ChromatogramPeakFeature chromPeakFeature, IAnnotationQuery<MsScanMatchResult> query) {
            var candidates = query.FindCandidates();
            var results = _evaluator.FilterByThreshold(candidates);
            var matches = _evaluator.SelectReferenceMatchResults(results);
            if (matches.Count > 0) {
                var best = _evaluator.SelectTopHit(matches);
                best.IsReferenceMatched = true;
                chromPeakFeature.MatchResults.AddResult(best);
            }
            else if (results.Count > 0) {
                var best = _evaluator.SelectTopHit(results);
                best.IsAnnotationSuggested = true;
                chromPeakFeature.MatchResults.AddResult(best);
            }
        }

        private void SetRepresentativeProperty(ChromatogramPeakFeature chromPeakFeature) {
            var representative = chromPeakFeature.MatchResults.Representative;
            if (_evaluator is null) {
                return;
            }
            else if (_evaluator.IsReferenceMatched(representative)) {
                DataAccess.SetMoleculeMsProperty(chromPeakFeature, _refer.Refer(representative), representative);
            }
            else if (_evaluator.IsAnnotationSuggested(representative)) {
                DataAccess.SetMoleculeMsPropertyAsSuggested(chromPeakFeature, _refer.Refer(representative), representative);
            }
        }
    }
}
