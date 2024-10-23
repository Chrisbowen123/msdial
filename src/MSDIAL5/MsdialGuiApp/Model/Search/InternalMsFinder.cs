﻿using CompMs.App.Msdial.Model.Chart;
using CompMs.Common.Components;
using CompMs.CommonMVVM;
using CompMs.Graphics.Design;
using Reactive.Bindings.Extensions;
using System.Windows.Media;

namespace CompMs.App.Msdial.Model.Search
{
    internal sealed class InternalMsFinder : DisposableModelBase {
        public InternalMsFinderMetaboliteList InternalMsFinderMetaboliteList { get; }

        public InternalMsFinder(InternalMsFinderMetaboliteList metaboliteList) {
            InternalMsFinderMetaboliteList = metaboliteList;
            Disposables.Add(metaboliteList);
            var ms1HorizontalAxis = metaboliteList.internalMsFinderMs1.CreateAxisPropertySelectors(new PropertySelector<SpectrumPeak, double>(p => p.Mass), "m/z", "m/z");
            var ms1VerticalAxis = metaboliteList.internalMsFinderMs1.CreateAxisPropertySelectors2(new PropertySelector<SpectrumPeak, double>(p => p.Intensity), "Intensity");
            var ms2HorizontalAxis = metaboliteList.internalMsFinderMs2.CreateAxisPropertySelectors(new PropertySelector<SpectrumPeak, double>(p => p.Mass), "m/z", "m/z");
            var ms2VerticalAxis = metaboliteList.internalMsFinderMs2.CreateAxisPropertySelectors2(new PropertySelector<SpectrumPeak, double>(p => p.Intensity), "Intensity");
            GraphLabels msGraphLabels = new GraphLabels(string.Empty, "m/z", "Abundance", nameof(SpectrumPeak.Mass), nameof(SpectrumPeak.Intensity));
            spectrumModelMs1 = new SingleSpectrumModel(metaboliteList.internalMsFinderMs1, ms1HorizontalAxis, ms1VerticalAxis, new ChartHueItem(string.Empty, new ConstantBrushMapper(Brushes.Black)), msGraphLabels).AddTo(Disposables);
            spectrumModelMs2 = new SingleSpectrumModel(metaboliteList.internalMsFinderMs2, ms2HorizontalAxis, ms2VerticalAxis, new ChartHueItem(string.Empty, new ConstantBrushMapper(Brushes.Black)), msGraphLabels).AddTo(Disposables);
        }

        public SingleSpectrumModel spectrumModelMs1 { get; }
        public SingleSpectrumModel spectrumModelMs2 { get; }
    }

}