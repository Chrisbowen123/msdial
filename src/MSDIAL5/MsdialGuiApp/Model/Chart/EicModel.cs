﻿using CompMs.App.Msdial.Model.DataObj;
using CompMs.App.Msdial.Model.Loader;
using CompMs.App.Msdial.Utility;
using CompMs.CommonMVVM;
using CompMs.Graphics.Core.Base;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Linq;
using System.Reactive.Linq;

namespace CompMs.App.Msdial.Model.Chart
{
    public sealed class EicModel : DisposableModelBase
    {
        private EicModel(IReadOnlyReactiveProperty<PeakChromatogram> chromatogram_, ReadOnlyReactivePropertySlim<bool> itemLoaded, string graphTitle, string horizontalTitle, string verticalTitle) {
            GraphTitle = graphTitle;
            HorizontalTitle = horizontalTitle;
            VerticalTitle = verticalTitle;
            HorizontalProperty = nameof(PeakItem.Time);
            VerticalProperty = nameof(PeakItem.Intensity);

            Chromatogram = chromatogram_;
            ItemLoaded = itemLoaded;
            ChromRangeSource = chromatogram_.Select(chromatogram => chromatogram?.GetTimeRange() ?? new Range(0d, 1d))
                .ToReadOnlyReactivePropertySlim()
                .AddTo(Disposables);
            AbundanceRangeSource = chromatogram_.Select(chromatogram => chromatogram?.GetAbundanceRange() ?? new Range(0d, 1d))
                .ToReadOnlyReactivePropertySlim()
                .AddTo(Disposables);
            chromatogram_.Subscribe(chromatogram => GraphTitle = chromatogram?.Description ?? string.Empty).AddTo(Disposables);
        }

        public EicModel(IObservable<ChromatogramPeakFeatureModel> targetSource, IChromatogramLoader<ChromatogramPeakFeatureModel> loader, string graphTitle, string horizontalTitle, string verticalTitle) {

            GraphTitle = graphTitle;
            HorizontalTitle = horizontalTitle;
            VerticalTitle = verticalTitle;

            HorizontalProperty = nameof(PeakItem.Time);
            VerticalProperty = nameof(PeakItem.Intensity);

            var sources = targetSource.SelectSwitch(t => Observable.FromAsync(token => loader.LoadChromatogramAsync(t, token)));
            var chromatogram_ = sources
                .ToReactiveProperty()
                .AddTo(Disposables);
            Chromatogram = chromatogram_;

            ItemLoaded = new[]
                {
                    targetSource.ToConstant(false),
                    chromatogram_.Delay(TimeSpan.FromSeconds(.05d)).ToConstant(true),
                }.Merge()
                .Throttle(TimeSpan.FromSeconds(.1d))
                .ToReadOnlyReactivePropertySlim()
                .AddTo(Disposables);

            ChromRangeSource = Chromatogram.Select(chromatogram => chromatogram?.GetTimeRange() ?? new Range(0d, 1d))
                .ToReadOnlyReactivePropertySlim()
                .AddTo(Disposables);
            AbundanceRangeSource = Chromatogram.Select(chromatogram => chromatogram?.GetAbundanceRange() ?? new Range(0d, 1d))
                .ToReadOnlyReactivePropertySlim()
                .AddTo(Disposables);

            Chromatogram.Subscribe(chromatogram => GraphTitle = chromatogram?.Description ?? string.Empty).AddTo(Disposables);
        }

        public EicModel(IObservable<ChromatogramPeakFeatureModel> targetSource, IChromatogramLoader<ChromatogramPeakFeatureModel> loader)
            : this(targetSource, loader, string.Empty, string.Empty, string.Empty) {

        }

        public ReadOnlyReactivePropertySlim<bool> ItemLoaded { get; }

        public IReadOnlyReactiveProperty<PeakChromatogram> Chromatogram { get; }

        public IObservable<Range> ChromRangeSource { get; }
        public IObservable<Range> AbundanceRangeSource { get; }

        public string HorizontalTitle {
            get => horizontalTitle;
            set => SetProperty(ref horizontalTitle, value);
        }
        private string horizontalTitle;

        public string VerticalTitle {
            get => verticalTitle;
            set => SetProperty(ref verticalTitle, value);
        }
        private string verticalTitle;

        public string GraphTitle {
            get => graphTitle;
            set => SetProperty(ref graphTitle, value);
        }
        private string graphTitle;

        public string HorizontalProperty {
            get => horizontalProperty;
            set => SetProperty(ref horizontalProperty, value);
        }
        private string horizontalProperty;

        public string VerticalProperty {
            get => verticalProperty;
            set => SetProperty(ref verticalProperty, value);
        }
        private string verticalProperty;

        public static EicModel Create<T>(IObservable<T> targetSource, IChromatogramLoader<T> loader, string graphTitle, string horizontalTitle, string verticalTitle) {
            var source = targetSource.SelectSwitch(t => Observable.FromAsync(token => loader.LoadChromatogramAsync(t, token)).Select(c => (c, true)).StartWith((null, false))).Publish();
            var chromatogram = source.Select(p => p.c).ToReactiveProperty();
            var itemLoaded = source.Select(p => p.Item2).ToReadOnlyReactivePropertySlim();
            var result = new EicModel(chromatogram, itemLoaded, graphTitle, horizontalTitle, verticalTitle);
            result.Disposables.Add(source.Connect());
            result.Disposables.Add(chromatogram);
            result.Disposables.Add(itemLoaded);
            return result;
        }
    }
}
