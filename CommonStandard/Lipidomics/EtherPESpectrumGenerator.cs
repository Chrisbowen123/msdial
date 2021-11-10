﻿using CompMs.Common.Components;
using CompMs.Common.DataObj.Property;
using CompMs.Common.Enum;
using CompMs.Common.FormulaGenerator.DataObj;
using CompMs.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CompMs.Common.Lipidomics
{
    public class EtherPESpectrumGenerator : ILipidSpectrumGenerator
    {
        private static readonly double C2H8NO4P = new[]
        {
            MassDiffDictionary.CarbonMass * 2,
            MassDiffDictionary.HydrogenMass * 8,
            MassDiffDictionary.NitrogenMass,
            MassDiffDictionary.OxygenMass * 4,
            MassDiffDictionary.PhosphorusMass,
        }.Sum();

        private static readonly double H3PO4 = new[]
        {
            MassDiffDictionary.HydrogenMass * 3,
            MassDiffDictionary.PhosphorusMass,
            MassDiffDictionary.OxygenMass * 4,
        }.Sum();

        private static readonly double CH2 = new[]
        {
            MassDiffDictionary.HydrogenMass * 2,
            MassDiffDictionary.CarbonMass,
        }.Sum();

        public bool CanGenerate(ILipid lipid, AdductIon adduct) {
            return adduct.AdductIonName == "[M+H]+" && lipid.LipidClass == LbmClass.EtherPE;
        }

        public IMSScanProperty Generate(SubLevelLipid lipid, AdductIon adduct, IMoleculeProperty molecule = null) {
            var spectrum = new List<SpectrumPeak>();
            spectrum.AddRange(GetEtherPESpectrum(lipid));
            spectrum = spectrum.GroupBy(spec => spec, comparer)
                .Select(specs => new SpectrumPeak(specs.First().Mass, specs.First().Intensity, string.Join(", ", specs.Select(spec => spec.Comment))))
                .OrderBy(peak => peak.Mass)
                .ToList();
            return CreateReference(lipid, adduct, spectrum, molecule);
        }

        public IMSScanProperty Generate(SomeAcylChainLipid lipid, AdductIon adduct, IMoleculeProperty molecule = null) {
            throw new NotSupportedException();
        }

        public IMSScanProperty Generate(PositionSpecificAcylChainLipid lipid, AdductIon adduct, IMoleculeProperty molecule = null) {
            var spectrum = new List<SpectrumPeak>();
            spectrum.AddRange(GetEtherPESpectrum(lipid));
            spectrum.AddRange(GetAlkylPositionSpectrum(lipid, lipid.Chains[0]));
            if (lipid.Chains[0] is SpecificAlkylChain alkyl) {
                if (alkyl.DoubleBondPosition.Contains(1)) {
                    spectrum.AddRange(GetEtherPEPSpectrum(lipid, alkyl, lipid.Chains[1]));
                }
                else {
                    spectrum.AddRange(GetEtherPEOSpectrum(lipid, lipid.Chains[0], lipid.Chains[1]));
                }
                spectrum.AddRange(GetAlkylDoubleBondSpectrum(lipid, alkyl));
            }
            if (lipid.Chains[1] is SpecificAcylChain acyl) {
                spectrum.AddRange(GetAcylDoubleBondSpectrum(lipid, acyl));
            }
            spectrum = spectrum.GroupBy(spec => spec, comparer)
                .Select(specs => new SpectrumPeak(specs.First().Mass, specs.First().Intensity, string.Join(", ", specs.Select(spec => spec.Comment))))
                .OrderBy(peak => peak.Mass)
                .ToList();
            return CreateReference(lipid, adduct, spectrum, molecule);
        }

        private MoleculeMsReference CreateReference(ILipid lipid, AdductIon adduct, List<SpectrumPeak> spectrum, IMoleculeProperty molecule) {
            return new MoleculeMsReference
            {
                PrecursorMz = lipid.Mass + adduct.AdductIonAccurateMass,
                IonMode = adduct.IonMode,
                Spectrum = spectrum,
                Name = lipid.Name,
                Formula = molecule?.Formula,
                Ontology = molecule?.Ontology,
                SMILES = molecule?.SMILES,
                InChIKey = molecule?.InChIKey,
                AdductType = adduct,
                CompoundClass = lipid.LipidClass.ToString(),
                Charge = adduct.ChargeNumber,
            };
        }

        private SpectrumPeak[] GetEtherPESpectrum(ILipid lipid) {
            return new[]
            {
                new SpectrumPeak(lipid.Mass + MassDiffDictionary.ProtonMass, 999d, "Precursor") { SpectrumComment = SpectrumComment.precursor },
                new SpectrumPeak(lipid.Mass - C2H8NO4P + MassDiffDictionary.ProtonMass, 999d, "Precursor -C2H8NO4P"),
            };
        }

        private SpectrumPeak[] GetEtherPEPSpectrum(ILipid lipid, IChain alkylChain, IChain acylChain) {
            return new[]
            {
                new SpectrumPeak(alkylChain.Mass - MassDiffDictionary.HydrogenMass + C2H8NO4P + MassDiffDictionary.ProtonMass, 750d, "Sn1Ether+C2H8NO3P"), // Sn1 + O + C2H8NO3P
                new SpectrumPeak(alkylChain.Mass - MassDiffDictionary.HydrogenMass + C2H8NO4P - H3PO4 + MassDiffDictionary.ProtonMass, 750d, "Sn1Ether+C2H8NO3P-H3PO4"),
                new SpectrumPeak(lipid.Mass - C2H8NO4P - alkylChain.Mass + MassDiffDictionary.HydrogenMass + MassDiffDictionary.ProtonMass, 750d, "NL of C2H8NO4P+Sn1"),
                new SpectrumPeak(lipid.Mass - alkylChain.Mass + MassDiffDictionary.HydrogenMass, 750d, $"-{alkylChain}"),
                new SpectrumPeak(lipid.Mass - acylChain.Mass + MassDiffDictionary.HydrogenMass, 750d, $"-{acylChain}"),
                new SpectrumPeak(lipid.Mass - alkylChain.Mass + MassDiffDictionary.HydrogenMass - MassDiffDictionary.OxygenMass, 750d, $"-{alkylChain}-O"),
                new SpectrumPeak(lipid.Mass - acylChain.Mass + MassDiffDictionary.HydrogenMass - MassDiffDictionary.OxygenMass, 750d, $"-{acylChain}-O"),
            };
        }

        private SpectrumPeak[] GetEtherPEOSpectrum(ILipid lipid, IChain alkylChain, IChain acylChain) {
            return new[]
            {
                new SpectrumPeak(acylChain.Mass - MassDiffDictionary.HydrogenMass + MassDiffDictionary.ProtonMass, 750d, "Sn2 acyl"),
                new SpectrumPeak(lipid.Mass - alkylChain.Mass + MassDiffDictionary.HydrogenMass, 750d, $"-{alkylChain}"),
                new SpectrumPeak(lipid.Mass - acylChain.Mass + MassDiffDictionary.HydrogenMass, 750d, $"-{acylChain}"),
                new SpectrumPeak(lipid.Mass - alkylChain.Mass + MassDiffDictionary.HydrogenMass - MassDiffDictionary.OxygenMass, 750d, $"-{alkylChain}-O"),
                new SpectrumPeak(lipid.Mass - acylChain.Mass + MassDiffDictionary.HydrogenMass - MassDiffDictionary.OxygenMass, 750d, $"-{acylChain}-O"),
            };
        }

        private SpectrumPeak[] GetAlkylPositionSpectrum(ILipid lipid, IChain alkylChain) {
            return new[]
            {
                new SpectrumPeak(lipid.Mass - alkylChain.Mass - MassDiffDictionary.OxygenMass - CH2 + MassDiffDictionary.ProtonMass, 500d, "-CH2(Sn1)"),
            };
        }

        private IEnumerable<SpectrumPeak> GetAcylDoubleBondSpectrum(ILipid lipid, SpecificAcylChain acylChain) {
            var chainLoss = lipid.Mass - acylChain.Mass + MassDiffDictionary.ProtonMass;
            var diffs = new double[acylChain.CarbonCount];
            for (int i = 0; i < acylChain.CarbonCount; i++) {
                diffs[i] = CH2;
            }
            diffs[0] += MassDiffDictionary.OxygenMass - MassDiffDictionary.HydrogenMass * 2;
            foreach (var i in acylChain.DoubleBondPosition) {
                diffs[i - 1] -= MassDiffDictionary.HydrogenMass;
                diffs[i] -= MassDiffDictionary.HydrogenMass;
            }
            for (int i = 1; i < acylChain.CarbonCount; i++) {
                diffs[i] += diffs[i - 1];
            }
            return diffs.Take(acylChain.CarbonCount - 1)
                .Select((diff, i) => new SpectrumPeak(chainLoss + diff, 250d, $"{acylChain} C{i + 1}"));
        }

        private IEnumerable<SpectrumPeak> GetAlkylDoubleBondSpectrum(ILipid lipid, SpecificAlkylChain alkylChain) {
            var chainLoss = lipid.Mass - alkylChain.Mass + MassDiffDictionary.ProtonMass;
            var diffs = new double[alkylChain.CarbonCount];
            for (int i = 0; i < alkylChain.CarbonCount; i++) {
                diffs[i] = CH2;
            }
            foreach (var i in alkylChain.DoubleBondPosition) {
                diffs[i - 1] -= MassDiffDictionary.HydrogenMass;
                diffs[i] -= MassDiffDictionary.HydrogenMass;
            }
            for (int i = 1; i < alkylChain.CarbonCount; i++) {
                diffs[i] += diffs[i - 1];
            }
            return diffs.Take(alkylChain.CarbonCount - 1)
                .Select((diff, i) => new SpectrumPeak(chainLoss + diff, 250d, $"{alkylChain} C{i + 1}"));
        }


        private static readonly IEqualityComparer<SpectrumPeak> comparer = new SpectrumComparer();
        class SpectrumComparer : IEqualityComparer<SpectrumPeak>
        {
            private static readonly double EPS = 1e6;
            public bool Equals(SpectrumPeak x, SpectrumPeak y) {
                return Math.Abs(x.Mass - y.Mass) <= EPS;
            }

            public int GetHashCode(SpectrumPeak obj) {
                return Math.Round(obj.Mass, 6).GetHashCode();
            }
        }
    }
}
