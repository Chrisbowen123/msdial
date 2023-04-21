﻿using CompMs.App.Msdial.Model.Chart;
using CompMs.App.Msdial.Model.DataObj;
using CompMs.CommonMVVM;
using CompMs.Graphics.Base;
using CompMs.Graphics.Core.Base;
using Reactive.Bindings;
using System.Collections.ObjectModel;

namespace CompMs.App.Msdial.ViewModel.Chart
{
    internal sealed class SpectrumFeaturePlotViewModel : ViewModelBase
    {
        private readonly SpectrumFeaturePlotModel _model;

        public SpectrumFeaturePlotViewModel(SpectrumFeaturePlotModel model) {
            _model = model;
        }

        public ReactivePropertySlim<object> SelectedSpectrum => _model.SelectedSpectrumWrapper;
        public ReadOnlyObservableCollection<object> Spectra => _model.SpectraWrapper;
        public ReactivePropertySlim<ChromatogramPeakFeatureModel> SelectedChromatogramPeak => _model.SelectedChromatogramPeak;
        public ReadOnlyObservableCollection<ChromatogramPeakFeatureModel> ChromatogramPeaks => _model.ChromatogramPeaks;

        public BrushMapDataSelector<ChromatogramPeakFeatureModel> BrushMapDataSelector => _model.BrushMapDataSelector;
        public string HorizontalProperty { get; } = nameof(ChromatogramPeakFeatureModel.ChromXValue);
        public string VerticalProperty { get; } = nameof(ChromatogramPeakFeatureModel.Mass);
        public string LabelProperty { get; } = nameof(ChromatogramPeakFeatureModel.MasterPeakID);
        public IAxisManager HorizontalAxis => _model.HorizontalAxis;
        public IAxisManager VerticalAxis => _model.VerticalAxis;
        public IReadOnlyReactiveProperty<string> HorizontalLabel => _model.HorizontalLabel;
        public IReadOnlyReactiveProperty<string> VerticalLabel => _model.VerticalLabel;
        public IReadOnlyReactiveProperty<string> Title => _model.Title;
    }
}
