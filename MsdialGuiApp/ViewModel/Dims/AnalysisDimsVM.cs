﻿using CompMs.App.Msdial.ViewModel.DataObj;
using CompMs.Common.Components;
using CompMs.Common.DataObj;
using CompMs.Common.DataObj.Result;
using CompMs.Common.Extension;
using CompMs.CommonMVVM;
using CompMs.Graphics.Core.Base;
using CompMs.MsdialCore.DataObj;
using CompMs.MsdialCore.Parameter;
using CompMs.MsdialCore.Parser;
using CompMs.MsdialCore.Utility;
using CompMs.RawDataHandler.Core;
using NSSplash;
using NSSplash.impl;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Data;

namespace CompMs.App.Msdial.ViewModel.Dims
{
    public class AnalysisDimsVM : AnalysisFileVM
    {
        public ICollectionView Ms1Peaks {
            get => ms1Peaks;
            set {
                var old = ms1Peaks;
                if (SetProperty(ref ms1Peaks, value)) {
                    if (old != null) old.Filter -= PeakFilter;
                    if (ms1Peaks != null) ms1Peaks.Filter += PeakFilter;
                }
            }
        }
        private ICollectionView ms1Peaks;

        public List<ChromatogramPeakWrapper> Eic {
            get => eic;
            set {
                if (SetProperty(ref eic, value)) {
                    OnPropertyChanged(nameof(EicMaxIntensity));
                }
            }
        }

        public double EicMaxIntensity => Eic.Select(peak => peak.Intensity).DefaultIfEmpty().Max();

        private List<ChromatogramPeakWrapper> eic;

        public List<ChromatogramPeakWrapper> PeakEic {
            get => peakEic;
            set => SetProperty(ref peakEic, value);
        }
        private List<ChromatogramPeakWrapper> peakEic;

        public List<ChromatogramPeakWrapper> FocusedEic {
            get => focusedEic;
            set => SetProperty(ref focusedEic, value);
        }
        private List<ChromatogramPeakWrapper> focusedEic;

        public List<SpectrumPeakWrapper> Ms2Spectrum {
            get => ms2Spectrum;
            set {
                if (SetProperty(ref ms2Spectrum, value)) {
                    OnPropertyChanged(nameof(Ms2MassMax));
                    OnPropertyChanged(nameof(Ms2MassMin));
                }
            }
        }
        private List<SpectrumPeakWrapper> ms2Spectrum;

        public List<SpectrumPeakWrapper> Ms2ReferenceSpectrum {
            get => ms2ReferenceSpectrum;
            set {
                if (SetProperty(ref ms2ReferenceSpectrum, value)) {
                    OnPropertyChanged(nameof(Ms2MassMax));
                    OnPropertyChanged(nameof(Ms2MassMin));
                }
            }
        }
        private List<SpectrumPeakWrapper> ms2ReferenceSpectrum;

        public double Ms2MassMin => Ms2Spectrum.Concat(Ms2ReferenceSpectrum).Select(peak => peak.Mass).DefaultIfEmpty().Min();
        public double Ms2MassMax => Ms2Spectrum.Concat(Ms2ReferenceSpectrum).Select(peak => peak.Mass).DefaultIfEmpty().Max();

        public List<ChromatogramPeakFeature> Peaks { get; }

        public ChromatogramPeakFeatureVM Target {
            get => target;
            set => SetProperty(ref target, value);
        }
        private ChromatogramPeakFeatureVM target;

        public string FileName {
            get => fileName;
            set => SetProperty(ref fileName, value);
        }
        private string fileName;

        public string RawSplashKey {
            get => rawSplashKey;
            set => SetProperty(ref rawSplashKey, value);
        }
        private string rawSplashKey;

        public string DisplayLabel {
            get => displayLabel;
            set => SetProperty(ref displayLabel, value);
        }
        private string displayLabel;

        public bool RefMatchedChecked => ReadDisplayFilters(DisplayFilter.RefMatched);
        public bool SuggestedChecked => ReadDisplayFilters(DisplayFilter.Suggested);
        public bool UnknownChecked => ReadDisplayFilters(DisplayFilter.Unknown);
        public bool Ms2AcquiredChecked => ReadDisplayFilters(DisplayFilter.Ms2Acquired);
        public bool MolecularIonChecked => ReadDisplayFilters(DisplayFilter.MolecularIon);
        // public bool BlankFilterChecked => ReadDisplayFilters(DisplayFilter.Blank);
        // public bool UniqueIonsChecked => ReadDisplayFilters(DisplayFilter.UniqueIons);

        internal DisplayFilter DisplayFilters {
            get => displayFilters;
            set => SetProperty(ref displayFilters, value);
        }
        private DisplayFilter displayFilters = 0;


        public double AmplitudeLowerValue {
            get => amplitudeLowerValue;
            set => SetProperty(ref amplitudeLowerValue, value);
        }

        public double AmplitudeUpperValue {
            get => amplitudeUpperValue;
            set => SetProperty(ref amplitudeUpperValue, value);
        }
        private double amplitudeLowerValue = 0d, amplitudeUpperValue = 1d;

        public double AmplitudeOrderMin { get; }
        public double AmplitudeOrderMax { get; }

        public int FocusID {
            get => focusID;
            set => SetProperty(ref focusID, value);
        }
        private int focusID;

        public double FocusMz {
            get => focusMz;
            set => SetProperty(ref focusMz, value);
        }
        private double focusMz;

        public double MassMin => _ms1Peaks.Min(peak => peak.Mass);
        public double MassMax => _ms1Peaks.Max(peak => peak.Mass);
        public double IntensityMin => _ms1Peaks.Min(peak => peak.Intensity);
        public double IntensityMax => _ms1Peaks.Max(peak => peak.Intensity);
        private ObservableCollection<ChromatogramPeakFeatureVM> _ms1Peaks;

        public double Ms1Tolerance => param.CentroidMs1Tolerance;

        private ParameterBase param;
        private List<RawSpectrum> spectrumList;
        private IReadOnlyList<MoleculeMsReference> msps;

        public AnalysisDimsVM(AnalysisFileBean analysisFileBean, ParameterBase param, IReadOnlyList<MoleculeMsReference> msps) {
            this.param = param;
            this.msps = msps;

            FileName = analysisFileBean.AnalysisFileName;

            var peaks = MsdialSerializer.LoadChromatogramPeakFeatures(analysisFileBean.PeakAreaBeanInformationFilePath);
            _ms1Peaks = new ObservableCollection<ChromatogramPeakFeatureVM>(
                peaks.Select(peak => new ChromatogramPeakFeatureVM(peak))
            );
            Peaks = peaks;
            AmplitudeOrderMin = _ms1Peaks.Min(peak => peak.AmplitudeOrderValue);
            AmplitudeOrderMax = _ms1Peaks.Max(peak => peak.AmplitudeOrderValue);
            Ms1Peaks = CollectionViewSource.GetDefaultView(_ms1Peaks);

            using (var access = new RawDataAccess(analysisFileBean.AnalysisFilePath, 0, true)) {
                RawMeasurement rawObj = null;
                foreach (var i in Enumerable.Range(0, 5)) {
                    rawObj = DataAccess.GetRawDataMeasurement(access);
                    if (rawObj != null) break;
                    Thread.Sleep(2000);
                }
                if (rawObj == null) {
                    throw new FileLoadException($"Loading {analysisFileBean.AnalysisFilePath} failed.");
                }
                spectrumList = rawObj.SpectrumList;
            }

            PropertyChanged += OnTargetChanged;
            PropertyChanged += OnFilterChanged;

            Target = _ms1Peaks.FirstOrDefault();
        }

        bool PeakFilter(object obj) {
            if (obj is ChromatogramPeakFeatureVM peak) {
                return AnnotationFilter(peak)
                    && AmplitudeFilter(peak)
                    && (!Ms2AcquiredChecked || peak.IsMsmsContained)
                    && (!MolecularIonChecked || peak.IsotopeWeightNumber == 0);
            }
            return false;
        }

        bool AnnotationFilter(ChromatogramPeakFeatureVM peak) {
            if (!ReadDisplayFilters(DisplayFilter.Annotates)) return true;
            return RefMatchedChecked && peak.IsRefMatched
                || SuggestedChecked && peak.IsSuggested
                || UnknownChecked && peak.IsUnknown;
        }

        bool AmplitudeFilter(ChromatogramPeakFeatureVM peak) {
            return AmplitudeLowerValue * (AmplitudeOrderMax - AmplitudeOrderMin) <= peak.AmplitudeOrderValue - AmplitudeOrderMin
                && peak.AmplitudeScore - AmplitudeOrderMin <= AmplitudeUpperValue * (AmplitudeOrderMax - AmplitudeOrderMin);
        }

        void OnFilterChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(DisplayFilters)
                || e.PropertyName == nameof(AmplitudeLowerValue)
                || e.PropertyName == nameof(AmplitudeUpperValue))
                Ms1Peaks?.Refresh();
        }

        void OnTargetChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(Target)) {
                if (Target == null) {
                    Eic = new List<ChromatogramPeakWrapper>();
                    return;
                }

                Eic = DataAccess.GetSmoothedPeaklist(
                    DataAccess.GetMs1Peaklist(
                        spectrumList, Target.Mass, param.CentroidMs1Tolerance, param.IonMode,
                        ChromXType.RT, ChromXUnit.Min,  // TODO: hard coded for Di
                        param.RetentionTimeBegin, param.RetentionTimeEnd
                        ),
                    param.SmoothingMethod, param.SmoothingLevel
                ).Select(peak => new ChromatogramPeakWrapper(peak)).DefaultIfEmpty().ToList();

                PeakEic = Eic.Where(peak => Target.ChromXLeftValue <= peak.ChromXValue && peak.ChromXValue <= Target.ChromXRightValue).ToList();
                FocusedEic = Target.ChromXValue.HasValue
                    ? new List<ChromatogramPeakWrapper> {
                        Eic.Where(peak => peak.ChromXValue.HasValue)
                           .DefaultIfEmpty()
                           .Argmin(peak => Math.Abs(Target.ChromXValue.Value - peak.ChromXValue.Value))
                    }
                    : new List<ChromatogramPeakWrapper>();

                var spectra = DataAccess.GetCentroidMassSpectra(spectrumList, param.MSDataType, Target.MS1RawSpectrumIdTop, 0, float.MinValue, float.MaxValue);
                spectra = DataAccess.GetCentroidMassSpectra(spectrumList, param.MS2DataType, Target.MS2RawSpectrumId, 0, float.MinValue, float.MaxValue);

                Ms2Spectrum = spectra.Select(peak => new SpectrumPeakWrapper(peak)).ToList();
                RawSplashKey = CalculateSplashKey(spectra);

                Ms2ReferenceSpectrum = new List<SpectrumPeakWrapper>();
                if (Target.TextDbBasedMatchResult == null && Target.MspBasedMatchResult is MsScanMatchResult matched) {
                    var reference = msps[matched.LibraryIDWhenOrdered];
                    if (matched.LibraryID != reference.ScanID) {
                        reference = msps.FirstOrDefault(msp => msp.ScanID == matched.LibraryID);
                    }
                    Ms2ReferenceSpectrum = reference?.Spectrum.Select(peak => new SpectrumPeakWrapper(peak)).ToList() ?? new List<SpectrumPeakWrapper>();
                }

                FocusID = Target.InnerModel.MasterPeakID;
                FocusMz = Target.Mass;
            }
        }

        static string CalculateSplashKey(IReadOnlyCollection<SpectrumPeak> spectra) {
            if (spectra.IsEmptyOrNull() || spectra.Count <= 2 && spectra.All(peak => peak.Intensity == 0))
                return "N/A";
            var msspectrum = new MSSpectrum(string.Join(" ", spectra.Select(peak => $"{peak.Mass}:{peak.Intensity}").ToArray()));
            return new Splash().splashIt(msspectrum);
        }

        public DelegateCommand<object> FocusByIDCommand => focusByIDCommand ?? (focusByIDCommand = new DelegateCommand<object>(FocusByID));
        private DelegateCommand<object> focusByIDCommand;

        private void FocusByID(object axis) {
            var focus = _ms1Peaks.FirstOrDefault(peak => peak.InnerModel.MasterPeakID == FocusID);
            Ms1Peaks.MoveCurrentTo(focus);
            (axis as AxisManager)?.Focus(focus.Mass - MzTol, focus.Mass + MzTol);
        }

        public DelegateCommand<AxisManager> FocusByMzCommand => focusByMzCommand ?? (focusByMzCommand = new DelegateCommand<AxisManager>(FocusByMz));
        private DelegateCommand<AxisManager> focusByMzCommand;

        private static readonly double MzTol = 20;
        private void FocusByMz(AxisManager axis) {
            axis.Focus(FocusMz - MzTol, FocusMz + MzTol);
        }

        private bool ReadDisplayFilters(DisplayFilter flags) {
            return (flags & DisplayFilters) != 0;
        }
    }
}
