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
using System.Windows;
using System.Windows.Media;

namespace CompMs.App.Msdial.Model.Dims
{
    internal class DimsAlignmentModel : AlignmentModelBase
    {
        static DimsAlignmentModel() {
            CHROMATOGRAM_SPOT_SERIALIZER = ChromatogramSerializerFactory.CreateSpotSerializer("CSS1", ChromXType.Mz);
        }

        private static readonly ChromatogramSerializer<ChromatogramSpotInfo> CHROMATOGRAM_SPOT_SERIALIZER;
        private static readonly double MZ_TOLERANCE = 20d;

        private readonly AlignmentFileBeanModel _alignmentFile;
        private readonly DataBaseMapper _dataBaseMapper;
        private readonly IMatchResultEvaluator<MsScanMatchResult> _matchResultEvaluator;
        private readonly ReadOnlyReactivePropertySlim<MSDecResult> _msdecResult;
        private readonly ParameterBase _parameter;
        private readonly List<AnalysisFileBean> _files;
        private readonly AnalysisFileBeanModelCollection _fileCollection;
        private readonly CompoundSearcherCollection _compoundSearchers;
        private readonly IMessageBroker _broker;
        private readonly UndoManager _undoManager;

        public DimsAlignmentModel(
            AlignmentFileBeanModel alignmentFileModel,
            DataBaseStorage databaseStorage,
            IMatchResultEvaluator<MsScanMatchResult> evaluator,
            DataBaseMapper mapper,
            ProjectBaseParameterModel projectBaseParameter,
            ParameterBase parameter,
            List<AnalysisFileBean> files,
            AnalysisFileBeanModelCollection fileCollection,
            PeakFilterModel peakFilterModel,
            IMessageBroker broker)
            : base(alignmentFileModel) {
            if (projectBaseParameter is null) {
                throw new ArgumentNullException(nameof(projectBaseParameter));
            }

            _alignmentFile = alignmentFileModel;

            _parameter = parameter;
            _files = files ?? throw new ArgumentNullException(nameof(files));
            _fileCollection = fileCollection ?? throw new ArgumentNullException(nameof(fileCollection));
            _broker = broker;
            _undoManager = new UndoManager().AddTo(Disposables);
            _dataBaseMapper = mapper;
            _matchResultEvaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
            _compoundSearchers = CompoundSearcherCollection.BuildSearchers(databaseStorage, mapper);

            var spotsSource = new AlignmentSpotSource(alignmentFileModel, Container, CHROMATOGRAM_SPOT_SERIALIZER).AddTo(Disposables);
            Ms1Spots = spotsSource.Spots.Items;
            InternalStandardSetModel = new InternalStandardSetModel(Ms1Spots, TargetMsMethod.Dims).AddTo(Disposables);
            NormalizationSetModel = new NormalizationSetModel(Container, _files, _fileCollection, _dataBaseMapper, _matchResultEvaluator, InternalStandardSetModel, _parameter, _broker).AddTo(Disposables);
            PeakSpotNavigatorModel = new PeakSpotNavigatorModel(Ms1Spots, peakFilterModel, evaluator, status: ~(FilterEnableStatus.Rt | FilterEnableStatus.Dt)).AddTo(Disposables);

            var brushMapDataSelector = BrushMapDataSelectorFactory.CreateAlignmentSpotBrushes(parameter.TargetOmics);
            var target = new ReactivePropertySlim<AlignmentSpotPropertyModel>().AddTo(Disposables);
            Target = target;
            var labelSource = PeakSpotNavigatorModel.ObserveProperty(m => m.SelectedAnnotationLabel)
                .ToReadOnlyReactivePropertySlim()
                .AddTo(Disposables);
            PlotModel = new AlignmentPeakPlotModel(spotsSource, spot => spot.MassCenter, spot => spot.KMD, Target, labelSource, brushMapDataSelector.SelectedBrush, brushMapDataSelector.Brushes.ToList())
            {
                GraphTitle = ((IFileBean)_alignmentFile).FileName,
                HorizontalProperty = nameof(AlignmentSpotPropertyModel.MassCenter),
                VerticalProperty = nameof(AlignmentSpotPropertyModel.KMD),
                HorizontalTitle = "m/z",
                VerticalTitle = "Kendrick mass defect"
            }.AddTo(Disposables);

            MatchResultCandidatesModel = new MatchResultCandidatesModel(Target.Select(t => t?.MatchResultsModel)).AddTo(Disposables);
            var refLoader = (parameter.ProjectParam.TargetOmics == TargetOmics.Proteomics)
                ? (IMsSpectrumLoader<MsScanMatchResult>)new ReferenceSpectrumLoader<PeptideMsReference>(mapper)
                : (IMsSpectrumLoader<MsScanMatchResult>)new ReferenceSpectrumLoader<MoleculeMsReference>(mapper);
            IMsSpectrumLoader<AlignmentSpotPropertyModel> decSpecLoader = new AlignmentMSDecSpectrumLoader(_alignmentFile);
            GraphLabels ms2GraphLabels = new GraphLabels("Representation vs. Reference", "m/z", "Relative abundance", nameof(SpectrumPeak.Mass), nameof(SpectrumPeak.Intensity));
            ChartHueItem upperSpectrumHueItem = new ChartHueItem(projectBaseParameter, Colors.Blue);
            SingleSpectrumModel upperSpectrumModel = SingleSpectrumModel.Create(
                Target,
                decSpecLoader,
                new PropertySelector<SpectrumPeak, double>(spot => spot.Mass),
                new PropertySelector<SpectrumPeak, double>(spot => spot.Intensity),
                upperSpectrumHueItem,
                ms2GraphLabels,
                Observable.Return<ISpectraExporter>(null)).AddTo(Disposables);
            ChartHueItem lowerSpectrumHueItem = new ChartHueItem(projectBaseParameter, Colors.Red);
            SingleSpectrumModel lowerSpectrumModel = SingleSpectrumModel.Create(
                MatchResultCandidatesModel.SelectedCandidate,
                refLoader,
                new PropertySelector<SpectrumPeak, double>(spot => spot.Mass),
                new PropertySelector<SpectrumPeak, double>(spot => spot.Intensity),
                lowerSpectrumHueItem,
                ms2GraphLabels,
                Observable.Return<ISpectraExporter>(null)).AddTo(Disposables);
            Ms2SpectrumModel = new MsSpectrumModel(upperSpectrumModel, lowerSpectrumModel, MatchResultCandidatesModel.GetCandidatesScorer(_compoundSearchers))
            {
                GraphTitle = ms2GraphLabels.GraphTitle,
                HorizontalTitle = ms2GraphLabels.HorizontalTitle,
                VerticalTitle = ms2GraphLabels.VerticalTitle,
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
            var peakSpotAxisLabelAsObservable = target.OfType<AlignmentSpotPropertyModel>().SelectSwitch(t => t.ObserveProperty(t_ => t_.IonAbundanceUnit).Select(t_ => t_.ToLabel())).Publish();
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
            BarChartModel = new BarChartModel(Target, barItemsLoaderDataProperty, barItemLoaderDatas, Observable.Return(classBrush), projectBaseParameter, projectBaseParameter.ClassProperties).AddTo(Disposables);

            var classToColor = parameter.ClassnameToColorBytes
                .ToDictionary(kvp => kvp.Key, kvp => Color.FromRgb(kvp.Value[0], kvp.Value[1], kvp.Value[2]));
            var fileIdToFileName = files.ToDictionary(file => file.AnalysisFileId, file => file.AnalysisFileName);
            var eicLoader = alignmentFileModel.CreateEicLoader(CHROMATOGRAM_SPOT_SERIALIZER, fileCollection, projectBaseParameter).AddTo(Disposables);
            AlignmentEicModel = AlignmentEicModel.Create(
                Target, eicLoader,
                files, parameter,
                spot => spot.Time,
                spot => spot.Intensity).AddTo(Disposables);
            AlignmentEicModel.Elements.GraphTitle = "TIC, EIC or BPC chromatograms";
            AlignmentEicModel.Elements.HorizontalTitle = "m/z";
            AlignmentEicModel.Elements.VerticalTitle = "Abundance";
            AlignmentEicModel.Elements.HorizontalProperty = nameof(PeakItem.Time);
            AlignmentEicModel.Elements.VerticalProperty = nameof(PeakItem.Intensity);

            var barItemsLoaderProperty = barItemsLoaderDataProperty.SkipNull().SelectSwitch(data => data.ObservableLoader).ToReactiveProperty().AddTo(Disposables);
            AlignmentSpotTableModel = new DimsAlignmentSpotTableModel(Ms1Spots, Target, Observable.Return(classBrush), projectBaseParameter.ClassProperties, barItemsLoaderProperty, PeakSpotNavigatorModel).AddTo(Disposables);

            _msdecResult = Target.SkipNull()
                .Select(t => _alignmentFile.LoadMSDecResultByIndexAsync(t.MasterAlignmentID))
                .Switch()
                .ToReadOnlyReactivePropertySlim()
                .AddTo(Disposables);
            CanSeachCompound = new[] {
                Target.Select(t => t?.innerModel != null),
                _msdecResult.Select(r => r != null),
            }.CombineLatestValuesAreAllTrue()
            .ToReadOnlyReactivePropertySlim()
            .AddTo(Disposables);

            var mzSpotFocus = new ChromSpotFocus(PlotModel.HorizontalAxis, MZ_TOLERANCE, Target.Select(t => t?.MassCenter ?? 0d), "F3", "m/z", isItalic: true).AddTo(Disposables);
            var idSpotFocus = new IdSpotFocus<AlignmentSpotPropertyModel>(
                Target,
                id => Ms1Spots.Argmin(spot => Math.Abs(spot.MasterAlignmentID - id)),
                Target.Select(t => t?.MasterAlignmentID ?? 0d),
                "ID",
                (mzSpotFocus, spot => spot.MassCenter)).AddTo(Disposables);
            FocusNavigatorModel = new FocusNavigatorModel(idSpotFocus, mzSpotFocus);

            var peakInformationModel = new PeakInformationAlignmentModel(Target).AddTo(Disposables);
            peakInformationModel.Add(
                t => new MzPoint(t?.innerModel.TimesCenter.Mz.Value ?? 0d, t.Refer<MoleculeMsReference>(mapper)?.PrecursorMz));
            peakInformationModel.Add(t => new HeightAmount(t?.HeightAverage ?? 0d));
            PeakInformationModel = peakInformationModel;

            var compoundDetailModel = new CompoundDetailModel(Target.SkipNull().SelectSwitch(t => t.ObserveProperty(p => p.ScanMatchResult)).Publish().RefCount(), mapper).AddTo(Disposables);
            compoundDetailModel.Add(
                r_ => new MzSimilarity(r_?.AcurateMassSimilarity ?? 0d),
                r_ => new SpectrumSimilarity(r_?.WeightedDotProduct ?? 0d, r_?.ReverseDotProduct ?? 0d));
            CompoundDetailModel = compoundDetailModel;

            var moleculeStructureModel = new MoleculeStructureModel().AddTo(Disposables);
            MoleculeStructureModel = moleculeStructureModel;
            Target.Subscribe(t => moleculeStructureModel.UpdateMolecule(t?.innerModel)).AddTo(Disposables);
        }

        public UndoManager UndoManager => _undoManager;

        public ReadOnlyObservableCollection<AlignmentSpotPropertyModel> Ms1Spots { get; }

        public PeakSpotNavigatorModel PeakSpotNavigatorModel { get; }
        public AlignmentPeakPlotModel PlotModel { get; }

        public MsSpectrumModel Ms2SpectrumModel { get; }

        public AlignmentEicModel AlignmentEicModel { get; }

        public BarChartModel BarChartModel { get; }

        public DimsAlignmentSpotTableModel AlignmentSpotTableModel { get; }

        public ReactivePropertySlim<AlignmentSpotPropertyModel> Target { get; }
        public ReadOnlyReactivePropertySlim<bool> CanSeachCompound { get; }
        public FocusNavigatorModel FocusNavigatorModel { get; }
        public PeakInformationAlignmentModel PeakInformationModel { get; }
        public CompoundDetailModel CompoundDetailModel { get; }
        public MoleculeStructureModel MoleculeStructureModel { get; }
        public MatchResultCandidatesModel MatchResultCandidatesModel { get; }

        public ICompoundSearchModel BuildCompoundSearchModel() {
            return new CompoundSearchModel(_files[Target.Value.RepresentativeFileID], Target.Value, _msdecResult.Value, _compoundSearchers, _undoManager);
        }

        public InternalStandardSetModel InternalStandardSetModel { get; }

        public NormalizationSetModel NormalizationSetModel { get; }

        public IObservable<bool> CanSetUnknown => Target.Select(t => !(t is null));
        public void SetUnknown() => Target.Value?.SetUnknown(_undoManager);

        public bool CanSaveSpectra() => Target.Value.innerModel != null && _msdecResult.Value != null;

        public void SaveSpectra(Stream stream, ExportSpectraFileFormat format) {
            SpectraExport.SaveSpectraTable(
                format,
                stream,
                Target.Value.innerModel,
                _msdecResult.Value,
                _dataBaseMapper,
                _parameter);
        }

        public void SaveSpectra(string filename) {
            var format = (ExportSpectraFileFormat)Enum.Parse(typeof(ExportSpectraFileFormat), Path.GetExtension(filename).Trim('.'));
            using (var file = File.Open(filename, FileMode.Create)) {
                SaveSpectra(file, format);
            }
        }

        public void CopySpectrum() {
            var memory = new MemoryStream();
            SaveSpectra(memory, ExportSpectraFileFormat.msp);
            Clipboard.SetText(System.Text.Encoding.UTF8.GetString(memory.ToArray()));
        }

        public override void SearchFragment() {
            using (var decLoader = _alignmentFile.CreateTemporaryMSDecLoader()) {
                MsdialCore.Algorithm.FragmentSearcher.Search(Ms1Spots.Select(n => n.innerModel).ToList(), decLoader, _parameter);
            }
        }

        public override void InvokeMsfinder() {
            if (Target.Value is null || (_msdecResult.Value?.Spectrum).IsEmptyOrNull()) {
                return;
            }
            MsDialToExternalApps.SendToMsFinderProgram(
                _alignmentFile,
                Target.Value.innerModel,
                _msdecResult.Value,
                _dataBaseMapper,
                _parameter);
        }

        public void Undo() => _undoManager.Undo();
        public void Redo() => _undoManager.Redo();
    }
}
