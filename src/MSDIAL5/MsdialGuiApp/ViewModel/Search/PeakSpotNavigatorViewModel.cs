﻿using CompMs.App.Msdial.Model.Search;
using CompMs.App.Msdial.Utility;
using CompMs.CommonMVVM;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.ComponentModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Windows.Data;

namespace CompMs.App.Msdial.ViewModel.Search
{
    internal sealed class PeakSpotNavigatorViewModel : ViewModelBase
    {
        private readonly PeakSpotNavigatorModel model;

        public PeakSpotNavigatorViewModel(PeakSpotNavigatorModel model) {
            this.model = model;
            SelectedAnnotationLabel = model
                .ToReactivePropertySlimAsSynchronized(m => m.SelectedAnnotationLabel)
                .AddTo(Disposables);
            PeakFilterViewModel = new PeakFilterViewModel(model.PeakFilters.ToArray()).AddTo(Disposables);
            TagSearchBuilderViewModel = new PeakSpotTagSearchQueryBuilderViewModel(model.TagSearchQueryBuilder).AddTo(Disposables);

            AmplitudeLowerValue = model.AmplitudeFilterModel
                .ToReactivePropertyAsSynchronized(m => m.Lower)
                .AddTo(Disposables);
            AmplitudeUpperValue = model.AmplitudeFilterModel
                .ToReactivePropertyAsSynchronized(m => m.Upper)
                .AddTo(Disposables);
            AmplitudeLowerValue.SetValidateNotifyError(v => AmplitudeUpperValue.Value >= v ? null : "Too large");
            AmplitudeUpperValue.SetValidateNotifyError(v => AmplitudeLowerValue.Value <= v ? null : "Too small");
            MzLowerValue = model.MzFilterModel
                .ToReactivePropertyAsSynchronized(m => m.Lower)
                .AddTo(Disposables);
            MzUpperValue = model.MzFilterModel
                .ToReactivePropertyAsSynchronized(m => m.Upper)
                .AddTo(Disposables);
            MzLowerValue.SetValidateNotifyError(v => MzUpperValue.Value >= v ? null : "Too large");
            MzUpperValue.SetValidateNotifyError(v => MzLowerValue.Value <= v ? null : "Too small");
            RtLowerValue = model.RtFilterModel
                .ToReactivePropertyAsSynchronized(m => m.Lower)
                .AddTo(Disposables);
            RtUpperValue = model.RtFilterModel
                .ToReactivePropertyAsSynchronized(m => m.Upper)
                .AddTo(Disposables);
            RtLowerValue.SetValidateNotifyError(v => RtUpperValue.Value >= v ? null : "Too large");
            RtUpperValue.SetValidateNotifyError(v => RtLowerValue.Value <= v ? null : "Too small");
            DtLowerValue = model.DtFilterModel
                .ToReactivePropertyAsSynchronized(m => m.Lower)
                .AddTo(Disposables);
            DtUpperValue = model.DtFilterModel
                .ToReactivePropertyAsSynchronized(m => m.Upper)
                .AddTo(Disposables);
            DtLowerValue.SetValidateNotifyError(v => DtUpperValue.Value >= v ? null : "Too large");
            DtUpperValue.SetValidateNotifyError(v => DtLowerValue.Value <= v ? null : "Too small");

            MetaboliteFilterKeyword = new ReactivePropertySlim<string>(string.Empty).AddTo(Disposables);
            MetaboliteFilterKeyword
                .Where(keywords => !(keywords is null))
                .SelectSwitch(keywords => Observable.FromAsync(token => model.MetaboliteFilterModel.SetKeywordsAsync(keywords.Split(), token)))
                .Subscribe()
                .AddTo(Disposables);
            ProteinFilterKeyword = new ReactivePropertySlim<string>(string.Empty).AddTo(Disposables);
            ProteinFilterKeyword
                .Where(keywords => !(keywords is null))
                .SelectSwitch(keywords => Observable.FromAsync(token => model.ProteinFilterModel.SetKeywordsAsync(keywords.Split(), token)))
                .Subscribe()
                .AddTo(Disposables);
            CommentFilterKeyword = new ReactivePropertySlim<string>(string.Empty).AddTo(Disposables);
            CommentFilterKeyword
                .Where(keywords => keywords != null)
                .SelectSwitch(keywords => Observable.FromAsync(token => model.CommentFilterModel.SetKeywordsAsync(keywords.Split(), token)))
                .Subscribe()
                .AddTo(Disposables);

            OntologyFilterKeyword = new ReactivePropertySlim<string>(string.Empty).AddTo(Disposables);
            OntologyFilterKeyword
                .Where(keywords => keywords != null)
                .SelectSwitch(keywords => Observable.FromAsync(token => model.OntologyFilterModel.SetKeywordsAsync(keywords.Split(), token)))
                .Subscribe()
                .AddTo(Disposables);

            AdductFilterKeyword = new ReactivePropertySlim<string>(string.Empty).AddTo(Disposables);
            AdductFilterKeyword
                .Where(keywords => keywords != null)
                .SelectSwitch(keywords => Observable.FromAsync(token => model.AdductFilterModel.SetKeywordsAsync(keywords.Split(), token)))
                .Subscribe()
                .AddTo(Disposables);

            IsEditting = new ReactivePropertySlim<bool>().AddTo(Disposables);

            PeakSpotsView = CollectionViewSource.GetDefaultView(model.PeakSpots);

            var needRefresh = new[]
            {
                PeakFilterViewModel.CheckedFilter.ToUnit(),
                TagSearchBuilderViewModel.ObserveChanged,
                AmplitudeLowerValue.ToUnit(),
                AmplitudeUpperValue.ToUnit(),
                MzLowerValue.ToUnit(),
                MzUpperValue.ToUnit(),
                RtLowerValue.ToUnit(),
                RtUpperValue.ToUnit(),
                DtLowerValue.ToUnit(),
                DtUpperValue.ToUnit(),
                MetaboliteFilterKeyword.ToUnit(),
                ProteinFilterKeyword.ToUnit(),
                CommentFilterKeyword.ToUnit(),
                OntologyFilterKeyword.ToUnit(),
                AdductFilterKeyword.ToUnit(),
            }.Merge();

            var ifIsEditting = needRefresh.Take(1).Zip(IsEditting.Where(x => !x)).Select(x => x.First);
            var ifIsNotEditting = needRefresh;

            IsEditting
                .SelectSwitch(isEditting => isEditting ? ifIsEditting : ifIsNotEditting)
                .Throttle(TimeSpan.FromMilliseconds(500))
                .ObserveOnUIDispatcher()
                .SelectMany(_ => Observable.Defer(() => {
                    model.RefreshCollectionViews();
                    return Observable.Return(Unit.Default);
                }))
                .OnErrorRetry<Unit, InvalidOperationException>(_ => System.Diagnostics.Debug.WriteLine("Failed to refresh. Retry after 0.1 seconds."), retryCount: 5, delay: TimeSpan.FromSeconds(.1d))
                .Catch<Unit, InvalidOperationException>(e => {
                    System.Diagnostics.Debug.WriteLine("Failed to refresh. CollectionView couldn't be refreshed.");
                    return Observable.Return(Unit.Default);
                })
                .Repeat()
                .Subscribe()
                .AddTo(Disposables);
        }

        public ReactivePropertySlim<string> SelectedAnnotationLabel { get; }

        public ICollectionView PeakSpotsView { get; }

        public ReactivePropertySlim<bool> IsEditting { get; }

        public ReactiveProperty<double> AmplitudeLowerValue { get; }
        public ReactiveProperty<double> AmplitudeUpperValue { get; }
        public ReactiveProperty<double> MzLowerValue { get; }
        public ReactiveProperty<double> MzUpperValue { get; }
        public ReactiveProperty<double> RtLowerValue { get; }
        public ReactiveProperty<double> RtUpperValue { get; }
        public ReactiveProperty<double> DtLowerValue { get; }
        public ReactiveProperty<double> DtUpperValue { get; }

        public ReactivePropertySlim<string> MetaboliteFilterKeyword { get; }
        public ReactivePropertySlim<string> ProteinFilterKeyword { get; }
        public ReactivePropertySlim<string> CommentFilterKeyword { get; }
        public ReactivePropertySlim<string> OntologyFilterKeyword { get; }
        public ReactivePropertySlim<string> AdductFilterKeyword { get; }

        public PeakFilterViewModel PeakFilterViewModel { get; }
        public PeakSpotTagSearchQueryBuilderViewModel TagSearchBuilderViewModel { get; }

        protected override void Dispose(bool disposing) {
            if (disposing) {
                // PeakSpotsView.Filter -= PeakFilter;
            }
            base.Dispose(disposing);
        }

        ~PeakSpotNavigatorViewModel() {
            Dispose(disposing: false);
        }
    }
}
