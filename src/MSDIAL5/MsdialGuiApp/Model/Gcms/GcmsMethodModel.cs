﻿using CompMs.App.Msdial.Model.Chart;
using CompMs.App.Msdial.Model.Core;
using CompMs.App.Msdial.Model.DataObj;
using CompMs.App.Msdial.Model.Export;
using CompMs.App.Msdial.Model.Search;
using CompMs.Common.Components;
using CompMs.Common.Enum;
using CompMs.Graphics.UI.ProgressBar;
using CompMs.MsdialCore.Algorithm;
using CompMs.MsdialCore.Algorithm.Annotation;
using CompMs.MsdialCore.DataObj;
using CompMs.MsdialCore.Export;
using CompMs.MsdialCore.Parser;
using CompMs.MsdialGcMsApi.Algorithm;
using CompMs.MsdialGcMsApi.Algorithm.Alignment;
using CompMs.MsdialGcMsApi.Export;
using CompMs.MsdialGcMsApi.Parameter;
using CompMs.MsdialGcMsApi.Process;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using Reactive.Bindings.Notifiers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CompMs.App.Msdial.Model.Gcms
{
    internal sealed class GcmsMethodModel : MethodModelBase
    {
        private readonly IMsdialDataStorage<MsdialGcmsParameter> _storage;
        private readonly FilePropertiesModel _projectBaseParameter;
        private readonly StudyContextModel _studyContext;
        private readonly FacadeMatchResultEvaluator _evaluator;
        private readonly IMessageBroker _broker;
        private readonly StandardDataProviderFactory _providerFactory;
        private readonly PeakFilterModel _peakFilterModel;
        private readonly PeakSpotFiltering<AlignmentSpotPropertyModel> _peakSpotFiltering;
        private readonly List<CalculateMatchScore> _calculateMatchScores;
        private readonly ChromatogramSerializer<ChromatogramSpotInfo> _chromatogramSpotSerializer;

        public GcmsMethodModel(AnalysisFileBeanModelCollection analysisFileBeanModelCollection, AlignmentFileBeanModelCollection alignmentFiles, IMsdialDataStorage<MsdialGcmsParameter> storage, FilePropertiesModel projectBaseParameter, StudyContextModel studyContext, IMessageBroker broker) : base(analysisFileBeanModelCollection, alignmentFiles, projectBaseParameter) {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _projectBaseParameter = projectBaseParameter;
            _studyContext = studyContext;
            _evaluator = FacadeMatchResultEvaluator.FromDataBases(storage.DataBases);
            _broker = broker;
            _providerFactory = new StandardDataProviderFactory(retry: 5, isGuiProcess: true);
            _peakFilterModel = new PeakFilterModel(DisplayFilter.RefMatched | DisplayFilter.Unknown /*&& DisplayFilter.Blank*/); // TODO: Implement blank filtering
            _peakSpotFiltering = new PeakSpotFiltering<AlignmentSpotPropertyModel>(FilterEnableStatus.All & ~FilterEnableStatus.Dt & ~FilterEnableStatus.Protein).AddTo(Disposables);
            switch (storage.Parameter.RetentionType) {
                case RetentionType.RI:
                    _calculateMatchScores = storage.DataBases.MetabolomicsDataBases.Select(db => new CalculateMatchScore(db, storage.Parameter.MspSearchParam, RetentionType.RI)).ToList();
                    break;
                case RetentionType.RT:
                default:
                    _calculateMatchScores = storage.DataBases.MetabolomicsDataBases.Select(db => new CalculateMatchScore(db, storage.Parameter.MspSearchParam, RetentionType.RT)).ToList();
                    break;
            }
            switch (storage.Parameter.AlignmentIndexType) {
                case AlignmentIndexType.RI:
                    _chromatogramSpotSerializer = ChromatogramSerializerFactory.CreateSpotSerializer("CSS1", ChromXType.RT);
                    break;
                case AlignmentIndexType.RT:
                default:
                    _chromatogramSpotSerializer = ChromatogramSerializerFactory.CreateSpotSerializer("CSS1", ChromXType.RI);
                    break;
            }

            var filterEnabled = FilterEnableStatus.All & ~FilterEnableStatus.Dt & ~FilterEnableStatus.Protein & ~FilterEnableStatus.Adduct;
            var filter = _peakSpotFiltering.CreateFilter(_peakFilterModel, _evaluator.Contramap((AlignmentSpotPropertyModel spot) => spot.ScanMatchResult), filterEnabled);
            var currentAlignmentResult = this.ObserveProperty(m => m.SelectedAlignmentModel).ToReadOnlyReactivePropertySlim().AddTo(Disposables);
            AlignmentFilesForExport alignmentFilesForExport = new AlignmentFilesForExport(alignmentFiles.Files, this.ObserveProperty(m => m.AlignmentFile)).AddTo(Disposables);
            var isNormalized = alignmentFilesForExport.CanExportNormalizedData(currentAlignmentResult.Select(r => r?.NormalizationSetModel.IsNormalized ?? Observable.Return(false)).Switch()).ToReadOnlyReactivePropertySlim().AddTo(Disposables);
            AlignmentPeakSpotSupplyer peakSpotSupplyer = new AlignmentPeakSpotSupplyer(currentAlignmentResult, filter);
            var stats = new List<StatsValue> { StatsValue.Average, StatsValue.Stdev, };
            var metadataAccessorFactory = new GcmsAlignmentMetadataAccessorFactory(storage.DataBaseMapper, storage.Parameter);
            var peakGroup = new AlignmentExportGroupModel(
                "Peaks",
                new ExportMethod(
                    analysisFileBeanModelCollection.AnalysisFiles.Select(f => f.File).ToArray(),
                    metadataAccessorFactory,
                    ExportFormat.Tsv,
                    ExportFormat.Csv
                ),
                new[]
                {
                    new ExportType("Raw data (Height)", new LegacyQuantValueAccessor("Height", storage.Parameter), "Height", stats, true),
                    new ExportType("Raw data (Area)", new LegacyQuantValueAccessor("Area", storage.Parameter), "Area", stats),
                    //new ExportType("Normalized data (Height)", new LegacyQuantValueAccessor("Normalized height", storage.Parameter), "NormalizedHeight", stats, isNormalized),
                    //new ExportType("Normalized data (Area)", new LegacyQuantValueAccessor("Normalized area", storage.Parameter), "NormalizedArea", stats, isNormalized),
                    new ExportType("Peak ID", new LegacyQuantValueAccessor("ID", storage.Parameter), "PeakID"),
                    new ExportType("Quant mass", new LegacyQuantValueAccessor("MZ", storage.Parameter), "Quant mass"),
                    new ExportType("Retention time", new LegacyQuantValueAccessor("RT", storage.Parameter), "Rt"),
                    new ExportType("Retention index", new LegacyQuantValueAccessor("RI", storage.Parameter), "Ri"),
                    new ExportType("S/N", new LegacyQuantValueAccessor("SN", storage.Parameter), "SN"),
                    //new ExportType("Identification method", new AnnotationMethodAccessor(), "IdentificationMethod"),
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
            var exportGroups = new List<IAlignmentResultExportModel> { peakGroup, spectraGroup, };


            AlignmentResultExportModel = new AlignmentResultExportModel(exportGroups, alignmentFilesForExport, peakSpotSupplyer, storage.Parameter.DataExportParam, broker);
        }

        public GcmsAnalysisModel? SelectedAnalysisModel {
            get => _selectedAnalysisModel;
            private set {
                var old = _selectedAnalysisModel;
                if (SetProperty(ref _selectedAnalysisModel, value)) {
                    if (value != null) {
                        Disposables.Add(value);
                    }
                    if (old != null && Disposables.Contains(old)) {
                        Disposables.Remove(old);
                    }
                }
            }
        }
        private GcmsAnalysisModel? _selectedAnalysisModel;

        public GcmsAlignmentModel? SelectedAlignmentModel {
            get => _selectedAlignmentModel;
            private set {
                var old = _selectedAlignmentModel;
                if (SetProperty(ref _selectedAlignmentModel, value)) {
                    if (value != null) {
                        Disposables.Add(value);
                    }
                    if (old != null && Disposables.Contains(old)) {
                        Disposables.Remove(old);
                    }
                }
            }
        }
        private GcmsAlignmentModel? _selectedAlignmentModel;

        public override Task RunAsync(ProcessOption option, CancellationToken token) {
            if (option.HasFlag(ProcessOption.PeakSpotting | ProcessOption.Identification)) {
                if (!RunFromPeakSpotting()) {
                    return Task.CompletedTask;
                }
            }
            else if (option.HasFlag(ProcessOption.Identification)) {
                if (!RunFromIdentification()) {
                    return Task.CompletedTask;
                }
            }

            if (option.HasFlag(ProcessOption.Alignment)) {
                if (!RunAlignment()) {
                    return Task.CompletedTask;
                }
            }
            return Task.CompletedTask;
        }

        private bool RunFromPeakSpotting() {
            var request = new ProgressBarMultiContainerRequest(
                vm_ =>
                {
                    var processor = new FileProcess(_providerFactory, _storage, _calculateMatchScores.FirstOrDefault());
                    var runner = new ProcessRunner(processor);
                    return runner.RunAllAsync(
                        _storage.AnalysisFiles,
                        vm_.ProgressBarVMs.Select(pbvm => (Action<int>)((int v) => pbvm.CurrentValue = v)),
                        Math.Max(1, _storage.Parameter.ProcessBaseParam.UsableNumThreads / 2),
                        vm_.Increment,
                        default);
                },
                _storage.AnalysisFiles.Select(file => file.AnalysisFileName).ToArray());
            _broker.Publish(request);
            return request.Result ?? false;
        }

        private bool RunFromIdentification() {
            var request = new ProgressBarMultiContainerRequest(
                vm_ =>
                {
                    var processor = new FileProcess(_providerFactory, _storage, _calculateMatchScores.FirstOrDefault());
                    var runner = new ProcessRunner(processor);
                    return runner.AnnotateAllAsync(
                        _storage.AnalysisFiles,
                        vm_.ProgressBarVMs.Select(pbvm => (Action<int>)((int v) => pbvm.CurrentValue = v)),
                        Math.Max(1, _storage.Parameter.ProcessBaseParam.UsableNumThreads / 2),
                        vm_.Increment,
                        default);
                },
                _storage.AnalysisFiles.Select(file => file.AnalysisFileName).ToArray());
            _broker.Publish(request);
            return request.Result ?? false;
        }

        private bool RunAlignment() {
            var request = new ProgressBarRequest("Process alignment..", isIndeterminate: false,
                async vm => {
                    var factory = new GcmsAlignmentProcessFactory(_storage.AnalysisFiles, _storage, _evaluator)
                    {
                        ReportAction = v => vm.CurrentValue = v
                    };
                    var aligner = factory.CreatePeakAligner();
                    aligner.ProviderFactory = _providerFactory; // TODO: I'll remove this later.

                    var alignmentFileModel = AlignmentFiles.Files.Last();
                    var result = await Task.Run(() => alignmentFileModel.RunAlignment(aligner, _chromatogramSpotSerializer)).ConfigureAwait(false);

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

        protected override IAlignmentModel LoadAlignmentFileCore(AlignmentFileBeanModel alignmentFileModel) {
            return SelectedAlignmentModel = new GcmsAlignmentModel(alignmentFileModel, _evaluator, _storage.DataBases, _peakSpotFiltering, _peakFilterModel, _storage.DataBaseMapper, _storage.Parameter, _projectBaseParameter, _storage.AnalysisFiles, AnalysisFileModelCollection, _calculateMatchScores.FirstOrDefault(), _broker);
        }

        protected override IAnalysisModel LoadAnalysisFileCore(AnalysisFileBeanModel analysisFile) {
            var providerFactory = _providerFactory.ContraMap((AnalysisFileBeanModel fileModel) => fileModel.File);
            return SelectedAnalysisModel = new GcmsAnalysisModel(analysisFile, providerFactory, _storage.Parameter, _storage.DataBaseMapper, _storage.DataBases, _projectBaseParameter, _peakFilterModel, _calculateMatchScores.FirstOrDefault(), _broker);
        }

        public CheckChromatogramsModel? ShowChromatograms(bool tic, bool bpc, bool highestEic) {
            var analysisModel = SelectedAnalysisModel;
            if (analysisModel is null) {
                return null;
            }

            var loadChromatogramsUsecase = analysisModel.LoadChromatogramsUsecase();
            loadChromatogramsUsecase.InsertTic = tic;
            loadChromatogramsUsecase.InsertBpc = bpc;
            loadChromatogramsUsecase.InsertHighestEic = highestEic;
            var model = new CheckChromatogramsModel(loadChromatogramsUsecase, _storage.Parameter.AdvancedProcessOptionBaseParam);
            model.Update();
            return model;
        }

        public AnalysisResultExportModel ExportAnalysis() {
            var spectraTypes = new ISpectraType[]
            {
                new SpectraType<SpectrumFeature>(
                    ExportspectraType.deconvoluted,
                    new GcmsAnalysisMetadataAccessor(_storage.DataBaseMapper, new DelegateMsScanPropertyLoader<SpectrumFeature>(spectrum => spectrum.AnnotatedMSDecResult.MSDecResult)),
                    f => f.LoadSpectrumFeatures().Items),
                //new SpectraType(
                //    ExportspectraType.centroid,
                //    new LcmsAnalysisMetadataAccessor(_storage.DataBaseMapper, _storage.Parameter, ExportspectraType.centroid)),
                //new SpectraType(
                //    ExportspectraType.profile,
                //    new LcmsAnalysisMetadataAccessor(_storage.DataBaseMapper, _storage.Parameter, ExportspectraType.profile)),
            };
            var spectraFormats = new[]
            {
                new SpectraFormat(ExportSpectraFileFormat.txt, new AnalysisCSVExporterFactory(separator: "\t")),
            };

            var models = new IMsdialAnalysisExport[]
            {
                new MsdialAnalysisTableExportModel(spectraTypes, spectraFormats),
                new SpectraTypeSelectableMsdialAnalysisExportModel(new Dictionary<ExportspectraType, IAnalysisExporter<ChromatogramPeakFeatureCollection>> {
                    [ExportspectraType.deconvoluted] = new AnalysisMspExporter(_storage.DataBaseMapper, _storage.Parameter),
                    [ExportspectraType.centroid] = new AnalysisMspExporter(_storage.DataBaseMapper, _storage.Parameter, file => new CentroidMsScanPropertyLoader(_providerFactory.Create(file), _storage.Parameter.MS2DataType)),
                })
                {
                    FilePrefix = "Msp",
                    FileSuffix = "msp",
                    Label = "Nist format (*.msp)"
                },
                // new MsdialAnalysisMassBankRecordExportModel(_storage.Parameter.ProjectParam, _studyContext),
            };
            return new AnalysisResultExportModel(AnalysisFileModelCollection, _storage.Parameter.ProjectParam.ProjectFolderPath, models);
        }

        public AlignmentResultExportModel AlignmentResultExportModel { get; }
    }
}
