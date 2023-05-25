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
    public class Hex2CerSpectrumGenerator : ILipidSpectrumGenerator
    {
        private static readonly double H2O = new[]
        {
            MassDiffDictionary.HydrogenMass * 2,
            MassDiffDictionary.OxygenMass,
        }.Sum();
        private static readonly double C2H5NO = new[]
        {
            MassDiffDictionary.CarbonMass * 2,
            MassDiffDictionary.HydrogenMass * 5,
            MassDiffDictionary.NitrogenMass *1,
            MassDiffDictionary.OxygenMass *1,
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
        private static readonly double NH = new[]
        {
            MassDiffDictionary.NitrogenMass * 1,
            MassDiffDictionary.HydrogenMass * 1,
        }.Sum();
        private static readonly double C6H10O5 = new[]
        {
            MassDiffDictionary.CarbonMass * 6,
            MassDiffDictionary.HydrogenMass * 10,
            MassDiffDictionary.OxygenMass *5,
        }.Sum();
        private static readonly double C5H10O4 = new[]
        {
            MassDiffDictionary.CarbonMass * 5,
            MassDiffDictionary.HydrogenMass * 10,
            MassDiffDictionary.OxygenMass *4,
        }.Sum();
        public Hex2CerSpectrumGenerator()
        {
            spectrumGenerator = new SpectrumPeakGenerator();
        }
        public Hex2CerSpectrumGenerator(ISpectrumPeakGenerator spectrumGenerator)
        {
            this.spectrumGenerator = spectrumGenerator ?? throw new ArgumentNullException(nameof(spectrumGenerator));
        }

        private readonly ISpectrumPeakGenerator spectrumGenerator;

        public bool CanGenerate(ILipid lipid, AdductIon adduct)
        {
            if (lipid.LipidClass == LbmClass.Hex2Cer)
            {
                if (adduct.AdductIonName == "[M+H]+" || adduct.AdductIonName == "[M+Na]+")
                {
                    return true;
                }
            }
            return false;
        }

        public IMSScanProperty Generate(Lipid lipid, AdductIon adduct, IMoleculeProperty molecule = null)
        {
            var spectrum = new List<SpectrumPeak>();
            var nlmass = adduct.AdductIonName == "[M+H]+" ? H2O * 2 + C6H10O5 * 2 : H2O + C6H10O5 * 2;
            spectrum.AddRange(GetHex2CerSpectrum(lipid, adduct));
            if (lipid.Chains is PositionLevelChains plChains)
            {
                if (plChains.Chains[0] is SphingoChain sphingo)
                {
                    spectrum.AddRange(GetSphingoSpectrum(lipid, sphingo, adduct));
                    //spectrum.AddRange(spectrumGenerator.GetSphingoDoubleBondSpectrum(lipid, sphingo, adduct, nlmass, 30d));
                    spectrum.AddRange(spectrumGenerator.GetSphingoDoubleBondSpectrum(lipid, sphingo, adduct, nlmass, 30d)); //-header
                }
                if (plChains.Chains[1] is AcylChain acyl)
                {
                    spectrum.AddRange(GetAcylSpectrum(lipid, acyl, adduct));
                    //spectrum.AddRange(spectrumGenerator.GetAcylDoubleBondSpectrum(lipid, acyl, adduct, nlmass, 30d));
                    spectrum.AddRange(spectrumGenerator.GetAcylDoubleBondSpectrum(lipid, acyl, adduct, nlmass, 30d)); //-header
                }
            }
            spectrum = spectrum.GroupBy(spec => spec, comparer)
                .Select(specs => new SpectrumPeak(specs.First().Mass, specs.Sum(n => n.Intensity), string.Join(", ", specs.Select(spec => spec.Comment)), specs.Aggregate(SpectrumComment.none, (a, b) => a | b.SpectrumComment)))
                .OrderBy(peak => peak.Mass)
                .ToList();
            return CreateReference(lipid, adduct, spectrum, molecule);
        }
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

        private SpectrumPeak[] GetHex2CerSpectrum(ILipid lipid, AdductIon adduct)
        {
            var spectrum = new List<SpectrumPeak>
            {
                new SpectrumPeak(adduct.ConvertToMz(lipid.Mass), 999d, "Precursor") { SpectrumComment = SpectrumComment.precursor },
                        new SpectrumPeak(adduct.ConvertToMz(lipid.Mass) - H2O, 200d, "Precursor-H2O"){ SpectrumComment = SpectrumComment.metaboliteclass },
                        //new SpectrumPeak(adduct.ConvertToMz(lipid.Mass)-C6H10O5, 300d, "Precursor-Hex") { SpectrumComment = SpectrumComment.metaboliteclass },
                        //new SpectrumPeak(adduct.ConvertToMz(lipid.Mass)-C6H10O5-H2O, 200d, "Precursor-Hex-H2O") { SpectrumComment = SpectrumComment.metaboliteclass },
                        //new SpectrumPeak(adduct.ConvertToMz(lipid.Mass)-C6H10O5-H2O*2, 200d, "Precursor-Hex-2H2O") { SpectrumComment = SpectrumComment.metaboliteclass },
                        new SpectrumPeak(adduct.ConvertToMz(lipid.Mass)-C6H10O5*2, 100d, "Precursor-2Hex") { SpectrumComment = SpectrumComment.metaboliteclass },
                        new SpectrumPeak(adduct.ConvertToMz(lipid.Mass)-C6H10O5*2-H2O, 400d, "Precursor-2Hex-H2O") { SpectrumComment = SpectrumComment.metaboliteclass },
                        new SpectrumPeak(adduct.ConvertToMz(lipid.Mass)-C6H10O5*2-H2O*2, 100d, "Precursor-2Hex-2H2O") { SpectrumComment = SpectrumComment.metaboliteclass },
            };
            return spectrum.ToArray();
        }

        private SpectrumPeak[] GetSphingoSpectrum(ILipid lipid, SphingoChain sphingo, AdductIon adduct)
        {
            var chainMass = sphingo.Mass + MassDiffDictionary.HydrogenMass;
            var spectrum = new List<SpectrumPeak>();
            //if (adduct.AdductIonName != "[M+Na]+")
            {
                spectrum.AddRange
                (
                     new[]
                     {
                        new SpectrumPeak(chainMass + MassDiffDictionary.ProtonMass - H2O, 200d, "[sph+H]+ -H2O") { SpectrumComment = SpectrumComment.acylchain },
                        new SpectrumPeak(chainMass + MassDiffDictionary.ProtonMass - H2O*2, 500d, "[sph+H]+ -2H2O") { SpectrumComment = SpectrumComment.acylchain },
                        new SpectrumPeak(chainMass + MassDiffDictionary.ProtonMass - CH4O2, 150d, "[sph+H]+ -CH4O2") { SpectrumComment = SpectrumComment.acylchain },
                     }
                );
            }
            return spectrum.ToArray();
        }

        private SpectrumPeak[] GetAcylSpectrum(ILipid lipid, AcylChain acyl, AdductIon adduct)
        {
            var chainMass = acyl.Mass + MassDiffDictionary.HydrogenMass;
            var spectrum = new List<SpectrumPeak>()
            {
                             new SpectrumPeak(chainMass+ MassDiffDictionary.ProtonMass +C2H3NO - MassDiffDictionary.HydrogenMass -MassDiffDictionary.OxygenMass, 200d, "[FAA+C2H+H]+") { SpectrumComment = SpectrumComment.acylchain },
                             new SpectrumPeak(adduct.ConvertToMz(chainMass) +C2H3NO +MassDiffDictionary.HydrogenMass+ C6H10O5*2, 150d, "[FAA+C2H4O+2Hex+adduct]+") { SpectrumComment = SpectrumComment.acylchain },

            };
            if (adduct.AdductIonName == "[M+H]+")
            {
                spectrum.AddRange
                (
                     new[]
                     {
                             new SpectrumPeak(chainMass+ MassDiffDictionary.ProtonMass +NH, 200d, "[FAA+H]+") { SpectrumComment = SpectrumComment.acylchain },
                             new SpectrumPeak(chainMass+ MassDiffDictionary.ProtonMass +C2H3NO, 150d, "[FAA+C2H2O+H]+") { SpectrumComment = SpectrumComment.acylchain },
                     }
                );
            }
            return spectrum.ToArray();
        }

        private static readonly IEqualityComparer<SpectrumPeak> comparer = new SpectrumEqualityComparer();

    }
}
