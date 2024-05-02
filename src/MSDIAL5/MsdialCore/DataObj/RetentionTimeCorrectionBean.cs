﻿using CompMs.Common.Components;
using CompMs.Common.Extension;
using CompMs.Common.Interfaces;
using CompMs.Common.Utility;
using CompMs.MsdialCore.Algorithm;
using MessagePack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CompMs.MsdialCore.DataObj;

public enum InterpolationMethod { Linear }
public enum ExtrapolationMethodBegin { UserSetting, FirstPoint, LinearExtrapolation }
public enum ExtrapolationMethodEnd { LastPoint, LinearExtrapolation }
public enum RtDiffCalcMethod { SampleMinusSampleAverage, SampleMinusReference }
public enum SampleListCellInfo { Normal, Zero, ManualModified, AutoModified }

[MessagePackObject]
public class RetentionTimeCorrectionBean
{
    [Key(0)]
    public List<double>? OriginalRt { get; set; }
    [Key(1)]
    public List<double>? RtDiff { get; set; }
    [Key(2)]
    public List<double>? PredictedRt { get; set; }
    [Key(3)]
    public List<StandardPair> StandardList { get; set; } = new List<StandardPair>();
    [Key(4)]
    public bool isTarget { get; set; }
    [Key(5)]
    public string RetentionTimeCorrectionResultFilePath { get; set; } = string.Empty; // *.rtc

    [IgnoreMember]
    public bool IsLoaded => OriginalRt is not null && RtDiff is not null && PredictedRt is not null;

    [SerializationConstructor]
    public RetentionTimeCorrectionBean() { }

    public RetentionTimeCorrectionBean(string retentionTimeCorrectionResultFilePath) {
        RetentionTimeCorrectionResultFilePath = retentionTimeCorrectionResultFilePath;
    }

    public void ClearPredicts(bool isSampleLarge = false) {
        OriginalRt = null;
        RtDiff = null;
        PredictedRt = null;

        if (isSampleLarge) {
            this.StandardList = null;
        }
    }

    public void Save() {
        if ( OriginalRt is null || RtDiff is null || PredictedRt is null) {
            throw new InvalidOperationException("Retention time correction result is not loaded.");
        }
        RetentionTimeCorrectionMethod.SaveRetentionCorrectionResult(RetentionTimeCorrectionResultFilePath, OriginalRt, RtDiff, PredictedRt);
    }

    public void Restore() {
        RetentionTimeCorrectionMethod.LoadRetentionCorrectionResult(RetentionTimeCorrectionResultFilePath, out var originalRt, out var rtDiff, out var predictedRt);
        OriginalRt = originalRt;
        RtDiff = rtDiff;
        PredictedRt = predictedRt;
    }
}

[MessagePackObject]
public class StandardPair
{
    [Key(0)]
    public ChromatogramPeakFeature SamplePeakAreaBean { get; set; }
    [Key(1)]
    public MoleculeMsReference Reference { get; set; }
    [Key(2)]
    public List<ChromatogramPeak> Chromatogram { get; set; }

    [IgnoreMember]
    public double RtDiff { get { return (SamplePeakAreaBean.ChromXsTop.Value - Reference.ChromXs.Value); } }
    [System.Diagnostics.Conditional("DEBUG")]
    public void Write() {
        Console.WriteLine("Name: " + Reference.Name + ", mass diff: " + (Math.Abs(SamplePeakAreaBean.PrecursorMz - Reference.PrecursorMz)) +
            " Da (ref: " + Reference.PrecursorMz + ", act: " + SamplePeakAreaBean.PrecursorMz + "), RT diff: " +
            RtDiff + " min (ref: " + Reference.ChromXs.Value + ", act: " + SamplePeakAreaBean.ChromXsTop.Value + ")");
    }
}

[MessagePackObject]
public class RetentionTimeCorrectionParam {

    [Key(0)]
    public bool ExcuteRtCorrection { get; set; } = false;
    [Key(1)]
    public InterpolationMethod InterpolationMethod { get; set; }
    [Key(2)]
    public ExtrapolationMethodBegin ExtrapolationMethodBegin { get; set; }
    [Key(3)]
    public ExtrapolationMethodEnd ExtrapolationMethodEnd { get; set; }
    [Key(4)]
    public double UserSettingIntercept { get; set; } = 0.0;
    [Key(5)]
    public RtDiffCalcMethod RtDiffCalcMethod { get; set; }
    [Key(6)]
    public bool doSmoothing { get; set; }
}

[MessagePackObject]
public class RetentionTimeCorrectionCommon{
    [Key(0)]
    public RetentionTimeCorrectionParam RetentionTimeCorrectionParam { get; set; } = new RetentionTimeCorrectionParam();
    [Key(1)]
    public List<MoleculeMsReference> StandardLibrary { get; set; } = new List<MoleculeMsReference>();

    // to keep manual modification results. foreach(var sample in SampleCellInfoListList){foreach(var std in sample){Console.Write("cell info: " + cell);}}
    [Key(2)]
    public List<List<SampleListCellInfo>> SampleCellInfoListList { get; set; } = new List<List<SampleListCellInfo>>();
}


public sealed class CommonStdData
{
    public MoleculeMsReference Reference { get; set; }
    public List<IReadOnlyList<IChromatogramPeak>> Chromatograms { get; set; } = new List<IReadOnlyList<IChromatogramPeak>>();
    public List<double> PeakHeightList { get; set; } = new List<double>();
    public List<double> PeakAreaList { get; set; } = new List<double>();
    public List<double> PeakWidthList { get; set; } = new List<double>();
    public List<double> MzList { get; set; } = new List<double>();
    public List<double> RetentionTimeList { get; set; } = new List<double>();
    public float AverageRetentionTime { get; set; }
    public int NumHit { get; set; } = 0;

    public CommonStdData(MoleculeMsReference comp) {
        Reference = comp;
    }

    public void SetStandard(StandardPair std) {
        Chromatograms.Add(std.Chromatogram);
        var peak = std.SamplePeakAreaBean.PeakFeature;
        if (peak.ChromXsTop.Value == 0) {
            PeakAreaList.Add(0);
            PeakHeightList.Add(0);
            PeakWidthList.Add(0);
            RetentionTimeList.Add(0);
            MzList.Add(0);
        }
        else {
            PeakAreaList.Add(peak.PeakAreaAboveZero);
            PeakHeightList.Add(peak.PeakHeightTop);
            if (peak.ChromXsRight is not null && peak.ChromXsLeft is not null)
                PeakWidthList.Add(peak.ChromXsRight.Value - peak.ChromXsLeft.Value);
            RetentionTimeList.Add(peak.ChromXsTop.Value);
            MzList.Add(peak.Mass);

            NumHit++;
        }
    }

    public void CalcAverageRetentionTime() {
        double sum = 0;
        if(NumHit > 0) {
            foreach(var rt in RetentionTimeList.Where(x => x > 0)) { sum += rt; }
            AverageRetentionTime = (float)(sum / (float)NumHit);
        }
        else {
            AverageRetentionTime = 0;
        }
    }

    public bool IsSameReference(MoleculeMsReference reference) {
        return Math.Abs(Reference.PrecursorMz - reference.PrecursorMz) <= 1e-6
            && Math.Abs(Reference.ChromXs.RT.Value - reference.ChromXs.RT.Value) <= 1e-2;
    }
}

public class RetentionTimeCorrectionMethod {
    public static void UpdateRtCorrectionBean(RetentionTimeCorrectionBean[] retentionTimeCorrectionBeans, ParallelOptions parallelOptions, RetentionTimeCorrectionParam rtParam, List<CommonStdData> commonStdList) {
        if (rtParam.RtDiffCalcMethod == RtDiffCalcMethod.SampleMinusSampleAverage) {
            Parallel.ForEach(retentionTimeCorrectionBeans, parallelOptions, retentionTimeCorrectionBean => {
                if (retentionTimeCorrectionBean.StandardList != null && retentionTimeCorrectionBean.StandardList.Count > 0) {
                    var (originalRt, rtDiff, predictedRt) = RetentionTimeCorrection.GetRetentionTimeCorrectionBean_SampleMinusAverage(
                        rtParam, retentionTimeCorrectionBean.StandardList, retentionTimeCorrectionBean.OriginalRt.ToArray(), commonStdList);
                    retentionTimeCorrectionBean.OriginalRt = originalRt;
                    retentionTimeCorrectionBean.RtDiff = rtDiff;
                    retentionTimeCorrectionBean.PredictedRt = predictedRt;
                }
            });
        }
        else {
            Parallel.ForEach(retentionTimeCorrectionBeans, parallelOptions, retentionTimeCorrectionBean => {
                if (retentionTimeCorrectionBean.StandardList != null && retentionTimeCorrectionBean.StandardList.Count > 0) {
                    var (originalRt, rtDiff, predictedRt) = RetentionTimeCorrection.GetRetentionTimeCorrectionBean_SampleMinusReference(
                        rtParam, retentionTimeCorrectionBean.StandardList, retentionTimeCorrectionBean.OriginalRt.ToArray());
                    retentionTimeCorrectionBean.OriginalRt = originalRt;
                    retentionTimeCorrectionBean.RtDiff = rtDiff;
                    retentionTimeCorrectionBean.PredictedRt = predictedRt;
                }
            });
        }
    }

    public static void SaveRetentionCorrectionResult(string filepath, List<double> originalRt, List<double> rtDiff, List<double> predictedRt) {
        using var fs = File.Open(filepath, FileMode.Create, FileAccess.ReadWrite);
        fs.Write(BitConverter.GetBytes(originalRt.Count), 0, ByteConvertion.ToByteCount(originalRt.Count));
        for (int i = 0; i < originalRt.Count; i++) {
            fs.Write(BitConverter.GetBytes(originalRt[i]), 0, ByteConvertion.ToByteCount(originalRt[i]));
            if (rtDiff.IsEmptyOrNull()) {
                fs.Write(BitConverter.GetBytes(originalRt[i]), 0, ByteConvertion.ToByteCount(originalRt[i]));
            }
            else {
                fs.Write(BitConverter.GetBytes(rtDiff[i]), 0, ByteConvertion.ToByteCount(rtDiff[i]));
            }
            if (predictedRt.IsEmptyOrNull()) {
                fs.Write(BitConverter.GetBytes(originalRt[i]), 0, ByteConvertion.ToByteCount(originalRt[i]));
            }
            else {
                fs.Write(BitConverter.GetBytes(predictedRt[i]), 0, ByteConvertion.ToByteCount(predictedRt[i]));
            }
        }
    }

    public static void LoadRetentionCorrectionResult(string filepath, out List<double> originalRt, out List<double> rtDiff, out List<double> predictedRt) {
        
        if (!File.Exists(filepath)) {
            originalRt = null;
            rtDiff = null;
            predictedRt = null;
            return;
        }

        originalRt = new List<double>();
        rtDiff = new List<double>();
        predictedRt = new List<double>();   

        using (var fs = File.Open(filepath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
            var buffer = new byte[4];
            fs.Read(buffer, 0, 4);
            var count = BitConverter.ToInt32(buffer, 0);

            for (int i = 0; i < count; i++) {
                buffer = new byte[24];
                fs.Read(buffer, 0, 24);

                originalRt.Add(BitConverter.ToDouble(buffer, 0));
                rtDiff.Add(BitConverter.ToDouble(buffer, 8));
                predictedRt.Add(BitConverter.ToDouble(buffer, 16));
            }
        }
    }

    public static List<CommonStdData> MakeCommonStdList(RetentionTimeCorrectionBean[] retentionTimeCorrectionBeans, List<MoleculeMsReference> iStdList) {
        var commonStdList = new List<CommonStdData>();
        var tmpStdList = iStdList.Where(x => x.IsTargetMolecule).OrderBy(x => x.ChromXs.RT.Value);
        foreach (var std in tmpStdList) {
            commonStdList.Add(new CommonStdData(std));
        }
        for (var i = 0; i < retentionTimeCorrectionBeans.Length; i++) {
            for (var j = 0; j < commonStdList.Count; j++) {
                if (j < retentionTimeCorrectionBeans[i].StandardList.Count) {
                    commonStdList[j].SetStandard(retentionTimeCorrectionBeans[i].StandardList[j]);
                }
            }
        }
        foreach (var d in commonStdList) {
            d.CalcAverageRetentionTime();
        }
        return commonStdList;
    }
}
