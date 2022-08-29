﻿using CompMs.App.Msdial.Model.DataObj;
using CompMs.App.Msdial.Model.Lcms;
using CompMs.Common.Mathematics.Statistics;
using CompMs.CommonMVVM;
using CompMs.Graphics.Chart;
using CompMs.MsdialCore.Algorithm.Annotation;
using CompMs.MsdialLcmsApi.Parameter;
using System;
using System.Collections.ObjectModel;
using System.Data.Common;
using System.Drawing.Text;
using System.Linq;

namespace CompMs.App.Msdial.Model.Setting {
    public class PcaSettingModel : BindableBase {
        public int MaxPcNumber {
            get => maxPcNumber;
            set => SetProperty(ref maxPcNumber, value);
        }
        private int maxPcNumber;
        public bool IsIdentifiedImportedInStatistics
        {
            get => isIdentifiedImportedInStatistics;
            set => SetProperty(ref isIdentifiedImportedInStatistics, value);
        }
        private bool isIdentifiedImportedInStatistics;

        public bool IsAnnotatedImportedInStatistics
        {
            get => isAnnotatedImportedInStatistics;
            set => SetProperty(ref isAnnotatedImportedInStatistics, value);
        }
        private bool isAnnotatedImportedInStatistics;

        public bool IsUnknownImportedInStatistics
        {
            get => isUnknownImportedInStatistics;
            set => SetProperty(ref isUnknownImportedInStatistics, value);
        }
        private bool isUnknownImportedInStatistics;

        private readonly MsdialLcmsParameter _parameter;
        private readonly ObservableCollection<AlignmentSpotPropertyModel> _spotprops;
        private readonly FacadeMatchResultEvaluator _evaluator;
        public PcaSettingModel(MsdialLcmsParameter parameter, ObservableCollection<AlignmentSpotPropertyModel> spotprops, FacadeMatchResultEvaluator evaluator) {
            _parameter = parameter;
            _spotprops = spotprops;
            _evaluator = evaluator;
        }

        public void RunPca()
        {

            var statObj = new StatisticsObject()
            {
                //XDataMatrix = new double[_spotprops.Count, _parameter.FileID_AnalysisFileType.Keys.Count],
                XScaled = new double[_parameter.FileID_AnalysisFileType.Keys.Count, _spotprops.Count],
            };

            //private Dictionary<int, string> ColumnIndex_MetaboliteName { get; set; } = null;

            int counterSample = 0;
            int counterMetabolite = 0;

            var metaboliteIDs = new ObservableCollection<int>();

            foreach (var spot in _spotprops) {
                if (isIdentifiedImportedInStatistics && _evaluator.IsReferenceMatched(spot.ScanMatchResult)) {
                    metaboliteIDs.Add(spot.MasterAlignmentID);
                }
                if (isAnnotatedImportedInStatistics && _evaluator.IsAnnotationSuggested(spot.ScanMatchResult)) {
                    metaboliteIDs.Add(spot.MasterAlignmentID);
                }
                if (isUnknownImportedInStatistics && !_evaluator.IsReferenceMatched(spot.ScanMatchResult) && !_evaluator.IsAnnotationSuggested(spot.ScanMatchResult)) {
                    metaboliteIDs.Add(spot.MasterAlignmentID);
                }
            }

            for (int i = 0; i < _parameter.FileID_AnalysisFileType.Keys.Count; i++) {
                counterMetabolite = 0;
                for (int j = 0; j < _spotprops.Count; j++) {
                    if (!metaboliteIDs.Contains(_spotprops[j].MasterAlignmentID)) continue;
                    var alignProp = _spotprops[j].AlignedPeakProperties;
                    statObj.XScaled[counterSample, counterMetabolite] = alignProp[i].NormalizedPeakHeight;
                    counterMetabolite++;
                }
                counterSample++;
            }

            //for (int i = 0; i < _spotprops.Count; i++) {
            //    var alignProp = _spotprops[i].AlignedPeakProperties;
            //    counterMetabolite = 0;
            //    for (int j = 0; j < _parameter.FileID_AnalysisFileType.Keys.Count; j++) {
            //        //counterMetabolite = 0;
            //        //Console.WriteLine("CounterSample" + counterSample);
            //        //Console.WriteLine("CounterMetabolite" + counterMetabolite);
            //        statObj.XDataMatrix[counterSample, counterMetabolite] = alignProp[j].NormalizedPeakHeight;
            //        //counterSample++;
            //    }                
            //    counterSample++;
            //}

            //foreach (var spot in _spotprops) {
            //    foreach(var peak in spot.AlignedPeakProperties) {
            //        statObj.XDataMatrix[peak.FileID, spot.MasterAlignmentID] = peak.NormalizedPeakHeight;
            //    }
            //}

            var foo = StatisticsMathematics.PrincipalComponentAnalysis(statObj, MultivariateAnalysisOption.Pca, maxPcNumber);
        }
    }
}
