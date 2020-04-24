﻿using System;
using System.Collections.Generic;
using System.Text;
using CompMs.Common.Enum;
using CompMs.Common.Interfaces;

namespace CompMs.Common.Components
{
    public class MSScanProperty: IMSScanProperty
    {
        public int ID { get; set; }
        public double PrecursorMz { get; set; }
        public IonMode IonMode { get; set; }
        public Times Times { get; set; }
        public List<SpectrumPeak> Spectrum { get; set; } = new List<SpectrumPeak>();
        
        public MSScanProperty() { }
        public MSScanProperty(int id, double precursorMz, Time time, IonMode ionmode)
        {
            ID = id;
            PrecursorMz = precursorMz;
            Times = new Times(time);
            IonMode = ionmode;
        }

        public void AddPeak(double mass, double intensity, string comment = null)
        {
            Spectrum.Add(new SpectrumPeak(mass, intensity, comment));
        }
    }
}
