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
    public class SMOadSpectrumGenerator : ILipidSpectrumGenerator
    {
        private static readonly double C5H14NO4P = new[] {
            MassDiffDictionary.CarbonMass * 5,
            MassDiffDictionary.HydrogenMass * 14,
            MassDiffDictionary.NitrogenMass,
            MassDiffDictionary.OxygenMass * 4,
            MassDiffDictionary.PhosphorusMass,
        }.Sum();

        private static readonly double H2O = new[]
        {
            MassDiffDictionary.HydrogenMass * 2,
            MassDiffDictionary.OxygenMass,
        }.Sum();

        private static readonly double CH3 = new[]
{
            MassDiffDictionary.HydrogenMass * 3,
            MassDiffDictionary.CarbonMass,
        }.Sum();
        private static readonly double C3H9N = new[] {
            MassDiffDictionary.CarbonMass * 3,
            MassDiffDictionary.HydrogenMass * 9,
            MassDiffDictionary.NitrogenMass,
        }.Sum();
        private static readonly double CH4O2 = new[]
        {
            MassDiffDictionary.CarbonMass * 1,
            MassDiffDictionary.HydrogenMass * 4,
            MassDiffDictionary.OxygenMass *2,
        }.Sum();
        private static readonly double C2H3NO = new[]
        {
            MassDiffDictionary.CarbonMass * 2,
            MassDiffDictionary.HydrogenMass * 3,
            MassDiffDictionary.NitrogenMass *1,
            MassDiffDictionary.OxygenMass *1,
        }.Sum();
        private static readonly double C2H2N = new[]
        {
            MassDiffDictionary.CarbonMass * 2,
            MassDiffDictionary.HydrogenMass * 2,
            MassDiffDictionary.NitrogenMass *1,
        }.Sum();

        private static readonly double Electron = 0.00054858026;

        private readonly ISpectrumPeakGenerator spectrumGenerator;
        public SMOadSpectrumGenerator()
        {
            spectrumGenerator = new OadSpectrumPeakGenerator();
        }

        public SMOadSpectrumGenerator(ISpectrumPeakGenerator spectrumGenerator)
        {
            this.spectrumGenerator = spectrumGenerator ?? throw new ArgumentNullException(nameof(spectrumGenerator));
        }

        public bool CanGenerate(ILipid lipid, AdductIon adduct)
        {
            if (adduct.AdductIonName == "[M+H]+" //||
                //adduct.AdductIonName == "[M+Na]+" ||
                //adduct.AdductIonName == "[M+HCOO]-" ||
                //adduct.AdductIonName == "[M+CH3COO]-"
                )
            {
                return true;
            }
            return false;
        }

        public IMSScanProperty Generate(Lipid lipid, AdductIon adduct, IMoleculeProperty molecule = null)
        {
            var abundance = 40.0;
            var nlMass = 0.0;
            var spectrum = new List<SpectrumPeak>();
            spectrum.AddRange(GetSMOadSpectrum(lipid, adduct));

            if (lipid.Chains is PositionLevelChains plChains)
            {
                if (plChains.Chains[0] is SphingoChain sphingo)
                {
                    //spectrum.AddRange(GetSphingoSpectrum(lipid, sphingo, adduct));
                    spectrum.AddRange(spectrumGenerator.GetSphingoDoubleBondSpectrum(lipid, sphingo, adduct, nlMass, 30d));
                }
                if (plChains.Chains[1] is AcylChain acyl)
                {
                    //spectrum.AddRange(GetAcylSpectrum(lipid, acyl, adduct));
                    spectrum.AddRange(spectrumGenerator.GetAcylDoubleBondSpectrum(lipid, acyl, adduct, nlMass, 30d));
                }
            }
            spectrum = spectrum.GroupBy(spec => spec, comparer)
                .Select(specs => new SpectrumPeak(specs.First().Mass, specs.Sum(n => n.Intensity), string.Join(", ", specs.Select(spec => spec.Comment)), specs.Aggregate(SpectrumComment.none, (a, b) => a | b.SpectrumComment)))
                .OrderBy(peak => peak.Mass)
                .ToList();
            return CreateReference(lipid, adduct, spectrum, molecule);
        }

        private SpectrumPeak[] GetSMOadSpectrum(Lipid lipid, AdductIon adduct)
        {
            var spectrum = new List<SpectrumPeak>();

            if (adduct.AdductIonName == "[M+H]+")
            {
                spectrum.AddRange
                (
                    new[] {
                        new SpectrumPeak(adduct.ConvertToMz(lipid.Mass), 999d, "Precursor") { SpectrumComment = SpectrumComment.precursor },
                        new SpectrumPeak(adduct.ConvertToMz(C5H14NO4P), 100d, "Header") { SpectrumComment = SpectrumComment.metaboliteclass, IsAbsolutelyRequiredFragmentForAnnotation = true },
                    }
                );
            }
            //else if (adduct.AdductIonName == "[M+HCOO]-" || adduct.AdductIonName == "[M+CH3COO]-")
            //{
            //    spectrum.AddRange
            //    (
            //        new[] {
            //            new SpectrumPeak(adduct.ConvertToMz(lipid.Mass), 999d, "Precursor") { SpectrumComment = SpectrumComment.precursor },
            //            new SpectrumPeak(lipid.Mass-CH3, 100d, "[M-CH3]-") { SpectrumComment = SpectrumComment.metaboliteclass, },
            //        }
            //    );
            //    if (lipid.Chains is SeparatedChains Chains)
            //    {
            //        foreach (AcylChain chain in Chains.Chains)
            //        {
            //            spectrum.AddRange
            //            (
            //                new[]
            //                {
            //                    new SpectrumPeak(chain.Mass+MassDiffDictionary.OxygenMass+Electron, 30d, $"-{chain}") { SpectrumComment = SpectrumComment.acylchain },
            //                }
            //            );
            //        }
            //    }
            //}
            else
            {
                spectrum.AddRange
                (
                    new[] {
                        new SpectrumPeak(adduct.ConvertToMz(lipid.Mass), 999d, "Precursor") { SpectrumComment = SpectrumComment.precursor },
                    }
                );
            }
            return spectrum.ToArray();
        }
        //private SpectrumPeak[] GetSphingoSpectrum(ILipid lipid, SphingoChain sphingo, AdductIon adduct)
        //{
        //    var chainMass = sphingo.Mass + MassDiffDictionary.HydrogenMass;
        //    var spectrum = new List<SpectrumPeak>();
        //    if (adduct.AdductIonName == "[M+H]+")
        //    {
        //        spectrum.AddRange
        //        (
        //             new[]
        //             {
        //                new SpectrumPeak(chainMass + MassDiffDictionary.ProtonMass - H2O*2,100d, "[sph+H]+ -Header -H2O") { SpectrumComment = SpectrumComment.acylchain },
        //                //new SpectrumPeak(chainMass + MassDiffDictionary.ProtonMass - CH4O2, 100d, "[sph+H]+ -CH4O2"),
        //             }
        //        );
        //    }
        //    return spectrum.ToArray();
        //}

        //private SpectrumPeak[] GetAcylSpectrum(ILipid lipid, AcylChain acyl, AdductIon adduct)
        //{
        //    var chainMass = acyl.Mass + MassDiffDictionary.HydrogenMass;
        //    var spectrum = new List<SpectrumPeak>()
        //    {
        //        new SpectrumPeak(adduct.ConvertToMz(chainMass) +C2H2N , 200d, "[FAA+C2H+adduct]+") { SpectrumComment = SpectrumComment.acylchain },
        //        new SpectrumPeak(adduct.ConvertToMz(chainMass) +C5H14NO4P+C2H2N -MassDiffDictionary.HydrogenMass, 200d, "[FAA+C2H+Header+adduct]+") { SpectrumComment = SpectrumComment.acylchain },
        //    };
        //    return spectrum.ToArray();
        //}


        private MoleculeMsReference CreateReference(ILipid lipid, AdductIon adduct, List<SpectrumPeak> spectrum, IMoleculeProperty molecule)
        {
            return new MoleculeMsReference
            {
                PrecursorMz = adduct.ConvertToMz(lipid.Mass),
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

        private static readonly IEqualityComparer<SpectrumPeak> comparer = new SpectrumEqualityComparer();

    }
}

