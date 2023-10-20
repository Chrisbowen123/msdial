﻿using CompMs.MsdialCore.DataObj;
using CompMs.MsdialCore.MSDec;
using System.IO;

namespace CompMs.MsdialCore.Export
{
    public sealed class AlignmentMgfExporter : IAlignmentSpectraExporter
    {
        public void Export(Stream stream, AlignmentSpotProperty spot, MSDecResult msdecResult) {
            SpectraExport.SaveSpectraTableAsMgfFormat(stream, spot, msdecResult.Spectrum);
        }
    }
}
