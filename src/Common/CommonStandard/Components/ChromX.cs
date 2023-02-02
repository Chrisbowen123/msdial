﻿using MessagePack;

namespace CompMs.Common.Components
{
    public enum ChromXType { RT, RI, Drift, Mz }
    public enum ChromXUnit { Min, Sec, Msec, Mz, None, K0, OneOverK0 }

    [Union(0, typeof(ChromXs))]
    [Union(1, typeof(RetentionTime))]
    [Union(2, typeof(RetentionIndex))]
    [Union(3, typeof(MzValue))]
    [Union(4, typeof(DriftTime))]
    public interface IChromX {
        double Value { get; set; }
        ChromXType Type { get; set; } 
        ChromXUnit Unit { get; set; }
    }

    [MessagePackObject]
    public sealed class RetentionTime : IChromX
    {
        [Key(0)]
        public double Value { get; set; } = -1;
        [Key(1)]
        public ChromXType Type { get; set; }
        [Key(2)]
        public ChromXUnit Unit { get; set; }

        public RetentionTime() { }
        public RetentionTime(double retentionTime, ChromXUnit unit = ChromXUnit.Min)
        {
            Value = retentionTime;
            Type = ChromXType.RT;
            Unit = unit;
        }

        public override string ToString()
        {
            switch (Type)
            {
                case ChromXType.RT:
                    return $"RT: {Value:F3} {GetUnitString()}";
                case ChromXType.RI:
                    return $"RI: {Value:F3} {GetUnitString()}";
                case ChromXType.Drift:
                    return $"Drift: {Value:F3} {GetUnitString()}";
                case ChromXType.Mz:
                    return $"Mz: {Value:F3} {GetUnitString()}";
                default:
                    return "";
            }
        }
        public string GetUnitString()
        {
            switch (Unit)
            {
                case ChromXUnit.Min:
                    return "min";
                case ChromXUnit.Sec:
                    return "sec";
                case ChromXUnit.None:
                    return "";
                case ChromXUnit.Mz:
                    return "m/z";
                case ChromXUnit.Msec:
                    return "msec";
                default:
                    return "";
            }
        }
    }

    [MessagePackObject]
    public sealed class RetentionIndex : IChromX
    {
        [Key(0)]
        public double Value { get; set; } = -1;
        [Key(1)]
        public ChromXType Type { get; set; }
        [Key(2)]
        public ChromXUnit Unit { get; set; }

        public RetentionIndex() { }
        public RetentionIndex(double retentionIndex, ChromXUnit unit = ChromXUnit.None)
        {
            Value = retentionIndex;
            Type = ChromXType.RI;
            Unit = unit;
        }

        public override string ToString()
        {
            switch (Type)
            {
                case ChromXType.RT:
                    return $"RT: {Value:F3} {GetUnitString()}";
                case ChromXType.RI:
                    return $"RI: {Value:F3} {GetUnitString()}";
                case ChromXType.Drift:
                    return $"Drift: {Value:F3} {GetUnitString()}";
                case ChromXType.Mz:
                    return $"Mz: {Value:F3} {GetUnitString()}";
                default:
                    return "";
            }
        }
        public string GetUnitString()
        {
            switch (Unit)
            {
                case ChromXUnit.Min:
                    return "min";
                case ChromXUnit.Sec:
                    return "sec";
                case ChromXUnit.None:
                    return "";
                case ChromXUnit.Mz:
                    return "m/z";
                case ChromXUnit.Msec:
                    return "msec";
                default:
                    return "";
            }
        }
    }

    [MessagePackObject]
    public sealed class DriftTime : IChromX
    {
        [Key(0)]
        public double Value { get; set; } = -1;
        [Key(1)]
        public ChromXType Type { get; set; }
        [Key(2)]
        public ChromXUnit Unit { get; set; }

        public DriftTime() { }
        public DriftTime(double driftTime, ChromXUnit unit = ChromXUnit.Msec)
        {
            Value = driftTime;
            Type = ChromXType.Drift;
            Unit = unit;
        }

        public override string ToString()
        {
            switch (Type)
            {
                case ChromXType.RT:
                    return $"RT: {Value:F3} {GetUnitString()}";
                case ChromXType.RI:
                    return $"RI: {Value:F3} {GetUnitString()}";
                case ChromXType.Drift:
                    return $"Drift: {Value:F3} {GetUnitString()}";
                case ChromXType.Mz:
                    return $"Mz: {Value:F3} {GetUnitString()}";
                default:
                    return "";
            }
        }
        public string GetUnitString()
        {
            switch (Unit)
            {
                case ChromXUnit.Min:
                    return "min";
                case ChromXUnit.Sec:
                    return "sec";
                case ChromXUnit.None:
                    return "";
                case ChromXUnit.Mz:
                    return "m/z";
                case ChromXUnit.Msec:
                    return "msec";
                default:
                    return "";
            }
        }

    }

    [MessagePackObject]
    public sealed class MzValue : IChromX {
        [Key(0)]
        public double Value { get; set; } = -1;
        [Key(1)]
        public ChromXType Type { get; set; }
        [Key(2)]
        public ChromXUnit Unit { get; set; }

        public MzValue() { }
        public MzValue(double mz, ChromXUnit unit = ChromXUnit.Mz) {
            Value = mz;
            Type = ChromXType.Mz;
            Unit = unit;
        }

        public override string ToString()
        {
            switch (Type)
            {
                case ChromXType.RT:
                    return $"RT: {Value:F3} {GetUnitString()}";
                case ChromXType.RI:
                    return $"RI: {Value:F3} {GetUnitString()}";
                case ChromXType.Drift:
                    return $"Drift: {Value:F3} {GetUnitString()}";
                case ChromXType.Mz:
                    return $"Mz: {Value:F3} {GetUnitString()}";
                default:
                    return "";
            }
        }
        public string GetUnitString()
        {
            switch (Unit)
            {
                case ChromXUnit.Min:
                    return "min";
                case ChromXUnit.Sec:
                    return "sec";
                case ChromXUnit.None:
                    return "";
                case ChromXUnit.Mz:
                    return "m/z";
                case ChromXUnit.Msec:
                    return "msec";
                default:
                    return "";
            }
        }
    }
}
