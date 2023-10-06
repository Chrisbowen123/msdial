﻿using CompMs.App.Msdial.Model.DataObj;
using CompMs.App.Msdial.Model.Loader;
using CompMs.App.Msdial.Model.Search;
using CompMs.App.Msdial.Model.Setting;
using CompMs.Graphics.Base;
using CompMs.MsdialCore.DataObj;
using Reactive.Bindings;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace CompMs.App.Msdial.Model.Table
{
    internal abstract class AlignmentSpotTableModelBase : PeakSpotTableModelBase<AlignmentSpotPropertyModel>
    {
        private readonly IReactiveProperty<AlignmentSpotPropertyModel> _target;
        private readonly PeakSpotFiltering<AlignmentSpotPropertyModel>.PeakSpotFilter _peakSpotFilter;
        private readonly AlignmentSpotSpectraLoader _spectraLoader;

        public AlignmentSpotTableModelBase(
            IReadOnlyList<AlignmentSpotPropertyModel> spots,
            IReactiveProperty<AlignmentSpotPropertyModel> target,
            IObservable<IBrushMapper<BarItem>> classBrush,
            FileClassPropertiesModel classProperties,
            IObservable<IBarItemsLoader> barItemsLoader,
            PeakSpotFiltering<AlignmentSpotPropertyModel>.PeakSpotFilter peakSpotFilter,
            AlignmentSpotSpectraLoader spectraLoader)
            : base(spots, target) {
            _target = target;
            ClassBrush = classBrush;
            BarItemsLoader = barItemsLoader;
            _peakSpotFilter = peakSpotFilter;
            _spectraLoader = spectraLoader;
            FileClassProperties = classProperties;
        }

        public IObservable<IBrushMapper<BarItem>> ClassBrush { get; }
        public IObservable<IBarItemsLoader> BarItemsLoader { get; }
        public FileClassPropertiesModel FileClassProperties { get; }

        public void MarkAllAsConfirmed() {
            foreach (var peak in PeakSpots) {
                peak.Confirmed = true;
            }
        }

        public void SwitchTag(PeakSpotTag tag) {
            if (_target.Value is null) {
                return;
            }
            _target.Value.SwitchPeakSpotTag(tag);
        }
    }
}
