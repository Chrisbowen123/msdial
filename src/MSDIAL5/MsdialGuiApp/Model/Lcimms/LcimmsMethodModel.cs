﻿using CompMs.App.Msdial.Model.Chart;
using CompMs.App.Msdial.Model.Core;
using CompMs.App.Msdial.Model.DataObj;
using CompMs.App.Msdial.Model.Export;
using CompMs.App.Msdial.Model.Search;
using CompMs.Common.Components;
using CompMs.Common.DataObj;
using CompMs.Common.Enum;
using CompMs.Graphics.UI.ProgressBar;
using CompMs.MsdialCore.Algorithm;
using CompMs.MsdialCore.Algorithm.Alignment;
using CompMs.MsdialCore.Algorithm.Annotation;
using CompMs.MsdialCore.DataObj;
using CompMs.MsdialCore.Export;
using CompMs.MsdialCore.Parser;
using CompMs.MsdialLcImMsApi.Algorithm;
using CompMs.MsdialLcImMsApi.Algorithm.Alignment;
using CompMs.MsdialLcImMsApi.Algorithm.Annotation;
using CompMs.MsdialLcImMsApi.Parameter;
using CompMs.MsdialLcImMsApi.Process;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using Reactive.Bindings.Notifiers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CompMs.App.Msdial.Model.Lcimms
{
    internal sealed class LcimmsMethodModel : MethodModelBase
    {
        static LcimmsMethodModel() {
            chromatogramSpotSerializer = ChromatogramSerializerFactory.CreateSpotSerializer("CSS1", ChromXType.Drift);
        }

        public LcimmsMethodModel(AnalysisFileBeanModelCollection analysisFileBeanModelCollection, AlignmentFileBeanModelCollection alignmentFileBeanModelCollection, IMsdialDataStorage<MsdialLcImMsParameter> storage, FilePropertiesModel projectBaseParameter, IMessageBroker broker)
            : base(analysisFileBeanModelCollection, alignmentFileBeanModelCollection, projectBaseParameter) {
            if (storage is null) {
                throw new ArgumentNullException(nameof(storage));
            }

            Storage = storage;
            _projectBaseParameter = projectBaseParameter ?? throw new ArgumentNullException(nameof(projectBaseParameter));
            _broker = broker;
            providerFactory = new StandardDataProviderFactory();
            accProviderFactory = new LcimmsAccumulateDataProviderFactory();
            matchResultEvaluator = FacadeMatchResultEvaluator.FromDataBases(storage.DataBases);
            PeakFilterModel = new PeakFilterModel(DisplayFilter.All);
            AccumulatedPeakFilterModel = new PeakFilterModel(DisplayFilter.All & ~DisplayFilter.CcsMatched);

            List<AnalysisFileBean> analysisFiles = analysisFileBeanModelCollection.AnalysisFiles.Select(f => f.File).ToList();
            var filterEnabled = FilterEnableStatus.All & ~FilterEnableStatus.Protein;
            if (storage.Parameter.TargetOmics == TargetOmics.Proteomics) {
                filterEnabled |= FilterEnableStatus.Protein;
            }
            _peakSpotFiltering = new PeakSpotFiltering<AlignmentSpotPropertyModel>(filterEnabled).AddTo(Disposables);
            var filter = _peakSpotFiltering.CreateFilter(PeakFilterModel, matchResultEvaluator.Contramap((AlignmentSpotPropertyModel spot) => spot.ScanMatchResult), filterEnabled);
            var stats = new List<StatsValue> { StatsValue.Average, StatsValue.Stdev, };
            var metadataAccessorFactory = new LcimmsAlignmentMetadataAccessorFactory(storage.DataBaseMapper, storage.Parameter);
            var currentAlignmentResult = this.ObserveProperty(m => m.AlignmentModel).ToReadOnlyReactivePropertySlim().AddTo(Disposables);
            AlignmentFilesForExport alignmentFilesForExport = new AlignmentFilesForExport(alignmentFileBeanModelCollection.Files, this.ObserveProperty(m => m.AlignmentFile)).AddTo(Disposables);
            var isNormalized = alignmentFilesForExport.CanExportNormalizedData(currentAlignmentResult.Select(r => r?.NormalizationSetModel.IsNormalized ?? Observable.Return(false)).Switch()).ToReadOnlyReactivePropertySlim().AddTo(Disposables);
            AlignmentPeakSpotSupplyer peakSpotSupplyer = new AlignmentPeakSpotSupplyer(currentAlignmentResult, filter);
            var peakGroup = new AlignmentExportGroupModel(
                "Peaks",
                new ExportMethod(
                    analysisFiles,
                    metadataAccessorFactory,
                    ExportFormat.Tsv,
                    ExportFormat.Csv
                ),
                new[]
                {
                    new ExportType("Raw data (Height)", new LegacyQuantValueAccessor("Height", storage.Parameter), "Height", stats, true),
                    new ExportType("Raw data (Area)", new LegacyQuantValueAccessor("Area", storage.Parameter), "Area", stats),
                    new ExportType("Normalized data (Height)", new LegacyQuantValueAccessor("Normalized height", storage.Parameter), "NormalizedHeight", stats, isNormalized),
                    new ExportType("Normalized data (Area)", new LegacyQuantValueAccessor("Normalized area", storage.Parameter), "NormalizedArea", stats, isNormalized),
                    new ExportType("Peak ID", new LegacyQuantValueAccessor("ID", storage.Parameter), "PeakID"),
                    new ExportType("m/z", new LegacyQuantValueAccessor("MZ", storage.Parameter), "Mz"),
                    new ExportType("Retention time", new LegacyQuantValueAccessor("RT", storage.Parameter), "Rt"),
                    new ExportType("Mobility", new LegacyQuantValueAccessor("Mobility", storage.Parameter), "Mobility"),
                    new ExportType("CCS", new LegacyQuantValueAccessor("CCS", storage.Parameter), "CCS"),
                    new ExportType("S/N", new LegacyQuantValueAccessor("SN", storage.Parameter), "SN"),
                    new ExportType("MS/MS included", new LegacyQuantValueAccessor("MSMS", storage.Parameter), "MsmsIncluded"),
                    new ExportType("Identification method", new AnnotationMethodAccessor(), "IdentificationMethod"),
                },
                new[]
                {
                    ExportspectraType.deconvoluted,
                },
                peakSpotSupplyer);
            var spectraGroup = new AlignmentSpectraExportGroupModel(
                new[]
                {
                    ExportspectraType.deconvoluted,
                },
                peakSpotSupplyer,
                new AlignmentSpectraExportFormat("Msp", "msp", new AlignmentMspExporter(storage.DataBaseMapper, storage.Parameter)),
                new AlignmentSpectraExportFormat("Mgf", "mgf", new AlignmentMgfExporter()),
                new AlignmentSpectraExportFormat("Mat", "mat", new AlignmentMatExporter(storage.DataBaseMapper, storage.Parameter)));
            var spectraAndReference = new AlignmentMatchedSpectraExportModel(peakSpotSupplyer, storage.DataBaseMapper, analysisFileBeanModelCollection.IncludedAnalysisFiles, CompoundSearcherCollection.BuildSearchers(storage.DataBases, storage.DataBaseMapper));

            AlignmentResultExportModel = new AlignmentResultExportModel(new IAlignmentResultExportModel[] { peakGroup, spectraGroup, spectraAndReference, }, alignmentFilesForExport, peakSpotSupplyer, storage.Parameter.DataExportParam, broker);

            ParameterExportModel = new ParameterExportModel(storage.DataBases, storage.Parameter, broker);
        }

        private FacadeMatchResultEvaluator matchResultEvaluator;

        public IMsdialDataStorage<MsdialLcImMsParameter> Storage { get; }

        public LcimmsAnalysisModel? AnalysisModel {
            get => analysisModel;
            private set => SetProperty(ref analysisModel, value);
        }
        private LcimmsAnalysisModel? analysisModel;

        public LcimmsAlignmentModel? AlignmentModel {
            get => alignmentModel;
            private set => SetProperty(ref alignmentModel, value);
        }
        private LcimmsAlignmentModel? alignmentModel;

        private static readonly ChromatogramSerializer<ChromatogramSpotInfo> chromatogramSpotSerializer;
        private readonly IDataProviderFactory<RawMeasurement> providerFactory;
        private readonly IDataProviderFactory<RawMeasurement> accProviderFactory;
        private readonly FilePropertiesModel _projectBaseParameter;
        private readonly IMessageBroker _broker;
        private readonly PeakSpotFiltering<AlignmentSpotPropertyModel> _peakSpotFiltering;

        public PeakFilterModel AccumulatedPeakFilterModel { get; }
        public PeakFilterModel PeakFilterModel { get; }
        public AlignmentResultExportModel AlignmentResultExportModel { get; }
        public ParameterExportModel ParameterExportModel { get; }

        protected override IAnalysisModel LoadAnalysisFileCore(AnalysisFileBeanModel analysisFile) {
            if (AnalysisModel != null) {
                AnalysisModel.Dispose();
                Disposables.Remove(AnalysisModel);
            }
            var rawObj = analysisFile.File.LoadRawMeasurement(isImagingMsData: false, isGuiProcess: true, retry: 5, sleepMilliSeconds: 5000);
            return AnalysisModel = new LcimmsAnalysisModel(
                analysisFile,
                providerFactory.Create(rawObj),
                accProviderFactory.Create(rawObj),
                Storage.DataBases,
                matchResultEvaluator,
                Storage.DataBaseMapper,
                Storage.Parameter,
                PeakFilterModel,
                AccumulatedPeakFilterModel,
                _projectBaseParameter,
                _broker)
            .AddTo(Disposables);
        }

        protected override IAlignmentModel LoadAlignmentFileCore(AlignmentFileBeanModel alignmentFileModel) {
            if (AlignmentModel != null) {
                AlignmentModel.Dispose();
                Disposables.Remove(AlignmentModel);
            }
            return AlignmentModel = new LcimmsAlignmentModel(
                alignmentFileModel,
                AnalysisFileModelCollection,
                matchResultEvaluator,
                Storage.DataBases,
                Storage.DataBaseMapper,
                _projectBaseParameter,
                Storage.Parameter,
                Storage.AnalysisFiles,
                _peakSpotFiltering,
                PeakFilterModel,
                AccumulatedPeakFilterModel,
                _broker)
            .AddTo(Disposables);
        }

        public override async Task RunAsync(ProcessOption processOption, CancellationToken token) {
            // Set analysis param
            var annotationProcess = BuildAnnotationProcess();

            // Run Identification
            if (processOption.HasFlag(ProcessOption.Identification)) {
                int usable = Math.Max(Storage.Parameter.ProcessBaseParam.UsableNumThreads / 2, 1);
                FileProcess processor = new FileProcess(providerFactory, accProviderFactory, annotationProcess, matchResultEvaluator, Storage, isGuiProcess: true);
                var runner = new ProcessRunner(processor);
                if (processOption.HasFlag(ProcessOption.PeakSpotting)) {
                    if (!RunFileProcess(Storage.AnalysisFiles, usable, runner)) {
                        return;
                    }
                }
                else {
                    if (!RunAnnotation(Storage.AnalysisFiles, usable, runner)) {
                        return;
                    }
                }
            }

            // Run Alignment
            if (processOption.HasFlag(ProcessOption.Alignment)) {
                if (!RunAlignmentProcess()) {
                    return;
                }
            }

            await LoadAnalysisFileAsync(AnalysisFileModelCollection.AnalysisFiles.FirstOrDefault(), token).ConfigureAwait(false);
        }

        private IAnnotationProcess BuildAnnotationProcess() {
            return new LcimmsStandardAnnotationProcess(Storage.CreateAnnotationQueryFactoryStorage().MoleculeQueryFactories, matchResultEvaluator, Storage.DataBaseMapper);
        }

        private bool RunFileProcess(List<AnalysisFileBean> analysisFiles, int usable, ProcessRunner runner) {
            var request = new ProgressBarMultiContainerRequest(
                vm => runner.RunAllAsync(analysisFiles, vm.ProgressBarVMs.Select(vm_ => (Action<int>)((int v) => vm_.CurrentValue = v)), usable, vm.Increment, default),
                analysisFiles.Select(file => file.AnalysisFileName).ToArray());
            _broker.Publish(request);
            return request.Result ?? false;
        }

        private bool RunAnnotation(List<AnalysisFileBean> analysisFiles, int usable, ProcessRunner runner) {
            var request = new ProgressBarMultiContainerRequest(
                vm => runner.AnnotateAllAsync(analysisFiles, vm.ProgressBarVMs.Select(vm_ => (Action<int>)((int v) => vm_.CurrentValue = v)), usable, vm.Increment, default),
                analysisFiles.Select(file => file.AnalysisFileName).ToArray());
            _broker.Publish(request);
            return request.Result ?? false;
        }

        public bool RunAlignmentProcess() {
            var request = new ProgressBarRequest("Process alignment..", isIndeterminate: true,
                async _ =>
                {
                    RawMeasurement map(AnalysisFileBean file) {
                        return file.LoadRawMeasurement(false, true, 5, 1000);
                    }
                    AlignmentProcessFactory aFactory = new LcimmsAlignmentProcessFactory(Storage, matchResultEvaluator, providerFactory.ContraMap((Func<AnalysisFileBean, RawMeasurement>)map), accProviderFactory.ContraMap((Func<AnalysisFileBean, RawMeasurement>)map));
                    var alignmentFile = Storage.AlignmentFiles.Last();
                    var alignmentFileModel = AlignmentFiles.Files.Last();
                    var aligner = aFactory.CreatePeakAligner();
                    var result = await Task.Run(() => alignmentFileModel.RunAlignment(aligner, chromatogramSpotSerializer)).ConfigureAwait(false);
                    var tasks = new[]
                    {
                        alignmentFileModel.SaveAlignmentResultAsync(result),
                        alignmentFileModel.SaveMSDecResultsAsync(alignmentFileModel.LoadMSDecResultsFromEachFiles(result.AlignmentSpotProperties)),
                    };
                    await Task.WhenAll(tasks).ConfigureAwait(false);
                });
            _broker.Publish(request);
            return request.Result ?? false;
        }

        public void SaveProject() {
            AlignmentModel?.SaveProject();
        }

        public CheckChromatogramsModel? PrepareChromatograms(bool tic, bool bpc, bool highestEic) {
            var analysisModel = AnalysisModel;
            if (analysisModel is null) {
                return null;
            }

            var loadChromatogramsUsecase = analysisModel.LoadChromatogramsUsecase();
            loadChromatogramsUsecase.InsertTic = tic;
            loadChromatogramsUsecase.InsertBpc = bpc;
            loadChromatogramsUsecase.InsertHighestEic = highestEic;
            var model = new CheckChromatogramsModel(loadChromatogramsUsecase, analysisModel.AccumulateSpectraUsecase, analysisModel.CompoundSearcher, Storage.Parameter.AdvancedProcessOptionBaseParam, analysisModel.AnalysisFileModel, _broker);
            model.Update();
            return model;
        }
    }
}
