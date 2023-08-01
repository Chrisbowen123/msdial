﻿using CompMs.App.Msdial.ExternalApp;
using CompMs.App.Msdial.Model.Chart;
using CompMs.App.Msdial.Model.Core;
using CompMs.App.Msdial.Model.DataObj;
using CompMs.App.Msdial.Model.Information;
using CompMs.App.Msdial.Model.Loader;
using CompMs.App.Msdial.Model.Search;
using CompMs.App.Msdial.Model.Service;
using CompMs.App.Msdial.Model.Statistics;
using CompMs.App.Msdial.Utility;
using CompMs.Common.Components;
using CompMs.Common.DataObj.Result;
using CompMs.Common.Enum;
using CompMs.Common.Extension;
using CompMs.Common.Proteomics.DataObj;
using CompMs.Graphics.AxisManager.Generic;
using CompMs.Graphics.Core.Base;
using CompMs.Graphics.Design;
using CompMs.MsdialCore.Algorithm.Annotation;
using CompMs.MsdialCore.DataObj;
using CompMs.MsdialCore.Export;
using CompMs.MsdialCore.MSDec;
using CompMs.MsdialCore.Parameter;
using CompMs.MsdialCore.Parser;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using Reactive.Bindings.Notifiers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Windows.Media;

namespace CompMs.App.Msdial.Model.Imms
{
    internal sealed class ImmsAlignmentModel : AlignmentModelBase
    {
        private static readonly ChromatogramSerializer<ChromatogramSpotInfo> CHROMATOGRAM_SPOT_SERIALIZER;

        private readonly AlignmentFileBeanModel _alignmentFile;
        private readonly List<AnalysisFileBean> _files;
        private readonly ParameterBase _parameter;
        private readonly DataBaseMapper _dataBaseMapper;
        private readonly CompoundSearcherCollection _compoundSearchers;
        private readonly UndoManager _undoManager;

        public ImmsAlignmentModel(
            AlignmentFileBeanModel alignmentFileModel,
            AnalysisFileBeanModelCollection fileCollection,
            IMatchResultEvaluator<MsScanMatchResult> evaluator,
            DataBaseStorage databases,
            DataBaseMapper mapper,
            PeakSpotFiltering<AlignmentSpotPropertyModel> peakSpotFiltering,
            PeakFilterModel peakFilterModel,
            ProjectBaseParameterModel projectBaseParameter,
            ParameterBase parameter,
            List<AnalysisFileBean> files,
            IMessageBroker broker)
            : base(alignmentFileModel) {

            _alignmentFile = alignmentFileModel;
            _parameter = parameter;
            _files = files ?? throw new ArgumentNullException(nameof(files));
            _dataBaseMapper = mapper;
            MatchResultEvaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
            _compoundSearchers = CompoundSearcherCollection.BuildSearchers(databases, mapper);
            _undoManager = new UndoManager().AddTo(Disposables);

            var spotsSource = new AlignmentSpotSource(alignmentFileModel, Container, CHROMATOGRAM_SPOT_SERIALIZER).AddTo(Disposables);
            Ms1Spots = spotsSource.Spots.Items;
            InternalStandardSetModel = new InternalStandardSetModel(Ms1Spots, TargetMsMethod.Imms).AddTo(Disposables);
            NormalizationSetModel = new NormalizationSetModel(Container, _files, fileCollection, _dataBaseMapper, MatchResultEvaluator, InternalStandardSetModel, _parameter, broker).AddTo(Disposables);

            var brushMapDataSelector = BrushMapDataSelectorFactory.CreateAlignmentSpotBrushes(parameter.TargetOmics);
            Brushes = brushMapDataSelector.Brushes.ToList();
            SelectedBrush = brushMapDataSelector.SelectedBrush;
            Target = new ReactivePropertySlim<AlignmentSpotPropertyModel>().AddTo(Disposables);

            var filterRegistrationManager = new FilterRegistrationManager<AlignmentSpotPropertyModel>(Ms1Spots, peakSpotFiltering).AddTo(Disposables);
            PeakSpotNavigatorModel = filterRegistrationManager.PeakSpotNavigatorModel;
            filterRegistrationManager.AttachFilter(Ms1Spots, peakFilterModel, evaluator.Contramap<AlignmentSpotPropertyModel, MsScanMatchResult>(filterable => filterable.ScanMatchResult, (e, f) => f.IsRefMatched(e), (e, f) => f.IsSuggested(e)), status: ~FilterEnableStatus.Rt);

            var labelSource = PeakSpotNavigatorModel.ObserveProperty(m => m.SelectedAnnotationLabel);
            PlotModel = new AlignmentPeakPlotModel(Ms1Spots, spot => spot.TimesCenter, spot => spot.MassCenter, Target, labelSource, SelectedBrush, Brushes)
            {
                GraphTitle = ((IFileBean)alignmentFileModel).FileName,
                HorizontalProperty = nameof(AlignmentSpotPropertyModel.TimesCenter),
                VerticalProperty = nameof(AlignmentSpotPropertyModel.MassCenter),
                HorizontalTitle = "Mobility [1/k0]",
                VerticalTitle = "m/z",
            }.AddTo(Disposables);

            MatchResultCandidatesModel = new MatchResultCandidatesModel(Target.Select(t => t?.MatchResultsModel)).AddTo(Disposables);
            var refLoader = (parameter.ProjectParam.TargetOmics == TargetOmics.Proteomics)
                ? (IMsSpectrumLoader<MsScanMatchResult>)new ReferenceSpectrumLoader<PeptideMsReference>(mapper)
                : (IMsSpectrumLoader<MsScanMatchResult>)new ReferenceSpectrumLoader<MoleculeMsReference>(mapper);
            IMsSpectrumLoader<AlignmentSpotPropertyModel> decLoader = new AlignmentMSDecSpectrumLoader(_alignmentFile);
            var spectraExporter = new NistSpectraExporter<AlignmentSpotProperty>(Target.Select(t => t?.innerModel), mapper, parameter).AddTo(Disposables);
            GraphLabels ms2GraphLabels = new GraphLabels("Representation vs. Reference", "m/z", "Relative abundance", nameof(SpectrumPeak.Mass), nameof(SpectrumPeak.Intensity));
            ChartHueItem deconvolutedSpectrumHueItem = new ChartHueItem(projectBaseParameter, Colors.Blue);
            var referenceExporter = new MoleculeMsReferenceExporter(MatchResultCandidatesModel.SelectedCandidate.Select(c => mapper.MoleculeMsRefer(c)));
            ObservableMsSpectrum lowerObservableMsSpectrum = ObservableMsSpectrum.Create(MatchResultCandidatesModel.SelectedCandidate, refLoader, referenceExporter).AddTo(Disposables);
            ObservableMsSpectrum upperObservableMsSpectrum = ObservableMsSpectrum.Create(Target, decLoader, spectraExporter).AddTo(Disposables);
            PropertySelector<SpectrumPeak, double> horizontalPropertySelector = new PropertySelector<SpectrumPeak, double>(peak => peak.Mass);
            PropertySelector<SpectrumPeak, double> verticalPropertySelector = new PropertySelector<SpectrumPeak, double>(spot => spot.Intensity);
            SingleSpectrumModel upperSpectrumModel = new SingleSpectrumModel(
                upperObservableMsSpectrum,
                upperObservableMsSpectrum.CreateAxisPropertySelectors(horizontalPropertySelector, "m/z", "m/z"),
                upperObservableMsSpectrum.CreateAxisPropertySelectors2(verticalPropertySelector, "abundance"),
                deconvolutedSpectrumHueItem,
                ms2GraphLabels).AddTo(Disposables);
            ChartHueItem referenceSpectrumHueItem = new ChartHueItem(projectBaseParameter, Colors.Red);
            SingleSpectrumModel lowerSpectrumModel = new SingleSpectrumModel(
                lowerObservableMsSpectrum,
                lowerObservableMsSpectrum.CreateAxisPropertySelectors(horizontalPropertySelector, "m/z", "m/z"),
                lowerObservableMsSpectrum.CreateAxisPropertySelectors2(verticalPropertySelector, "abundance"),
                referenceSpectrumHueItem,
                ms2GraphLabels).AddTo(Disposables);
            Ms2SpectrumModel = new MsSpectrumModel(upperSpectrumModel, lowerSpectrumModel, MatchResultCandidatesModel.GetCandidatesScorer(_compoundSearchers))
            {
                GraphTitle = "Representation vs. Reference",
                HorizontalTitle = "m/z",
                VerticalTitle = "Relative abundance",
            }.AddTo(Disposables);

            var classBrush = new KeyBrushMapper<BarItem, string>(
                _parameter.ProjectParam.ClassnameToColorBytes
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => Color.FromRgb(kvp.Value[0], kvp.Value[1], kvp.Value[2])
                ),
                item => item.Class,
                Colors.Blue);

            var fileIdToClassNameAsObservable = projectBaseParameter.ObserveProperty(p => p.FileIdToClassName).ToReadOnlyReactivePropertySlim().AddTo(Disposables);
            var peakSpotAxisLabelAsObservable = Target.OfType<AlignmentSpotPropertyModel>().SelectSwitch(t => t.ObserveProperty(t_ => t_.IonAbundanceUnit).Select(t_ => t_.ToLabel())).Publish();
            var normalizedAreaZeroLoader = new BarItemsLoaderData("Normalized peak area above zero", peakSpotAxisLabelAsObservable, new NormalizedAreaAboveZeroBarItemsLoader(fileIdToClassNameAsObservable, fileCollection), NormalizationSetModel.IsNormalized);
            var normalizedAreaBaselineLoader = new BarItemsLoaderData("Normalized peak area above base line", peakSpotAxisLabelAsObservable, new NormalizedAreaAboveBaseLineBarItemsLoader(fileIdToClassNameAsObservable, fileCollection), NormalizationSetModel.IsNormalized);
            var normalizedHeightLoader = new BarItemsLoaderData("Normalized peak height", peakSpotAxisLabelAsObservable, new NormalizedHeightBarItemsLoader(fileIdToClassNameAsObservable, fileCollection), NormalizationSetModel.IsNormalized);
            var areaZeroLoader = new BarItemsLoaderData("Peak area above zero", "Area", new AreaAboveZeroBarItemsLoader(fileIdToClassNameAsObservable, fileCollection));
            var areaBaselineLoader = new BarItemsLoaderData("Peak area above base line", "Area", new AreaAboveBaseLineBarItemsLoader(fileIdToClassNameAsObservable, fileCollection));
            var heightLoader = new BarItemsLoaderData("Peak height", "Height", new HeightBarItemsLoader(fileIdToClassNameAsObservable, fileCollection));
            var barItemLoaderDatas = new[]
            {
                heightLoader, areaBaselineLoader, areaZeroLoader,
                normalizedHeightLoader, normalizedAreaBaselineLoader, normalizedAreaZeroLoader,
            };
            var barItemsLoaderDataProperty = NormalizationSetModel.Normalized.ToConstant(normalizedHeightLoader).ToReactiveProperty(NormalizationSetModel.IsNormalized.Value ? normalizedHeightLoader : heightLoader).AddTo(Disposables);
            Disposables.Add(peakSpotAxisLabelAsObservable.Connect());
            BarChartModel = new BarChartModel(Target, barItemsLoaderDataProperty, barItemLoaderDatas, Observable.Return(classBrush), projectBaseParameter, fileCollection, projectBaseParameter.ClassProperties).AddTo(Disposables);

            var classToColor = parameter.ClassnameToColorBytes
                .ToDictionary(kvp => kvp.Key, kvp => Color.FromRgb(kvp.Value[0], kvp.Value[1], kvp.Value[2]));
            var fileIdToFileName = files.ToDictionary(file => file.AnalysisFileId, file => file.AnalysisFileName);
            var eicLoader = alignmentFileModel.CreateEicLoader(CHROMATOGRAM_SPOT_SERIALIZER, fileCollection, projectBaseParameter).AddTo(Disposables);
            AlignmentEicModel = AlignmentEicModel.Create(
                Target, eicLoader, files, parameter,
                peak => peak.Time,
                peak => peak.Intensity).AddTo(Disposables);
            AlignmentEicModel.Elements.GraphTitle = "TIC, EIC, or BPC chromatograms";
            AlignmentEicModel.Elements.HorizontalTitle = "Mobility [1/k0]";
            AlignmentEicModel.Elements.VerticalTitle = "Abundance";
            AlignmentEicModel.Elements.HorizontalProperty = nameof(PeakItem.Time);
            AlignmentEicModel.Elements.VerticalProperty = nameof(PeakItem.Intensity);

            var barItemsLoaderProperty = barItemsLoaderDataProperty.SkipNull().SelectSwitch(data => data.ObservableLoader).ToReactiveProperty().AddTo(Disposables);
            AlignmentSpotTableModel = new ImmsAlignmentSpotTableModel(Ms1Spots, Target, Observable.Return(classBrush), projectBaseParameter.ClassProperties, barItemsLoaderProperty, PeakSpotNavigatorModel).AddTo(Disposables);

            MsdecResult = Target.SkipNull()
                .Select(t => _alignmentFile.LoadMSDecResultByIndexAsync(t.MasterAlignmentID))
                .Switch()
                .ToReadOnlyReactivePropertySlim()
                .AddTo(Disposables);

            CanSearchCompound = new[]
            {
                Target.Select(t => t?.innerModel is null),
                MsdecResult.Select(r => r is null),
            }.CombineLatestValuesAreAllFalse()
            .ToReadOnlyReactivePropertySlim()
            .AddTo(Disposables);

            var peakInformationModel = new PeakInformationAlignmentModel(Target).AddTo(Disposables);
            peakInformationModel.Add(
                t => new MzPoint(t?.MassCenter ?? 0d, t.Refer<MoleculeMsReference>(mapper)?.PrecursorMz),
                t => new DriftPoint(t?.innerModel.TimesCenter.Drift.Value ?? 0d),
                t => new CcsPoint(t?.CollisionCrossSection ?? 0d, t.Refer<MoleculeMsReference>(mapper)?.CollisionCrossSection));
            peakInformationModel.Add(t => new HeightAmount(t?.HeightAverage ?? 0d));
            PeakInformationModel = peakInformationModel;

            var compoundDetailModel = new CompoundDetailModel(Target.SkipNull().SelectSwitch(t => t.ObserveProperty(p => p.ScanMatchResult)).Publish().RefCount(), mapper).AddTo(Disposables);
            compoundDetailModel.Add(
                r_ => new MzSimilarity(r_?.AcurateMassSimilarity ?? 0d),
                r_ => new CcsSimilarity(r_?.CcsSimilarity ?? 0d),
                r_ => new SpectrumSimilarity(r_?.WeightedDotProduct ?? 0d, r_?.ReverseDotProduct ?? 0d));
            CompoundDetailModel = compoundDetailModel;

            var moleculeStructureModel = new MoleculeStructureModel().AddTo(Disposables);
            MoleculeStructureModel = moleculeStructureModel;
            Target.Subscribe(t => moleculeStructureModel.UpdateMolecule(t?.innerModel)).AddTo(Disposables);
        }

        static ImmsAlignmentModel() {
            CHROMATOGRAM_SPOT_SERIALIZER = ChromatogramSerializerFactory.CreateSpotSerializer("CSS1", ChromXType.Drift);
        }

        public UndoManager UndoManager => _undoManager;

        public ReadOnlyObservableCollection<AlignmentSpotPropertyModel> Ms1Spots { get; }
        public InternalStandardSetModel InternalStandardSetModel { get; }
        public NormalizationSetModel NormalizationSetModel { get; }
        public ReactivePropertySlim<AlignmentSpotPropertyModel> Target { get; }
        public PeakSpotNavigatorModel PeakSpotNavigatorModel { get; }
        public ReadOnlyReactivePropertySlim<MSDecResult> MsdecResult { get; }

        public AlignmentPeakPlotModel PlotModel { get; }

        public MsSpectrumModel Ms2SpectrumModel { get; }

        public BarChartModel BarChartModel { get; }

        public AlignmentEicModel AlignmentEicModel { get; }

        public ImmsAlignmentSpotTableModel AlignmentSpotTableModel { get; }

        public PeakInformationAlignmentModel PeakInformationModel { get; }
        public CompoundDetailModel CompoundDetailModel { get; }

        public ReadOnlyReactivePropertySlim<bool> CanSearchCompound { get; }

        public ImmsCompoundSearchModel CreateCompoundSearchModel() {
            if (Target.Value?.innerModel is null || MsdecResult.Value is null) {
                return null;
            }

            return new ImmsCompoundSearchModel(
                _files[Target.Value.RepresentativeFileID],
                Target.Value,
                MsdecResult.Value,
                _compoundSearchers.Items,
                _undoManager);
        }

        public List<BrushMapData<AlignmentSpotPropertyModel>> Brushes { get; }

        public BrushMapData<AlignmentSpotPropertyModel> SelectedBrush {
            get => _selectedBrush;
            set => SetProperty(ref _selectedBrush, value);
        }
        private BrushMapData<AlignmentSpotPropertyModel> _selectedBrush;

        public IMatchResultEvaluator<MsScanMatchResult> MatchResultEvaluator { get; }
        public MoleculeStructureModel MoleculeStructureModel { get; }
        public MatchResultCandidatesModel MatchResultCandidatesModel { get; }

        public void SaveSpectra(string filename) {
            using (var file = File.Open(filename, FileMode.Create)) {
                SpectraExport.SaveSpectraTable(
                    (ExportSpectraFileFormat)Enum.Parse(typeof(ExportSpectraFileFormat), Path.GetExtension(filename).Trim('.')),
                    file,
                    Target.Value.innerModel,
                    MsdecResult.Value,
                    _dataBaseMapper,
                    _parameter);
            }
        }

        public bool CanSaveSpectra() => Target.Value.innerModel != null && MsdecResult.Value != null;

        public IObservable<bool> CanSetUnknown => Target.Select(t => !(t is null));
        public void SetUnknown() => Target.Value?.SetUnknown(_undoManager);

        public override void SearchFragment() {
            using (var decLoader = _alignmentFile.CreateTemporaryMSDecLoader()) {
                MsdialCore.Algorithm.FragmentSearcher.Search(Ms1Spots.Select(n => n.innerModel).ToList(), decLoader, _parameter);
            }
        }

        public override void InvokeMsfinder() {
            if (Target.Value is null || (MsdecResult.Value?.Spectrum).IsEmptyOrNull()) {
                return;
            }
            MsDialToExternalApps.SendToMsFinderProgram(
                _alignmentFile,
                Target.Value.innerModel,
                MsdecResult.Value,
                _dataBaseMapper,
                _parameter);
        }

        public void SaveProject() {
            _alignmentFile.SaveAlignmentResultAsync(Container).Wait();
        }

        public void Undo() => _undoManager.Undo();
        public void Redo() => _undoManager.Redo();
    }
}
