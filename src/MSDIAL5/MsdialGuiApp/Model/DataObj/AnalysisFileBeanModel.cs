﻿using CompMs.Common.Enum;
using CompMs.CommonMVVM;
using CompMs.MsdialCore.DataObj;
using CompMs.MsdialCore.MSDec;
using CompMs.MsdialCore.Parser;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace CompMs.App.Msdial.Model.DataObj
{
    public sealed class AnalysisFileBeanModel : DisposableModelBase, IFileBean
    {
        private readonly AnalysisFileBean _file;

        internal AnalysisFileBeanModel(AnalysisFileBean file) {
            _file = file;
        }

        public AnalysisFileBean File => _file;

        public AnalysisFileType AnalysisFileType {
            get => _file.AnalysisFileType;
            set {
                if (_file.AnalysisFileType != value) {
                    _file.AnalysisFileType = value;
                    OnPropertyChanged(nameof(AnalysisFileType));
                }
            }
        }
        public string AnalysisFileClass {
            get => _file.AnalysisFileClass;
            set {
                if (_file.AnalysisFileClass != value) {
                    _file.AnalysisFileClass = value;
                    OnPropertyChanged(nameof(AnalysisFileClass));
                }
            }
        }
        public int AnalysisFileAnalyticalOrder {
            get => _file.AnalysisFileAnalyticalOrder;
            set {
                if (_file.AnalysisFileAnalyticalOrder != value) {
                    _file.AnalysisFileAnalyticalOrder = value;
                    OnPropertyChanged(nameof(AnalysisFileAnalyticalOrder));
                }
            }
        }
        public bool AnalysisFileIncluded {
            get => _file.AnalysisFileIncluded;
            set {
                if (_file.AnalysisFileIncluded != value) {
                    _file.AnalysisFileIncluded = value;
                    OnPropertyChanged(nameof(AnalysisFileIncluded));
                }
            }
        }
        public int AnalysisBatch {
            get => _file.AnalysisBatch;
            set {
                if (_file.AnalysisBatch != value) {
                    _file.AnalysisBatch = value;
                    OnPropertyChanged(nameof(AnalysisBatch));
                }
            }
        }

        public double DilutionFactor {
            get => _file.DilutionFactor;
            set {
                if (_file.DilutionFactor != value) {
                    _file.DilutionFactor = value;
                    OnPropertyChanged(nameof(DilutionFactor));
                }
            }
        }

        public double ResponseVariable {
            get => _file.ResponseVariable;
            set {
                if (_file.ResponseVariable != value) {
                    _file.ResponseVariable = value;
                    OnPropertyChanged(nameof(ResponseVariable));
                }
            }
        }

        public string AnalysisFilePath {
            get => _file.AnalysisFilePath;
            set {
                if (_file.AnalysisFilePath != value) {
                    _file.AnalysisFilePath = value;
                    OnPropertyChanged(nameof(AnalysisFilePath));
                }
            }
        }

        public string AnalysisFileName {
            get => _file.AnalysisFileName;
            set {
                if (_file.AnalysisFileName != value) {
                    _file.AnalysisFileName = value;
                    OnPropertyChanged(nameof(AnalysisFileName));
                }
            }
        }

        public int AnalysisFileId {
            get => _file.AnalysisFileId;
            set {
                if (_file.AnalysisFileId != value) {
                    _file.AnalysisFileId = value;
                    OnPropertyChanged(nameof(AnalysisFileId));
                }
            }
        }

        public AcquisitionType AcquisitionType {
            get => _file.AcquisitionType;
            set {
                if (_file.AcquisitionType != value) {
                    _file.AcquisitionType = value;
                    OnPropertyChanged(nameof(AcquisitionType));
                }
            }
        }

        public string PeakAreaBeanInformationFilePath => _file.PeakAreaBeanInformationFilePath;
        [Obsolete("Use MSDecLoader property directly.")]
        public string DeconvolutionFilePath => _file.DeconvolutionFilePath;
        public string ProteinAssembledResultFilePath => _file.ProteinAssembledResultFilePath;

        public MSDecLoader MSDecLoader {
            get => _mSDecLoader ?? (_mSDecLoader = new MSDecLoader(_file.DeconvolutionFilePath).AddTo(Disposables));
        }
        private MSDecLoader _mSDecLoader;

        public void ReleaseMSDecLoader() {
            var loader = _mSDecLoader;
            _mSDecLoader = null;
            loader?.Dispose();
        }

        public Ms1BasedSpectrumFeatureCollection LoadMs1BasedSpectrumFeatureCollection() {
            var collection = _file.LoadSpectrumFeatures();
            return new Ms1BasedSpectrumFeatureCollection(collection);
        }

        public ObservableCollection<ChromatogramPeakFeatureModel> LoadChromatogramPeakFeatureModels() {
            var peaks = _file.LoadChromatogramPeakFeatureCollectionAsync().Result;
            return new ObservableCollection<ChromatogramPeakFeatureModel>(
                peaks.Items.Select(peak => new ChromatogramPeakFeatureModel(peak))
            );
        }

        int IFileBean.FileID => AnalysisFileId;
        string IFileBean.FileName => AnalysisFileName;
        string IFileBean.FilePath => AnalysisFilePath;
    }
}
