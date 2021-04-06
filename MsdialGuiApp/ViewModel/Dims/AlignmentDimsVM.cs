﻿using CompMs.App.Msdial.Model.DataObj;
using CompMs.Common.Components;
using CompMs.Common.DataObj.Result;
using CompMs.Common.Enum;
using CompMs.Common.Interfaces;
using CompMs.Common.MessagePack;
using CompMs.CommonMVVM;
using CompMs.Graphics.AxisManager;
using CompMs.MsdialCore.Algorithm.Annotation;
using CompMs.MsdialCore.DataObj;
using CompMs.MsdialCore.Export;
using CompMs.MsdialCore.MSDec;
using CompMs.MsdialCore.Parameter;
using CompMs.MsdialCore.Parser;
using CompMs.MsdialDimsCore.Algorithm.Annotation;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;


namespace CompMs.App.Msdial.ViewModel.Dims
{
    public class AlignmentDimsVM : AlignmentFileVM
    {
        public ICollectionView Ms1Spots {
            get => ms1Spots;
            set {
                var old = ms1Spots;
                if (SetProperty(ref ms1Spots, value)) {
                    if (old != null) old.Filter -= PeakFilter;
                    if (ms1Spots != null) ms1Spots.Filter += PeakFilter;
                }
            }
        }
        private ICollectionView ms1Spots;
        private ObservableCollection<AlignmentSpotPropertyModel> _ms1Spots = new ObservableCollection<AlignmentSpotPropertyModel>();

        public double MassMin => _ms1Spots.Min(spot => spot.MassCenter);
        public double MassMax => _ms1Spots.Max(spot => spot.MassCenter);
        public double MassLower {
            get => massLower;
            set {
                if (SetProperty(ref massLower, value))
                    Ms1Spots?.Refresh();
            }
        }
        public double MassUpper {
            get => massUpper;
            set {
                if (SetProperty(ref massUpper, value))
                    Ms1Spots?.Refresh();
            }
        }
        private double massLower, massUpper;

        public List<BarItem> BarItems {
            get => barItems;
            set => SetProperty(ref barItems, value);
        }
        private List<BarItem> barItems = new List<BarItem>();

        public AlignmentResultContainer Container {
            get => container;
            set => SetProperty(ref container, value);
        }
        private AlignmentResultContainer container;

        public AlignmentSpotPropertyModel Target {
            get => target;
            set {
                if (SetProperty(ref target, value))
                    SearchCompoundCommand.RaiseCanExecuteChanged();
            }
        }
        private AlignmentSpotPropertyModel target;

        public List<Chromatogram> EicChromatograms {
            get => eicChromatograms;
            set {
                if (SetProperty(ref eicChromatograms, value)) {
                    OnPropertyChanged(nameof(EicMax));
                    OnPropertyChanged(nameof(EicMin));
                    OnPropertyChanged(nameof(IntensityMax));
                    OnPropertyChanged(nameof(IntensityMin));
                }
            }
        }
        private List<Chromatogram> eicChromatograms;

        public double EicMax => EicChromatograms?.SelectMany(chrom => chrom.Peaks).DefaultIfEmpty().Max(peak => peak?.Time) ?? 0;
        public double EicMin => EicChromatograms?.SelectMany(chrom => chrom.Peaks).DefaultIfEmpty().Min(peak => peak?.Time) ?? 0;
        public double IntensityMax => EicChromatograms?.SelectMany(chrom => chrom.Peaks).DefaultIfEmpty().Max(peak => peak?.Intensity) ?? 0;
        public double IntensityMin => EicChromatograms?.SelectMany(chrom => chrom.Peaks).DefaultIfEmpty().Min(peak => peak?.Intensity) ?? 0;

        public List<SpectrumPeakWrapper> Ms2Spectrum {
            get => ms2Spectrum;
            set {
                if (SetProperty(ref ms2Spectrum, value)) {
                    OnPropertyChanged(nameof(Ms2MassMin));
                    OnPropertyChanged(nameof(Ms2MassMax));
                }
            }
        }
        private List<SpectrumPeakWrapper> ms2Spectrum = new List<SpectrumPeakWrapper>();

        public List<SpectrumPeakWrapper> Ms2ReferenceSpectrum {
            get => ms2ReferenceSpectrum;
            set {
                if (SetProperty(ref ms2ReferenceSpectrum, value)) {
                    OnPropertyChanged(nameof(Ms2MassMin));
                    OnPropertyChanged(nameof(Ms2MassMax));
                }
            }
        }
        private List<SpectrumPeakWrapper> ms2ReferenceSpectrum = new List<SpectrumPeakWrapper>();

        public double Ms2MassMin => Ms2Spectrum.Concat(Ms2ReferenceSpectrum).DefaultIfEmpty().Min(peak => peak?.Mass) ?? 0;
        public double Ms2MassMax => Ms2Spectrum.Concat(Ms2ReferenceSpectrum).DefaultIfEmpty().Max(peak => peak?.Mass) ?? 0;

        public string FileName {
            get => fileName;
            set => SetProperty(ref fileName, value);
        }
        private string fileName = string.Empty;

        public bool RefMatchedChecked => ReadDisplayFilters(DisplayFilter.RefMatched);
        public bool SuggestedChecked => ReadDisplayFilters(DisplayFilter.Suggested);
        public bool UnknownChecked => ReadDisplayFilters(DisplayFilter.Unknown);
        public bool Ms2AcquiredChecked => ReadDisplayFilters(DisplayFilter.Ms2Acquired);
        public bool MolecularIonChecked => ReadDisplayFilters(DisplayFilter.MolecularIon);
        public bool BlankFilterChecked => ReadDisplayFilters(DisplayFilter.Blank);
        // public bool UniqueIonsChecked => ReadDisplayFilters(DisplayFilter.UniqueIons);
        public bool ManuallyModifiedChecked => ReadDisplayFilters(DisplayFilter.ManuallyModified);

        public DisplayFilter DisplayFilters {
            get => displayFilters;
            internal set {
                if (SetProperty(ref displayFilters, value))
                    Ms1Spots?.Refresh();
            }
        }
        private DisplayFilter displayFilters = 0;

        public string CommentFilterKeyword {
            get => commentFilterKeyword;
            set {
                if (SetProperty(ref commentFilterKeyword, value)){
                    if (!string.IsNullOrEmpty(commentFilterKeyword)) {
                        commentFilterKeywords = commentFilterKeyword.Split().ToList();
                    }
                    else {
                        commentFilterKeywords = new List<string>(0);
                    }
                    Ms1Spots?.Refresh();
                }
            }
        }
        private string commentFilterKeyword;
        private List<string> commentFilterKeywords = new List<string>(0);

        public string MetaboliteFilterKeyword {
            get => metaboliteFilterKeyword;
            set {
                if (SetProperty(ref metaboliteFilterKeyword, value)) {
                    if (!string.IsNullOrEmpty(metaboliteFilterKeyword)) {
                        metaboliteFilterKeywords = metaboliteFilterKeyword.Split().ToList();
                    }
                    else {
                        metaboliteFilterKeywords = new List<string>(0);
                    }
                    Ms1Spots?.Refresh();
                }
            }
        }
        private string metaboliteFilterKeyword;
        private List<string> metaboliteFilterKeywords = new List<string>(0);

        private readonly AlignmentFileBean alignmentFile;
        private readonly List<long> seekPointers = new List<long>();
        private readonly ParameterBase param = null;
        private readonly string resultFile = string.Empty;
        private readonly string eicFile = string.Empty;
        private readonly string spectraFile = string.Empty;
        private readonly IAnnotator<AlignmentSpotProperty, MSDecResult> mspAnnotator;

        private MSDecResult msdecResult = null;

        
        private static ChromatogramSerializer<ChromatogramSpotInfo> chromatogramSpotSerializer;

        static AlignmentDimsVM() {
            chromatogramSpotSerializer = ChromatogramSerializerFactory.CreateSpotSerializer("CSS1", CompMs.Common.Components.ChromXType.Mz);
        }

        public AlignmentDimsVM(AlignmentFileBean alignmentFileBean, ParameterBase param, List<MoleculeMsReference> msp)
            : this(alignmentFileBean, param, new DimsMspAnnotator(msp, param.MspSearchParam, param.TargetOmics)) {
        }

        public AlignmentDimsVM(AlignmentFileBean alignmentFileBean, ParameterBase param, IAnnotator<AlignmentSpotProperty, MSDecResult> mspAnnotator) {
            alignmentFile = alignmentFileBean;
            fileName = alignmentFileBean.FileName;
            resultFile = alignmentFileBean.FilePath;
            eicFile = alignmentFileBean.EicFilePath;
            spectraFile = alignmentFileBean.SpectraFilePath;

            this.param = param;
            this.mspAnnotator = mspAnnotator;

            Container = MessagePackHandler.LoadFromFile<AlignmentResultContainer>(resultFile);

            _ms1Spots = new ObservableCollection<AlignmentSpotPropertyModel>(Container.AlignmentSpotProperties.Select(prop => new AlignmentSpotPropertyModel(prop, param.FileID_ClassName)));
            Ms1Spots = CollectionViewSource.GetDefaultView(_ms1Spots);
            MassLower = MassMin;
            MassUpper = MassMax;

            MsdecResultsReader.GetSeekPointers(alignmentFileBean.SpectraFilePath, out _, out seekPointers, out _);

            var mzAxis = new ContinuousAxisManager
            {
                MinValue = MassMin,
                MaxValue = MassMax,
                ChartMargin = new Graphics.Core.Base.ChartMargin
                {
                    Left = 0.05, Right = 0.05
                },
            };

            var kmdAxis = new ContinuousAxisManager
            {
                MinValue = -0.5,
                MaxValue = 0.5,
                ChartMargin = new Graphics.Core.Base.ChartMargin
                {
                    Left = 0.05, Right = 0.05
                },
            };

            PlotViewModel = new AlignmentPeakPlotVM(
                _ms1Spots,
                mzAxis,
                kmdAxis,
                "MassCenter",
                "KMD",
                FileName,
                "m/z",
                "Kendrick mass defect");

            PlotViewModel.PropertyChanged += OnPlotViewModelTargetChanged;
            PropertyChanged += OnTargetChanged;
        }

        public AlignmentPeakPlotVM PlotViewModel {
            get => plotViewModel;
            private set => SetProperty(ref plotViewModel, value);
        }
        private AlignmentPeakPlotVM plotViewModel;

        private void OnPlotViewModelTargetChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(PlotViewModel.Target)) {
                Target = PlotViewModel.Target;
            }
        }

        private async void OnTargetChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(Target)) {
                await OnTargetChanged(Target).ConfigureAwait(false);
            }
        }

        private async Task OnTargetChanged(AlignmentSpotPropertyModel target) {
            await Task.WhenAll(
                LoadBarItemsAsync(target),
                LoadEicAsync(target),
                LoadMs2SpectrumAsync(target),
                LoadMs2ReferenceAsync(target)
           ).ConfigureAwait(false);
        }

        async Task LoadBarItemsAsync(AlignmentSpotPropertyModel target) {
            BarItems = new List<BarItem>();
            if (target == null)
                return;

            // TODO: Implement other features (PeakHeight, PeakArea, Normalized PeakHeight, Normalized PeakArea)
            BarItems = await Task.Run(() => 
                target.AlignedPeakProperties
                .GroupBy(peak => param.FileID_ClassName[peak.FileID])
                .Select(pair => new BarItem { Class = pair.Key, Height = pair.Average(peak => peak.PeakHeightTop) })
                .ToList() ).ConfigureAwait(false);
        }

        async Task LoadEicAsync(AlignmentSpotPropertyModel target) {
            EicChromatograms = new List<Chromatogram>();
            if (target == null)
                return;

            // maybe using file pointer is better
            EicChromatograms = await Task.Run(() => {
                var spotinfo = chromatogramSpotSerializer.DeserializeAtFromFile(eicFile, target.MasterAlignmentID);
                var chroms = new List<Chromatogram>(spotinfo.PeakInfos.Count);
                foreach (var peakinfo in spotinfo.PeakInfos) {
                    var items = peakinfo.Chromatogram.Select(chrom => new PeakItem(chrom)).ToList();
                    var peakitems = items.Where(item => peakinfo.ChromXsLeft.Value <= item.Time && item.Time <= peakinfo.ChromXsRight.Value).ToList();
                    chroms.Add(new Chromatogram
                    {
                        Class = param.FileID_ClassName[peakinfo.FileID],
                        Peaks = items,
                        PeakArea = peakitems,
                    });
                }
                return chroms;
            }).ConfigureAwait(false);
        }

        async Task LoadMs2SpectrumAsync(AlignmentSpotPropertyModel target) {
            Ms2Spectrum = new List<SpectrumPeakWrapper>();
            if (target == null)
                return;

            await Task.Run(() => {
                var idx = _ms1Spots.IndexOf(target);
                msdecResult = MsdecResultsReader.ReadMSDecResult(spectraFile, seekPointers[idx]);
                Ms2Spectrum = msdecResult.Spectrum.Select(spec => new SpectrumPeakWrapper(spec)).ToList();
            }).ConfigureAwait(false);
        }

        async Task LoadMs2ReferenceAsync(AlignmentSpotPropertyModel target) {
            Ms2ReferenceSpectrum = new List<SpectrumPeakWrapper>();
            if (target == null)
                return;

            await Task.Run(() => {
                var representative = RetrieveMspMatchResult(target.innerModel);
                if (representative == null)
                    return;

                var reference = mspAnnotator.Refer(representative);
                if (reference != null) {
                    Ms2ReferenceSpectrum = reference.Spectrum.Select(peak => new SpectrumPeakWrapper(peak)).ToList();
                }
            }).ConfigureAwait(false);
        }

        MsScanMatchResult RetrieveMspMatchResult(AlignmentSpotProperty prop) {
            if (prop.MatchResults?.Representative is MsScanMatchResult representative) {
                if ((representative.Priority & (DataBasePriority.Unknown | DataBasePriority.Manual)) == (DataBasePriority.Unknown | DataBasePriority.Manual))
                    return null;
                if (prop.MatchResults.TextDbBasedMatchResults.Contains(representative)) {
                    return null;
                }
                if ((representative.Priority & DataBasePriority.Unknown) == DataBasePriority.None) {
                    return representative;
                }
            }
            return prop.MspBasedMatchResult;
        }

        bool PeakFilter(object obj) {
            if (obj is AlignmentSpotPropertyModel spot) {
                return AnnotationFilter(spot)
                    && MzFilter(spot)
                    && (!Ms2AcquiredChecked || spot.IsMsmsAssigned)
                    && (!MolecularIonChecked || spot.IsBaseIsotopeIon)
                    && (!BlankFilterChecked || spot.IsBlankFiltered)
                    && MetaboliteFilter(spot, metaboliteFilterKeywords)
                    && CommentFilter(spot, commentFilterKeywords)
                    && (!ManuallyModifiedChecked || spot.innerModel.IsManuallyModifiedForAnnotation);
            }
            return false;
        }

        bool AnnotationFilter(AlignmentSpotPropertyModel spot) {
            if (!ReadDisplayFilters(DisplayFilter.Annotates)) return true;
            return RefMatchedChecked && spot.IsRefMatched
                || SuggestedChecked && spot.IsSuggested
                || UnknownChecked && spot.IsUnknown;
        }

        bool MzFilter(AlignmentSpotPropertyModel spot) {
            return MassLower <= spot.MassCenter
                && spot.MassCenter <= MassUpper;
        }

        bool CommentFilter(AlignmentSpotPropertyModel spot, IEnumerable<string> keywords) {
            return keywords.All(keyword => spot.Comment.Contains(keyword));
        }

        bool MetaboliteFilter(AlignmentSpotPropertyModel spot, IEnumerable<string> keywords) {
            return keywords.All(keyword => spot.Name.Contains(keyword));
        }

        public DelegateCommand<Window> SearchCompoundCommand => searchCompoundCommand ?? (searchCompoundCommand = new DelegateCommand<Window>(SearchCompound, CanSearchCompound));
        private DelegateCommand<Window> searchCompoundCommand;

        private void SearchCompound(Window owner) {
            if (Target?.innerModel == null)
                return;

            var vm = new CompoundSearchVM<AlignmentSpotProperty>(alignmentFile, Target.innerModel, msdecResult, null, mspAnnotator, param.MspSearchParam);
            var window = new View.CompoundSearchWindow
            {
                DataContext = vm,
                Owner = owner,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };

            if (window.ShowDialog() == true) {
                Target.RaisePropertyChanged();
                OnPropertyChanged(nameof(Target));
                Ms1Spots?.Refresh();
            }
        }

        private bool CanSearchCompound(Window owner) => (Target?.innerModel) != null;

        public DelegateCommand<Window> SaveMs2SpectrumCommand => saveMs2SpectrumCommand ?? (saveMs2SpectrumCommand = new DelegateCommand<Window>(SaveSpectra, CanSaveSpectra));
        private DelegateCommand<Window> saveMs2SpectrumCommand;

        private void SaveSpectra(Window owner)
        {
            var sfd = new SaveFileDialog
            {
                Title = "Save spectra",
                Filter = "NIST format(*.msp)|*.msp", // MassBank format(*.txt)|*.txt;|MASCOT format(*.mgf)|*.mgf;
                RestoreDirectory = true,
                AddExtension = true,
            };

            if (sfd.ShowDialog(owner) == true)
            {
                var filename = sfd.FileName;
                SpectraExport.SaveSpectraTable(
                    (ExportSpectraFileFormat)Enum.Parse(typeof(ExportSpectraFileFormat), Path.GetExtension(filename).Trim('.')),
                    filename,
                    Target.innerModel,
                    msdecResult,
                    param);
            }
        }

        private bool CanSaveSpectra(Window owner)
        {
            if (Target.innerModel == null)
                return false;
            if (msdecResult == null)
                return false;
            return true;
        }

        public DelegateCommand<Window> ShowIonTableCommand => showIonTableCommand ?? (showIonTableCommand = new DelegateCommand<Window>(ShowIonTable));
        private DelegateCommand<Window> showIonTableCommand;

        private void ShowIonTable(Window owner) {
            var window = new View.Dims.IonTableViewer
            {
                DataContext = this,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Owner = owner,
            };

            window.Show();
        }

        public void SaveProject() {
            MessagePackHandler.SaveToFile<AlignmentResultContainer>(Container, resultFile);
        }

        private bool ReadDisplayFilters(DisplayFilter flags) {
            return (flags & DisplayFilters) != 0;
        }
    }
}
