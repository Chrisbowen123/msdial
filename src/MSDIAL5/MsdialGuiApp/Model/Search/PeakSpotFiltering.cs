﻿using CompMs.MsdialCore.Algorithm.Annotation;
using CompMs.MsdialCore.DataObj;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace CompMs.App.Msdial.Model.Search
{
    internal class PeakSpotFiltering<T> : IDisposable where T: IFilterable, IAnnotatedObject
    {
        private readonly Dictionary<ICollectionView, AttachedPeakFilters<T>> _viewToFilterMethods = new Dictionary<ICollectionView, AttachedPeakFilters<T>>();
        private readonly Dictionary<ICollectionView, CompositeDisposable> _viewToDisposables = new Dictionary<ICollectionView, CompositeDisposable>();
        private bool _disposedValue;

        public void AttachFilter(ICollectionView view, PeakFilterModel peakFilterModel, PeakSpotTagSearchQueryBuilderModel tagSearchQueryBuilder, IMatchResultEvaluator<T> evaluator) {
            var pred = CreateFilter(peakFilterModel, evaluator, tagSearchQueryBuilder);
            AttachFilterCore(pred.Invoke, view);
        }

        public void AttachFilter(ValueFilterModel filterModel, Func<T, double> convert, ICollectionView view) {
            bool predicate(T filterable) => filterModel.Contains(convert(filterable));
            AttachFilterCore(predicate, view, filterModel.ObserveProperty(m => m.IsEnabled, isPushCurrentValueAtFirst: false), filterModel.IsEnabled);
            if (view.SourceCollection is INotifyCollectionChanged notifyCollection) {
                if (!_viewToDisposables.ContainsKey(view)) {
                    _viewToDisposables[view] = new CompositeDisposable();
                }
                notifyCollection.CollectionChangedAsObservable().ToUnit()
                    .StartWith(Unit.Default)
                    .Throttle(TimeSpan.FromSeconds(.05d))
                    .Subscribe(_ =>
                    {
                        filterModel.Minimum = view.SourceCollection.Cast<T>().DefaultIfEmpty().Min(convert);
                        filterModel.Maximum = view.SourceCollection.Cast<T>().DefaultIfEmpty().Max(convert);
                    }).AddTo(_viewToDisposables[view]);
            }
        }

        public void AttachFilter(KeywordFilterModel filterModel, Func<T, string> convert, ICollectionView view) {
            bool predicate(T filterable) => filterModel.Match(convert(filterable));
            AttachFilterCore(predicate, view, filterModel.ObserveProperty(m => m.IsEnabled, isPushCurrentValueAtFirst: false), filterModel.IsEnabled);
        }

        private void AttachFilterCore(Predicate<T> predicate, ICollectionView view) {
            if (!_viewToFilterMethods.ContainsKey(view)) {
                _viewToFilterMethods[view] = new AttachedPeakFilters<T>(view);
            }
            _viewToFilterMethods[view].Attatch(predicate);
        }

        private void AttachFilterCore(Predicate<T> predicate, ICollectionView view, IObservable<bool> enabled, bool initial) {
            if (!_viewToFilterMethods.ContainsKey(view)) {
                _viewToFilterMethods[view] = new AttachedPeakFilters<T>(view);
            }
            _viewToFilterMethods[view].Attatch(predicate, enabled, initial);
        }

        public bool DetatchFilter(ICollectionView view) {
            if (_viewToFilterMethods.ContainsKey(view)) {
                _viewToFilterMethods[view].Detatch();
                _viewToFilterMethods.Remove(view);
                _viewToDisposables[view].Dispose();
                _viewToDisposables.Remove(view);
                return true;
            }
            return false;
        }

        private Predicate<T> CreateFilter(PeakFilterModel peakFilterModel, IMatchResultEvaluator<T> evaluator, PeakSpotTagSearchQueryBuilderModel tagSearchQueryBuilder) {
            return filterable => peakFilterModel.PeakFilter(filterable, evaluator) && filterable.TagCollection.IsSelected(tagSearchQueryBuilder.CreateQuery());
        }

        private void Dispose(bool disposing) {
            if (!_disposedValue) {
                if (disposing) {
                }
                var views = _viewToFilterMethods.Keys.ToArray();
                foreach (var view in views) {
                    DetatchFilter(view);
                }
                _viewToFilterMethods.Clear();
                _viewToDisposables.Clear();
                _disposedValue = true;
            }
        }

        public void Dispose() {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
