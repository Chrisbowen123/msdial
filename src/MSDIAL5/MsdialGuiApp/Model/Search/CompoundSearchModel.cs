﻿using CompMs.App.Msdial.Model.Chart;
using CompMs.App.Msdial.Model.DataObj;
using CompMs.App.Msdial.Model.Information;
using CompMs.App.Msdial.Model.Service;
using CompMs.App.Msdial.Utility;
using CompMs.Common.Algorithm.Scoring;
using CompMs.Common.Components;
using CompMs.Common.DataObj;
using CompMs.Common.DataObj.Result;
using CompMs.CommonMVVM;
using CompMs.MsdialCore.DataObj;
using CompMs.MsdialCore.MSDec;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;

namespace CompMs.App.Msdial.Model.Search
{
    interface ICompoundSearchModel : INotifyPropertyChanged, IDisposable {
        IReadOnlyList<CompoundSearcher> CompoundSearchers { get; }

        CompoundSearcher SelectedCompoundSearcher { get; set; }
        
        IFileBean File { get; }

        IPeakSpotModel PeakSpot { get; }

        MsSpectrumModel MsSpectrumModel { get; }

        ICompoundResult SelectedCompoundResult { get; set; }

        MoleculeMsReference SelectedReference { get; set; }

        MsScanMatchResult SelectedMatchResult { get; set; }

        CompoundResultCollection Search();

        void SetConfidence();

        void SetUnsettled();

        void SetUnknown();
    }

    internal class CompoundSearchModel : DisposableModelBase, ICompoundSearchModel
    {
        private readonly MSDecResult _msdecResult;
        private readonly SetAnnotationService _setAnnotationService;
        private readonly IPeakSpotModel _peakSpot;
        private readonly PlotComparedMsSpectrumService _plotService;

        public CompoundSearchModel(IFileBean fileBean, IPeakSpotModel peakSpot, MSDecResult msdecResult, IReadOnlyList<CompoundSearcher> compoundSearchers, SetAnnotationService setAnnotationService) {
            File = fileBean ?? throw new ArgumentNullException(nameof(fileBean));
            _peakSpot = peakSpot ?? throw new ArgumentNullException(nameof(peakSpot));
            CompoundSearchers = compoundSearchers;
            _setAnnotationService = setAnnotationService;
            SelectedCompoundSearcher = CompoundSearchers.FirstOrDefault();
            _msdecResult = msdecResult ?? throw new ArgumentNullException(nameof(msdecResult));

            _plotService = new PlotComparedMsSpectrumService(msdecResult).AddTo(Disposables);
            this.ObserveProperty(m => SelectedReference)
                .Subscribe(_plotService.UpdateReference).AddTo(Disposables);
            this.ObserveProperty(m => SelectedCompoundSearcher)
                .SkipNull()
                .Select(s => new Ms2ScanMatching(s.MsRefSearchParameter))
                .Subscribe(_plotService.UpdateMatchingScorer).AddTo(Disposables);
        }

        public IReadOnlyList<CompoundSearcher> CompoundSearchers { get; }

        public CompoundSearcher SelectedCompoundSearcher {
            get => _compoundSearcher;
            set => SetProperty(ref _compoundSearcher, value);
        }
        private CompoundSearcher _compoundSearcher;
        
        public IFileBean File { get; }

        public IPeakSpotModel PeakSpot => _peakSpot;

        public MsSpectrumModel MsSpectrumModel => _plotService.MsSpectrumModel;

        public ICompoundResult SelectedCompoundResult {
            get => _selectedCompoundResult;
            set => SetProperty(ref _selectedCompoundResult, value);
        }
        private ICompoundResult _selectedCompoundResult;

        public MoleculeMsReference SelectedReference { 
            get => _selectedReference;
            set => SetProperty(ref _selectedReference, value);
        }
        private MoleculeMsReference _selectedReference;

        public MsScanMatchResult SelectedMatchResult {
            get => _selectedMatchResult;
            set => SetProperty(ref _selectedMatchResult, value);
        }
        private MsScanMatchResult _selectedMatchResult;

        public virtual CompoundResultCollection Search() {
            return new CompoundResultCollection
            {
                Results = SearchCore().ToList(),
            };
        }

        protected IEnumerable<ICompoundResult> SearchCore() {
            return SelectedCompoundSearcher.Search(
                _peakSpot.MSIon,
                _msdecResult,
                new List<RawPeakElement>(),
                new IonFeatureCharacter { IsotopeWeightNumber = 0, } // Assume this is not isotope.
            );
        }

        public void SetConfidence() {
            _setAnnotationService.SetConfidence(SelectedCompoundResult);
        }

        public void SetUnsettled() {
            _setAnnotationService.SetUnsettled(SelectedCompoundResult);
        }

        public void SetUnknown() {
            _setAnnotationService.SetUnknown();
        }
    }
}
