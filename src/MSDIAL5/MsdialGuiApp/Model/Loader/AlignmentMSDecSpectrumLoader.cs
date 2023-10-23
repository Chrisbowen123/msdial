﻿using CompMs.App.Msdial.Model.DataObj;
using CompMs.Common.Components;
using CompMs.Common.Interfaces;
using CompMs.MsdialCore.MSDec;
using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace CompMs.App.Msdial.Model.Loader
{
    internal sealed class AlignmentMSDecSpectrumLoader : IMsSpectrumLoader<AlignmentSpotPropertyModel> {
        private readonly AlignmentFileBeanModel _alignmentFile;

        public AlignmentMSDecSpectrumLoader(AlignmentFileBeanModel alignmentFile) {
            _alignmentFile = alignmentFile;
        }

        private Task<MSDecResult?> LoadMSDecResultAsync(AlignmentSpotPropertyModel target) {
            if (target is null) {
                throw new ArgumentNullException(nameof(target));
            }
            return _alignmentFile.LoadMSDecResultByIndexAsync(target.MasterAlignmentID);
        }

        IObservable<List<SpectrumPeak>> IMsSpectrumLoader<AlignmentSpotPropertyModel>.LoadSpectrumAsObservable(AlignmentSpotPropertyModel target) {
            return Observable.FromAsync(() => LoadMSDecResultAsync(target)).Select(r => r?.Spectrum ?? new List<SpectrumPeak>(0));
        }

        IObservable<IMSScanProperty> IMsSpectrumLoader<AlignmentSpotPropertyModel>.LoadScanAsObservable(AlignmentSpotPropertyModel target) {
            return Observable.FromAsync(() => LoadMSDecResultAsync(target));
        }
    }
}
