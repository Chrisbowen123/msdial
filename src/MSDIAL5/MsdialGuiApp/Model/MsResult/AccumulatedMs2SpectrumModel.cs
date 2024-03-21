﻿using CompMs.App.Msdial.Model.Chart;
using CompMs.App.Msdial.Model.DataObj;
using CompMs.App.Msdial.Model.Search;
using CompMs.App.Msdial.ViewModel.Service;
using CompMs.Common.Algorithm.PeakPick;
using CompMs.Common.Components;
using CompMs.Common.DataObj;
using CompMs.Common.Parameter;
using CompMs.CommonMVVM;
using CompMs.Graphics.AxisManager.Generic;
using CompMs.Graphics.Core.Base;
using CompMs.MsdialCore.Export;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using Reactive.Bindings.Notifiers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;

namespace CompMs.App.Msdial.Model.MsResult;

internal sealed class AccumulatedMs2SpectrumModel : DisposableModelBase
{
    private readonly AccumulateSpectraUsecase _accumulateSpectra;
    private readonly ICompoundSearchUsecase<ICompoundResult, PeakSpotModel>? _compoundSearch;
    private readonly LoadChromatogramsUsecase _loadingChromatograms;
    private readonly IMessageBroker _broker;
    private readonly BehaviorSubject<MsSpectrum?> _subject;

    public AccumulatedMs2SpectrumModel(DisplayExtractedIonChromatogram chromatogram, AccumulateSpectraUsecase accumulateSpectra, ICompoundSearchUsecase<ICompoundResult, PeakSpotModel>? compoundSearch, LoadChromatogramsUsecase loadingChromatograms, IMessageBroker broker) {
        Chromatogram = chromatogram;
        _accumulateSpectra = accumulateSpectra;
        _compoundSearch = compoundSearch;
        _loadingChromatograms = loadingChromatograms;
        _broker = broker;
        _plotDisposable = new SerialDisposable().AddTo(Disposables);
        _subject = new BehaviorSubject<MsSpectrum?>(null).AddTo(Disposables);
        SearchParameter = (compoundSearch?.ObserveProperty(m => m.SearchParameter) ?? Observable.Return<MsRefSearchParameterBase?>(null)).ToReadOnlyReactivePropertySlim().AddTo(Disposables);
    }

    public DisplayExtractedIonChromatogram Chromatogram { get; }

    public MSScanProperty? Scan {
        get => _scan;
        private set {
            if (SetProperty(ref _scan, value)) {
                if (_scan is not null) {
                    _subject.OnNext(new MsSpectrum(_scan.Spectrum));
                }
                else {
                    _subject.OnNext(null);
                }
            }
        }
    }
    private MSScanProperty? _scan;

    public PeakSpotModel? PeakSpot {
        get => _peakSpot;
        private set => SetProperty(ref _peakSpot, value);
    }
    private PeakSpotModel? _peakSpot;

    public PlotComparedMsSpectrumUsecase? PlotComparedSpectrum {
        get => _plotComparedSpectrum;
        private set {
            if (SetProperty(ref _plotComparedSpectrum, value)) {
                _plotDisposable.Disposable = value;
            }
        }
    }
    private PlotComparedMsSpectrumUsecase? _plotComparedSpectrum;
    private readonly SerialDisposable _plotDisposable;

    public IReadOnlyList<ICompoundResult>? Compounds {
        get => _compounds;
        private set => SetProperty(ref _compounds, value);
    }
    private IReadOnlyList<ICompoundResult>? _compounds;

    public ICompoundResult? SelectedCompound {
        get => _selectedCompound;
        set {
            if (SetProperty(ref _selectedCompound, value)) {
                PlotComparedSpectrum?.UpdateReference(value?.MsReference);
            }
        }
    }
    private ICompoundResult? _selectedCompound;

    public AxisRange? SelectedRange {
        get => _selectedRange;
        set => SetProperty(ref _selectedRange, value);
    }
    private AxisRange? _selectedRange;

    public ChromatogramsModel? ProductIonChromatogram {
        get => _productIonChromatogram;
        private set => SetProperty(ref _productIonChromatogram, value);
    }
    private ChromatogramsModel? _productIonChromatogram;

    public AxisRange? ProductIonRange {
        get => _productIonRange;
        set => SetProperty(ref _productIonRange, value);
    }
    private AxisRange? _productIonRange;

    public IList SearchMethods => _compoundSearch?.SearchMethods ?? Array.Empty<object>();

    public object? SearchMethod {
        get => _compoundSearch?.SearchMethod;
        set {
            if (_compoundSearch is not null && _compoundSearch.SearchMethod != value) {
                _compoundSearch.SearchMethod = value;
                OnPropertyChanged(nameof(SearchMethod));
            }
        }
    }

    public ReadOnlyReactivePropertySlim<MsRefSearchParameterBase?> SearchParameter { get; }

    public async Task CalculateMs2Async((double start, double end) baseRange, IEnumerable<(double start, double end)> subtracts, CancellationToken token = default) {
        Scan = await _accumulateSpectra.AccumulateMs2Async(Chromatogram.Mz, Chromatogram.Tolerance, baseRange, subtracts, token).ConfigureAwait(false);
        var anotatedSpot = new AnnotatedSpotModel(
            Chromatogram.DetectPeak(baseRange.start, baseRange.end),
            new MoleculeProperty());
        PeakSpot = new PeakSpotModel(anotatedSpot, Scan);
        PlotComparedSpectrum = new PlotComparedMsSpectrumUsecase(Scan);

        CalculateProductIonTotalIonChromatogram();
    }

    public void CalculateProductIonTotalIonChromatogram() {
        if (PlotComparedSpectrum is null) {
            return;
        }

        var range = MzRange.FromRange(0d, double.MaxValue);
        ProductIonChromatogram = _loadingChromatograms.LoadMS2Eic(Chromatogram.MzRange, range);
        if (ProductIonChromatogram.AbundanceAxisItemSelector.SelectedAxisItem.AxisManager is BaseAxisManager<double> chromAxis) {
            chromAxis.ChartMargin = new ConstantMargin(0, 60);
        }
    }

    public void CalculateProductIonChromatogram() {
        if (PlotComparedSpectrum is null) {
            return;
        }

        var axis = PlotComparedSpectrum.MsSpectrumModel.UpperSpectrumModel.HorizontalPropertySelectors.AxisItemSelector.SelectedAxisItem.AxisManager;
        var (start, end) = new RangeSelection(SelectedRange).ConvertBy(axis);
        var range = MzRange.FromRange(start, end);
        ProductIonChromatogram = _loadingChromatograms.LoadMS2Eic(Chromatogram.MzRange, range);
        if (ProductIonChromatogram.AbundanceAxisItemSelector.SelectedAxisItem.AxisManager is BaseAxisManager<double> chromAxis) {
            chromAxis.ChartMargin = new ConstantMargin(0, 40);
        }
    }

    public void DetectPeaks() {
        var detector = new PeakDetection(1, 0d);
        ProductIonChromatogram?.DetectPeaks(detector);
    }

    public void AddPeak() {
        if (ProductIonRange is not null && ProductIonChromatogram is not null) {
            var range = new RangeSelection(ProductIonRange).ConvertBy(ProductIonChromatogram.ChromAxisItemSelector.SelectedAxisItem.AxisManager);
            ProductIonChromatogram?.AddPeak(range.Item1, range.Item2);
            ProductIonRange = null;
        }
    }

    public void ResetPeaks() {
        ProductIonChromatogram?.ResetPeaks();
    }

    public void SearchCompound() {
        if (PeakSpot is null || _compoundSearch is null) {
            return;
        }
        Compounds = _compoundSearch.Search(PeakSpot);
    }

    public void Export() {
        if (PeakSpot is null || Scan is null) {
            return;
        }
        var builder = new NistRecordBuilder();
        builder.SetIonPropertyProperties(PeakSpot.PeakSpot.MSIon);
        builder.SetMSProperties(PeakSpot.PeakSpot.MSIon);
        builder.SetScan(Scan);
        var request = new SaveFileNameRequest(filename => {
            using var stream = File.Open(filename, FileMode.Create);
            builder.Export(stream);
        })
        {
            Title = "Save spectrum",
            Filter = "NIST format|*.msp|All|*",
            RestoreDirectory = true,
            AddExtension = true,
        };
        _broker.Publish(request);
    }
}
