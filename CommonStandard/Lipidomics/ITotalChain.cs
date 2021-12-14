﻿using CompMs.Common.FormulaGenerator.DataObj;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace CompMs.Common.Lipidomics
{
    public interface ITotalChain
    {
        int CarbonCount { get; }
        int DoubleBondCount { get; }
        int OxidizedCount { get; }
        int ChainCount { get; }
        double Mass { get; }

        IEnumerable<ITotalChain> GetCandidateSets(IChainGenerator generator);
    }

    public static class TotalChainExtension
    {
        public static ITotalChain GetChains(this LipidMolecule lipid) {
            switch (lipid.AnnotationLevel) {
                case 1:
                    // return new TotalChains(lipid.TotalCarbonCount, lipid.TotalDoubleBondCount, lipid.TotalOxidizedCount, 0, alkylChainCount: 0);
                case 2:
                case 3:
                    // return new MolecularSpeciesLevelChains(new AcylChain);
                default:
                    break;
            }
            return default;
        }
    }

    public class TotalChain : ITotalChain {
        public TotalChain(int carbonCount, int doubleBondCount, int oxidizedCount, int acylChainCount, int alkylChainCount, int sphingoChainCount) {
            CarbonCount = carbonCount;
            DoubleBondCount = doubleBondCount;
            OxidizedCount = oxidizedCount;
            AcylChainCount = acylChainCount;
            AlkylChainCount = alkylChainCount;
            SphingoChainCount = sphingoChainCount;
        }

        public int CarbonCount { get; }
        public int DoubleBondCount { get; }
        public int OxidizedCount { get; }
        public int ChainCount => AcylChainCount + AlkylChainCount + SphingoChainCount;
        public int AcylChainCount { get; }
        public int AlkylChainCount { get; }
        public int SphingoChainCount { get; }

        public double Mass => CalculateSubLevelMass(CarbonCount, DoubleBondCount, OxidizedCount, ChainCount, AcylChainCount, AlkylChainCount, SphingoChainCount);

        private static double CalculateSubLevelMass(int carbon, int doubleBond, int oxidize, int chain, int acyl, int alkyl, int sphingo) {
            var carbonGain = carbon * MassDiffDictionary.CarbonMass;
            var hydrogenGain = (2 * carbon - 2 * doubleBond + chain) * MassDiffDictionary.HydrogenMass;
            var oxygenGain = oxidize * MassDiffDictionary.OxygenMass;
            var acylGain = acyl * AcylGain;
            var alkylGain = alkyl * AlkylGain;
            var sphingoGain = sphingo * SphingoGain;
            var result = carbonGain + hydrogenGain + oxygenGain + acylGain + alkylGain + sphingoGain;
            return result;
        }

        private static readonly double AcylGain = MassDiffDictionary.OxygenMass - 2 * MassDiffDictionary.HydrogenMass;

        private static readonly double AlkylGain = 0d;

        private static readonly double SphingoGain = MassDiffDictionary.NitrogenMass + MassDiffDictionary.HydrogenMass;

        IEnumerable<ITotalChain> ITotalChain.GetCandidateSets(IChainGenerator generator) {
            return generator.Separate(this);
        }

        public override string ToString() {
            return string.Format("{0}:{1}{2}", CarbonCount, DoubleBondCount, OxidizeSymbol(OxidizedCount));
        }

        private static string OxidizeSymbol(int oxidize) {
            if (oxidize == 0) {
                return "";
            }
            if (oxidize == 1) {
                return ";O";
            }
            return $";O{oxidize}";
        }
    }

    public abstract class SeparatedChains
    {
        public SeparatedChains(IChain[] chains) {
            Chains = new ReadOnlyCollection<IChain>(chains);           
        }

        public int CarbonCount => Chains.Sum(c => c.CarbonCount);

        public int DoubleBondCount => Chains.Sum(c => c.DoubleBondCount);

        public int OxidizedCount => Chains.Sum(c => c.OxidizedCount);

        public double Mass => Chains.Sum(c => c.Mass);

        public int ChainCount => Chains.Count;

        public ReadOnlyCollection<IChain> Chains { get; }
    }

    public class MolecularSpeciesLevelChains : SeparatedChains, ITotalChain
    {
        public MolecularSpeciesLevelChains(params IChain[] chains) : base(chains) {

        }

        public IEnumerable<ITotalChain> GetCandidateSets(IChainGenerator generator) {
            return generator.Permutate(this);
        }

        public override string ToString() {
            return string.Join("_", Chains.Select(c => c.ToString()));
        }
    }

    public class PositionLevelChains : SeparatedChains, ITotalChain
    {
        public PositionLevelChains(params IChain[] chains) : base(chains) {

        }

        public IEnumerable<ITotalChain> GetCandidateSets(IChainGenerator generator) {
            return generator.Product(this);
        }

        public override string ToString() {
            return string.Join("/", Chains.Select(c => c.ToString()));
        }
    }
}
