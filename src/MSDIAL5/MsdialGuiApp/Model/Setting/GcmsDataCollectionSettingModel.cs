﻿using CompMs.Common.Enum;
using CompMs.CommonMVVM;
using CompMs.MsdialCore.Parameter;

namespace CompMs.App.Msdial.Model.Setting
{
    public sealed class GcmsDataCollectionSettingModel : BindableBase, IDataCollectionSettingModel
    {
        private readonly ParameterBase _parameter;

        public GcmsDataCollectionSettingModel(ParameterBase parameter, ProcessOption process) {
            _parameter = parameter;
            IsReadOnly = (process & ProcessOption.PeakSpotting) == 0;

            MassRange = new Ms1CollectionRangeSetting(parameter.PeakPickBaseParam, needAccmulation: false);
            RtRange = new RetentionTimeCollectionRangeSetting(parameter.PeakPickBaseParam, needAccmulation: false);
            NumberOfThreads = parameter.ProcessBaseParam.NumThreads;
        }

        public bool IsReadOnly { get; }

        public Ms1CollectionRangeSetting MassRange {
            get => _massRange;
            private set => SetProperty(ref _massRange, value);
        }
        private Ms1CollectionRangeSetting _massRange;

        public RetentionTimeCollectionRangeSetting RtRange {
            get => _rtRange;
            private set => SetProperty(ref _rtRange, value);
        }
        private RetentionTimeCollectionRangeSetting _rtRange;

        public int NumberOfThreads {
            get => _numberOfThreads;
            set => SetProperty(ref _numberOfThreads, value);
        }
        private int _numberOfThreads;

        public bool TryCommit() {
            if (IsReadOnly) {
                return false;
            }
            _parameter.ProcessBaseParam.NumThreads = NumberOfThreads;
            MassRange.Commit();
            RtRange.Commit();
            return true;
        }

        public void LoadParameter(ParameterBase parameter) {
            if (IsReadOnly) {
                return;
            }

            MassRange.Update(parameter);
            RtRange.Update(parameter);
            NumberOfThreads = parameter.ProcessBaseParam.NumThreads;
        }
    }
}
