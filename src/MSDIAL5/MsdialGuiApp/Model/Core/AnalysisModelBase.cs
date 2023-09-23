﻿using CompMs.App.Msdial.Model.Chart;
using CompMs.App.Msdial.Model.DataObj;
using CompMs.App.Msdial.Utility;
using CompMs.App.Msdial.ViewModel.Service;
using CompMs.Common.Algorithm.Function;
using CompMs.Common.Extension;
using CompMs.CommonMVVM;
using CompMs.MsdialCore.DataObj;
using CompMs.MsdialCore.MSDec;
using CompMs.MsdialCore.Parameter;
using CompMs.MsdialCore.Parser;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using Reactive.Bindings.Notifiers;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace CompMs.App.Msdial.Model.Core {
    public abstract class AnalysisModelBase : BindableBase, IAnalysisModel, IDisposable
    {
        private readonly ChromatogramPeakFeatureCollection _peakCollection;
        private readonly IMessageBroker _broker;

        public AnalysisModelBase(AnalysisFileBeanModel analysisFileModel, IMessageBroker broker) {
            AnalysisFileModel = analysisFileModel;
            _broker = broker;
            var peaks = MsdialPeakSerializer.LoadChromatogramPeakFeatures(analysisFileModel.PeakAreaBeanInformationFilePath);
            _peakCollection = new ChromatogramPeakFeatureCollection(peaks);
            Ms1Peaks = new ObservableCollection<ChromatogramPeakFeatureModel>(
                peaks.Select(peak => new ChromatogramPeakFeatureModel(peak).AddTo(Disposables))
            );
            if (Ms1Peaks.IsEmptyOrNull()) {
                MessageBox.Show("No peak information. Check your polarity setting.");
            }

            Target = new ReactivePropertySlim<ChromatogramPeakFeatureModel>().AddTo(Disposables);

            decLoader = new MSDecLoader(analysisFileModel.DeconvolutionFilePath).AddTo(Disposables);
            MsdecResult = Target.SkipNull()
                .Select(t => decLoader.LoadMSDecResult(t.MSDecResultIDUsedForAnnotation))
                .ToReadOnlyReactivePropertySlim()
                .AddTo(Disposables);

            CanSearchCompound = new[]
            {
                Target.Select(t => t is null || t.InnerModel is null),
                MsdecResult.Select(r => r is null),
            }.CombineLatestValuesAreAllFalse()
            .ToReadOnlyReactivePropertySlim()
            .AddTo(Disposables);
        }

        protected readonly MSDecLoader decLoader;

        public AnalysisFileBeanModel AnalysisFileModel { get; }

        public ObservableCollection<ChromatogramPeakFeatureModel> Ms1Peaks { get; }

        public ReactivePropertySlim<ChromatogramPeakFeatureModel> Target { get; }

        public ReadOnlyReactivePropertySlim<MSDecResult> MsdecResult { get; }

        public ReadOnlyReactivePropertySlim<bool> CanSearchCompound { get; }

        public abstract void SearchFragment();
        public abstract void InvokeMsfinder();
        public void RunMoleculerNetworking(MolecularSpectrumNetworkingBaseParameter parameter) {
            var publisher = new TaskProgressPublisher(_broker, $"Exporting MN results in {parameter.ExportFolderPath}");
            using (publisher.Start()) {
                var spots = Ms1Peaks;
                var peaks = MsdecResultsReader.ReadMSDecResults(AnalysisFileModel.DeconvolutionFilePath, out _, out _);

                void notify(double progressRate) {
                    publisher.Progress(progressRate, $"Exporting MN results in {parameter.ExportFolderPath}");
                }

                var rootObj = MoleculerNetworkingBase.GetMoleculerNetworkingRootObj(spots, peaks, parameter.MsmsSimilarityCalc, parameter.MnMassTolerance,
                   parameter.MnAbsoluteAbundanceCutOff, parameter.MnRelativeAbundanceCutOff, parameter.MnSpectrumSimilarityCutOff,
                   parameter.MinimumPeakMatch, parameter.MaxEdgeNumberPerNode, parameter.MaxPrecursorDifference, parameter.MaxPrecursorDifferenceAsPercent, notify);

                for (int i = 0; i < rootObj.nodes.Count; i++) {
                    var node = rootObj.nodes[i];
                    node.data.BarGraph = CytoscapejsModel.GetBarGraphProperty(spots[i], AnalysisFileModel.AnalysisFileName);
                }

                MoleculerNetworkingBase.ExportNodesEdgesFiles(parameter.ExportFolderPath, rootObj.nodes, rootObj.edges);
            }
        }

        public Task SaveAsync(CancellationToken token) {
            return _peakCollection.SerializeAsync(AnalysisFileModel.File, token);
        }

        // IDisposable fields and methods
        protected CompositeDisposable Disposables = new CompositeDisposable();
        private bool disposedValue;

        protected virtual void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    Disposables.Dispose();
                }
                disposedValue = true;
            }
        }

        public void Dispose() {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        
    }
}
