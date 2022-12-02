﻿using CompMs.App.Msdial.Model.DataObj;
using CompMs.Common.DataObj;
using CompMs.Common.Extension;
using CompMs.CommonMVVM;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;

namespace CompMs.App.Msdial.Model.Imaging
{
    internal sealed class ImagingRoiModel : DisposableModelBase
    {
        public ImagingRoiModel(string id, RoiModel roi, RawSpectraOnPixels rawSpectraOnPixels, IEnumerable<ChromatogramPeakFeatureModel> peaks, ReactiveProperty<ChromatogramPeakFeatureModel> selectedPeak) {
            Id = id;
            Roi = roi ?? throw new ArgumentNullException(nameof(roi));
            RoiPeakSummaries = new ObservableCollection<RoiPeakSummaryModel>(
                rawSpectraOnPixels.PixelPeakFeaturesList.Zip(peaks, (pixelFeaturs, peak) => new RoiPeakSummaryModel(roi, pixelFeaturs, peak)));
            RoiPeakSummaries.Select(m => selectedPeak.Where(p => m.Peak == p).Select(_ => m)).Merge().Subscribe(m => SelectedRoiPeakSummary = m).AddTo(Disposables);
        }

        public string Id { get; }
        public RoiModel Roi { get; }
        public ObservableCollection<RoiPeakSummaryModel> RoiPeakSummaries { get; }
        public RoiPeakSummaryModel SelectedRoiPeakSummary {
            get => _selectedRoiPeakSummary;
            set => SetProperty(ref _selectedRoiPeakSummary, value);
        }
        private RoiPeakSummaryModel _selectedRoiPeakSummary;
    }
}
